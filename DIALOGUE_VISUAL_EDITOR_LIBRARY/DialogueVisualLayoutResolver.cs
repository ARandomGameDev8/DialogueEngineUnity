using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ResolvedDialogueLayout
{
    public Rect CanvasRect;
    public Rect MainPanelRect;
    public int MainPanelZ;
    public readonly List<ResolvedDialogueArea> Areas = new List<ResolvedDialogueArea>();
    public readonly List<ResolvedDialogueSlot> Slots = new List<ResolvedDialogueSlot>();
    public readonly List<ResolvedDialogueComponentRect> Components = new List<ResolvedDialogueComponentRect>();
}

public enum ResolvedDialogueAreaKind
{
    MainInner,
    Top,
    Bottom,
    Left,
    Right
}

[Serializable]
public sealed class ResolvedDialogueArea
{
    public string Name;
    public ResolvedDialogueAreaKind AreaKind;
    public DialogueAttachedAreaSide Side;
    public Rect Rect;
    public int ZLayer;
}

[Serializable]
public sealed class ResolvedDialogueSlot
{
    public string AreaName;
    public ResolvedDialogueAreaKind AreaKind;
    public int SlotIndex;
    public string SlotId;
    public Rect Rect;
}

[Serializable]
public sealed class ResolvedDialogueComponentRect
{
    public string AreaName;
    public ResolvedDialogueAreaKind AreaKind;
    public int SlotIndex;
    public int ComponentIndex;
    public string SlotId;
    public string DisplayName;
    public DialogueComponentType ComponentType;
    public Rect Rect;
    public bool ClipToSlot;
    public int ZLayer;
}

public static class DialogueVisualLayoutResolver
{
    public static ResolvedDialogueLayout Resolve(DialogueLayoutAsset asset, Rect canvasRect)
    {
        var resolved = new ResolvedDialogueLayout
        {
            CanvasRect = canvasRect
        };
        if (asset == null || asset.MainPanel == null) return resolved;

        resolved.MainPanelRect = ResolveMainPanelRect(asset.MainPanel, canvasRect);
        resolved.MainPanelZ = asset.MainPanel.ZLayer;

        Rect mainInnerRect = ShrinkRect(resolved.MainPanelRect, asset.MainPanel.Padding);
        ResolveInnerRegion(asset.MainPanel.InnerRegion, mainInnerRect, "Main Panel / Inner Region", resolved);

        ResolveOuterArea(asset.TopAreaEnabled ? asset.TopArea : null, resolved.MainPanelRect, canvasRect, resolved);
        ResolveOuterArea(asset.BottomAreaEnabled ? asset.BottomArea : null, resolved.MainPanelRect, canvasRect, resolved);
        ResolveOuterArea(asset.LeftAreaEnabled ? asset.LeftArea : null, resolved.MainPanelRect, canvasRect, resolved);
        ResolveOuterArea(asset.RightAreaEnabled ? asset.RightArea : null, resolved.MainPanelRect, canvasRect, resolved);

        return resolved;
    }

