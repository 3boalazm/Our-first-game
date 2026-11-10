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
    const float GapH = 3.9f;
    const float PipeSpacing = 6.5f;
    const float FirstPipeX = 13f;
    const float FloorY = -4.55f;
    const float CeilY = 4.75f;
    const float BaseSpeed = 4.5f;
    const float SpeedGain = 0.04f;
    const float MaxSpeed = 6.6f;
    const float CountdownSecs = 3.2f;
    const float SendInterval = 1f / 15f;

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
        return Mathf.Lerp(-2.3f, 2.3f, r);
    }

    // ------------------------------------------------------------ state
    enum State { Menu, Hosting, Joining, Round, Result }
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
        public int score;
        public bool alive = true;
    }

    Bird mustafa, doha;
    Bird Me { get { return (iAmHost || !online) ? mustafa : doha; } }
    Bird Other { get { return Me == mustafa ? doha : mustafa; } }

    class PipeView
    {
        public Transform root;
        public SpriteRenderer topBody, topCap, botBody, botCap;
        public int index = -1;
    }
    readonly List<PipeView> pipes = new List<PipeView>();
    const int PoolSize = 8;

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

        // Opened through a friend's invite link (?room=1234): jump straight to joining.
        string r = PeerNet.UrlParam("room");
        if (!string.IsNullOrEmpty(r) && r.Length == 4 && IsDigits(r))
        {
            joinCode = r;
            GoJoin();
            TryJoin();
        }
        else
        {
            SetState(State.Menu);
        }
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

        mustafa = new Bird { name = "Mustafa", idle = birdIdle, flap = birdFlap, dead = birdDead };
        doha = new Bird { name = "Doha" };
        dohaStyle = Mathf.Clamp(PlayerPrefs.GetInt("ofg_doha_style", 0), 0, ProcArt.Styles.Length - 1);
        ApplyDohaStyle(dohaStyle, false);
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

    void PickStyle(int i)
    {
        PlayerPrefs.SetInt("ofg_doha_style", i);
        PlayerPrefs.Save();
        ApplyDohaStyle(i, false);
        if (menuBirdB != null) menuBirdB.sprite = doha.idle;
        MoveRing(i);
    }

    Vector2 SwatchPos(int i) { return new Vector2(130 + (i - (ProcArt.Styles.Length - 1) * 0.5f) * 50f, 4f); }

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

        foreach (var b in new[] { mustafa, doha })
        {
            b.go = new GameObject(b.name + " bird");
            b.go.transform.SetParent(worldRoot, false);
            b.sr = b.go.AddComponent<SpriteRenderer>();
            b.sr.sprite = b.idle;
            b.sr.sortingOrder = b == doha ? 11 : 10;
            b.go.transform.localScale = Vector3.one * 0.26f;
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
        menuP = UiKit.Panel(ct, "Menu");
        hostP = UiKit.Panel(ct, "Host");
        joinP = UiKit.Panel(ct, "Join");
        hudP = UiKit.Panel(ct, "Hud");
        countP = UiKit.Panel(ct, "Countdown");
        resultP = UiKit.Panel(ct, "Result");

        BuildDeco();
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
        UiKit.Label(menuP, "Mustafa & Doha", 62, new Color(1f, 0.78f, 0.88f), new Vector2(0, 208), new Vector2(560, 80));
        UiKit.Pic(menuP, UiKit.HeartSprite, new Vector2(-285, 208), new Vector2(52, 52));
        UiKit.Pic(menuP, UiKit.HeartSprite, new Vector2(285, 208), new Vector2(52, 52));

        menuBirdA = UiKit.Pic(menuP, mustafa.idle, new Vector2(-130, 118), new Vector2(130, 130));
        menuBirdB = UiKit.Pic(menuP, doha.idle, new Vector2(130, 118), new Vector2(130, 130));
        UiKit.Label(menuP, "Mustafa", 30, new Color(0.75f, 1f, 0.55f), new Vector2(-130, 48), new Vector2(240, 38));
        UiKit.Label(menuP, "Doha", 30, new Color(1f, 0.62f, 0.82f), new Vector2(130, 48), new Vector2(240, 38));

        // Doha picks her look here.
        var circle = ProcArt.Circle(64, Color.white, new Color(0.16f, 0.05f, 0.27f), 4, 100f);
        swatchRing = UiKit.Pic(menuP, circle, SwatchPos(dohaStyle), new Vector2(50, 50), Color.white);
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
        }

        UiKit.Btn(menuP, "Create Room", Pink, new Vector2(0, -72), new Vector2(520, 82), StartHosting);
        UiKit.Btn(menuP, "Join Room", Purple, new Vector2(0, -164), new Vector2(520, 82), GoJoin);
        UiKit.Btn(menuP, "Play Solo", Teal, new Vector2(0, -256), new Vector2(520, 82), StartSolo);

        UiKit.Label(menuP, "Based on Flappy Bird Unity by Abhiraj (MIT)", 22, new Color(1, 1, 1, 0.7f),
            new Vector2(0, -332), new Vector2(800, 34), TextAnchor.MiddleCenter, false, true);
    }

    void BuildHost()
    {
        UiKit.Label(hostP, "Create Room", 80, Color.white, new Vector2(0, 285), new Vector2(800, 110));
        UiKit.Label(hostP, "Tell Doha this code:", 38, Cream, new Vector2(0, 195), new Vector2(800, 60), TextAnchor.MiddleCenter, false);
        hostCode = UiKit.Label(hostP, "----", 170, new Color(1f, 0.85f, 0.4f), new Vector2(0, 70), new Vector2(700, 190));
        hostLink = UiKit.Label(hostP, "", 24, Cream, new Vector2(0, -50), new Vector2(900, 60), TextAnchor.MiddleCenter, false);
        hostStatus = UiKit.Label(hostP, "Connecting...", 40, Color.white, new Vector2(0, -125), new Vector2(900, 70), TextAnchor.MiddleCenter, false);
        UiKit.Btn(hostP, "Share Invite", Pink, new Vector2(-150, -215), new Vector2(290, 80), ShareInvite, 36);
        UiKit.Btn(hostP, "Copy Link", Teal, new Vector2(150, -215), new Vector2(250, 80), CopyLink, 36);
        UiKit.Btn(hostP, "Back", Grey, new Vector2(0, -310), new Vector2(320, 72), ToMenu, 38);
    }

    void BuildJoin()
    {
        UiKit.Label(joinP, "Join Room", 80, Color.white, new Vector2(0, 300), new Vector2(800, 110));
        UiKit.Label(joinP, "Enter Mustafa's 4 digit code", 34, Cream, new Vector2(0, 232), new Vector2(900, 50), TextAnchor.MiddleCenter, false);
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
        hudLeft = UiKit.LabelAt(hudP, "", 34, new Color(0.7f, 1f, 0.5f), new Vector2(0f, 1f), new Vector2(190, -40), new Vector2(360, 52), TextAnchor.MiddleLeft);
        hudRight = UiKit.LabelAt(hudP, "", 34, new Color(1f, 0.6f, 0.8f), new Vector2(1f, 1f), new Vector2(-190, -40), new Vector2(360, 52), TextAnchor.MiddleRight);

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
        bool menuLike = s == State.Menu || s == State.Hosting || s == State.Joining;
        decoP.gameObject.SetActive(menuLike || s == State.Result);
        menuP.gameObject.SetActive(s == State.Menu);
        hostP.gameObject.SetActive(s == State.Hosting);
        joinP.gameObject.SetActive(s == State.Joining);
        hudP.gameObject.SetActive(s == State.Round);
        countP.gameObject.SetActive(false);
        resultP.gameObject.SetActive(s == State.Result);
        bool showWorld = s == State.Round || s == State.Result;
        foreach (var p in pipes) if (!showWorld) p.root.gameObject.SetActive(false);
        mustafa.go.SetActive(showWorld);
        doha.go.SetActive(showWorld && online);
        if (!showWorld && s == State.Menu) { }
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
        BeginRound(Random.Range(1, 1000000));
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
        b.sr.sprite = b.idle;
        b.go.transform.rotation = Quaternion.identity;
    }

    void BeginRound(int s)
    {
        seed = s;
        startAt = Time.unscaledTime + CountdownSecs;
        if (online) ApplyDohaStyle(dohaStyle, true); // build all of her frames before the round starts
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
        hudLeft.text = online ? (iAmHost ? "Mustafa (you)" : "Mustafa") : "";
        hudRight.text = online ? (iAmHost ? "Doha" : "Doha (you)") : "";
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
            PeerNet.Send("S|" + s.ToString(CultureInfo.InvariantCulture));
            BeginRound(s);
        }
        else
        {
            BeginRound(Random.Range(1, 1000000));
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
        menuBirdA.rectTransform.anchoredPosition = new Vector2(-130, 118 + Mathf.Sin(t * 2.2f) * 10f);
        menuBirdB.rectTransform.anchoredPosition = new Vector2(130, 118 + Mathf.Sin(t * 2.2f + 1.6f) * 10f);
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
                        if (p.Length > 2) dohaStyle = Mathf.Clamp(I(p[2]), 0, ProcArt.Styles.Length - 1); // Doha's chosen look
                        int s = Random.Range(1, 1000000);
                        PeerNet.Send("S|" + s.ToString(CultureInfo.InvariantCulture));
                        BeginRound(s);
                    }
                    break;
                case "S": // host starts a round
                    if (!iAmHost) BeginRound(I(p[1]));
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
                Toast("Network problem (" + PeerNet.Error + ")");
                ToMenu();
                break;
            case PeerNet.Status.Hosting:
                hostStatus.text = "Waiting for Doha" + Dots();
                break;
            case PeerNet.Status.Reconnecting:
                // e.g. you switched to WhatsApp: keep the room, just re-register it
                hostStatus.text = "Reconnecting your room" + Dots();
                break;
            case PeerNet.Status.Connected:
                hostStatus.text = "Doha is here! Starting...";
                break;
            case PeerNet.Status.Closed:
                hostStatus.text = "Doha left. Waiting" + Dots();
                break;
            default:
                hostStatus.text = "Connecting to the server" + Dots();
                break;
        }
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
                if (!helloSent) { PeerNet.Send("H|Doha|" + dohaStyle.ToString(CultureInfo.InvariantCulture)); helloSent = true; }
                joinStatus.text = "Connected! Waiting for Mustafa to start" + Dots();
                break;
            case PeerNet.Status.NotFound:
                Toast("Room not found. Check the code");
                joinWaiting = false;
                PeerNet.Close();
                joinStatus.text = "";
                break;
            case PeerNet.Status.Error:
                Toast("Network problem (" + PeerNet.Error + ")");
                joinWaiting = false;
                PeerNet.Close();
                joinStatus.text = "";
                break;
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
            }

            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + SendInterval;
                PeerNet.Send("P|" + seed.ToString(CultureInfo.InvariantCulture) + "|" + me.y.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                             me.vy.ToString("F1", CultureInfo.InvariantCulture) + "|" + me.score + "|" + (me.alive ? "1" : "0"));
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
        me.score = ScoreAt(Dist(tt));
        if (me.y < FloorY || HitsPipe(tt, me.y)) Die(me, tt);
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

        RenderBird(mustafa, -0.30f, t);
        if (online) RenderBird(doha, 0.30f, t);
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
    }

    // Name tag above (Mustafa) / below (Doha) a bird, so the two never overlap.
    void PlaceName(Text label, Bird b, float dy)
    {
        bool show = b.go.activeSelf && b.alive && (b != doha || online);
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
            Bird left = mustafa, right = doha;
            hudLeft.text = "Mustafa" + (left == me ? " (you)" : "") + "  " + left.score;
            hudRight.text = right.score + "  Doha" + (right == me ? " (you)" : "");
        }
        else
        {
            hudLeft.text = "";
            hudRight.text = "";
        }

        PlaceName(nameMustafa, mustafa, 0.95f);
        PlaceName(nameDoha, doha, -0.95f);

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
            resultLine.text = "Mustafa: " + mustafa.score + "      Doha: " + doha.score;
            if (otherLeft) resultLine.text += "\n" + other.name + " left the game";
        }
        else
        {
            int best = PlayerPrefs.GetInt("ofg_best", 0);
            if (me.score > best) { best = me.score; PlayerPrefs.SetInt("ofg_best", best); PlayerPrefs.Save(); }
            resultTitle.text = "Game over";
            resultLine.text = "Score: " + me.score + "      Best: " + best;
        }

        bool canRestart = !online || iAmHost;
        playAgainBtn.SetActive(canRestart);
        resultHint.text = canRestart ? "" : "Waiting for Mustafa to start another round...";
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
