# M4: Thai House Graybox + Krasue Ghost Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A graybox Thai stilt house (ใต้ถุน, upper rooms, yard with ศาลพระภูมิ, escape gate) built and NavMesh-baked by code, with a server-run Krasue ghost that wanders, hears thrown items, running and screams, warns for 1 s, chases, grabs and kills players, flees from a flashlight held on its face for 2 s, and never harms an unexposed Spy. Each client feels Fear effects (25/15/8 m) and Adrenaline from the synced ghost position.

**Architecture:**
- Ghost decisions are a pure Core state machine, `GhostBrain`, fed a `GhostSenses` snapshot each tick (TDD).
- Perception geometry (sight cone, flashlight cone, hearing radius) is pure Core math in `GhostPerception`.
- `GhostController` (Game, server only) gathers senses with Physics raycasts, drives a `NavMeshAgent` and syncs state with NetworkVariables.
- Clients read the ghost's position from `GhostRegistry` to drive `FearDirector`: post-processing, procedural audio layers, light flicker, and `PlayerMotor.GhostDistance` for Adrenaline.
- `PlayerLife` holds the server-written alive/grabbed/dead state. M4 respawns the dead after 5 s as a stopgap; M5 replaces this with the mini ghost.
- `HouseBuilder` builds the `House` scene, which becomes the default play scene. The `Sandbox` scene stays for player-smoke (`-playscene Sandbox`).

**Tech Stack:** NGO 2.13.3, AI Navigation 2.0.16 (`NavMeshSurface.BuildNavMesh`, built-in `NavMeshAgent`), URP 17.6 Volume (`Vignette`, `ChromaticAberration`), `AudioClip.Create` for procedural sounds (self-made, no license needed).

**Spec:** `docs/GDD.md` §6.1–6.3, §6.4 (Krasue), §9 (map); `docs/PLAN.md` M4.

## Global Constraints
- All earlier constraints (M0–M3) still apply. Builders are idempotent. Rerunning them produces no git diff.
- GDD numbers:
  - Alert warning 1 s before a chase, with a screech and the ghost stopping.
  - Grab 2 s, then death; afterwards 10 s Cooldown in which the ghost hunts nobody.
  - Lose the chase after the target is out of sight for 6 s.
  - Flashlight on the face for 2 s → the ghost flees.
  - Fear bands 25/15/8 m (existing `FearModel`); Adrenaline (existing `AdrenalineModel`).
  - The ghost never grabs an unexposed Spy.
- My tuning calls, as constants in `Core/Ghosts/GhostTuning.cs` (Krasue):
  - Speeds: wander 2.2, investigate 3.6, chase 6.0, cooldown 1.6, flee 5.0 m/s.
  - Chase speed must stay above plain run 5.6 and below run × 1.25 = 7.0.
  - Sight 14 m with a 120° FOV. Grab range 1.4 m. Hearing × 1.5 (Krasue hears well).
  - Flashlight reach 15 m, cone 12°. Flee 3 s, then the flashlight is ignored for 8 s (no stun-lock). Without light, exposure decays at 2 × dt.
  - Investigate: give up after 15 s, and linger 2 s at the noise point.
  - Spared Spy: ignored for 8 s.
- Scream (Z, GDD controls): voice noise of loudness 1 with a 3 s cooldown.
  - `NoiseRules.FromVoice(level)`: loudness = clamp01(level), radius = 4 + 16 × loudness. Below 0.05 it is silent.
  - M7 Vivox feeds the same RPC with mic level.
- Running footsteps: the server raises `NoiseRules.Footstep` every 0.5 s while `IsRunningNet`.
- Ghost markers (server, batchmode logs):
  - `GHOST_SPAWNED`
  - `GHOST_STATE state=X target=N t=T`
  - `GHOST_HEARD at=... t=T`
  - `GHOST_GRAB target=N t=T`
  - `GHOST_SPARED target=N`
  - `PLAYER_KILLED id=N`
  - `PLAYER_RESPAWNED id=N`
- Client markers: `FEAR band=none|far|mid|near`, `PLAYER_ADRENALINE state=X mult=M`.
- Launch args:
  - `-playscene NAME` (default `House`)
  - `-ghostwait`: smoke only; the ghost holds still in Wander until it hears or sees something.

