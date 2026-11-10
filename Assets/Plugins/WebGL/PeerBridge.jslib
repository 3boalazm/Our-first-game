// Browser-side half of the online 2-player link (WebRTC via PeerJS).
// C# cannot talk WebRTC directly, so this plugin owns the Peer/connection objects
// and C# polls it every frame (no callbacks into WASM are needed).
//
// Status codes (mirrored in PeerNet.cs):
// 0 idle, 1 loading, 2 hosting (waiting for friend), 3 connecting, 4 connected,
// 5 closed, 6 error, 7 room code already taken, 8 room not found

var PeerBridgePlugin = {

    $pb: {
        peer: null,
        conn: null,
        queue: [],
        status: 0,
        error: "",
        timer: null,

        ice: [
            { urls: "stun:stun.l.google.com:19302" },
            { urls: "stun:stun1.l.google.com:19302" },
            // Public relay, only used when a direct connection is impossible (strict NAT).
            { urls: "turn:openrelay.metered.ca:80", username: "openrelayproject", credential: "openrelayproject" },
            { urls: "turn:openrelay.metered.ca:443", username: "openrelayproject", credential: "openrelayproject" },
            { urls: "turn:openrelay.metered.ca:443?transport=tcp", username: "openrelayproject", credential: "openrelayproject" }
        ],

        load: function (cb) {
            if (window.Peer) { cb(true); return; }
            var urls = [
                "TemplateData/peerjs.min.js", // shipped with the game
                "https://unpkg.com/peerjs@1.5.4/dist/peerjs.min.js",
                "https://cdn.jsdelivr.net/npm/peerjs@1.5.4/dist/peerjs.min.js"
            ];
            var tryNext = function (i) {
                if (i >= urls.length) { cb(false); return; }
                var s = document.createElement("script");
                s.src = urls[i];
                s.onload = function () { cb(!!window.Peer); };
                s.onerror = function () { tryNext(i + 1); };
                document.head.appendChild(s);
            };
            tryNext(0);
        },

        attach: function (c) {
            c.on("open", function () {
                if (pb.timer) { clearTimeout(pb.timer); pb.timer = null; }
                pb.conn = c;
                pb.status = 4;
            });
            c.on("data", function (d) {
                pb.queue.push(typeof d === "string" ? d : String(d));
            });
            c.on("close", function () {
                if (pb.conn === c) { pb.conn = null; pb.status = 5; }
            });
            c.on("error", function (e) {
                pb.error = (e && e.type) ? e.type : "connection";
                pb.status = 6;
            });
        },

        // ---- host side: survive the page being backgrounded (e.g. switching to WhatsApp) ----
        role: "",
        hostId: "",
        hostTries: 0,
        everOpen: false,
        retryTimer: null,
        opts: null,
        watching: false,

        makeHostPeer: function () {
            var p = new Peer(pb.hostId, pb.opts);
            pb.peer = p;
            p.on("open", function () {
                console.log("[pb] host open", pb.hostId);
                if (pb.peer !== p) return;
                if (pb.retryTimer) { clearTimeout(pb.retryTimer); pb.retryTimer = null; }
                pb.hostTries = 0;
                pb.everOpen = true;
                pb.error = "";
                if (pb.status !== 4) pb.status = 2; // keep "connected" if the friend is already here
            });
            p.on("connection", function (c) {
                if (pb.peer !== p) return;
                if (pb.conn) { c.on("open", function () { c.close(); }); return; } // only 2 players
                pb.attach(c);
            });
            p.on("disconnected", function () {
                console.log("[pb] host disconnected");
                if (pb.peer !== p) return;
                if (pb.status === 2 || pb.status === 1) pb.status = 9;
                pb.scheduleHostRetry(false, 300);
            });
            p.on("close", function () {
                console.log("[pb] host peer closed");
                if (pb.peer !== p) return;
                if (pb.status === 2 || pb.status === 1) pb.status = 9;
                pb.scheduleHostRetry(true, 300);
            });
            p.on("error", function (e) {
                console.log("[pb] host error", e && e.type);
                if (pb.peer !== p) return;
                pb.error = (e && e.type) ? e.type : "error";
                if (e.type === "unavailable-id") {
                    // first attempt: the code is really taken. Later: the server still holds our old session.
                    if (!pb.everOpen && pb.hostTries === 0) { pb.status = 7; return; }
                    if (pb.status === 2) pb.status = 9;
                    pb.scheduleHostRetry(true, 2000);
                    return;
                }
                if (e.type === "network" || e.type === "server-error" || e.type === "socket-error" || e.type === "socket-closed") {
                    if (pb.status === 2 || pb.status === 1) pb.status = 9;
                    pb.scheduleHostRetry(false, 1500);
                    return;
                }
                pb.status = 6;
            });
        },

        // Retries use exponential back-off: the free public signalling server rate-limits (HTTP 429)
        // clients that reconnect too fast, so we must never hammer it.
        scheduleHostRetry: function (recreate, delay, force) {
            if (pb.role !== "host" || pb.retryTimer) return;
            if (pb.conn && pb.conn.open) return; // friend is connected: the server link is not needed any more
            var wait = force ? (delay || 50) : Math.max(delay || 0, Math.min(30000, 2000 * Math.pow(2, pb.hostTries)));
            pb.retryTimer = setTimeout(function () {
                pb.retryTimer = null;
                if (pb.role !== "host" || (pb.conn && pb.conn.open)) return;
                var p = pb.peer;
                // already healthy again (the reconnect worked): nothing to do
                if (p && p.open && !p.disconnected && !p.destroyed) {
                    if (pb.status === 9) pb.status = 2;
                    return;
                }
                pb.hostTries++;
                console.log("[pb] host retry", pb.hostTries, recreate ? "recreate" : "reconnect");
                if (pb.hostTries > 12) { if (!pb.error) pb.error = "network"; pb.status = 6; return; }
                try {
                    if (!recreate && p && p.disconnected && !p.destroyed) {
                        p.reconnect();
                        pb.scheduleHostRetry(true, 8000); // if still not open by then, start over
                    } else {
                        pb.peer = null;                    // so the old peer's "close" event is ignored
                        if (p) { try { p.destroy(); } catch (e) {} }
                        pb.makeHostPeer();
                    }
                } catch (ex) {
                    pb.scheduleHostRetry(true, 8000);
                }
            }, wait);
        },

        watchVisibility: function () {
            if (pb.watching) return;
            pb.watching = true;
            var kick = function () {
                if (pb.role !== "host" || (pb.conn && pb.conn.open)) return;
                var p = pb.peer;
                if (!p || p.destroyed || p.disconnected) {
                    if (pb.retryTimer) { clearTimeout(pb.retryTimer); pb.retryTimer = null; }
                    pb.scheduleHostRetry(!p || p.destroyed, 50, true);
                }
            };
            document.addEventListener("visibilitychange", function () { if (!document.hidden) kick(); });
            window.addEventListener("online", kick);
            window.addEventListener("pageshow", kick);
        },

        shutdown: function () {
            if (pb.timer) { clearTimeout(pb.timer); pb.timer = null; }
            if (pb.retryTimer) { clearTimeout(pb.retryTimer); pb.retryTimer = null; }
            pb.role = "";
            pb.hostTries = 0;
            pb.everOpen = false;
            try { if (pb.conn) pb.conn.close(); } catch (e) {}
            try { if (pb.peer) pb.peer.destroy(); } catch (e) {}
            pb.conn = null;
            pb.peer = null;
            pb.queue = [];
            pb.status = 0;
            pb.error = "";
        },

        str: function (s) {
            var len = lengthBytesUTF8(s) + 1;
            var buf = _malloc(len);
            stringToUTF8(s, buf, len);
            return buf;
        }
    },

    // isHost: 1 = create room "prefix+code", 0 = join room "prefix+code"
    PB_Start: function (prefixPtr, isHost, codePtr) {
        var prefix = UTF8ToString(prefixPtr);
        var code = UTF8ToString(codePtr);
        console.log("[pb] start", isHost ? "host" : "join", code);
        pb.shutdown();
        pb.status = 1;

        pb.load(function (ok) {
            if (!ok) { pb.error = "load"; pb.status = 6; return; }
            var opts = { config: { iceServers: pb.ice }, debug: 0 };
            pb.opts = opts;
            try {
                if (isHost) {
                    pb.role = "host";
                    pb.hostId = prefix + code;
                    pb.hostTries = 0;
                    pb.everOpen = false;
                    pb.watchVisibility();
                    pb.makeHostPeer();
                } else {
                    pb.peer = new Peer(undefined, opts);
                    pb.peer.on("open", function () {
                        pb.status = 3;
                        var c = pb.peer.connect(prefix + code, { reliable: true });
                        pb.attach(c);
                        pb.timer = setTimeout(function () {
                            if (pb.status === 3) { pb.error = "timeout"; pb.status = 8; }
                        }, 15000);
                    });
                    pb.peer.on("error", function (e) {
                        pb.error = e.type;
                        pb.status = (e.type === "peer-unavailable") ? 8 : 6;
                    });
                }
            } catch (ex) {
                pb.error = String(ex);
                pb.status = 6;
            }
        });
    },

    PB_GetStatus: function () { return pb.status; },

    PB_GetError: function () { return pb.str(pb.error || ""); },

    PB_Send: function (msgPtr) {
        if (pb.conn && pb.conn.open) {
            try { pb.conn.send(UTF8ToString(msgPtr)); } catch (e) {}
        }
    },

    // Returns the next received message, or an empty string when there is none.
    PB_Poll: function () {
        return pb.str(pb.queue.length ? pb.queue.shift() : "");
    },

    PB_Close: function () { pb.shutdown(); },

    PB_GetUrlParam: function (namePtr) {
        var s = "";
        try { s = new URLSearchParams(window.location.search).get(UTF8ToString(namePtr)) || ""; } catch (e) {}
        return pb.str(s);
    },

    PB_PageUrl: function () {
        return pb.str(window.location.origin + window.location.pathname);
    },

    // Opens the phone's native share sheet (WhatsApp etc.) on top of the game, without leaving the page.
    PB_Share: function (textPtr, urlPtr) {
        var text = UTF8ToString(textPtr);
        var url = UTF8ToString(urlPtr);
        try {
            if (navigator.share) {
                navigator.share({ title: "Our First Game", text: text, url: url }).catch(function () {});
                return 1;
            }
        } catch (e) {}
        return 0;
    },

    PB_CopyText: function (textPtr) {
        var text = UTF8ToString(textPtr);
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(text);
                return 1;
            }
        } catch (e) {}
        return 0;
    }
};

autoAddDeps(PeerBridgePlugin, "$pb");
mergeInto(LibraryManager.library, PeerBridgePlugin);