    static Rect ResolveMainPanelRect(DialogueMainPanelDefinition def, Rect canvas)
    {
        float width = ResolveSize(def != null ? def.Width : null, canvas.width, 460f);
        float height = ResolveSize(def != null ? def.Height : null, canvas.height, 180f);

        if (def != null)
        {
            width = Mathf.Clamp(width, def.MinMax.MinWidth > 0f ? def.MinMax.MinWidth : 0f,
                def.MinMax.MaxWidth > 0f ? def.MinMax.MaxWidth : 100000f);
            height = Mathf.Clamp(height, def.MinMax.MinHeight > 0f ? def.MinMax.MinHeight : 0f,
                def.MinMax.MaxHeight > 0f ? def.MinMax.MaxHeight : 100000f);
        }

        DialogueAnchorPreset anchor = def != null ? def.AnchorPreset : DialogueAnchorPreset.Bottom;
        DialoguePanelFillMode fill = def != null ? def.FillMode : DialoguePanelFillMode.Fixed;

        if (anchor == DialogueAnchorPreset.Top || anchor == DialogueAnchorPreset.Bottom)
        {
            if (fill == DialoguePanelFillMode.StretchHorizontal)
                width = canvas.width;
        }
        else if (anchor == DialogueAnchorPreset.Left || anchor == DialogueAnchorPreset.Right)
        {
            if (fill == DialoguePanelFillMode.StretchVertical)
                height = canvas.height;
        }

        float x = canvas.center.x - width * 0.5f;
        float y = canvas.center.y - height * 0.5f;

        switch (anchor)
        {
            case DialogueAnchorPreset.TopLeft:
                x = canvas.xMin;
                y = canvas.yMin;
                break;
            case DialogueAnchorPreset.Top:
                x = canvas.center.x - width * 0.5f;
                y = canvas.yMin;
                break;
            case DialogueAnchorPreset.TopRight:
                x = canvas.xMax - width;
                y = canvas.yMin;
                break;
            case DialogueAnchorPreset.Left:
                x = canvas.xMin;
                y = canvas.center.y - height * 0.5f;
                break;
            case DialogueAnchorPreset.Center:
                x = canvas.center.x - width * 0.5f;
                y = canvas.center.y - height * 0.5f;
                break;
            case DialogueAnchorPreset.Right:
                x = canvas.xMax - width;
                y = canvas.center.y - height * 0.5f;
                break;
            case DialogueAnchorPreset.BottomLeft:
                x = canvas.xMin;
                y = canvas.yMax - height;
                break;
            case DialogueAnchorPreset.Bottom:
                x = canvas.center.x - width * 0.5f;
                y = canvas.yMax - height;
                break;
            case DialogueAnchorPreset.BottomRight:
                x = canvas.xMax - width;
                y = canvas.yMax - height;
                break;
            case DialogueAnchorPreset.Custom:
                x = ResolveCustomX(def != null ? def.CustomAnchor : null, canvas, width);
                y = ResolveCustomY(def != null ? def.CustomAnchor : null, canvas, height);
                break;
        }

        return new Rect(x, y, width, height);
    }

    static void ResolveInnerRegion(DialogueInnerRegionDefinition def, Rect parentRect,
        string areaName, ResolvedDialogueLayout resolved)
    {
        if (def == null) return;

        float width = ResolveSize(def.Width, parentRect.width, parentRect.width);
        float height = ResolveSize(def.Height, parentRect.height, parentRect.height);
        int slotCount = GetPartitionSlotCount(def.PartitionLevel);
        if (slotCount > 1)
            ResolvePartitionedParentSize(def.Slots, true, slotCount, def.InterSlotSpacing,
                width, height, out width, out height);
        width = Mathf.Min(width, parentRect.width);
        height = Mathf.Min(height, parentRect.height);

        Rect areaRect = new Rect(
            parentRect.center.x - width * 0.5f + def.Offset.x,
            parentRect.center.y - height * 0.5f + def.Offset.y,
            width,
            height);

        resolved.Areas.Add(new ResolvedDialogueArea
        {
            Name = areaName,
            AreaKind = ResolvedDialogueAreaKind.MainInner,
            Side = DialogueAttachedAreaSide.Top,
            Rect = areaRect,
            ZLayer = 0
        });

        ResolveSlotsAndComponents(areaName, ResolvedDialogueAreaKind.MainInner,
            areaRect, true, GetPartitionSlotCount(def.PartitionLevel),
            def.InterSlotSpacing, def.Slots, resolved);
    }

