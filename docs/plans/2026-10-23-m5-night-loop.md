# M5: One Night Loop (puzzles, Spy, meeting, win/lose) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A whole game (1–5 nights, default 3) plays end to end in the House:
- **Lobby:** the host starts.
- **Each night:**
  - Roles are dealt privately (Spy only at 5+ players).
  - N seals (safe-code and fuse-box puzzles) must be closed.
  - Ritual items are carried to the altar, then the gate opens and the crew escape.
  - Meanwhile: the Krasue hunts, the Spy sabotages puzzles and blacks out zones (with a cold-breath clue), anyone alive can ring the shrine bell for a meeting and vote, dead players become mini ghosts who can be revived at the shrine.
  - Night end: summary with Spy reveal and coins.
- **After the last night:** ranking, then back to the lobby.

**Architecture:**
- All night rules live in pure Core classes (TDD): `NightSession` (one night), `Meeting`, `SpyPowerCooldowns`, `GameSession` (wallets + ranking across nights) and `SealPlanner`.
- `NightDirector` is the only network adapter. It is an in-scene server NetworkBehaviour in House that owns the Core sessions, drives the stations, ghost, gate and players, and publishes state through NetworkVariables.
- Private data (roles) goes through targeted RPCs only.
- World interactions (keypad buttons, levers, bell) are `Interactable` parts of station NetworkBehaviours. The player's `PlayerActions.InteractRpc` (owner → server) carries them. The server checks distance and life state.
- A temporary IMGUI `NightHud` shows phase, timer, tasks, role, meeting vote buttons, summary and ranking. M6 replaces it with UI Toolkit + Localization.

**Tech Stack:** NGO 2.13.3 (`[Rpc(SendTo.SpecifiedInParams)]` + `RpcTarget.Single`, NetworkVariables), NavMesh (bots follow `NavMesh.CalculatePath` corners), legacy `TextMesh` for digits and clue notes (built-in font, no asset needed), IMGUI.

