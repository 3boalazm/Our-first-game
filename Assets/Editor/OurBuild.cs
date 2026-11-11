using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor tooling for "Our First Game".
///   Menu: Our First Game / Create Scene       - (re)creates Assets/Scenes/OurFirstGame.unity
///   Menu: Our First Game / Build WebGL        - builds to ./WebBuild
/// Both can also be run from the command line with -executeMethod.
/// </summary>
public static class OurBuild
{
    const string ScenePath = "Assets/Scenes/OurFirstGame.unity";

    static readonly string[] ReadableTextures =
    {
        "Assets/bird.png", "Assets/bird-wings-down.png", "Assets/bird-ded.png"
    };

    [MenuItem("Our First Game/Create Scene")]
    public static void CreateScene()
    {
        // The female bird is recoloured at runtime, so the source sprites must be readable.
        foreach (var path in ReadableTextures)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && !ti.isReadable)
            {
                ti.isReadable = true;
                ti.SaveAndReimport();
            }
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.25f, 0.12f, 0.35f);
        camGo.transform.position = new Vector3(0, 0, -10);

        var go = new GameObject("OurGame");
        var game = go.AddComponent<OurGame>();
        game.background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/gbg.png");
        game.birdIdle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/bird.png");
        game.birdFlap = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/bird-wings-down.png");
        game.birdDead = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/bird-ded.png");
        game.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Machine Gunk.otf");

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        ApplySettings();
        AssetDatabase.SaveAssets();
        Debug.Log("OurBuild: scene created. background=" + (game.background != null) +
                  " font=" + (game.titleFont != null));
    }

    public static void ApplySettings()
    {
        PlayerSettings.productName = "Our First Game";
        PlayerSettings.companyName = "Doha & Mustafa";
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.runInBackground = true;
        // Gzip + JS fallback: the files work on any static host without special headers and every
        // file stays below the 25 MB limit of Cloudflare's static hosting.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "PROJECT:OurFirstGame";
    }

    [MenuItem("Our First Game/Build WebGL")]
    public static void BuildWebGL()
    {
        CreateScene();
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "WebBuild",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("BUILD RESULT: " + report.summary.result);
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // ------------------------------------------------------------ app icons
    const string IconDir = "Assets/WebGLTemplates/OurFirstGame/icons";

    /// <summary>Draws the app icons (Mustafa + Doha + heart on a purple/pink gradient) into the web template.</summary>
    [MenuItem("Our First Game/Generate App Icons")]
    public static void GenerateIcons()
    {
        foreach (var path in ReadableTextures)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && !ti.isReadable) { ti.isReadable = true; ti.SaveAndReimport(); }
        }
        var green = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/bird.png");
        var pink = ProcArt.MakeFemaleBird(green, true, 0);
        var heart = ProcArt.Heart(256, new Color(1f, 0.35f, 0.55f), new Color(0.55f, 0.08f, 0.3f));

        Directory.CreateDirectory(IconDir);
        File.WriteAllBytes(IconDir + "/icon-512.png", ComposeIcon(512, false, green, pink, heart).EncodeToPNG());
        File.WriteAllBytes(IconDir + "/icon-192.png", ComposeIcon(192, false, green, pink, heart).EncodeToPNG());
        File.WriteAllBytes(IconDir + "/maskable-512.png", ComposeIcon(512, true, green, pink, heart).EncodeToPNG());
        File.WriteAllBytes(IconDir + "/apple-touch-icon.png", ComposeIcon(180, true, green, pink, heart).EncodeToPNG());
        AssetDatabase.Refresh();
        Debug.Log("OurBuild: icons written to " + IconDir);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static Texture2D ComposeIcon(int size, bool fullBleed, Sprite green, Sprite pink, Sprite heart)
    {
        var px = new Color32[size * size];
        var c0 = new Color(0.36f, 0.16f, 0.53f);
        var c1 = new Color(1f, 0.56f, 0.69f);
        float radius = size * 0.22f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float t = (x + (size - 1 - y)) / (2f * size);
                Color c = Color.Lerp(c0, c1, t);
                float a = 1f;
                if (!fullBleed)
                {
                    // rounded corners
                    float qx = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                    float qy = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    a = Mathf.Clamp01(0.5f - d);
                }
                px[y * size + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(a * 255));
            }
        }

        // keep the artwork inside the "safe zone" so Android can crop the icon to any shape
        float box = fullBleed ? 0.62f : 0.80f;
        float ox = (1f - box) * 0.5f, oy = (1f - box) * 0.5f;
        System.Action<Sprite, float, float, float> draw = (s, cx, cy, sz) =>
        {
            int dstSize = Mathf.RoundToInt(sz * box * size);
            int dx = Mathf.RoundToInt((ox + cx * box) * size - dstSize * 0.5f);
            int dy = Mathf.RoundToInt((oy + cy * box) * size - dstSize * 0.5f);
            BlitSprite(px, size, s, dx, dy, dstSize);
        };
        draw(green, 0.27f, 0.42f, 0.52f);
        draw(pink, 0.73f, 0.42f, 0.52f);
        draw(heart, 0.50f, 0.80f, 0.30f);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Bilinear scaled alpha-blit of a whole sprite texture into dst (bottom-left origin).
    static void BlitSprite(Color32[] dst, int dsize, Sprite s, int dx, int dy, int size)
    {
        var tex = s.texture;
        var src = tex.GetPixels32();
        int sw = tex.width, sh = tex.height;
        for (int y = 0; y < size; y++)
        {
            int ty = dy + y;
            if (ty < 0 || ty >= dsize) continue;
            for (int x = 0; x < size; x++)
            {
                int tx = dx + x;
                if (tx < 0 || tx >= dsize) continue;
                float u = (x + 0.5f) / size * sw - 0.5f;
                float v = (y + 0.5f) / size * sh - 0.5f;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, sw - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, sh - 1);
                int x1 = Mathf.Min(x0 + 1, sw - 1), y1 = Mathf.Min(y0 + 1, sh - 1);
                float fx = u - Mathf.Floor(u), fy = v - Mathf.Floor(v);
                Color32 a = src[y0 * sw + x0], b = src[y0 * sw + x1], c = src[y1 * sw + x0], d = src[y1 * sw + x1];
                float r = Bil(a.r, b.r, c.r, d.r, fx, fy), g = Bil(a.g, b.g, c.g, d.g, fx, fy);
                float bl = Bil(a.b, b.b, c.b, d.b, fx, fy), al = Bil(a.a, b.a, c.a, d.a, fx, fy) / 255f;
                if (al <= 0f) continue;
                Color32 o = dst[ty * dsize + tx];
                float ia = 1f - al;
                dst[ty * dsize + tx] = new Color32(
                    (byte)(r * al + o.r * ia), (byte)(g * al + o.g * ia), (byte)(bl * al + o.b * ia),
                    (byte)Mathf.Max(o.a, al * 255f));
            }
        }
    }

    static float Bil(float a, float b, float c, float d, float fx, float fy)
    {
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// <summary>Writes the generated Doha sprites to PNG so the art can be inspected.</summary>
    public static void DumpArt()
    {
        CreateScene();
        string dir = "ArtDump";
        Directory.CreateDirectory(dir);
        var pairs = new[]
        {
            new { name = "female_idle", path = "Assets/bird.png", lashes = true },
            new { name = "female_flap", path = "Assets/bird-wings-down.png", lashes = true },
            new { name = "female_dead", path = "Assets/bird-ded.png", lashes = false },
        };
        foreach (var p in pairs)
        {
            var src = AssetDatabase.LoadAssetAtPath<Sprite>(p.path);
            var spr = ProcArt.MakeFemaleBird(src, p.lashes);
            File.WriteAllBytes(Path.Combine(dir, p.name + ".png"), spr.texture.EncodeToPNG());
        }
        for (int i = 0; i < ProcArt.Styles.Length; i++)
        {
            var src = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/bird.png");
            var spr = ProcArt.MakeFemaleBird(src, true, i);
            File.WriteAllBytes(Path.Combine(dir, "style" + i + ".png"), spr.texture.EncodeToPNG());
        }
        var heart = ProcArt.Heart(128, new Color(1f, 0.35f, 0.55f), new Color(0.55f, 0.08f, 0.3f));
        File.WriteAllBytes(Path.Combine(dir, "heart.png"), heart.texture.EncodeToPNG());
        Debug.Log("OurBuild: art dumped");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
