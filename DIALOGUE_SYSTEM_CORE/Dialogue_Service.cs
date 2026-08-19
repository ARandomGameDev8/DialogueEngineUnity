using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// ══════════════════════════════════════════════════════════════════════════════
// DIALOGUE SERVICE — the Service-Client bridge (HTTP-like, in-process IPC)
// ══════════════════════════════════════════════════════════════════════════════
//
// The Dialogue_Engine is the SERVER. Any other system — plain code
// (Dialogue_Engine.Play / your own scripts) or a behaviour-tree action node —
// is a CLIENT. The client talks to the engine by sending small HTML-like
// request messages and receiving HTML-like response messages. No sockets, no
// OS involvement: one in-process message bus, drained by the engine in
// Update (exactly how an HTTP server would answer a request).
//
// REQUESTS (client -> server), sent as one HTML-like message:
//
//   <request type="snapshot" client="my_bt_node"></request>
//       A live snapshot: where the user is inside the text, the IO status,
//       the current dialogue / choice, the last emitted event, recent rows.
//       Non-blocking — the server answers on its next Update.
//
//   <request type="query" command="events" text="scripts/a.txt" event="name"></request>
//       A simple query the server executes against its internal database:
//         command="events"    (optional text= / event= filters)
//         command="status"    (optional text=)   latest status of a text
//         command="dsl"                         the DSL table (unique texts)
//         command="texts"     (optional text=)  the event-table rows
//         command="position"                      where the user is in the text now
//         command="history"                       the recent spoken lines
//
//   <request type="wait" event="name" blocking="true" timeout="5"></request>
//   <request type="wait" text="the message to watch for" blocking="false"></request>
//       Wait until an event is emitted, or until a message is displayed.
//         blocking="false"  -> ONE check per request (for loops):
//                              200 when it got what it wanted,
//                              204 when it did not (call it again next frame).
//         blocking="true"   -> the SERVER keeps requesting a live snapshot of
//                              its own state every frame until the answer
//                              arrives. The repeat is conditional — it stops
//                              when the answer is in, when timeout=".."
//                              seconds pass, or when the dialogue closes:
//                              200 / 408 (timed out) / 503 (dialogue closed).
//
// RESPONSES (server -> client):
//
//   <response type="snapshot" status="200" io="waiting_for_input" ...>...</response>
//
//   Status codes are HTTP-flavoured:
//     200 got it · 204 not yet · 400 bad request · 404 unknown text
//     408 timed out · 503 dialogue closed / engine gone
// ══════════════════════════════════════════════════════════════════════════════

// ── Tiny XML-escape helpers (messages are HTML-like) ─────────────────────────
public static class XmlUtil
{
    public static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        var sb = new StringBuilder(s.Length);

        foreach (char c in s)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                default:  sb.Append(c); break;
            }
        }

        return sb.ToString();
    }

    public static string Unescape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&amp;", "&");
    }
}

// ── One parsed HTML-like message: a root tag, attribute pairs, an optional body ──
public class ServiceMessage
{
    public string Tag = "";
    public string Body = "";

    readonly Dictionary<string, string> attributes
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string Attr(string name, string fallback = null)
    {
        string v;
        return attributes.TryGetValue(name, out v) ? v : fallback;
    }

    public bool HasAttr(string name)
    {
        return attributes.ContainsKey(name);
    }

