# Our First Game

A two-bird flappy race for **Mustafa & Doha** — playable in the browser on phone or PC.

- **Mustafa** is the green bird, **Doha** is the pink bird with the bow and eyelashes.
- **Play online together:** one of you creates a room and gets a 4 digit code (and an invite link),
  the other joins with the code. You both fly through the *same* pipes at the same time.
  Whoever crashes first keeps watching the other one fly; when both are down you see who won.
- **Play solo** if nobody is around.
- **Controls:** tap / click anywhere, or press Space / Up arrow.

## How the online mode works

There is no game server. The two browsers connect directly to each other (WebRTC, through
[PeerJS](https://peerjs.com)'s free public signalling server). Both players run the same
deterministic pipe course from a shared seed and clock, and only send their own bird's
position and score ~15 times a second. Because of that:

- it is free to host (static files only, e.g. Vercel),
- the 4 digit room code only has to be typed once,
- if both players are behind very strict networks the direct connection may fail; a public
  TURN relay is configured as a fallback, but it is a shared free service and can be slow.

Scores are computed on each player's device, so this is meant for friends, not competitions.

## Project layout

| Path | What it is |
|---|---|
| `Assets/Scripts/OurFirstGame/OurGame.cs` | the whole game: world, rules, UI flow, online sync |
| `Assets/Scripts/OurFirstGame/PeerNet.cs` + `Assets/Plugins/WebGL/PeerBridge.jslib` | WebRTC link (C# wrapper + browser side) |
| `Assets/Scripts/OurFirstGame/ProcArt.cs` | procedural art: hearts, buttons, and the pink Doha bird (recoloured from the green one) |
| `Assets/Scripts/OurFirstGame/UiKit.cs` | small helpers to build the UI from code |
| `Assets/Editor/OurBuild.cs` | menu **Our First Game → Build WebGL** (also usable from the command line) |
| `Assets/WebGLTemplates/OurFirstGame/` | the web page that wraps the game (no Unity logo) |

## Build & publish

Requires **Unity 6000.3.10f1** with the **WebGL Build Support** module.

1. Open the project in Unity and run the menu **Our First Game → Build WebGL**
   (it creates the scene, applies the settings and builds into `WebBuild/`).
2. Upload the contents of `WebBuild/` to any static host, e.g. `npx vercel deploy` from inside it.

Command line:

```bash
Unity -batchmode -nographics -quit -projectPath . -executeMethod OurBuild.BuildWebGL -logFile build.log
```

## Credits

Started from [abhie7/flappy-bird-unity](https://github.com/abhie7/flappy-bird-unity)
by Abhiraj Chaudhuri (MIT License, see `LICENSE`) — the bird and background artwork and the font come
from that project. Online play uses [PeerJS](https://peerjs.com) (MIT), loaded from a CDN at runtime.
