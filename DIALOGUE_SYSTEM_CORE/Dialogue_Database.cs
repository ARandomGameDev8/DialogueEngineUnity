using System;
using System.Collections.Generic;
using UnityEngine;

// ══════════════════════════════════════════════════════════════════════════════
// DIALOGUE DATABASE — the engine's internal mini data base
// ══════════════════════════════════════════════════════════════════════════════
//
// Two in-memory tables, alive for ONE play session only:
//
//   ┌ DSL TABLE ──────────────────────────────────────────────────────────────┐
//   │ one row per unique TEXT DSL (script) that has been played               │
//   │ key: text name (the path passed to Dialogue_Engine.Play)                │
//   └─────────────────────────────────────────────────────────────────────────┘
//
//        1 text DSL  ──<  many (text + timestamp) rows in the EVENT TABLE
//
//   ┌ EVENT TABLE ────────────────────────────────────────────────────────────┐
//   │ PK : timestamp (minute:second of the play session) + text name          │
//   │    -> emitted event     (can be an EMPTY STRING when nothing emitted)   │
//   │    -> status code of text (Idle / WaitingForInput / TakingChoice /      │
//   │                          EventEmitted)                                  │
//   └─────────────────────────────────────────────────────────────────────────┘
//
//   (one text DSL has many event rows; one event row always belongs to
//    exactly one text)
//
// The database dies with the play session: it is wiped when we stop playing
// or exit the play scene (see Dialogue_Engine: Awake / OnDestroy /
// OnApplicationQuit / editor play-mode hook). It is NOT wiped between
// Dialogue_Engine.Play() calls — that is the whole point: several scripts in
// one play session communicate through this database.
// ══════════════════════════════════════════════════════════════════════════════

// ── Status codes of a text — what the text is doing at a recorded moment ────
public enum DialogueStatusCode
{
    Idle,             // nothing live (dialogue closed, between statements, transition)
    WaitingForInput,  // waiting for IO: the user to hit Enter or Space
    TakingChoice,     // currently taking in a choice (waiting for an option)
    EventEmitted      // this row records an event emission
}

[Serializable]
public class DslRecord
{
    public string TextName;     // text DSL name (path passed to Play)
    public string FirstSeenAt;  // session timestamp (mm:ss) of the first play
    public int    PlayCount;    // how many times this text DSL was played
    public string LastStatus;   // status code of its last recorded row
}

[Serializable]
public class EventRow
{
    public string Timestamp;    // PK part 1 — session time, minute:second
    public string TextName;     // PK part 2 — the text that produced this row
    public string EmittedEvent; // event emitted at this moment ("" = none)
    public string StatusCode;   // status code of the text at this moment
    public float  Seconds;      // raw session-clock seconds (precise compares)
    public int    Seq;          // row number — disambiguates same-second rows
}

public static class DialogueDatabase
{
    static Dictionary<string, DslRecord> dslTable;
    static List<EventRow> eventTable;
    static int nextSeq;
    static float sessionStart;

    static DialogueDatabase()
    {
        // First access (editor or runtime) — start with a live, empty DB so
        // nobody ever touches a null table.
        Reset();
    }

    // ── Session clock (the "minute:second" timestamps) ──────────────────────

    /// <summary>Seconds since the play session's database was (re)started.</summary>
    public static float Now()
    {
        return Mathf.Max(0f, Time.time - sessionStart);
    }

    /// <summary>Formats session-clock seconds as minute:second (the PK format).</summary>
    public static string FormatTs(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return string.Format("{0:00}:{1:00}", s / 60, s % 60);
    }

    // ── Reset (stop playing / exit play scene / new play session) ───────────

    public static void Reset()
    {
        dslTable   = new Dictionary<string, DslRecord>(StringComparer.Ordinal);
        eventTable = new List<EventRow>();
        nextSeq    = 0;
        sessionStart = Time.time;
    }

    // ── DSL table ────────────────────────────────────────────────────────────

    /// <summary>Registers (or re-registers) a played text DSL; bumps its play count.</summary>
    public static DslRecord RegisterDsl(string textName)
    {
        DslRecord rec;

        if (!dslTable.TryGetValue(textName, out rec))
        {
            rec = new DslRecord
            {
                TextName = textName,
                FirstSeenAt = FormatTs(Now()),
                PlayCount = 0,
                LastStatus = ""
            };
            dslTable[textName] = rec;
        }

        rec.PlayCount++;
        return rec;
    }

    /// <summary>All unique text DSLs, in the order they were first played.</summary>
    public static List<DslRecord> GetDsls()
    {
        var list = new List<DslRecord>(dslTable.Values);
        list.Sort((a, b) => string.CompareOrdinal(a.FirstSeenAt, b.FirstSeenAt));
        return list;
    }

    // ── Event table ──────────────────────────────────────────────────────────

    /// <summary>
    /// Writes one row. emittedEvent may be "" — a status-only moment
    /// (text started, waiting for IO, taking a choice, dialogue closed).
    /// </summary>
    public static EventRow Record(string textName, string emittedEvent, DialogueStatusCode status)
    {
        float now = Now();

        var row = new EventRow
        {
            Timestamp = FormatTs(now),
            TextName = textName,
            EmittedEvent = emittedEvent ?? "",
            StatusCode = status.ToString(),
            Seconds = now,
            Seq = nextSeq++
        };

        eventTable.Add(row);

        DslRecord rec;
        if (dslTable.TryGetValue(textName, out rec))
            rec.LastStatus = row.StatusCode;

        return row;
    }

    /// <summary>All rows, optionally filtered by text name and/or event name.</summary>
    public static List<EventRow> GetEvents(string textName = null, string eventName = null)
    {
        var list = new List<EventRow>();

        foreach (var r in eventTable)
        {
            if (textName != null && r.TextName != textName) continue;
            if (eventName != null && r.EmittedEvent != eventName) continue;
            list.Add(r);
        }

        return list;
    }

    /// <summary>Rows recorded at or after a session-clock moment.</summary>
    public static List<EventRow> GetSince(float seconds, string textName = null)
    {
        var list = new List<EventRow>();

        foreach (var r in eventTable)
        {
            if (r.Seconds < seconds) continue;
            if (textName != null && r.TextName != textName) continue;
            list.Add(r);
        }

        return list;
    }

    /// <summary>
    /// True when the event was emitted at/after `since` seconds
    /// (optionally by a specific text).
    /// </summary>
    public static bool HasEvent(string eventName, float since = 0f, string textName = null)
    {
        foreach (var r in eventTable)
        {
            if (r.Seconds < since) continue;
            if (eventName != r.EmittedEvent) continue;
            if (textName != null && r.TextName != textName) continue;
            return true;
        }

        return false;
    }

    /// <summary>The most recent row for a text (or overall when textName is null).</summary>
    public static EventRow GetLatest(string textName)
    {
        EventRow last = null;

        foreach (var r in eventTable)
        {
            if (textName == null || r.TextName == textName)
                last = r;
        }

        return last;
    }

    public static int EventCount { get { return eventTable.Count; } }
    public static int DslCount   { get { return dslTable.Count; } }
}
