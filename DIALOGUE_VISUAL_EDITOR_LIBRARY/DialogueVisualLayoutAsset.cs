using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DialogueLayoutAsset",
    menuName = "MyNDS/Dialogue Visual Editor/Dialogue Layout Asset")]
public sealed class DialogueLayoutAsset : ScriptableObject
{
    public int DataVersion = 1;
    public string LayoutName = "New Dialogue Layout";

    public DialogueMainPanelDefinition MainPanel =
        new DialogueMainPanelDefinition();

    public bool TopAreaEnabled;
    public bool BottomAreaEnabled;
    public bool LeftAreaEnabled;
    public bool RightAreaEnabled;

    public DialogueAttachedAreaDefinition TopArea =
        new DialogueAttachedAreaDefinition
        {
            Side = DialogueAttachedAreaSide.Top,
            DisplayName = "Top Area"
        };

    public DialogueAttachedAreaDefinition BottomArea =
        new DialogueAttachedAreaDefinition
        {
            Side = DialogueAttachedAreaSide.Bottom,
            DisplayName = "Bottom Area"
        };

    public DialogueAttachedAreaDefinition LeftArea =
        new DialogueAttachedAreaDefinition
        {
            Side = DialogueAttachedAreaSide.Left,
            DisplayName = "Left Area"
        };

    public DialogueAttachedAreaDefinition RightArea =
        new DialogueAttachedAreaDefinition
        {
            Side = DialogueAttachedAreaSide.Right,
            DisplayName = "Right Area"
        };

    // ── Choice event UI ───────────────────────────────────────────────────────
    // A second, fully customizable panel that appears ONLY while the player is
    // taking a choice. Same customization surface as the main panel (anchor,
    // fill, size, min/max, padding, background, border, shadow, opacity, z).
    // Its inner region partitions into 1-3 terminal choice slots — one per
    // option; the first text panel component in each slot becomes that
    // option's live label.
    public bool ChoicePanelEnabled;

    public DialogueMainPanelDefinition ChoicePanel = new DialogueMainPanelDefinition
    {
        DisplayName = "Choice Panel",
        Enabled = true,
        AnchorPreset = DialogueAnchorPreset.Center,
        Width = new DialogueSizeValue { Unit = DialogueSizeUnit.Percent, Value = 40f },
        Height = new DialogueSizeValue { Unit = DialogueSizeUnit.Pixels, Value = 200f },
        Padding = new DialoguePadding { Left = 16f, Right = 16f, Top = 12f, Bottom = 12f },
        Background = new DialogueBackgroundStyle
        {
            Mode = DialogueBackgroundMode.SolidColor,
            ColorA = new Color(0f, 0f, 0f, 0.92f)
        },
        Border = new DialogueBorderStyle { Enabled = true, BorderColor = new Color(1f, 1f, 1f, 0.35f) },
        InnerRegion = new DialogueInnerRegionDefinition
        {
            DisplayName = "Choice Region",
            PartitionLevel = 0,
            InterSlotSpacing = 8f,
            Slots = new List<DialogueSlotDefinition>
            {
                new DialogueSlotDefinition { SlotId = "A", DisplayName = "Choice 1" },
                new DialogueSlotDefinition { SlotId = "B", DisplayName = "Choice 2" },
                new DialogueSlotDefinition { SlotId = "C", DisplayName = "Choice 3" }
            }
        }
    };

    // How the choice region partitions: Vertical = stacked rows (default;
    // "bottom-most" slot), Horizontal = side-by-side columns.
    public DialogueChoiceRegionOrientation ChoiceRegionOrientation =
        DialogueChoiceRegionOrientation.Vertical;

    // Hypothetical choice count for DESIGNING (0-6). The holder partitions
    // itself automatically — at Play the ACTUAL option count decides.
    [Range(0, 6)] public int ChoicePreviewCount = 3;

    // Which choice-region slot holds the buttons. -1 = automatic: the
    // bottom-most (last) visible slot when partitioned, the slot itself at
    // partition level 0. The other slots hold whatever components you like.
    public int ChoiceHolderSlotIndex = -1;

    // Button groups inside the holder slot: up to 3 groups, each with up to 2
    // leaf slots (choice buttons) in its Children list → up to 6 visual
    // choices. The DSL is not limited; extra options are simply not shown.
    public List<DialogueSlotDefinition> ChoiceGroups = BuildDefaultChoiceGroups();

