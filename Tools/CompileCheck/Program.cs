// Semantic tests for the Unity-independent core:
//   1. Compiler — @EMIT / EventToken in all three positions, var resolution,
//      leaf tokens, error cases.
//   2. DialogueDatabase — DSL table, event table, PK timestamps, statuses,
//      filters, reset.
//   3. DialogueService — HTML-like message parsing, the request/response
//      bus, DialogueClient (blocking & non-blocking flows).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static class Tests
{
    static int failures = 0;
    static int passes = 0;

    static void Check(bool cond, string what)
    {
        if (cond)
        {
            passes++;
            Console.WriteLine("  PASS  " + what);
        }
        else
        {
            failures++;
            Console.WriteLine("  FAIL  " + what);
        }
    }

    static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine("── " + title + " ────────────────────────────────");
    }

    static string WriteTemp(string name, string content)
    {
        string path = Path.Combine(Path.GetTempPath(), "dlg_" + name + ".txt");
        File.WriteAllText(path, content);
        return path;
    }

    static EventToken FindEvent(SyntaxToken parent, string eventName)
    {
        if (parent == null || parent.Children == null) return null;
        foreach (var c in parent.Children)
        {
            if (c is EventToken et && et.EventName == eventName)
                return et;
        }
        return null;
    }

    static EventToken FindOptionEmit(SyntaxToken parent, string eventName)
    {
        if (parent == null || parent.Children == null) return null;
        foreach (var c in parent.Children)
        {
            if (c is OptionToken ot && ot.Emit != null && ot.Emit.EventName == eventName)
                return ot.Emit;
        }
        return null;
    }

    static int Main()
    {
        UnityEngine.Debug.Verbose = false;

        TestCompiler();
        TestDatabase();
        TestService();

        Console.WriteLine();
        Console.WriteLine("════════════════════════════════════════════════");
        Console.WriteLine($" RESULT: {passes} passed, {failures} failed");
        Console.WriteLine("════════════════════════════════════════════════");
        return failures == 0 ? 0 : 1;
    }

    // ════════════════════════════════════════════════════════════════════════
    static void TestCompiler()
    {
        Section("COMPILER — @EMIT / EventToken");

        string script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "emit_demo.txt"));

        var file = new File_S(script);
        var graph = Compiler_S.Compile(file);
        file.Close();

        Check(graph != null, "emit_demo.txt compiles");
        if (graph == null) return;

        Check(graph.EntryNode != null && graph.EntryNode.SectionID == "meet",
            "entry section is 'meet'");
        Check(graph.WarningCount == 0, "compiled with 0 warnings");

        // ── meet: character + standalone emit + nested shop ──
        var meet = graph.AdjacencyList["meet"];
        Check(meet.Children.Count == 3, "meet has 3 children (char, emit, section)");

        Check(meet.Children[0] is CharacterToken, "meet[0] is a character line");

        var meetEmit = meet.Children[1] as EventToken;
        Check(meetEmit != null && meetEmit.EventName == "player_met_aria",
            "standalone @EMIT in section -> EventToken \"player_met_aria\"");
        Check(meetEmit != null && meetEmit.Children == null,
            "EventToken has NO children (leaf)");

        Check(meet.Children[2] is SectionToken st && st.SectionID == "shop",
            "meet[2] is the nested 'shop' section");

        // ── shop: character + choice with emit + option emits ──
        var shop = graph.AdjacencyList["shop"];
        var choice = shop.Children[1] as ChoiceToken;
        Check(choice != null, "shop contains a choice block");

        var choiceEmit = choice.Children[0] as EventToken;
        Check(choiceEmit != null && choiceEmit.EventName == "shop_choice_offered",
            "@EMIT inside CHOICE -> EventToken \"shop_choice_offered\"");
        Check(choiceEmit != null && choiceEmit.Children == null,
            "choice EventToken has no children");

        var opt0 = choice.Children[1] as OptionToken;
        var opt1 = choice.Children[2] as OptionToken;
        Check(opt0 != null && opt0.TargetSectionID == "buy",
            "OPTION_0 still resolves its GOTO (buy)");
        Check(opt0 != null && opt0.Emit != null && opt0.Emit.EventName == "lantern_bought",
            "OPTION_0 trailing @EMIT -> EventToken \"lantern_bought\"");
        Check(opt0 != null && opt0.EmitText == "lantern_bought",
            "OptionToken.EmitText convenience property");
        Check(opt1 != null && opt1.Emit == null && opt1.EmitText == "",
            "OPTION_1 has no emit");

        // ── buy: var-resolved event name ──
        var buy = graph.AdjacencyList["buy"];
        var buyEmit = buy.Children[1] as EventToken;
        Check(buyEmit != null && buyEmit.EventName == "purchase_complete",
            "@EMIT purchase_event; resolves the var -> \"purchase_complete\" (string)");

        // ── error cases ──
        string bad1 =
            "START\nSECTION a\n[A]: \"hi\";\n@EMIT;\nEND_SECTION\nEND\n";
        var g1 = Compiler_S.Compile(new File_S(WriteTemp("bad1", bad1)));
        Check(g1 == null, "@EMIT without a name is a compile error");

        string bad2 =
            "START\nSECTION a\n[A]: \"hi\";\n@EMIT \"ok\";\nEND_SECTION\n";
        var g2 = Compiler_S.Compile(new File_S(WriteTemp("bad2", bad2)));
        Check(g2 == null, "script without END still fails");

        // no-semicolon @EMIT is accepted (standalone token)
        string ok1 =
            "START\nSECTION a\n[A]: \"hi\";\n@EMIT my_event\nEND_SECTION\nEND\n";
        var g3 = Compiler_S.Compile(new File_S(WriteTemp("ok1", ok1)));
        Check(g3 != null, "@EMIT without trailing ';' compiles");
        if (g3 != null)
        {
            var a = g3.AdjacencyList["a"];
            Check(FindEvent(a, "my_event") != null, "event name parsed unquoted");
        }

        // quoted event name with spaces
        string ok2 =
            "START\nSECTION a\n[A]: \"hi\";\n@EMIT \"player met aria\";\nEND_SECTION\nEND\n";
        var g4 = Compiler_S.Compile(new File_S(WriteTemp("ok2", ok2)));
        Check(g4 != null, "quoted @EMIT compiles");
        if (g4 != null)
        {
            Check(FindEvent(g4.AdjacencyList["a"], "player met aria") != null,
                "quoted event name with spaces kept as-is");
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    static void TestDatabase()
    {
        Section("DATABASE — DSL table / event table");

        UnityEngine.Time.time = 0f;
        DialogueDatabase.Reset();

        var rec = DialogueDatabase.RegisterDsl("scripts/a.txt");
        Check(rec.PlayCount == 1, "first play registers the DSL");
        DialogueDatabase.RegisterDsl("scripts/a.txt");
        Check(DialogueDatabase.DslCount == 1, "DSL table holds unique texts only");
        var rec2 = DialogueDatabase.RegisterDsl("scripts/b.txt");
        Check(DialogueDatabase.DslCount == 2 && rec2.PlayCount == 1, "second DSL registered");

        // event rows
        UnityEngine.Time.time = 65f;                       // session 1:05
        var emitRow = DialogueDatabase.Record(
            "scripts/a.txt", "shop_entered", DialogueStatusCode.EventEmitted);

        Check(emitRow.Timestamp == "01:05", "timestamp is minute:second (01:05)");
        Check(emitRow.EmittedEvent == "shop_entered", "emitted event stored as string");
        Check(emitRow.StatusCode == "EventEmitted", "status code stored");

        UnityEngine.Time.time = 66f;
        DialogueDatabase.Record("scripts/a.txt", "", DialogueStatusCode.WaitingForInput);
        UnityEngine.Time.time = 67f;
        DialogueDatabase.Record("scripts/a.txt", "", DialogueStatusCode.TakingChoice);

        Check(DialogueDatabase.EventCount == 3, "event table has 3 rows");

        var evs = DialogueDatabase.GetEvents("scripts/a.txt", "shop_entered");
        Check(evs.Count == 1 && evs[0].EmittedEvent == "shop_entered",
            "filter by text + event");

        var noEmit = DialogueDatabase.GetEvents("scripts/a.txt");
        Check(noEmit.Count == 3 &&
              noEmit.Any(r => r.EmittedEvent == ""),
            "rows with empty event string exist (no emission)");

        Check(DialogueDatabase.HasEvent("shop_entered", 0f), "HasEvent finds the event");
        Check(!DialogueDatabase.HasEvent("other_event", 0f), "HasEvent rejects unknown");
        Check(!DialogueDatabase.HasEvent("shop_entered", 66f),
            "HasEvent with since=66s excludes the 65s emission");

        var latest = DialogueDatabase.GetLatest("scripts/a.txt");
        Check(latest != null && latest.StatusCode == "TakingChoice",
            "latest row status of the text");

        // 1 text -> many rows; each row belongs to exactly one text
        var allA = DialogueDatabase.GetEvents("scripts/a.txt");
        Check(allA.Count == 3 && allA.All(r => r.TextName == "scripts/a.txt"),
            "1 text DSL -> many rows, all with that text name");

        // reset wipes everything
        DialogueDatabase.Reset();
        Check(DialogueDatabase.EventCount == 0 && DialogueDatabase.DslCount == 0,
            "Reset() wipes both tables (database dies with the play session)");
    }

    // ════════════════════════════════════════════════════════════════════════
    static void TestService()
    {
        Section("SERVICE — HTML-like messages / bus / client");

        // ── escape roundtrip ──
        string tricky = "a<b>\"c\"&d";
        Check(XmlUtil.Unescape(XmlUtil.Escape(tricky)) == tricky,
            "XmlUtil escape/unescape roundtrip");
        Check(XmlUtil.Escape("<") == "&lt;", "angle brackets escaped");

        // ── message parsing ──
        var msg = ServiceMessage.Parse(
            "<request type=\"wait\" blocking=\"true\" event=\"shop_entered\" timeout=\"5\"></request>");
        Check(msg.Tag == "request", "root tag parsed");
        Check(msg.Attr("type") == "wait", "type attribute");
        Check(msg.Attr("blocking") == "true", "blocking attribute");
        Check(msg.Attr("event") == "shop_entered", "event attribute");
        Check(msg.Attr("timeout") == "5", "timeout attribute");
        Check(msg.Body == "", "body empty");

        var msg2 = ServiceMessage.Parse("<request type=\"query\" text=\"a &amp; b\" />");
        Check(msg2.Attr("text") == "a & b", "attribute values unescaped");

        var msg3 = ServiceMessage.Parse("<response status=\"200\"><summary>hi there</summary></response>");
        Check(msg3.Tag == "response" && msg3.Body == "hi there", "body parsed");

        // ── bus: client -> server ──
        var client = new DialogueClient("test_client");

        client.RequestSnapshot();
        Check(DialogueService.PendingRequests == 1, "request queued for the server");

        ServiceRequest req;
        Check(DialogueService.TryDequeueRequest(out req), "server dequeues the request");
        Check(req.Type == "snapshot", "request type parsed");
        Check(req.ClientId == "test_client", "client id carried");
        Check(req.Blocking == false, "snapshot is non-blocking");

        DialogueService.SendRequest(
            "c2", "<request type=\"wait\" event=\"e1\" blocking=\"true\" timeout=\"2.5\"></request>");
        ServiceRequest req2;
        Check(DialogueService.TryDequeueRequest(out req2), "second request dequeued");
        Check(req2.Type == "wait" && req2.Blocking && req2.Event == "e1",
            "blocking wait parsed");
        Check(Math.Abs(req2.Timeout - 2.5f) < 0.0001f, "float timeout parsed (2.5)");

        DialogueService.SendRequest("c3", "<request type=\"bogus\"></request>");
        ServiceRequest req3;
        Check(DialogueService.TryDequeueRequest(out req3) && req3.Type == "bogus",
            "unknown type passes through (server answers 400)");

        // ── bus: server -> client ──
        string viaEvent = null;
        client.OnResponse += r => viaEvent = r;

        DialogueService.Deliver("test_client", "<response type=\"snapshot\" status=\"200\" />");
        Check(client.TryGetResponse(out string resp) && resp.Contains("status=\"200\""),
            "client pops the response");
        Check(viaEvent != null && viaEvent.Contains("200"), "OnResponse event routed");
        Check(!client.TryGetResponse(out string _), "inbox empty after pop");

        // ── non-blocking wait check (loop pattern) ──
        string checkResp = null;
        Check(!client.CheckForEvent("never_happens", out checkResp),
            "CheckForEvent false before the server answers (204 path)");

        // the server (simulated) answers 204 first, then 200
        DialogueService.Deliver("test_client",
            ServiceMessages.Fail(new ServiceRequest { ClientId = "test_client", Type = "wait" }, "204", "not yet"));
        Check(client.CheckForEvent("never_happens", out string r204),
            "CheckForEvent pops the 204 'not yet' answer");
        Check(r204 != null && r204.Contains("204"), "204 response content");

        DialogueService.Deliver("test_client",
            "<response type=\"wait\" status=\"200\" matched=\"true\" event=\"never_happens\" />");
        Check(client.CheckForEvent("never_happens", out string r200),
            "CheckForEvent true once the answer arrived");
        Check(r200 != null && r200.Contains("matched=\"true\""), "200 response content");

        // ── message wait request building ──
        string waitReq = client.BuildWaitRequest("shop_entered", null, true, 10f);
        Check(waitReq.Contains("type=\"wait\"") && waitReq.Contains("event=\"shop_entered\"")
              && waitReq.Contains("blocking=\"true\"") && waitReq.Contains("timeout=\"10\""),
            "BuildWaitRequest (event, blocking)");

        string msgReq = client.BuildWaitRequest(null, "the line", false, 0f);
        Check(msgReq.Contains("text=\"the line\"") && msgReq.Contains("blocking=\"false\""),
            "BuildWaitRequest (message, non-blocking)");

        string queryReq = client.BuildQueryRequest("events", "text", "scripts/a.txt", "event", "e&v");
        Check(queryReq.Contains("command=\"events\"") && queryReq.Contains("text=\"scripts/a.txt\"")
              && queryReq.Contains("event=\"e&amp;v\""),
            "BuildQueryRequest attributes escaped");

        // ── engine going away: pending requests fail ──
        client.ClearInbox();
        DialogueService.DiscardPending();
        Check(DialogueService.PendingRequests == 0, "DiscardPending empties the inbox");
        Check(client.TryGetResponse(out string failResp) && failResp.Contains("503"),
            "discarded requests answered 503 (engine gone)");

        client.Unregister();
    }
}