    static void ResolveOuterArea(DialogueAttachedAreaDefinition def, Rect mainRect,
        Rect canvasRect, ResolvedDialogueLayout resolved)
    {
        if (def == null || !def.Enabled) return;

        float widthReference =
            (def.Side == DialogueAttachedAreaSide.Top || def.Side == DialogueAttachedAreaSide.Bottom)
                ? mainRect.width : canvasRect.width;
        float heightReference =
            (def.Side == DialogueAttachedAreaSide.Left || def.Side == DialogueAttachedAreaSide.Right)
                ? mainRect.height : canvasRect.height;

        float width = ResolveSize(def.Width, widthReference,
            def.Side == DialogueAttachedAreaSide.Left || def.Side == DialogueAttachedAreaSide.Right
                ? 180f : mainRect.width);
        float height = ResolveSize(def.Height, heightReference,
            def.Side == DialogueAttachedAreaSide.Top || def.Side == DialogueAttachedAreaSide.Bottom
                ? 100f : mainRect.height);
        int slotCount = GetPartitionSlotCount(def.PartitionLevel);
        bool horizontal = def.Side == DialogueAttachedAreaSide.Top ||
                          def.Side == DialogueAttachedAreaSide.Bottom;
        if (slotCount > 1 && HasPartitionedSlotOverrides(def.Slots, horizontal, slotCount))
            ResolvePartitionedParentSize(def.Slots, horizontal, slotCount, def.InterSlotSpacing,
                width, height, out width, out height);

        Rect areaRect = new Rect(mainRect.xMin, mainRect.yMin, width, height);
        switch (def.Side)
        {
            case DialogueAttachedAreaSide.Top:
                areaRect.x = mainRect.center.x - width * 0.5f;
                areaRect.y = mainRect.yMin - def.GapFromMainPanel - height;
                break;
            case DialogueAttachedAreaSide.Bottom:
                areaRect.x = mainRect.center.x - width * 0.5f;
                areaRect.y = mainRect.yMax + def.GapFromMainPanel;
                break;
            case DialogueAttachedAreaSide.Left:
                areaRect.x = mainRect.xMin - def.GapFromMainPanel - width;
                areaRect.y = mainRect.center.y - height * 0.5f;
                break;
            case DialogueAttachedAreaSide.Right:
                areaRect.x = mainRect.xMax + def.GapFromMainPanel;
                areaRect.y = mainRect.center.y - height * 0.5f;
                break;
        }

        ResolvedDialogueAreaKind kind = ToAreaKind(def.Side);
        resolved.Areas.Add(new ResolvedDialogueArea
        {
            Name = def.DisplayName,
            AreaKind = kind,
            Side = def.Side,
            Rect = areaRect,
            ZLayer = def.ZLayer
        });

        ResolveSlotsAndComponents(def.DisplayName, kind, areaRect, horizontal,
            slotCount, def.InterSlotSpacing, def.Slots, resolved);
    }

    static void ResolveSlotsAndComponents(string areaName,
        ResolvedDialogueAreaKind areaKind, Rect areaRect, bool horizontal,
        int slotCount, float interSlotSpacing,
        List<DialogueSlotDefinition> slots, ResolvedDialogueLayout resolved)
    {
        if (slots == null || slots.Count == 0) return;
        int clampedSlotCount = Mathf.Clamp(slotCount, 1, 3);
        ResolveSlotRow(areaName, areaKind, areaRect, horizontal, 0, clampedSlotCount,
            interSlotSpacing, slots, resolved, 0);
    }

    static void ResolveSlotRow(string areaName, ResolvedDialogueAreaKind areaKind,
        Rect rowRect, bool horizontal, int startIndex, int slotCount, float spacing,
        List<DialogueSlotDefinition> slots, ResolvedDialogueLayout resolved, int slotOffset)
    {
        if (slotCount <= 0) return;

        List<DialogueSlotDefinition> active = new List<DialogueSlotDefinition>();
        List<int> activeIndices = new List<int>();
        for (int i = 0; i < slotCount; i++)
        {
            int idx = startIndex + i;
            if (idx >= slots.Count) break;
            DialogueSlotDefinition slot = slots[idx];
            if (slot == null || !slot.Enabled) continue;
            active.Add(slot);
            activeIndices.Add(idx);
        }
        if (active.Count == 0) return;

        List<float> sizes = ResolveDistributedSizes(active, horizontal,
            horizontal ? rowRect.width : rowRect.height, rowRect.width, rowRect.height,
            spacing);
        float cursor = horizontal ? rowRect.xMin : rowRect.yMin;
        for (int i = 0; i < active.Count; i++)
        {
            DialogueSlotDefinition slot = active[i];
            int slotIndex = activeIndices[i];
            Rect slotRect;
            float gapAfter = i < active.Count - 1 ? GetGapAfter(slot, spacing) : 0f;
            if (horizontal)
            {
                float slotHeight = ResolveSize(slot.Height, rowRect.height, rowRect.height);
                slotHeight = Mathf.Min(slotHeight, rowRect.height);
                slotRect = new Rect(cursor, rowRect.yMin, sizes[i], slotHeight);
                cursor += sizes[i] + gapAfter;
            }
            else
            {
                float slotWidth = ResolveSize(slot.Width, rowRect.width, rowRect.width);
                slotWidth = Mathf.Min(slotWidth, rowRect.width);
                slotRect = new Rect(rowRect.xMin, cursor, slotWidth, sizes[i]);
                cursor += sizes[i] + gapAfter;
            }

            resolved.Slots.Add(new ResolvedDialogueSlot
            {
                AreaName = areaName,
                AreaKind = areaKind,
                SlotIndex = slotIndex,
                SlotId = slot.SlotId,
                Rect = slotRect
            });

            Rect contentRect = ShrinkRect(slotRect, slot.Padding);
            if (slot.Components == null) continue;
            for (int c = 0; c < slot.Components.Count; c++)
            {
                DialogueComponentDefinition component = slot.Components[c];
                if (component == null || !component.Enabled) continue;
                Rect componentRect = ResolveComponentRect(component, contentRect);
                resolved.Components.Add(new ResolvedDialogueComponentRect
                {
                    AreaName = areaName,
                    AreaKind = areaKind,
                    SlotIndex = slotIndex,
                    ComponentIndex = c,
                    SlotId = slot.SlotId,
                    DisplayName = string.IsNullOrEmpty(component.DisplayName)
                        ? component.ComponentType.ToString() : component.DisplayName,
                    ComponentType = component.ComponentType,
                    Rect = componentRect,
                    ClipToSlot = component.ClipToSlot,
                    ZLayer = component.ZLayer
                });
            }
        }
    }

