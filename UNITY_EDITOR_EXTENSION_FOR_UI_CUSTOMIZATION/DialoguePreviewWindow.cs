#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class DialoguePreviewWindow : EditorWindow
{
    Dialogue_Engine target;

    string previewSpeakerA = "SYSTEM_1";
    string previewSpeakerB = "SYSTEM_2";
    string previewText     = "Always has been. Welcome to the dialogue system.";

    Vector2 scroll;
    int   previewTab = 0; // 0 = ui, 1 = edit fields

    public static void Open(Dialogue_Engine engine)
    {
        var window = GetWindow<DialoguePreviewWindow>("Dialogue Preview");
        window.target  = engine;
        window.minSize = new Vector2(560, 480);
        window.Show();
    }

    void OnGUI()
    {
        if (target == null)
        {
            EditorGUILayout.HelpBox("No Dialogue_Engine target. Close and reopen from the inspector.", MessageType.Warning);
            return;
        }

        previewTab = GUILayout.Toolbar(previewTab, new[] { "Preview", "Sample Content & Quick Edit" });
        EditorGUILayout.Space(4);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        switch (previewTab)
        {
            case 0: DrawPreviewCanvas(); break;
            case 1: DrawQuickEdit();     break;
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
            Repaint();
        }
    }

    // ─── Quick edit (subset of the inspector, mirroring the old window) ───────
    void DrawQuickEdit()
    {
        GUILayout.Label("Sample Content", EditorStyles.boldLabel);
        previewSpeakerA = EditorGUILayout.TextField("Active Speaker",   previewSpeakerA);
        previewSpeakerB = EditorGUILayout.TextField("Inactive Speaker", previewSpeakerB);
        previewText     = EditorGUILayout.TextField("Dialogue Text",    previewText);

        EditorGUILayout.Space(8);

        GUILayout.Label("Box", EditorStyles.boldLabel);
        target.backgroundMode = (BackgroundMode)EditorGUILayout.EnumPopup("Background Mode", target.backgroundMode);
        if (target.backgroundMode == BackgroundMode.Colour)
            target.backgroundColour = EditorGUILayout.ColorField("Background", target.backgroundColour);
        else
            target.backgroundImage.sprite = (Sprite)EditorGUILayout.ObjectField("Background Sprite", target.backgroundImage.sprite, typeof(Sprite), false);
        target.borderColour = EditorGUILayout.ColorField(new GUIContent("Border"), target.borderColour, true, false, false);
        target.borderWidth  = EditorGUILayout.Slider("Border Width",      target.borderWidth,  0f, 8f);
        target.borderRadiusTL = EditorGUILayout.Slider("Border Radius TL", target.borderRadiusTL, 0f, 32f);
        target.borderRadiusTR = EditorGUILayout.Slider("Border Radius TR", target.borderRadiusTR, 0f, 32f);
        target.borderRadiusBL = EditorGUILayout.Slider("Border Radius BL", target.borderRadiusBL, 0f, 32f);
        target.borderRadiusBR = EditorGUILayout.Slider("Border Radius BR", target.borderRadiusBR, 0f, 32f);
        target.panelHeightMode = (PanelSizeMode)EditorGUILayout.EnumPopup("Height Mode", target.panelHeightMode);
        target.panelHeightValue = EditorGUILayout.Slider("Height Value", target.panelHeightValue, 1f,
            target.panelHeightMode == PanelSizeMode.Percent ? 100f : 2000f);

        EditorGUILayout.Space(6);
        GUILayout.Label("Speaker Name", EditorStyles.boldLabel);
        target.nameColour    = EditorGUILayout.ColorField("Name Colour", target.nameColour);
        target.nameFontSize  = EditorGUILayout.IntSlider("Font Size",    target.nameFontSize, 8, 64);
        target.nameUppercase = EditorGUILayout.Toggle("Uppercase",       target.nameUppercase);
        target.namePosition  = (NamePosition)EditorGUILayout.EnumPopup("Position", target.namePosition);

        EditorGUILayout.Space(6);
        GUILayout.Label("Dialogue Text", EditorStyles.boldLabel);
        target.textColour   = EditorGUILayout.ColorField("Text Colour", target.textColour);
        target.textFontSize = EditorGUILayout.IntSlider("Font Size",    target.textFontSize, 8, 64);
        target.textVAnchor  = (TextVAnchor)EditorGUILayout.EnumPopup("Vertical Anchor",   target.textVAnchor);
        target.textHAnchor  = (TextHAnchor)EditorGUILayout.EnumPopup("Horizontal Anchor", target.textHAnchor);

        EditorGUILayout.Space(6);
        GUILayout.Label("Portrait", EditorStyles.boldLabel);
        target.showPortrait      = EditorGUILayout.Toggle("Show Portrait",      target.showPortrait);
        target.portraitMode      = (PortraitMode)EditorGUILayout.EnumPopup("Mode", target.portraitMode);
        target.portraitPlacement = (PortraitPlacement)EditorGUILayout.EnumPopup("Placement", target.portraitPlacement);
        target.portraitShape     = (PortraitShape)EditorGUILayout.EnumPopup("Shape", target.portraitShape);
        target.portraitSize      = EditorGUILayout.Slider("Portrait Size",      target.portraitSize, 48f, 256f);
        target.portraitBorderColour = EditorGUILayout.ColorField(new GUIContent("Portrait Border"), target.portraitBorderColour, true, false, false);

        target.clickToAdvance = EditorGUILayout.Toggle("Click To Advance", target.clickToAdvance);

        EditorGUILayout.Space(6);
        GUILayout.Label("Advance Hint", EditorStyles.boldLabel);
        target.showAdvanceHint = EditorGUILayout.Toggle("Show Hint",    target.showAdvanceHint);
        target.advanceHintText = EditorGUILayout.TextField("Hint Text", target.advanceHintText);
        target.hintColour      = EditorGUILayout.ColorField("Hint Colour", target.hintColour);
        target.hintFontSize    = EditorGUILayout.IntSlider("Font Size",  target.hintFontSize, 6, 24);

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f, 1f);
        if (GUILayout.Button("Apply — Build Layout", GUILayout.Height(32)))
        {
            DialogueLayoutBuilder.Build(target);
            EditorUtility.SetDirty(target);
        }
        GUI.backgroundColor = Color.white;
    }

    // ─── Preview canvas ────────────────────────────────────────────────────────
    void DrawPreviewCanvas()
    {
        float canvasW = position.width - 24f;
        float canvasH = 260f;
        Rect  canvas  = GUILayoutUtility.GetRect(canvasW, canvasH);

        EditorGUI.DrawRect(canvas, new Color(0.12f, 0.12f, 0.12f, 1f));

        float boxH;
        if (target.panelHeightMode == PanelSizeMode.Percent)
            boxH = canvas.height * Mathf.Clamp01(target.panelHeightValue / 100f) + 40f;
        else
            boxH = Mathf.Min(target.panelHeightValue, canvas.height * 0.9f);
        float padH    = Mathf.Max(boxH, 90f);
        Rect  boxRect = new Rect(canvas.x + 16, canvas.y + canvas.height - padH - 12, canvas.width - 32, padH);

        // Background
        if (target.backgroundMode == BackgroundMode.Image && target.backgroundImage.sprite != null)
            GUI.DrawTexture(boxRect, target.backgroundImage.sprite.texture, ScaleMode.ScaleAndCrop);
        else
            EditorGUI.DrawRect(boxRect, target.backgroundColour);

        // Border: colour OR image (image wins)
        bool borderHasImage = target.borderImage != null &&
            (target.borderImage.sprite != null || !string.IsNullOrEmpty(target.borderImage.path));
        if (borderHasImage)
            DrawBorderRect(boxRect, target.borderWidth, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        else
            DrawBorderRect(boxRect, target.borderWidth, target.borderColour);

        if (target.padding == null) target.padding = new RectOffset(28, 28, 20, 20);

        float innerX = boxRect.x + target.padding.left;
        float innerY = boxRect.y + target.padding.top;

        // ── Portrait ──────────────────────────────────────────────────────────
        float ps = Mathf.Min(target.portraitSize, boxRect.height - target.padding.top - target.padding.bottom);
        bool dual = target.showPortrait && target.portraitMode == PortraitMode.Dual;

        if (target.showPortrait && target.portraitMode != PortraitMode.None)
        {
            // Left portrait (active)
            Rect leftRect = DrawPortrait(innerX, boxRect.y + (boxRect.height - ps) * 0.5f, ps, ps, target.portraitShape, true);
            DrawNameNear(leftRect, previewSpeakerA, target.namePosition, target.nameDistance, target.nameColour, target.nameFontSize, target.nameUppercase);

            // Right portrait (inactive, greyed) — duel mode only
            if (dual)
            {
                Rect rightRect = DrawPortrait(boxRect.xMax - target.padding.right - ps, boxRect.y + (boxRect.height - ps) * 0.5f, ps, ps, target.portraitShape, false);
                DrawNameNear(rightRect, previewSpeakerB, target.namePosition, target.nameDistance, target.nameColour, target.nameFontSize, target.nameUppercase, greyed: true);
            }
            innerX += ps + 20f;
        }

        // ── Name + text column ────────────────────────────────────────────────
        string displayName = target.nameUppercase ? previewSpeakerA.ToUpper() : previewSpeakerA;
        var nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = Mathf.Clamp(target.nameFontSize - 2, 8, 28),
            normal    = { textColor = target.nameColour },
            wordWrap  = false
        };
        float textColW = boxRect.xMax - innerX - target.padding.right;
        Rect  nameRect = new Rect(innerX, innerY, textColW, nameStyle.fontSize + 4);
        GUI.Label(nameRect, displayName, nameStyle);

        var textStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = Mathf.Clamp(target.textFontSize - 2, 8, 24),
            normal   = { textColor = target.textColour },
            wordWrap = true
        };
        Rect textRect = new Rect(innerX, nameRect.yMax + 4, textColW, boxRect.yMax - nameRect.yMax - target.padding.bottom - 20);
        GUI.Label(textRect, previewText, textStyle);

        if (target.showAdvanceHint)
        {
            var hintStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize  = Mathf.Clamp(target.hintFontSize, 6, 18),
                normal    = { textColor = target.hintColour },
                alignment = TextAnchor.LowerRight
            };
            Rect hintRect = new Rect(innerX, boxRect.yMax - 20 - target.padding.bottom, textColW, 18);
            GUI.Label(hintRect, target.advanceHintText, hintStyle);
        }

        // Character figure panels beside the box
        DrawCharacterPanelSketch(canvas, boxRect, false);
        if (dual) DrawCharacterPanelSketch(canvas, boxRect, true);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            $"Mode: {target.portraitMode} · Placement: {target.portraitPlacement} · Shape: {target.portraitShape} · " +
            $"Name pos: {target.namePosition}\nThis is an approximate IMGUI sketch — run the scene for the real UI Toolkit result.",
            MessageType.Info);
    }

    Rect DrawPortrait(float x, float y, float w, float h, PortraitShape shape, bool active)
    {
        Rect r = new Rect(x, y, w, h);
        Color fill = active ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.2f, 0.2f, 0.2f, 0.45f);

        if (shape == PortraitShape.Circle)
        {
            if (target.showPortraitBorder)
            {
                var c = active ? target.portraitBorderColour : Color.Lerp(target.portraitBorderColour, target.inactiveTintColour, 0.5f);
                DrawCircleFilled(r, c);
                float bw = Mathf.Max(2f, w * 0.08f);
                DrawCircleFilled(new Rect(r.x + bw, r.y + bw, r.width - bw * 2f, r.height - bw * 2f), fill);
            }
            else DrawCircleFilled(r, fill);
        }
        else
        {
            EditorGUI.DrawRect(r, fill);
            if (target.showPortraitBorder)
            {
                var c = active ? target.portraitBorderColour : Color.Lerp(target.portraitBorderColour, target.inactiveTintColour, 0.5f);
                DrawBorderRect(r, 1f, c);
            }
        }

        var tag = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f, active ? 0.8f : 0.35f) }
        };
        GUI.Label(r, active ? "active" : "inactive", tag);
        return r;
    }

    static Texture2D circleTex;
    static Texture2D GetCircleTexture()
    {
        if (circleTex == null)
        {
            int s = 64;
            circleTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color[s * s];
            float aa = 1.5f / s;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s - 0.5f;
                    float dy = (y + 0.5f) / s - 0.5f;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    float a  = Mathf.Clamp01((0.5f - d) / aa + 0.5f);
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            circleTex.SetPixels(px);
            circleTex.Apply();
            circleTex.hideFlags = HideFlags.HideAndDontSave;
        }
        return circleTex;
    }

    static void DrawCircleFilled(Rect r, Color colour)
    {
        Color prev = GUI.color;
        GUI.color = colour;
        GUI.DrawTexture(r, GetCircleTexture(), ScaleMode.StretchToFill, true);
        GUI.color = prev;
    }

    void DrawCharacterPanelSketch(Rect canvas, Rect boxRect, bool right)
    {
        if (target.portraitPlacement != PortraitPlacement.CharacterPanel) return;

        float pw = Mathf.Min(target.characterPanelWidth, 130f);
        float ph = pw + 46f;
        Rect panel = right
            ? new Rect(boxRect.xMax + 10, boxRect.yMax - ph - 24, pw, ph)
            : new Rect(boxRect.x - pw - 10, boxRect.yMax - ph - 24, pw, ph);

        EditorGUI.DrawRect(panel, target.characterPanelBg);
        DrawBorderRect(panel, target.characterPanelBorderWidth, target.characterPanelBorderColour);

        // Image sub-panel (with silhouette placeholder)
        Rect img = new Rect(panel.x + 8, panel.y + 8, panel.width - 16, panel.width - 16);
        EditorGUI.DrawRect(img, target.characterImagePanelBg);
        if (target.characterImagePanelBorderWidth > 0f)
            DrawBorderRect(img, target.characterImagePanelBorderWidth, target.characterImagePanelBorderColour);
        DrawCircleFilled(new Rect(img.x + 10, img.y + 10, img.width - 20, img.height - 20), new Color(0.16f, 0.16f, 0.18f, 1f));

        // Name sub-panel
        Rect nm = new Rect(panel.x + 8, img.yMax + 6, panel.width - 16, 24);
        EditorGUI.DrawRect(nm, target.characterNamePanelBg);
        if (target.characterNamePanelBorderWidth > 0f)
            DrawBorderRect(nm, target.characterNamePanelBorderWidth, target.characterNamePanelBorderColour);
        string tag = right ? "INACTIVE" : "ACTIVE";
        if (target.nameUppercase) tag = tag.ToUpper();
        GUI.Label(nm, tag, new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = right ? new Color(target.nameColour.r, target.nameColour.g, target.nameColour.b, 0.35f) : target.nameColour }
        });
    }

    void DrawNameNear(Rect portrait, string name, NamePosition pos, float distance, Color colour, int fontSize, bool upper, bool greyed = false)
    {
        string display = upper ? name.ToUpper() : name;
        var style = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = Mathf.Clamp(fontSize - 2, 8, 24),
            normal    = { textColor = greyed ? new Color(colour.r, colour.g, colour.b, 0.35f) : colour },
            wordWrap  = false
        };

        Vector2 size = style.CalcSize(new GUIContent(display));
        Rect r = Rect.zero;
        switch (pos)
        {
            case NamePosition.Above: r = new Rect(portrait.center.x - size.x * 0.5f, portrait.y - size.y - distance, size.x, size.y); break;
            case NamePosition.Below: r = new Rect(portrait.center.x - size.x * 0.5f, portrait.yMax + distance, size.x, size.y); break;
            case NamePosition.Left:  r = new Rect(portrait.x - size.x - distance, portrait.center.y - size.y * 0.5f, size.x, size.y); break;
            case NamePosition.Right: r = new Rect(portrait.xMax + distance, portrait.center.y - size.y * 0.5f, size.x, size.y); break;
        }
        GUI.Label(r, display, style);
    }

    void DrawBorderRect(Rect rect, float width, Color colour)
    {
        if (width <= 0f) return;
        EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        rect.width, width),         colour);
        EditorGUI.DrawRect(new Rect(rect.x,        rect.yMax - width, rect.width, width),     colour);
        EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        width, rect.height),        colour);
        EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y,    width, rect.height),        colour);
    }
}
#endif