    /// <summary>
    /// Parses an HTML-like message: one root tag, attr="value" pairs, an
    /// optional body (cut off at the matching closing tag when present).
    /// </summary>
    public static ServiceMessage Parse(string raw)
    {
        var msg = new ServiceMessage();

        if (string.IsNullOrWhiteSpace(raw))
            return msg;

        int open = raw.IndexOf('<');
        if (open == -1)
            return msg;

        int tagEnd = raw.IndexOf('>', open);
        if (tagEnd == -1)
            return msg;

        string head = raw.Substring(open + 1, tagEnd - open - 1);

        // tag name
        int nameEnd = 0;
        while (nameEnd < head.Length && !char.IsWhiteSpace(head[nameEnd]) && head[nameEnd] != '/')
            nameEnd++;

        msg.Tag = head.Substring(0, nameEnd);

        // attribute pairs: name="value"
        int i = nameEnd;

        while (i < head.Length)
        {
            while (i < head.Length && char.IsWhiteSpace(head[i])) i++;
            if (i >= head.Length) break;

            int nameStart = i;
            while (i < head.Length && head[i] != '=' && !char.IsWhiteSpace(head[i])) i++;
            string name = head.Substring(nameStart, i - nameStart);

            while (i < head.Length && char.IsWhiteSpace(head[i])) i++;

            string value = "";

            if (i < head.Length && head[i] == '=')
            {
                i++;
                while (i < head.Length && char.IsWhiteSpace(head[i])) i++;

                if (i < head.Length && head[i] == '"')
                {
                    i++;
                    int valStart = i;
                    while (i < head.Length && head[i] != '"') i++;
                    value = head.Substring(valStart, i - valStart);
                    if (i < head.Length) i++;   // closing quote
                }
                else
                {
                    int valStart = i;
                    while (i < head.Length && !char.IsWhiteSpace(head[i])) i++;
                    value = head.Substring(valStart, i - valStart);
                }
            }

            if (name.Length > 0)
                msg.attributes[name] = XmlUtil.Unescape(value);
        }

        // body (up to the matching closing tag, if the message has one)
        int bodyStart = tagEnd + 1;
        int closeIdx = raw.IndexOf("</" + msg.Tag, bodyStart, StringComparison.OrdinalIgnoreCase);

        if (closeIdx != -1)
            msg.Body = raw.Substring(bodyStart, closeIdx - bodyStart).Trim();
        else
            msg.Body = raw.Substring(bodyStart).Trim();

        return msg;
    }
}

// ── A request as the server sees it (parsed from the HTML-like message) ──────
public class ServiceRequest
{
    public int    Id;
    public string ClientId;
    public string Raw;              // the original message
    public string Type;             // snapshot | query | wait
    public string Command;          // query: events | status | dsl | texts | position | history
    public string Event;            // wait: event to wait for / query: event filter
    public string Text;             // query: text-DSL filter / wait: message to wait for
    public bool   Blocking;         // wait: keep polling until answered
    public float  Timeout = 10f;    // wait: seconds before the server gives up

    // stamped by the server when it registers the request
    public float RegisteredAt;      // session clock seconds
    public int   HistoryCountAtRegistration;
}

// ── Response builders ────────────────────────────────────────────────────────
public static class ServiceMessages
{
    public static string Response(string type, string status, string extraAttrs, string body)
    {
        return "<response type=\"" + XmlUtil.Escape(type ?? "") + "\" status=\"" + status + "\""
             + (string.IsNullOrEmpty(extraAttrs) ? "" : extraAttrs)
             + ">" + body + "</response>";
    }

    /// <summary>A negative/terminal answer to a request (204 / 400 / 404 / 408 / 503 …).</summary>
    public static string Fail(ServiceRequest req, string status, string reason)
    {
        string type = string.IsNullOrEmpty(req.Type) ? "unknown" : req.Type;
        string client = req.ClientId ?? "";

        return Response(type, status,
            " client=\"" + XmlUtil.Escape(client) + "\" matched=\"false\" reason=\"" + XmlUtil.Escape(reason) + "\"",
            "");
    }
}

// ── The in-process message bus (the "socket" that is not a socket) ───────────
public static class DialogueService
{
    static readonly Queue<ServiceRequest> inbox
        = new Queue<ServiceRequest>();

    static readonly Dictionary<string, Queue<string>> outboxes
        = new Dictionary<string, Queue<string>>();

    static int nextRequestId;

    /// <summary>Raised for every delivered response: (clientId, responseHtml).</summary>
    public static event Action<string, string> OnResponse;

