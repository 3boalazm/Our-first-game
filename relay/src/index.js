// "Our First Game" - one Cloudflare Worker that does two jobs:
//   1. serves the game itself (the WebGL build copied into ./public, with the PWA files), and
//   2. is the room relay for online play: a tiny WebSocket room server (Durable Objects).
//
// One Durable Object per 4 digit room code. Exactly two sockets can be in a room:
//   role=host   the player who created the room
//   role=guest  the player who joined with the code
// Every text message from one side is forwarded unchanged to the other side.
//
// Control messages sent BY THE RELAY (single letters, never produced by the game itself):
//   "R"  to the host:  you are registered, the room is open
//   "J"  to the host:  a guest is in the room (also re-sent when the host reconnects)
//   "A"  to the guest: you are in the room
//   "L"  to either:    the other player left / lost connection
// Clients may send "p" as a keep-alive; it is swallowed.
//
// Close codes: 4001 room code already used by another host, 4003 room is full,
//              4004 room not found (no host), 4002 replaced by a newer connection of yours.
//
// Reconnecting: a client picks a random `key` and sends it on every connection. A new
// connection with the same key replaces the old one (e.g. a phone that was in the
// background), so a stale socket never locks you out of your own room.

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (url.pathname === "/health") {
      return new Response("Our First Game relay is running.\n", {
        headers: { "content-type": "text/plain", "access-control-allow-origin": "*" },
      });
    }

    const m = url.pathname.match(/^\/room\/(\d{4})$/);
    if (!m) {
      // everything that is not a room is the game itself (static files from ./public)
      if (env.ASSETS) return env.ASSETS.fetch(request);
      return new Response("Our First Game relay is running.\n", { headers: { "content-type": "text/plain" } });
    }

    if (request.headers.get("Upgrade") !== "websocket") {
      return new Response("Expected a WebSocket connection", { status: 426 });
    }

    const stub = env.ROOMS.get(env.ROOMS.idFromName("room-" + m[1]));
    return stub.fetch(request);
  },
};

export class Room {
  constructor(state, env) {
    this.state = state;
  }

  sockets(role) {
    return this.state.getWebSockets(role);
  }

  reject(server, code, reason) {
    server.accept();
    try { server.close(code, reason); } catch (e) { /* ignore */ }
  }

  async fetch(request) {
    const url = new URL(request.url);
    const role = url.searchParams.get("role");
    const key = (url.searchParams.get("key") || "").slice(0, 64);

    const pair = new WebSocketPair();
    const client = pair[0];
    const server = pair[1];

    if (role === "host") {
      const live = this.sockets("host");
      const storedKey = await this.state.storage.get("hostKey");
      if (live.length > 0 && storedKey !== key) {
        this.reject(server, 4001, "taken");
        return new Response(null, { status: 101, webSocket: client });
      }
      for (const old of live) {
        old.serializeAttachment({ replaced: true });
        try { old.close(4002, "replaced"); } catch (e) { /* ignore */ }
      }
      await this.state.storage.put("hostKey", key);
      this.state.acceptWebSocket(server, ["host"]);
      server.send("R");
      if (this.sockets("guest").length > 0) server.send("J");
    } else if (role === "guest") {
      const hosts = this.sockets("host");
      if (hosts.length === 0) {
        this.reject(server, 4004, "notfound");
        return new Response(null, { status: 101, webSocket: client });
      }
      const live = this.sockets("guest");
      const storedKey = await this.state.storage.get("guestKey");
      if (live.length > 0 && storedKey !== key) {
        this.reject(server, 4003, "full");
        return new Response(null, { status: 101, webSocket: client });
      }
      for (const old of live) {
        old.serializeAttachment({ replaced: true });
        try { old.close(4002, "replaced"); } catch (e) { /* ignore */ }
      }
      await this.state.storage.put("guestKey", key);
      this.state.acceptWebSocket(server, ["guest"]);
      server.send("A");
      for (const h of hosts) { try { h.send("J"); } catch (e) { /* ignore */ } }
    } else {
      return new Response("role must be host or guest", { status: 400 });
    }

    return new Response(null, { status: 101, webSocket: client });
  }

  async webSocketMessage(ws, message) {
    if (typeof message !== "string") return;     // text only
    if (message === "p") return;                 // keep-alive
    if (message.length > 2048) return;           // game messages are tiny
    const tags = this.state.getTags(ws);
    const other = this.sockets(tags[0] === "host" ? "guest" : "host");
    for (const o of other) {
      try { o.send(message); } catch (e) { /* ignore */ }
    }
  }

  peerGone(ws) {
    let att = null;
    try { att = ws.deserializeAttachment(); } catch (e) { /* ignore */ }
    if (att && att.replaced) return;             // we closed it ourselves, nobody left
    const tags = this.state.getTags(ws);
    if (!tags.length) return;
    const other = this.sockets(tags[0] === "host" ? "guest" : "host");
    for (const o of other) {
      try { o.send("L"); } catch (e) { /* ignore */ }
    }
  }

  async webSocketClose(ws, code, reason, wasClean) {
    this.peerGone(ws);
    try { ws.close(code, reason); } catch (e) { /* ignore */ }
  }

  async webSocketError(ws, error) {
    this.peerGone(ws);
  }
}
