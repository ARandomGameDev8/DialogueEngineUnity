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
        ApplyImageLayoutHints(engine, asset);
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

    static void ApplyTextPanels(Dialogue_Engine engine, DialogueLayoutAsset asset)
    {
        DialogueTextPanelDefinition text = FindFirstComponent<DialogueTextPanelDefinition>(asset);
        if (text == null || text.TextStyle == null) return;

        engine.textFontSize = Mathf.RoundToInt(text.TextStyle.FontSize);
        engine.textColour = text.TextStyle.Color;
        engine.textHAnchor = ToTextHAnchor(text.TextStyle.HorizontalAlignment);
        engine.textVAnchor = ToTextVAnchor(text.TextStyle.VerticalAlignment);
        engine.enableTypewriter = text.TypewriterEnabled;
        engine.typewriterSpeed = text.CharactersPerSecond > 0f
            ? Mathf.Clamp(1f / text.CharactersPerSecond, 0.005f, 0.1f)
            : engine.typewriterSpeed;
    }

    static void ApplyNamePanels(Dialogue_Engine engine, DialogueLayoutAsset asset)
    {
        DialogueNamePanelDefinition name = FindFirstComponent<DialogueNamePanelDefinition>(asset);
        if (name == null || name.TextStyle == null) return;

        engine.nameFontSize = Mathf.RoundToInt(name.TextStyle.FontSize);
        engine.nameColour = name.TextStyle.Color;
    }

    static void ApplyImageLayoutHints(Dialogue_Engine engine, DialogueLayoutAsset asset)
    {
        DialogueImagePanelDefinition image = FindFirstComponent<DialogueImagePanelDefinition>(asset);
        if (image == null) return;

        engine.showPortrait = true;
        if (image.Mode == DialogueImagePanelMode.CharacterFigure)
        {
            engine.portraitPlacement = PortraitPlacement.CharacterPanel;
            engine.showPortraitWhenEmpty = false;
        }
        else
        {
            engine.portraitPlacement = PortraitPlacement.Outside;
        }
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
}
