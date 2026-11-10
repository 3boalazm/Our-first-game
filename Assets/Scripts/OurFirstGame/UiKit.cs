using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Tiny helpers to build uGUI elements from code.</summary>
public static class UiKit
{
    public static Font TitleFont;
    public static Font BodyFont;
    public static Sprite RoundSprite;   // 9-sliced rounded rect used for buttons / panels
    public static Sprite HeartSprite;

    public static readonly Color Ink = new Color(0.16f, 0.05f, 0.27f, 0.95f);

    // Wide text boxes are shrunk on narrow (portrait) screens so nothing is cut off.
    public static readonly List<KeyValuePair<RectTransform, float>> Clamped = new List<KeyValuePair<RectTransform, float>>();

    public static void ClampWidths(float canvasWidth)
    {
        float maxW = Mathf.Max(200f, canvasWidth - 40f);
        for (int i = 0; i < Clamped.Count; i++)
        {
            var rt = Clamped[i].Key;
            if (rt == null) continue;
            var sd = rt.sizeDelta;
            float w = Mathf.Min(Clamped[i].Value, maxW);
            if (!Mathf.Approximately(sd.x, w)) { sd.x = w; rt.sizeDelta = sd; }
        }
    }

    public static RectTransform Panel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform Place(GameObject go, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Text Label(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box,
        TextAnchor align = TextAnchor.MiddleCenter, bool title = true, bool outline = true)
    {
        return LabelAt(parent, text, size, color, new Vector2(0.5f, 0.5f), pos, box, align, title, outline);
    }

    public static Text LabelAt(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box,
        TextAnchor align = TextAnchor.MiddleCenter, bool title = true, bool outline = true)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var rect = Place(go, parent, anchor, pos, box);
        if (box.x > 420f) Clamped.Add(new KeyValuePair<RectTransform, float>(rect, box.x));
        var t = go.GetComponent<Text>();
        t.font = (title && TitleFont != null) ? TitleFont : (BodyFont != null ? BodyFont : TitleFont);
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMaxSize = size;
        t.resizeTextMinSize = Mathf.Max(10, size / 3);
        t.raycastTarget = false;
        if (outline)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(Mathf.Max(2, size / 28f), -Mathf.Max(2, size / 28f));
        }
        return t;
    }

    public static Image Pic(Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color? tint = null)
    {
        var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
        Place(go, parent, new Vector2(0.5f, 0.5f), pos, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = tint ?? Color.white;
        img.raycastTarget = false;
        img.preserveAspect = true;
        return img;
    }

    public static Button Btn(Transform parent, string label, Color color, Vector2 pos, Vector2 size,
        UnityAction onClick, int fontSize = 44, Vector2? anchor = null)
    {
        var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        Place(go, parent, anchor ?? new Vector2(0.5f, 0.5f), pos, size);
        var img = go.GetComponent<Image>();
        img.sprite = RoundSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;
        if (onClick != null) btn.onClick.AddListener(onClick);

        var t = Label(go.transform, label, fontSize, Color.white, Vector2.zero, size - new Vector2(30, 14));
        t.raycastTarget = false;
        return btn;
    }

    public static Image Box(Transform parent, Color color, Vector2 pos, Vector2 size, Vector2? anchor = null)
    {
        var go = new GameObject("Box", typeof(RectTransform), typeof(Image));
        Place(go, parent, anchor ?? new Vector2(0.5f, 0.5f), pos, size);
        var img = go.GetComponent<Image>();
        img.sprite = RoundSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