**Spec:** `docs/GDD.md` §4 (core loop, numbers, win/lose, coins), §5.1 (mini ghost), §6.1 (ghost spares the unexposed Spy), §7 (Spy count, powers 1–2, clues, meeting/vote), §8 (puzzles #1–3, solo fallbacks); `docs/PLAN.md` M5.

## Global Constraints
- All earlier constraints (M0–M4) still apply. Reuse the existing Core rules unchanged where they fit: `RoleAssigner`, `RoomRules.SpyCount/SealCount`, `NightJudge`, `VoteResolver`, `MeetingRules`, `SabotageTracker`, `ExorciseEffects`, `ScoreRules`, `Wallet`, `SafeCodePuzzle`, `FuseBoxPuzzle`, `RitualOfferingPuzzle`, `JoinPolicy`.
- GDD numbers:
  - Night 10 min × 3.
  - Seals: 2 players 3, 3–4 players 4, 5–6 players 5, 7–8 players 6 (`RoomRules.SealCount`).
  - Spy only at 5+ players, re-dealt every night. Two Spies know each other.
  - Sabotage puzzle: range 3 m, cooldown 60 s. Counts only if the puzzle has someone else's progress (existing `TrySabotage`).
  - Blackout: whole zone dark for 10 s, cooldown 75 s. Counts if at least one living crew member is in the zone.
  - Cold breath visible on the Spy for 3 s after any power use.
  - Bell: once per living player per night, not while being chased. Meeting: discuss 45 s, then vote 15 s (a person or skip). The unique top beats skip → ejected.
  - Correct vote: the Spy becomes a mini ghost that cannot be revived this night; ghost ×0.8 speed for the rest of the night; +60 s night time.
  - Wrong vote: ghost ×1.2 speed and ×1.5 hearing for 30 s; the ejected crew member becomes a revivable mini ghost.
  - Dead → mini ghost plus a soul orb left where they died. A living player carries the orb to the ศาลพระภูมิ and holds it there 3 s → revive at the shrine with full stamina.
  - Mini ghosts cannot vote, ring, grab, push or use puzzles. Boo (Z) has an 8 s cooldown and everyone hears it.
  - A Spy who disconnects mid-night loses that night (`Exposed` + caught).
  - Coins: seal +20 to each contributor, ritual item +10 to each deliverer, plus `ScoreRules.NightEndAwards`.
  - Rejoin only between nights (`SessionPhase.Summary`/`Lobby`).
- My calls:
  - Seal stations in House: 3 safes + 3 fuse boxes. `SealPlanner` picks N of the 6 at random each night. With 2 players at most 1 fuse box is picked (GDD §8 "fewer 2-person puzzles").
  - Ritual items: `urn` (heavy, terrace) and `incense` (light, shrine room). The ritual completes when both rest on the altar, then the gate opens at once (no separate hold step).
  - Escape: a living player standing outside the open gate (z < −22.6, |x| < 4) is Escaped. Escaped players are frozen outside; they can still look around.
  - Spy power keys until M6's wheel: Q = sabotage puzzle, X = blackout. Mini ghost Boo = Z.
  - Mini ghosts fly (Space up, Ctrl down, 3.2 m/s, no gravity) and still collide with walls. They do not collide with living players: player layer 8, mini ghost layer 9, `Physics.IgnoreLayerCollision(8, 9)` and `(9, 9)`.
  - The night clock keeps running during meetings; the ghost is frozen during meetings.
  - Between nights: summary 12 s. After the last night: game-over ranking 15 s, then Lobby.
  - Wallets are keyed by clientId. A rejoiner starts at 0 (deferred).
  - HUD text is Thai with English in brackets, like DebugNetMenu.
  - No ghost in the Lobby phase. The ghost spawns at night start and despawns at night end.
- Launch args (dev/smoke; M6 UI sets the real config):
  - `-autostart N`: start when N players are connected
  - `-nights K`
  - `-nightseconds a[,b,...]`: per-night override; the last value repeats
  - `-nospy`
  - `-spies 2`
  - `-noghost`
  - `-meetingscale f`: multiplies the 45/15 s
  - `-seed N`: seeds roles, seals and codes
- Markers:
  - server: `PHASE phase=X night=N`, `NIGHT_START night=N players=K seals=S`, `NIGHT_ROLES spies=a,b` (batchmode only), `SEAL_SOLVED station=X by=a,b done=d/t`, `RITUAL_DELIVERED item=X`, `GATE_OPEN`, `PLAYER_ESCAPED id=N`, `PLAYER_MINIGHOST id=N cause=C`, `PLAYER_REVIVED id=N`, `POWER_USED by=N power=P counted=B`, `POWER_DENIED by=N reason=R`, `MEETING_START caller=N`, `MEETING_VOTE_OPEN`, `VOTE_CAST by=N target=T`, `MEETING_RESULT ejected=T spy=B`, `NIGHT_END night=N winner=W reason=R sabotage=s/r`, `AWARD id=N coins=C total=T`, `GAME_OVER ranking=id:score,...`
  - client: `ROLE_RECEIVED role=R partners=...`, `SPY_TRACE id=N`, `BLACKOUT zone=Z`, `SUMMARY_RECEIVED night=N`

## Review Focus
1. **Role privacy:** a crew client must never receive any other player's role. Spies get only their partners. → targeted `RoleRpc`; smoke greps every crew log for the Spy's id in `ROLE_RECEIVED`.
2. **A player disconnects mid-night** (Spy, meeting caller, voter, the only crew member, a soul-orb holder): the night still ends correctly and nothing waits forever. → `NightSession_PlayerLeft_*` tests; the meeting counts only connected voters.
3. **Mini ghost or escaped player tries actions:** ring, vote, interact, power or grab are refused server-side. → `NightSession` tests + server checks in `PlayerActions`.
4. **Votes:** duplicate votes, a vote outside the Vote phase, a vote for a mini ghost or for oneself. Last vote wins; invalid ones are ignored; voting for oneself is allowed. → `Meeting_*` tests.
5. **Night ends in the middle of a meeting or a grab:** the meeting is cancelled, ghost despawned, players reset next night (alive, stamina full, items home). → `NightSession_TimeUpDuringMeeting_Ends` + smoke runs 2 nights.

---

### Task 1: Core night rules (TDD)
**Files:**
- Create: `Core/Night/NightSession.cs`, `Meeting.cs`, `SpyPowers.cs` (`enum SpyPower { SabotagePuzzle, Blackout }`, `static class SpyPowerRules`, `sealed class SpyPowerCooldowns`), `GameSession.cs`, `SealPlanner.cs`, `MeetingOutcome.cs`
- Modify: `Core/Fear/Stamina.cs` (`void Refill()`)
- Test: `Tests/EditMode/NightSessionTests.cs`, `MeetingTests.cs`, `GameSessionTests.cs`

**Interfaces (Produces):**
- `SpyPowerRules`: `Cooldown(SpyPower)` → 60 / 75 s; `SabotageRange = 3f`, `BlackoutSeconds = 10f`, `TraceSeconds = 3f`
- `SpyPowerCooldowns`: `bool Ready(SpyPower, double now)`, `void Use(SpyPower, double now)`
- `Meeting(ulong caller, IReadOnlyCollection<ulong> voters, IReadOnlyCollection<ulong> targets, float discussSeconds, float voteSeconds)`:
  - `MeetingPhase Phase { Discuss, Vote, Closed }`, `float TimeLeft`
  - `MeetingPhase Tick(float dt)`: Vote opens after the discussion; closes when the vote time runs out or every voter has voted
  - `bool Cast(ulong voter, ulong? target)`: Vote phase only; voter eligible; target null = skip, or must be in targets; last vote wins
  - `void RemoveVoter(ulong id)`
  - `VoteResult Result` (via `VoteResolver`, valid after Closed)
  - `IReadOnlyDictionary<ulong, ulong?> Ballots`
- `MeetingOutcome { bool Ejected; ulong Target; bool WasSpy; IReadOnlyList<ulong> CorrectVoters; }`
- `NightSession(IReadOnlyList<ulong> players, int spyCount, double seconds, IRandom rng)`:
  - state: `Roles`, `Players` (`NightPlayer` list), `Sabotage`, `double SecondsLeft`, `bool RitualComplete`, `Meeting ActiveMeeting`, `float GhostSpeedMultiplier`, `float GhostHearingMultiplier`
  - `NightVerdict Tick(float dt)`: clock, modifier expiry, meeting tick, then `NightJudge`. Closing a meeting applies its outcome and exposes it once via `MeetingOutcome? TakeOutcome()`.
  - lookups: `bool IsAlive(id)`, `bool IsHarmable(id)` (alive and not an unexposed Spy), `Role RoleOf(id)`
  - life events: `bool Kill(id)` (Alive → MiniGhost), `bool CanRevive(id)` (MiniGhost and not an exposed Spy), `bool Revive(id)`, `bool Escape(id)` (Alive and RitualComplete), `void PlayerLeft(id)` (→ Disconnected; a Spy is also Exposed + `MarkCaught`; the meeting drops them as a voter)
  - tasks: `void SealClosed(IEnumerable<ulong> contributors)` (+20 each), `void ItemDelivered(IEnumerable<ulong> by)` (+10 each), `void CompleteRitual()`
  - meeting: `bool RingBell(ulong id, bool beingChased, float discuss, float vote)` (voters and targets = Alive players)
  - powers: `bool TryUsePower(ulong spy, SpyPower power, out string reason)` (alive, unexposed Spy, cooldown by the session clock, no meeting); `bool RecordSabotage(ulong spy, bool hadEffect)`
  - `IReadOnlyDictionary<ulong, int> FinishAwards(NightVerdict v)` (in-night coins + `ScoreRules.NightEndAwards(players, v.Winner, correctVoters, Sabotage.GoalWon)`)
- `GameSession(int nights)`: `int NightIndex` (0-based), `bool IsOver`, `void AddPlayer(ulong)`, `void ApplyAwards(IReadOnlyDictionary<ulong,int>)`, `void NextNight()`, `IReadOnlyList<(ulong Id, int Score)> Ranking()` (TotalScore desc, then id asc), `Wallet WalletOf(ulong)`
- `SealPlanner.Pick(IRandom rng, IReadOnlyList<bool> isTwoPerson, int count, bool limitTwoPerson)` → indices (distinct; when `limitTwoPerson`, at most one two-person station unless there are not enough others)
- `Stamina.Refill()`

- [ ] **Step 1: Write failing tests**
  - `NightSessionTests`:
    - `Start_DealsSpiesBySpyCount`, `CoOp_NoSpies_NothingHarmlessExceptNone`
    - `IsHarmable_UnexposedSpyFalse_ExposedTrue`
    - `Kill_MiniGhost_CanRevive_Revive`, `Kill_CaughtSpy_CannotRevive`
    - `Escape_OnlyAfterRitual`
    - `PlayerLeft_Spy_ExposedAndCaught`
    - `PlayerLeft_OnlyCrew_NightEndsCrewWiped`
    - `SealAndItem_AwardsContributors`
    - `RingBell_OncePerNight_NotWhileChased_NotMiniGhost`
    - `Meeting_CorrectVote_SpyMiniGhost_Plus60s_GhostSlow`
    - `Meeting_WrongVote_Rage30s_ThenNormal`
    - `Meeting_Skip_NoEjection`
    - `Power_OnlyUnexposedAliveSpy_Cooldowns_60_75`
    - `Power_DuringMeeting_Denied`
    - `RecordSabotage_GoalReached_SpiesWin`
    - `TimeUp_UncaughtSpy_SpiesWin`
    - `TimeUpDuringMeeting_Ends`
    - `FinishAwards_CrewWinEscaped`
  - `MeetingTests`:
    - `Discuss45_ThenVote15_ThenClosed`
    - `Cast_OnlyDuringVote`, `Cast_LastVoteWins`
    - `Cast_IneligibleVoterOrTarget_Ignored`
    - `AllVoted_ClosesEarly`
    - `RemoveVoter_DropsBallot`
  - `GameSessionTests`:
    - `Ranking_ByTotalDescThenId`, `NightsCount_IsOver`
    - `SealPlanner_PicksDistinctCount`, `SealPlanner_TwoPlayers_AtMostOneTwoPerson`
    - `Stamina_Refill`
- [ ] **Step 2:** `run-tests.ps1 -Filter Night` → FAIL (missing types).
- [ ] **Step 3:** Implement. The `NightSession` clock is the sum of the Tick dts (deterministic, no wall time).
- [ ] **Step 4:** `run-tests.ps1` → all pass.
- [ ] **Step 5:** Commit `feat(core): night session, meeting, spy power and game session rules`.

### Task 2: Player life states, mini ghost, actions and soul orb
**Files:**
- Modify: `Game/Player/PlayerLife.cs`:
  - `NetworkVariable<LifeState> State` (server-write) replaces `Dead`; keep `Grabbed`
  - `IsAlive`; `CanMove` (Alive, or MiniGhost; not Grabbed, not Escaped); `CanAct` (Alive and not Grabbed)
  - server: `SetState(LifeState)`, `Kill()` (→ MiniGhost via `NightDirector` if present, else the M4 behaviour), remove the 5 s respawn when a NightDirector exists
  - per state on all peers: Alive shows the body; MiniGhost shows `MiniGhostVisual` and sets layer 9; Escaped shows the body (frozen)
- Modify: `PlayerMotor` (MiniGhost: no gravity, Jump = up, Crouch = down at 2 m/s; `Stamina.Refill` on revive / night start via `public void ResetForNight()`; `TeleportRpc(Vector3, Quaternion)` Server→Owner), plus `PlayerHands`, `PlayerPush`, `PlayerNoise`, `PlayerFlashlight`, which gate on `CanAct` (Boo: Z when MiniGhost → `BooRpc`, 8 s cooldown, server raises `BOO` and a `BooClientRpc` to all for sound)
- Create: `Game/Player/PlayerActions.cs` (NetworkBehaviour):
  - owner: E on an aimed `Interactable` (checked before PlayerHands' grab), Q / X powers, `RequestVote(ulong? target)`
  - RPCs (Owner→Server): `InteractRpc(NetworkObjectReference station, int part)`, `PowerRpc(SpyPower)`, `VoteRpc(bool skip, ulong target)`
  - every RPC is forwarded to `NightDirector.Instance` with the sender id
  - test hooks: `RequestInteract(Interactable)`, `RequestPower(SpyPower)`
- Create: `Game/World/Interactable.cs` (`NetworkBehaviour Station`, `int Part`, `string Label`) and `Game/World/IStation.cs` (`void ServerInteract(ulong clientId, int part)`)
- Create: `Game/Player/PlayerLayers.cs` (`[RuntimeInitializeOnLoadMethod]` sets the IgnoreLayerCollision pairs; constants `Player = 8`, `MiniGhost = 9`)
- Modify: `Game/Items/GrabbableItem.cs`:
  - `Home` pose recorded on spawn; `ResetHome()` (server)
  - `NetworkVariable<bool> Locked` (no grabs)
  - `IReadOnlyList<ulong> LastHolders` (server; the holders at the last release)
- Create: `Editor/Builders/SoulOrbPrefabBuilder.cs` → `Prefabs/Items/SoulOrb.prefab` (light GrabbableItem, glowing sphere r 0.18, point light), registered in NetworkPrefabBuilder
- Modify: `PlayerPrefabBuilder` (layer 8 on root, `MiniGhostVisual` child: white sphere with eyes, inactive; PlayerActions)
- Test: compile; `player-smoke.ps1` and `ghost-smoke.ps1` still pass (no NightDirector → M4 behaviour).

- [ ] **Step 1:** Implement. `compile.ps1` → OK.
- [ ] **Step 2:** Build; player-smoke + ghost-smoke → OK.
- [ ] **Step 3:** Commit `feat(player): mini ghost state, player actions, soul orb`.

### Task 3: Stations, zones and ritual props in the House
**Files:**
- Create: `Game/World/SealStation.cs`:
  - abstract NetworkBehaviour, IStation
  - `int StationIndex`, `bool TwoPerson`
  - `NetworkVariable<bool> Active`, `Solved`
  - abstract `IPuzzle Puzzle`; `ResetForNight(IRandom rng, bool active)`; `bool Sabotage(IRandom, ulong)`; `IReadOnlyCollection<ulong> Contributors`
  - event `Action<SealStation> SolvedServer`
- Create: `Game/World/SafeStation.cs`:
  - keypad: parts 0–9 = digits, 10 = clear
  - `NetworkVariable<int> Code` (4 digits packed); `NetworkVariable<int> EnteredCount`
  - `Transform Clue`: a TextMesh note showing the code on every peer
  - a sabotaged code updates the note
- Create: `Game/World/FuseStation.cs`:
  - parts 0, 1 = levers (each lever is an Interactable child placed in another room)
  - lever pull uses `NetworkManager.ServerTime.Time`
  - prop: a non-held GrabbableItem within 0.6 m of a lever, checked by the server each 0.25 s
  - lever visuals rotate when pulled or propped (`NetworkVariable<byte> LeverState`)
- Create: `Game/World/RitualAltar.cs`:
  - IStation-free NetworkBehaviour; bounds 2 × 1.5 × 2
  - server: a non-held required item inside → `Deliver` → lock it on the altar; sabotage knocks one off → `ResetHome`
  - `NetworkVariable<int> Delivered`, `string[] RequiredIds`
- Create: `Game/World/ShrineBell.cs`: IStation, part 0 → `NightDirector.RingBell`
- Create: `Game/World/NightZone.cs`: `string ZoneName`, `Bounds` via center/size; `static NightZone Find(Vector3)`
- Create: `Game/World/MeetingSpot.cs`: `int Index`
- Modify: `Editor/Builders/HouseBuilder.cs` places:
  - 3 SafeStations (bedroom / under-house / kitchen) with clue notes in other rooms
  - 3 FuseStations with lever pairs in two rooms each
  - RitualAltar at (−6, 0, −12)
  - `incense` light item in the shrine room
  - ShrineBell on the spirit house
  - 5 NightZones: Under, West, Kitchen, Terrace, Yard
  - 8 MeetingSpots on a 3 m circle around the shrine
  - every TextMesh uses `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`
- Test: builders twice → no diff; HouseBuilder reachability check extended to every station part and the altar.

- [ ] **Step 1:** Implement. Compile; run builders twice → `git status` clean after the first commit.
- [ ] **Step 2:** Commit `feat(map): seal stations, ritual altar, shrine bell, zones`.

### Task 4: NightDirector, spy powers, meeting, HUD
**Files:**
- Create: `Game/Night/NightDirector.cs` (in-scene NetworkBehaviour, `static Instance`):
  - NetworkVariables: `Phase` (SessionPhase), `NightNumber`, `NightsTotal`, `SecondsLeft` (float, updated 4×/s), `SealsDone`, `SealsTotal`, `RitualDelivered`, `RitualRequired`, `GateOpen`, `MeetingPhase` (byte), `MeetingTimeLeft`
  - server flow: Lobby → (`StartGame()` from the host HUD or `-autostart`) → per night `BeginNight` → `InNight` → `EndNight` → `Summary` (12 s) → next night or `GameOver` (15 s) → Lobby. Sets `ConnectionService.Phase`.
  - `BeginNight`:
    - new NightSession
    - every player Alive, `ResetForNight` via `TeleportRpc` to its spawn point
    - items home; stations reset; `SealPlanner` picks
    - `GhostTargeting.IsHarmable = session.IsHarmable`
    - ghost spawns (unless `-noghost`)
    - `RoleRpc` to each client (targeted)
  - per tick:
    - session.Tick
    - ghost modifiers → `GhostController.Brain.SpeedMultiplier` / `HearingMultiplier`
    - ghost paused during meetings (`GhostController.Paused`)
    - escape checks; soul-orb revive checks (3 s hold within 3 m of the shrine)
    - meeting outcome → state changes + `MeetingResultRpc`
  - `EndNight`: awards → GameSession, `SummaryRpc(string)` to all (winner, reason, Spy ids, sabotage count, per-player coins/total), despawn the ghost and orbs, cancel grabs
  - API for PlayerActions: `ServerInteract(ulong client, NetworkObjectReference station, int part)` (range 2.5 m, `CanAct`), `ServerPower(ulong, SpyPower)`, `ServerVote(ulong, ulong?)`, `RingBell(ulong)`
  - API for GhostController/PlayerLife: `OnPlayerKilled(ulong, Vector3)` (→ MiniGhost + soul orb), `IsChasing(ulong)` (asks the ghost)
  - Powers:
    - sabotage: nearest active unsolved SealStation or the altar ≤ 3 m
    - blackout: `NightZone.Find(spy position)`; counted if any living crew member is in the zone; `BlackoutRpc(zoneIndex, 10)` to all
    - every power use → `SpyTraceRpc(spyId)` to all
- Modify: `GhostController` (`bool Paused` → speed 0 and the brain does not tick; `bool IsChasing(ulong)`), `GhostSpawner` (spawn/despawn on request: `SpawnGhost()`, `DespawnGhost()`; no auto-spawn when a NightDirector exists)
- Create: `Game/Night/NightHud.cs` (IMGUI in Bootstrap Services):
  - status line, role banner, interact hint, host "เริ่มเกม (Start)" in Lobby
  - Vote panel: buttons per living player + skip; frees the cursor while voting
  - summary and ranking panels
- Create: `Game/Player/SpyTraceView.cs` (cold-breath puff on the player for 3 s) and `Game/World/BlackoutReceiver` logic inside NightDirector's client side (disables lights in zone bounds for 10 s)
- Modify: `LaunchArgs` + `LaunchArgsTests` (`AutoStart`, `Nights`, `NightSeconds` float[], `NoSpy`, `Spies`, `NoGhost`, `MeetingScale`, `Seed`); `SceneBuilder`/`HouseBuilder` (NightDirector in-scene object, NightHud in Bootstrap Services)

- [ ] **Step 1:** Write `LaunchArgsTests` (`Parse_NightFlags`, `NightSeconds_CommaList`, `Bad_NightSeconds_Ignored`) → FAIL → implement → PASS.
- [ ] **Step 2:** Implement the rest. Compile; builders twice no diff; build `BOOTRAYAL_SMOKE_OK`; ghost-smoke and player-smoke OK.
- [ ] **Step 3:** Commit `feat(night): night director, spy powers, meetings, mini ghosts, HUD`.

### Task 5: Night smoke tests
**Files:**
- Create: `tools/unity/night-smoke.ps1 -Scenario Coop|Spy`
- Modify: `Game/Dev/SmokeProbe.cs`: `WalkPath(Vector3)` along NavMesh corners; roles `coop` and `spybot`; `Bot` helpers to interact, vote, power, grab

**Coop** (host + 1 client, `-autostart 2 -nights 2 -nightseconds 200,15 -noghost -seed 7`; both instances run role `coop`):
- Night 1: every active seal gets solved.
  - Safes go to the lower-id bot, which reads `SafeStation.Code`.
  - Fuse boxes: lower id takes lever 0, higher id lever 1. Each pulls when both are within 1.2 m of their levers, at the next whole even second of ServerTime.
- Ritual: the lower-id bot drags the urn to the altar while the other carries the incense. Then both walk out of the gate.
- Expect:
  - `SEAL_SOLVED` × seals
  - `RITUAL_DELIVERED` × 2, `GATE_OPEN`, `PLAYER_ESCAPED` × 2
  - `NIGHT_END night=1 winner=Crew reason=escaped`
  - `AWARD` lines with coins > 0 (each ≥ 50 + 50)
  - `PHASE phase=Summary`
  - `NIGHT_END night=2 winner=None reason=time_up`
  - `GAME_OVER ranking=`, `PHASE phase=Lobby`
  - both client logs have `SUMMARY_RECEIVED night=1` and `ROLE_RECEIVED role=Crew`

**Spy** (host + 4 clients, `-autostart 5 -nights 2 -nightseconds 45,40 -noghost -meetingscale 0.1 -seed 11`; all run role `spybot`):
- Night 1:
  - A crew bot enters 2 correct digits on an active safe.
  - The Spy walks there and uses Q (`POWER_USED power=SabotagePuzzle counted=True`), then X in its zone with a crew member present (`counted=True`).
  - Crew bots log `SPY_TRACE id=<spy>`.
  - A crew bot rings the bell (`MEETING_START`). After the vote opens, crew bots vote for the id they saw in `SPY_TRACE` and the Spy votes skip.
  - Expect `MEETING_RESULT ejected=<spy> spy=True`, `PLAYER_MINIGHOST id=<spy> cause=vote`, then `NIGHT_END night=1 winner=None reason=time_up`.
- Night 2:
  - The lowest-id crew bot rings; everyone except the Spy votes for the ringer, and the Spy votes skip.
  - Expect `MEETING_RESULT ejected=<ringer> spy=False`. A crew bot grabs the ringer's soul orb at the shrine and holds it → `PLAYER_REVIVED id=<ringer>`.
  - Time runs out with the Spy uncaught → `NIGHT_END night=2 winner=Spies reason=time_up`, and an `AWARD id=<spy2>` of at least 150 coins.
  - `GAME_OVER`.
- Privacy and errors:
  - Exactly one `ROLE_RECEIVED role=Spy` per night across the 5 logs, and it matches `NIGHT_ROLES`.
  - No crew log mentions the Spy in `ROLE_RECEIVED`.
  - No exceptions anywhere.

- [ ] **Step 1:** Implement the probes and the script.
- [ ] **Step 2:** Build → `night-smoke.ps1 -Scenario Coop` → `NIGHT SMOKE OK`; `-Scenario Spy` → `NIGHT SMOKE OK`; ghost-smoke, player-smoke, net-smoke Local → OK.
- [ ] **Step 3:** Commit `test(night): coop and spy night smoke tests`.

### Task 6: Review, docs, merge
- [ ] Final review (requesting-code-review), fix Critical/Important with tests, re-run all smokes.
- [ ] Ledger `docs/plans/2026-10-23-m5-ledger.md`, PROGRESS.md M5 section, merge `m5-night-loop` → main, push.