## Review Focus
1. **The chase target disconnects or dies mid-chase or mid-grab:** the ghost drops the target and returns to Wander/Cooldown instead of freezing. → brain tests `Chase_TargetInvalid_Wander`, `Grab_TargetInvalid_Cooldown`.
2. **Players chain flashlights to stun-lock the ghost:** after a flee the light is ignored for 8 s. → brain test `Flee_ThenImmuneToLight`.
3. **A noise heard during Cooldown, Grab or Flee:** ignored. A noise beyond the radius × hearing multiplier: not heard. → `OnNoise_IgnoredWhileCooldownGrabFlee`, `Perception_HearingRadius`.
4. **A client sends a scream every frame or a NaN/huge level:** the server enforces the 3 s cooldown and clamps the level. → `PlayerNoise` server check + `FromVoice_ClampsAndNaN` test.
5. **A late joiner or the host's own player:** sees the ghost and gets Fear (the ghost is a spawned NetworkObject, `GhostRegistry` registers on every peer). The victim client logs FEAR bands in ghost-smoke.

---

### Task 1: Core ghost rules (TDD)
**Files:**
- Create: `Core/Ghosts/GhostState.cs` (`enum GhostState { Wander, Investigate, Alert, Chase, Grab, Cooldown, Flee }`, `enum GhostEvent { None, Heard, Alerted, ChaseStarted, Grabbed, Killed, Spared, GaveUp, Fled }`)
- Create: `Core/Ghosts/GhostTuning.cs`, `GhostSenses.cs`, `GhostBrain.cs`, `GhostPerception.cs`
- Modify: `Core/Player/NoiseRules.cs` (add `FromVoice`)
- Test: `Tests/EditMode/GhostBrainTests.cs`, `GhostPerceptionTests.cs`; extend `PlayerRulesTests.cs`

**Interfaces (Produces):**
- `struct GhostSenses { bool SeesPlayer; ulong SeenId; bool TargetValid; bool TargetVisible; float TargetDistance; bool TargetHarmable; bool ReachedPoint; bool Lit; }`
- `sealed class GhostBrain`:
  - `GhostState State`, `ulong TargetId`, `bool HasTarget`, `float StateTime`
  - `float SpeedMultiplier = 1`, `float HearingMultiplier = GhostTuning.KrasueHearing`
  - `float Speed`: the state's speed × SpeedMultiplier; Alert and Grab are 0
  - `bool IsIgnoring(ulong id)`
  - `bool OnNoise(float distance, float radius)`: true when the ghost starts or retargets an Investigate. Only from Wander or Investigate, and only if `GhostPerception.Hears`.
  - `GhostEvent Tick(float dt, in GhostSenses s)`
- `static class GhostPerception`:
  - `bool CanSee(float distance, float angleFromForwardDeg, bool clear)`
  - `bool IsLit(float distance, float angleFromBeamDeg, bool clear)`
  - `bool Hears(float distance, float radius, float hearingMultiplier)`
  - `float AngleDeg(float ax, float ay, float az, float bx, float by, float bz)` (no engine types in Core)
- `NoiseRules.FromVoice(float level)`

**Transition rules (the brain implements exactly these):**
- Lit counts in every state except Grab/Flee, and only while not immune. At exposure ≥ 2 s → Flee (`Fled`). Lit has priority over all other transitions.
- Wander/Investigate + `SeesPlayer` (and not ignoring) → Alert with that target (`Alerted`).
- Investigate + `ReachedPoint` → linger 2 s → Wander. Investigate older than 15 s → Wander.
- Alert: after 1 s → Chase (`ChaseStarted`); target invalid → Wander.
- Chase:
  - distance ≤ 1.4 and harmable → Grab (`Grabbed`)
  - distance ≤ 1.4 and not harmable → Wander, ignore that id for 8 s (`Spared`)
  - out of sight for ≥ 6 s, or target invalid → Wander (`GaveUp`)
- Grab: after 2 s → Cooldown (`Killed`, TargetId still set this tick); target invalid → Cooldown.
- Cooldown: after 10 s → Wander. Flee: after 3 s → Wander, with light immunity for 8 s.

