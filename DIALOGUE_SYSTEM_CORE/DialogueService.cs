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

public static class DialogueMessage
{
    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("&", "&amp;").Replace("<", "&lt;")
            .Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
