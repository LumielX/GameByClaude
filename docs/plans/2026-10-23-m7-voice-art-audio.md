# M7: Vivox Voice + First Art and Audio Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:**
- **Voice:**
  - In online rooms friends talk by proximity (Vivox positional).
  - During meetings the whole room hears everyone.
  - Mini ghosts talk only to each other, and still hear the living nearby.
  - Your mic level is a noise the Krasue hears.
  - Open mic or push-to-talk (V).
- **Art:** The graybox primitives are replaced by self-made low-poly toon models built by a Blender script: the player with rig + idle/walk/run/carry/fall, the Krasue, the urn, the incense pot, the clay pot, the bell and the spirit house. They use a URP toon shader with an outline.
- **Audio:** The sound set is completed: ambience, a third fear layer, bell, UI clicks.

**Architecture:**
- **Voice:**
  - Pure Core `VoiceRules` decide which channels a player is in and where they transmit (TDD).
  - Game `VoiceService` wraps `Unity.Services.Vivox`: init → login (needs the UGS sign-in that online rooms already do; Solo/localhost has no voice) → join channels named from the room code → switch the transmission channel as life state and meetings change → update the 3D position 4×/s → read the local participant's `AudioEnergy` → `PlayerNoise.VoiceRpc` (server rate-limited) → NoiseBus.
- **Art:**
  - `tools/blender/build_models.py` (run headless by `tools/blender/build-models.ps1`) builds every model from code. It writes FBX + preview PNG + a tris report into `Assets/_Project/Art/Models/`.
  - Unity `ModelImportBuilder` sets import options and builds the player's AnimatorController.
  - The prefab/scene builders swap visuals and keep the existing colliders.
- **Audio:** `ProceduralClips` stays the source (self-made, deterministic); a small `AudioDirector` plays ambience, the bell and UI sounds.

**Tech Stack:**
- Vivox 16.12.1 (`VivoxService.Instance`, `JoinPositionalChannelAsync`, `JoinGroupChannelAsync`, `SetChannelTransmissionModeAsync(TransmissionMode.Single, name)`, `Set3DPosition`, `VivoxParticipant.AudioEnergy/SpeechDetected`, `MuteInputDevice`)
- Blender 5.2.2 (`X:\TheBlender\blender.exe --background --python`)
- URP 17.6 hand-written shader: `UniversalForward` toon pass + `SRPDefaultUnlit` inverted-hull outline

**Spec:** `docs/GDD.md` §5.1 (mini ghost voice), §6.4 (Krasue hears the mic), §7.4 (meeting: everyone hears everyone), §11 (art style: low-poly toon + outline), §12 (audio list), §13 (Vivox stack); `docs/PLAN.md` M7.

## Global Constraints
- All earlier constraints apply. No downloaded assets: every model is built by our Blender script, every sound by `ProceduralClips`.
- Voice channel names:
  - `bt-{code}-pos` (positional; audible 32 m, conversational 3 m, fade 1.0, InverseByDistance)
  - `bt-{code}-meet` (group)
  - `bt-{code}-ghost` (group)
  - `{code}` is the 6-letter room code
- Membership and transmit:
  | life state | joined | transmits to |
  |---|---|---|
  | Alive | pos + meet | pos (meet while a meeting runs) |
  | MiniGhost | pos + meet + ghost | ghost |
  | Escaped | pos + meet | pos |
  | not in a session | none | none |
- Mic → ghost:
  - `VoiceRules.LoudnessFromEnergy(energy)` = clamp01((energy − 0.15) / 0.45); below 0.15 gives 0. NaN gives 0.
  - The client sends at most every 0.5 s and only while speech is detected.
  - The server accepts at most every 0.4 s per player, clamps to 0..1 and raises `NoiseRules.FromVoice(level)`.
  - The existing scream RPC is unchanged.
- PTT key V (GDD §5). Setting `bt.voice`: 0 = open mic (default), 1 = push-to-talk. Settings also gets voice volume (`bt.voicevol`, Vivox output via `SetChannelVolumeAsync`, −50..50 mapped from 0..1).
- HUD shows "<name> กำลังพูด" for speaking participants (DisplayName = the NGO client id).
- Tris budgets (checked by an EditMode test over the imported meshes):
  - player 3000
  - Krasue 2500
  - urn / incense / pot / bell 800 each
  - spirit house 1500
