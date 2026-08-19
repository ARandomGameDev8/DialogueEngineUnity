using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// ══════════════════════════════════════════════════════════════════════════════
// AST NODE HIERARCHY
// ══════════════════════════════════════════════════════════════════════════════

public abstract class SyntaxToken
{
    public List<SyntaxToken> Children { get; set; } = new List<SyntaxToken>();

    public void AddChild(SyntaxToken child)
    {
        if (child != null)
            Children.Add(child);
    }
}

public class StartToken : SyntaxToken
{
}

public class VarToken : SyntaxToken
{
    public string VarName { get; }
    public string VarValue { get; }
    public int Index { get; }

    public VarToken(string name, string value, int index)
    {
        VarName = name;
        VarValue = value;
        Index = index;
    }
}

public class SectionToken : SyntaxToken
{
    public string SectionID { get; }

    public SectionToken(string id)
    {
        SectionID = id;
    }
}

public class CharacterToken : SyntaxToken
{
    public string Speaker { get; }
    public string ImageSource { get; }
    public bool ImageIsUnresolved { get; }
    public string Text { get; }

    public CharacterToken(
        string speaker,
        string imageSource,
        bool imageIsUnresolved,
        string text)
    {
        Speaker = speaker;
        ImageSource = imageSource;
        ImageIsUnresolved = imageIsUnresolved;
        Text = text;

        Children = null;
    }
}

public class ChoiceToken : SyntaxToken
{
    public int ChoiceIndex { get; }

    public ChoiceToken(int index)
    {
        ChoiceIndex = index;
    }
}

public class OptionToken : SyntaxToken
{
    public string OptionText { get; }
    public string TargetSectionID { get; }
    public string EmitText { get; }
    public int OptionIndex { get; }