    // The shared choice button preset: every button instance looks exactly
    // like this. Only its size may differ per instance (Variable mode).
    public DialogueChoiceButtonSettings ChoiceButtons = new DialogueChoiceButtonSettings();

    static List<DialogueSlotDefinition> BuildDefaultChoiceGroups()
    {
        return new List<DialogueSlotDefinition>
        {
            new DialogueSlotDefinition
            {
                SlotId = "G1", DisplayName = "Button Group 1",
                Children = new List<DialogueSlotDefinition>
                {
                    new DialogueSlotDefinition { SlotId = "B1", DisplayName = "Choice Button 1" },
                    new DialogueSlotDefinition { SlotId = "B2", DisplayName = "Choice Button 2" }
                }
            },
            new DialogueSlotDefinition
            {
                SlotId = "G2", DisplayName = "Button Group 2",
                Children = new List<DialogueSlotDefinition>
                {
                    new DialogueSlotDefinition { SlotId = "B3", DisplayName = "Choice Button 3" },
                    new DialogueSlotDefinition { SlotId = "B4", DisplayName = "Choice Button 4" }
                }
            },
            new DialogueSlotDefinition
            {
                SlotId = "G3", DisplayName = "Button Group 3",
                Children = new List<DialogueSlotDefinition>
                {
                    new DialogueSlotDefinition { SlotId = "B5", DisplayName = "Choice Button 5" },
                    new DialogueSlotDefinition { SlotId = "B6", DisplayName = "Choice Button 6" }
                }
            }
        };
    }

    // ── Free-floating UI panels ────────────────────────────────────────────────
    // Add as many standalone panels as you wish. Each is a full panel citizen
    // (anchor anywhere, main-panel move/resize model, own region with 1-3
    // slots, any components, full style stack, optional image background).
    public List<DialogueMainPanelDefinition> FreePanels = new List<DialogueMainPanelDefinition>();

    // How the current speaker is emphasized against recently interrupted ones.
    public DialogueSpeakerEmphasisSettings SpeakerEmphasis =
        new DialogueSpeakerEmphasisSettings();
}

public enum DialogueAnchorPreset
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
    Custom
}

public enum DialoguePanelFillMode
{
    Fixed,
    StretchHorizontal,
    StretchVertical,
    Auto
}

public enum DialogueAttachedAreaSide
{
    Top,
    Bottom,
    Left,
    Right
}

public enum DialogueComponentType
{
    TextPanel,
    NamePanel,
    ImagePanel
}

public enum DialogueImagePanelMode
{
    Icon,
    CharacterFigure
}

public enum DialogueIconShape
{
    Circle,
    RoundedRectangle,
    Square,
    Diamond,
    Hexagon
}

public enum DialogueFigureScaleMode
{
    Fit,
    Fill
}

public enum DialogueBackgroundMode
{
    None,
    SolidColor,
    Gradient,
    Sprite
}

public enum DialogueGradientDirection
{
    Horizontal,
    Vertical,
    DiagonalDown,
    DiagonalUp
}

public enum DialogueSizeUnit
{
    Pixels,
    Percent,
    Auto
}

public enum DialogueHorizontalAlignment
{
    Left,
    Center,
    Right,
    Stretch
}

public enum DialogueVerticalAlignment
{
    Top,
    Center,
    Bottom,
    Stretch
}

public enum DialogueFontWeight
{
    Regular,
    Medium,
    SemiBold,
    Bold
}

public enum DialogueAnchorReferenceEdge
{
    Left,
    Right,
    Top,
    Bottom,
    Center
}

[Serializable]
public sealed class DialogueMainPanelDefinition
{
    public string DisplayName = "Main Panel";
    public bool Enabled = true;

    public DialogueAnchorPreset AnchorPreset = DialogueAnchorPreset.Bottom;
    public DialogueCustomAnchorDefinition CustomAnchor =
        new DialogueCustomAnchorDefinition();

    public DialoguePanelFillMode FillMode = DialoguePanelFillMode.Fixed;
    public DialogueSizeValue Width = new DialogueSizeValue
    {
        Unit = DialogueSizeUnit.Percent,
        Value = 80f
    };
    public DialogueSizeValue Height = new DialogueSizeValue
    {
        Unit = DialogueSizeUnit.Pixels,
        Value = 220f
    };

