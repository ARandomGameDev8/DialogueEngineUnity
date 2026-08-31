using UnityEngine;

public static class DialogueVisualLayoutBridge
{
    public static void ApplyToEngine(Dialogue_Engine engine,
        DialogueLayoutAsset asset)
    {
        if (engine == null || asset == null || asset.MainPanel == null) return;

        engine.layoutAssetAnchorPreset = asset.MainPanel.AnchorPreset;
        engine.layoutAssetCustomAnchor = asset.MainPanel.CustomAnchor != null
            ? new DialogueCustomAnchorDefinition
            {
                HorizontalReference = asset.MainPanel.CustomAnchor.HorizontalReference,
                VerticalReference = asset.MainPanel.CustomAnchor.VerticalReference,
                OffsetX = asset.MainPanel.CustomAnchor.OffsetX,
                OffsetY = asset.MainPanel.CustomAnchor.OffsetY
            }
            : new DialogueCustomAnchorDefinition();

        ApplyMainPanel(engine, asset.MainPanel);
        ApplyTextPanels(engine, asset);
        ApplyNamePanels(engine, asset);
        ApplyImagePanels(engine, asset);
        ApplySpeakerEmphasis(engine, asset);
    }

    static void ApplyMainPanel(Dialogue_Engine engine,
        DialogueMainPanelDefinition panel)
    {
        engine.panelWidthMode = ToPanelSizeMode(panel.Width != null ? panel.Width.Unit : DialogueSizeUnit.Percent);
        engine.panelWidthValue = panel.Width != null ? panel.Width.Value : engine.panelWidthValue;
        engine.panelHeightMode = ToPanelSizeMode(panel.Height != null ? panel.Height.Unit : DialogueSizeUnit.Pixels);
        engine.panelHeightValue = panel.Height != null ? panel.Height.Value : engine.panelHeightValue;

        if (panel.Padding != null)
        {
            if (engine.padding == null) engine.padding = new RectOffset();
            engine.padding.left = Mathf.RoundToInt(panel.Padding.Left);
            engine.padding.right = Mathf.RoundToInt(panel.Padding.Right);
            engine.padding.top = Mathf.RoundToInt(panel.Padding.Top);
            engine.padding.bottom = Mathf.RoundToInt(panel.Padding.Bottom);
        }

        if (panel.CustomAnchor != null)
        {
            engine.panelOffsetX = panel.CustomAnchor.OffsetX;
            engine.panelOffsetY = panel.CustomAnchor.OffsetY;
        }

        if (panel.Background != null)
        {
            engine.backgroundMode = panel.Background.Mode == DialogueBackgroundMode.None
                ? BackgroundMode.Colour
                : panel.Background.Mode == DialogueBackgroundMode.Sprite
                    ? BackgroundMode.Image
                    : BackgroundMode.Colour;
            Color color = panel.Background.ColorA;
            color.a *= panel.Background.Opacity;
            engine.backgroundColour = color;
        }

        if (panel.Border != null)
        {
            engine.borderWidth = Mathf.Max(
                panel.Border.LeftThickness,
                panel.Border.RightThickness,
                panel.Border.TopThickness,
                panel.Border.BottomThickness);
            engine.borderColour = panel.Border.BorderColor;
            engine.borderColour.a *= panel.Border.Opacity;
            engine.borderRadiusTL = panel.Border.CornerRadiusTopLeft;
            engine.borderRadiusTR = panel.Border.CornerRadiusTopRight;
            engine.borderRadiusBL = panel.Border.CornerRadiusBottomLeft;
            engine.borderRadiusBR = panel.Border.CornerRadiusBottomRight;
        }
    }

    // ─── Text panels ──────────────────────────────────────────────────────────
    static void ApplyTextPanels(Dialogue_Engine engine, DialogueLayoutAsset asset)
    {
        DialogueTextPanelDefinition text = FindFirstComponent<DialogueTextPanelDefinition>(asset);
        if (text == null) return;

        if (text.TextStyle != null)
        {
            engine.textFontSize = Mathf.RoundToInt(Mathf.Clamp(text.TextStyle.FontSize, 8f, 64f));
            engine.textColour = text.TextStyle.Color;
            engine.textHAnchor = ToTextHAnchor(text.TextStyle.HorizontalAlignment);
            engine.textVAnchor = ToTextVAnchor(text.TextStyle.VerticalAlignment);
            engine.textLetterSpacing = Mathf.Clamp(text.TextStyle.LetterSpacing, -8f, 32f);
            Font font = ResolveFont(text.TextStyle.FontSourceKey);
            if (font != null) engine.textFont = font;
        }

        engine.enableTypewriter = text.TypewriterEnabled;
        if (text.CharactersPerSecond > 0f)
            engine.typewriterSpeed = Mathf.Clamp(1f / text.CharactersPerSecond, 0.005f, 0.1f);
        engine.typewriterStartDelay = Mathf.Clamp(text.StartDelay, 0f, 5f);

        ApplyLetterEffect(engine, text.LetterEffect, text.BaseAnimationProfile, false);
    }