- [ ] **Step 1: Write failing tests** `GhostBrainTests`:
  - `Starts_Wander`
  - `SeesPlayer_Alert_ThenChaseAfter1s` (0.99 s still Alert, Speed 0)
  - `Chase_Speed_6`
  - `Chase_InRange_Harmable_Grab`
  - `Grab_2s_Killed_ThenCooldown10s_ThenWander`
  - `Chase_InRange_NotHarmable_Spared_IgnoredFor8s`
  - `Chase_LostSight6s_GaveUp`
  - `Chase_SightReturns_ResetsLostTimer`
  - `Chase_TargetInvalid_Wander`
  - `Alert_TargetInvalid_Wander`
  - `Grab_TargetInvalid_Cooldown`
  - `Lit2s_Flee3s_ThenWander`
  - `Lit_Interrupted_Decays`
  - `Flee_ThenImmuneToLight` (8 s)
  - `Lit_IgnoredDuringGrab`
  - `OnNoise_Wander_Investigate`
  - `OnNoise_IgnoredWhileChaseCooldownGrabFlee`
  - `OnNoise_TooFar_NotHeard`
  - `Investigate_Reached_Linger2s_Wander`
  - `Investigate_Timeout15s_Wander`
  - `SpeedMultiplier_Applies`
  - `Tuning_ChaseBetweenRunAndAdrenalineRun` (`ChaseSpeed > MovementModel.RunSpeed && ChaseSpeed < MovementModel.RunSpeed * (1 + AdrenalineModel.MinBonus)`)

  `GhostPerceptionTests`:
  - `CanSee_InsideRangeAndFov`, `CanSee_Behind_False`, `CanSee_Blocked_False`
  - `IsLit_InCone`, `IsLit_OutsideCone_False`, `IsLit_TooFar_False`
  - `Hears_RadiusTimesMultiplier`
  - `AngleDeg_ZeroVector_Returns180` (a degenerate vector is never "in front")

  `PlayerRulesTests`: `FromVoice_Scream_Loud1_Radius20`, `FromVoice_Quiet_Silent`, `FromVoice_ClampsAndNaN`.
- [ ] **Step 2:** `tools/unity/run-tests.ps1 -Filter Ghost` → FAIL (CS0246, missing types).
- [ ] **Step 3:** Implement the files above. `GhostBrain` keeps an `ignore` dictionary of id → seconds left, decremented each Tick.
- [ ] **Step 4:** `tools/unity/run-tests.ps1` → all EditMode tests pass (213 + new).
- [ ] **Step 5:** Commit `feat(core): ghost brain, perception and voice noise rules`.

### Task 2: Thai house scene + NavMesh + play-scene switch
**Files:**
- Create: `Editor/Builders/HouseBuilder.cs` (`const string ScenePath = "Assets/_Project/Scenes/House.unity"`, `static void Build()`)
- Create: `Game/Ghosts/GhostWaypoint.cs` (`int Index`)
- Create: `Game/Fear/FlickerLight.cs` (empty marker + base intensity; behavior in Task 4)
- Modify: `SceneBuilder.BuildAll` (adds House to build scenes after Sandbox), `LaunchArgs` (`PlayScene`, `GhostWait`), `SessionFlow` (`PlayScene` is read from args, default `"House"`; keep `const DefaultPlayScene`), `SmokeProbe` (uses `SessionFlow.CurrentPlayScene`), `tools/unity/player-smoke.ps1` (host passes `-playscene Sandbox`)
- Test: `LaunchArgsTests` (`PlayScene_DefaultNull_ParsesValue`, `GhostWait_Flag`)

**Layout (metres; y = 0 is the yard ground):**
- Yard ground 60 × 50, centred at (0, 0, −1). A fence 1.6 m high around x ±25, z −22..+20, with a 3 m `Gate` gap at (0, 0, −22). The gate is a closed bar object named `EscapeGate`, without a collider in M4, and is opened in M5.
- House platform at y = 2.6, x −10..10, z −5..5, with 0.2 m slab colliders. Under it (ใต้ถุน): 4 × 3 pillars 0.4 m, plus crates and a loom as obstacles.
- Upper rooms, each with a 1.2 m doorway gap and walls 2.4 m high:
  - Bedroom: x −10..−3, z 0..5
  - Shrine room: x −10..−3, z −5..0
  - Open ชาน terrace with a railing: x −3..3
  - Kitchen: x 3..10
- Roof slabs: visual only, no colliders.
- Stairs are ramp colliders (rise 2.6 over 5 m), with visual-only steps:
  - Front, south from the terrace: down to (0, 0, −10)
  - Back, east from the kitchen: down to (13, 0, 3)
  - Two stairs give chase loops.
- ศาลพระภูมิ at (0, 0, −16): pedestal plus a small house, named `SpiritHouse`.
- 8 `SpawnPoint`s in an arc of radius 3 around (0, 0, −17), facing the house.
- `GhostSpawn` at (0, 0, 3), under the house, facing +z.
- 8 `GhostWaypoint`s: 3 under the house, 1 per room (4), 1 in the yard corner.
- Items: 3 light pots (`pot1..3`) in the yard and rooms, 1 heavy `urn` on the terrace. Reuse SandboxBuilder's item helper by moving it to `BuilderUtil.Item` (public).
- Lights: dim moon, a green lantern at the spirit house, purple lamps in the rooms. Room lamps get `FlickerLight`.
- `NavMeshSurface` on the `Environment` root:
  - `collectObjects = Children`, `useGeometry = PhysicsColliders`
  - agent radius 0.4, height 1.6, step 0.4, slope 45
  - baked data saved as `Scenes/House/NavMesh-House.asset`, overwriting the same asset path

