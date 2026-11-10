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
        PlayerSettings.companyName = "Mustafa & Doha";
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.runInBackground = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
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