    public DialogueMinMaxSize MinMax = new DialogueMinMaxSize
    {
        MinWidth = 100f,
        MinHeight = 60f,
        MaxWidth = 10000f,
        MaxHeight = 10000f
    };

    public DialoguePadding Padding = new DialoguePadding
    {
        Left = 20f,
        Right = 20f,
        Top = 16f,
        Bottom = 16f
    };

    public DialogueOpacitySettings Opacity =
        new DialogueOpacitySettings();
    public DialogueBackgroundStyle Background =
        new DialogueBackgroundStyle();
    public DialogueBorderStyle Border =
        new DialogueBorderStyle();
    public DialogueShadowStyle Shadow =
        new DialogueShadowStyle();

    [Range(-10, 10)] public int ZLayer;

    // Image-based panel: the panel's own surface goes invisible at Play and
    // this image becomes the panel body, stretched exactly with the panel
    // (bigger panel = bigger image). Children stay fully visible; the editor
    // canvas shows the panel outline faintly on top of the image.
    public bool UseImageBackground;
    public string ImageBackgroundPath = "";

    public DialogueInnerRegionDefinition InnerRegion =
        new DialogueInnerRegionDefinition();
}

[Serializable]
public sealed class DialogueInnerRegionDefinition
{
    public string DisplayName = "Inner Region";
    public bool Enabled = true;

    public DialogueSizeValue Width = new DialogueSizeValue
    {
        Unit = DialogueSizeUnit.Percent,
        Value = 100f
    };
    public DialogueSizeValue Height = new DialogueSizeValue
    {
        Unit = DialogueSizeUnit.Percent,
        Value = 100f
    };

    public Vector2 Offset;

    [Range(0, 2)] public int PartitionLevel = 0;
    public float InterSlotSpacing = 8f;

    public DialogueBackgroundStyle Background =
        new DialogueBackgroundStyle();
    public DialogueBorderStyle Border =
        new DialogueBorderStyle();
    public DialogueShadowStyle Shadow =
        new DialogueShadowStyle();
    public DialogueOpacitySettings Opacity =
        new DialogueOpacitySettings();
    [Range(-10, 10)] public int ZLayer;

    public List<DialogueSlotDefinition> Slots =
        BuildDefaultSlots();

    static List<DialogueSlotDefinition> BuildDefaultSlots()
    {
        return new List<DialogueSlotDefinition>
        {
            new DialogueSlotDefinition { SlotId = "A", DisplayName = "Slot A" },
            new DialogueSlotDefinition { SlotId = "B", DisplayName = "Slot B" },
            new DialogueSlotDefinition { SlotId = "C", DisplayName = "Slot C" },
            new DialogueSlotDefinition { SlotId = "D", DisplayName = "Slot D" },
            new DialogueSlotDefinition { SlotId = "E", DisplayName = "Slot E" },
            new DialogueSlotDefinition { SlotId = "F", DisplayName = "Slot F" }
        };
    }
}

[Serializable]
public sealed class DialogueAttachedAreaDefinition
{
    public string DisplayName = "Attached Area";
    public bool Enabled = true;
    public DialogueAttachedAreaSide Side;

    public DialogueSizeValue Width = new DialogueSizeValue
    {
        Unit = DialogueSizeUnit.Percent,
        Value = 100f
    };
    public DialogueSizeValue Height = new DialogueSizeValue
    {
        Unit = DialogueSizeUnit.Pixels,
        Value = 120f
    };

    public float GapFromMainPanel = 8f;
    public Vector2 Offset;

    [Range(0, 2)] public int PartitionLevel = 0;
    public float InterSlotSpacing = 8f;

    public DialogueBackgroundStyle Background =
        new DialogueBackgroundStyle();
    public DialogueBorderStyle Border =
        new DialogueBorderStyle();
    public DialogueShadowStyle Shadow =
        new DialogueShadowStyle();
    public DialogueOpacitySettings Opacity =
        new DialogueOpacitySettings();

    [Range(-10, 10)] public int ZLayer;

    // Image-based area (same behavior as the image-based main panel).
    public bool UseImageBackground;
    public string ImageBackgroundPath = "";

