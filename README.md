<div align="center">

# Paper.io 2 Clone — 3D Multiplayer

**Authoritative Rust server · Unity URP client · Protobuf/WebSocket networking**

A fully server-authoritative multiplayer territory-capture game inspired by [Voodoo](https://www.voodoo.io/)'s Paper.io 2.  
Custom Rust game server + Unity 3D client with real-time state sync, procedural rendering, flood-fill territory claiming, and client-side prediction.

[**▶ Play in Browser**](https://wallerthedeveloper.itch.io/paperio-clone) · [**Server Repository**](https://github.com/WallerTheDeveloper/rust-server/tree/game/paperio) · [**Report Bug**](https://github.com/WallerTheDeveloper/paperio-clone/issues)

<br>

[![Unity](https://img.shields.io/badge/Unity-6-000000?logo=unity&logoColor=white)](#)
[![Rust](https://img.shields.io/badge/Rust-Tokio-f74c00?logo=rust&logoColor=white)](#)
[![Protobuf](https://img.shields.io/badge/Protocol-Protobuf-4285F4?logo=google&logoColor=white)](#)
[![WebSocket](https://img.shields.io/badge/Transport-WebSocket%20%2B%20UDP-blue)](#)
[![License](https://img.shields.io/badge/License-Portfolio-yellow)](#disclaimer)

</div>

---

> ** Portfolio Project** — Built as a technical showcase for a Senior Game Developer application at Voodoo, the original creators of Paper.io. All game mechanics are reimplemented from scratch; no original code or assets from Voodoo are used.

---

## What is this?

A 3D multiplayer Paper.io 2 clone — a territory-capture game where players navigate a grid, leave colored trails, and claim land by circling back to their territory. Every enclosed region becomes yours, but your trail is your vulnerability: if anyone crosses it before you close the loop, you're eliminated.

**Desktop:** WASD or Arrow Keys &nbsp;·&nbsp; **Mobile:** Swipe to move

## Core Features

| | Feature | Description |
|---|---------|-------------|
| ◈ | **Territory Claiming** | Draw trails outside your zone, return home to claim everything enclosed. Edge-based flood-fill determines captured regions — including enemy territory |
| ⚡ | **Trail Collisions** | Cross any player's trail (or your own) and they're eliminated. Head-on collisions resolve by score. Four elimination types handled server-side |
| ↻ | **Respawn System** | Eliminated players respawn after a configurable delay with brief invulnerability. Safe-distance spawn algorithm |
| ▦ | **3D Rendering** | Procedural mesh generation for territory and trails. Vertex-colored grid, glowing emissive trails, smooth interpolation between server ticks |
| ⟐ | **Client Prediction** | Input applied locally on the frame it's pressed. Pending inputs buffered with tick IDs. Server reconciliation replays unacknowledged inputs |
| △ | **Delta Compression** | Only changed cells transmitted per tick. Full state on join + periodic keyframes. RLE encoding for territory rows |
| 🧭 | **Minimap & UI** | Dedicated minimap camera with per-player indicators, score percentage, leaderboard, territory claim popups |
| 🔌 | **Dual Transport** | UDP for native builds, WebSocket for WebGL — unified routing through virtual socket addresses |

## Architecture

```
┌─────────────────┐         Protobuf / WS          ┌──────────────────┐
│                  │◄──────────────────────────────► │                  │
│   Unity Client   │         ~50ms ticks             │   Rust Server    │
│                  │◄──────────────────────────────► │                  │
└─────────────────┘                                  └──────────────────┘
  Input capture                                        Session management
  Client-side prediction                               Room lifecycle
  Position interpolation                               Game trait interface
  Procedural mesh rendering                            Movement simulation
  Trail tube geometry                                  Collision detection
  Object-pooled visuals                                Flood-fill claiming
  Camera + minimap                                     Delta state broadcast
```

The client **never** determines game outcomes. The server owns simulation, collision, and territory state. The client handles input, rendering, and prediction.

### Server (Rust)

| System | Details |
|--------|---------|
| **Tick Loop** | 20 Hz (50ms). Processes queued inputs, advances movement, runs collision, broadcasts state |
| **Movement** | Grid-based with configurable move interval. Direction changes apply next tick. Boundary kills |
| **Trail System** | Positions tracked when player is outside own territory. Claim triggered on return home |
| **Territory Claiming** | Trail cells converted to territory → BFS from all map edges → unreachable cells = enclosed = claimed |
| **Collision** | Trail-cut (another player crosses your trail), self-intersection, head-on (score-based resolution), boundary |
| **State Sync** | Keyframe/delta pattern. Full `PaperioState` at intervals, `PaperioDelta` with only changes in between. Territory rows use RLE |
| **Sessions** | Reconnect tokens, sequence numbering, packet loss detection, timeout with auto-cleanup |
| **Game Trait** | Generic `Game` trait — networking code is game-agnostic. Adding a new game = implementing one trait |

### Client (Unity)

| System | Details |
|--------|---------|
| **Prediction** | `ClientPrediction.cs` — records pending inputs with estimated tick, replays on correction. Stats tracking for debugging |
| **Reconciliation** | On `PaperioState` arrival: snap to server position, discard acknowledged inputs, replay remaining |
| **Territory Renderer** | Procedural mesh with vertex colors per cell. Updates incrementally on delta arrival |
| **Trail Renderer** | 3D tube geometry with rounded corners and emissive glow material. Per-player tracking with object pooling |
| **Player Visuals** | Spawn/despawn with pooling. MaterialPropertyBlock for per-instance colors. Name labels track camera |
| **Interpolation** | Smooth movement between grid positions over tick duration for all remote players |
| **Service Container** | Lightweight DI: `IService` vs `ITickableService` split. Bootstrap registers, subsystems resolve by interface |

## Tech Stack

| Layer | Technology |
|-------|------------|
| **Server** | Rust, Tokio (async runtime), Prost (protobuf), tokio-tungstenite (WebSocket) |
| **Client** | Unity 6 with URP, New Input System, Google.Protobuf NuGet |
| **Protocol** | Protobuf over WebSocket (WebGL) / UDP (native). Sequence-numbered packets |
| **Rendering** | Procedural mesh generation, vertex colors, custom shaders (ShaderLab/HLSL) |

## Key Design Decisions

**Game Trait Abstraction** — Generic networking modules (`network/`, `session/`, `room/`) never import game-specific code. All communication goes through the `Game` trait interface. This means adding a second game to the same server infrastructure requires implementing one trait, not rewriting transport.

**Flat Array Territory** — The territory grid is stored as `Vec<Option<PlayerId>>` indexed by `y * width + x`. This gives O(1) cell lookup, cache-friendly iteration, and trivial snapshot diffing for delta compression. No HashMap overhead on the hot path.

**Edge Flood-Fill** — Instead of tracing enclosed regions inward (which is fragile with complex shapes), the claiming algorithm seeds a BFS from every map edge cell that isn't owned by the player. Anything the BFS can't reach from the edges is enclosed and gets claimed. Simple, correct, handles arbitrary trail shapes.

**Virtual Socket Addresses** — WebSocket clients receive virtual `127.255.x.x` addresses so the entire session/room/game pipeline treats them identically to UDP clients. Think of it like adding a second entrance to the same building while keeping all the internal hallways identical.

**Keyframe + Delta Pattern** — Full state is sent periodically as a baseline. Between keyframes, only territory cell changes and modified player data are transmitted. Territory rows use RLE encoding. A 3-cell territory change produces a delta that is orders of magnitude smaller than the 100×100 full state.

**Service Container DI** — Lightweight dependency injection on the Unity side. Services register at bootstrap and resolve by type. `IService` vs `ITickableService` split ensures non-ticking services don't participate in the frame loop.

## Getting Started

### Prerequisites

- **Unity 6** (URP project)
- **Rust** (stable toolchain) — for the server
- **Protoc** — Protocol Buffers compiler

### Running the Server

```bash
# Clone the server repo
git clone https://github.com/WallerTheDeveloper/paperio-server.git
cd paperio-server

# Build and run
cargo run --bin paperio_server
```

The server starts a WebSocket listener (for WebGL clients) and a UDP socket (for native clients).

### Running the Client

```bash
# Clone this repo
git clone https://github.com/WallerTheDeveloper/paperio-clone.git

# Open in Unity 6
# Set the server address in the connection config
# Build for WebGL or run in Editor
```

For WebGL builds, the client automatically uses the WebSocket transport. For Editor/standalone, it uses UDP.

## Disclaimer

This is a **portfolio / technical showcase project**, not a commercial release. Built to demonstrate multiplayer game architecture, server-authoritative design, and production-level Unity development.

**Original game credit:** Paper.io and Paper.io 2 are created by [Voodoo](https://www.voodoo.io/). This project is an independent clone built for educational and portfolio purposes. All game mechanics are reimplemented from scratch — no original code or assets are used.

## Author

**Danylo Golosov** — Software Engineer / Game Developer, Berlin

📧 golo7ov.danil@gmail.com · [itch.io](https://wallerthedeveloper.itch.io/paperio-clone)