    // ─── Name panels ──────────────────────────────────────────────────────────
    static void ApplyNamePanels(Dialogue_Engine engine, DialogueLayoutAsset asset)
    {
        DialogueNamePanelDefinition name = FindFirstComponent<DialogueNamePanelDefinition>(asset);
        if (name == null) return;

        // The name panel renders through the engine's speaker-name element;
        // make sure the portrait slot hosting it is not switched off.
        engine.showPortrait = true;

        if (name.TextStyle != null)
        {
            engine.nameFontSize = Mathf.RoundToInt(Mathf.Clamp(name.TextStyle.FontSize, 8f, 64f));
            engine.nameColour = name.TextStyle.Color;
            engine.nameLetterSpacing = Mathf.Clamp(name.TextStyle.LetterSpacing, -8f, 32f);
            Font font = ResolveFont(name.TextStyle.FontSourceKey);
            if (font != null) engine.nameFont = font;
        }

        engine.nameUppercase = name.Uppercase;
        ApplyLetterEffect(engine, name.LetterEffect, name.BaseAnimationProfile, true);
    }

    /// <summary>
    /// Maps the per-letter behaviour of a text/name panel onto the engine's
    /// letter renderer. An assigned TextAnimationProfile overrides the inline
    /// LetterEffect values.
    /// </summary>
    static void ApplyLetterEffect(Dialogue_Engine engine,
        DialogueLetterEffectSettings effect, TextAnimationProfile profile, bool isName)
    {
        DialogueTextEffectType type = profile != null
            ? profile.EffectType
            : effect != null ? effect.EffectType : DialogueTextEffectType.None;
        float amplitude = profile != null ? profile.Amplitude
            : effect != null ? effect.Amplitude : 6f;
        float frequency = profile != null ? profile.Frequency
            : effect != null ? effect.Frequency : 0.6f;
        float phase = effect != null ? effect.PhaseOffset : 0f;
        float animSpeed = effect != null ? effect.AnimationSpeed : 2f;

        if (isName)
        {
            engine.nameLetterMode = ToLetterMode(type);
            engine.nameLetterAmplitude = Mathf.Clamp(amplitude, 0f, 48f);
            engine.nameLetterFrequency = Mathf.Clamp(frequency, 0.05f, 3f);
            engine.nameLetterPhase = Mathf.Clamp(phase, 0f, 6.28f);
            engine.nameLetterAnimationSpeed = Mathf.Clamp(animSpeed, 0.1f, 8f);
        }
        else
        {
            engine.textLetterMode = ToLetterMode(type);
            engine.textLetterAmplitude = Mathf.Clamp(amplitude, 0f, 48f);
            engine.textLetterFrequency = Mathf.Clamp(frequency, 0.05f, 3f);
            engine.textLetterPhase = Mathf.Clamp(phase, 0f, 6.28f);
            engine.textLetterAnimationSpeed = Mathf.Clamp(animSpeed, 0.1f, 8f);
        }
    }

    // ─── Image panels (icon / character figure) ───────────────────────────────
    static void ApplyImagePanels(Dialogue_Engine engine, DialogueLayoutAsset asset)
    {
        DialogueImagePanelDefinition image = FindFirstComponent<DialogueImagePanelDefinition>(asset);
        if (image == null) return;

        engine.showPortrait = true;
        engine.portraitFlipHorizontal = image.FlipHorizontal;

        if (image.Mode == DialogueImagePanelMode.CharacterFigure)
        {
            // Figure: a panel that hugs the loaded image, paints nothing while
            // empty, and never grows past its parent container.
            engine.portraitPlacement = PortraitPlacement.CharacterPanel;
            engine.portraitDisplayType = PortraitDisplayType.Figure;
            engine.showPortraitWhenEmpty = !image.HideWhenEmpty;
            engine.dynamicPortraitSize = image.FitToImage;
            engine.maxPortraitSize = Mathf.Clamp(512f * Mathf.Clamp01(image.MaxSizePercent / 100f), 48f, 512f);
            engine.portraitFillMode = image.FigureScaleMode == DialogueFigureScaleMode.Fill
                ? PortraitFillMode.FillCrop
                : PortraitFillMode.Fit;
            if (image.FitToImage)
            {
                // Content sizing lets the figure panel shrink around the image
                // instead of reserving a fixed partition.
                engine.characterPanelWidthMode = CharacterPanelSizeMode.Content;
                engine.characterPanelHeightMode = CharacterPanelSizeMode.Content;
            }
        }
        else
        {
            // Icon: a geometric shape with a customizable border, fitted to the image.
            engine.portraitPlacement = PortraitPlacement.Outside;
            engine.portraitDisplayType = PortraitDisplayType.Icon;
            engine.portraitShape = ToPortraitShape(image.Shape);
            engine.portraitSize = Mathf.Clamp(96f * Mathf.Max(0.1f, image.UniformScale), 48f, 512f);
            engine.showPortraitWhenEmpty = !image.HideWhenEmpty;
            engine.portraitFillMode = PortraitFillMode.FillCrop;
        }

        if (image.Border != null)
        {
            engine.showPortraitBorder = image.Border.Enabled;
            engine.portraitBorderWidth = Mathf.Clamp(Mathf.Max(
                image.Border.LeftThickness,
                image.Border.RightThickness,
                image.Border.TopThickness,
                image.Border.BottomThickness), 0f, 8f);
            Color borderColour = image.Border.BorderColor;
            borderColour.a = 1f;
            engine.portraitBorderColour = borderColour;
            engine.portraitBorderRadius = Mathf.Clamp(
                (image.Border.CornerRadiusTopLeft + image.Border.CornerRadiusTopRight +
                 image.Border.CornerRadiusBottomLeft + image.Border.CornerRadiusBottomRight) * 0.25f, 0f, 32f);
        }
    }

