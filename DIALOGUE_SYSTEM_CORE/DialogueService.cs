using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>Status recorded for a dialogue text/event row in the play-session database.</summary>
public enum DialogueRuntimeStatus
{
    Idle,
    TypingText,
    WaitingForInput,
    TakingChoice,
    ChoiceSelected,
    EventEmitted,
    Transitioning,
    Interrupted,
    Resumed,
    Completed
}

public enum DialogueRequestType
{
    LiveSnapshot,
    GetDialogue,
    GetEvents,
    HasEvent,
    WaitForEvent
}

public enum DialogueResponseCode
{
    Ok = 200,
    Pending = 202,
    InvalidRequest = 400,
    NotFound = 404,
    Timeout = 408
}

public enum DialoguePriorityDispatchResult
{
    Continue,
    CullLowerPriorities,
    DeregisterLowerPriorities
}

[Serializable]
public sealed class DialogueScriptRecord
{
    public string DialogueId;
    public string Path;
    public DateTime StartedAtUtc;
    public int PlayCount;
}

[Serializable]
public sealed class DialogueEventRecord
{
    // Composite primary key requested by the DSL service design.
    public string PrimaryKey;       // mm:ss.fff + text name (made collision-safe)
    public long Sequence;
    public string DialogueId;       // FK -> DialogueScriptRecord.DialogueId
    public string Timestamp;        // play-session relative mm:ss.fff
    public string TextName;
    public string Text;
    public string EmittedEvent;     // empty for non-emission status rows
    public DialogueRuntimeStatus Status;
    public string Detail;
}

[Serializable]
public sealed class DialogueLiveSnapshot
{
    public bool IsPlaying;
    public string DialogueId;
    public string DialoguePath;
    public string SectionId;
    public string TextName;
    public string Text;
    public string LastEvent;
    public DialogueRuntimeStatus Status;
    public string Detail;
    public long LatestSequence;

    public string ToMessage()
    {
        return "<dialogue-snapshot>" +
               "<playing>" + IsPlaying + "</playing>" +
               "<dialogue id=\"" + DialogueMessage.Escape(DialogueId) + "\">" + DialogueMessage.Escape(DialoguePath) + "</dialogue>" +
               "<section>" + DialogueMessage.Escape(SectionId) + "</section>" +
               "<text name=\"" + DialogueMessage.Escape(TextName) + "\">" + DialogueMessage.Escape(Text) + "</text>" +
               "<io-status>" + Status + "</io-status>" +
               "<detail>" + DialogueMessage.Escape(Detail) + "</detail>" +
               "<last-event>" + DialogueMessage.Escape(LastEvent) + "</last-event>" +
               "<sequence>" + LatestSequence + "</sequence>" +
               "</dialogue-snapshot>";
    }
}

[Serializable]
public sealed class DialogueRequest
{
    public string RequestId = Guid.NewGuid().ToString("N");
    public string ClientId;
    public DialogueRequestType Type = DialogueRequestType.LiveSnapshot;
    public string DialogueId;
    public string DialoguePath;
    public string EventName;
    public long SinceSequence;
    public float TimeoutSeconds = 10f;

    public static DialogueRequest Snapshot()
    {
        return new DialogueRequest { Type = DialogueRequestType.LiveSnapshot };
    }

    public static DialogueRequest HasEvent(string eventName, long sinceSequence = 0)
    {
        return new DialogueRequest
        {
            Type = DialogueRequestType.HasEvent,
            EventName = eventName,
            SinceSequence = sinceSequence
        };
    }
}

[Serializable]
public sealed class DialogueResponse
{
    public string RequestId;
    public DialogueResponseCode Code;
    public string Message;
    public DialogueLiveSnapshot Snapshot;
    public DialogueScriptRecord Dialogue;
    public List<DialogueEventRecord> Events = new List<DialogueEventRecord>();
    public bool Matched;

    public bool IsSuccess { get { return Code == DialogueResponseCode.Ok; } }
    public bool IsPending { get { return Code == DialogueResponseCode.Pending; } }
    public bool IsFail { get { return Code != DialogueResponseCode.Ok && Code != DialogueResponseCode.Pending; } }
}

/// <summary>
/// In-process client/server contract. It deliberately resembles a tiny HTTP
/// service, but does not use sockets or block Unity's main thread.
/// </summary>
public interface IDialogueService
{
    DialogueResponse Send(DialogueRequest request);
    void SendAsync(DialogueRequest request, Action<DialogueResponse> completed);
    IEnumerator SendBlocking(DialogueRequest request, Action<DialogueResponse> completed);
}