- Toon shader:
  - 3-band ramp on the main light with shadows, plus additional lights (simple Lambert); ambient from SH
  - rim light
  - outline width 0.02 world units, color `#120C1C`
  - shader name `BooTrayal/Toon`
- Smoke tests must not need audio devices or Vivox: `VoiceService` only starts when the room is an online (Relay) session and `-novoice` is not given. Relay smoke passes `-novoice` except for an opt-in `-Voice` run.

## Review Focus
1. **Life state changes mid-meeting** (death, revive, escape): the transmission channel follows within a second; a mini ghost is never heard by the living. → `VoiceRules` tests cover every (state, meeting) pair; VoiceService re-applies on every change.
2. **Vivox login/join fails** (offline, no mic, service down): the game keeps working; one Thai/English notice in Settings; no exception spam. → VoiceService catches and logs `VOICE_FAILED reason=…`, disables itself.
3. **Leaving a room or the host leaving:** all channels are left and logged out, so the next room does not inherit old channels. → `VoiceService.Stop()` on session end; logs `VOICE_STOPPED`.
4. **A client flooding `VoiceRpc` or sending NaN/huge levels:** ignored or clamped. → `VoiceRules.AcceptVoice` tests + server check.
5. **Animator on remote players:** speed comes from the synced transform (the owner's motor speed is not networked); carry/fall come from NetworkVariables. → PlayerAvatar computes the speed from position deltas on non-owners.

---

### Task 1: Core voice rules (TDD)
**Files:** Create `Core/Voice/VoiceRules.cs`; test `Tests/EditMode/VoiceRulesTests.cs`

**Interfaces:**
- `enum VoiceChannel { Positional, Meeting, Ghost }`
- `static class VoiceRules`:
  - `string ChannelName(string roomCode, VoiceChannel c)` (`bt-{code}-pos|meet|ghost`; null/empty code → null)
  - `IReadOnlyList<VoiceChannel> Joined(LifeState s)`
  - `VoiceChannel? TransmitTo(LifeState s, bool meetingRunning)`
  - `float LoudnessFromEnergy(double energy)`
  - `const double ClientSendInterval = 0.5`, `const double ServerAcceptInterval = 0.4`
  - `bool AcceptVoice(double now, double lastAccepted)`
  - `const int AudibleDistance = 32, ConversationalDistance = 3`
- Tests:
  - `ChannelName_Format_AndNullCode`
  - `Joined_PerState`
  - `TransmitTo_EveryStateAndMeeting` (Alive/meeting → Meeting, MiniGhost/meeting → Ghost, Disconnected → null)
  - `Loudness_Threshold_Clamp_NaN`
  - `AcceptVoice_Interval`
- [ ] RED → implement → all tests pass → commit `feat(core): voice channel and mic loudness rules`.

### Task 2: VoiceService, settings and HUD
**Files:**
- Create `Game/Voice/VoiceService.cs` (on Services): `Start(string roomCode)`, `Stop()`, `IReadOnlyList<ulong> Speaking`, `bool Active`, `string LastError`.
  - Applies `VoiceRules` each 0.25 s: desired joined set vs actual; transmission mode; `Set3DPosition(localHead, pos)`.
  - PTT via `GameSettings.VoiceMode` + `PlayerInputReader.TalkHeld` (V).
  - Mic energy → `PlayerNoise.ReportVoice(level)`.
- Modify `PlayerNoise`: `ReportVoice(float)` → `VoiceRpc(float)` with the server checks.
- Modify `ConnectionService`: `IsOnlineRoom`; start/stop voice on Hosting/Connected/Offline; `-novoice` in `LaunchArgs` (test).
- Modify `IPlayerInput`/`PlayerInputReader`/`BotInput`: `TalkHeld` (V).
- Modify `GameSettings`: `VoiceMode`, `VoiceVolume`.
- Modify Settings UXML + controller: voice mode pills, voice volume slider, error line; lang keys.
- Modify HUD: speaking line; lang keys `hud.speaking`, `settings.voice.*`.
- [ ] Compile → `run-tests` (LaunchArgs `-novoice` test RED → GREEN) → build → all smokes OK (they run without voice) → opt-in `net-smoke.ps1 -Mode Relay -Voice` logs `VOICE_JOINED` on both instances, or `VOICE_FAILED` with a reason (allowed in batchmode; recorded) → commit `feat(voice): Vivox proximity, meeting and mini-ghost channels`.

### Task 3: Blender models
**Files:**
- Create `tools/blender/build_models.py` + `tools/blender/build-models.ps1`.
- Outputs `Assets/_Project/Art/Models/{Player,Krasue,Urn,IncensePot,ClayPot,Bell,SpiritHouse}.fbx`, `docs/art_previews/models/*.png`, `Assets/_Project/Art/Models/tris.txt`.
- **Player:** chibi body (head 45% of height, simple hands and feet, eyes as separate material slots so `PlayerAvatar` can tint the body).
  - Armature bones: Root, Hips, Spine, Head, ArmL, ArmR, LegL, LegR; automatic weights.
  - Actions:
    - Idle (2 s loop, breathing)
    - Walk (0.8 s)
    - Run (0.5 s)
    - Carry (arms forward, 1 s loop)
    - Fall (0.6 s, tip over)
  - Height 1.7 m, feet at origin, facing +Y in Blender (= +Z in Unity).
- **Krasue:** head with hair and red eyes, entrails as tapered tubes with a glowing material slot.
- **Props:** urn (lathe profile), incense pot with three sticks, clay pot, bell (lathe), spirit house (pedestal + small Thai roof with chofa tips).
- [ ] Run → every file exists → tris within budget (script exits non-zero otherwise) → look at every preview PNG → commit `feat(art): Blender script for the first low-poly toon models`.

### Task 4: Import, toon shader, prefabs
**Files:**
- Create `Assets/_Project/Art/Shaders/Toon.shader`.
- Create `Editor/Builders/ModelImportBuilder.cs`: model importer settings (scale 1, no import of cameras/lights, materials extracted to our toon materials), animation clips split by action name with loop flags, `PlayerAnimator.controller` (Idle/Walk/Run blend on `Speed`, `Carry` bool layer, `Fall` bool).
- Modify:
  - `BuilderUtil.Lit` → `Toon(name, color)` for characters/props (keep Lit for the house graybox)
  - `PlayerPrefabBuilder` (model under Visual, Animator; colliders unchanged)
  - `PlayerAvatar` (Animator params from motor/hands/knockdown; remote speed from position delta; tint the "Body" material slot)
  - `GhostPrefabBuilder` (Krasue model)
  - `HouseBuilder` / `HouseStationsBuilder` (urn, incense, pot items, bell, spirit house visuals)
- Test: `Tests/EditMode/ModelBudgetTests.cs`:
  - `Models_WithinTrisBudget` (loads each FBX's meshes via AssetDatabase)
  - `PlayerAnimator_HasAllStates`
  - `ToonShader_Compiles` (`Shader.Find("BooTrayal/Toon")` not null, no errors through `ShaderUtil.ShaderHasError`)
- [ ] RED (no models/shader) → implement → builders twice no diff → build → all smokes → `ui-smoke -Shots` HUD screenshot shows the new models → commit `feat(art): toon shader, imported models and player animator`.

### Task 5: Audio set
**Files:**
- Modify `Game/Audio/ProceduralClips.cs`: `Ambience()` (night insects + far wind, 8 s loop), `Whispers()` (third fear layer), `Bell()` (inharmonic partials, 2.5 s), `UiClick()`, `UiHover()`.
- Create `Game/Audio/AudioDirector.cs` (Services): ambience while in the House; bell on `MEETING_START` (`NightDirector.MeetPhase` → Discuss); `PlayUi(click|hover)`.
- Modify `FearDirector`: a third layer, Whispers at the mid band.
- Modify `UIRoot`: button click/hover sounds through AudioDirector.
- Test: `Tests/EditMode/ProceduralClipsTests.cs`: `Clips_HaveExpectedLengthAndNoNaNOrClipping`.
- [ ] RED → implement → tests → build → ui-smoke OK → commit `feat(audio): ambience, bell, whispers and UI sounds`.

### Task 6: Review, docs, merge
- [ ] Final review → fixes with tests → ledger → PROGRESS (a manual 2-PC voice checklist for the user) → merge → push.
