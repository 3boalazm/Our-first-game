// This is the browser-side half of the WebSocket bridge.
// Unity WebGL compiles C# to WASM, but WASM can't create WebSockets
// directly. Instead, this JS plugin creates real browser WebSockets
// and calls back into C# via SendMessage / function pointers.
//
// Think of it like a translator at a border crossing: C# speaks one
// language, the browser speaks another, and this plugin translates
// between them.

var WebSocketPlugin = {

    // Storage for active WebSocket instances
    $webSocketState: {
        instances: {},
        nextId: 1,
        onOpenCallback: null,
        onCloseCallback: null,
        onErrorCallback: null,
        onMessageCallback: null,
    },

    // Initialize callbacks from C#
    WebSocket_Initialize: function(onOpenPtr, onClosePtr, onErrorPtr, onMessagePtr) {
        webSocketState.onOpenCallback = onOpenPtr;
        webSocketState.onCloseCallback = onClosePtr;
        webSocketState.onErrorCallback = onErrorPtr;
        webSocketState.onMessageCallback = onMessagePtr;
    },

    // Create and connect a WebSocket
    WebSocket_Connect: function(urlPtr) {
        var url = UTF8ToString(urlPtr);
        var id = webSocketState.nextId++;

        try {
            var ws = new WebSocket(url);
            ws.binaryType = "arraybuffer";

            ws.onopen = function() {
                if (webSocketState.onOpenCallback) {
                    {{{ makeDynCall('vi', 'webSocketState.onOpenCallback') }}}(id);
                }
            };

            ws.onclose = function(event) {
                if (webSocketState.onCloseCallback) {
                    {{{ makeDynCall('vii', 'webSocketState.onCloseCallback') }}}(id, event.code);
                }
                delete webSocketState.instances[id];
            };

            ws.onerror = function() {
                if (webSocketState.onErrorCallback) {
                    var msgPtr = stringToNewUTF8("WebSocket error");
                    {{{ makeDynCall('vii', 'webSocketState.onErrorCallback') }}}(id, msgPtr);
                    _free(msgPtr);
                }
            };

            ws.onmessage = function(event) {
                if (webSocketState.onMessageCallback && event.data instanceof ArrayBuffer) {
                    var data = new Uint8Array(event.data);
                    var len = data.length;
                    var buf = _malloc(len);
                    HEAPU8.set(data, buf);
                    {{{ makeDynCall('viii', 'webSocketState.onMessageCallback') }}}(id, buf, len);
                    _free(buf);
                }
            };

            webSocketState.instances[id] = ws;
            return id;

        } catch (e) {
            console.error("[WebSocketPlugin] Connect failed:", e);
            return -1;
        }
    },

    // Send binary data
    WebSocket_Send: function(id, bufferPtr, length) {
        var ws = webSocketState.instances[id];
        if (!ws || ws.readyState !== WebSocket.OPEN) {
            return 0; // false
        }

        try {
            var data = HEAPU8.subarray(bufferPtr, bufferPtr + length);
            ws.send(data);
            return 1; // true
        } catch (e) {
            console.error("[WebSocketPlugin] Send failed:", e);
            return 0;
        }
    },

    // Close the connection
    WebSocket_Close: function(id) {
        var ws = webSocketState.instances[id];
        if (ws) {
            ws.close();
            delete webSocketState.instances[id];
        }
    },

    // Get ready state
    WebSocket_GetState: function(id) {
        var ws = webSocketState.instances[id];
        if (!ws) return 3; // CLOSED
        return ws.readyState;
    },
};

autoAddDeps(WebSocketPlugin, '$webSocketState');
mergeInto(LibraryManager.library, WebSocketPlugin);