    public OptionToken(
        string text,
        string targetSectionID,
        string emitText,
        int optionIndex)
    {
        OptionText = text;
        TargetSectionID = targetSectionID;
        EmitText = emitText;
        OptionIndex = optionIndex;

        Children = null;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// DIALOGUE GRAPH
// ══════════════════════════════════════════════════════════════════════════════

public class DialogueGraph
{
    public StartToken ASTRoot { get; set; }

    public Dictionary<string, SectionToken> AdjacencyList { get; set; }

    public SectionToken EntryNode { get; set; }

    public List<string> UnresolvedPortraitKeys { get; set; }
        = new List<string>();

    // ── ADDED: warning summary ────────────────────────────────────────────
    // Every non-fatal warning raised during compilation is collected here.
    // Unresolved portrait placeholders (the inspector-driven edge case) are
    // exactly the warnings listed in UnresolvedPortraitKeys, plus their
    // messages in Warnings. A script with 0 warnings compiles clean.
    public List<string> Warnings { get; set; } = new List<string>();

    public int WarningCount
    {
        get { return Warnings != null ? Warnings.Count : 0; }
    }

    public void PrintGraph()
    {
        Console.WriteLine("\n==========================================");
        Console.WriteLine(" DIALOGUE GRAPH DEBUG OUTPUT");
        Console.WriteLine("==========================================");

        Console.WriteLine(
            $"Entry Node: {EntryNode?.SectionID ?? "NONE"}");

        Console.WriteLine(
            $"Total Registered Sections: {AdjacencyList.Count}");

        if (UnresolvedPortraitKeys.Count > 0)
        {
            Console.WriteLine(
                "\nUnresolved Portrait Keys (Inspector Required):");

            foreach (var key in UnresolvedPortraitKeys)
                Console.WriteLine($"  - {key}");
        }

        Console.WriteLine("\n--- Graph Topology ---");

        foreach (var kvp in AdjacencyList)
        {
            Console.WriteLine($"\nSECTION: [{kvp.Key}]");

            PrintNodeContents(
                kvp.Value,
                "  ");
        }

        Console.WriteLine(
            "\n==========================================\n");
    }

    private void PrintNodeContents(
        SyntaxToken parentNode,
        string indent)
    {
        if (parentNode.Children == null)
            return;

        foreach (var child in parentNode.Children)
        {
            if (child is CharacterToken ct)
            {
                string portrait =
                    string.IsNullOrEmpty(ct.ImageSource)
                        ? ""
                        : $" | Portrait: {ct.ImageSource}";

                string debugText =
                    ct.Text.Replace("\n", "\\n");

                Console.WriteLine(
                    $"{indent}[Character] " +
                    $"{ct.Speaker}{portrait}: \"{debugText}\"");
            }

            else if (child is ChoiceToken ch)
            {
                Console.WriteLine(
                    $"{indent}[Choice Block #{ch.ChoiceIndex}]");

                PrintNodeContents(
                    ch,
                    indent + "  ");
            }

            else if (child is OptionToken ot)
            {
                string emit =
                    string.IsNullOrEmpty(ot.EmitText)
                        ? ""
                        : $" [@EMIT: {ot.EmitText}]";

                string target =
                    ot.Children != null &&
                    ot.Children.Count > 0 &&
                    ot.Children[0] is SectionToken st
                        ? st.SectionID
                        : ot.TargetSectionID;

                string debugOptionText = ot.OptionText.Replace("\n", "\\n");

                Console.WriteLine(
                    $"{indent}  -> Option #{ot.OptionIndex}: " +
                    $"\"{debugOptionText}\" ==> " +
                    $"GOTO [{target}]{emit}");
            }

            else if (child is SectionToken childSection)
            {
                Console.WriteLine(
                    $"{indent}[Child SECTION]: " +
                    $"[{childSection.SectionID}]");

                PrintNodeContents(
                    childSection,
                    indent + "  ");
            }
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// COMPILER
// ══════════════════════════════════════════════════════════════════════════════

public static class Compiler_S
{
    static Dictionary<string, string> varTable =
        new Dictionary<string, string>();

    // ── ADDED: per-compile warning accumulator (attached to the graph) ────
    static List<string> compileWarnings = new List<string>();

    public static event Action<string> OnWarning;

    static void Warn(string message)
    {
        Console.WriteLine($"[Warning] {message}");
        compileWarnings.Add(message);
        OnWarning?.Invoke(message);
    }

    public static DialogueGraph Compile(File_S file)
    {
        varTable.Clear();
        compileWarnings = new List<string>();

        Console.WriteLine("\n==========================================");
        Console.WriteLine(" Compiler_S: Starting Compilation");
        Console.WriteLine("==========================================");

        List<string> lines = Linearize(file);

        if (lines == null || lines.Count == 0)
        {
            Console.WriteLine(
                "[Error] File is empty or could not be read.");

            return null;
        }

        if (!lines[0].Equals(
                "START",
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "[Error] Script must begin with START.");

            return null;
        }

        if (!lines[lines.Count - 1].Equals(
                "END",
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "[Error] Script must end with END.");

            return null;
        }

        lines = lines.GetRange(
            1,
            lines.Count - 2);

        StartToken astRoot = new StartToken();

        bool astOk =
            BuildAST(
                lines,
                astRoot,
                out List<string> unresolvedPortraits);

        if (!astOk)
            return null;

        bool valid =
            ValidateAST(astRoot);

        if (!valid)
            return null;

        DialogueGraph graph =
            BuildGraph(astRoot);

        if (graph == null)
            return null;

        graph.UnresolvedPortraitKeys =
            unresolvedPortraits;

        // ── ADDED: attach the warning summary to the graph ────────────────
        graph.Warnings = new List<string>(compileWarnings);

        Console.WriteLine("\n==========================================");
        Console.WriteLine(" Compiler_S: Compilation Complete");
        Console.WriteLine($" Warnings: {graph.WarningCount}");
        if (graph.WarningCount > 0)
        {
            foreach (string w in graph.Warnings)
                Console.WriteLine($"   - {w}");
        }
        Console.WriteLine("==========================================\n");

        graph.PrintGraph();

        return graph;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LINEARIZE & HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    static bool EndsWithUnquotedSemicolon(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmed = text.TrimEnd();
        if (!trimmed.EndsWith(";"))
            return false;

        bool inString = false;
        bool escapeNext = false;

        for (int i = 0; i < trimmed.Length - 1; i++)
        {
            char c = trimmed[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
            }
        }

        return !inString;
    }

    static List<string> SplitOutsideQuotes(string input, char delimiter)
    {
        var results = new List<string>();
        var current = new StringBuilder();
        bool inString = false;
        bool escapeNext = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (escapeNext)
            {
                current.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                current.Append(c);
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                current.Append(c);
                continue;
            }

            if (c == delimiter && !inString)
            {
                results.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            results.Add(current.ToString());
        }

        return results;
    }

    static List<string> Linearize(File_S file)
    {
        Console.WriteLine(
            "\n[Linearize] Reading and flattening file...");

        StreamReader reader = file.get_reader();

        if (reader == null)
            return null;

        string source = reader.ReadToEnd();
        reader.Close();

        var logicalLines = new List<string>();
        var current = new StringBuilder();

        bool inString = false;
        bool inBlockComment = false;
        bool escapeNext = false;

        int i = 0;

        while (i < source.Length)
        {
            char c = source[i];

            // ================================================================
            // BLOCK COMMENT
            // ================================================================

            if (inBlockComment)
            {
                if (c == '*' &&
                    i + 1 < source.Length &&
                    source[i + 1] == '/')
                {
                    inBlockComment = false;
                    i += 2;
                }
                else
                {
                    i++;
                }

                continue;
            }

            // ================================================================
            // STRING
            // ================================================================

            if (inString)
            {
                current.Append(c);

                if (escapeNext)
                {
                    escapeNext = false;
                    i++;
                    continue;
                }

                if (c == '\\')
                {
                    escapeNext = true;
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = false;
                }

                i++;
                continue;
            }

            // ================================================================
            // NORMAL STATE
            // ================================================================

            // Start string
            if (c == '"')
            {
                inString = true;
                current.Append(c);
                i++;
                continue;
            }

            // Start block comment
            if (c == '/' &&
                i + 1 < source.Length &&
                source[i + 1] == '*')
            {
                inBlockComment = true;
                i += 2;
                continue;
            }

            // Start line comment
            if (c == '/' &&
                i + 1 < source.Length &&
                source[i + 1] == '/')
            {
                // Skip until newline
                i += 2;

                while (i < source.Length &&
                       source[i] != '\n' &&
                       source[i] != '\r')
                {
                    i++;
                }

                continue;
            }

            // ================================================================
            // NEWLINE
            // ================================================================

            if (c == '\r' || c == '\n')
            {
                string line = current.ToString().Trim();

                if (!string.IsNullOrEmpty(line))
                    logicalLines.Add(line);

                current.Clear();

                // Handle Windows \r\n
                if (c == '\r' &&
                    i + 1 < source.Length &&
                    source[i + 1] == '\n')
                {
                    i++;
                }

                i++;
                continue;
            }

            current.Append(c);
            i++;
        }

        if (inBlockComment)
        {
            Console.WriteLine(
                "[Error] Unclosed block comment /*");

            return null;
        }

        if (inString)
        {
            Console.WriteLine(
                "[Error] Unclosed string literal (\").");

            return null;
        }

        string finalLine = current.ToString().Trim();

        if (!string.IsNullOrEmpty(finalLine))
            logicalLines.Add(finalLine);

        // ================================================================
        // SECOND PASS:
        // Convert physical lines into DSL logical lines.
        //
        // A statement ends with ';' ONLY when outside quotes.
        // Structural tokens such as SECTION / END_SECTION / CHOICE
        // are standalone tokens.
        // ================================================================

        var flat = new List<string>();
        var acc = new StringBuilder();

        foreach (string line in logicalLines)
        {
            string trimmed = line.Trim();

            bool standalone =
                trimmed.Equals(
                    "START",
                    StringComparison.OrdinalIgnoreCase)

                || trimmed.Equals(
                    "END",
                    StringComparison.OrdinalIgnoreCase)

                || trimmed.Equals(
                    ";",
                    StringComparison.OrdinalIgnoreCase)

                || trimmed.Equals(
                    "END_SECTION",
                    StringComparison.OrdinalIgnoreCase)

                || trimmed.StartsWith(
                    "SECTION ",
                    StringComparison.OrdinalIgnoreCase)

                || trimmed.StartsWith(
                    "CHOICE:",
                    StringComparison.OrdinalIgnoreCase)

                || trimmed.StartsWith(
                    "@ENTRY",
                    StringComparison.OrdinalIgnoreCase);

            if (standalone)
            {
                if (acc.Length > 0)
                {
                    flat.Add(acc.ToString().Trim());
                    acc.Clear();
                }

                flat.Add(trimmed);
                continue;
            }

            if (acc.Length > 0)
                acc.Append('\n');

            acc.Append(trimmed);

            if (EndsWithUnquotedSemicolon(acc.ToString()))
            {
                flat.Add(acc.ToString().Trim());
                acc.Clear();
            }
        }

        if (acc.Length > 0)
            flat.Add(acc.ToString().Trim());

        Console.WriteLine(
            $"[Linearize] {flat.Count} logical lines.");

        foreach (string l in flat)
        {
            Console.WriteLine(
                $"  >> {l.Replace("\n", "\\n")}");
        }

        return flat;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // BUILD AST
    // ══════════════════════════════════════════════════════════════════════════

    static bool BuildAST(
        List<string> lines,
        StartToken root,
        out List<string> unresolvedPortraits)
    {
        Console.WriteLine(
            "\n[Stage 1] Building AST with Nested Sections...");

        unresolvedPortraits = new List<string>();

        int varIndex = 0;
        string explicitEntry = null;
        string firstSectionID = null;
        bool hasExplicitSections = false;

        foreach (string line in lines)
        {
            if (IsSectionStart(line))
            {
                hasExplicitSections = true;

                if (firstSectionID == null)
                    firstSectionID = ParseSectionID(line);
            }

            if (line.StartsWith("var ", StringComparison.OrdinalIgnoreCase))
            {
                VarToken vt = ParseVar(line, varIndex);

                if (vt == null)
                    return false;

                if (varTable.ContainsKey(vt.VarName))
                {
                    Console.WriteLine($"[Error] Duplicate variable: \"{vt.VarName}\"");
                    return false;
                }

                varTable[vt.VarName] = vt.VarValue;
                root.AddChild(vt);
                varIndex++;

                Console.WriteLine($"  [Var] {vt.VarName} = \"{vt.VarValue}\"");
                continue;
            }

            if (line.StartsWith("@ENTRY", StringComparison.OrdinalIgnoreCase))
            {
                string rest = line.Substring(6).Trim().TrimEnd(':').Trim();

                if (string.IsNullOrEmpty(rest))
                {
                    Console.WriteLine("[Error] @ENTRY must be followed by a section ID.");
                    return false;
                }

                if (explicitEntry != null)
                {
                    Console.WriteLine("[Error] Multiple @ENTRY declarations.");
                    return false;
                }

                explicitEntry = rest;
                Console.WriteLine($"  [@ENTRY] \"{explicitEntry}\"");
            }
        }

        var sectionTable = new Dictionary<string, SectionToken>(StringComparer.Ordinal);
        var sectionStack = new Stack<SectionToken>();

        ChoiceToken currentChoice = null;
        bool inChoice = false;
        int optionIndex = 0;
        int choiceIndex = 0;

        foreach (string line in lines)
        {
            if (line.StartsWith("var ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@ENTRY", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsSectionStart(line))
            {
                if (inChoice)
                {
                    Console.WriteLine("[Error] SECTION cannot begin inside a CHOICE block.");
                    return false;
                }

                string sid = ParseSectionID(line);

                if (string.IsNullOrEmpty(sid))
                {
                    Console.WriteLine("[Error] SECTION must have an ID.");
                    return false;
                }

                if (sectionTable.ContainsKey(sid))
                {
                    Console.WriteLine($"[Error] Duplicate section ID: \"{sid}\"");
                    return false;
                }

                SectionToken st = new SectionToken(sid);
                sectionTable[sid] = st;

                if (sectionStack.Count > 0)
                {
                    sectionStack.Peek().AddChild(st);
                    Console.WriteLine($"  [Nested Section] \"{sid}\" added as child to \"{sectionStack.Peek().SectionID}\"");
                }
                else
                {
                    root.AddChild(st);
                    Console.WriteLine($"  [Root Section Registered] {sid}");
                }

                sectionStack.Push(st);
                currentChoice = null;
                inChoice = false;
                continue;
            }

            if (line.Equals("END_SECTION", StringComparison.OrdinalIgnoreCase))
            {
                if (sectionStack.Count == 0)
                {
                    Console.WriteLine("[Error] Unmatched END_SECTION.");
                    return false;
                }

                if (inChoice)
                {
                    Console.WriteLine("[Error] CHOICE must be closed with ';' before END_SECTION.");
                    return false;
                }

                SectionToken closed = sectionStack.Pop();
                currentChoice = null;
                inChoice = false;

                Console.WriteLine($"  [Section Closed] \"{closed.SectionID}\"");
                continue;
            }

            if (sectionStack.Count == 0)
            {
                if (hasExplicitSections)
                {
                    Console.WriteLine($"[Error] Content exists outside a SECTION: \"{line}\"");
                    return false;
                }

                if (!sectionTable.ContainsKey("SECTION_0"))
                {
                    Console.WriteLine("[Stage 1] No explicit sections — creating SECTION_0.");
                    SectionToken implicitSection = new SectionToken("SECTION_0");
                    sectionTable["SECTION_0"] = implicitSection;
                    root.AddChild(implicitSection);
                    sectionStack.Push(implicitSection);

                    if (firstSectionID == null)
                        firstSectionID = "SECTION_0";
                }
            }

            SectionToken currentSection = sectionStack.Peek();

            if (line.StartsWith("CHOICE:", StringComparison.OrdinalIgnoreCase))
            {
                if (inChoice)
                {
                    Console.WriteLine("[Error] Nested CHOICE blocks are not allowed.");
                    return false;
                }

                currentChoice = new ChoiceToken(choiceIndex++);
                currentSection.AddChild(currentChoice);

                inChoice = true;
                optionIndex = 0;

                Console.WriteLine($"  [Choice] index={currentChoice.ChoiceIndex} in Section \"{currentSection.SectionID}\"");
                continue;
            }

            if (line == ";")
            {
                if (!inChoice)
                {
                    Console.WriteLine("[Error] Unexpected ';' outside of CHOICE block.");
                    return false;
                }

                inChoice = false;
                currentChoice = null;

                Console.WriteLine("  [Choice] block closed.");
                continue;
            }

            if (line.StartsWith("OPTION_", StringComparison.OrdinalIgnoreCase))
            {
                if (!inChoice)
                {
                    Console.WriteLine("[Error] OPTION found outside of CHOICE block.");
                    return false;
                }

                string optionHeader = line.Substring(0, line.IndexOf(':')).Trim();
                if (int.TryParse(optionHeader.Substring(7), out int parsedIdx))
                {
                    if (parsedIdx != optionIndex)
                    {
                        Console.WriteLine($"[Error] Non-sequential OPTION index. Expected OPTION_{optionIndex}, found OPTION_{parsedIdx}.");
                        return false;
                    }
                }

                OptionToken ot = ParseOption(line, optionIndex);

                if (ot == null)
                    return false;

                if (string.IsNullOrEmpty(ot.OptionText))
                {
                    Console.WriteLine("[Error] OPTION has no text.");
                    return false;
                }

                if (string.IsNullOrEmpty(ot.TargetSectionID))
                {
                    Console.WriteLine($"[Error] OPTION \"{ot.OptionText}\" has no GOTO target.");
                    return false;
                }

                currentChoice.AddChild(ot);
                optionIndex++;

                string debugOptionText = ot.OptionText.Replace("\n", "\\n");
                Console.WriteLine($"  [Option] index={ot.OptionIndex} text=\"{debugOptionText}\" goto={ot.TargetSectionID} emit=\"{ot.EmitText}\"");
                continue;
            }

            if (line.StartsWith("["))
            {
                if (inChoice)
                {
                    Console.WriteLine("[Error] Character dialogue cannot appear inside a CHOICE block.");
                    return false;
                }

                CharacterToken ct = ParseCharacter(line, unresolvedPortraits);

                if (ct == null)
                    return false;

                currentSection.AddChild(ct);

                if (ct.ImageIsUnresolved && !string.IsNullOrEmpty(ct.ImageSource))
                {
                    Warn($"Unresolved portrait key: \"{ct.ImageSource}\" — expose in inspector.");
                }

                Console.WriteLine($"  [Character] speaker=\"{ct.Speaker}\" image=\"{ct.ImageSource}\" unresolved={ct.ImageIsUnresolved} text=\"{ct.Text.Replace("\n", "\\n")}\"");
                continue;
            }

            Console.WriteLine($"[Error] Unrecognised line: \"{line}\"");
            return false;
        }

        if (inChoice)
        {
            Console.WriteLine("[Error] CHOICE block was never closed with ';'.");
            return false;
        }

        if (sectionStack.Count > 0)
        {
            // Auto-close the implicit SECTION_0 — explicit sections must be closed manually
            if (!hasExplicitSections &&
                sectionStack.Count == 1 &&
                sectionStack.Peek().SectionID == "SECTION_0")
            {
                Console.WriteLine("  [Section Auto-closed] SECTION_0");
                sectionStack.Pop();
            }
            else
            {
                Console.WriteLine($"[Error] Unclosed SECTION block: \"{sectionStack.Peek().SectionID}\"");
                return false;
            }
        }

        string entryTarget = explicitEntry ?? firstSectionID ?? "SECTION_0";

        if (!sectionTable.ContainsKey(entryTarget))
        {
            Console.WriteLine($"[Error] @ENTRY references undefined section: \"{entryTarget}\"");
            return false;
        }

        root.Children.Insert(0, new VarToken("__ENTRY__", entryTarget, -1));

        return true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // VALIDATE AST
    // ══════════════════════════════════════════════════════════════════════════

    static bool ValidateAST(StartToken root)
    {
        Console.WriteLine("\n[Stage 2] Validating AST via BFS...");

        bool valid = true;
        var sectionIDs = new HashSet<string>();
        var bfs = new Queue<SyntaxToken>();

        bfs.Enqueue(root);

        while (bfs.Count > 0)
        {
            SyntaxToken node = bfs.Dequeue();

            if (node is SectionToken st)
            {
                if (!sectionIDs.Add(st.SectionID))
                {
                    Console.WriteLine($"[Validation Error] Duplicate section ID: \"{st.SectionID}\"");
                    valid = false;
                }
            }

            if (node.Children != null)
            {
                foreach (SyntaxToken child in node.Children)
                    bfs.Enqueue(child);
            }
        }

        bfs.Enqueue(root);

        while (bfs.Count > 0)
        {
            SyntaxToken node = bfs.Dequeue();

            if (node is OptionToken ot)
            {
                if (!string.IsNullOrEmpty(ot.TargetSectionID))
                {
                    if (!sectionIDs.Contains(ot.TargetSectionID))
                    {
                        Console.WriteLine($"[Validation Error] Option goto undefined section: \"{ot.TargetSectionID}\"");
                        valid = false;
                    }
                }
            }

            if (node.Children != null)
            {
                foreach (SyntaxToken child in node.Children)
                    bfs.Enqueue(child);
            }
        }

        if (valid)
            Console.WriteLine("[Stage 2] AST is valid.");

        return valid;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // BUILD GRAPH
    // ══════════════════════════════════════════════════════════════════════════

    static DialogueGraph BuildGraph(StartToken root)
    {
        Console.WriteLine("\n[Stage 3] Building adjacency list via BFS...");

        var adjacency = new Dictionary<string, SectionToken>();
        SectionToken entry = null;
        string entryID = null;

        VarToken sentinel = null;

        foreach (SyntaxToken child in root.Children)
        {
            if (child is VarToken vt && vt.VarName == "__ENTRY__")
            {
                entryID = vt.VarValue;
                sentinel = vt;
                break;
            }
        }

        if (sentinel != null)
            root.Children.Remove(sentinel);

        var bfs = new Queue<SyntaxToken>();
        bfs.Enqueue(root);

        var options = new List<OptionToken>();

        while (bfs.Count > 0)
        {
            SyntaxToken node = bfs.Dequeue();

            if (node is SectionToken st)
            {
                adjacency[st.SectionID] = st;

                if (entry == null && st.SectionID == entryID)
                {
                    entry = st;
                }

                Console.WriteLine($"  [Graph] Registered \"{st.SectionID}\"");
            }

            if (node is OptionToken ot)
                options.Add(ot);

            if (node.Children != null)
            {
                foreach (SyntaxToken child in node.Children)
                    bfs.Enqueue(child);
            }
        }

        if (entry == null)
        {
            foreach (var kv in adjacency)
            {
                entry = kv.Value;
                break;
            }
        }

        foreach (OptionToken ot in options)
        {
            if (string.IsNullOrEmpty(ot.TargetSectionID))
            {
                continue;
            }

            if (adjacency.TryGetValue(ot.TargetSectionID, out SectionToken target))
            {
                ot.Children = new List<SyntaxToken> { target };

                string debugOptionText = ot.OptionText.Replace("\n", "\\n");
                Console.WriteLine($"  [Graph] Wired \"{debugOptionText}\" -> \"{ot.TargetSectionID}\"");
            }
        }

        return new DialogueGraph
        {
            ASTRoot = root,
            AdjacencyList = adjacency,
            EntryNode = entry
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PARSERS
    // ══════════════════════════════════════════════════════════════════════════

    static VarToken ParseVar(string line, int index)
    {
        string body = line.Substring(4).Trim();
        int eq = body.IndexOf('=');

        if (eq == -1)
        {
            Console.WriteLine($"[Error] Malformed var: \"{line}\"");
            return null;
        }

        string name = body.Substring(0, eq).Trim();
        string val = body.Substring(eq + 1).Trim();

        if (val.EndsWith(";"))
        {
            val = val.Substring(0, val.Length - 1).Trim();
        }

        val = val.Trim('"');

        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine("[Error] Variable name cannot be empty.");
            return null;
        }

        return new VarToken(name, val, index);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CHARACTER PARSER
    // ══════════════════════════════════════════════════════════════════════════

    static CharacterToken ParseCharacter(
        string line,
        List<string> unresolvedPortraits)
    {
        string cleanLine = line.Trim();

        if (cleanLine.EndsWith(";"))
        {
            cleanLine = cleanLine.Substring(0, cleanLine.Length - 1).TrimEnd();
        }
        else
        {
            Console.WriteLine("[Error] Character dialogue domain must end with ';'.");
            return null;
        }

        int cb = cleanLine.IndexOf(']');

        if (cb == -1)
        {
            Console.WriteLine($"[Error] Malformed character line: \"{line}\"");
            return null;
        }

        if (cb == 1)
        {
            Console.WriteLine("[Error] Character speaker cannot be empty.");
            return null;
        }

        string header = cleanLine.Substring(1, cb - 1);
        string rawText = cleanLine.Substring(cb + 1).TrimStart(':', ' ');

        if (!rawText.StartsWith("\"") || !rawText.EndsWith("\"") || rawText.Length < 2)
        {
            Console.WriteLine($"[Error] Character dialogue text must be enclosed in double quotes (\"\"): \"{rawText}\"");
            return null;
        }

        rawText = rawText.Substring(1, rawText.Length - 2);

        rawText = rawText.Replace("\\n", "\n");

        string speaker = header.Trim();
        string imageRaw = "";
        bool imageIsUnresolved = false;

        if (header.Contains("|"))
        {
            string[] parts = header.Split('|');

            if (parts.Length != 2)
            {
                Console.WriteLine($"[Error] Malformed character portrait declaration: \"{header}\"");
                return null;
            }

            speaker = parts[0].Trim();
            imageRaw = parts[1].Trim();

            if (string.IsNullOrEmpty(imageRaw))
            {
                Console.WriteLine("[Error] Portrait key cannot be empty.");
                return null;
            }

            if (varTable.ContainsKey(imageRaw))
            {
                imageRaw = varTable[imageRaw];
                imageIsUnresolved = false;
            }
            else
            {
                imageIsUnresolved = true;

                if (!unresolvedPortraits.Contains(imageRaw))
                {
                    unresolvedPortraits.Add(imageRaw);
                }
            }
        }

        speaker = Resolve(speaker);
        string text = ResolveMultiline(rawText);

        return new CharacterToken(
            speaker,
            imageRaw,
            imageIsUnresolved,
            text);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // OPTION PARSER
    // ══════════════════════════════════════════════════════════════════════════

    static OptionToken ParseOption(
        string line,
        int index)
    {
        List<string> segments = SplitOutsideQuotes(line, ';');

        if (segments.Count == 0)
            return null;

        string optionText = "";
        string targetSectionID = "";
        string emitText = null;

        foreach (string rawSeg in segments)
        {
            string seg = rawSeg.Trim();

            if (string.IsNullOrEmpty(seg))
                continue;

            if (seg.StartsWith("OPTION_", StringComparison.OrdinalIgnoreCase))
            {
                int col = seg.IndexOf(':');

                if (col != -1)
                {
                    optionText = seg.Substring(col + 1).Trim();

                    if (!optionText.StartsWith("\"") || !optionText.EndsWith("\"") || optionText.Length < 2)
                    {
                        Console.WriteLine($"[Error] OPTION text must be enclosed in double quotes (\"\"): \"{optionText}\"");
                        return null;
                    }

                    optionText = optionText.Substring(1, optionText.Length - 2);
                }
                else
                {
                    Console.WriteLine($"[Error] Malformed OPTION: \"{seg}\"");
                    return null;
                }
            }
            else if (seg.StartsWith("goto ", StringComparison.OrdinalIgnoreCase))
            {
                targetSectionID = seg.Substring(5).Trim();
            }
            else if (seg.StartsWith("@EMIT", StringComparison.OrdinalIgnoreCase))
            {
                emitText = Resolve(seg.Substring(5).Trim());
            }
            else
            {
                Console.WriteLine($"[Error] Unknown OPTION segment: \"{seg}\"");
                return null;
            }
        }

        return new OptionToken(
            ResolveMultiline(optionText),
            targetSectionID,
            emitText,
            index);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RESOLUTION
    // ══════════════════════════════════════════════════════════════════════════

    static string Resolve(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        string clean = input.Trim().Trim('"');

        if (varTable.TryGetValue(clean, out string val))
        {
            return val;
        }

        return clean;
    }

    static string ResolveMultiline(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        string trimmed = input.Trim();

        if (varTable.TryGetValue(trimmed.Trim('"'), out string val))
        {
            return val;
        }

        return input;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SECTION HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    static bool IsSectionStart(string line)
    {
        return line.StartsWith("SECTION ", StringComparison.OrdinalIgnoreCase);
    }

    static string ParseSectionID(string line)
    {
        if (!IsSectionStart(line))
            return "";

        string sid = line.Substring(8).Trim().TrimEnd(':').Trim();
        return sid;
    }
}
