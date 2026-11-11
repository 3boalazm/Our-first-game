// Service worker for "Our First Game" (makes the game installable and playable offline).
//
// Strategy: network first, cache as the fallback. Online you always get the newest version of the
// game (Unity's file names never change between builds, so a cache-first strategy would keep serving
// an old build); offline you get the last version that was downloaded.
// Only files from our own origin are cached - the online room relay is never touched.

const CACHE = "our-first-game-v1";

self.addEventListener("install", (event) => {
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    (async () => {
      const names = await caches.keys();
      await Promise.all(names.filter((n) => n !== CACHE).map((n) => caches.delete(n)));
      await self.clients.claim();
    })()
  );
});

self.addEventListener("fetch", (event) => {
  const req = event.request;
  if (req.method !== "GET") return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;       // other sites: not ours to cache
  if (url.pathname.startsWith("/room/")) return;         // the relay is a WebSocket endpoint

  event.respondWith(
    (async () => {
      try {
        const res = await fetch(req);
        if (res && res.status === 200 && res.type === "basic") {
          const cache = await caches.open(CACHE);
          cache.put(req, res.clone());
        }
        return res;
      } catch (err) {
        const hit = await caches.match(req, { ignoreSearch: true });
        if (hit) return hit;
        if (req.mode === "navigate") {
          const page = (await caches.match("./index.html")) || (await caches.match("./"));
          if (page) return page;
        }
        throw err;
      }
    })()
  );
});

// The page tells us which game files to keep for offline play once the game has loaded.
self.addEventListener("message", (event) => {
  const data = event.data;
  if (!data || data.type !== "cache-urls" || !Array.isArray(data.urls)) return;
  event.waitUntil(
    (async () => {
      const cache = await caches.open(CACHE);
      for (const u of data.urls) {
        try {
          const url = new URL(u, self.location.href);
          if (url.origin !== self.location.origin) continue;
          const res = await fetch(url.href);
          if (res && res.status === 200) await cache.put(url.href, res);
        } catch (e) { /* offline or missing: ignore */ }
      }
    })()
  );
});