    public List<DialogueSlotDefinition> Slots =
        new List<DialogueSlotDefinition>
        {
            new DialogueSlotDefinition { SlotId = "A", DisplayName = "Slot A" },
            new DialogueSlotDefinition { SlotId = "B", DisplayName = "Slot B" },
            new DialogueSlotDefinition { SlotId = "C", DisplayName = "Slot C" },
            new DialogueSlotDefinition { SlotId = "D", DisplayName = "Slot D" },
            new DialogueSlotDefinition { SlotId = "E", DisplayName = "Slot E" },
            new DialogueSlotDefinition { SlotId = "F", DisplayName = "Slot F" }
        };
}

[Serializable]
public sealed class DialogueSlotDefinition
{
    public string SlotId = "A";
    public string DisplayName = "Slot";
    public bool Enabled = true;

    public DialogueSizeValue Width = new DialogueSizeValue
    {
        Unit = DialogueSizeUnit.Auto,
        Value = 0f
    };
    public DialogueSizeValue Height = new DialogueSizeValue
    {
        Unit = DialogueSizeUnit.Auto,
        Value = 0f
    };

    [Tooltip("Set below zero to use the parent region's inter-slot spacing.")]
    public float GapAfter = -1f;
    public Vector2 Offset;

    public DialoguePadding Padding = new DialoguePadding();
    public DialogueBackgroundStyle Background =
        new DialogueBackgroundStyle();
    public DialogueBorderStyle Border =
        new DialogueBorderStyle();
    public DialogueShadowStyle Shadow =
        new DialogueShadowStyle();
    public DialogueOpacitySettings Opacity =
        new DialogueOpacitySettings();
    [Range(-10, 10)] public int ZLayer;

    [SerializeReference]
    public List<DialogueComponentDefinition> Components =
        new List<DialogueComponentDefinition>();

    // Sub-partitioning — used ONLY by choice button groups (each group holds
    // up to 2 leaf slots = choice buttons). Null for every other slot; slots
    // elsewhere remain terminal and cannot be partitioned.
    [SerializeReference]
    public List<DialogueSlotDefinition> Children;
}

[Serializable]
public abstract class DialogueComponentDefinition
{
    public string ComponentId = Guid.NewGuid().ToString("N");
    public string DisplayName = "Component";
    public bool Enabled = true;
    public DialogueComponentType ComponentType;

    public DialogueHorizontalAlignment HorizontalAlignment =
        DialogueHorizontalAlignment.Stretch;
    public DialogueVerticalAlignment VerticalAlignment =
        DialogueVerticalAlignment.Stretch;

    public Vector2 Offset;

    public DialogueSizeValue Width = new DialogueSizeValue
    {
        Unit = DialogueSizeUnit.Auto,
        Value = 0f
    };
    public DialogueSizeValue Height = new DialogueSizeValue
    {
        Unit = DialogueSizeUnit.Auto,
        Value = 0f
    };

    public DialoguePadding Padding = new DialoguePadding();
    public DialogueOpacitySettings Opacity = new DialogueOpacitySettings();

    public bool ClipToSlot = true;
    [Range(-10, 10)] public int ZLayer;

    public DialogueBackgroundStyle Background =
        new DialogueBackgroundStyle();
    public DialogueBorderStyle Border =
        new DialogueBorderStyle();
    public DialogueShadowStyle Shadow =
        new DialogueShadowStyle();
}

[Serializable]
public sealed class DialogueTextPanelDefinition : DialogueComponentDefinition
{
    public DialogueTextStyle TextStyle = new DialogueTextStyle();
    public bool TypewriterEnabled = true;
    public float CharactersPerSecond = 30f;
    public float StartDelay;
    public string CharacterAudioKey = "";
    public DialogueLetterEffectSettings LetterEffect = new DialogueLetterEffectSettings();
    public TextAnimationProfile BaseAnimationProfile;
    public TextAnimationProfile OverlayAnimationProfile;

    public DialogueTextPanelDefinition()
    {
        ComponentType = DialogueComponentType.TextPanel;
        DisplayName = "Text Panel";
    }
}

[Serializable]
public sealed class DialogueNamePanelDefinition : DialogueComponentDefinition
{
    public DialogueHorizontalAlignment TextAlignmentHorizontalSafe()
    {
        return TextStyle != null ? TextStyle.HorizontalAlignment : DialogueHorizontalAlignment.Center;
    }