/// <summary>
/// Coalesces asynchronous one-shot requests. Each client keeps only its latest
/// pending request so monitoring traffic cannot flood a single frame.
/// </summary>
public sealed class DialogueQueryServer
{
    sealed class PendingQuerySlot
    {
        public string ClientId;
        public DialogueRequest Request;
        public Action<DialogueResponse> Completed;
        public int MaxDeferrals = -1;
        public int DeferredFrames;
        public int LastDeferredFrame = -1;
    }

    readonly Dictionary<string, PendingQuerySlot> pendingByClient =
        new Dictionary<string, PendingQuerySlot>(StringComparer.Ordinal);
    readonly Queue<string> pendingOrder = new Queue<string>();
    readonly Dictionary<string, int> lastProcessedFrameByClient =
        new Dictionary<string, int>(StringComparer.Ordinal);
    int lastProcessFrame = -1;
    int processedThisFrameCount;

    public int PendingClientCount { get { return pendingByClient.Count; } }

    public void EnqueueLatest(string clientId, DialogueRequest request,
        Action<DialogueResponse> completed, int maxDeferrals = -1)
    {
        string resolvedClientId = string.IsNullOrEmpty(clientId)
            ? Guid.NewGuid().ToString("N") : clientId;
        bool existed = pendingByClient.TryGetValue(resolvedClientId,
            out PendingQuerySlot slot);
        if (!existed || slot == null)
        {
            slot = new PendingQuerySlot();
            pendingByClient[resolvedClientId] = slot;
            pendingOrder.Enqueue(resolvedClientId);
        }

        slot.ClientId = resolvedClientId;
        slot.Request = request;
        slot.Completed = completed;
        slot.MaxDeferrals = maxDeferrals;
        slot.DeferredFrames = 0;
        slot.LastDeferredFrame = -1;
    }

    public bool ContainsPending(string clientId, string requestId)
    {
        return !string.IsNullOrEmpty(clientId) &&
               pendingByClient.TryGetValue(clientId, out PendingQuerySlot slot) &&
               slot != null && slot.Request != null &&
               string.Equals(slot.Request.RequestId, requestId, StringComparison.Ordinal);
    }

    public void Cancel(string clientId)
    {
        if (string.IsNullOrEmpty(clientId)) return;
        pendingByClient.Remove(clientId);
        lastProcessedFrameByClient.Remove(clientId);
    }

    public void Clear()
    {
        pendingByClient.Clear();
        pendingOrder.Clear();
        lastProcessedFrameByClient.Clear();
        lastProcessFrame = -1;
        processedThisFrameCount = 0;
    }