- [ ] **Step 1:** Write the `LaunchArgsTests` → run → FAIL.
- [ ] **Step 2:** Implement LaunchArgs/SessionFlow/SmokeProbe/player-smoke changes, then run tests → PASS.
- [ ] **Step 3:** Implement HouseBuilder and register it in SceneBuilder. `tools/unity/run-method.ps1 -Method BooTrayal.Editor.Builders.SceneBuilder.BuildAll` → log has `[HouseBuilder] House scene ready triangles=N` with N > 0.
- [ ] **Step 4:** Run BuildAll a second time → `git status --short` shows no modified files.
- [ ] **Step 5:** `tools/unity/build-windows.ps1` then `tools/unity/player-smoke.ps1` → `PLAYER SMOKE OK` (Sandbox still works).
- [ ] **Step 6:** Commit `feat(map): graybox Thai house scene with baked NavMesh`.

### Task 3: Player life, scream and footsteps
**Files:**
- Create: `Game/Player/PlayerLife.cs`:
  - `NetworkVariable<bool> Dead`, `NetworkVariable<bool> Grabbed`, both server-write
  - `bool IsAlive`, `bool CanMove`
  - server: `void SetGrabbed(bool)`, `void Kill()`
  - Dead → hide the Visual and disable the CharacterController on all peers; the server drops the held item via `PlayerHands.ForceRelease("death")`
  - after `RespawnSeconds = 5f` the server sends `RespawnRpc` (to the owner), which teleports to the spawn point. Logs `PLAYER_KILLED` / `PLAYER_RESPAWNED`.
- Create: `Game/Player/PlayerNoise.cs`:
  - server: footstep noise every 0.5 s while `IsRunningNet`
  - owner: `ScreamPressed` → `ScreamRpc(float level)`. The server clamps the level, enforces `ScreamCooldown = 3f` and raises `NoiseRules.FromVoice` at the head.
  - `public void ReportVoice(float level)` is the entry point M7 will reuse.
- Modify:
  - `IPlayerInput` + `PlayerInputReader` (Z / gamepad right-shoulder `ScreamPressed`)
  - `BotInput` (`BotButton.Scream`, ButtonCount 7)
  - `PlayerMotor` (no input movement while `!life.CanMove`)
  - `PlayerLook` (no look while grabbed or dead)
  - `PlayerHands` (`ForceRelease(string reason)`, server; ignore input while `!CanMove`)
  - `PlayerPrefabBuilder` (add PlayerLife, PlayerNoise)
- Test: compile + player-smoke (existing behavior unchanged). Scream is exercised in Task 5.

- [ ] **Step 1:** Implement. `tools/unity/compile.ps1` → 0 errors, 0 new warnings.
- [ ] **Step 2:** Rebuild prefabs/scenes, build, then `player-smoke.ps1` → `PLAYER SMOKE OK`.
- [ ] **Step 3:** Commit `feat(player): life state, scream and running footstep noise`.

### Task 4: Krasue ghost on the network + client Fear
**Files:**
- Create: `Game/Ghosts/GhostController.cs` (NetworkBehaviour):
  - server: owns `GhostBrain`, drives the agent, subscribes `NoiseBus.Raised`
  - `NetworkVariable<GhostState> StateNet`, `NetworkVariable<ulong> TargetNet`
  - senses: nearest alive non-ignored player by `GhostPerception.CanSee`, with a raycast from the ghost head (1.4 m) to the player head that ignores players, items and triggers; flashlight from `PlayerFlashlight.On` + the HeadPivot forward
  - destinations: Wander picks the next `GhostWaypoint` with Core `SeededRandom` (seed from `Environment.TickCount`); Investigate goes to the noise point; Chase goes to the target; Flee goes 8 m away from the light holder on the NavMesh (`NavMesh.SamplePosition`)
  - `-ghostwait` keeps Wander's speed 0 until the first Heard or Alerted event
  - Grabbed → `PlayerLife.SetGrabbed(true)`; Killed → `Kill()`
