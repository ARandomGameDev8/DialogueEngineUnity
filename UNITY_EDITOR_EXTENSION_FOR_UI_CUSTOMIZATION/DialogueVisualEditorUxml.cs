#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// OWNED BY THE VISUAL EDITOR. Builds THE canonical runtime UXML from a
/// DialogueLayoutAsset's RESOLVED layout — the exact same geometry the visual
/// editor canvas draws. There is exactly ONE builder: the editor writes the
/// canonical file (<asset name>_dialogue_ui.uxml next to the asset), keeps it
/// current on every edit, and Dialogue_Engine simply instantiates a copy of
/// that file at Play. Nothing at play time re-derives or approximates the
/// layout.
///
/// Every panel, attached area, slot and component is emitted at its resolved
/// rect (percentages of the design canvas for the box, pixel offsets for the
/// nested pieces), with the backgrounds / borders / radii / text styles taken
/// straight from the asset definitions.
///
/// The first text-panel component becomes the live dialogue text
/// (TextScroll/DialogueText), the first name-panel component becomes the live
/// speaker-name element, and the first image-panel component becomes the live
/// portrait (icon → outside portrait structure, character figure → character
/// panel structure). All element names match what Dialogue_Engine queries at
/// runtime, so the engine keeps owning text, names, images and speaker
/// emphasis while reproducing the edited layout exactly.
/// </summary>
[InitializeOnLoad]
public static class DialogueVisualEditorUxml
{
    static DialogueVisualEditorUxml()
    {
        // Install the build hook on the engine. The engine cannot reference
        // this class directly (editor scripts often live in an "Editor" magic
        // folder = a separate assembly), so the dependency is inverted.
        Dialogue_Engine.EnsureVisualLayoutUxmlBuilt = EnsureBuilt;
    }

