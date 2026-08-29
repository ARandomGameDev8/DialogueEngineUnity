#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DialogueVisualStylePreviewUtility
{
    struct CornerRadii
    {
        public float TopLeft;
        public float TopRight;
        public float BottomRight;
        public float BottomLeft;
    }

    public static void DrawStyledElement(
        Rect rect,
        DialogueBackgroundStyle background,
        DialogueBorderStyle border,
        DialogueShadowStyle shadow,
        DialogueOpacitySettings opacity,
        Color fallbackFill,
        Color fallbackOutline,
        float fallbackOutlineThickness)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float overallOpacity = opacity != null ? Mathf.Clamp01(opacity.Opacity) : 1f;
        CornerRadii outerRadii = GetCornerRadii(rect, border);

        DrawShadow(rect, outerRadii, shadow, overallOpacity);

        Color fillColor = ResolveFillColor(background, fallbackFill, overallOpacity);
        bool hasBorder = HasVisibleBorder(border, overallOpacity);

        if (hasBorder)
        {
            Color borderColor = border.BorderColor;
            borderColor.a *= Mathf.Clamp01(border.Opacity) * overallOpacity;
            DrawRoundedFill(rect, outerRadii, borderColor);

            Rect innerRect = InsetRect(
                rect,
                Mathf.Max(0f, border.LeftThickness),
                Mathf.Max(0f, border.TopThickness),
                Mathf.Max(0f, border.RightThickness),
                Mathf.Max(0f, border.BottomThickness));

            if (innerRect.width > 0f && innerRect.height > 0f && fillColor.a > 0f)
                DrawRoundedFill(innerRect, GetInnerCornerRadii(border, outerRadii, innerRect), fillColor);
        }
        else
        {
            if (fillColor.a > 0f)
                DrawRoundedFill(rect, outerRadii, fillColor);

            Color outline = fallbackOutline;
            outline.a *= overallOpacity;
            if (fallbackOutlineThickness > 0f && outline.a > 0f)
                DrawRoundedOutline(rect, outerRadii, fallbackOutlineThickness, outline);
        }
    }

    public static void DrawSelectionOutline(
        Rect rect,
        DialogueBorderStyle border,
        Color color,
        float thickness)
    {
        if (rect.width <= 0f || rect.height <= 0f || thickness <= 0f || color.a <= 0f)
            return;

        DrawRoundedOutline(rect, GetCornerRadii(rect, border), thickness, color);
    }

    static bool HasVisibleBorder(DialogueBorderStyle border, float overallOpacity)
    {
        return border != null && border.Enabled && overallOpacity > 0f &&
               Mathf.Clamp01(border.Opacity) > 0f &&
               GetMaxBorderThickness(border) > 0f;
    }

    static void DrawShadow(Rect rect, CornerRadii radii, DialogueShadowStyle shadow, float overallOpacity)
    {
        if (shadow == null || !shadow.Enabled || overallOpacity <= 0f)
            return;

        Color shadowColor = shadow.Color;
        shadowColor.a *= Mathf.Clamp01(shadow.Opacity) * overallOpacity;
        if (shadowColor.a <= 0f)
            return;

        float expand = Mathf.Max(0f, shadow.Blur * 0.5f);
        Rect shadowRect = new Rect(
            rect.x + shadow.Offset.x - expand,
            rect.y + shadow.Offset.y - expand,
            rect.width + expand * 2f,
            rect.height + expand * 2f);

        CornerRadii shadowRadii = new CornerRadii
        {
            TopLeft = radii.TopLeft + expand,
            TopRight = radii.TopRight + expand,
            BottomRight = radii.BottomRight + expand,
            BottomLeft = radii.BottomLeft + expand
        };

        DrawRoundedFill(shadowRect, shadowRadii, shadowColor);
    }

    static Color ResolveFillColor(DialogueBackgroundStyle background, Color fallbackFill, float overallOpacity)
    {
        Color result = fallbackFill;
        float backgroundOpacity = 1f;

        if (background != null)
        {
            backgroundOpacity = Mathf.Clamp01(background.Opacity);
            switch (background.Mode)
            {
                case DialogueBackgroundMode.None:
                    break;
                case DialogueBackgroundMode.SolidColor:
                    result = background.ColorA;
                    break;
                case DialogueBackgroundMode.Gradient:
                    result = Color.Lerp(background.ColorA, background.ColorB, 0.5f);
                    break;
                case DialogueBackgroundMode.Sprite:
                    result = background.ColorA;
                    break;
            }
        }

        result.a *= backgroundOpacity * overallOpacity;
        return result;
    }

    static float GetMaxBorderThickness(DialogueBorderStyle border)
    {
        if (border == null)
            return 0f;

        return Mathf.Max(
            Mathf.Max(Mathf.Max(0f, border.LeftThickness), Mathf.Max(0f, border.RightThickness)),
            Mathf.Max(Mathf.Max(0f, border.TopThickness), Mathf.Max(0f, border.BottomThickness)));
    }

    static Rect InsetRect(Rect rect, float left, float top, float right, float bottom)
    {
        float x = rect.x + left;
        float y = rect.y + top;
        float width = Mathf.Max(0f, rect.width - left - right);
        float height = Mathf.Max(0f, rect.height - top - bottom);
        return new Rect(x, y, width, height);
    }

    static CornerRadii GetCornerRadii(Rect rect, DialogueBorderStyle border)
    {
        float maxRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        return new CornerRadii
        {
            TopLeft = Mathf.Clamp(border != null ? border.CornerRadiusTopLeft : 0f, 0f, maxRadius),
            TopRight = Mathf.Clamp(border != null ? border.CornerRadiusTopRight : 0f, 0f, maxRadius),
            BottomRight = Mathf.Clamp(border != null ? border.CornerRadiusBottomRight : 0f, 0f, maxRadius),
            BottomLeft = Mathf.Clamp(border != null ? border.CornerRadiusBottomLeft : 0f, 0f, maxRadius)
        };
    }

    static CornerRadii GetInnerCornerRadii(DialogueBorderStyle border, CornerRadii outerRadii, Rect innerRect)
    {
        if (border == null)
            return GetCornerRadii(innerRect, null);

        float left = Mathf.Max(0f, border.LeftThickness);
        float right = Mathf.Max(0f, border.RightThickness);
        float top = Mathf.Max(0f, border.TopThickness);
        float bottom = Mathf.Max(0f, border.BottomThickness);
        float maxRadius = Mathf.Min(innerRect.width, innerRect.height) * 0.5f;

        return new CornerRadii
        {
            TopLeft = Mathf.Clamp(outerRadii.TopLeft - Mathf.Max(left, top), 0f, maxRadius),
            TopRight = Mathf.Clamp(outerRadii.TopRight - Mathf.Max(right, top), 0f, maxRadius),
            BottomRight = Mathf.Clamp(outerRadii.BottomRight - Mathf.Max(right, bottom), 0f, maxRadius),
            BottomLeft = Mathf.Clamp(outerRadii.BottomLeft - Mathf.Max(left, bottom), 0f, maxRadius)
        };
    }

    static void DrawRoundedFill(Rect rect, CornerRadii radii, Color color)
    {
        if (color.a <= 0f || rect.width <= 0f || rect.height <= 0f)
            return;

        Vector3[] polygon = BuildRoundedRectPolygon(rect, radii, 6);
        if (polygon.Length < 3)
            return;

        Handles.color = color;
        Handles.DrawAAConvexPolygon(polygon);
    }

    static void DrawRoundedOutline(Rect rect, CornerRadii radii, float thickness, Color color)
    {
        if (color.a <= 0f || rect.width <= 0f || rect.height <= 0f)
            return;

        Vector3[] polygon = BuildRoundedRectPolygon(rect, radii, 8);
        if (polygon.Length < 2)
            return;

        var closed = new Vector3[polygon.Length + 1];
        for (int i = 0; i < polygon.Length; i++)
            closed[i] = polygon[i];
        closed[polygon.Length] = polygon[0];

        Handles.color = color;
        Handles.DrawAAPolyLine(Mathf.Max(1f, thickness), closed);
    }

    static Vector3[] BuildRoundedRectPolygon(Rect rect, CornerRadii radii, int arcSteps)
    {
        var points = new List<Vector3>(32);

        AppendPoint(points, new Vector2(rect.xMin + radii.TopLeft, rect.yMin));
        AppendPoint(points, new Vector2(rect.xMax - radii.TopRight, rect.yMin));
        AppendArc(points, new Vector2(rect.xMax - radii.TopRight, rect.yMin + radii.TopRight), radii.TopRight, 270f, 360f, arcSteps);

        AppendPoint(points, new Vector2(rect.xMax, rect.yMax - radii.BottomRight));
        AppendArc(points, new Vector2(rect.xMax - radii.BottomRight, rect.yMax - radii.BottomRight), radii.BottomRight, 0f, 90f, arcSteps);

        AppendPoint(points, new Vector2(rect.xMin + radii.BottomLeft, rect.yMax));
        AppendArc(points, new Vector2(rect.xMin + radii.BottomLeft, rect.yMax - radii.BottomLeft), radii.BottomLeft, 90f, 180f, arcSteps);

        AppendPoint(points, new Vector2(rect.xMin, rect.yMin + radii.TopLeft));
        AppendArc(points, new Vector2(rect.xMin + radii.TopLeft, rect.yMin + radii.TopLeft), radii.TopLeft, 180f, 270f, arcSteps);

        if (points.Count > 1 && Vector2.SqrMagnitude((Vector2)points[0] - (Vector2)points[points.Count - 1]) <= 0.0001f)
            points.RemoveAt(points.Count - 1);

        return points.ToArray();
    }

    static void AppendArc(List<Vector3> points, Vector2 center, float radius, float startDegrees, float endDegrees, int steps)
    {
        if (radius <= 0f)
            return;

        int clampedSteps = Mathf.Max(1, steps);
        for (int i = 1; i <= clampedSteps; i++)
        {
            float t = i / (float)clampedSteps;
            float angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
            AppendPoint(points, new Vector2(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius));
        }
    }

    static void AppendPoint(List<Vector3> points, Vector2 point)
    {
        if (points.Count > 0)
        {
            Vector3 last = points[points.Count - 1];
            if (Vector2.SqrMagnitude((Vector2)last - point) <= 0.0001f)
                return;
        }

        points.Add(new Vector3(point.x, point.y, 0f));
    }
}
#endif
