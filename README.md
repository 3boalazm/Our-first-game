# Our First Game

A two-bird flappy race for **Doha & Mustafa** — playable in the browser on phone or PC.

- **Doha** is the pink bird with the bow and eyelashes (she picks her colour in the menu), **Mustafa** is the green bird.
- **Play online together:** one of you creates a room and gets a 4 digit code (and an invite link),
  the other joins with the code. You both fly through the *same* pipes at the same time.
  Whoever crashes first keeps watching the other one fly; when both are down you see who won.
- **Play solo** if nobody is around.
- **Levels:** Easy / Medium / Hard (wider pipes, more space between them and slower on Easy). The level the
  host picks is used for the whole room.
- **Stars:** golden stars float in the gaps and between the pipes. Every star is worth **+2 points**, and every
  **5 stars give you a shield** (a bubble around your bird) that saves you from one crash.
- **Controls:** tap / click anywhere, or press Space / Up arrow.

## How the online mode works

The two players connect through a tiny **room relay** (a Cloudflare Worker, see `relay/`). Both open a
WebSocket to the relay with the 4 digit code and the relay forwards every message to the other player, so it
works between any two networks (home wifi, mobile data...). Both players run the same deterministic pipe course
from a shared seed and clock, and only send their own bird's position and score ~15 times a second.

- the room survives the page going to the background (e.g. while you share the code on WhatsApp),
- the game and the relay are served by the same worker, so there is nothing to configure,
- scores are computed on each player's device, so this is meant for friends, not competitions.

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
