using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Our First Game" - a two bird flappy race for Mustafa &amp; Doha.
///
/// Everything (world, UI, networking glue) is built from code at start-up, so the scene
/// only needs one object with this component and a camera.
///
/// Online play is peer-to-peer (WebRTC). Both players run the same deterministic pipe
/// course (same seed, same clock) and only exchange their own bird state, so no game
/// server is needed. The player who crashes first keeps watching the other one.
/// </summary>
public class OurGame : MonoBehaviour
{
    [Header("Art (assigned by the scene builder)")]
    public Sprite background;
    public Sprite birdIdle;
    public Sprite birdFlap;
    public Sprite birdDead;
    public Font titleFont;

    // ------------------------------------------------------------ tuning
    const float CamSize = 5f;
    const float Gravity = 26f;
    const float FlapV = 8.6f;
    const float MaxFall = -14f;
    const float BirdR = 0.50f;
    const float PipeW = 1.5f;
    const float FirstPipeX = 13f;
    const float FloorY = -4.55f;
    const float CeilY = 4.75f;
    const float CountdownSecs = 3.2f;
    const float SendInterval = 1f / 15f;

    // ------------------------------------------------------------ difficulty levels
    // Both players always play the level the host chose (it is sent with the round start).
    struct Level { public string name; public float gap, spacing, baseSpeed, maxSpeed, gain; }
    static readonly Level[] Levels =
    {
        // gap = the opening the bird flies through (it used to be 3.9); spacing = distance between two pipes
        new Level { name = "Easy",   gap = 5.4f, spacing = 7.6f, baseSpeed = 3.8f, maxSpeed = 5.2f, gain = 0.030f },
        new Level { name = "Medium", gap = 4.9f, spacing = 6.8f, baseSpeed = 4.4f, maxSpeed = 6.0f, gain = 0.035f },
        new Level { name = "Hard",   gap = 4.3f, spacing = 6.2f, baseSpeed = 5.0f, maxSpeed = 6.8f, gain = 0.040f },
    };
    static Level Lv = Levels[1];
    static float GapH { get { return Lv.gap; } }
    static float PipeSpacing { get { return Lv.spacing; } }
    static float BaseSpeed { get { return Lv.baseSpeed; } }
    static float MaxSpeed { get { return Lv.maxSpeed; } }
    static float SpeedGain { get { return Lv.gain; } }
    int level = 1;   // chosen in the menu (0 easy, 1 medium, 2 hard)
    int playingLevel = 1;

    // ------------------------------------------------------------ stars and shield
    const int StarPool = 16;
    const int StarPoints = 2;        // every star is worth 2 points
    const int StarsForShield = 5;    // every 5 stars give a shield that saves one crash

    static float Speed(float t)
    {
        return Mathf.Min(MaxSpeed, BaseSpeed + SpeedGain * Mathf.Max(0f, t));
    }

    // Distance the world has scrolled after t seconds (integral of Speed).
    static float Dist(float t)
    {
        if (t <= 0f) return 0f;
        float tCap = (MaxSpeed - BaseSpeed) / SpeedGain;
        if (t <= tCap) return BaseSpeed * t + 0.5f * SpeedGain * t * t;
        return BaseSpeed * tCap + 0.5f * SpeedGain * tCap * tCap + MaxSpeed * (t - tCap);
    }

    static int ScoreAt(float dist)
    {
        return dist >= FirstPipeX ? Mathf.FloorToInt((dist - FirstPipeX) / PipeSpacing) + 1 : 0;
    }

    int seed;
    float GapCenter(int i)
    {
        uint h = (uint)(seed * 73856093) ^ (uint)(i * 19349663 + 17);
        h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
        float r = (h & 0xFFFF) / 65535f;
        float range = Mathf.Min(2.4f, 4.55f - Lv.gap * 0.5f);
        return Mathf.Lerp(-range, range, r);
    }

    float H01(int a, int b)
    {
        uint h = (uint)(seed * 73856093) ^ (uint)(a * 19349663 + b * 83492791 + 12345);
        h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
        return (h & 0xFFFF) / 65535f;
    }

    // Star ids: 2*i is the star inside the gap of pipe i, 2*i+1 sits between pipe i and i+1.
    bool StarAt(int id, out float sx, out float sy)
    {
        sx = 0f; sy = 0f;
        if (id < 0) return false;
        int i = id >> 1;
        if ((id & 1) == 0)
        {
            if (H01(i, 11) > 0.55f) return false;
            sx = FirstPipeX + i * PipeSpacing;
            sy = GapCenter(i) + (H01(i, 12) - 0.5f) * GapH * 0.45f;
        }
        else
        {
            if (H01(i, 21) > 0.45f) return false;
            sx = FirstPipeX + i * PipeSpacing + PipeSpacing * 0.5f;
            sy = Mathf.Lerp(-2.8f, 2.8f, H01(i, 22));
        }
        return true;
    }

    // ------------------------------------------------------------ state
    enum State { Who, Menu, Hosting, Joining, Round, Result }
    State state = State.Menu;

    bool online;
    bool iAmHost = true;
    string roomCode = "";
    string joinCode = "";
    int hostTries;
    bool helloSent;
    bool joinWaiting;
    float startAt;
    float frozenT;
    float nextSend;
    float simT;
    bool pendingFlap;
    const float Step = 1f / 120f;
    float resultTimer;
    bool otherLeft;
    float toastUntil;
    float otherLastMsg;

    class Bird
    {
        public string name;
        public GameObject go;
        public SpriteRenderer sr;
        public Sprite idle, flap, dead;
        public float y, vy, drift, flapUntil, deathT, targetY;
        public int score, bonus, stars;
        public bool alive = true;
        public bool shield;
        public float invulnUntil;
        public SpriteRenderer bubble;
    }

    Bird mustafa, doha;

    // Who am I? Chosen on the first screen. Doha and Mustafa are always different players:
    // the host's choice wins and the guest automatically gets the other name.
    const int IdDoha = 0, IdMustafa = 1;
    int myId = IdMustafa;
    int myLook;                      // my Doha colour (used when I am Doha)
    string pendingRoom;              // room code from an invite link, joined after choosing a name
    string MyName { get { return myId == IdDoha ? "Doha" : "Mustafa"; } }
    string PartnerName { get { return myId == IdDoha ? "Mustafa" : "Doha"; } }
    Bird Me { get { return myId == IdDoha ? doha : mustafa; } }
    Bird Other { get { return myId == IdDoha ? mustafa : doha; } }

    class PipeView
    {
        public Transform root;
        public SpriteRenderer topBody, topCap, botBody, botCap;
        public int index = -1;
    }
    readonly List<PipeView> pipes = new List<PipeView>();
    const int PoolSize = 8;

    class StarView { public SpriteRenderer sr; public int id = -1; }
    readonly List<StarView> starViews = new List<StarView>();
    readonly HashSet<int> collected = new HashSet<int>();
    Sprite starSprite, bubbleSprite;
    Text hudInfo;
    Text[] popTexts = new Text[6];
    float[] popBorn = new float[6];
    float[] popDx = new float[6];
    Image[] levelBtnImgs = new Image[3];

    // ------------------------------------------------------------ scene objects
    Camera cam;
    Transform worldRoot;
    SpriteRenderer bgSr;
    Sprite pipeBodySprite, pipeCapSprite;

    Canvas canvas;
    RectTransform decoP, menuP, hostP, joinP, hudP, countP, resultP;
    Text hostCode, hostStatus, hostLink, joinCodeText, joinStatus, countText, hudScore, hudLeft, hudRight,
         bannerText, toastText, resultTitle, resultLine, resultHint;
    GameObject banner, playAgainBtn;
    Image menuBirdA, menuBirdB;
    Text nameMustafa, nameDoha;
    GameObject hostRetryBtn;

