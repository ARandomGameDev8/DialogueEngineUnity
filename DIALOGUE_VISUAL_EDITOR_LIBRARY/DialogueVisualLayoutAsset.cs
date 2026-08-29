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

    public DialogueInnerRegionDefinition InnerRegion =
        new DialogueInnerRegionDefinition();
}

[Serializable]
public sealed class DialogueInnerRegionDefinition
{
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

    [Range(0, 1)] public int PartitionLevel = 0;
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

    [Range(0, 1)] public int PartitionLevel = 0;
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

    public DialoguePadding Padding = new DialoguePadding();

    [SerializeReference]
    public List<DialogueComponentDefinition> Components =
        new List<DialogueComponentDefinition>();
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
    public DialogueTextStyle TextStyle = new DialogueTextStyle();
    public bool TypewriterEnabled;
    public float CharactersPerSecond = 30f;
    public float StartDelay;
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
    public string ImageSourceKey = "";

    public DialogueImagePanelDefinition()
    {
        ComponentType = DialogueComponentType.ImagePanel;
        DisplayName = "Image Panel";
    }
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
