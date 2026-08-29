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
        DialogueAttachedAreaDefinition area = GetArea(asset, kind);
        return area != null ? area.Slots : null;
    }

    public static DialogueSlotDefinition GetSlot(DialogueLayoutAsset asset,
        ResolvedDialogueAreaKind kind, int slotIndex)
    {
        List<DialogueSlotDefinition> slots = GetSlots(asset, kind);
        return slots != null && slotIndex >= 0 && slotIndex < slots.Count
            ? slots[slotIndex] : null;
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

    public static void RecordChange(Object target, string actionName)
    {
        Undo.RecordObject(target, actionName);
        EditorUtility.SetDirty(target);
    }
}
#endif