    /// <summary>
    /// Client -> server. Parses the HTML-like request message and queues it;
    /// the engine (the server) picks it up in its Update.
    /// </summary>
    public static int SendRequest(string clientId, string requestHtml)
    {
        var msg = ServiceMessage.Parse(requestHtml);

        float timeout = 10f;
        float parsed;
        string timeoutAttr = msg.Attr("timeout");
        if (!string.IsNullOrEmpty(timeoutAttr) &&
            float.TryParse(timeoutAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            timeout = parsed;
        }

        var req = new ServiceRequest
        {
            Id = ++nextRequestId,
            ClientId = clientId,
            Raw = requestHtml,
            Type = (msg.Attr("type") ?? "").ToLowerInvariant(),
            Command = msg.Attr("command"),
            Event = msg.Attr("event"),
            Text = msg.Attr("text"),
            Blocking = string.Equals(msg.Attr("blocking", "false"), "true", StringComparison.OrdinalIgnoreCase),
            Timeout = timeout
        };

        inbox.Enqueue(req);
        return req.Id;
    }

    /// <summary>Server side: dequeue one pending request (the engine does this in Update).</summary>
    public static bool TryDequeueRequest(out ServiceRequest request)
    {
        if (inbox.Count > 0)
        {
            request = inbox.Dequeue();
            return true;
        }

        request = null;
        return false;
    }

    /// <summary>Server -> client: deliver a response into the client's outbox and raise the event.</summary>
    public static void Deliver(string clientId, string responseHtml)
    {
        if (clientId == null) clientId = "unknown";

        Queue<string> box;

        if (!outboxes.TryGetValue(clientId, out box))
        {
            box = new Queue<string>();
            outboxes[clientId] = box;
        }

        box.Enqueue(responseHtml);
        OnResponse?.Invoke(clientId, responseHtml);
    }

    /// <summary>Client side: pop one response if any has arrived.</summary>
    public static bool TryGetResponse(string clientId, out string response)
    {
        response = null;

        Queue<string> box;
        if (clientId == null || !outboxes.TryGetValue(clientId, out box))
            return false;

        if (box.Count == 0)
            return false;

        response = box.Dequeue();
        return true;
    }

    public static int PendingRequests { get { return inbox.Count; } }

    /// <summary>The engine is going away: fail every still-queued request (503).</summary>
    public static void DiscardPending()
    {
        while (inbox.Count > 0)
        {
            var req = inbox.Dequeue();
            Deliver(req.ClientId, ServiceMessages.Fail(req, "503", "engine gone"));
        }
    }
}

// ── The client (what plain code or a behaviour-tree action node holds) ───────
/// <summary>
/// One client connection to the Dialogue_Engine server.
///
/// Code integration:
///   var client = new DialogueClient("my_script");
///   client.RequestSnapshot();                    // non-blocking, read with TryGetResponse
///   client.WaitForEvent("shop_entered", 10f, response => { ... });
///
/// Behaviour-tree integration: an action node holds one DialogueClient and
/// either polls CheckForEvent every tick (non-blocking, loop-friendly) or
/// drives WaitForEventCoroutine until the node reports finished.
/// </summary>
public class DialogueClient
{
    public string ClientId { get; private set; }

    static readonly Dictionary<string, DialogueClient> registry
        = new Dictionary<string, DialogueClient>();

    static int autoId;

    /// <summary>Raised when a response for this client arrives.</summary>
    public event Action<string> OnResponse;

    public DialogueClient(string clientId = null)
    {
        ClientId = string.IsNullOrEmpty(clientId)
            ? "client_" + (++autoId).ToString(CultureInfo.InvariantCulture)
            : clientId;

        registry[ClientId] = this;
        DialogueService.OnResponse += Route;
    }

    void Route(string clientId, string response)
    {
        if (clientId == ClientId)
            OnResponse?.Invoke(response);
    }

    /// <summary>Releases the client (call from OnDestroy of a node, when done).</summary>
    public void Unregister()
    {
        DialogueService.OnResponse -= Route;
        registry.Remove(ClientId);
    }

    // ── low level ────────────────────────────────────────────────────────────

    /// <summary>Sends an HTML-like request message; returns the request id.</summary>
    public int Send(string requestHtml)
    {
        return DialogueService.SendRequest(ClientId, requestHtml);
    }

    /// <summary>Non-blocking: true (with the response) when one has arrived.</summary>
    public bool TryGetResponse(out string response)
    {
        return DialogueService.TryGetResponse(ClientId, out response);
    }

    /// <summary>Drops all unread responses.</summary>
    public void ClearInbox()
    {
        string dummy;
        while (DialogueService.TryGetResponse(ClientId, out dummy)) { }
    }

    // ── request builders (HTML-like messages) ───────────────────────────────

    public string BuildSnapshotRequest()
    {
        return "<request type=\"snapshot\" client=\"" + XmlUtil.Escape(ClientId) + "\"></request>";
    }

    /// <summary>attributes = "name", "value", "name", "value", …</summary>
    public string BuildQueryRequest(string command, params string[] attributes)
    {
        var sb = new StringBuilder();
        sb.Append("<request type=\"query\" command=\"").Append(XmlUtil.Escape(command ?? ""));

        if (attributes != null)
        {
            for (int i = 0; i + 1 < attributes.Length; i += 2)
            {
                sb.Append(' ')
                  .Append(XmlUtil.Escape(attributes[i]))
                  .Append("=\"")
                  .Append(XmlUtil.Escape(attributes[i + 1] ?? ""))
                  .Append('"');
            }
        }

        sb.Append("</request>");
        return sb.ToString();
    }

