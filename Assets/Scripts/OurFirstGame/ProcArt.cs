using UnityEngine;

/// <summary>
/// Small procedural art helpers: rounded rectangles, hearts and the "Doha" bird
/// (the green bird recoloured pink, with a bow and eyelashes).
/// Everything is generated at runtime, so the game needs no extra image files.
/// </summary>
public static class ProcArt
{
    static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    /// <summary>9-sliceable rounded rectangle with an outline. hShade lightens the left, darkens the right.</summary>
    public static Sprite RoundedRect(int size, int radius, Color fill, Color outline, int outlineW, float hShade, float ppu)
    {
        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        float inner = half - radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f - half;
                float fy = y + 0.5f - half;
                float qx = Mathf.Abs(fx) - inner;
                float qy = Mathf.Abs(fy) - inner;
                float ox = Mathf.Max(qx, 0f);
                float oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float cover = Mathf.Clamp01(0.5f - d);
                float inside = Mathf.Clamp01(0.5f - (d + outlineW));

                float t = (fx / half) * 0.5f + 0.5f; // 0 left .. 1 right
                float shade = hShade * (0.5f - Mathf.Abs(t - 0.3f)) ;
                Color f = new Color(
                    Mathf.Clamp01(fill.r + shade), Mathf.Clamp01(fill.g + shade), Mathf.Clamp01(fill.b + shade), 1f);
                Color c = Color.Lerp(outline, f, inside);
                px[y * size + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(cover * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    public static Sprite Circle(int size, Color fill, Color outline, int outlineW, float ppu)
    {
        return RoundedRect(size, size / 2, fill, outline, outlineW, 0f, ppu);
    }

    public static Sprite Heart(int size, Color fill, Color outline)
    {
        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        const int ss = 3;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cover = 0f, innerCover = 0f, light = 0f;
                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = ((x + (sx + 0.5f) / ss) / size * 2f - 1f) * 1.3f;
                        float v = ((y + (sy + 0.5f) / ss) / size * 2f - 1f) * 1.3f + 0.15f;
                        float a = u * u + v * v - 1f;
                        float f = a * a * a - u * u * v * v * v;
                        if (f <= 0f) cover += 1f;
                        if (f <= -0.035f) innerCover += 1f;
                        // glossy highlight on the upper-left lobe
                        float hu = (u + 0.52f) / 0.22f;
                        float hv = (v - 0.55f) / 0.12f;
                        if (hu * hu + hv * hv < 1f && f <= -0.035f) light += 1f;
                    }
                }
                float n = ss * ss;
                cover /= n; innerCover /= n; light /= n;
                float vShade = Mathf.Lerp(0.85f, 1.1f, (float)y / size);
                Color f2 = new Color(Mathf.Clamp01(fill.r * vShade), Mathf.Clamp01(fill.g * vShade), Mathf.Clamp01(fill.b * vShade), 1f);
                Color c = Color.Lerp(outline, f2, innerCover);
                c = Color.Lerp(c, Color.white, light * 0.75f);
                px[y * size + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(cover * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ------------------------------------------------------------------ Doha bird

    /// <summary>
    /// Recolours a bird sprite: green body -> pink, red cap -> violet. Optionally adds
    /// a bow on the cap and eyelashes. Needs the source texture to be readable.
    /// </summary>
    public struct BirdStyle
    {
        public string name;
        public float bodyHue, capHue;     // hue (0..1) of the body and of the cap
        public Color32 bow, bowLight;
        public Color swatch;              // colour shown in the style picker
    }

    /// <summary>The looks Doha can choose from.</summary>
    public static readonly BirdStyle[] Styles =
    {
        new BirdStyle { name = "Rose",     bodyHue = 0.93f, capHue = 0.76f, bow = new Color32(255, 70, 150, 255),  bowLight = new Color32(255, 150, 195, 255), swatch = new Color(1.00f, 0.36f, 0.64f) },
        new BirdStyle { name = "Lavender", bodyHue = 0.76f, capHue = 0.93f, bow = new Color32(255, 105, 190, 255), bowLight = new Color32(255, 170, 215, 255), swatch = new Color(0.62f, 0.42f, 0.95f) },
        new BirdStyle { name = "Sky",      bodyHue = 0.55f, capHue = 0.93f, bow = new Color32(255, 70, 150, 255),  bowLight = new Color32(255, 150, 195, 255), swatch = new Color(0.30f, 0.74f, 0.98f) },
        new BirdStyle { name = "Mint",     bodyHue = 0.43f, capHue = 0.90f, bow = new Color32(255, 110, 175, 255), bowLight = new Color32(255, 175, 210, 255), swatch = new Color(0.30f, 0.88f, 0.70f) },
        new BirdStyle { name = "Sunny",    bodyHue = 0.12f, capHue = 0.55f, bow = new Color32(255, 70, 150, 255),  bowLight = new Color32(255, 150, 195, 255), swatch = new Color(1.00f, 0.74f, 0.22f) },
        new BirdStyle { name = "Cherry",   bodyHue = 0.99f, capHue = 0.60f, bow = new Color32(255, 215, 90, 255),  bowLight = new Color32(255, 240, 160, 255), swatch = new Color(0.95f, 0.22f, 0.30f) },
    };

    public static Sprite MakeFemaleBird(Sprite src, bool lashes)
    {
        return MakeFemaleBird(src, lashes, 0);
    }

    public static Sprite MakeFemaleBird(Sprite src, bool lashes, int styleIndex)
    {
        BirdStyle st = Styles[Mathf.Clamp(styleIndex, 0, Styles.Length - 1)];
        Texture2D t = src.texture;
        int w = t.width, h = t.height;
        Color32[] px = t.GetPixels32();
        var o = new Color32[px.Length];

        int capMinX = w, capMinY = h, capMaxX = 0, capMaxY = 0;

        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            if (c.a == 0) { o[i] = c; continue; }
            float hh, s, v;
            Color.RGBToHSV(new Color(c.r / 255f, c.g / 255f, c.b / 255f), out hh, out s, out v);
            bool isRed = (hh < 0.025f || hh > 0.94f) && s > 0.5f && v > 0.2f; // the orange beak stays orange
            bool isGreen = hh > 0.15f && hh < 0.45f && s > 0.3f;
            if (isGreen)
            {
                hh = st.bodyHue; s = Mathf.Clamp01(s * 0.85f); v = Mathf.Clamp01(v * 1.08f);
            }
            else if (isRed)
            {
                hh = st.capHue; s *= 0.75f; v = Mathf.Clamp01(v * 1.1f);
                int x = i % w, y = i / w;
                if (x < capMinX) capMinX = x;
                if (x > capMaxX) capMaxX = x;
                if (y < capMinY) capMinY = y;
                if (y > capMaxY) capMaxY = y;
            }
            Color n = Color.HSVToRGB(hh, s, v);
            o[i] = new Color32((byte)(n.r * 255), (byte)(n.g * 255), (byte)(n.b * 255), c.a);
        }

        if (capMaxX > capMinX)
        {
            int capW = capMaxX - capMinX;
            int capH = capMaxY - capMinY;

            // A proper bow, seated on the top-left of the cap: two lobes, a knot, two little tails
            // and a shine on each lobe.
            float bx = capMinX + 0.27f * capW;
            float by = capMaxY - 0.20f * capH;
            Color32 bowC = st.bow;
            Color32 bowL = st.bowLight;
            Color32 dark = new Color32(70, 10, 55, 255);
            float ow = Mathf.Max(3f, capW * 0.02f);
            float rx = capW * 0.17f, ry = capW * 0.105f;
            float off = rx * 0.82f;
            float tilt = 0.42f;

            // two wide wings meeting at a knot, like a real bow
            Ellipse(o, w, h, bx - off, by + ry * 0.35f, rx, ry, tilt, bowC, dark, ow);
            Ellipse(o, w, h, bx + off, by + ry * 0.35f, rx, ry, -tilt, bowC, dark, ow);
            // shine on each wing
            Ellipse(o, w, h, bx - off - rx * 0.05f, by + ry * 0.75f, rx * 0.5f, ry * 0.28f, tilt, bowL, bowL, 0f);
            Ellipse(o, w, h, bx + off + rx * 0.05f, by + ry * 0.75f, rx * 0.5f, ry * 0.28f, -tilt, bowL, bowL, 0f);
            // knot on top
            Ellipse(o, w, h, bx, by, capW * 0.052f, capW * 0.058f, 0f, bowL, dark, ow);

            if (lashes)
            {
                // Bounding box of the white of the eyes (below the cap).
                int eMinX = w, eMaxX = 0, eMinY = h, eMaxY = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    Color32 c = px[i];
                    if (c.a < 200 || c.r < 225 || c.g < 225 || c.b < 225) continue;
                    int y = i / w, x = i % w;
                    if (y > capMinY - 6) continue;
                    if (x < eMinX) eMinX = x;
                    if (x > eMaxX) eMaxX = x;
                    if (y < eMinY) eMinY = y;
                    if (y > eMaxY) eMaxY = y;
                }
                if (eMaxX > eMinX)
                {
                    // The big eye is on the right; its top-right arc faces empty background,
                    // so the lashes fan out from there where they stay visible.
                    float eW = eMaxX - eMinX;
                    Color32 ink = new Color32(10, 4, 14, 255);
                    float len = eW * 0.36f;
                    float th = Mathf.Max(9f, eW * 0.075f);
                    Lash(o, w, h, eMaxX - 0.12f * eW, eMaxY - 0.01f * eW, 62f, len, th, ink);
                    Lash(o, w, h, eMaxX - 0.03f * eW, eMaxY - 0.09f * eW, 34f, len * 1.08f, th, ink);
                    Lash(o, w, h, eMaxX + 0.00f * eW, eMaxY - 0.19f * eW, 8f, len, th, ink);
                }
            }
        }

        var tex = NewTex(w, h);
        tex.SetPixels32(o);
        tex.Apply();
        Vector2 pivot = new Vector2(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height);
        return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, src.pixelsPerUnit);
    }

    static void Put(Color32[] px, int w, int h, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        px[y * w + x] = c;
    }

    static void Ellipse(Color32[] px, int w, int h, float cx, float cy, float rx, float ry, float angle,
        Color32 fill, Color32 outline, float outlineW)
    {
        float cosA = Mathf.Cos(angle), sinA = Mathf.Sin(angle);
        float r = Mathf.Max(rx, ry);
        int x0 = Mathf.FloorToInt(cx - r - 2), x1 = Mathf.CeilToInt(cx + r + 2);
        int y0 = Mathf.FloorToInt(cy - r - 2), y1 = Mathf.CeilToInt(cy + r + 2);
        float edge = outlineW / Mathf.Min(rx, ry);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx, dy = y - cy;
                float u = (dx * cosA + dy * sinA) / rx;
                float v = (-dx * sinA + dy * cosA) / ry;
                float d = Mathf.Sqrt(u * u + v * v);
                if (d > 1f) continue;
                Put(px, w, h, x, y, d > 1f - edge ? outline : fill);
            }
        }
    }

    static void Lash(Color32[] px, int w, int h, float x, float y, float angleDeg, float length, float thick, Color32 ink)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        float dx = Mathf.Cos(a), dy = Mathf.Sin(a);
        int steps = Mathf.CeilToInt(length * 2f);
        float rad = thick * 0.5f;
        for (int i = 0; i <= steps; i++)
        {
            float k = (float)i / steps;
            float cx = x + dx * length * k;
            float cy = y + dy * length * k;
            float rr = rad * (1f - 0.45f * k); // tapered
            int x0 = Mathf.FloorToInt(cx - rr), x1 = Mathf.CeilToInt(cx + rr);
            int y0 = Mathf.FloorToInt(cy - rr), y1 = Mathf.CeilToInt(cy + rr);
            for (int yy = y0; yy <= y1; yy++)
                for (int xx = x0; xx <= x1; xx++)
                {
                    float ddx = xx - cx, ddy = yy - cy;
                    if (ddx * ddx + ddy * ddy <= rr * rr) Put(px, w, h, xx, yy, ink);
                }
        }
    }
}
