#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

[CustomEditor(typeof(Dialogue_Engine))]
public class DialogueEngineEditor : Editor
{
    // ─── Foldout state (remembered per-session) ───────────────────────────────
    static bool showBackgroundSection = true;
    static bool showBorderSection     = true;
    static bool showNameSection       = true;
    static bool showTextSection       = true;
    static bool showPortraitSection   = true;
    static bool showCharacterPanelSection = true;
    static bool showHintSection       = true;
    static bool showToolbarSection    = true;
    static bool showPortraitsFoldout  = true;
    static bool showDirtyFoldout      = true;
    static bool showPresetFoldout     = true;

    public override void OnInspectorGUI()
    {
        Dialogue_Engine e = (Dialogue_Engine)target;
        serializedObject.Update();

        // ── Preset ─────────────────────────────────────────────────────────────
        showPresetFoldout = EditorGUILayout.Foldout(showPresetFoldout, "Preset", true);
        if (showPresetFoldout)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "Leave the preset name empty to use the fields below (the layout is generated automatically on Play).\n" +
                "Or pick a saved preset from " + Dialogue_Engine.PRESETS_PATH + " — it is used as-is at play time.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            e.presetName = EditorGUILayout.TextField("Preset Name", e.presetName);
            if (GUILayout.Button("…", GUILayout.Width(26)))
            {
                string dir = Dialogue_Engine.PRESETS_PATH;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string picked = EditorUtility.OpenFilePanel("Select Preset", dir, "uxml");
                if (!string.IsNullOrEmpty(picked))
                    e.presetName = Path.GetFileNameWithoutExtension(picked);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f, 1f);
            if (GUILayout.Button("Save As Preset…", GUILayout.Height(26)))
            {
                string dir = Dialogue_Engine.PRESETS_PATH;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string picked = EditorUtility.SaveFilePanel("Save Dialogue Preset", dir, "dialogue_preset.uxml", "uxml");
                if (!string.IsNullOrEmpty(picked))
                {
                    DialogueLayoutBuilder.SaveAsPreset(e, picked);
                    EditorUtility.SetDirty(e);
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Load Preset…", GUILayout.Height(26)))
            {
                string dir = Dialogue_Engine.PRESETS_PATH;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string picked = EditorUtility.OpenFilePanel("Load Dialogue Preset", dir, "uxml");
                if (!string.IsNullOrEmpty(picked))
                {
                    DialogueLayoutBuilder.LoadPreset(e, picked);
                    EditorUtility.SetDirty(e);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);

        // ── Panel ──────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Panel", EditorStyles.boldLabel);
        e.panelSettings = (UnityEngine.UIElements.PanelSettings)EditorGUILayout.ObjectField(
            "Panel Settings", e.panelSettings, typeof(UnityEngine.UIElements.PanelSettings), false);

        EditorGUILayout.LabelField("Size & Position", EditorStyles.miniBoldLabel);
        e.panelWidthMode  = (PanelSizeMode)EditorGUILayout.EnumPopup("Width Mode",  e.panelWidthMode);
        e.panelWidthValue = EditorGUILayout.Slider("Width Value",  e.panelWidthValue, 1f, e.panelWidthMode == PanelSizeMode.Percent ? 100f : 4000f);
        e.panelHeightMode = (PanelSizeMode)EditorGUILayout.EnumPopup("Height Mode", e.panelHeightMode);
        e.panelHeightValue = EditorGUILayout.Slider("Height Value", e.panelHeightValue, 1f, e.panelHeightMode == PanelSizeMode.Percent ? 100f : 4000f);
        e.panelOffsetX = EditorGUILayout.Slider("Offset X (px)", e.panelOffsetX, -500f, 500f);
        e.panelOffsetY = EditorGUILayout.Slider("Offset Y (px)", e.panelOffsetY, -500f, 500f);

        if (e.padding == null) e.padding = new RectOffset(28, 28, 20, 20);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Padding (L R T B)", GUILayout.Width(140));
        e.padding.left   = EditorGUILayout.IntField(e.padding.left,   GUILayout.Width(40));
        e.padding.right  = EditorGUILayout.IntField(e.padding.right,  GUILayout.Width(40));
        e.padding.top    = EditorGUILayout.IntField(e.padding.top,    GUILayout.Width(40));
        e.padding.bottom = EditorGUILayout.IntField(e.padding.bottom, GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // ── Background ─────────────────────────────────────────────────────────
        showBackgroundSection = EditorGUILayout.Foldout(showBackgroundSection, "Background", true);
        if (showBackgroundSection)
        {
            EditorGUI.indentLevel++;
            e.backgroundMode = (BackgroundMode)EditorGUILayout.EnumPopup("Mode", e.backgroundMode);
            if (e.backgroundMode == BackgroundMode.Colour)
            {
                e.backgroundColour = EditorGUILayout.ColorField("Colour", e.backgroundColour);
            }
            else
            {
                DrawTiledImageSettings("Background Image", e.backgroundImage);
                EditorGUILayout.HelpBox(
                    "Image backgrounds are drawn behind the text: tiled or stretched, and optionally looping or animating (scroll direction + speed).",
                    MessageType.None);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Box Border ─────────────────────────────────────────────────────────
        showBorderSection = EditorGUILayout.Foldout(showBorderSection, "Box Border", true);
        if (showBorderSection)
        {
            EditorGUI.indentLevel++;
            bool hasBorderImage = e.borderImage != null &&
                (e.borderImage.sprite != null || !string.IsNullOrEmpty(e.borderImage.path));

            // Border colour OR border image — the image wins, so the colour
            // field is locked while an image is loaded. Border colours are
            // always fully opaque (no alpha) — translucent borders read as
            // greyed out on dark panels.
            GUI.enabled = !hasBorderImage;
            e.borderColour = EditorGUILayout.ColorField(new GUIContent("Colour"), e.borderColour, true, false, false);
            GUI.enabled = true;

            e.borderWidth   = EditorGUILayout.Slider("Width",            e.borderWidth,    0f, 8f);
            e.borderRadiusTL= EditorGUILayout.Slider("Radius Top-Left",  e.borderRadiusTL, 0f, 32f);
            e.borderRadiusTR= EditorGUILayout.Slider("Radius Top-Right", e.borderRadiusTR, 0f, 32f);
            e.borderRadiusBL= EditorGUILayout.Slider("Radius Bot-Left",  e.borderRadiusBL, 0f, 32f);
            e.borderRadiusBR= EditorGUILayout.Slider("Radius Bot-Right", e.borderRadiusBR, 0f, 32f);
            if (GUILayout.Button("Sync All Radii", GUILayout.Height(20)))
            {
                e.borderRadiusTR = e.borderRadiusBL = e.borderRadiusBR = e.borderRadiusTL;
            }
            DrawTiledImageSettings("Border Image", e.borderImage);
            EditorGUILayout.HelpBox(
                "A border image is drawn only inside the border ring (capped inside the border). " +
                "While an image is loaded, the border colour is disabled (image OR colour). " +
                "The Colour Tint multiplies the image's pixels (white = untouched).",
                MessageType.None);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Speaker Name ───────────────────────────────────────────────────────
        showNameSection = EditorGUILayout.Foldout(showNameSection, "Speaker Name", true);
        if (showNameSection)
        {
            EditorGUI.indentLevel++;
            e.nameColour   = EditorGUILayout.ColorField("Colour",    e.nameColour);
            e.nameFontSize = EditorGUILayout.IntSlider("Font Size",  e.nameFontSize, 8, 64);
            e.nameUppercase= EditorGUILayout.Toggle("Uppercase",     e.nameUppercase);
            e.nameFont     = (Font)EditorGUILayout.ObjectField("Font", e.nameFont, typeof(Font), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Position (relative to portrait)", EditorStyles.miniBoldLabel);
            e.namePosition = (NamePosition)EditorGUILayout.EnumPopup("Name Position", e.namePosition);
            e.nameDistance = EditorGUILayout.Slider("Distance from Image", e.nameDistance, 0f, 64f);

            EditorGUILayout.Space(4);
            DrawLetterBehaviour("Letter Behaviour", ref e.nameLetterMode,
                                ref e.nameLetterAmplitude, ref e.nameLetterFrequency, ref e.nameLetterSpacing);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Dialogue Text ──────────────────────────────────────────────────────
        showTextSection = EditorGUILayout.Foldout(showTextSection, "Dialogue Text", true);
        if (showTextSection)
        {
            EditorGUI.indentLevel++;
            e.textColour   = EditorGUILayout.ColorField("Colour",   e.textColour);
            e.textFontSize = EditorGUILayout.IntSlider("Font Size", e.textFontSize, 8, 64);
            e.textFont     = (Font)EditorGUILayout.ObjectField("Font", e.textFont, typeof(Font), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Text Anchoring (VN-style)", EditorStyles.miniBoldLabel);
            e.textVAnchor = (TextVAnchor)EditorGUILayout.EnumPopup("Vertical Anchor", e.textVAnchor);
            e.textHAnchor = (TextHAnchor)EditorGUILayout.EnumPopup("Horizontal Anchor", e.textHAnchor);

            DrawLetterBehaviour("Letter Behaviour", ref e.textLetterMode,
                                ref e.textLetterAmplitude, ref e.textLetterFrequency, ref e.textLetterSpacing);
            EditorGUILayout.HelpBox(
                "The text panel itself is transparent; the letters are opaque. " +
                "Long text scrolls vertically inside the panel. Default anchoring " +
                "(Center + Left) is the classic visual-novel look.",
                MessageType.None);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Typewriter ─────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Typewriter", EditorStyles.boldLabel);
        e.enableTypewriter = EditorGUILayout.Toggle("Enable",  e.enableTypewriter);
        if (e.enableTypewriter)
            e.typewriterSpeed = EditorGUILayout.Slider("Speed", e.typewriterSpeed, 0.005f, 0.1f);

        EditorGUILayout.Space(4);

        // ── Portrait ───────────────────────────────────────────────────────────
        showPortraitSection = EditorGUILayout.Foldout(showPortraitSection, "Portrait", true);
        if (showPortraitSection)
        {
            EditorGUI.indentLevel++;
            e.showPortrait       = EditorGUILayout.Toggle("Enable",        e.showPortrait);
            if (e.showPortrait)
            {
                e.portraitMode      = (PortraitMode)EditorGUILayout.EnumPopup("Mode (uni / duel)", e.portraitMode);
                if (e.portraitMode != PortraitMode.None)
                {
                    e.portraitPlacement = (PortraitPlacement)EditorGUILayout.EnumPopup("Placement", e.portraitPlacement);
                    e.portraitShape     = (PortraitShape)EditorGUILayout.EnumPopup("Shape",     e.portraitShape);
                    e.portraitDisplayType = (PortraitDisplayType)EditorGUILayout.EnumPopup("Display Type", e.portraitDisplayType);
                    e.portraitFillMode  = (PortraitFillMode)EditorGUILayout.EnumPopup("Image Fit", e.portraitFillMode);
                    e.portraitSize      = EditorGUILayout.Slider("Size",       e.portraitSize, 48f, 512f);
                    e.dynamicPortraitSize = EditorGUILayout.Toggle("Dynamic Size", e.dynamicPortraitSize);
                    if (e.dynamicPortraitSize)
                        e.maxPortraitSize = EditorGUILayout.Slider("Max Size",  e.maxPortraitSize, 48f, 512f);
                    e.portraitOffsetX = EditorGUILayout.Slider("Offset X (px)", e.portraitOffsetX, -300f, 300f);
                    e.portraitOffsetY = EditorGUILayout.Slider("Offset Y (px)", e.portraitOffsetY, -300f, 300f);
                    e.showPortraitWhenEmpty = EditorGUILayout.Toggle("Show When Empty", e.showPortraitWhenEmpty);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Portrait Border", EditorStyles.miniBoldLabel);
                    e.showPortraitBorder   = EditorGUILayout.Toggle("Enable Border",   e.showPortraitBorder);
                    if (e.showPortraitBorder)
                    {
                        bool hasPBorderImage = e.portraitBorderImage != null &&
                            (e.portraitBorderImage.sprite != null || !string.IsNullOrEmpty(e.portraitBorderImage.path));
                        GUI.enabled = !hasPBorderImage;
                        e.portraitBorderColour = EditorGUILayout.ColorField(new GUIContent("Colour"), e.portraitBorderColour, true, false, false);
                        GUI.enabled = true;
                        e.portraitBorderWidth  = EditorGUILayout.Slider("Width",       e.portraitBorderWidth,  0f, 8f);
                        e.portraitBorderRadius = EditorGUILayout.Slider("Radius",      e.portraitBorderRadius, 0f, 32f);
                    }
                    DrawTiledImageSettings("Border Image", e.portraitBorderImage);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Opacity (duel mode)", EditorStyles.miniBoldLabel);
                    e.activePortraitOpacity   = EditorGUILayout.Slider("Active",   e.activePortraitOpacity,   0f, 1f);
                    e.inactivePortraitOpacity = EditorGUILayout.Slider("Inactive", e.inactivePortraitOpacity, 0f, 1f);
                    e.inactiveTintColour      = EditorGUILayout.ColorField("Inactive Tint", e.inactiveTintColour);
                    EditorGUILayout.HelpBox(
                        "Duel mode shows two portraits: the active speaker at full opacity and the " +
                        "previous speaker greyed out (tint + opacity editable above).",
                        MessageType.None);
                }
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Character Figure Panel ─────────────────────────────────────────────
        showCharacterPanelSection = EditorGUILayout.Foldout(showCharacterPanelSection, "Character Panel (figure panel)", true);
        if (showCharacterPanelSection)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "The figure panel sits OUTSIDE the main panel ([figure panel] [main panel]). " +
                "It is segmented into an image panel and a name panel, both fully customizable.",
                MessageType.None);

            e.characterPanelShowImagePanel = EditorGUILayout.Toggle("Show Image Panel", e.characterPanelShowImagePanel);
            e.characterPanelShowNamePanel  = EditorGUILayout.Toggle("Show Name Panel",  e.characterPanelShowNamePanel);
            e.characterPanelOrder = (CharacterPanelOrder)EditorGUILayout.EnumPopup("Panel Order", e.characterPanelOrder);
            if (e.characterPanelOrder == CharacterPanelOrder.ImageTop || e.characterPanelOrder == CharacterPanelOrder.NameTop)
                e.characterPanelWidth = EditorGUILayout.Slider("Panel Width (px)", e.characterPanelWidth, 80f, 600f);
            e.characterPanelSpacing = EditorGUILayout.Slider("Panel Spacing (px)", e.characterPanelSpacing, 0f, 32f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Outer Panel", EditorStyles.miniBoldLabel);
            e.characterPanelBg = EditorGUILayout.ColorField("Background", e.characterPanelBg);
            e.characterPanelBorderColour = EditorGUILayout.ColorField(new GUIContent("Border Colour"), e.characterPanelBorderColour, true, false, false);
            e.characterPanelBorderWidth  = EditorGUILayout.Slider("Border Width",  e.characterPanelBorderWidth, 0f, 8f);
            e.characterPanelRadius       = EditorGUILayout.Slider("Radius",        e.characterPanelRadius, 0f, 32f);
            DrawRectOffset("Padding (L R T B)", ref e.characterPanelPadding);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Image Panel", EditorStyles.miniBoldLabel);
            e.characterImagePanelBg = EditorGUILayout.ColorField("Background", e.characterImagePanelBg);
            e.characterImagePanelBorderColour = EditorGUILayout.ColorField(new GUIContent("Border Colour"), e.characterImagePanelBorderColour, true, false, false);
            e.characterImagePanelBorderWidth  = EditorGUILayout.Slider("Border Width",  e.characterImagePanelBorderWidth, 0f, 8f);
            e.characterImagePanelRadius       = EditorGUILayout.Slider("Radius",        e.characterImagePanelRadius, 0f, 32f);
            DrawRectOffset("Padding (L R T B)", ref e.characterImagePanelPadding);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Name Panel", EditorStyles.miniBoldLabel);
            e.characterNamePanelBg = EditorGUILayout.ColorField("Background", e.characterNamePanelBg);
            e.characterNamePanelBorderColour = EditorGUILayout.ColorField(new GUIContent("Border Colour"), e.characterNamePanelBorderColour, true, false, false);
            e.characterNamePanelBorderWidth  = EditorGUILayout.Slider("Border Width",  e.characterNamePanelBorderWidth, 0f, 8f);
            e.characterNamePanelRadius       = EditorGUILayout.Slider("Radius",        e.characterNamePanelRadius, 0f, 32f);
            DrawRectOffset("Padding (L R T B)", ref e.characterNamePanelPadding);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Default Portrait Placeholder ───────────────────────────────────────
        e.useDefaultPortraitPlaceholder = EditorGUILayout.Toggle("Use Default Portrait Placeholder", e.useDefaultPortraitPlaceholder);
        if (e.useDefaultPortraitPlaceholder)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "Shown when a character has no portrait image: a shaded unidentified-character " +
                "silhouette by default, or your own sprite / image file.",
                MessageType.None);
            e.defaultPortraitSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", e.defaultPortraitSprite, typeof(Sprite), false);
            EditorGUILayout.BeginHorizontal();
            e.defaultPortraitPath = EditorGUILayout.TextField("File Path", e.defaultPortraitPath);
            if (GUILayout.Button("Browse…", GUILayout.Width(70)))
            {
                string picked = EditorUtility.OpenFilePanel("Select Placeholder Image", "", "png,jpg,jpeg,tga,bmp,psd");
                if (!string.IsNullOrEmpty(picked))
                {
                    e.defaultPortraitPath  = picked;
                    e.defaultPortraitSprite = null;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Interaction ────────────────────────────────────────────────────────
        e.clickToAdvance = EditorGUILayout.Toggle("Click To Advance", e.clickToAdvance);
        EditorGUILayout.HelpBox(
            "Space / click → advance (or complete typing). Ctrl held → fast typewriter. " +
            "Choices: ↑/↓ to highlight, Enter to confirm, or click.",
            MessageType.None);

        EditorGUILayout.Space(4);

        // ── Advance Hint ───────────────────────────────────────────────────────
        showHintSection = EditorGUILayout.Foldout(showHintSection, "Advance Hint", true);
        if (showHintSection)
        {
            EditorGUI.indentLevel++;
            e.showAdvanceHint = EditorGUILayout.Toggle("Enable",    e.showAdvanceHint);
            if (e.showAdvanceHint)
            {
                e.advanceHintText = EditorGUILayout.TextField("Text",       e.advanceHintText);
                e.hintColour      = EditorGUILayout.ColorField("Colour",    e.hintColour);
                e.hintFontSize    = EditorGUILayout.IntSlider("Font Size",  e.hintFontSize, 6, 24);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Toolbar & History ──────────────────────────────────────────────────
        showToolbarSection = EditorGUILayout.Foldout(showToolbarSection, "Toolbar & History", true);
        if (showToolbarSection)
        {
            EditorGUI.indentLevel++;
            e.showToolbar           = EditorGUILayout.Toggle("Enable", e.showToolbar);
            if (e.showToolbar)
            {
                e.toolbarSlideDirection = (ToolbarSlideDirection)EditorGUILayout.EnumPopup("Slide From", e.toolbarSlideDirection);
                e.showSettingsButton    = EditorGUILayout.Toggle("Settings Button", e.showSettingsButton);
            }
            EditorGUILayout.HelpBox(
                "History shows the dialogue transcript. Settings shows a live summary of this " +
                "dialogue instance's configuration.",
                MessageType.None);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);

        // ── Unresolved portraits ───────────────────────────────────────────────
        if (e.portraits != null && e.portraits.Count > 0)
        {
            showPortraitsFoldout = EditorGUILayout.Foldout(showPortraitsFoldout, $"Unresolved Portraits ({e.portraits.Count})", true);
            if (showPortraitsFoldout)
            {
                EditorGUILayout.HelpBox(
                    "These portrait keys were not defined in the DSL's variable section. " +
                    "Drag a Sprite in, type a file path, or Browse for an image file.",
                    MessageType.Warning);

                for (int i = 0; i < e.portraits.Count; i++)
                {
                    var entry = e.portraits[i];
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"Key: \"{entry.key}\"", EditorStyles.boldLabel);
                    entry.sprite = (Sprite)EditorGUILayout.ObjectField("Sprite", entry.sprite, typeof(Sprite), false);

                    EditorGUILayout.BeginHorizontal();
                    entry.path = EditorGUILayout.TextField("File Path", entry.path);
                    if (GUILayout.Button("Browse…", GUILayout.Width(70)))
                    {
                        string picked = EditorUtility.OpenFilePanel("Select Portrait Image", "", "png,jpg,jpeg,tga,bmp,psd");
                        if (!string.IsNullOrEmpty(picked))
                        {
                            entry.path   = picked;
                            entry.sprite = null;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    // Clear this entry
                    if (GUILayout.Button("Clear", GUILayout.Width(70)))
                    {
                        entry.sprite = null;
                        entry.path   = "";
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                if (GUILayout.Button("Clear Portrait List", GUILayout.Height(22)))
                {
                    e.portraits.Clear();
                    EditorUtility.SetDirty(e);
                }
            }
            EditorGUILayout.Space(6);
        }

        // ── Dirty scripts ──────────────────────────────────────────────────────
        if (e.dirtyScripts != null && e.dirtyScripts.Count > 0)
        {
            showDirtyFoldout = EditorGUILayout.Foldout(showDirtyFoldout, $"Dirty Scripts ({e.dirtyScripts.Count})", true);
            if (showDirtyFoldout)
            {
                EditorGUILayout.HelpBox(
                    "These scripts compiled with warnings (portrait placeholders that are not defined in the DSL). " +
                    "Fill their image sources above; once a script compiles with 0 warnings it is removed from this list.",
                    MessageType.Info);

                foreach (var dirty in e.dirtyScripts)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField(Path.GetFileName(dirty.path), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(dirty.path, EditorStyles.miniLabel);
                    if (dirty.unresolvedKeys != null && dirty.unresolvedKeys.Count > 0)
                        EditorGUILayout.LabelField("Missing keys: " + string.Join(", ", dirty.unresolvedKeys), EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                if (GUILayout.Button("Clear Dirty List", GUILayout.Height(22)))
                {
                    e.dirtyScripts.Clear();
                    EditorUtility.SetDirty(e);
                }
            }
            EditorGUILayout.Space(6);
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // ── Buttons ────────────────────────────────────────────────────────────
        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f, 1f);
        if (GUILayout.Button("Open Layout Preview", GUILayout.Height(30)))
            DialoguePreviewWindow.Open(e);
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f, 1f);
        if (GUILayout.Button("Build Layout  (saves UXML)", GUILayout.Height(28)))
        {
            DialogueLayoutBuilder.Build(e);
            EditorUtility.SetDirty(e);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "1. Customize fields above.\n" +
            "2. Hit Play — the layout is generated automatically (or click 'Build Layout').\n" +
            "3. 'Save As Preset' stores everything as a .uxml + .json in Dialogue_Presets.",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
        if (GUI.changed) EditorUtility.SetDirty(e);
    }

    // ─── Letter behaviour drawer ───────────────────────────────────────────────
    static void DrawLetterBehaviour(string label, ref LetterMode mode,
                                    ref float amplitude, ref float frequency, ref float spacing)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        mode = (LetterMode)EditorGUILayout.EnumPopup("Mode", mode);
        if (mode != LetterMode.Normal)
        {
            amplitude = EditorGUILayout.Slider("Amplitude (px)", amplitude, 0f, 48f);
            frequency = EditorGUILayout.Slider("Frequency",      frequency, 0.05f, 3f);
        }
        spacing = EditorGUILayout.Slider("Letter Spacing (px)", spacing, -8f, 32f);
    }

    // ─── RectOffset drawer (L R T B) ───────────────────────────────────────────
    static void DrawRectOffset(string label, ref RectOffset r)
    {
        if (r == null) r = new RectOffset(8, 8, 8, 8);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(140));
        r.left   = EditorGUILayout.IntField(r.left,   GUILayout.Width(40));
        r.right  = EditorGUILayout.IntField(r.right,  GUILayout.Width(40));
        r.top    = EditorGUILayout.IntField(r.top,    GUILayout.Width(40));
        r.bottom = EditorGUILayout.IntField(r.bottom, GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();
    }

    // ─── Tiled image settings drawer ───────────────────────────────────────────
    static void DrawTiledImageSettings(string label, TiledImageSettings s)
    {
        if (s == null) return;
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

        EditorGUI.BeginChangeCheck();
        s.sprite = (Sprite)EditorGUILayout.ObjectField("Sprite", s.sprite, typeof(Sprite), false);
        if (EditorGUI.EndChangeCheck() && s.sprite != null)
            s.path = "";

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        s.path = EditorGUILayout.TextField("File Path", s.path);
        if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(s.path))
            s.sprite = null;
        if (GUILayout.Button("Browse…", GUILayout.Width(70)))
        {
            string picked = EditorUtility.OpenFilePanel("Select Image File", "", "png,jpg,jpeg,tga,bmp,psd");
            if (!string.IsNullOrEmpty(picked))
            {
                s.path   = picked;
                s.sprite = null;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (s.sprite != null || !string.IsNullOrEmpty(s.path))
        {
            s.tintEnabled = EditorGUILayout.Toggle("Colour Tint", s.tintEnabled);
            if (s.tintEnabled)
                s.tintColour = EditorGUILayout.ColorField("Tint Colour", s.tintColour);

            s.scaleMode = (ImageScaleMode)EditorGUILayout.EnumPopup("Scale Mode", s.scaleMode);
            if (s.scaleMode == ImageScaleMode.Tile)
                s.tileScale = EditorGUILayout.Slider("Tile Scale", s.tileScale, 0.1f, 8f);
            s.animate = EditorGUILayout.Toggle("Animate", s.animate);
            if (s.animate)
            {
                s.animDirection = (TiledAnimDirection)EditorGUILayout.EnumPopup("Direction", s.animDirection);
                s.animSpeed     = EditorGUILayout.Slider("Speed (px/s)", s.animSpeed, 1f, 400f);
                s.loop          = EditorGUILayout.Toggle("Loop", s.loop);
            }
        }
    }
}
#endif