    public string BuildWaitRequest(string eventName, string messageText, bool blocking, float timeout)
    {
        var sb = new StringBuilder();
        sb.Append("<request type=\"wait\" blocking=\"").Append(blocking ? "true" : "false");

        if (!string.IsNullOrEmpty(eventName))
            sb.Append("\" event=\"").Append(XmlUtil.Escape(eventName));

        if (!string.IsNullOrEmpty(messageText))
            sb.Append("\" text=\"").Append(XmlUtil.Escape(messageText));

        sb.Append("\" timeout=\"")
          .Append(timeout.ToString("0.##", CultureInfo.InvariantCulture))
          .Append("\"></request>");

        return sb.ToString();
    }

    // ── convenience: non-blocking (meaningful when called in a loop) ────────

    /// <summary>Requests a live snapshot. The response arrives within a frame; read it with TryGetResponse.</summary>
    public void RequestSnapshot()
    {
        Send(BuildSnapshotRequest());
    }

    public void RequestQuery(string command, params string[] attributes)
    {
        Send(BuildQueryRequest(command, attributes));
    }

    /// <summary>
    /// Non-blocking event check, for loops: sends one check and reports
    /// whether the answer has already arrived. Call it again next frame —
    /// it returns true (with the response) once the event was seen.
    /// </summary>
    public bool CheckForEvent(string eventName, out string response)
    {
        Send(BuildWaitRequest(eventName, null, false, 0f));
        return DialogueService.TryGetResponse(ClientId, out response);
    }

    /// <summary>Non-blocking message check, for loops (same shape as CheckForEvent).</summary>
    public bool CheckForMessage(string messageText, out string response)
    {
        Send(BuildWaitRequest(null, messageText, false, 0f));
        return DialogueService.TryGetResponse(ClientId, out response);
    }

    // ── convenience: blocking (the SERVER keeps polling until answered) ─────

    /// <summary>
    /// Blocking wait: the engine keeps polling for the event every frame —
    /// conditionally: until it matches, the timeout passes, or the dialogue
    /// closes — and delivers exactly one response (200 / 408 / 503).
    /// onAnswer fires with that response; pass null to just send the request.
    /// </summary>
    public void WaitForEvent(string eventName, float timeout, Action<string> onAnswer)
    {
        if (onAnswer != null)
        {
            Action<string, string> handler = null;
            handler = (clientId, response) =>
            {
                if (clientId != ClientId) return;
                DialogueService.OnResponse -= handler;
                onAnswer(response);
            };
            DialogueService.OnResponse += handler;
        }

        Send(BuildWaitRequest(eventName, null, true, timeout));
    }

    /// <summary>Blocking wait for a message (dialogue line) to be displayed.</summary>
    public void WaitForMessage(string messageText, float timeout, Action<string> onAnswer)
    {
        if (onAnswer != null)
        {
            Action<string, string> handler = null;
            handler = (clientId, response) =>
            {
                if (clientId != ClientId) return;
                DialogueService.OnResponse -= handler;
                onAnswer(response);
            };
            DialogueService.OnResponse += handler;
        }

        Send(BuildWaitRequest(null, messageText, true, timeout));
    }

    /// <summary>
    /// Coroutine form for behaviour-tree action nodes: sends the request and
    /// yields until the server answers (bounded by the server-side timeout,
    /// plus a safety cap). Responses of other request types that arrive in
    /// between are re-routed to the client's OnResponse event instead of
    /// being swallowed.
    /// </summary>
    public IEnumerator WaitFor(string requestHtml, Action<string> onAnswer = null)
    {
        Send(requestHtml);

        float cap = 60f;
        float t0 = Time.unscaledTime;

        while (Time.unscaledTime - t0 < cap)
        {
            string response;
            if (DialogueService.TryGetResponse(ClientId, out response))
            {
                bool isWaitAnswer = response.IndexOf(
                    "type=\"wait\"", StringComparison.Ordinal) != -1;

                if (isWaitAnswer)
                {
                    if (onAnswer != null) onAnswer(response);
                    yield break;
                }

                // Not our answer — hand it back to whoever listens.
                OnResponse?.Invoke(response);
            }
            yield return null;
        }
    }

    public IEnumerator WaitForEventCoroutine(string eventName, float timeout, Action<string> onAnswer = null)
    {
        yield return WaitFor(BuildWaitRequest(eventName, null, true, timeout), onAnswer);
    }

    public IEnumerator WaitForMessageCoroutine(string messageText, float timeout, Action<string> onAnswer = null)
    {
        yield return WaitFor(BuildWaitRequest(null, messageText, true, timeout), onAnswer);
    }
}