    static Rect ResolveComponentRect(DialogueComponentDefinition component, Rect slotContentRect)
    {
        Rect padded = ShrinkRect(slotContentRect, component.Padding);
        float width = ResolveSize(component.Width, padded.width, padded.width);
        float height = ResolveSize(component.Height, padded.height, padded.height);
        width = Mathf.Min(width, padded.width);
        height = Mathf.Min(height, padded.height);

        float x = padded.xMin;
        switch (component.HorizontalAlignment)
        {
            case DialogueHorizontalAlignment.Center:
                x = padded.center.x - width * 0.5f;
                break;
            case DialogueHorizontalAlignment.Right:
                x = padded.xMax - width;
                break;
            case DialogueHorizontalAlignment.Stretch:
                x = padded.xMin;
                width = padded.width;
                break;
        }

        float y = padded.yMin;
        switch (component.VerticalAlignment)
        {
            case DialogueVerticalAlignment.Center:
                y = padded.center.y - height * 0.5f;
                break;
            case DialogueVerticalAlignment.Bottom:
                y = padded.yMax - height;
                break;
            case DialogueVerticalAlignment.Stretch:
                y = padded.yMin;
                height = padded.height;
                break;
        }

        return new Rect(x + component.Offset.x, y + component.Offset.y, width, height);
    }

    static List<float> ResolveDistributedSizes(List<DialogueSlotDefinition> slots,
        bool horizontal, float totalPrimarySpace, float availableWidth,
        float availableHeight, float defaultSpacing)
    {
        var sizes = new List<float>(slots.Count);
        float totalSpacing = 0f;
        for (int i = 0; i < slots.Count - 1; i++)
            totalSpacing += GetGapAfter(slots[i], defaultSpacing);
        float usable = Mathf.Max(0f, totalPrimarySpace - totalSpacing);

        float fixedTotal = 0f;
        int autoCount = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            DialogueSizeValue size = horizontal ? slots[i].Width : slots[i].Height;
            if (size == null || size.Unit == DialogueSizeUnit.Auto)
            {
                sizes.Add(-1f);
                autoCount++;
                continue;
            }
            float resolved = ResolveSize(size, usable, horizontal ? 180f : 140f);
            resolved = Mathf.Max(0f, resolved);
            sizes.Add(resolved);
            fixedTotal += resolved;
        }

        float remaining = Mathf.Max(0f, usable - fixedTotal);
        float autoSize = autoCount > 0 ? remaining / autoCount : 0f;
        if (autoCount > 0 && autoSize <= 0f)
            autoSize = horizontal ? 180f : 140f;