    public DialogueVerticalAlignment TextAlignmentVerticalSafe()
    {
        return TextStyle != null ? TextStyle.VerticalAlignment : DialogueVerticalAlignment.Center;
    }

    public DialogueTextStyle TextStyle = new DialogueTextStyle();
    public bool TypewriterEnabled;
    public float CharactersPerSecond = 30f;
    public float StartDelay;
    public bool Uppercase = true;
    public DialogueLetterEffectSettings LetterEffect = new DialogueLetterEffectSettings();
    public TextAnimationProfile BaseAnimationProfile;
    public TextAnimationProfile OverlayAnimationProfile;

    public DialogueNamePanelDefinition()
    {
        ComponentType = DialogueComponentType.NamePanel;
        DisplayName = "Name Panel";
    }
}

[Serializable]
public sealed class DialogueImagePanelDefinition : DialogueComponentDefinition
{
    public DialogueImagePanelMode Mode = DialogueImagePanelMode.Icon;

    public DialogueIconShape Shape = DialogueIconShape.RoundedRectangle;
    public float UniformScale = 1f;
    public float InnerPadding = 8f;
    public DialogueImageStyle ImageStyle = new DialogueImageStyle();

    public DialogueFigureScaleMode FigureScaleMode = DialogueFigureScaleMode.Fit;
    public bool FlipHorizontal;

    // Character-figure behavior: the panel hugs the loaded image (up to the
    // parent container size) and paints nothing while no image is loaded.
    public bool FitToImage = true;
    public bool HideWhenEmpty = true;
    [Range(10f, 100f)] public float MaxSizePercent = 100f;
    public string ImageSourceKey = "";

    public DialogueImagePanelDefinition()
    {
        ComponentType = DialogueComponentType.ImagePanel;
        DisplayName = "Image Panel";
    }
}

/// <summary>
/// Per-letter behaviour of a text/name panel: how each letter in a word is laid
/// out or animated (wave, zigzag, staircase, shake, fade-in, bounce).
/// A TextAnimationProfile assigned to the component overrides these inline values.
/// </summary>
[Serializable]
public sealed class DialogueLetterEffectSettings
{
    public DialogueTextEffectType EffectType = DialogueTextEffectType.None;
    [Range(0f, 48f)] public float Amplitude = 6f;
    [Range(0.05f, 3f)] public float Frequency = 0.6f;
    [Range(0f, 6.28f)] public float PhaseOffset;
    [Range(0.1f, 8f)] public float AnimationSpeed = 2f;
    public bool Loop = true;
}

/// <summary>
/// Speaker emphasis: the current speaker is fully visible while the most
/// recent interrupted speaker stays on screen greyed out.
/// </summary>
[Serializable]
public sealed class DialogueSpeakerEmphasisSettings
{
    [Tooltip("Keep the previously interrupted speaker visible next to the current speaker, greyed out.")]
    public bool GreyOutPastSpeakers = true;
    [Range(0f, 1f)] public float ActiveOpacity = 1f;
    [Range(0f, 1f)] public float InactiveOpacity = 0.4f;
    public Color InactiveTint = new Color(0.5f, 0.5f, 0.5f, 1f);
}

[Serializable]
public sealed class DialogueBackgroundStyle
{
    public DialogueBackgroundMode Mode = DialogueBackgroundMode.None;
    public Color ColorA = new Color(0f, 0f, 0f, 0.75f);
    public Color ColorB = new Color(0f, 0f, 0f, 0.75f);
    [Range(0f, 1f)] public float Opacity = 1f;
    public string SpriteSourceKey = "";
    public DialogueGradientDirection GradientDirection =
        DialogueGradientDirection.Vertical;
}

[Serializable]
public sealed class DialogueBorderStyle
{
    public bool Enabled;
    public float LeftThickness = 1f;
    public float RightThickness = 1f;
    public float TopThickness = 1f;
    public float BottomThickness = 1f;
    public Color BorderColor = Color.white;
    public float CornerRadiusTopLeft = 8f;
    public float CornerRadiusTopRight = 8f;
    public float CornerRadiusBottomLeft = 8f;
    public float CornerRadiusBottomRight = 8f;
    public string BorderSpriteSourceKey = "";
    [Range(0f, 1f)] public float Opacity = 1f;
}