    // ─── Speaker emphasis ─────────────────────────────────────────────────────
    static void ApplySpeakerEmphasis(Dialogue_Engine engine, DialogueLayoutAsset asset)
    {
        DialogueSpeakerEmphasisSettings emphasis = asset.SpeakerEmphasis;
        if (emphasis == null) return;

        if (emphasis.GreyOutPastSpeakers && engine.showPortrait)
            engine.portraitMode = PortraitMode.Dual;

        engine.activePortraitOpacity = emphasis.ActiveOpacity;
        engine.inactivePortraitOpacity = emphasis.InactiveOpacity;
        engine.inactiveTintColour = emphasis.InactiveTint;
    }

    // ─── Shared helpers ───────────────────────────────────────────────────────
    static Font ResolveFont(string sourceKey)
    {
        if (string.IsNullOrEmpty(sourceKey)) return null;
        return Resources.Load<Font>(sourceKey);
    }

    static T FindFirstComponent<T>(DialogueLayoutAsset asset)
        where T : DialogueComponentDefinition
    {
        if (asset == null) return null;
        T found = FindFirstComponentInSlots<T>(asset.MainPanel != null && asset.MainPanel.InnerRegion != null
            ? asset.MainPanel.InnerRegion.Slots : null);
        if (found != null) return found;
        if (asset.TopAreaEnabled) found = FindFirstComponentInSlots<T>(asset.TopArea.Slots);
        if (found != null) return found;
        if (asset.BottomAreaEnabled) found = FindFirstComponentInSlots<T>(asset.BottomArea.Slots);
        if (found != null) return found;
        if (asset.LeftAreaEnabled) found = FindFirstComponentInSlots<T>(asset.LeftArea.Slots);
        if (found != null) return found;
        if (asset.RightAreaEnabled) found = FindFirstComponentInSlots<T>(asset.RightArea.Slots);
        return found;
    }

    static T FindFirstComponentInSlots<T>(System.Collections.Generic.List<DialogueSlotDefinition> slots)
        where T : DialogueComponentDefinition
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

    static PanelSizeMode ToPanelSizeMode(DialogueSizeUnit unit)
    {
        return unit == DialogueSizeUnit.Percent
            ? PanelSizeMode.Percent
            : PanelSizeMode.Pixels;
    }

    static TextHAnchor ToTextHAnchor(DialogueHorizontalAlignment alignment)
    {
        switch (alignment)
        {
            case DialogueHorizontalAlignment.Center: return TextHAnchor.Center;
            case DialogueHorizontalAlignment.Right: return TextHAnchor.Right;
            default: return TextHAnchor.Left;
        }
    }

    static TextVAnchor ToTextVAnchor(DialogueVerticalAlignment alignment)
    {
        switch (alignment)
        {
            case DialogueVerticalAlignment.Top: return TextVAnchor.Top;
            case DialogueVerticalAlignment.Bottom: return TextVAnchor.Bottom;
            default: return TextVAnchor.Center;
        }
    }

    static LetterMode ToLetterMode(DialogueTextEffectType effect)
    {
        switch (effect)
        {
            case DialogueTextEffectType.Wave: return LetterMode.Wave;
            case DialogueTextEffectType.Zigzag: return LetterMode.Zigzag;
            case DialogueTextEffectType.Staircase: return LetterMode.Staircase;
            case DialogueTextEffectType.Shake: return LetterMode.Shake;
            case DialogueTextEffectType.FadeIn: return LetterMode.FadeIn;
            case DialogueTextEffectType.Bounce: return LetterMode.Bounce;
            default: return LetterMode.Normal;
        }
    }

    static PortraitShape ToPortraitShape(DialogueIconShape shape)
    {
        switch (shape)
        {
            case DialogueIconShape.Circle: return PortraitShape.Circle;
            case DialogueIconShape.Square: return PortraitShape.Square;
            case DialogueIconShape.RoundedRectangle: return PortraitShape.Rounded;
            case DialogueIconShape.Diamond:
            case DialogueIconShape.Hexagon:
            default: return PortraitShape.Rounded;
        }
    }
}
