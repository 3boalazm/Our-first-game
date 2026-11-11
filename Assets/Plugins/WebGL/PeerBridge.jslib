// Browser-side half of the online 2-player link.
//
// The two players talk through a tiny relay (a Cloudflare Worker, see /relay): both open a
// WebSocket to  <relay>/room/<4 digit code>?role=host|guest&key=<random>  and the relay forwards
// every text message to the other side. There is nothing to register, so a room survives the
// page being sent to the background (e.g. while you share the code on WhatsApp): the socket
// simply reconnects with the same key and takes its place back.
//
// C# polls this plugin every frame (no callbacks into WASM are needed).
//
// Status codes (mirrored in PeerNet.cs):
// 0 idle, 1 loading, 2 hosting (waiting for friend), 3 connecting, 4 connected,
// 5 closed (the other player left), 6 error, 7 room code already taken, 8 room not found / full,
// 9 reconnecting (connection to the relay lost, retrying)
//
// Relay control messages: "R" host registered, "J" a guest is in the room, "A" guest accepted,
// "L" the other player left. Anything else is a game message and is queued for C#.

var PeerBridgePlugin = {

    $pb: {
        ws: null,
        queue: [],
        status: 0,
        error: "",
        role: "",
        code: "",
        key: "",
        everOpen: false,
        tries: 0,
        closedByUs: false,
        retryTimer: null,
        pingTimer: null,
        openTimer: null,
        watching: false,

        base: function () {
            var o = "";
            try {
                // local testing only: ?relay=ws://127.0.0.1:8787
                var q = new URLSearchParams(window.location.search).get("relay");
                if (q && /^ws:\/\/(127\.0\.0\.1|localhost)(:\d+)?$/.test(q)) o = q;
            } catch (e) {}
            if (!o) o = window.OFG_RELAY || "";
            // nothing configured: the game is served by the relay itself, use the same address
            if (!o) o = (window.location.protocol === "https:" ? "wss://" : "ws://") + window.location.host;
            return String(o).replace(/\/+$/, "");
        },

        str: function (s) {
            var len = lengthBytesUTF8(s) + 1;
            var buf = _malloc(len);
            stringToUTF8(s, buf, len);
            return buf;
        },

        stopTimers: function () {
            if (pb.retryTimer) { clearTimeout(pb.retryTimer); pb.retryTimer = null; }
            if (pb.openTimer) { clearTimeout(pb.openTimer); pb.openTimer = null; }
            if (pb.pingTimer) { clearInterval(pb.pingTimer); pb.pingTimer = null; }
        },

        connect: function () {
            var base = pb.base();
            if (!base) { pb.error = "norelay"; pb.status = 6; return; }
            var url = base + "/room/" + pb.code + "?role=" + pb.role + "&key=" + pb.key;
            var ws;
            try { ws = new WebSocket(url); } catch (e) { pb.error = "socket"; pb.scheduleRetry(); return; }
            pb.ws = ws;

            // give up on a socket that never opens and try again
            pb.openTimer = setTimeout(function () {
                if (pb.ws === ws && ws.readyState !== 1) { try { ws.close(); } catch (e) {} }
            }, 8000);

            ws.onopen = function () {
                if (pb.ws !== ws) return;
                if (pb.openTimer) { clearTimeout(pb.openTimer); pb.openTimer = null; }
                if (pb.pingTimer) clearInterval(pb.pingTimer);
                pb.pingTimer = setInterval(function () {
                    if (pb.ws === ws && ws.readyState === 1) { try { ws.send("p"); } catch (e) {} }
                }, 20000);
            };

            ws.onmessage = function (e) {
                if (pb.ws !== ws) return;
                var m = String(e.data);
                if (pb.role === "host") {
                    if (m === "R") { pb.tries = 0; pb.error = ""; pb.everOpen = true; if (pb.status !== 4) pb.status = 2; return; }
                    if (m === "J") { pb.status = 4; return; }
                } else if (m === "A") {
                    pb.tries = 0; pb.error = ""; pb.everOpen = true; pb.status = 4; return;
                }
                if (m === "L") { pb.status = 5; pb.queue.push("L"); return; }
                pb.queue.push(m);
            };

            ws.onerror = function () { if (pb.ws === ws) pb.error = "network"; };

            ws.onclose = function (ev) {
                if (pb.ws !== ws) return;
                pb.ws = null;
                if (pb.openTimer) { clearTimeout(pb.openTimer); pb.openTimer = null; }
                if (pb.pingTimer) { clearInterval(pb.pingTimer); pb.pingTimer = null; }
                if (pb.closedByUs) return;
                var c = ev.code;
                if (c === 4001) { pb.error = "taken"; pb.status = 7; return; }
                if (c === 4003) { pb.error = "full"; pb.status = 8; return; }
                if (c === 4004) { pb.error = "notfound"; pb.status = 8; return; }
                if (c === 4002) return; // replaced by our own newer connection
                // lost connection (page in the background, bad signal...): keep the room and retry
                if (pb.status === 1 || pb.status === 2 || pb.status === 9) pb.status = 9;
                pb.scheduleRetry();
            };
        },

        scheduleRetry: function (immediate) {
            if (pb.closedByUs || pb.retryTimer || !pb.role) return;
            if (!pb.everOpen && pb.tries >= 4) { if (!pb.error) pb.error = "network"; pb.status = 6; return; }
            if (pb.tries >= 40) { if (!pb.error) pb.error = "network"; pb.status = 6; return; }
            var wait = immediate ? 50 : Math.min(8000, 1000 * Math.pow(2, pb.tries));
            pb.retryTimer = setTimeout(function () {
                pb.retryTimer = null;
                if (pb.closedByUs || !pb.role) return;
                if (pb.ws && pb.ws.readyState <= 1) return; // already (re)connecting or open
                pb.tries++;
                pb.connect();
            }, wait);
        },

        watchVisibility: function () {
            if (pb.watching) return;
            pb.watching = true;
            var kick = function () {
                if (!pb.role || pb.closedByUs) return;
                if (pb.ws && pb.ws.readyState <= 1) return;
                if (pb.status === 6) return; // gave up: the player has to press Try Again
                if (pb.retryTimer) { clearTimeout(pb.retryTimer); pb.retryTimer = null; }
                pb.tries = 0;
                pb.scheduleRetry(true);
            };
            document.addEventListener("visibilitychange", function () { if (!document.hidden) kick(); });
            window.addEventListener("online", kick);
            window.addEventListener("pageshow", kick);
        },

        shutdown: function () {
            pb.closedByUs = true;
            pb.stopTimers();
            try { if (pb.ws) pb.ws.close(1000); } catch (e) {}
            pb.ws = null;
            pb.role = "";
            pb.code = "";
            pb.queue = [];
            pb.status = 0;
            pb.error = "";
            pb.everOpen = false;
            pb.tries = 0;
        }
    },

    // isHost: 1 = create room `code`, 0 = join room `code`.  (prefix is unused with the relay)
    PB_Start: function (prefixPtr, isHost, codePtr) {
        var code = UTF8ToString(codePtr);
        pb.shutdown();
        pb.closedByUs = false;
        pb.role = isHost ? "host" : "guest";
        pb.code = code;
        pb.key = Math.random().toString(36).slice(2, 12) + Math.random().toString(36).slice(2, 12);
        pb.status = isHost ? 1 : 3;
        pb.watchVisibility();
        console.log("[relay] start", pb.role, code);
        pb.connect();
    },

    PB_GetStatus: function () { return pb.status; },

    PB_GetError: function () { return pb.str(pb.error || ""); },

    PB_Send: function (msgPtr) {
        if (pb.ws && pb.ws.readyState === 1) {
            try { pb.ws.send(UTF8ToString(msgPtr)); } catch (e) {}
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