    class FloatHeart { public RectTransform rt; public float speed, phase, x; }
    readonly List<FloatHeart> hearts = new List<FloatHeart>();

    // ============================================================ start-up
    void Start()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.runInBackground = true;

        cam = Camera.main;
        if (cam == null)
        {
            var cg = new GameObject("Main Camera", typeof(Camera));
            cg.tag = "MainCamera";
            cam = cg.GetComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = CamSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.25f, 0.12f, 0.35f);
        cam.transform.position = new Vector3(0, 0, -10);

        BuildArt();
        BuildWorld();
        BuildUi();

        // Opened through a friend's invite link (?room=1234): remember the code, join it right after
        // you have said who you are.
        string r = PeerNet.UrlParam("room");
        if (!string.IsNullOrEmpty(r) && r.Length == 4 && IsDigits(r)) pendingRoom = r;
        RefreshIdentityUi();
        SetState(State.Who);
    }

    static bool IsDigits(string s)
    {
        for (int i = 0; i < s.Length; i++) if (s[i] < '0' || s[i] > '9') return false;
        return true;
    }

    void BuildArt()
    {
        UiKit.TitleFont = titleFont;
        UiKit.BodyFont = titleFont != null ? titleFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        UiKit.RoundSprite = ProcArt.RoundedRect(64, 24, Color.white, new Color(1, 1, 1, 1), 0, 0f, 100f);
        UiKit.HeartSprite = ProcArt.Heart(128, new Color(1f, 0.35f, 0.55f), new Color(0.55f, 0.08f, 0.3f));

        pipeBodySprite = ProcArt.RoundedRect(64, 12, new Color(0.42f, 0.24f, 0.62f), new Color(0.16f, 0.05f, 0.27f), 3, 0.30f, 64f);
        pipeCapSprite = ProcArt.RoundedRect(64, 12, new Color(1f, 0.50f, 0.70f), new Color(0.16f, 0.05f, 0.27f), 3, 0.22f, 64f);

        starSprite = ProcArt.Star(128);
        bubbleSprite = ProcArt.Bubble(256);
        level = Mathf.Clamp(PlayerPrefs.GetInt("ofg_level", 1), 0, Levels.Length - 1);

        mustafa = new Bird { name = "Mustafa", idle = birdIdle, flap = birdFlap, dead = birdDead };
        doha = new Bird { name = "Doha" };
        myLook = Mathf.Clamp(PlayerPrefs.GetInt("ofg_doha_style", 0), 0, ProcArt.Styles.Length - 1);
        myId = Mathf.Clamp(PlayerPrefs.GetInt("ofg_who", IdMustafa), 0, 1);   // last time's choice is pre-selected
        ApplyDohaStyle(myLook, false);
    }

    // ---- Doha's look (she picks it in the menu; the host receives it when she joins) ----
    class StyleSet { public Sprite idle, flap, dead; }
    readonly StyleSet[] styleCache = new StyleSet[ProcArt.Styles.Length];
    int dohaStyle;

    StyleSet GetStyle(int i, bool full)
    {
        i = Mathf.Clamp(i, 0, ProcArt.Styles.Length - 1);
        var s = styleCache[i];
        if (s == null)
        {
            s = new StyleSet { idle = ProcArt.MakeFemaleBird(birdIdle, true, i) };
            styleCache[i] = s;
        }
        if (full && s.flap == null)
        {
            s.flap = ProcArt.MakeFemaleBird(birdFlap, true, i);
            s.dead = ProcArt.MakeFemaleBird(birdDead, false, i);
        }
        return s;
    }

    void ApplyDohaStyle(int i, bool full)
    {
        dohaStyle = Mathf.Clamp(i, 0, ProcArt.Styles.Length - 1);
        var s = GetStyle(dohaStyle, full);
        doha.idle = s.idle;
        doha.flap = s.flap != null ? s.flap : s.idle;
        doha.dead = s.dead != null ? s.dead : s.idle;
        if (doha.sr != null) doha.sr.sprite = doha.idle;
    }

    Image swatchRing;
    readonly List<GameObject> swatchObjs = new List<GameObject>();
    RectTransform whoP;
    Image[] whoCards = new Image[2];
    Text switchLabel, hostTell, joinPrompt;

    void PickStyle(int i)
    {
        myLook = i;
        PlayerPrefs.SetInt("ofg_doha_style", i);
        PlayerPrefs.Save();
        ApplyDohaStyle(i, false);
        if (menuBirdB != null) menuBirdB.sprite = doha.idle;
        MoveRing(i);
    }

    Vector2 SwatchPos(int i) { return new Vector2(-130 + (i - (ProcArt.Styles.Length - 1) * 0.5f) * 50f, 4f); }

    void MoveRing(int i)
    {
        if (swatchRing != null) swatchRing.rectTransform.anchoredPosition = SwatchPos(i);
    }

    void BuildWorld()
    {
        worldRoot = new GameObject("World").transform;

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(worldRoot, false);
        bgSr = bgGo.AddComponent<SpriteRenderer>();
        bgSr.sprite = background;
        bgSr.sortingOrder = -100;

        for (int i = 0; i < PoolSize; i++) pipes.Add(MakePipe());
        for (int i = 0; i < StarPool; i++)
        {
            var sg = new GameObject("Star");
            sg.transform.SetParent(worldRoot, false);
            var ssr = sg.AddComponent<SpriteRenderer>();
            ssr.sprite = starSprite;
            ssr.sortingOrder = 5;
            sg.SetActive(false);
            starViews.Add(new StarView { sr = ssr });
        }

        foreach (var b in new[] { mustafa, doha })
        {
            b.go = new GameObject(b.name + " bird");
            b.go.transform.SetParent(worldRoot, false);
            b.sr = b.go.AddComponent<SpriteRenderer>();
            b.sr.sprite = b.idle;
            b.sr.sortingOrder = b == doha ? 11 : 10;
            b.go.transform.localScale = Vector3.one * 0.26f;

            var bubble = new GameObject(b.name + " shield");
            bubble.transform.SetParent(worldRoot, false);
            b.bubble = bubble.AddComponent<SpriteRenderer>();
            b.bubble.sprite = bubbleSprite;
            b.bubble.sortingOrder = 12;
            bubble.SetActive(false);
        }
    }

    PipeView MakePipe()
    {
        var p = new PipeView();
        p.root = new GameObject("Pipe").transform;
        p.root.SetParent(worldRoot, false);
        p.topBody = MakeSliced(p.root, pipeBodySprite, 1);
        p.botBody = MakeSliced(p.root, pipeBodySprite, 1);
        p.topCap = MakeSliced(p.root, pipeCapSprite, 2);
        p.botCap = MakeSliced(p.root, pipeCapSprite, 2);
        p.root.gameObject.SetActive(false);
        return p;
    }

    SpriteRenderer MakeSliced(Transform parent, Sprite s, int order)
    {
        var go = new GameObject("part");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sortingOrder = order;
        return sr;
    }

    // ============================================================ UI
    void BuildUi()
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var ct = cgo.transform;

        decoP = UiKit.Panel(ct, "Deco");
        whoP = UiKit.Panel(ct, "Who");
        menuP = UiKit.Panel(ct, "Menu");
        hostP = UiKit.Panel(ct, "Host");
        joinP = UiKit.Panel(ct, "Join");
        hudP = UiKit.Panel(ct, "Hud");
        countP = UiKit.Panel(ct, "Countdown");
        resultP = UiKit.Panel(ct, "Result");

        BuildDeco();
        BuildWho();
        BuildMenu();
        BuildHost();
        BuildJoin();
        BuildHud();
        BuildCountdown();
        BuildResult();

        toastText = UiKit.LabelAt(ct, "", 34, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(760, 90));
        toastText.gameObject.SetActive(false);
    }

    static readonly Color Pink = new Color(1f, 0.42f, 0.66f);
    static readonly Color Purple = new Color(0.55f, 0.36f, 0.85f);
    static readonly Color Teal = new Color(0.18f, 0.72f, 0.72f);
    static readonly Color Grey = new Color(0.45f, 0.4f, 0.55f);
    static readonly Color Cream = new Color(1f, 0.96f, 0.9f);

    void BuildDeco()
    {
        var rng = new System.Random(7);
        for (int i = 0; i < 12; i++)
        {
            var go = new GameObject("Heart", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(decoP, false);
            var img = go.GetComponent<Image>();
            img.sprite = UiKit.HeartSprite;
            img.raycastTarget = false;
            float size = 40 + (float)rng.NextDouble() * 60;
            img.color = new Color(1f, 0.55f + (float)rng.NextDouble() * 0.3f, 0.7f, 0.55f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            float x = (float)rng.NextDouble();
            rt.anchorMin = rt.anchorMax = new Vector2(x, 0f);
            hearts.Add(new FloatHeart
            {
                rt = rt,
                speed = 40 + (float)rng.NextDouble() * 60,
                phase = (float)rng.NextDouble() * 10f,
                x = x
            });
            rt.anchoredPosition = new Vector2(0, (float)rng.NextDouble() * 700f);
        }
    }

    void BuildMenu()
    {
        UiKit.Label(menuP, "Our First Game", 120, Color.white, new Vector2(0, 292), new Vector2(900, 130));

        // Mustafa & Doha, flanked by hearts.
        UiKit.Label(menuP, "Doha & Mustafa", 62, new Color(1f, 0.78f, 0.88f), new Vector2(0, 208), new Vector2(560, 80));
        UiKit.Pic(menuP, UiKit.HeartSprite, new Vector2(-285, 208), new Vector2(52, 52));
        UiKit.Pic(menuP, UiKit.HeartSprite, new Vector2(285, 208), new Vector2(52, 52));

        menuBirdA = UiKit.Pic(menuP, mustafa.idle, new Vector2(130, 118), new Vector2(130, 130));   // Mustafa (right)
        menuBirdB = UiKit.Pic(menuP, doha.idle, new Vector2(-130, 118), new Vector2(130, 130));     // Doha (left, first)
        UiKit.Label(menuP, "Doha", 30, new Color(1f, 0.62f, 0.82f), new Vector2(-130, 48), new Vector2(240, 38));
        UiKit.Label(menuP, "Mustafa", 30, new Color(0.75f, 1f, 0.55f), new Vector2(130, 48), new Vector2(240, 38));

        // Doha picks her look here.
        var circle = ProcArt.Circle(64, Color.white, new Color(0.16f, 0.05f, 0.27f), 4, 100f);
        swatchRing = UiKit.Pic(menuP, circle, SwatchPos(myLook), new Vector2(50, 50), Color.white);
        swatchObjs.Add(swatchRing.gameObject);
        for (int i = 0; i < ProcArt.Styles.Length; i++)
        {
            int idx = i;
            var go = new GameObject("Look" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(menuP, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = SwatchPos(i);
            rt.sizeDelta = new Vector2(40, 40);
            var img = go.GetComponent<Image>();
            img.sprite = circle;
            img.color = ProcArt.Styles[i].swatch;
            go.GetComponent<Button>().targetGraphic = img;
            go.GetComponent<Button>().onClick.AddListener(() => PickStyle(idx));
            swatchObjs.Add(go);
        }

        // difficulty: the host's choice is used for the whole room
        for (int i = 0; i < Levels.Length; i++)
        {
            int lv = i;
            var lb = UiKit.Btn(menuP, Levels[i].name, Color.white, new Vector2((i - 1) * 172f, -50f), new Vector2(160, 52), () => PickLevel(lv), 28);
            levelBtnImgs[i] = lb.GetComponent<Image>();
        }
        RefreshLevelButtons();

        // who am I (tap to go back to the name screen)
        var sw = UiKit.Btn(menuP, "", new Color(0.45f, 0.40f, 0.60f, 0.92f), new Vector2(130, -42), new Vector2(240, 52), SwitchPlayer, 24, new Vector2(0f, 1f));
        switchLabel = sw.GetComponentInChildren<Text>();

        UiKit.Btn(menuP, "Create Room", Pink, new Vector2(0, -122), new Vector2(520, 76), StartHosting, 42);
        UiKit.Btn(menuP, "Join Room", Purple, new Vector2(0, -206), new Vector2(520, 76), GoJoin, 42);
        UiKit.Btn(menuP, "Play Solo", Teal, new Vector2(0, -290), new Vector2(520, 76), StartSolo, 42);

        UiKit.Label(menuP, "Based on Flappy Bird Unity by Abhiraj (MIT)", 22, new Color(1, 1, 1, 0.7f),
            new Vector2(0, -343), new Vector2(800, 32), TextAnchor.MiddleCenter, false, true);
    }

    void BuildHost()
    {
        UiKit.Label(hostP, "Create Room", 80, Color.white, new Vector2(0, 285), new Vector2(800, 110));
        hostTell = UiKit.Label(hostP, "Tell your friend this code:", 38, Cream, new Vector2(0, 195), new Vector2(800, 60), TextAnchor.MiddleCenter, false);
        hostCode = UiKit.Label(hostP, "----", 170, new Color(1f, 0.85f, 0.4f), new Vector2(0, 70), new Vector2(700, 190));
        hostLink = UiKit.Label(hostP, "", 24, Cream, new Vector2(0, -50), new Vector2(900, 60), TextAnchor.MiddleCenter, false);
        hostStatus = UiKit.Label(hostP, "Connecting...", 40, Color.white, new Vector2(0, -125), new Vector2(900, 70), TextAnchor.MiddleCenter, false);
        UiKit.Btn(hostP, "Share Invite", Pink, new Vector2(-150, -215), new Vector2(290, 80), ShareInvite, 36);
        UiKit.Btn(hostP, "Copy Link", Teal, new Vector2(150, -215), new Vector2(250, 80), CopyLink, 36);
        UiKit.Btn(hostP, "Back", Grey, new Vector2(-170, -310), new Vector2(300, 72), ToMenu, 38);
        hostRetryBtn = UiKit.Btn(hostP, "Try Again", Purple, new Vector2(170, -310), new Vector2(300, 72), StartHosting, 38).gameObject;
        hostRetryBtn.SetActive(false);
    }

    void BuildJoin()
    {
        UiKit.Label(joinP, "Join Room", 80, Color.white, new Vector2(0, 300), new Vector2(800, 110));
        joinPrompt = UiKit.Label(joinP, "Enter your friend's 4 digit code", 34, Cream, new Vector2(0, 232), new Vector2(900, 50), TextAnchor.MiddleCenter, false);
        joinCodeText = UiKit.Label(joinP, "_ _ _ _", 120, new Color(1f, 0.85f, 0.4f), new Vector2(0, 150), new Vector2(700, 140));
        joinStatus = UiKit.Label(joinP, "", 32, Color.white, new Vector2(0, -312), new Vector2(1000, 50), TextAnchor.MiddleCenter, false);

        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "DEL", "0", "GO" };
        for (int i = 0; i < keys.Length; i++)
        {
            int col = i % 3, row = i / 3;
            string k = keys[i];
            Color c = k == "GO" ? Pink : (k == "DEL" ? Grey : Purple);
            UiKit.Btn(joinP, k, c, new Vector2((col - 1) * 170, 50 - row * 100), new Vector2(155, 88),
                () => KeyPressed(k), 46);
        }
        UiKit.Btn(joinP, "Back", Grey, new Vector2(115, -50), new Vector2(190, 70), ToMenu, 34, new Vector2(0f, 1f));
    }

    void BuildHud()
    {
        hudScore = UiKit.LabelAt(hudP, "0", 110, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -75), new Vector2(400, 130));
        hudLeft = UiKit.LabelAt(hudP, "", 34, new Color(1f, 0.6f, 0.8f), new Vector2(0f, 1f), new Vector2(190, -40), new Vector2(360, 52), TextAnchor.MiddleLeft);
        hudRight = UiKit.LabelAt(hudP, "", 34, new Color(0.7f, 1f, 0.5f), new Vector2(1f, 1f), new Vector2(-190, -40), new Vector2(360, 52), TextAnchor.MiddleRight);

        // level / stars / shield line under the score
        hudInfo = UiKit.LabelAt(hudP, "", 28, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -158), new Vector2(900, 40), TextAnchor.MiddleCenter, false);
        for (int i = 0; i < popTexts.Length; i++)
        {
            var pt = UiKit.Label(hudP, "", 40, new Color(1f, 0.92f, 0.4f), Vector2.zero, new Vector2(360, 54));
            pt.gameObject.SetActive(false);
            popTexts[i] = pt;
        }

        // name tags that follow the birds
        nameMustafa = UiKit.Label(hudP, "Mustafa", 30, new Color(0.75f, 1f, 0.55f), Vector2.zero, new Vector2(240, 40));
        nameDoha = UiKit.Label(hudP, "Doha", 30, new Color(1f, 0.62f, 0.82f), Vector2.zero, new Vector2(240, 40));

        var pill = UiKit.Box(hudP, new Color(0.15f, 0.05f, 0.25f, 0.75f), new Vector2(0, 95), new Vector2(900, 90), new Vector2(0.5f, 0f));
        banner = pill.gameObject;
        bannerText = UiKit.Label(pill.transform, "", 36, Color.white, Vector2.zero, new Vector2(860, 76), TextAnchor.MiddleCenter, false);
        banner.SetActive(false);
    }

    void BuildCountdown()
    {
        countText = UiKit.Label(countP, "3", 300, Color.white, new Vector2(0, 40), new Vector2(700, 340));
        var tip = UiKit.Label(countP, "Tap, click or press Space to flap!", 40, Cream, new Vector2(0, -170), new Vector2(1000, 70), TextAnchor.MiddleCenter, false);
        tip.gameObject.name = "Tip";
    }

    void BuildResult()
    {
        var dim = UiKit.Box(resultP, new Color(0.1f, 0.03f, 0.2f, 0.62f), Vector2.zero, new Vector2(1000, 560));
        dim.transform.SetAsFirstSibling();
        resultTitle = UiKit.Label(resultP, "", 100, new Color(1f, 0.85f, 0.4f), new Vector2(0, 200), new Vector2(940, 140));
        resultLine = UiKit.Label(resultP, "", 46, Color.white, new Vector2(0, 80), new Vector2(940, 130), TextAnchor.MiddleCenter, false);
        playAgainBtn = UiKit.Btn(resultP, "Play Again", Pink, new Vector2(0, -50), new Vector2(480, 86), PlayAgain, 44).gameObject;
        UiKit.Btn(resultP, "Menu", Grey, new Vector2(0, -150), new Vector2(480, 80), ToMenu, 40);
        resultHint = UiKit.Label(resultP, "", 30, Cream, new Vector2(0, -235), new Vector2(940, 50), TextAnchor.MiddleCenter, false);
    }

    // ============================================================ state changes
    void SetState(State s)
    {
        state = s;
        bool menuLike = s == State.Who || s == State.Menu || s == State.Hosting || s == State.Joining;
        decoP.gameObject.SetActive(menuLike || s == State.Result);
        whoP.gameObject.SetActive(s == State.Who);
        menuP.gameObject.SetActive(s == State.Menu);
        hostP.gameObject.SetActive(s == State.Hosting);
        joinP.gameObject.SetActive(s == State.Joining);
        hudP.gameObject.SetActive(s == State.Round);
        countP.gameObject.SetActive(false);
        resultP.gameObject.SetActive(s == State.Result);
        bool showWorld = s == State.Round || s == State.Result;
        foreach (var p in pipes) if (!showWorld) p.root.gameObject.SetActive(false);
        if (!showWorld)
        {
            // the birds are switched on again by RenderBird (only the ones that are playing)
            mustafa.go.SetActive(false);
            doha.go.SetActive(false);
            if (mustafa.bubble != null) mustafa.bubble.gameObject.SetActive(false);
            if (doha.bubble != null) doha.bubble.gameObject.SetActive(false);
        }
    }

    void ToMenu()
    {
        if (online) PeerNet.Send("L");
        PeerNet.Close();
        online = false;
        joinWaiting = false;
        helloSent = false;
        SetState(State.Menu);
    }

    void StartSolo()
    {
        online = false;
        iAmHost = true;
        BeginRound(Random.Range(1, 1000000), level);
    }

    static string RandomCode()
    {
        return Random.Range(1000, 10000).ToString(CultureInfo.InvariantCulture);
    }

    void StartHosting()
    {
        PeerNet.Close();
        online = true;
        iAmHost = true;
        hostTries = 0;
        roomCode = RandomCode();
        PeerNet.Start(true, roomCode);
        SetState(State.Hosting);
        hostTell.text = "Tell " + PartnerName + " this code:";
        if (hostRetryBtn != null) hostRetryBtn.SetActive(false);
        RefreshHostTexts();
    }

    void RefreshHostTexts()
    {
        hostCode.text = roomCode;
        hostLink.text = PeerNet.PageUrl() + "?room=" + roomCode;
    }

    void CopyLink()
    {
        string link = PeerNet.PageUrl() + "?room=" + roomCode;
        bool ok = PeerNet.CopyText(link);
        Toast(ok ? "Invite link copied!" : "Send Doha the code above");
    }

    void BuildWho()
    {
        UiKit.Label(whoP, "Who are you?", 100, Color.white, new Vector2(0, 268), new Vector2(900, 130));
        UiKit.Label(whoP, "Pick your name - your friend plays the other one", 30, Cream, new Vector2(0, 190), new Vector2(900, 44), TextAnchor.MiddleCenter, false);
        whoCards[IdDoha] = UiKit.Pic(whoP, doha.idle, new Vector2(-150, 58), new Vector2(190, 190));
        whoCards[IdMustafa] = UiKit.Pic(whoP, mustafa.idle, new Vector2(150, 58), new Vector2(190, 190));
        UiKit.Btn(whoP, "I'm Doha", Pink, new Vector2(-150, -92), new Vector2(270, 84), () => ChooseIdentity(IdDoha), 40);
        UiKit.Btn(whoP, "I'm Mustafa", new Color(0.35f, 0.72f, 0.30f), new Vector2(150, -92), new Vector2(270, 84), () => ChooseIdentity(IdMustafa), 40);
        UiKit.Label(whoP, "Doha & Mustafa", 46, new Color(1f, 0.78f, 0.88f), new Vector2(0, -222), new Vector2(700, 64));
    }

    void ChooseIdentity(int id)
    {
        myId = id;
        PlayerPrefs.SetInt("ofg_who", id);
        PlayerPrefs.Save();
        if (id == IdDoha) ApplyDohaStyle(myLook, false);
        RefreshIdentityUi();
        if (!string.IsNullOrEmpty(pendingRoom))
        {
            // came from an invite link: go straight into that room
            joinCode = pendingRoom;
            pendingRoom = null;
            GoJoin();
            TryJoin();
        }
        else
        {
            SetState(State.Menu);
        }
    }

    void SwitchPlayer()
    {
        SetState(State.Who);
    }

    void RefreshIdentityUi()
    {
        if (switchLabel != null) switchLabel.text = "I'm " + MyName + "  (change)";
        // only Doha has a colour to pick
        foreach (var o in swatchObjs) if (o != null) o.SetActive(myId == IdDoha);
        // the card of the name you used last time is a little bigger
        for (int i = 0; i < whoCards.Length; i++)
            if (whoCards[i] != null) whoCards[i].rectTransform.localScale = i == myId ? Vector3.one * 1.1f : Vector3.one;
    }

    static readonly Color[] LevelColors =
    {
        new Color(0.30f, 0.72f, 0.40f), new Color(0.98f, 0.62f, 0.20f), new Color(0.90f, 0.28f, 0.34f)
    };

    void PickLevel(int i)
    {
        level = Mathf.Clamp(i, 0, Levels.Length - 1);
        PlayerPrefs.SetInt("ofg_level", level);
        PlayerPrefs.Save();
        RefreshLevelButtons();
    }

    void RefreshLevelButtons()
    {
        for (int i = 0; i < levelBtnImgs.Length; i++)
        {
            if (levelBtnImgs[i] == null) continue;
            Color c = LevelColors[i];
            levelBtnImgs[i].color = i == level ? c : new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.85f);
            levelBtnImgs[i].rectTransform.localScale = i == level ? Vector3.one * 1.08f : Vector3.one;
        }
    }

    static void ApplyLevel(int i)
    {
        Lv = Levels[Mathf.Clamp(i, 0, Levels.Length - 1)];
    }

    // Native share sheet (WhatsApp...) opens on top of the game, so the page is not left.
    void ShareInvite()
    {
        string link = PeerNet.PageUrl() + "?room=" + roomCode;
        string text = "Come play Our First Game with me! Room code: " + roomCode;
        if (!PeerNet.Share(text, link)) CopyLink();
    }

    void GoJoin()
    {
        PeerNet.Close();
        online = true;
        iAmHost = false;
        joinWaiting = false;
        helloSent = false;
        SetState(State.Joining);
        joinPrompt.text = "Enter " + PartnerName + "'s 4 digit code";
        RefreshJoinCode();
        joinStatus.text = "";
    }

    void RefreshJoinCode()
    {
        string s = "";
        for (int i = 0; i < 4; i++) s += (i < joinCode.Length ? joinCode[i].ToString() : "_") + (i < 3 ? " " : "");
        joinCodeText.text = s;
    }

    void KeyPressed(string k)
    {
        if (state != State.Joining || joinWaiting) return;
        if (k == "DEL") { if (joinCode.Length > 0) joinCode = joinCode.Substring(0, joinCode.Length - 1); }
        else if (k == "GO") { TryJoin(); }
        else if (joinCode.Length < 4) { joinCode += k; if (joinCode.Length == 4) TryJoin(); }
        RefreshJoinCode();
    }

    void TryJoin()
    {
        if (joinCode.Length != 4) { Toast("Enter all 4 digits"); return; }
        helloSent = false;
        joinWaiting = true;
        PeerNet.Start(false, joinCode);
        joinStatus.text = "Connecting...";
    }

    // ============================================================ rounds
    void ResetBird(Bird b)
    {
        b.y = 0; b.vy = 0; b.drift = 0; b.score = 0; b.alive = true; b.deathT = 0;
        b.flapUntil = 0; b.targetY = 0;
        b.bonus = 0; b.stars = 0; b.shield = false; b.invulnUntil = 0;
        b.sr.color = Color.white;
        if (b.bubble != null) b.bubble.gameObject.SetActive(false);
        b.sr.sprite = b.idle;
        b.go.transform.rotation = Quaternion.identity;
    }

    void BeginRound(int s, int lv)
    {
        seed = s;
        playingLevel = Mathf.Clamp(lv, 0, Levels.Length - 1);
        ApplyLevel(lv);
        collected.Clear();
        for (int i = 0; i < popTexts.Length; i++) if (popTexts[i] != null) popTexts[i].gameObject.SetActive(false);
        startAt = Time.unscaledTime + CountdownSecs;
        if (online || myId == IdDoha) ApplyDohaStyle(dohaStyle, true); // build all of her frames before the round starts
        ResetBird(mustafa);
        ResetBird(doha);
        otherLeft = false;
        resultTimer = 0;
        nextSend = 0;
        simT = 0f;
        pendingFlap = false;
        otherLastMsg = Time.unscaledTime;
        SetState(State.Round);
        banner.SetActive(false);
        hudLeft.text = online ? (myId == IdDoha ? "Doha (you)" : "Doha") : "";
        hudRight.text = online ? (myId == IdMustafa ? "Mustafa (you)" : "Mustafa") : "";
        hudScore.text = "0";
        countP.gameObject.SetActive(true);
    }

    void PlayAgain()
    {
        if (online)
        {
            if (PeerNet.Current != PeerNet.Status.Connected)
            {
                Toast("Your friend left the room");
                ToMenu();
                return;
            }
            int s = Random.Range(1, 1000000);
            PeerNet.Send("S|" + s.ToString(CultureInfo.InvariantCulture) + "|" + level.ToString(CultureInfo.InvariantCulture) +
                         "|" + myId.ToString(CultureInfo.InvariantCulture) + "|" + dohaStyle.ToString(CultureInfo.InvariantCulture));
            BeginRound(s, level);
        }
        else
        {
            BeginRound(Random.Range(1, 1000000), level);
        }
    }

    // ============================================================ main loop
    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        UpdateCamera();
        UiKit.ClampWidths(((RectTransform)canvas.transform).rect.width);
        PumpNet();
        AnimateDeco(dt);

        switch (state)
        {
            case State.Menu: AnimateMenuBirds(); break;
            case State.Hosting: UpdateHosting(); break;
            case State.Joining: UpdateJoining(); break;
            case State.Round: UpdateRound(dt); break;
            case State.Result: UpdateResult(); break;
        }

        if (toastText.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastText.gameObject.SetActive(false);
    }

    void UpdateCamera()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        float camW = CamSize * 2f * aspect;
        cam.orthographicSize = CamSize;
        // the bird sits ~28% from the left edge
        cam.transform.position = new Vector3((0.5f - 0.28f) * camW, 0f, -10f);

        if (background != null)
        {
            Vector2 bs = background.bounds.size;
            float scale = Mathf.Max(camW / bs.x, (CamSize * 2f) / bs.y) * 1.02f;
            bgSr.transform.localScale = new Vector3(scale, scale, 1f);
            bgSr.transform.position = new Vector3(cam.transform.position.x, 0f, 10f);
        }
    }

    float ViewLeft()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        return -0.28f * CamSize * 2f * aspect;
    }

    float ViewRight()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        return 0.72f * CamSize * 2f * aspect;
    }

    void AnimateDeco(float dt)
    {
        if (!decoP.gameObject.activeSelf) return;
        float h = ((RectTransform)canvas.transform).rect.height;
        for (int i = 0; i < hearts.Count; i++)
        {
            var f = hearts[i];
            Vector2 p = f.rt.anchoredPosition;
            p.y += f.speed * dt;
            if (p.y > h + 80) p.y = -80;
            p.x = Mathf.Sin(Time.unscaledTime * 0.8f + f.phase) * 30f;
            f.rt.anchoredPosition = p;
        }
    }

    void AnimateMenuBirds()
    {
        if (menuBirdA == null) return;
        float t = Time.unscaledTime;
        menuBirdA.rectTransform.anchoredPosition = new Vector2(130, 118 + Mathf.Sin(t * 2.2f) * 10f);
        menuBirdB.rectTransform.anchoredPosition = new Vector2(-130, 118 + Mathf.Sin(t * 2.2f + 1.6f) * 10f);
        menuBirdA.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2.2f) * 6f);
        menuBirdB.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2.2f + 1.6f) * 6f);
    }

    // ------------------------------------------------------------ network
    void PumpNet()
    {
        if (!online) return;
        string m;
        int guard = 0;
        while ((m = PeerNet.Poll()) != null && guard++ < 64) Handle(m);
    }

    static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
    static int I(string s) { return int.Parse(s, CultureInfo.InvariantCulture); }

    void Handle(string msg)
    {
        string[] p = msg.Split('|');
        try
        {
            switch (p[0])
            {
                case "H": // guest says hello -> host starts the first round
                    if (iAmHost && state == State.Hosting)
                    {
                        // the guest sends "H|<wanted name>|<colour>"; the host always keeps its own name
                        int gLook = p.Length > 2 ? Mathf.Clamp(I(p[2]), 0, ProcArt.Styles.Length - 1) : 0;
                        dohaStyle = myId == IdDoha ? myLook : gLook;      // whoever plays Doha decides her colour
                        int s = Random.Range(1, 1000000);
                        PeerNet.Send("S|" + s.ToString(CultureInfo.InvariantCulture) + "|" + level.ToString(CultureInfo.InvariantCulture) +
                                     "|" + myId.ToString(CultureInfo.InvariantCulture) + "|" + dohaStyle.ToString(CultureInfo.InvariantCulture));
                        BeginRound(s, level);
                    }
                    break;
                case "S": // host starts a round
                    if (!iAmHost)
                    {
                        int hostId = p.Length > 3 ? Mathf.Clamp(I(p[3]), 0, 1) : IdMustafa;
                        if (p.Length > 4) dohaStyle = Mathf.Clamp(I(p[4]), 0, ProcArt.Styles.Length - 1);
                        if (myId == hostId)
                        {
                            string taken = hostId == IdDoha ? "Doha" : "Mustafa";
                            string mine = hostId == IdDoha ? "Mustafa" : "Doha";
                            Toast(taken + " is already taken - you play as " + mine);
                        }
                        myId = 1 - hostId;
                        RefreshIdentityUi();
                        BeginRound(I(p[1]), p.Length > 2 ? I(p[2]) : 1);
                    }
                    break;
                case "P": // opponent state: P|seed|y|vy|score|alive
                    {
                        if (state != State.Round || I(p[1]) != seed) break;
                        Bird o = Other;
                        otherLastMsg = Time.unscaledTime;
                        o.targetY = F(p[2]);
                        float ovy = F(p[3]);
                        o.score = I(p[4]);
                        if (ovy > FlapV - 1.5f) o.flapUntil = Time.unscaledTime + 0.15f;
                        if (p.Length > 6) o.shield = p[6] == "1";
                        if (p[5] == "0" && o.alive) KillOther(o);
                        break;
                    }
                case "D": // opponent crashed: D|seed|time|score
                    {
                        if (state != State.Round || I(p[1]) != seed) break;
                        Bird o = Other;
                        o.deathT = F(p[2]);
                        o.score = I(p[3]);
                        if (o.alive) KillOther(o);
                        break;
                    }
                case "L":
                    otherLeft = true;
                    if (Other.alive && state == State.Round) KillOther(Other);
                    break;
            }
        }
        catch (System.Exception)
        {
            // ignore malformed messages
        }
    }

    void KillOther(Bird o)
    {
        o.alive = false;
        o.vy = 4f;
        if (state == State.Round) o.deathT = Mathf.Max(o.deathT, Time.unscaledTime - startAt);
    }

    void UpdateHosting()
    {
        var st = PeerNet.Current;
        switch (st)
        {
            case PeerNet.Status.IdTaken:
                if (hostTries++ < 6)
                {
                    roomCode = RandomCode();
                    PeerNet.Start(true, roomCode);
                    RefreshHostTexts();
                }
                else { Toast("Could not create a room, try again"); ToMenu(); }
                break;
            case PeerNet.Status.Error:
                // stay on this screen and say what went wrong instead of jumping back to the menu
                hostStatus.text = "Can't reach the room server " + Describe(PeerNet.Error);
                hostRetryBtn.SetActive(true);
                break;
            case PeerNet.Status.Hosting:
                hostStatus.text = "Waiting for " + PartnerName + Dots();
                hostRetryBtn.SetActive(false);
                break;
            case PeerNet.Status.Reconnecting:
                // e.g. you switched to WhatsApp, or the connection is flaky: keep the room and keep trying
                hostStatus.text = "Reaching the room server" + Dots() + " " + Describe(PeerNet.Error);
                break;
            case PeerNet.Status.Connected:
                hostStatus.text = PartnerName + " is here! Starting...";
                break;
            case PeerNet.Status.Closed:
                hostStatus.text = PartnerName + " left. Waiting" + Dots();
                break;
            default:
                hostStatus.text = "Connecting to the server" + Dots();
                break;
        }
    }

    static string Describe(string err)
    {
        return string.IsNullOrEmpty(err) ? "" : "[" + err + "]";
    }

    string Dots()
    {
        int n = (int)(Time.unscaledTime * 2f) % 4;
        return new string('.', n);
    }

    void UpdateJoining()
    {
        // keyboard entry
        if (!joinWaiting)
        {
            foreach (char c in Input.inputString)
            {
                if (c >= '0' && c <= '9') KeyPressed(c.ToString());
                else if (c == '\b') KeyPressed("DEL");
                else if (c == '\n' || c == '\r') KeyPressed("GO");
            }
        }
        if (!joinWaiting) return;

        switch (PeerNet.Current)
        {
            case PeerNet.Status.Connected:
                if (!helloSent) { PeerNet.Send("H|" + myId.ToString(CultureInfo.InvariantCulture) + "|" + myLook.ToString(CultureInfo.InvariantCulture)); helloSent = true; }
                joinStatus.text = "Connected! Waiting for " + PartnerName + " to start" + Dots();
                break;
            case PeerNet.Status.NotFound:
                Toast(PeerNet.Error == "full" ? "That room already has two players" : "Room not found. Check the code");
                joinWaiting = false;
                PeerNet.Close();
                joinStatus.text = "";
                break;
            case PeerNet.Status.Error:
                {
                    string why = Describe(PeerNet.Error);
                    Toast("Can't reach the room server " + why);
                    joinWaiting = false;
                    PeerNet.Close();
                    joinStatus.text = "Can't reach the room server " + why + " - try again";
                    break;
                }
            case PeerNet.Status.Closed:
                Toast("The room was closed");
                joinWaiting = false;
                PeerNet.Close();
                joinStatus.text = "";
                break;
            default:
                joinStatus.text = "Connecting" + Dots();
                break;
        }
    }

    // ------------------------------------------------------------ round
    bool FlapInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return true;
        if (Input.GetMouseButtonDown(0)) return true;
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
        return false;
    }

    void UpdateRound(float dt)
    {
        float t = Time.unscaledTime - startAt;
        Bird me = Me;
        Bird other = Other;

        // countdown overlay
        if (t < 0f)
        {
            countP.gameObject.SetActive(true);
            countText.text = Mathf.CeilToInt(-t).ToString(CultureInfo.InvariantCulture);
        }
        else if (t < 0.7f)
        {
            countP.gameObject.SetActive(true);
            countText.text = "GO!";
            var tip = countP.Find("Tip");
            if (tip != null) tip.gameObject.SetActive(false);
        }
        else
        {
            countP.gameObject.SetActive(false);
            var tip = countP.Find("Tip");
            if (tip != null) tip.gameObject.SetActive(true);
        }

        if (FlapInput())
        {
            if (me.alive) me.flapUntil = Time.unscaledTime + 0.15f;
            if (t >= 0f) pendingFlap = true;
        }

        if (t < 0f)
        {
            // hover during the countdown
            simT = 0f;
            pendingFlap = false;
            if (me.alive) { me.y = Mathf.Sin(Time.unscaledTime * 3f) * 0.25f; me.vy = 0f; }
        }
        else
        {
            // Fixed-step physics tied to the real clock, so a slow phone and a fast PC
            // see exactly the same bird physics (the pipes are driven by the clock too).
            if (t - simT > 0.25f) simT = t - 0.25f;
            while (simT + Step <= t)
            {
                simT += Step;
                StepBird(me, simT);
                if (online && !other.alive) FallStep(other, simT);
            }
        }

        if (online)
        {
            if (other.alive)
            {
                other.y = Mathf.Lerp(other.y, other.targetY, 1f - Mathf.Exp(-14f * dt));
                // connection dropped without a goodbye
                var st = PeerNet.Current;
                if ((st == PeerNet.Status.Closed || st == PeerNet.Status.Error) && t > 0f) { otherLeft = true; KillOther(other); }
                // nothing heard for a while (their phone went to sleep / lost signal)
                else if (t > 1f && Time.unscaledTime - otherLastMsg > 8f) { otherLeft = true; KillOther(other); }
            }

            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + SendInterval;
                PeerNet.Send("P|" + seed.ToString(CultureInfo.InvariantCulture) + "|" + me.y.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                             me.vy.ToString("F1", CultureInfo.InvariantCulture) + "|" + me.score + "|" + (me.alive ? "1" : "0") + "|" + (me.shield ? "1" : "0"));
            }
        }

        RenderWorld(t);
        UpdateHud(me, other);

        // round end
        bool over = online ? (!me.alive && !other.alive) : !me.alive;
        if (over)
        {
            resultTimer += dt;
            if (resultTimer > 1.1f) ShowResult(t);
        }
    }

    void StepBird(Bird me, float tt)
    {
        if (!me.alive) { FallStep(me, tt); return; }
        if (pendingFlap) { me.vy = FlapV; pendingFlap = false; }
        me.vy = Mathf.Max(me.vy - Gravity * Step, MaxFall);
        me.y += me.vy * Step;
        if (me.y > CeilY) { me.y = CeilY; if (me.vy > 0f) me.vy = 0f; }
        float d = Dist(tt);
        CollectStars(me, d);
        me.score = ScoreAt(d) + me.bonus;

        bool hit = me.y < FloorY || HitsPipe(tt, me.y);
        if (!hit) return;
        if (tt < me.invulnUntil)
        {
            // still protected after the shield popped: slide through, but never fall out of the world
            if (me.y < FloorY) { me.y = FloorY; if (me.vy < 0f) me.vy = 0f; }
            return;
        }
        if (me.shield)
        {
            me.shield = false;
            me.invulnUntil = tt + 1.3f;
            me.vy = FlapV;
            if (me.y < FloorY) me.y = FloorY;
            Pop("Shield saved you!");
            return;
        }
        Die(me, tt);
    }

    // Stars are the same for both players (they come from the round's seed), but each player
    // collects them on their own: +2 points each, and every 5 stars give a shield.
    void CollectStars(Bird me, float d)
    {
        int iMin = Mathf.Max(0, Mathf.FloorToInt((d - FirstPipeX) / PipeSpacing));
        int from = Mathf.Max(0, (iMin - 1) * 2), to = (iMin + 2) * 2 + 1;
        for (int id = from; id <= to; id++)
        {
            if (collected.Contains(id)) continue;
            float sx, sy;
            if (!StarAt(id, out sx, out sy)) continue;
            float dx = sx - d, dy = sy - me.y;
            if (dx * dx + dy * dy > 0.75f * 0.75f) continue;
            collected.Add(id);
            me.stars++;
            me.bonus += StarPoints;
            if (me.stars % StarsForShield == 0) { me.shield = true; Pop("Shield!"); }
            else Pop("+" + StarPoints);
        }
    }

    void Pop(string text)
    {
        int slot = 0;
        float oldest = float.MaxValue;
        for (int i = 0; i < popTexts.Length; i++)
        {
            if (popTexts[i] == null) continue;
            if (!popTexts[i].gameObject.activeSelf) { slot = i; oldest = -1f; break; }
            if (popBorn[i] < oldest) { oldest = popBorn[i]; slot = i; }
        }
        if (popTexts[slot] == null) return;
        popTexts[slot].text = text;
        popTexts[slot].gameObject.SetActive(true);
        popBorn[slot] = Time.unscaledTime;
    }

    void PlacePops(Bird me)
    {
        for (int i = 0; i < popTexts.Length; i++)
        {
            var pt = popTexts[i];
            if (pt == null || !pt.gameObject.activeSelf) continue;
            float age = Time.unscaledTime - popBorn[i];
            if (age > 0.9f) { pt.gameObject.SetActive(false); continue; }
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(me.go.transform.position.x, me.y + 1.0f + age * 1.4f, 0f));
            Vector2 lp;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, sp, null, out lp))
                pt.rectTransform.anchoredPosition = lp;
            var c = pt.color;
            c.a = 1f - age / 0.9f;
            pt.color = c;
        }
    }

    void FallStep(Bird b, float tt)
    {
        b.vy = Mathf.Max(b.vy - Gravity * Step, MaxFall);
        b.y += b.vy * Step;
        b.drift -= Speed(tt) * Step;
    }

    void Die(Bird b, float t)
    {
        b.alive = false;
        b.deathT = t;
        b.vy = 4f;
        if (online)
            PeerNet.Send("D|" + seed.ToString(CultureInfo.InvariantCulture) + "|" + t.ToString("F3", CultureInfo.InvariantCulture) + "|" + b.score);
    }

    bool HitsPipe(float t, float y)
    {
        float d = Dist(t);
        int iMin = Mathf.Max(0, Mathf.FloorToInt((d - FirstPipeX) / PipeSpacing));
        for (int i = iMin; i <= iMin + 1; i++)
        {
            float relX = FirstPipeX + i * PipeSpacing - d;
            float gc = GapCenter(i);
            // nearest point of the pipe column to the bird (bird is at x = 0)
            float nx = Mathf.Clamp(0f, relX - PipeW * 0.5f, relX + PipeW * 0.5f);
            float dx = -nx;
            float topEdge = gc + GapH * 0.5f;
            float botEdge = gc - GapH * 0.5f;
            float dyTop = y - Mathf.Max(y, topEdge);
            float dyBot = y - Mathf.Min(y, botEdge);
            if (dx * dx + dyTop * dyTop < BirdR * BirdR) return true;
            if (dx * dx + dyBot * dyBot < BirdR * BirdR) return true;
        }
        return false;
    }

    // ------------------------------------------------------------ rendering
    void RenderWorld(float t)
    {
        float d = Dist(t);
        float left = ViewLeft() - 2.5f;
        float right = ViewRight() + 2.5f;
        int iMin = Mathf.Max(0, Mathf.CeilToInt((d + left - FirstPipeX) / PipeSpacing));
        int iMax = Mathf.FloorToInt((d + right - FirstPipeX) / PipeSpacing);

        foreach (var p in pipes) p.index = -1;
        // assign slots
        for (int i = iMin; i <= iMax; i++)
        {
            var p = pipes[i % PoolSize];
            p.index = i;
            LayoutPipe(p, i, FirstPipeX + i * PipeSpacing - d);
        }
        foreach (var p in pipes) p.root.gameObject.SetActive(p.index >= 0);

        // stars that are still there
        int used = 0;
        int sFrom = Mathf.Max(0, iMin * 2 - 1), sTo = iMax * 2 + 1;
        for (int id = sFrom; id <= sTo && used < starViews.Count; id++)
        {
            float sx, sy;
            if (collected.Contains(id) || !StarAt(id, out sx, out sy)) continue;
            var sv = starViews[used++];
            sv.id = id;
            sv.sr.gameObject.SetActive(true);
            float bob = Mathf.Sin(Time.unscaledTime * 3f + id) * 0.10f;
            float pulse = 0.62f + Mathf.Sin(Time.unscaledTime * 5f + id) * 0.04f;
            sv.sr.transform.position = new Vector3(sx - d, sy + bob, 0f);
            sv.sr.transform.localScale = Vector3.one * pulse;
            sv.sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 2f + id) * 12f);
        }
        for (int k = used; k < starViews.Count; k++) starViews[k].sr.gameObject.SetActive(false);

        // together: Doha on the left, Mustafa on the right. Alone: just my bird in the middle.
        if (online)
        {
            RenderBird(doha, -0.30f, t);
            RenderBird(mustafa, 0.30f, t);
        }
        else
        {
            RenderBird(Me, 0f, t);
            Other.go.SetActive(false);
            if (Other.bubble != null) Other.bubble.gameObject.SetActive(false);
        }
    }

    void LayoutPipe(PipeView p, int i, float relX)
    {
        float gc = GapCenter(i);
        float capH = 0.55f;
        float capW = PipeW + 0.30f;
        float topEdge = gc + GapH * 0.5f;
        float botEdge = gc - GapH * 0.5f;
        float farTop = 6.5f, farBot = -6.5f;

        p.root.position = new Vector3(relX, 0f, 0f);

        float topBodyH = Mathf.Max(0.2f, farTop - (topEdge + capH));
        p.topBody.size = new Vector2(PipeW, topBodyH);
        p.topBody.transform.localPosition = new Vector3(0f, topEdge + capH + topBodyH * 0.5f, 0f);
        p.topCap.size = new Vector2(capW, capH);
        p.topCap.transform.localPosition = new Vector3(0f, topEdge + capH * 0.5f, 0f);

        float botBodyH = Mathf.Max(0.2f, (botEdge - capH) - farBot);
        p.botBody.size = new Vector2(PipeW, botBodyH);
        p.botBody.transform.localPosition = new Vector3(0f, botEdge - capH - botBodyH * 0.5f, 0f);
        p.botCap.size = new Vector2(capW, capH);
        p.botCap.transform.localPosition = new Vector3(0f, botEdge - capH * 0.5f, 0f);
    }

    void RenderBird(Bird b, float visX, float t)
    {
        b.go.SetActive(true);
        b.go.transform.position = new Vector3(visX + b.drift, b.y, 0f);
        float ang;
        if (!b.alive)
        {
            b.sr.sprite = b.dead;
            ang = Mathf.Clamp(b.vy * 6f, -85f, 40f);
        }
        else
        {
            b.sr.sprite = Time.unscaledTime < b.flapUntil ? b.flap : b.idle;
            ang = Mathf.Clamp(b.vy * 4f, -60f, 28f);
        }
        b.go.transform.rotation = Quaternion.Euler(0, 0, ang);

        if (b.bubble != null)
        {
            bool show = b.alive && b.shield;
            b.bubble.gameObject.SetActive(show);
            if (show)
            {
                b.bubble.transform.position = new Vector3(visX + b.drift, b.y, 0f);
                b.bubble.transform.localScale = Vector3.one * (0.60f + Mathf.Sin(Time.unscaledTime * 5f) * 0.03f);
            }
        }
        // flash while protected after a shield saved us
        if (b.alive && b == Me && simT < b.invulnUntil)
            b.sr.color = new Color(1f, 1f, 1f, Mathf.Sin(Time.unscaledTime * 40f) > 0f ? 1f : 0.35f);
        else
            b.sr.color = Color.white;
    }

    // Name tag above (Mustafa) / below (Doha) a bird, so the two never overlap.
    void PlaceName(Text label, Bird b, float dy)
    {
        bool show = b.go.activeSelf && b.alive;
        label.gameObject.SetActive(show);
        if (!show) return;
        Vector3 sp = cam.WorldToScreenPoint(new Vector3(b.go.transform.position.x, b.y + dy, 0f));
        Vector2 lp;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, sp, null, out lp))
            label.rectTransform.anchoredPosition = lp;
    }

    void UpdateHud(Bird me, Bird other)
    {
        hudScore.text = me.score.ToString(CultureInfo.InvariantCulture);
        if (online)
        {
            Bird left = doha, right = mustafa;
            hudLeft.text = "Doha" + (left == me ? " (you)" : "") + "  " + left.score;
            hudRight.text = right.score + "  Mustafa" + (right == me ? " (you)" : "");
        }
        else
        {
            hudLeft.text = "";
            hudRight.text = "";
        }

        PlaceName(nameMustafa, mustafa, 0.95f);
        PlaceName(nameDoha, doha, -0.95f);

        hudInfo.text = Lv.name.ToUpper() + "     Stars " + me.stars +
                       (me.shield ? "     SHIELD ON" : "     Shield in " + (StarsForShield - me.stars % StarsForShield));
        PlacePops(me);

        bool watching = online && !me.alive && other.alive;
        banner.SetActive(watching);
        if (watching) bannerText.text = "You crashed! Watching " + other.name + " fly... (" + other.score + ")";
    }

    // ------------------------------------------------------------ result
    void UpdateResult()
    {
        RenderWorld(frozenT);
    }

    void ShowResult(float t)
    {
        frozenT = t;
        Bird me = Me, other = Other;
        banner.SetActive(false);
        hudP.gameObject.SetActive(false);

        if (online)
        {
            float diff = me.deathT - other.deathT;
            bool tie = Mathf.Abs(diff) <= 0.15f && me.score == other.score;
            bool meWin = diff > 0.15f || (Mathf.Abs(diff) <= 0.15f && me.score > other.score);
            if (otherLeft) { meWin = true; tie = false; }
            resultTitle.text = tie ? "It's a tie!" : (meWin ? me.name + " wins!" : other.name + " wins!");
            resultLine.text = "Doha: " + doha.score + "      Mustafa: " + mustafa.score +
                              "\n" + Lv.name + " level   -   your stars: " + me.stars;
            if (otherLeft) resultLine.text += "\n" + other.name + " left the game";
        }
        else
        {
            string bestKey = "ofg_best_" + playingLevel;
            int best = PlayerPrefs.GetInt(bestKey, 0);
            if (me.score > best) { best = me.score; PlayerPrefs.SetInt(bestKey, best); PlayerPrefs.Save(); }
            resultTitle.text = "Game over";
            resultLine.text = "Score: " + me.score + "      Best (" + Lv.name + "): " + best +
                              "\nStars: " + me.stars;
        }

        bool canRestart = !online || iAmHost;
        playAgainBtn.SetActive(canRestart);
        resultHint.text = canRestart ? "" : "Waiting for " + other.name + " to start another round...";
        SetState(State.Result);
        hudP.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ misc
    void Toast(string msg)
    {
        toastText.text = msg;
        toastText.gameObject.SetActive(true);
        toastUntil = Time.unscaledTime + 3f;
        toastText.transform.SetAsLastSibling();
    }

    void OnDestroy()
    {
        PeerNet.Close();
    }
}