- Create: `Game/Ghosts/GhostTargeting.cs`: `static Func<ulong, bool> IsHarmable = _ => true` (M5 sets the Spy rule)
- Create: `Game/Ghosts/GhostRegistry.cs`: static `IReadOnlyList<Transform> Active`, `float NearestDistance(Vector3)`
- Create: `Game/Ghosts/GhostView.cs`: client bob, entrails pulse per state, procedural screech on entering Alert
- Create: `Game/Ghosts/GhostSpawner.cs`: in-scene NetworkBehaviour with a `GameObject Prefab` field; the server spawns it at the spawner's transform in `OnNetworkSpawn`
- Create: `Game/Audio/ProceduralClips.cs`: `static AudioClip Heartbeat()`, `Wind()`, `Drone()`, `Screech()` (deterministic, seeded)
- Create: `Game/Fear/FearDirector.cs` (on the player, owner only):
  - each frame: `GhostRegistry.NearestDistance` → `PlayerMotor.GhostDistance`, `FearModel.Evaluate`
  - a global `Volume` with runtime Vignette (`Vignette` × 0.45) and ChromaticAberration (`Chromatic`)
  - camera shake (`Shake` × 0.08 m) on the camera's local position
  - three looping AudioSources (Wind = Whisper, Heartbeat = Heartbeat, Drone = Shake) with volumes smoothed by `MoveTowards` at 1.5/s
  - static `Levels` for FlickerLight; batchmode band/adrenaline logs
- Modify: `Game/Fear/FlickerLight.cs` (flicker when local `Levels.Flicker > 0` and within 15 m of the local player)
- Create: `Editor/Builders/GhostPrefabBuilder.cs` (`Prefabs/Ghosts/Krasue.prefab`):
  - NetworkObject, NetworkTransform server, interpolate
  - NavMeshAgent (radius 0.4, height 1.6, base offset 0.6, acceleration 30, angular 720)
  - GhostController, GhostView
  - visuals: head sphere, hair, eyes, entrail capsules with a glowing green material `#39FF88`, a green point light
  - trigger sphere collider r 0.5
- Modify:
  - `NetworkPrefabBuilder` (register Krasue)
  - `HouseBuilder` (GhostSpawner at GhostSpawn with Prefab = Krasue)
  - `PlayerPrefabBuilder` (FearDirector)
  - `PlayerLook` (camera `UniversalAdditionalCameraData.renderPostProcessing = true`; expose `ShakeOffset`)
- Order: `NetworkPrefabBuilder.Build` must build Krasue before `SceneBuilder.BuildAll` runs (build-windows already runs them in that order).

- [ ] **Step 1:** Implement. Compile → 0 errors.
- [ ] **Step 2:** Run the builders twice → no diff on the second run. Then build → `BOOTRAYAL_SMOKE_OK`.
- [ ] **Step 3:** Commit `feat(ghost): server-run Krasue with NavMesh, senses and client fear effects`.

### Task 5: Ghost smoke test
**Files:**
- Create: `tools/unity/ghost-smoke.ps1` (same structure as player-smoke)
- Modify: `Game/Dev/SmokeProbe.cs` (roles `victim`, `lighter`; `WalkTo` gains a NavMesh-free straight walk as now; a `FaceTowards(Vector3)` helper that sets yaw + pitch via new `PlayerLook.SetPitch(float)`)

**Script:**
1. Host `-autohost-local -smokeprobe host -ghostwait` (House) → `GHOST_SPAWNED`.
2. `victim` joins: walks to (0, 0, −7), behind the ghost and outside its FOV, waits 1 s, presses Scream.
   - host: `GHOST_HEARD`, then `GHOST_STATE state=Alert target=1`, then `state=Chase target=1` with t(Chase) − t(Alert) ≥ 0.95, then `GHOST_GRAB target=1`, then `PLAYER_KILLED id=1` with t ≥ grab t + 1.9, then `state=Cooldown`, then `PLAYER_RESPAWNED id=1`.
   - victim log: `FEAR band=near` and `PLAYER_ADRENALINE state=Adrenaline`.
3. `lighter` joins during the Cooldown: walks to 8 m in front of the ghost, turns the flashlight on, faces the ghost's head every frame.
   - host: `GHOST_STATE state=Flee`, then later `state=Wander`.
4. No `Exception` / Netcode errors in any log.

- [ ] **Step 1:** Implement the probe roles and the script.
- [ ] **Step 2:** Build, then `tools/unity/ghost-smoke.ps1` → `GHOST SMOKE OK`; `player-smoke.ps1` → OK; `net-smoke.ps1 -Mode Local` → OK.
- [ ] **Step 3:** Commit `test(ghost): multi-instance ghost smoke test`.

### Task 6: Review, docs, merge
- [ ] Final code review (requesting-code-review), fix Critical/Important, re-run all smokes.
- [ ] Ledger `docs/plans/2026-10-23-m4-ledger.md`, PROGRESS.md M4 section, merge `m4-house-ghost` → main, push.