        for (int i = 0; i < sizes.Count; i++)
            if (sizes[i] < 0f) sizes[i] = autoSize;
        return sizes;
    }

    static void ResolvePartitionedParentSize(List<DialogueSlotDefinition> slots,
        bool horizontal, int slotCount, float defaultSpacing,
        float originalWidth, float originalHeight,
        out float fittedWidth, out float fittedHeight)
    {
        fittedWidth = originalWidth;
        fittedHeight = originalHeight;
        if (slots == null || slots.Count == 0 || slotCount <= 1) return;

        int visible = Mathf.Min(slotCount, slots.Count);
        float totalPrimary = 0f;
        float maxSecondary = 0f;
        for (int i = 0; i < visible; i++)
        {
            DialogueSlotDefinition slot = slots[i];
            if (slot == null || !slot.Enabled) continue;
            float primary = ResolveSize(horizontal ? slot.Width : slot.Height,
                horizontal ? originalWidth : originalHeight,
                horizontal ? Mathf.Max(160f, originalWidth / Mathf.Max(visible, 1))
                           : Mathf.Max(120f, originalHeight / Mathf.Max(visible, 1)));
            float secondary = ResolveSize(horizontal ? slot.Height : slot.Width,
                horizontal ? originalHeight : originalWidth,
                horizontal ? Mathf.Max(100f, originalHeight)
                           : Mathf.Max(160f, originalWidth));
            totalPrimary += primary;
            if (i < visible - 1)
                totalPrimary += GetGapAfter(slot, defaultSpacing);
            maxSecondary = Mathf.Max(maxSecondary, secondary);
        }

        if (horizontal)
        {
            fittedWidth = totalPrimary;
            fittedHeight = maxSecondary;
        }
        else
        {
            fittedWidth = maxSecondary;
            fittedHeight = totalPrimary;
        }
    }

    static bool HasPartitionedSlotOverrides(List<DialogueSlotDefinition> slots,
        bool horizontal, int slotCount)
    {
        if (slots == null || slotCount <= 1) return false;

        int visible = Mathf.Min(slotCount, slots.Count);
        for (int i = 0; i < visible; i++)
        {
            DialogueSlotDefinition slot = slots[i];
            if (slot == null) continue;

            DialogueSizeValue primary = horizontal ? slot.Width : slot.Height;
            DialogueSizeValue secondary = horizontal ? slot.Height : slot.Width;

            if (primary != null && primary.Unit != DialogueSizeUnit.Auto && primary.Value > 0f)
                return true;
            if (secondary != null && secondary.Unit != DialogueSizeUnit.Auto && secondary.Value > 0f)
                return true;
            if (i < visible - 1 && slot.GapAfter >= 0f)
                return true;
        }

        return false;
    }

    static float GetGapAfter(DialogueSlotDefinition slot, float defaultSpacing)
    {
        if (slot == null) return defaultSpacing;
        return slot.GapAfter >= 0f ? slot.GapAfter : defaultSpacing;
    }

    static int GetPartitionSlotCount(int partitionLevel)
    {
        return 1 + Mathf.Clamp(partitionLevel, 0, 2);
    }

    static ResolvedDialogueAreaKind ToAreaKind(DialogueAttachedAreaSide side)
    {
        switch (side)
        {
            case DialogueAttachedAreaSide.Top: return ResolvedDialogueAreaKind.Top;
            case DialogueAttachedAreaSide.Bottom: return ResolvedDialogueAreaKind.Bottom;
            case DialogueAttachedAreaSide.Left: return ResolvedDialogueAreaKind.Left;
            default: return ResolvedDialogueAreaKind.Right;
        }
    }

    static float ResolveCustomX(DialogueCustomAnchorDefinition custom, Rect canvas, float width)
    {
        if (custom == null) return canvas.center.x - width * 0.5f;
        switch (custom.HorizontalReference)
        {
            case DialogueAnchorReferenceEdge.Left: return canvas.xMin + custom.OffsetX;
            case DialogueAnchorReferenceEdge.Right: return canvas.xMax - width + custom.OffsetX;
            default: return canvas.center.x - width * 0.5f + custom.OffsetX;
        }
    }

    static float ResolveCustomY(DialogueCustomAnchorDefinition custom, Rect canvas, float height)
    {
        if (custom == null) return canvas.center.y - height * 0.5f;
        switch (custom.VerticalReference)
        {
            case DialogueAnchorReferenceEdge.Top: return canvas.yMin + custom.OffsetY;
            case DialogueAnchorReferenceEdge.Bottom: return canvas.yMax - height + custom.OffsetY;
            default: return canvas.center.y - height * 0.5f + custom.OffsetY;
        }
    }

    public static Rect ShrinkRect(Rect rect, DialoguePadding padding)
    {
        if (padding == null) return rect;
        float x = rect.x + padding.Left;
        float y = rect.y + padding.Top;
        float width = Mathf.Max(0f, rect.width - padding.Left - padding.Right);
        float height = Mathf.Max(0f, rect.height - padding.Top - padding.Bottom);
        return new Rect(x, y, width, height);
    }

    public static float ResolveSize(DialogueSizeValue size, float reference, float autoFallback)
    {
        if (size == null) return autoFallback;
        switch (size.Unit)
        {
            case DialogueSizeUnit.Percent:
                return reference * Mathf.Clamp01(size.Value / 100f);
            case DialogueSizeUnit.Auto:
                return autoFallback;
            default:
                return size.Value;
        }
    }
}