    public int Process(int maxClientsPerFrame, int currentFrame,
        Func<DialogueRequest, DialogueResponse> resolver,
        Action<DialogueRequest, DialogueResponse> onDropped = null)
    {
        if (resolver == null || maxClientsPerFrame <= 0) return 0;
        if (currentFrame != lastProcessFrame)
        {
            lastProcessFrame = currentFrame;
            processedThisFrameCount = 0;
        }

        int processed = 0;
        int availableThisFrame = pendingOrder.Count;
        var processedClientIds = new List<string>();
        while (processedThisFrameCount < maxClientsPerFrame &&
               availableThisFrame-- > 0 &&
               pendingOrder.Count > 0)
        {
            string clientId = pendingOrder.Dequeue();
            if (!pendingByClient.TryGetValue(clientId, out PendingQuerySlot slot) ||
                slot == null || slot.Request == null)
                continue;
            if (lastProcessedFrameByClient.TryGetValue(clientId, out int lastProcessedFrame) &&
                lastProcessedFrame == currentFrame)
            {
                pendingOrder.Enqueue(clientId);
                continue;
            }

            pendingByClient.Remove(clientId);
            DialogueResponse response = resolver(slot.Request);
            slot.Completed?.Invoke(response);
            processedClientIds.Add(clientId);
            lastProcessedFrameByClient[clientId] = currentFrame;
            processedThisFrameCount++;
            processed++;
        }

        if (pendingByClient.Count > 0)
        {
            var toRemove = new List<string>();
            foreach (var pair in pendingByClient)
            {
                PendingQuerySlot slot = pair.Value;
                if (slot == null || slot.MaxDeferrals < 0) continue;
                if (processedClientIds.Contains(pair.Key)) continue;
                if (slot.LastDeferredFrame == currentFrame) continue;

                slot.LastDeferredFrame = currentFrame;
                slot.DeferredFrames++;
                if (slot.DeferredFrames <= slot.MaxDeferrals) continue;

                DialogueResponse fail = BuildRetryLimitFailure(slot.Request,
                    slot.ClientId, slot.MaxDeferrals);
                slot.Completed?.Invoke(fail);
                onDropped?.Invoke(slot.Request, fail);
                toRemove.Add(pair.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
                pendingByClient.Remove(toRemove[i]);
        }

        return processed;
    }

    static DialogueResponse BuildRetryLimitFailure(DialogueRequest request,
        string clientId, int maxDeferrals)
    {
        return new DialogueResponse
        {
            RequestId = request != null ? request.RequestId : "",
            Code = DialogueResponseCode.Timeout,
            Message = "<error>One-shot request for client "" +
                DialogueMessage.Escape(clientId) + "" exceeded the query " +
                "server retry limit of " + maxDeferrals + " deferred frame(s).</error>"
        };
    }
}

/// <summary>
/// Pushes live dialogue snapshots to registered listeners outside the one-shot
/// request queue.
/// </summary>
public sealed class DialogueLiveSnapshotServer
{
    sealed class SnapshotSubscriber
    {
        public int SubscriptionId;
        public string ClientId;
        public string DialoguePathFilter;
        public Action<DialogueLiveSnapshot> Callback;
        public bool OnlyOnChange;
        public float MinIntervalSeconds;
        public long LastDeliveredVersion;
        public float LastSentAtSeconds = float.NegativeInfinity;
    }

    readonly Dictionary<int, SnapshotSubscriber> subscribers =
        new Dictionary<int, SnapshotSubscriber>();
    int nextSubscriptionId = 1;
    long latestVersion;
    DialogueLiveSnapshot latestSnapshot;

    public int Subscribe(string clientId, string dialoguePathFilter,
        Action<DialogueLiveSnapshot> callback, bool onlyOnChange = true,
        float minIntervalSeconds = 0f)
    {
        if (callback == null) return -1;
        int id = nextSubscriptionId++;
        subscribers[id] = new SnapshotSubscriber
        {
            SubscriptionId = id,
            ClientId = clientId ?? "",
            DialoguePathFilter = DialogueMessage.NormalizePath(dialoguePathFilter),
            Callback = callback,
            OnlyOnChange = onlyOnChange,
            MinIntervalSeconds = Math.Max(0f, minIntervalSeconds)
        };
        return id;
    }

    public void Unsubscribe(int subscriptionId)
    {
        subscribers.Remove(subscriptionId);
    }

    public void UnsubscribeClient(string clientId)
    {
        if (string.IsNullOrEmpty(clientId)) return;
        var toRemove = new List<int>();
        foreach (var pair in subscribers)
            if (string.Equals(pair.Value.ClientId, clientId, StringComparison.Ordinal))
                toRemove.Add(pair.Key);
        for (int i = 0; i < toRemove.Count; i++)
            subscribers.Remove(toRemove[i]);
    }

    public void Clear()
    {
        subscribers.Clear();
        latestSnapshot = null;
        latestVersion = 0;
    }

    public void MarkDirty(DialogueLiveSnapshot snapshot)
    {
        latestSnapshot = DialogueMessage.CloneSnapshot(snapshot);
        latestVersion++;
    }

    public int PublishDue(float nowSeconds)
    {
        if (latestVersion <= 0 || latestSnapshot == null || subscribers.Count == 0)
            return 0;

        int delivered = 0;
        var ids = new List<int>(subscribers.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            if (!subscribers.TryGetValue(ids[i], out SnapshotSubscriber sub))
                continue;
            if (!MatchesPathFilter(sub.DialoguePathFilter, latestSnapshot.DialoguePath))
                continue;

            bool hasNewVersion = sub.LastDeliveredVersion != latestVersion;
            bool intervalElapsed =
                nowSeconds - sub.LastSentAtSeconds >= sub.MinIntervalSeconds;
            if (sub.OnlyOnChange)
            {
                if (!hasNewVersion || !intervalElapsed) continue;
            }
            else if (!intervalElapsed)
            {
                continue;
            }

            sub.Callback?.Invoke(DialogueMessage.CloneSnapshot(latestSnapshot));
            sub.LastDeliveredVersion = latestVersion;
            sub.LastSentAtSeconds = nowSeconds;
            delivered++;
        }
        return delivered;
    }

    static bool MatchesPathFilter(string filter, string dialoguePath)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        return string.Equals(filter, DialogueMessage.NormalizePath(dialoguePath),
            StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Pushes live emitted event names to subscribers, similar to OnEmit but with
/// explicit registration and filtering.
/// </summary>
public sealed class DialogueLiveEventServer
{
    sealed class EventSubscriber
    {
        public int SubscriptionId;
        public string ClientId;
        public string DialoguePathFilter;
        public string EventNameFilter;
        public Action<string> Callback;
    }

    readonly Dictionary<int, EventSubscriber> subscribers =
        new Dictionary<int, EventSubscriber>();
    int nextSubscriptionId = 1;

    public int Subscribe(string clientId, string dialoguePathFilter,
        string eventNameFilter, Action<string> callback)
    {
        if (callback == null) return -1;
        int id = nextSubscriptionId++;
        subscribers[id] = new EventSubscriber
        {
            SubscriptionId = id,
            ClientId = clientId ?? "",
            DialoguePathFilter = DialogueMessage.NormalizePath(dialoguePathFilter),
            EventNameFilter = eventNameFilter ?? "",
            Callback = callback
        };
        return id;
    }

    public void Unsubscribe(int subscriptionId)
    {
        subscribers.Remove(subscriptionId);
    }

    public void UnsubscribeClient(string clientId)
    {
        if (string.IsNullOrEmpty(clientId)) return;
        var toRemove = new List<int>();
        foreach (var pair in subscribers)
            if (string.Equals(pair.Value.ClientId, clientId, StringComparison.Ordinal))
                toRemove.Add(pair.Key);
        for (int i = 0; i < toRemove.Count; i++)
            subscribers.Remove(toRemove[i]);
    }

    public void Clear()
    {
        subscribers.Clear();
    }

    public int Publish(string dialoguePath, string eventName)
    {
        if (string.IsNullOrEmpty(eventName) || subscribers.Count == 0) return 0;

        EnsureSorted();
        int delivered = 0;
        int suppressionFloor = int.MinValue;
        string normalizedPath = DialogueMessage.NormalizePath(dialoguePath);
        for (int i = 0; i < sortedSubscribers.Count; i++)
        {
            PrioritySubscriber sub = sortedSubscribers[i];
            if (sub == null) continue;
            if (suppressionFloor != int.MinValue && sub.Priority < suppressionFloor)
                break;
            if (!subscribers.ContainsKey(sub.SubscriptionId)) continue;
            if (!MatchesPathFilter(sub.DialoguePathFilter, normalizedPath))
                continue;
            if (!MatchesEventFilter(sub.EventNameFilter, eventName))
                continue;

            DialoguePriorityDispatchResult result =
                sub.Callback != null
                    ? sub.Callback(eventName)
                    : DialoguePriorityDispatchResult.Continue;
            delivered++;
            if (result == DialoguePriorityDispatchResult.CullLowerPriorities)
            {
                suppressionFloor = sub.Priority;
            }
            else if (result == DialoguePriorityDispatchResult.DeregisterLowerPriorities)
            {
                suppressionFloor = sub.Priority;
                RemoveLowerPriorities(sub.Priority);
            }
        }
        return delivered;
    }

    void RemoveLowerPriorities(int minimumPriorityToKeep)
    {
        var toRemove = new List<int>();
        foreach (var pair in subscribers)
            if (pair.Value.Priority < minimumPriorityToKeep)
                toRemove.Add(pair.Key);
        for (int i = 0; i < toRemove.Count; i++)
            subscribers.Remove(toRemove[i]);
        if (toRemove.Count > 0) sortDirty = true;
    }

    void EnsureSorted()
    {
        if (!sortDirty) return;
        sortedSubscribers.Clear();
        foreach (var pair in subscribers)
            sortedSubscribers.Add(pair.Value);
        sortedSubscribers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        sortDirty = false;
    }

    static bool MatchesPathFilter(string filter, string normalizedPath)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        return string.Equals(filter, normalizedPath,
            StringComparison.OrdinalIgnoreCase);
    }

    static bool MatchesEventFilter(string filter, string eventName)
    {
        return string.IsNullOrEmpty(filter) ||
            string.Equals(filter, eventName, StringComparison.Ordinal);
    }
}

/// <summary>
/// Volatile relational-style store. It is owned by Dialogue_Engine and is never
/// written to disk, so destroying the engine or leaving Play Mode destroys it.
/// </summary>
public sealed class DialogueRuntimeDatabase
{
    readonly DateTime sessionStartedUtc = DateTime.UtcNow;
    readonly Dictionary<string, DialogueScriptRecord> dialogues =
        new Dictionary<string, DialogueScriptRecord>(StringComparer.OrdinalIgnoreCase);
    readonly List<DialogueEventRecord> events = new List<DialogueEventRecord>();
    readonly HashSet<string> primaryKeys = new HashSet<string>(StringComparer.Ordinal);
    long nextSequence;

    public DateTime SessionStartedUtc { get { return sessionStartedUtc; } }
    public long LatestSequence { get { return nextSequence; } }

    public DialogueScriptRecord RegisterDialogue(string path)
    {
        string normalized = string.IsNullOrWhiteSpace(path) ? "<unknown>" : path.Trim();
        string id = normalized.Replace('\\', '/');
        if (!dialogues.TryGetValue(id, out DialogueScriptRecord row))
        {
            row = new DialogueScriptRecord
            {
                DialogueId = id,
                Path = normalized,
                StartedAtUtc = DateTime.UtcNow,
                PlayCount = 0
            };
            dialogues[id] = row;
        }
        row.PlayCount++;
        return row;
    }

    public DialogueEventRecord Record(string dialogueId, string textName, string text,
        DialogueRuntimeStatus status, string emittedEvent = "", string detail = "")
    {
        TimeSpan elapsed = DateTime.UtcNow - sessionStartedUtc;
        string timestamp = string.Format("{0:00}:{1:00}.{2:000}",
            (int)elapsed.TotalMinutes, elapsed.Seconds, elapsed.Milliseconds);
        string safeName = string.IsNullOrEmpty(textName) ? "<none>" : textName;
        string keyBase = timestamp + "+" + safeName;
        string key = keyBase;
        int collision = 1;
        while (!primaryKeys.Add(key)) key = keyBase + "#" + collision++;

        var row = new DialogueEventRecord
        {
            PrimaryKey = key,
            Sequence = ++nextSequence,
            DialogueId = dialogueId ?? "",
            Timestamp = timestamp,
            TextName = safeName,
            Text = text ?? "",
            EmittedEvent = emittedEvent ?? "",
            Status = status,
            Detail = detail ?? ""
        };
        events.Add(row);
        return row;
    }

    public DialogueScriptRecord FindDialogue(string idOrPath)
    {
        if (string.IsNullOrEmpty(idOrPath)) return null;
        dialogues.TryGetValue(idOrPath.Replace('\\', '/'), out DialogueScriptRecord row);
        return row;
    }

    public List<DialogueScriptRecord> GetDialogues()
    {
        return new List<DialogueScriptRecord>(dialogues.Values);
    }

    public List<DialogueEventRecord> QueryEvents(string dialogueId = null,
        string eventName = null, long sinceSequence = 0)
    {
        var result = new List<DialogueEventRecord>();
        foreach (DialogueEventRecord row in events)
        {
            if (row.Sequence <= sinceSequence) continue;
            if (!string.IsNullOrEmpty(dialogueId) &&
                !string.Equals(row.DialogueId, dialogueId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrEmpty(eventName) &&
                !string.Equals(row.EmittedEvent, eventName, StringComparison.Ordinal)) continue;
            result.Add(row);
        }
        return result;
    }
}

public static class DialogueEventMetrics
{
    public static int CountRows(ICollection<DialogueEventRecord> rows)
    {
        return rows != null ? rows.Count : 0;
    }

    public static int CountEmittedEvents(IList<DialogueEventRecord> rows)
    {
        if (rows == null) return 0;
        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            DialogueEventRecord row = rows[i];
            if (row == null) continue;
            if (row.Status == DialogueRuntimeStatus.EventEmitted ||
                !string.IsNullOrEmpty(row.EmittedEvent))
                count++;
        }
        return count;
    }
}

public static class DialogueMessage
{
    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("&", "&amp;").Replace("<", "&lt;")
            .Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    public static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? ""
            : path.Replace('\\', '/').Trim();
    }

    public static DialogueLiveSnapshot CloneSnapshot(DialogueLiveSnapshot snapshot)
    {
        if (snapshot == null) return null;
        return new DialogueLiveSnapshot
        {
            IsPlaying = snapshot.IsPlaying,
            DialogueId = snapshot.DialogueId,
            DialoguePath = snapshot.DialoguePath,
            SectionId = snapshot.SectionId,
            TextName = snapshot.TextName,
            Text = snapshot.Text,
            LastEvent = snapshot.LastEvent,
            Status = snapshot.Status,
            Detail = snapshot.Detail,
            LatestSequence = snapshot.LatestSequence
        };
    }
}
