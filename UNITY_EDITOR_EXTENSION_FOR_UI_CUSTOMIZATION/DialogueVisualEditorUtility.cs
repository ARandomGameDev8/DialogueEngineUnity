#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DialogueVisualEditorUtility
{
    public static DialogueAttachedAreaDefinition GetArea(DialogueLayoutAsset asset,
        ResolvedDialogueAreaKind kind)
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

    public static bool IsAreaEnabled(DialogueLayoutAsset asset,
        ResolvedDialogueAreaKind kind)
    {
        if (asset == null) return false;
        if (kind == ResolvedDialogueAreaKind.ChoiceInner)
            return asset.ChoicePanelEnabled && asset.ChoicePanel != null && asset.ChoicePanel.Enabled;
        if (kind == ResolvedDialogueAreaKind.FreeInner)
            return asset.FreePanelEnabled && asset.FreePanel != null && asset.FreePanel.Enabled;
        switch (kind)
        {
            case ResolvedDialogueAreaKind.Top: return asset.TopAreaEnabled;
            case ResolvedDialogueAreaKind.Bottom: return asset.BottomAreaEnabled;
            case ResolvedDialogueAreaKind.Left: return asset.LeftAreaEnabled;
            case ResolvedDialogueAreaKind.Right: return asset.RightAreaEnabled;
            default: return true;
        }
    }

    public static void SetAreaEnabled(DialogueLayoutAsset asset,
        ResolvedDialogueAreaKind kind, bool enabled)
    {
        if (asset == null) return;
        if (kind == ResolvedDialogueAreaKind.ChoiceInner)
        {
            asset.ChoicePanelEnabled = enabled;
            if (asset.ChoicePanel != null) asset.ChoicePanel.Enabled = enabled;
            return;
        }
        if (kind == ResolvedDialogueAreaKind.FreeInner)
        {
            asset.FreePanelEnabled = enabled;
            if (asset.FreePanel != null) asset.FreePanel.Enabled = enabled;
            return;
        }
        switch (kind)
        {
            case ResolvedDialogueAreaKind.Top: asset.TopAreaEnabled = enabled; break;
            case ResolvedDialogueAreaKind.Bottom: asset.BottomAreaEnabled = enabled; break;
            case ResolvedDialogueAreaKind.Left: asset.LeftAreaEnabled = enabled; break;
            case ResolvedDialogueAreaKind.Right: asset.RightAreaEnabled = enabled; break;
        }
        DialogueAttachedAreaDefinition area = GetArea(asset, kind);
        if (area != null) area.Enabled = enabled;
    }

    public static List<DialogueSlotDefinition> GetSlots(DialogueLayoutAsset asset,
        ResolvedDialogueAreaKind kind)
    {
        if (asset == null) return null;
        if (kind == ResolvedDialogueAreaKind.MainInner)
            return asset.MainPanel != null && asset.MainPanel.InnerRegion != null
                ? asset.MainPanel.InnerRegion.Slots : null;
        if (kind == ResolvedDialogueAreaKind.ChoiceInner)
            return asset.ChoicePanel != null && asset.ChoicePanel.InnerRegion != null
                ? asset.ChoicePanel.InnerRegion.Slots : null;
        if (kind == ResolvedDialogueAreaKind.FreeInner)
            return asset.FreePanel != null && asset.FreePanel.InnerRegion != null
                ? asset.FreePanel.InnerRegion.Slots : null;
        if (kind == ResolvedDialogueAreaKind.ChoiceGroup)
            return asset.ChoiceGroups;
        if (kind == ResolvedDialogueAreaKind.ChoiceLeaf)
        {
            var leaves = new List<DialogueSlotDefinition>();
            List<DialogueSlotDefinition> groups = asset.ChoiceGroups;
            if (groups != null)
                for (int g = 0; g < groups.Count && g < 3; g++)
                {
                    if (groups[g] == null || groups[g].Children == null) continue;
                    for (int l = 0; l < groups[g].Children.Count && l < 2; l++)
                        leaves.Add(groups[g].Children[l]);
                }
            return leaves;
        }
        DialogueAttachedAreaDefinition area = GetArea(asset, kind);
        return area != null ? area.Slots : null;
    }

    public static DialogueSlotDefinition GetSlot(DialogueLayoutAsset asset,
        ResolvedDialogueAreaKind kind, int slotIndex)
    {
        if (kind == ResolvedDialogueAreaKind.ChoiceLeaf)
        {
            List<DialogueSlotDefinition> groups = asset != null ? asset.ChoiceGroups : null;
            int g = slotIndex / 2;
            int l = slotIndex % 2;
            if (groups == null || g < 0 || g >= groups.Count || groups[g] == null ||
                groups[g].Children == null || l >= groups[g].Children.Count)
                return null;
            return groups[g].Children[l];
        }
        List<DialogueSlotDefinition> slots = GetSlots(asset, kind);
        return slots != null && slotIndex >= 0 && slotIndex < slots.Count
            ? slots[slotIndex] : null;
    }

    /// <summary>The group definition that owns the encoded leaf index.</summary>
    public static DialogueSlotDefinition GetChoiceLeafGroup(DialogueLayoutAsset asset, int leafIndex)
    {
        List<DialogueSlotDefinition> groups = asset != null ? asset.ChoiceGroups : null;
        int g = leafIndex / 2;
        return groups != null && g >= 0 && g < groups.Count ? groups[g] : null;
    }

    public static void EnsureChoiceGroups(DialogueLayoutAsset asset)
    {
        if (asset == null) return;
        if (asset.ChoiceGroups == null)
            asset.ChoiceGroups = new List<DialogueSlotDefinition>();
        while (asset.ChoiceGroups.Count < 3)
            asset.ChoiceGroups.Add(new DialogueSlotDefinition
            {
                SlotId = "G" + (asset.ChoiceGroups.Count + 1),
                DisplayName = "Button Group " + (asset.ChoiceGroups.Count + 1)
            });
        for (int i = 0; i < asset.ChoiceGroups.Count; i++)
        {
            if (asset.ChoiceGroups[i] == null) continue;
            if (asset.ChoiceGroups[i].Children == null)
                asset.ChoiceGroups[i].Children = new List<DialogueSlotDefinition>();
        }
    }

    public static void SetChoiceGroupLeafCount(DialogueSlotDefinition group, int count)
    {
        if (group == null) return;
        if (group.Children == null) group.Children = new List<DialogueSlotDefinition>();
        count = Mathf.Clamp(count, 1, 2);
        while (group.Children.Count > count) group.Children.RemoveAt(group.Children.Count - 1);
        while (group.Children.Count < count)
            group.Children.Add(new DialogueSlotDefinition
            {
                SlotId = "B" + (group.Children.Count + 1),
                DisplayName = "Choice Button " + (group.Children.Count + 1)
            });
    }

    public static DialogueComponentDefinition GetComponent(DialogueLayoutAsset asset,
        ResolvedDialogueAreaKind kind, int slotIndex, int componentIndex)
    {
        DialogueSlotDefinition slot = GetSlot(asset, kind, slotIndex);
        return slot != null && slot.Components != null &&
               componentIndex >= 0 && componentIndex < slot.Components.Count
            ? slot.Components[componentIndex] : null;
    }

    public static DialogueComponentDefinition CreateComponent(DialogueComponentType type)
    {
        switch (type)
        {
            case DialogueComponentType.NamePanel: return new DialogueNamePanelDefinition();
            case DialogueComponentType.ImagePanel: return new DialogueImagePanelDefinition();
            default: return new DialogueTextPanelDefinition();
        }
    }

    public static void EnsureSlotArrays(DialogueLayoutAsset asset)
    {
        if (asset == null) return;
        EnsureSlots(asset.MainPanel != null ? asset.MainPanel.InnerRegion : null);
        if (asset.ChoicePanelEnabled)
        {
            EnsureSlots(asset.ChoicePanel != null ? asset.ChoicePanel.InnerRegion : null);
            EnsureChoiceGroups(asset);
        }
        if (asset.FreePanelEnabled)
            EnsureSlots(asset.FreePanel != null ? asset.FreePanel.InnerRegion : null);
        EnsureSlots(asset.TopArea);
        EnsureSlots(asset.BottomArea);
        EnsureSlots(asset.LeftArea);
        EnsureSlots(asset.RightArea);
    }

    public static int GetVisibleSlotCount(DialogueInnerRegionDefinition region)
    {
        return region != null ? 1 + Mathf.Clamp(region.PartitionLevel, 0, 2) : 1;
    }

    public static int GetVisibleSlotCount(DialogueAttachedAreaDefinition area)
    {
        return area != null ? 1 + Mathf.Clamp(area.PartitionLevel, 0, 2) : 1;
    }

    public static void SyncVisibleSlotsFromRegion(DialogueInnerRegionDefinition region)
    {
        if (region == null || region.Slots == null) return;
        EnsureSlots(region.Slots);
        int count = GetVisibleSlotCount(region);
        for (int i = 0; i < count && i < region.Slots.Count; i++)
            ApplyParentDefaultsToSlot(region.Slots[i], region.Background, region.Border,
                region.Shadow, region.Opacity, region.ZLayer);
    }

    public static void SyncVisibleSlotsFromArea(DialogueAttachedAreaDefinition area)
    {
        if (area == null || area.Slots == null) return;
        EnsureSlots(area.Slots);
        int count = GetVisibleSlotCount(area);
        for (int i = 0; i < count && i < area.Slots.Count; i++)
            ApplyParentDefaultsToSlot(area.Slots[i], area.Background, area.Border,
                area.Shadow, area.Opacity, area.ZLayer);
    }

    static void EnsureSlots(DialogueInnerRegionDefinition region)
    {
        if (region == null)
            return;
        if (region.Slots == null)
            region.Slots = new List<DialogueSlotDefinition>();
        EnsureSlots(region.Slots);
    }

    static void EnsureSlots(DialogueAttachedAreaDefinition area)
    {
        if (area == null)
            return;
        if (area.Slots == null)
            area.Slots = new List<DialogueSlotDefinition>();
        EnsureSlots(area.Slots);
    }

    static void EnsureSlots(List<DialogueSlotDefinition> slots)
    {
        while (slots.Count < 6)
            slots.Add(new DialogueSlotDefinition { SlotId = GetSlotId(slots.Count), DisplayName = "Slot " + GetSlotId(slots.Count) });
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                slots[i] = new DialogueSlotDefinition();
            if (string.IsNullOrEmpty(slots[i].SlotId))
                slots[i].SlotId = GetSlotId(i);
            if (string.IsNullOrEmpty(slots[i].DisplayName))
                slots[i].DisplayName = "Slot " + slots[i].SlotId;
            if (slots[i].Components == null)
                slots[i].Components = new List<DialogueComponentDefinition>();
        }
    }

    static string GetSlotId(int index)
    {
        const string letters = "ABCDEF";
        return index >= 0 && index < letters.Length ? letters[index].ToString() : "S" + index;
    }

    static void ApplyParentDefaultsToSlot(DialogueSlotDefinition slot,
        DialogueBackgroundStyle background, DialogueBorderStyle border,
        DialogueShadowStyle shadow, DialogueOpacitySettings opacity,
        int zLayer)
    {
        if (slot == null) return;
        slot.Width.Unit = DialogueSizeUnit.Auto;
        slot.Width.Value = 0f;
        slot.Height.Unit = DialogueSizeUnit.Auto;
        slot.Height.Value = 0f;
        slot.GapAfter = -1f;
        slot.Offset = Vector2.zero;

        if (background != null)
        {
            slot.Background.Mode = background.Mode;
            slot.Background.ColorA = background.ColorA;
            slot.Background.ColorB = background.ColorB;
            slot.Background.Opacity = background.Opacity;
            slot.Background.SpriteSourceKey = background.SpriteSourceKey;
            slot.Background.GradientDirection = background.GradientDirection;
        }
        if (border != null)
        {
            slot.Border.Enabled = border.Enabled;
            slot.Border.LeftThickness = border.LeftThickness;
            slot.Border.RightThickness = border.RightThickness;
            slot.Border.TopThickness = border.TopThickness;
            slot.Border.BottomThickness = border.BottomThickness;
            slot.Border.BorderColor = border.BorderColor;
            slot.Border.CornerRadiusTopLeft = border.CornerRadiusTopLeft;
            slot.Border.CornerRadiusTopRight = border.CornerRadiusTopRight;
            slot.Border.CornerRadiusBottomLeft = border.CornerRadiusBottomLeft;
            slot.Border.CornerRadiusBottomRight = border.CornerRadiusBottomRight;
            slot.Border.BorderSpriteSourceKey = border.BorderSpriteSourceKey;
            slot.Border.Opacity = border.Opacity;
        }
        if (shadow != null)
        {
            slot.Shadow.Enabled = shadow.Enabled;
            slot.Shadow.Offset = shadow.Offset;
            slot.Shadow.Blur = shadow.Blur;
            slot.Shadow.Color = shadow.Color;
            slot.Shadow.Opacity = shadow.Opacity;
        }
        if (opacity != null)
            slot.Opacity.Opacity = opacity.Opacity;
        slot.ZLayer = zLayer;
    }

    public static bool TryGetOppositeAreaKind(ResolvedDialogueAreaKind kind,
        out ResolvedDialogueAreaKind opposite)
    {
        switch (kind)
        {
            case ResolvedDialogueAreaKind.Left:
                opposite = ResolvedDialogueAreaKind.Right;
                return true;
            case ResolvedDialogueAreaKind.Right:
                opposite = ResolvedDialogueAreaKind.Left;
                return true;
            case ResolvedDialogueAreaKind.Top:
                opposite = ResolvedDialogueAreaKind.Bottom;
                return true;
            case ResolvedDialogueAreaKind.Bottom:
                opposite = ResolvedDialogueAreaKind.Top;
                return true;
            default:
                opposite = ResolvedDialogueAreaKind.MainInner;
                return false;
        }
    }

    public static string GetAreaKindDisplayName(ResolvedDialogueAreaKind kind)
    {
        switch (kind)
        {
            case ResolvedDialogueAreaKind.Top: return "Top Area";
            case ResolvedDialogueAreaKind.Bottom: return "Bottom Area";
            case ResolvedDialogueAreaKind.Left: return "Left Area";
            case ResolvedDialogueAreaKind.Right: return "Right Area";
            default: return "Inner Region";
        }
    }

    public static void CopyAreaToOpposite(DialogueLayoutAsset asset,
        ResolvedDialogueAreaKind sourceKind)
    {
        ResolvedDialogueAreaKind oppositeKind;
        if (asset == null || !TryGetOppositeAreaKind(sourceKind, out oppositeKind))
            return;

        EnsureSlotArrays(asset);

        DialogueAttachedAreaDefinition source = GetArea(asset, sourceKind);
        DialogueAttachedAreaDefinition target = GetArea(asset, oppositeKind);
        if (source == null || target == null)
            return;

        string preservedDisplayName = string.IsNullOrEmpty(target.DisplayName)
            ? GetAreaKindDisplayName(oppositeKind)
            : target.DisplayName;
        DialogueAttachedAreaSide preservedSide = target.Side;

        CopyAreaDefinition(source, target);

        target.DisplayName = preservedDisplayName;
        target.Side = preservedSide;
        SetAreaEnabled(asset, oppositeKind, source.Enabled && IsAreaEnabled(asset, sourceKind));
    }

    public static void CopySlotToOpposite(DialogueLayoutAsset asset,
        ResolvedDialogueAreaKind sourceKind, int slotIndex)
    {
        ResolvedDialogueAreaKind oppositeKind;
        if (asset == null || !TryGetOppositeAreaKind(sourceKind, out oppositeKind))
            return;

        EnsureSlotArrays(asset);

        DialogueSlotDefinition source = GetSlot(asset, sourceKind, slotIndex);
        DialogueSlotDefinition target = GetSlot(asset, oppositeKind, slotIndex);
        if (source == null || target == null)
            return;

        DialogueAttachedAreaDefinition sourceArea = GetArea(asset, sourceKind);
        DialogueAttachedAreaDefinition targetArea = GetArea(asset, oppositeKind);
        if (sourceArea != null && targetArea != null)
        {
            targetArea.Enabled = sourceArea.Enabled;
            targetArea.PartitionLevel = Mathf.Max(targetArea.PartitionLevel, sourceArea.PartitionLevel);
            SetAreaEnabled(asset, oppositeKind, sourceArea.Enabled && IsAreaEnabled(asset, sourceKind));
            EnsureSlots(targetArea.Slots);
        }

        CopySlotDefinition(source, target);
    }

    static void CopyAreaDefinition(DialogueAttachedAreaDefinition source,
        DialogueAttachedAreaDefinition target)
    {
        if (source == null || target == null)
            return;

        target.Enabled = source.Enabled;
        CopySizeValue(source.Width, target.Width);
        CopySizeValue(source.Height, target.Height);
        target.GapFromMainPanel = source.GapFromMainPanel;
        target.Offset = source.Offset;
        target.PartitionLevel = source.PartitionLevel;
        target.InterSlotSpacing = source.InterSlotSpacing;
        CopyBackgroundStyle(source.Background, target.Background);
        CopyBorderStyle(source.Border, target.Border);
        CopyShadowStyle(source.Shadow, target.Shadow);
        CopyOpacitySettings(source.Opacity, target.Opacity);
        target.ZLayer = source.ZLayer;

        if (target.Slots == null)
            target.Slots = new List<DialogueSlotDefinition>();
        target.Slots.Clear();
        if (source.Slots != null)
        {
            for (int i = 0; i < source.Slots.Count; i++)
                target.Slots.Add(CloneSlotDefinition(source.Slots[i]));
        }

        EnsureSlots(target.Slots);
    }

    static DialogueSlotDefinition CloneSlotDefinition(DialogueSlotDefinition source)
    {
        var clone = new DialogueSlotDefinition();
        CopySlotDefinition(source, clone);
        return clone;
    }

    static void CopySlotDefinition(DialogueSlotDefinition source,
        DialogueSlotDefinition target)
    {
        if (source == null || target == null)
            return;

        target.SlotId = source.SlotId;
        target.DisplayName = source.DisplayName;
        target.Enabled = source.Enabled;
        CopySizeValue(source.Width, target.Width);
        CopySizeValue(source.Height, target.Height);
        target.GapAfter = source.GapAfter;
        target.Offset = source.Offset;
        CopyPadding(source.Padding, target.Padding);
        CopyBackgroundStyle(source.Background, target.Background);
        CopyBorderStyle(source.Border, target.Border);
        CopyShadowStyle(source.Shadow, target.Shadow);
        CopyOpacitySettings(source.Opacity, target.Opacity);
        target.ZLayer = source.ZLayer;

        if (target.Components == null)
            target.Components = new List<DialogueComponentDefinition>();
        target.Components.Clear();
        if (source.Components != null)
        {
            for (int i = 0; i < source.Components.Count; i++)
                target.Components.Add(CloneComponent(source.Components[i]));
        }
    }

    static DialogueComponentDefinition CloneComponent(DialogueComponentDefinition source)
    {
        if (source == null)
            return null;

        DialogueComponentDefinition clone = CreateComponent(source.ComponentType);
        if (clone == null)
            return null;

        clone.ComponentId = System.Guid.NewGuid().ToString("N");
        clone.DisplayName = source.DisplayName;
        clone.Enabled = source.Enabled;
        clone.ComponentType = source.ComponentType;
        clone.HorizontalAlignment = source.HorizontalAlignment;
        clone.VerticalAlignment = source.VerticalAlignment;
        clone.Offset = source.Offset;
        CopySizeValue(source.Width, clone.Width);
        CopySizeValue(source.Height, clone.Height);
        CopyPadding(source.Padding, clone.Padding);
        CopyOpacitySettings(source.Opacity, clone.Opacity);
        clone.ClipToSlot = source.ClipToSlot;
        clone.ZLayer = source.ZLayer;
        CopyBackgroundStyle(source.Background, clone.Background);
        CopyBorderStyle(source.Border, clone.Border);
        CopyShadowStyle(source.Shadow, clone.Shadow);

        if (source is DialogueTextPanelDefinition sourceText && clone is DialogueTextPanelDefinition cloneText)
        {
            CopyTextStyle(sourceText.TextStyle, cloneText.TextStyle);
            cloneText.TypewriterEnabled = sourceText.TypewriterEnabled;
            cloneText.CharactersPerSecond = sourceText.CharactersPerSecond;
            cloneText.StartDelay = sourceText.StartDelay;
            cloneText.CharacterAudioKey = sourceText.CharacterAudioKey;
            cloneText.BaseAnimationProfile = sourceText.BaseAnimationProfile;
            cloneText.OverlayAnimationProfile = sourceText.OverlayAnimationProfile;
        }
        else if (source is DialogueNamePanelDefinition sourceName && clone is DialogueNamePanelDefinition cloneName)
        {
            CopyTextStyle(sourceName.TextStyle, cloneName.TextStyle);
            cloneName.TypewriterEnabled = sourceName.TypewriterEnabled;
            cloneName.CharactersPerSecond = sourceName.CharactersPerSecond;
            cloneName.StartDelay = sourceName.StartDelay;
            cloneName.BaseAnimationProfile = sourceName.BaseAnimationProfile;
            cloneName.OverlayAnimationProfile = sourceName.OverlayAnimationProfile;
        }
        else if (source is DialogueImagePanelDefinition sourceImage && clone is DialogueImagePanelDefinition cloneImage)
        {
            cloneImage.Mode = sourceImage.Mode;
            cloneImage.Shape = sourceImage.Shape;
            cloneImage.UniformScale = sourceImage.UniformScale;
            cloneImage.InnerPadding = sourceImage.InnerPadding;
            CopyImageStyle(sourceImage.ImageStyle, cloneImage.ImageStyle);
            cloneImage.FigureScaleMode = sourceImage.FigureScaleMode;
            cloneImage.FlipHorizontal = sourceImage.FlipHorizontal;
            cloneImage.ImageSourceKey = sourceImage.ImageSourceKey;
        }

        return clone;
    }

    static void CopySizeValue(DialogueSizeValue source, DialogueSizeValue target)
    {
        if (source == null || target == null)
            return;

        target.Unit = source.Unit;
        target.Value = source.Value;
    }

    static void CopyPadding(DialoguePadding source, DialoguePadding target)
    {
        if (source == null || target == null)
            return;

        target.Left = source.Left;
        target.Right = source.Right;
        target.Top = source.Top;
        target.Bottom = source.Bottom;
    }

    static void CopyBackgroundStyle(DialogueBackgroundStyle source,
        DialogueBackgroundStyle target)
    {
        if (source == null || target == null)
            return;

        target.Mode = source.Mode;
        target.ColorA = source.ColorA;
        target.ColorB = source.ColorB;
        target.Opacity = source.Opacity;
        target.SpriteSourceKey = source.SpriteSourceKey;
        target.GradientDirection = source.GradientDirection;
    }

    static void CopyBorderStyle(DialogueBorderStyle source,
        DialogueBorderStyle target)
    {
        if (source == null || target == null)
            return;

        target.Enabled = source.Enabled;
        target.LeftThickness = source.LeftThickness;
        target.RightThickness = source.RightThickness;
        target.TopThickness = source.TopThickness;
        target.BottomThickness = source.BottomThickness;
        target.BorderColor = source.BorderColor;
        target.CornerRadiusTopLeft = source.CornerRadiusTopLeft;
        target.CornerRadiusTopRight = source.CornerRadiusTopRight;
        target.CornerRadiusBottomLeft = source.CornerRadiusBottomLeft;
        target.CornerRadiusBottomRight = source.CornerRadiusBottomRight;
        target.BorderSpriteSourceKey = source.BorderSpriteSourceKey;
        target.Opacity = source.Opacity;
    }

    static void CopyShadowStyle(DialogueShadowStyle source,
        DialogueShadowStyle target)
    {
        if (source == null || target == null)
            return;

        target.Enabled = source.Enabled;
        target.Offset = source.Offset;
        target.Blur = source.Blur;
        target.Color = source.Color;
        target.Opacity = source.Opacity;
    }

    static void CopyOpacitySettings(DialogueOpacitySettings source,
        DialogueOpacitySettings target)
    {
        if (source == null || target == null)
            return;

        target.Opacity = source.Opacity;
    }

    static void CopyTextStyle(DialogueTextStyle source, DialogueTextStyle target)
    {
        if (source == null || target == null)
            return;

        target.FontSourceKey = source.FontSourceKey;
        target.FontSize = source.FontSize;
        target.FontWeight = source.FontWeight;
        target.Color = source.Color;
        target.LineHeight = source.LineHeight;
        target.LetterSpacing = source.LetterSpacing;
        target.HorizontalAlignment = source.HorizontalAlignment;
        target.VerticalAlignment = source.VerticalAlignment;
    }

    static void CopyImageStyle(DialogueImageStyle source, DialogueImageStyle target)
    {
        if (source == null || target == null)
            return;

        target.ImageSourceKey = source.ImageSourceKey;
        target.Opacity = source.Opacity;
        target.PreserveAspect = source.PreserveAspect;
        target.Tint = source.Tint;
    }

    public static void RecordChange(Object target, string actionName)
    {
        Undo.RecordObject(target, actionName);
        EditorUtility.SetDirty(target);
    }
}
#endif