[Serializable]
public sealed class DialogueShadowStyle
{
    public bool Enabled;
    public Vector2 Offset = new Vector2(2f, -2f);
    public float Blur = 4f;
    public Color Color = Color.black;
    [Range(0f, 1f)] public float Opacity = 0.5f;
}

[Serializable]
public sealed class DialogueTextStyle
{
    public string FontSourceKey = "";
    public float FontSize = 18f;
    public DialogueFontWeight FontWeight = DialogueFontWeight.Regular;
    public Color Color = Color.white;
    public float LineHeight = 1f;
    public float LetterSpacing = 0f;
    public DialogueHorizontalAlignment HorizontalAlignment =
        DialogueHorizontalAlignment.Left;
    public DialogueVerticalAlignment VerticalAlignment =
        DialogueVerticalAlignment.Center;
}

[Serializable]
public sealed class DialogueImageStyle
{
    public string ImageSourceKey = "";
    [Range(0f, 1f)] public float Opacity = 1f;
    public bool PreserveAspect = true;
    public Color Tint = Color.white;
}

[Serializable]
public sealed class DialogueOpacitySettings
{
    [Range(0f, 1f)] public float Opacity = 1f;
}

[Serializable]
public sealed class DialoguePadding
{
    public float Left;
    public float Right;
    public float Top;
    public float Bottom;
}

[Serializable]
public sealed class DialogueMinMaxSize
{
    public float MinWidth;
    public float MinHeight;
    public float MaxWidth;
    public float MaxHeight;
}

[Serializable]
public sealed class DialogueSizeValue
{
    public DialogueSizeUnit Unit = DialogueSizeUnit.Pixels;
    public float Value = 100f;
}

[Serializable]
public sealed class DialogueCustomAnchorDefinition
{
    public DialogueAnchorReferenceEdge HorizontalReference =
        DialogueAnchorReferenceEdge.Left;
    public DialogueAnchorReferenceEdge VerticalReference =
        DialogueAnchorReferenceEdge.Top;
    public float OffsetX;
    public float OffsetY;
}

public enum DialogueChoiceRegionOrientation
{
    Vertical,
    Horizontal
}

public enum DialogueChoiceButtonSizing
{
    // One size for every button instance, relative to the choice holder.
    Fixed,
    // Each button may size itself individually.
    Variable
}

/// <summary>
/// THE choice button preset. All instances share every property here exactly;
/// in Variable sizing mode, a button's width/height may additionally be
/// overridden per instance (nothing else is per-instance).
/// </summary>
[Serializable]
public sealed class DialogueChoiceButtonSettings
{
    public DialogueChoiceButtonSizing SizingMode = DialogueChoiceButtonSizing.Fixed;

    // Fixed sizing: relative to the choice holder content rect, identical for
    // every button instance.
    public DialogueSizeValue FixedWidth = new DialogueSizeValue
    { Unit = DialogueSizeUnit.Percent, Value = 100f };
    public DialogueSizeValue FixedHeight = new DialogueSizeValue
    { Unit = DialogueSizeUnit.Percent, Value = 100f };

    public DialoguePadding Padding = new DialoguePadding
    { Left = 14f, Right = 14f, Top = 8f, Bottom = 8f };

    public DialogueBackgroundStyle Background = new DialogueBackgroundStyle
    { Mode = DialogueBackgroundMode.SolidColor, ColorA = new Color(0.09f, 0.09f, 0.12f, 0.95f) };
    public DialogueBorderStyle Border = new DialogueBorderStyle
    { Enabled = true, BorderColor = new Color(1f, 1f, 1f, 0.30f), CornerRadiusTopLeft = 6f,
      CornerRadiusTopRight = 6f, CornerRadiusBottomLeft = 6f, CornerRadiusBottomRight = 6f };
    public DialogueShadowStyle Shadow = new DialogueShadowStyle();
    public DialogueOpacitySettings Opacity = new DialogueOpacitySettings();

    public DialogueTextStyle TextStyle = new DialogueTextStyle
    { FontSize = 18f, Color = Color.white, HorizontalAlignment = DialogueHorizontalAlignment.Center,
      VerticalAlignment = DialogueVerticalAlignment.Center };

    public Color HoverBackground = new Color(0.20f, 0.24f, 0.34f, 0.98f);
}
