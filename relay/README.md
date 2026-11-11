# Our First Game - hosting on Cloudflare

One Cloudflare Worker (free plan) does two jobs:

1. **serves the game** - the WebGL build and the PWA files (manifest, service worker, icons) from `./public`,
2. **is the online room relay** - a tiny WebSocket room server (Durable Objects). The two players connect to
   `wss://<this worker>/room/<4 digit code>?role=host|guest&key=<random>` and the relay forwards every
   message to the other player.

Because the game and the relay share one address, nothing has to be configured in the game.

Live address: **https://ourfirstgame.doha-mustafa.workers.dev**

## Publish

```bash
# 1. build the game in Unity: menu "Our First Game > Build WebGL"  (output: ../WebBuild)
# 2. copy the build next to the worker and deploy
cd relay
node deploy.mjs         # copies ../WebBuild to ./public, then runs: npx wrangler deploy
```

The first time, `npx wrangler login` opens the browser to sign in to Cloudflare. Wrangler prints the address
(`https://ourfirstgame.<your-subdomain>.workers.dev`). That address is the game: open it on a phone and use
"Install app" / "Add to Home Screen".

## Tests

```bash
npx wrangler dev                      # runs everything locally on :8787
node test.mjs ws://127.0.0.1:8787     # relay behaviour tests
```