    // ─── Entry points ──────────────────────────────────────────────────────────
    /// <summary>Path of the canonical, editor-owned UXML for this asset.</summary>
    public static string BuildPathFor(DialogueLayoutAsset asset)
    {
        if (asset == null) return null;
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(assetPath))
            return Path.Combine("Assets/Scripts/Dialogue_Presets", asset.name + "_dialogue_ui.uxml");
        string folder = Path.GetDirectoryName(assetPath);
        return Path.Combine(string.IsNullOrEmpty(folder) ? "Assets" : folder,
            Path.GetFileNameWithoutExtension(assetPath) + "_dialogue_ui.uxml");
    }

    /// <summary>
    /// Makes sure the canonical UXML file exists and matches the asset's
    /// current state (rebuilding only when the content actually changed), and
    /// returns its path. Used by the editor on every change and by the engine
    /// at Play — always the same single builder, so the file can never drift
    /// from the editor.
    /// </summary>
    public static string EnsureBuilt(DialogueLayoutAsset asset, Dialogue_Engine engine, Vector2 canvas)
    {
        string xml = Build(asset, engine, canvas);
        string path = BuildPathFor(asset);
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        if (!File.Exists(path) || File.ReadAllText(path) != xml)
        {
            File.WriteAllText(path, xml);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        return path;
    }

    /// <summary>Builds the canonical UXML text from the resolved layout.</summary>
    public static string Build(DialogueLayoutAsset asset, Dialogue_Engine engine, Vector2 canvas)
    {
        canvas.x = Mathf.Max(64f, canvas.x);
        canvas.y = Mathf.Max(64f, canvas.y);

        ResolvedDialogueLayout resolved = DialogueVisualLayoutResolver.Resolve(
            asset, new Rect(0f, 0f, canvas.x, canvas.y));

        // Image-based panels import their images into the project so the
        // runtime UXML (and the True Preview) can render them via project: URLs.
        var media = new DialogueUiMediaImport(BuildPathFor(asset));

        var body = new StringBuilder();
        var uss = new StringBuilder();

        bool liveTextDone = false;

        DialogueComponentDefinition liveText = FindFirstComponent<DialogueTextPanelDefinition>(asset);

        // EVERY image panel and EVERY name panel is live — indexed in layout
        // order. The k-th speaker (order of first appearance) owns the k-th
        // panel pair: k-th image panel + k-th name panel. No mirrored
        // Left/Right pair, no first-panel funnel — one structure per panel at
        // that panel's exact resolved rect.
        var imagePanels = new List<DialogueImagePanelDefinition>();
        var namePanels  = new List<DialogueNamePanelDefinition>();
        CollectComponents(asset, imagePanels);
        CollectComponents(asset, namePanels);

        var livePanels = new HashSet<DialogueComponentDefinition>();
        for (int i = 0; i < imagePanels.Count; i++) livePanels.Add(imagePanels[i]);
        for (int i = 0; i < namePanels.Count; i++) livePanels.Add(namePanels[i]);

        // ── Main dialogue box at its resolved rect ─────────────────────────────
        Rect box = resolved.MainPanelRect;

        // ── Live cast slots: one per panel, indexed in layout order ───────────
        var portraits = new StringBuilder();
        int panelCount = Mathf.Max(imagePanels.Count, namePanels.Count);
        for (int i = 0; i < panelCount; i++)
        {
            portraits.Append(VisualSlotStructure(resolved, asset,
                i < imagePanels.Count ? imagePanels[i] : null,
                i < namePanels.Count  ? namePanels[i]  : null,
                namePanels.Count > 0, box, engine, i));
        }

        var boxEl = new StringBuilder();

        // Exact surface styling (per-side borders, per-corner radii, colour,
        // opacity) lives on a child so the box itself has zero insets and all
        // nested rects map 1:1 onto canvas coordinates.
        boxEl.Append(AbsEl("VisualPanelSurface",
            "position: absolute; left: 0; top: 0; right: 0; bottom: 0; " +
            PanelSurfaceStyle(asset.MainPanel, media) + " overflow: hidden;", null));
        boxEl.Append(AbsEl("BackgroundLayer",
            "position: absolute; left: 0; top: 0; right: 0; bottom: 0; display: none;", null));

        // ── Inner region + attached areas + their slots + components ──────────
        for (int i = 0; i < resolved.Areas.Count; i++)
        {
            ResolvedDialogueArea area = resolved.Areas[i];
            if (area.AreaKind == ResolvedDialogueAreaKind.ChoiceInner)
                continue; // emitted as its own standalone ChoicePanel below
            DialogueBackgroundStyle bg = null;
            DialogueBorderStyle border = null;
            DialogueOpacitySettings opacity = null;
            List<DialogueSlotDefinition> slots = null;

            if (area.AreaKind == ResolvedDialogueAreaKind.MainInner)
            {
                DialogueInnerRegionDefinition region = asset.MainPanel.InnerRegion;
                if (region != null) { bg = region.Background; border = region.Border; opacity = region.Opacity; slots = region.Slots; }
            }
            else
            {
                DialogueAttachedAreaDefinition def = GetArea(asset, area.AreaKind);
                if (def != null) { bg = def.Background; border = def.Border; opacity = def.Opacity; slots = def.Slots; }
            }

            var areaEl = new StringBuilder();
            if (slots != null)
            {
                for (int s = 0; s < slots.Count; s++)
                {
                    DialogueSlotDefinition slotDef = slots[s];
                    ResolvedDialogueSlot slot = FindSlot(resolved, area.AreaKind, s);
                    if (slot == null || slotDef == null) continue;

                    var slotEl = new StringBuilder();
                    if (slotDef.Components != null)
                    {
                        for (int c = 0; c < slotDef.Components.Count; c++)
                        {
                            DialogueComponentDefinition comp = slotDef.Components[c];
                            ResolvedDialogueComponentRect compRect = FindComponent(resolved, area.AreaKind, s, c);
                            if (comp == null || compRect == null) continue;

                            Rect r = Relative(compRect.Rect, slot.Rect);

                            if (comp == liveText && !liveTextDone)
                            {
                                liveTextDone = true;
                                // The live dialogue text lives exactly inside this
                                // component: styled surface + the engine's scroll.
                                string wrapStyle = AbsStyle(r.x, r.y, r.width, r.height) +
                                    SurfaceStyle(comp.Background, comp.Border, comp.Opacity) + " overflow: hidden;";
                                slotEl.Append("<ui:VisualElement style=\"" + wrapStyle + "\">\n" +
                                    TextScrollLocal(comp as DialogueTextPanelDefinition) +
                                    "</ui:VisualElement>" + "\n");
                                continue;
                            }

                            if (livePanels.Contains(comp))
                            {
                                // Every image/name panel renders inside its own
                                // indexed VisualSlot structure instead.
                                continue;
                            }

                            slotEl.Append(StaticComponentXml(comp, r));
                        }
                    }
                    areaEl.Append(AbsEl(null,
                        AbsStyle(slot.Rect.x - area.Rect.x, slot.Rect.y - area.Rect.y, slot.Rect.width, slot.Rect.height) +
                        SurfaceStyle(slotDef.Background, slotDef.Border, slotDef.Opacity) + " overflow: hidden;",
                        slotEl.ToString()));
                }
            }

            DialogueAttachedAreaDefinition areaDef = GetArea(asset, area.AreaKind);
            string areaSurface = areaDef != null && areaDef.UseImageBackground
                ? media.ImageStyleFor(areaDef.ImageBackgroundPath)
                : SurfaceStyle(bg, border, opacity);
            boxEl.Append(AbsEl(null,
                AbsStyle(area.Rect.x - box.x, area.Rect.y - box.y, area.Rect.width, area.Rect.height) +
                areaSurface + " overflow: hidden;",
                areaEl.ToString()));
        }

        // ── Choices + advance hint inside the box ──────────────────────────────
        Rect hintArea = liveText != null ? Relative(FindComponentRect(resolved, asset, liveText), box)
                                         : new Rect(0, box.height - 30, box.width, 26);
        float choiceTop = Mathf.Min(hintArea.yMax + 6f, box.height - 30f);
        float choiceHeight = Mathf.Max(60f, box.height - choiceTop - 8f);
        boxEl.Append(AbsEl("ChoiceContainer",
            AbsStyle(hintArea.x, choiceTop, hintArea.width, choiceHeight) + " display: none;", null));
        float hintTop = Mathf.Min(hintArea.yMax + 8f, box.height - 22f);
        float hintLeft = Mathf.Max(0f, hintArea.xMax - 170f);
        boxEl.Append($"<ui:Label name=\"AdvanceHint\" text=\"{Escape(engine.advanceHintText)}\" style=\"position: absolute; left: {hintLeft:0.#}px; top: {hintTop:0.#}px; width: 170px; color: {Rgba(engine.hintColour)}; font-size: {engine.hintFontSize}px; {(engine.showAdvanceHint ? "" : "display: none; ")}-unity-text-align: middle-right;\" />" + "\n");

        body.Append($"<ui:VisualElement name=\"DialogueBox\" style=\"position: absolute; left: {Pct(box.x, canvas.x)}; top: {Pct(box.y, canvas.y)}; width: {Pct(box.width, canvas.x)}; height: {Pct(box.height, canvas.y)}; overflow: visible;\">\n{boxEl}</ui:VisualElement>" + "\n");

        // Portraits/names come AFTER the box in DOM order so they paint on top
        // of it — a name panel inside or overlapping the box must stay visible.
        body.Append(portraits);

        // ── Choice event panel — hidden until the player takes a choice ──────
        body.Append(ChoicePanelXml(resolved, asset, media));

        // ── Free-floating UI panel — always visible, fully static ───────────
        body.Append(FreePanelXml(resolved, asset, media));

        // ── Standard chrome the engine binds to ────────────────────────────────
        body.Append("<ui:VisualElement name=\"BorderLayer\" style=\"position: absolute; overflow: hidden; display: none; picking-mode: Ignore;\" />" + "\n");
        body.Append(HistoryAndSettingsXml());
        body.Append(ToolbarXml(engine));

        BuildUss(uss, engine, asset);

        return $@"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:uie=""UnityEditor.UIElements"" xsi=""http://www.w3.org/2001/XMLSchema-instance"" engine=""UnityEngine.UIElements"" editor=""UnityEditor.UIElements"" noNamespaceSchemaLocation=""../../UIElementsSchema/UIElements.xsd"" editor-extension-mode=""False"">
    <ui:VisualElement name=""Root"" style=""width: 100%; height: 100%;"">
        <ui:VisualElement name=""RowContainer"" style=""width: 100%; height: 100%;"">
{body}
        </ui:VisualElement>
    </ui:VisualElement>
    <Style>
{uss}
    </Style>
</ui:UXML>";
    }

    // ─── Live text ─────────────────────────────────────────────────────────────
    static string TextScrollLocal(DialogueTextPanelDefinition text)
    {
        // Colour, size and alignment come from the text panel component itself —
        // exactly what the visual editor shows.
        DialogueTextStyle style = text != null ? text.TextStyle : null;
        Color color = style != null ? style.Color : new Color(0.93f, 0.93f, 0.93f, 1f);
        float fontSize = style != null ? Mathf.Max(1f, style.FontSize) : 15f;
        string align = style != null
            ? TextAlign(style.HorizontalAlignment, style.VerticalAlignment)
            : "middle-center";
        float spacing = style != null ? style.LetterSpacing : 0f;
        return "<ui:ScrollView name=\"TextScroll\" mode=\"Vertical\" style=\"position: absolute; left: 0; top: 0; right: 0; bottom: 0; background-color: rgba(0,0,0,0);\">\n" +
               "<ui:Label name=\"DialogueText\" style=\"color: " + Rgba(color) +
               "; font-size: " + fontSize.ToString("0.#") + "px; letter-spacing: " + spacing.ToString("0.#") +
               "px; white-space: normal; -unity-text-align: " + align + ";\" />\n" +
               "</ui:ScrollView>\n";
    }

    // ─── Portraits ─────────────────────────────────────────────────────────────
    // ─── Live cast slot (indexed: one structure per panel) ────────────────────
    static string VisualSlotStructure(ResolvedDialogueLayout resolved, DialogueLayoutAsset asset,
        DialogueImagePanelDefinition img, DialogueNamePanelDefinition nm,
        bool anyNamePanels, Rect box, Dialogue_Engine engine, int index)
    {
        Rect imageRect = img != null ? FindComponentRect(resolved, asset, img) : Rect.zero;
        Rect nameRect  = nm  != null ? FindComponentRect(resolved, asset, nm)  : Rect.zero;

        // A name panel that is not currently resolved (e.g. inside an attached
        // area auto-hidden by the main-panel anchor) must still show a name:
        // fall back to a nameplate just above the box.
        if (nm != null && (nameRect.width < 1f || nameRect.height < 1f))
            nameRect = new Rect(box.x + 8f, box.y - 30f, Mathf.Min(box.width * 0.5f, 360f), 26f);
        if (img != null && (imageRect.width < 1f || imageRect.height < 1f))
            imageRect = new Rect(box.x - 120f, box.y - 120f, 96f, 96f);
        // Icon slot with no name panel anywhere in the layout: keep the classic
        // nameplate above the image so speaker names never vanish.
        if (nm == null && img != null && !anyNamePanels)
            nameRect = DefaultIconNameRect(imageRect, engine);

        bool figure = img != null && img.Mode == DialogueImagePanelMode.CharacterFigure;

        string panelStyle;
        string fill;
        string hideClass = "";
        if (figure)
        {
            // A figure that hides when empty paints nothing itself — the
            // engine gates the whole panel on the loaded image (class marker).
            panelStyle = img.HideWhenEmpty
                ? "background-color: rgba(0,0,0,0); border-width: 0;"
                : SurfaceStyle(img.Background, img.Border, img.Opacity);
            if (img.HideWhenEmpty) hideClass = " class=\"dlg-fig-hide\"";
            fill = img.FigureScaleMode == DialogueFigureScaleMode.Fill
                ? "background-size: cover;" : "background-size: contain;";
        }
        else
        {
            panelStyle = "";
            fill = "background-size: cover;";
        }

        string frame = img != null ? FrameStyle(img) : "";
        string nameSurface = nm != null
            ? SurfaceStyle(nm.Background, nm.Border, nm.Opacity)
            : "";

        var sb = new StringBuilder();
        sb.Append($@"<ui:VisualElement name=""VisualSlot{index}Wrapper"" style=""position: absolute; left: 0; top: 0; width: 0; height: 0; display: none;"">
");
        if (img != null)
        {
            sb.Append($@"  <ui:VisualElement name=""VisualImagePanel{index}""{hideClass} style=""position: absolute; left: {imageRect.x:0.#}px; top: {imageRect.y:0.#}px; width: {imageRect.width:0.#}px; height: {imageRect.height:0.#}px; overflow: hidden;{Join(panelStyle)}"">
    <ui:VisualElement name=""VisualPortraitFrame{index}"" style=""position: absolute; left: 0; top: 0; right: 0; bottom: 0;{Join(frame)}"">
      <ui:VisualElement name=""VisualPortrait{index}"" style=""position: absolute; left: 0; top: 0; right: 0; bottom: 0; overflow: hidden; {fill}"" />
    </ui:VisualElement>
  </ui:VisualElement>
");
        }
        if (nm != null || (img != null && !anyNamePanels))
        {
            sb.Append($@"  <ui:VisualElement name=""VisualName{index}"" style=""position: absolute; left: {nameRect.x:0.#}px; top: {nameRect.y:0.#}px; width: {nameRect.width:0.#}px; height: {nameRect.height:0.#}px; justify-content: center; overflow: hidden;{Join(nameSurface)}"" />
");
        }
        sb.Append("</ui:VisualElement>" + "\n");
        return sb.ToString();
    }

    // ─── Choice event panel ─────────────────────────────────────────────────────
    static string ChoicePanelXml(ResolvedDialogueLayout resolved, DialogueLayoutAsset asset,
        DialogueUiMediaImport media)
    {
        if (!resolved.ChoicePanelActive) return "";

        DialogueMainPanelDefinition panel = asset.ChoicePanel;
        DialogueInnerRegionDefinition region = panel != null ? panel.InnerRegion : null;
        Rect panelRect = resolved.ChoicePanelRect;

        var sb = new StringBuilder();
        // Surface styling lives on a child so the panel maps 1:1 onto canvas
        // coordinates, exactly like the main box.
        // The panel itself is PX (like every child inside it): the exact rect
        // the editor resolved against the reference resolution. Percentages
        // here would rescale against the live game view and clip the bottom
        // of the region whenever the view differs from the reference.
        sb.Append($@"<ui:VisualElement name=""ChoicePanel"" style=""position: absolute; left: {panelRect.x:0.#}px; top: {panelRect.y:0.#}px; width: {panelRect.width:0.#}px; height: {panelRect.height:0.#}px; display: none;"">
  <ui:VisualElement name=""ChoicePanelSurface"" style=""position: absolute; left: 0; top: 0; right: 0; bottom: 0;{Join(PanelSurfaceStyle(panel, media))} overflow: hidden;"">
");

        if (region != null)
        {
            ResolvedDialogueArea area = null;
            for (int i = 0; i < resolved.Areas.Count; i++)
                if (resolved.Areas[i].AreaKind == ResolvedDialogueAreaKind.ChoiceInner)
                { area = resolved.Areas[i]; break; }

            if (area != null)
            {
                var areaEl = new StringBuilder();
                areaEl.Append($@"    <ui:VisualElement name=""ChoiceRegion"" style=""{AbsStyle(area.Rect.x - panelRect.x, area.Rect.y - panelRect.y, area.Rect.width, area.Rect.height)}{Join(SurfaceStyle(region.Background, region.Border, region.Opacity))} overflow: hidden;"">
");
                int slotCount = Mathf.Clamp(1 + Mathf.Clamp(region.PartitionLevel, 0, 2), 1, 3);
                for (int i = 0; i < slotCount; i++)
                {
                    ResolvedDialogueSlot slot = FindSlot(resolved, ResolvedDialogueAreaKind.ChoiceInner, i);
                    DialogueSlotDefinition slotDef = region.Slots != null && i < region.Slots.Count
                        ? region.Slots[i] : null;
                    if (slot == null || slotDef == null) continue;

                    var slotEl = new StringBuilder();
                    if (i == resolved.ChoiceHolderSlotIndex)
                    {
                        // This slot holds the choice BUTTONS (grouped leaves),
                        // styled entirely by the shared preset.
                        slotEl.Append(ChoiceButtonsXml(resolved, asset, slot));
                    }
                    else
                    {
                    slotEl.Append(StaticChoiceSlotComponents(resolved, asset, region, i, slot, slotDef));
                    }
                    areaEl.Append($@"    <ui:VisualElement name=""ChoiceSlot{i}"" style=""{AbsStyle(slot.Rect.x - area.Rect.x, slot.Rect.y - area.Rect.y, slot.Rect.width, slot.Rect.height)}{Join(SurfaceStyle(slotDef.Background, slotDef.Border, slotDef.Opacity))} overflow: hidden;"">
");
                    areaEl.Append(slotEl);
                    areaEl.Append("    </ui:VisualElement>" + "\n");
                }
                areaEl.Append("    </ui:VisualElement>" + "\n");
                sb.Append(areaEl);
            }
        }

        sb.Append("  </ui:VisualElement>\n</ui:VisualElement>" + "\n");
        return sb.ToString();
    }

    /// <summary>Components for a NON-holder choice slot (labels, images...).</summary>
    static string StaticChoiceSlotComponents(ResolvedDialogueLayout resolved, DialogueLayoutAsset asset,
        DialogueInnerRegionDefinition region, int slotIndex, ResolvedDialogueSlot slot,
        DialogueSlotDefinition slotDef)
    {
        var slotEl = new StringBuilder();
        bool labelDone = false;
        if (slotDef.Components != null)
                    {
                        for (int c = 0; c < slotDef.Components.Count; c++)
                        {
                            DialogueComponentDefinition comp = slotDef.Components[c];
                            ResolvedDialogueComponentRect compRect = FindComponent(
                                resolved, ResolvedDialogueAreaKind.ChoiceInner, slotIndex, c);
                            if (comp == null || compRect == null) continue;

                            Rect r = Relative(compRect.Rect, slot.Rect);
                            DialogueTextPanelDefinition text = comp as DialogueTextPanelDefinition;
                            if (text != null && !labelDone)
                            {
                                // The first text panel in a choice slot is that
                                // option's LIVE label — the engine writes the
                                // option text here at runtime.
                                labelDone = true;
                                slotEl.Append($@"      <ui:Label name=""ChoiceLabel{slotIndex}"" text="""" style=""{AbsStyle(r.x, r.y, r.width, r.height)}{Join(SurfaceStyle(comp.Background, comp.Border, comp.Opacity))}{TextStyle(text.TextStyle)}"" />
");
                                continue;
                            }
                            slotEl.Append(StaticComponentXml(comp, r));
                        }
                    }
        return slotEl.ToString();
    }

    // ─── Choice buttons (shared preset, auto-arranged leaves) ──────────────────
    static string ChoiceButtonsXml(ResolvedDialogueLayout resolved, DialogueLayoutAsset asset,
        ResolvedDialogueSlot holderSlot)
    {
        DialogueChoiceButtonSettings preset = asset.ChoiceButtons;
        if (preset == null) return "";

        // Preview arrangement for the visible buttons; the 6-button
        // arrangement positions the (hidden) extras so the file still renders
        // sensibly if the engine cannot re-arrange at runtime.
        Rect holderSlotRect, holderContent;
        List<Rect> previewRects, fullRects;
        bool hasPreview = TryCallArrange(asset, resolved, Mathf.Clamp(asset.ChoicePreviewCount, 0, 6), out previewRects);
        bool hasFull    = TryCallArrange(asset, resolved, 6, out fullRects);

        var sb = new StringBuilder();
        int total = 6;
        for (int k = 0; k < total; k++)
        {
            Rect rect;
            bool visible;
            if (hasPreview && k < previewRects.Count) { rect = previewRects[k]; visible = true; }
            else if (hasFull && k < fullRects.Count)  { rect = fullRects[k];    visible = false; }
            else continue;

            string buttonStyle = AbsStyle(rect.x - holderSlot.Rect.x,
                rect.y - holderSlot.Rect.y, rect.width, rect.height) +
                SurfaceStyle(preset.Background, preset.Border, preset.Opacity);
            DialoguePadding pad = preset.Padding != null ? preset.Padding : new DialoguePadding();
            sb.Append($@"        <ui:VisualElement name=""ChoiceButton{k}"" class=""dlg-choice-btn"" style=""{buttonStyle}{(visible ? "" : " display: none;")}"">
");
            sb.Append($@"          <ui:Label name=""ChoiceButtonText{k}"" text="""" style=""position: absolute; left: {pad.Left:0.#}px; top: {pad.Top:0.#}px; right: {pad.Right:0.#}px; bottom: {pad.Bottom:0.#}px;{TextStyle(preset.TextStyle)} white-space: normal;"" />
");
            sb.Append("        </ui:VisualElement>" + "\n");
        }
        return sb.ToString();
    }

    static bool TryCallArrange(DialogueLayoutAsset asset, ResolvedDialogueLayout resolved,
        int count, out List<Rect> rects)
    {
        Rect slotRect, content;
        // Same arrangement math the resolver uses, derived from this resolved
        // layout (the public entry re-resolves; this mirrors its core).
        return DialogueVisualLayoutResolver.ResolveChoiceButtonRectsFromLayout(asset, resolved,
            count, out slotRect, out content, out rects);
    }

    /// <summary>Surface style for a panel definition: image-based panels emit
    /// ONLY the stretched background image (their own surface is invisible at
    /// Play); normal panels emit background/border/opacity as before.</summary>
    static string PanelSurfaceStyle(DialogueMainPanelDefinition panel, DialogueUiMediaImport media)
    {
        if (panel == null) return "";
        if (panel.UseImageBackground && media != null)
        {
            string img = media.ImageStyleFor(panel.ImageBackgroundPath);
            if (!string.IsNullOrEmpty(img)) return img;
        }
        return SurfaceStyle(panel.Background, panel.Border, panel.Opacity);
    }

    // ─── Free-floating UI panels (as many as the layout declares) ─────────────
    static string FreePanelXml(ResolvedDialogueLayout resolved, DialogueLayoutAsset asset,
        DialogueUiMediaImport media)
    {
        if (asset.FreePanels == null || asset.FreePanels.Count == 0) return "";

        var all = new StringBuilder();
        int rectIndex = 0;
        for (int f = 0; f < asset.FreePanels.Count; f++)
        {
            DialogueMainPanelDefinition panel = asset.FreePanels[f];
            if (panel == null || !panel.Enabled) continue;
            if (rectIndex >= resolved.FreePanelRects.Count) break;
            Rect panelRect = resolved.FreePanelRects[rectIndex++];
            DialogueInnerRegionDefinition region = panel.InnerRegion;

            var sb = new StringBuilder();
            sb.Append($@"<ui:VisualElement name=""FreePanel{f}"" style=""position: absolute; left: {panelRect.x:0.#}px; top: {panelRect.y:0.#}px; width: {panelRect.width:0.#}px; height: {panelRect.height:0.#}px;"">
  <ui:VisualElement name=""FreePanelSurface{f}"" style=""position: absolute; left: 0; top: 0; right: 0; bottom: 0;{Join(PanelSurfaceStyle(panel, media))} overflow: hidden;"">
");

            ResolvedDialogueArea area = FindFreeArea(resolved, f);
            if (region != null && area != null)
            {
                var areaEl = new StringBuilder();
                areaEl.Append($@"    <ui:VisualElement style=""{AbsStyle(area.Rect.x - panelRect.x, area.Rect.y - panelRect.y, area.Rect.width, area.Rect.height)}{Join(SurfaceStyle(region.Background, region.Border, region.Opacity))} overflow: hidden;"">
");
                int slotCount = Mathf.Clamp(1 + Mathf.Clamp(region.PartitionLevel, 0, 2), 1, 3);
                for (int i = 0; i < slotCount; i++)
                {
                    ResolvedDialogueSlot slot = FindFreeSlot(resolved, f, i);
                    DialogueSlotDefinition slotDef = region.Slots != null && i < region.Slots.Count
                        ? region.Slots[i] : null;
                    if (slot == null || slotDef == null) continue;

                    var slotEl = new StringBuilder();
                    if (slotDef.Components != null)
                    {
                        for (int c = 0; c < slotDef.Components.Count; c++)
                        {
                            DialogueComponentDefinition comp = slotDef.Components[c];
                            ResolvedDialogueComponentRect compRect = FindFreeComponent(resolved, f, i, c);
                            if (comp == null || compRect == null) continue;
                            slotEl.Append(StaticComponentXml(comp, Relative(compRect.Rect, slot.Rect)));
                        }
                    }
                    areaEl.Append("    <ui:VisualElement style=\"" +
                        AbsStyle(slot.Rect.x - area.Rect.x, slot.Rect.y - area.Rect.y,
                            slot.Rect.width, slot.Rect.height) +
                        Join(SurfaceStyle(slotDef.Background, slotDef.Border, slotDef.Opacity)) +
                        " overflow: hidden;\">\n");
                    areaEl.Append(slotEl);
                    areaEl.Append("    </ui:VisualElement>\n");
                }
                areaEl.Append("    </ui:VisualElement>\n");
                sb.Append(areaEl);
            }

            sb.Append("  </ui:VisualElement>\n</ui:VisualElement>\n");
            all.Append(sb);
        }
        return all.ToString();
    }

    static ResolvedDialogueArea FindFreeArea(ResolvedDialogueLayout resolved, int panelIndex)
    {
        for (int i = 0; i < resolved.Areas.Count; i++)
            if (resolved.Areas[i].AreaKind == ResolvedDialogueAreaKind.FreeInner &&
                resolved.Areas[i].FreePanelIndex == panelIndex)
                return resolved.Areas[i];
        return null;
    }

    static ResolvedDialogueSlot FindFreeSlot(ResolvedDialogueLayout resolved, int panelIndex, int slotIndex)
    {
        for (int i = 0; i < resolved.Slots.Count; i++)
            if (resolved.Slots[i].AreaKind == ResolvedDialogueAreaKind.FreeInner &&
                resolved.Slots[i].FreePanelIndex == panelIndex &&
                resolved.Slots[i].SlotIndex == slotIndex)
                return resolved.Slots[i];
        return null;
    }

    static ResolvedDialogueComponentRect FindFreeComponent(ResolvedDialogueLayout resolved,
        int panelIndex, int slotIndex, int componentIndex)
    {
        for (int i = 0; i < resolved.Components.Count; i++)
            if (resolved.Components[i].AreaKind == ResolvedDialogueAreaKind.FreeInner &&
                resolved.Components[i].FreePanelIndex == panelIndex &&
                resolved.Components[i].SlotIndex == slotIndex &&
                resolved.Components[i].ComponentIndex == componentIndex)
                return resolved.Components[i];
        return null;
    }

    /// <summary>Joins an inline style fragment into a style attribute: ensures
    /// one leading space and a trailing semicolon (empty → empty).</summary>
    static string Join(string fragment)
    {
        if (string.IsNullOrEmpty(fragment)) return "";
        string t = fragment.Trim();
        if (t.Length == 0) return "";
        return " " + t + (t.EndsWith(";") ? "" : ";");
    }

    // ─── Static (non-live) components ──────────────────────────────────────────
    static string StaticComponentXml(DialogueComponentDefinition comp, Rect r)
    {
        string style = AbsStyle(r.x, r.y, r.width, r.height) +
            SurfaceStyle(comp.Background, comp.Border, comp.Opacity);

        DialogueTextPanelDefinition text = comp as DialogueTextPanelDefinition;
        if (text != null && text.TextStyle != null)
        {
            style += TextStyle(text.TextStyle);
            return $"<ui:Label text=\"\" style=\"{style}\" />\n";
        }
        DialogueNamePanelDefinition name = comp as DialogueNamePanelDefinition;
        if (name != null && name.TextStyle != null)
        {
            style += TextStyle(name.TextStyle);
            return $"<ui:Label text=\"\" style=\"{style}\" />\n";
        }
        return $"<ui:VisualElement style=\"{style}\" />\n";
    }

    // ─── Chrome ────────────────────────────────────────────────────────────────
    static string HistoryAndSettingsXml()
    {
        return
@"<ui:VisualElement name=""HistoryPanel"" style=""position: absolute; left: 10%; top: 15%; width: 80%; height: 70%; background-color: rgba(20, 20, 20, 0.95); border-radius: 8px; padding: 20px; display: none;"">
  <ui:ScrollView name=""HistoryContent"" style=""flex-grow: 1;"" />
  <ui:Button name=""CloseHistoryButton"" class=""dlg-close-button"" text=""Close History"" style=""margin-top: 10px;"" />
</ui:VisualElement>
<ui:VisualElement name=""SettingsPanel"" style=""position: absolute; left: 10%; top: 15%; width: 80%; height: 70%; background-color: rgba(20, 20, 20, 0.95); border-radius: 8px; padding: 20px; display: none;"">
  <ui:ScrollView name=""SettingsContent"" style=""flex-grow: 1;"" />
  <ui:Button name=""CloseSettingsButton"" class=""dlg-close-button"" text=""Close Settings"" style=""margin-top: 10px;"" />
</ui:VisualElement>
";
    }

    static string ToolbarXml(Dialogue_Engine e)
    {
        string tbPosition;
        string tbFlex;
        switch (e.toolbarSlideDirection)
        {
            case ToolbarSlideDirection.Top:   tbPosition = "top: 10px; right: 10px;";   tbFlex = "flex-direction: row;";    break;
            case ToolbarSlideDirection.Left:  tbPosition = "left: 10px; top: 10px;";    tbFlex = "flex-direction: column;"; break;
            case ToolbarSlideDirection.Right: tbPosition = "right: 10px; top: 10px;";   tbFlex = "flex-direction: column;"; break;
            default:                          tbPosition = "bottom: 10px; right: 10px;"; tbFlex = "flex-direction: row;";   break;
        }
        return
$@"<ui:Button name=""ToolbarToggle"" class=""dlg-toolbar-button"" text=""Menu"" style=""position: absolute; {tbPosition} {(e.showToolbar ? "" : "display: none;")}"" />
<ui:VisualElement name=""ToolbarPanel"" style=""position: absolute; {tbPosition} {tbFlex} display: none;"">
  <ui:Button name=""HistoryButton"" class=""dlg-toolbar-button"" text=""History"" />
  <ui:Button name=""SettingsButton"" class=""dlg-toolbar-button"" text=""Settings"" style=""{(e.showSettingsButton ? "" : "display: none;")}"" />
  <ui:Button name=""RewindButton"" class=""dlg-toolbar-button"" text=""Rewind"" />
</ui:VisualElement>
";
    }

    static void BuildUss(StringBuilder uss, Dialogue_Engine e, DialogueLayoutAsset asset)
    {
        Color baseBg = e.backgroundColour;
        Color hover = Color.Lerp(baseBg, Color.white, 0.08f);
        Color border = Color.Lerp(e.borderColour, Color.white, 0.35f);
        uss.AppendLine(".dlg-choice-button {");
        uss.AppendLine($"    background-color: {Rgba(baseBg)};");
        uss.AppendLine("    color: rgba(235, 235, 235, 0.95);");
        uss.AppendLine("    border-width: 1px;");
        uss.AppendLine($"    border-color: {RgbaOpaque(border)};");
        uss.AppendLine("    border-radius: 8px;");
        uss.AppendLine("    padding-top: 8px; padding-bottom: 8px; padding-left: 14px; padding-right: 14px;");
        uss.AppendLine("    -unity-text-align: middle-left;");
        uss.AppendLine("    transition-property: background-color, border-color;");
        uss.AppendLine("    transition-duration: 0.12s;");
        uss.AppendLine("}");
        uss.AppendLine($".dlg-choice-button:hover {{ background-color: {Rgba(hover)}; border-color: rgba(140, 191, 255, 1); }}");
        if (asset != null && asset.ChoiceButtons != null)
        {
            uss.AppendLine(".dlg-choice-btn {");
            uss.AppendLine("    transition-property: background-color, border-color;");
            uss.AppendLine("    transition-duration: 0.12s;");
            uss.AppendLine("}");
            uss.AppendLine($".dlg-choice-btn:hover {{ background-color: {Rgba(asset.ChoiceButtons.HoverBackground)}; }}");
        }
        uss.AppendLine(".dlg-choice-selected {");
        uss.AppendLine($"    background-color: {Rgba(Color.Lerp(baseBg, new Color(0.3f, 0.45f, 0.8f), 0.55f))};");
        uss.AppendLine("    border-color: rgba(166, 209, 255, 1);");
        uss.AppendLine("}");
        uss.AppendLine(".dlg-toolbar-button, .dlg-close-button {");
        uss.AppendLine($"    background-color: {Rgba(Color.Lerp(baseBg, Color.white, 0.05f))};");
        uss.AppendLine("    border-width: 1px;");
        uss.AppendLine($"    border-color: {RgbaOpaque(Color.Lerp(e.borderColour, Color.white, 0.25f))};");
        uss.AppendLine("    border-radius: 6px;");
        uss.AppendLine("    padding-top: 4px; padding-bottom: 4px; padding-left: 10px; padding-right: 10px;");
        uss.AppendLine("    margin-left: 4px; margin-right: 4px;");
        uss.AppendLine("}");
        uss.AppendLine(".dlg-toolbar-button:hover, .dlg-close-button:hover {");
        uss.AppendLine($"    background-color: {Rgba(hover)};");
        uss.AppendLine("    border-color: rgba(140, 191, 255, 1);");
        uss.AppendLine("}");
        uss.AppendLine(".dlg-history-entry {");
        uss.AppendLine("    padding-bottom: 6px;");
        uss.AppendLine("    margin-bottom: 6px;");
        uss.AppendLine("    border-bottom-width: 1px;");
        uss.AppendLine("    border-bottom-color: rgba(255, 255, 255, 0.08);");
        uss.AppendLine("}");
    }

    // ─── Style fragments ───────────────────────────────────────────────────────
    static string AbsStyle(float x, float y, float w, float h)
    {
        return $"position: absolute; left: {x:0.#}px; top: {y:0.#}px; width: {w:0.#}px; height: {h:0.#}px; ";
    }

    static string AbsEl(string name, string style, string inner)
    {
        string nameAttr = string.IsNullOrEmpty(name) ? "" : $" name=\"{name}\"";
        string children = string.IsNullOrEmpty(inner) ? " />" : $">\n{inner}</ui:VisualElement>\n";
        return $"<ui:VisualElement{nameAttr} style=\"{style}\"{children}";
    }

    static string SurfaceStyle(DialogueBackgroundStyle background,
        DialogueBorderStyle border, DialogueOpacitySettings opacity)
    {
        var sb = new StringBuilder();
        float alpha = opacity != null ? Mathf.Clamp01(opacity.Opacity) : 1f;

        if (background != null && background.Mode != DialogueBackgroundMode.None)
        {
            Color c = background.ColorA;
            sb.Append($"background-color: {Rgba(new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(background.Opacity) * alpha))}; ");
        }
        else
        {
            sb.Append("background-color: rgba(0,0,0,0); ");
        }

        if (border != null && border.Enabled)
        {
            Color bc = border.BorderColor;
            bc.a *= Mathf.Clamp01(border.Opacity);
            sb.Append($"border-color: {Rgba(bc)}; ");
            sb.Append($"border-left-width: {Mathf.Max(0f, border.LeftThickness):0.#}px; border-right-width: {Mathf.Max(0f, border.RightThickness):0.#}px; ");
            sb.Append($"border-top-width: {Mathf.Max(0f, border.TopThickness):0.#}px; border-bottom-width: {Mathf.Max(0f, border.BottomThickness):0.#}px; ");
            sb.Append($"border-top-left-radius: {Mathf.Max(0f, border.CornerRadiusTopLeft):0.#}px; border-top-right-radius: {Mathf.Max(0f, border.CornerRadiusTopRight):0.#}px; ");
            sb.Append($"border-bottom-left-radius: {Mathf.Max(0f, border.CornerRadiusBottomLeft):0.#}px; border-bottom-right-radius: {Mathf.Max(0f, border.CornerRadiusBottomRight):0.#}px; ");
        }
        else
        {
            sb.Append("border-width: 0; ");
        }
        return sb.ToString();
    }

    /// <summary>Frame styling for icon / figure portraits, shape-aware.</summary>
    static string FrameStyle(DialogueImagePanelDefinition def)
    {
        if (def == null)
            return " overflow: hidden; border-width: 0; border-radius: 0;";
        DialogueBorderStyle border = def.Border;
        float tl = 0f, tr = 0f, bl = 0f, br = 0f;
        bool hasBorder = border != null && border.Enabled;
        if (hasBorder)
        {
            tl = Mathf.Max(0f, border.CornerRadiusTopLeft);
            tr = Mathf.Max(0f, border.CornerRadiusTopRight);
            bl = Mathf.Max(0f, border.CornerRadiusBottomLeft);
            br = Mathf.Max(0f, border.CornerRadiusBottomRight);
        }
        if (def.Mode == DialogueImagePanelMode.Icon)
        {
            switch (def.Shape)
            {
                case DialogueIconShape.Circle:
                    // Diameter-based radius; approximated with the uniform scale size.
                    float d = 96f * Mathf.Max(0.1f, def.UniformScale);
                    tl = tr = bl = br = d * 0.5f;
                    break;
                case DialogueIconShape.Square:
                    tl = tr = bl = br = 0f;
                    break;
                case DialogueIconShape.Diamond:
                case DialogueIconShape.Hexagon:
                case DialogueIconShape.RoundedRectangle:
                default:
                    float avg = (tl + tr + bl + br) * 0.25f;
                    tl = tr = bl = br = avg;
                    break;
            }
        }

        var sb = new StringBuilder(" overflow: hidden;");
        if (hasBorder)
        {
            Color bc = border.BorderColor;
            bc.a *= Mathf.Clamp01(border.Opacity);
            sb.Append($" border-color: {Rgba(bc)};");
            sb.Append($" border-left-width: {Mathf.Max(0f, border.LeftThickness):0.#}px; border-right-width: {Mathf.Max(0f, border.RightThickness):0.#}px;");
            sb.Append($" border-top-width: {Mathf.Max(0f, border.TopThickness):0.#}px; border-bottom-width: {Mathf.Max(0f, border.BottomThickness):0.#}px;");
        }
        else
        {
            sb.Append(" border-width: 0;");
        }
        sb.Append($" border-top-left-radius: {tl:0.#}px; border-top-right-radius: {tr:0.#}px; border-bottom-left-radius: {bl:0.#}px; border-bottom-right-radius: {br:0.#}px;");
        return sb.ToString();
    }

    static string TextStyle(DialogueTextStyle style)
    {
        var sb = new StringBuilder();
        sb.Append($"color: {Rgba(style.Color)}; font-size: {Mathf.Max(1f, style.FontSize):0.#}px; ");
        sb.Append($"letter-spacing: {style.LetterSpacing:0.#}px; ");
        sb.Append("white-space: normal; ");
        sb.Append($"-unity-text-align: {TextAlign(style.HorizontalAlignment, style.VerticalAlignment)}; ");
        if (style.FontWeight == DialogueFontWeight.Bold || style.FontWeight == DialogueFontWeight.SemiBold)
            sb.Append("-unity-font-style: bold; ");
        return sb.ToString();
    }

    static string TextAlign(DialogueHorizontalAlignment h, DialogueVerticalAlignment v)
    {
        string vh = v == DialogueVerticalAlignment.Top ? "upper" : v == DialogueVerticalAlignment.Bottom ? "lower" : "middle";
        string hh = h == DialogueHorizontalAlignment.Left ? "left" : h == DialogueHorizontalAlignment.Right ? "right" : "center";
        return $"{vh}-{hh}";
    }

    // ─── Geometry helpers ──────────────────────────────────────────────────────
    static Rect Relative(Rect rect, Rect parent)
    {
        return new Rect(rect.x - parent.x, rect.y - parent.y, rect.width, rect.height);
    }

    static Rect DefaultIconNameRect(Rect icon, Dialogue_Engine engine)
    {
        float h = Mathf.Max(20f, engine.nameFontSize + 10f);
        return new Rect(icon.x, icon.y - h - 2f, icon.width, h);
    }

    static string Pct(float value, float total)
    {
        return $"{(value / Mathf.Max(1f, total) * 100f):0.###}%";
    }

    // ─── Lookups (runtime-safe, no editor utility) ─────────────────────────────
    static DialogueAttachedAreaDefinition GetArea(DialogueLayoutAsset asset, ResolvedDialogueAreaKind kind)
    {
        if (asset == null) return null;
        switch (kind)
        {
            case ResolvedDialogueAreaKind.Top: return asset.TopArea;
            case ResolvedDialogueAreaKind.Bottom: return asset.BottomArea;
            case ResolvedDialogueAreaKind.Left: return asset.LeftArea;
            case ResolvedDialogueAreaKind.Right: return asset.RightArea;
            default: return null;
        }
    }

    static List<DialogueSlotDefinition> GetSlots(DialogueLayoutAsset asset, ResolvedDialogueAreaKind kind)
    {
        if (asset == null) return null;
        if (kind == ResolvedDialogueAreaKind.MainInner)
            return asset.MainPanel != null && asset.MainPanel.InnerRegion != null
                ? asset.MainPanel.InnerRegion.Slots : null;
        DialogueAttachedAreaDefinition area = GetArea(asset, kind);
        return area != null ? area.Slots : null;
    }

    static void CollectComponents<T>(DialogueLayoutAsset asset, List<T> sink) where T : DialogueComponentDefinition
    {
        if (asset == null) return;
        CollectInSlots(asset.MainPanel != null && asset.MainPanel.InnerRegion != null
            ? asset.MainPanel.InnerRegion.Slots : null, sink);
        CollectInSlots(asset.TopArea != null ? asset.TopArea.Slots : null, sink);
        CollectInSlots(asset.BottomArea != null ? asset.BottomArea.Slots : null, sink);
        CollectInSlots(asset.LeftArea != null ? asset.LeftArea.Slots : null, sink);
        CollectInSlots(asset.RightArea != null ? asset.RightArea.Slots : null, sink);
    }

    static void CollectInSlots<T>(List<DialogueSlotDefinition> slots, List<T> sink) where T : DialogueComponentDefinition
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
        {
            DialogueSlotDefinition slot = slots[i];
            if (slot == null || slot.Components == null) continue;
            for (int c = 0; c < slot.Components.Count; c++)
                if (slot.Components[c] is T match) sink.Add(match);
        }
    }

    static T FindFirstComponent<T>(DialogueLayoutAsset asset) where T : DialogueComponentDefinition
    {
        if (asset == null) return null;
        T found = FindInSlots<T>(asset.MainPanel != null && asset.MainPanel.InnerRegion != null
            ? asset.MainPanel.InnerRegion.Slots : null);
        if (found != null) return found;
        found = FindInSlots<T>(asset.TopArea != null ? asset.TopArea.Slots : null);
        if (found != null) return found;
        found = FindInSlots<T>(asset.BottomArea != null ? asset.BottomArea.Slots : null);
        if (found != null) return found;
        found = FindInSlots<T>(asset.LeftArea != null ? asset.LeftArea.Slots : null);
        if (found != null) return found;
        return FindInSlots<T>(asset.RightArea != null ? asset.RightArea.Slots : null);
    }

    static T FindInSlots<T>(List<DialogueSlotDefinition> slots) where T : DialogueComponentDefinition
    {
        if (slots == null) return null;
        for (int i = 0; i < slots.Count; i++)
        {
            DialogueSlotDefinition slot = slots[i];
            if (slot == null || slot.Components == null) continue;
            for (int c = 0; c < slot.Components.Count; c++)
                if (slot.Components[c] is T match) return match;
        }
        return null;
    }

    static ResolvedDialogueSlot FindSlot(ResolvedDialogueLayout resolved,
        ResolvedDialogueAreaKind kind, int index)
    {
        for (int i = 0; i < resolved.Slots.Count; i++)
            if (resolved.Slots[i].AreaKind == kind && resolved.Slots[i].SlotIndex == index)
                return resolved.Slots[i];
        return null;
    }

    static ResolvedDialogueComponentRect FindComponent(ResolvedDialogueLayout resolved,
        ResolvedDialogueAreaKind kind, int slotIndex, int componentIndex)
    {
        for (int i = 0; i < resolved.Components.Count; i++)
        {
            ResolvedDialogueComponentRect c = resolved.Components[i];
            if (c.AreaKind == kind && c.SlotIndex == slotIndex && c.ComponentIndex == componentIndex)
                return c;
        }
        return null;
    }

    static Rect FindComponentRect(ResolvedDialogueLayout resolved, DialogueLayoutAsset asset,
        DialogueComponentDefinition component)
    {
        if (asset == null) return Rect.zero;

        for (int i = 0; i < resolved.Components.Count; i++)
        {
            ResolvedDialogueComponentRect c = resolved.Components[i];
            List<DialogueSlotDefinition> slots = GetSlots(asset, c.AreaKind);
            if (slots == null || c.SlotIndex >= slots.Count || slots[c.SlotIndex] == null) continue;
            DialogueSlotDefinition slot = slots[c.SlotIndex];
            if (slot.Components == null || c.ComponentIndex >= slot.Components.Count) continue;
            if (ReferenceEquals(slot.Components[c.ComponentIndex], component))
                return c.Rect;
        }
        return Rect.zero;
    }

    // ─── Formatting helpers ────────────────────────────────────────────────────
    static string Rgba(Color c)
    {
        return $"rgba({(int)(c.r * 255f)}, {(int)(c.g * 255f)}, {(int)(c.b * 255f)}, {Mathf.Clamp01(c.a):0.###})";
    }

    static string RgbaOpaque(Color c)
    {
        return $"rgba({(int)(c.r * 255f)}, {(int)(c.g * 255f)}, {(int)(c.b * 255f)}, 1)";
    }

    static string Escape(string s)
    {
        return string.IsNullOrEmpty(s) ? "" : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;");
    }
}
// ─── Image-based panel media import ─────────────────────────────────────────
/// <summary>
/// Copies image-based-panel source images into the project (dialogue_ui_media/
/// next to the canonical UXML) and returns project: URL style fragments, so
/// the runtime UXML and the True Preview render the images with no runtime
/// file access. Cached per source path; recopied when the source is newer.
/// </summary>
sealed class DialogueUiMediaImport
{
    readonly string folder;
    readonly Dictionary<string, string> cache = new Dictionary<string, string>();

    public DialogueUiMediaImport(string canonicalUxmlPath)
    {
        folder = string.IsNullOrEmpty(canonicalUxmlPath)
            ? "Assets/dialogue_ui_media"
            : Path.Combine(Path.GetDirectoryName(canonicalUxmlPath), "dialogue_ui_media");
    }

    public string ImageStyleFor(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return "";
        if (cache.TryGetValue(sourcePath, out string cached)) return cached;
        try
        {
            Directory.CreateDirectory(folder);
            string ext = Path.GetExtension(sourcePath);
            byte[] hash = System.Security.Cryptography.SHA256.Create()
                .ComputeHash(System.Text.Encoding.UTF8.GetBytes(sourcePath));
            string tag = System.BitConverter.ToString(hash).Replace("-", "").Substring(0, 8);
            string dest = Path.Combine(folder,
                Path.GetFileNameWithoutExtension(sourcePath) + "_" + tag + ext).Replace('\\', '/');
            if (!File.Exists(dest) || File.GetLastWriteTimeUtc(dest) < File.GetLastWriteTimeUtc(sourcePath))
                File.Copy(sourcePath, dest, true);
            AssetDatabase.ImportAsset(dest);
            string style = "background-image: url('project:" + dest + "'); background-size: 100% 100%; ";
            cache[sourcePath] = style;
            return style;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Dialogue visual editor: failed to import panel image '" +
                sourcePath + "': " + ex.Message);
            cache[sourcePath] = "";
            return "";
        }
    }
}
#endif
