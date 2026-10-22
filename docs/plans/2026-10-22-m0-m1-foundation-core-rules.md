# M0 + M1: Foundation and Core Rules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the template Unity project into a script-built, testable, buildable base (M0), and implement every BOO-TRAYAL game rule as pure, unit-tested C# (M1).

**Architecture:** Rules live in `BooTrayal.Core`, a pure C# assembly with `noEngineReferences: true`, so EditMode tests run fast and later Netcode code only wires rules to the network. Everything in the Unity project (settings, scenes, build) is produced by Editor scripts that run through `Unity.exe -batchmode -executeMethod`. PowerShell wrappers in `tools/unity/` run Unity and fail loudly on `error CS`, exceptions or failed tests.

**Tech Stack:** Unity 6000.6.7f1, URP 17.6.0, Unity Test Framework (NUnit), PowerShell 5.1, packages pinned in `docs/PLAN.md`.

**Spec:** `docs/GDD.md` (approved 2026-10-22); roadmap `docs/PLAN.md`.

## Global Constraints

- Unity: `C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe`; project: `X:\GameByClaude\GameByClaude`.
- Batchmode cannot open a project that an open Editor holds. Check `Get-Process Unity` first; if it is running, stop and ask the user to close it.
- Every batchmode run passes `-logFile`. A run counts as passing only when the log has no `error CS`, no unhandled `Exception`, and the exit code is 0.
- Root namespace `BooTrayal`; Core types in `BooTrayal.Core`. Code, comments and file names are in English.
- Players: 2–8. Spy only when players ≥ 5. 5–6 players: 1 Spy (host may disable). 7–8 players: 1 Spy by default, host may set 2. Two Spies know each other.
- Night length options 8/10/12/15 minutes, default 10. Nights per game 1/3/5, default 3.
- Seals per night: 2 players → 3, 3–4 → 4, 5–6 → 5, 7–8 → 6.
- Sabotage goal: 3 successes (1 Spy), 4 combined (2 Spies).
- Coins: seal closed +20, ritual item delivered +10, escaped +50, crew win +50 each, Spy win +150, correct exorcise vote +30 per correct voter. Total score = coins earned; spending never lowers it.
- Fear radii 25 / 15 / 8 m. Adrenaline +25% at 15 m rising to +40% at ≤ 5 m; stamina drain ×1.5 while active; after the ghost stays beyond 25 m for 3 s, Winded −20% speed for 4 s.
- Meeting: 45 s discussion + 15 s vote; one bell per living player per night; no bell while being chased.
- Exorcise correct: ghost speed ×0.8 for the rest of the night, +60 s on the clock. Wrong: ghost enraged 30 s (speed ×1.2, hearing ×1.5), ejected player becomes a mini ghost.
- Rejoin only between nights (lobby / summary / shop). Joining mid-night is refused.
- Version check: host and client versions must match exactly.
- Do not commit `Library/`, `Temp/`, `Logs/`, `Builds/`, `UserSettings/`, zips or any secret.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Player count at the Spy threshold:** 4 players with Spy enabled must give 0 Spies, and 5 must give 1. With 8 players and 2 Spies requested, both Spies must be distinct. → tests in Task 6 and Task 7.
2. **Vote edge cases:** all skip, ties, non-voters, votes for a player who is not eligible (dead or disconnected), and a self-vote. A person expects "no clear majority → nobody ejected". → tests in Task 8.
3. **Disconnects mid-night:** the last living crewmate disconnects → night ends, crew lose. The Spy disconnects → that Spy is treated as caught and loses the night. → tests in Task 10.
4. **Sabotage on puzzles without progress, or already solved:** this must not count toward the Spy goal and must not un-solve a seal. → tests in Task 13 and Task 14.
5. **A two-person puzzle with only one living player:** the fuse box must be solvable when one lever is propped by an item. → test in Task 14.

---

## File Map

| File | Responsibility |
|---|---|
| `tools/unity/UnityCommon.ps1` | Unity path, project path, `Invoke-Unity`, log scan |
| `tools/unity/compile.ps1` | open project in batchmode, fail on compile errors |
| `tools/unity/run-tests.ps1` | run EditMode/PlayMode tests, parse NUnit XML |
| `tools/unity/run-method.ps1` | run any `-executeMethod` |
| `tools/unity/build-windows.ps1` | build Windows x64 and run the smoke test |
| `Assets/_Project/Scripts/Core/BooTrayal.Core.asmdef` | pure C# assembly |
| `Assets/_Project/Scripts/Core/GameVersion.cs` | version constant and compatibility check |
| `Assets/_Project/Scripts/Core/Random/IRandom.cs`, `SeededRandom.cs` | injectable RNG |
| `Assets/_Project/Scripts/Core/Rules/RoomRules.cs`, `GameConfig.cs` | room config, Spy and seal counts |
| `Assets/_Project/Scripts/Core/Rules/RoleAssigner.cs` | Spy assignment |
| `Assets/_Project/Scripts/Core/Rules/VoteResolver.cs` | meeting vote result |
| `Assets/_Project/Scripts/Core/Rules/SabotageTracker.cs` | Spy goal counter |
| `Assets/_Project/Scripts/Core/Rules/NightJudge.cs`, `PlayerStatus.cs` | night end and winner |
| `Assets/_Project/Scripts/Core/Rules/MeetingRules.cs`, `ExorciseEffects.cs` | bell and exorcise results |
| `Assets/_Project/Scripts/Core/Rules/JoinPolicy.cs` | who may join when |
| `Assets/_Project/Scripts/Core/Economy/ScoreRules.cs`, `Wallet.cs` | coins and score |
| `Assets/_Project/Scripts/Core/Fear/FearModel.cs` | fear intensities by distance |
| `Assets/_Project/Scripts/Core/Fear/AdrenalineModel.cs`, `Stamina.cs` | speed and stamina |
| `Assets/_Project/Scripts/Core/Puzzles/IPuzzle.cs`, `SafeCodePuzzle.cs`, `FuseBoxPuzzle.cs`, `RitualOfferingPuzzle.cs` | puzzle logic |
| `Assets/_Project/Scripts/Game/BooTrayal.Game.asmdef`, `Bootstrap.cs` | runtime assembly, smoke-test hook |
| `Assets/_Project/Scripts/UI/BooTrayal.UI.asmdef` | UI assembly (empty for now) |
| `Assets/_Project/Editor/BooTrayal.Editor.asmdef` | editor assembly |
| `Assets/_Project/Editor/Builders/ProjectSetupBuilder.cs` | player/project settings by code |
| `Assets/_Project/Editor/Builders/SceneBuilder.cs` | builds `Bootstrap.unity`, sets build scenes |
| `Assets/_Project/Editor/BuildPipeline/WindowsBuild.cs` | Windows x64 build entry point |
| `Assets/_Project/Tests/EditMode/BooTrayal.Tests.EditMode.asmdef` + `*Tests.cs` | Core tests |

---

## M0 — Foundation

### Task 1: Clean template and install packages

**Files:**
- Modify: `Packages/manifest.json`
- Delete: `Assets/TutorialInfo/`, `Assets/Readme.asset`, `Assets/Scenes/SampleScene.unity` (+ `.meta`); commit the already-pending deletion of `Assets/Editor/HubForceResolve.cs`
- Modify: `.gitignore` (add `Builds/`, `*.zip`, `TestResults/`)

**Interfaces:**
- Produces: packages `com.unity.netcode.gameobjects@2.13.3`, `com.unity.services.multiplayer@2.4.1`, `com.unity.services.vivox@16.12.1`, `com.unity.localization@1.5.13`, `com.unity.ai.navigation@2.0.16`, `com.unity.test-framework` (keep the editor-bundled version if present, else `1.4.6`).

- [ ] **Step 1:** Check that Unity is not running: `Get-Process Unity -ErrorAction SilentlyContinue` returns nothing.
- [ ] **Step 2:** Edit `manifest.json`: remove `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.pipeline` and `com.unity.collab-proxy`, then add the packages above.
- [ ] **Step 3:** Delete the template files. Keep `Assets/Settings/` (URP assets) and `Assets/InputSystem_Actions.inputactions`.
- [ ] **Step 4:** Run `Unity.exe -batchmode -nographics -quit -projectPath <proj> -logFile Logs/m0-t1.log` (first import, can take 10+ min; run in background).
  Expected: exit 0, log has no `error CS`, and `Packages/packages-lock.json` lists the pinned versions.
- [ ] **Step 5:** Commit `chore: remove template content, pin multiplayer/voice/localization packages` (include `packages-lock.json` and all `.meta` deletions).

### Task 2: PowerShell Unity wrappers

**Files:**
- Create: `tools/unity/UnityCommon.ps1`, `compile.ps1`, `run-tests.ps1`, `run-method.ps1`

**Interfaces:**
- Produces:
  - `compile.ps1` → exit 0 when clean; prints the `error CS` lines and exits 1 otherwise.
  - `run-tests.ps1 -Platform EditMode|PlayMode [-Filter <regex>]` → writes `TestResults/<platform>.xml`, prints `passed/failed/total`, and exits 1 when any test fails or the XML is missing.
  - `run-method.ps1 -Method <Full.Type.Method> [-ExtraArgs <string[]>]` → exit 1 on log errors.
  - All of them fail fast with "Close the Unity Editor first" when `Unity` is running.

- [ ] **Step 1:** Implement `UnityCommon.ps1` with `$UnityExe` and `$ProjectPath` (constants from Global Constraints), `Assert-EditorClosed`, `Invoke-Unity([string[]]$args, [string]$logName) -> int exitCode`, and `Test-UnityLog([string]$logPath) -> string[] problems` (matches `error CS\d+`, `^\s*\w*Exception:`, `Scripts have compiler errors`).
- [ ] **Step 2:** Implement `compile.ps1` (`-quit`), `run-tests.ps1` (`-runTests -testPlatform X -testResults ...`, no `-quit`; parse `test-run` attributes `total`, `passed`, `failed`), and `run-method.ps1` (`-quit -executeMethod`).
- [ ] **Step 3:** Run `tools/unity/compile.ps1`. Expected: `Compile OK`, exit 0.
- [ ] **Step 4:** Commit `build: add PowerShell batchmode wrappers for compile, tests, methods`.

### Task 3: Assemblies, version constant, first test

**Files:**
- Create: the four asmdefs from the File Map; `Core/GameVersion.cs`; `Tests/EditMode/GameVersionTests.cs`

**Interfaces:**
- Produces:
  - `BooTrayal.Core` asmdef: `noEngineReferences: true`, `autoReferenced: true`, no references.
  - `BooTrayal.Game` references `BooTrayal.Core`, `Unity.Netcode.Runtime`, `Unity.Services.Multiplayer`, `Unity.Services.Core`, `Unity.Services.Authentication`, `Unity.Services.Vivox`, `Unity.InputSystem`, `Unity.AI.Navigation`, `Unity.RenderPipelines.Universal.Runtime`. Copy the exact assembly names from each package's `.asmdef` under `Library/PackageCache/` (they can differ from the package ID).
  - `BooTrayal.UI` references Core and Game.
  - `BooTrayal.Editor` (Editor platform only) references Core, Game and UI.
  - `BooTrayal.Tests.EditMode` (Editor only, `UNITY_INCLUDE_TESTS`, `nunit.framework.dll`) references Core.
  - `public static class GameVersion { public const string Current = "0.1.0"; public static bool IsCompatible(string host, string client); }`: exact ordinal match after trimming; null or empty → false.

- [ ] **Step 1: Write the failing tests** in `GameVersionTests`:
  - `IsCompatible_SameVersion_True`: `("0.1.0","0.1.0")` → true
  - `IsCompatible_DifferentPatch_False`: `("0.1.0","0.1.1")` → false
  - `IsCompatible_Whitespace_Trimmed`: `(" 0.1.0","0.1.0 ")` → true
  - `IsCompatible_NullOrEmpty_False`: `(null,"0.1.0")`, `("","")` → false
  - `Current_IsSemver`: matches `^\d+\.\d+\.\d+$`
- [ ] **Step 2:** Run `tools/unity/run-tests.ps1 -Platform EditMode`. Expected: compile failure because `GameVersion` does not exist.
- [ ] **Step 3:** Implement `GameVersion`.
- [ ] **Step 4:** Run the tests. Expected: 5 passed.
- [ ] **Step 5:** Commit `feat(core): assemblies and game version check`.

### Task 4: Project settings and Bootstrap scene by code

**Files:**
- Create: `Editor/Builders/ProjectSetupBuilder.cs`, `Editor/Builders/SceneBuilder.cs`, `Game/Bootstrap.cs`
- Output (generated, committed): `ProjectSettings/*`, `Assets/_Project/Scenes/Bootstrap.unity`

**Interfaces:**
- Produces:
  - `ProjectSetupBuilder.Apply()` (static, `-executeMethod BooTrayal.Editor.Builders.ProjectSetupBuilder.Apply`) sets: companyName `LumielX`, productName `BOO-TRAYAL`, bundleVersion = `GameVersion.Current`, `PlayerSettings.SetScriptingBackend(Standalone, Mono2x)`, `fullScreenMode = FullScreenWindow`, `runInBackground = true` (needed for multi-instance tests), `visibleInBackground = true`, `ColorSpace.Linear`, active input handler = Input System, default resolution 1920×1080, `resizableWindow = true`, `usePlayerLog = true`.
  - `SceneBuilder.BuildBootstrap()` creates `Assets/_Project/Scenes/Bootstrap.unity` containing a camera and a `Bootstrap` GameObject, then sets `EditorBuildSettings.scenes = [Bootstrap]`.
  - `Bootstrap : MonoBehaviour`: on `Start`, logs `BOO-TRAYAL v{Application.version} boot`. When the command line contains `-smoketest`, it also logs `BOOTRAYAL_SMOKE_OK` and calls `Application.Quit(0)`.

- [ ] **Step 1:** Implement both builders and `Bootstrap`.
- [ ] **Step 2:** Run `run-method.ps1 -Method BooTrayal.Editor.Builders.ProjectSetupBuilder.Apply`, then `... SceneBuilder.BuildBootstrap`. Expected: exit 0; `ProjectSettings/ProjectSettings.asset` contains `productName: BOO-TRAYAL` and `bundleVersion: 0.1.0`; the scene file exists.
- [ ] **Step 3:** Run both again. Expected: `git status` shows no further changes (the builders are idempotent).
- [ ] **Step 4:** Commit `feat(editor): project settings and bootstrap scene builders`.

### Task 5: Windows build + smoke test

**Files:**
- Create: `Editor/BuildPipeline/WindowsBuild.cs`, `tools/unity/build-windows.ps1`

**Interfaces:**
- Produces:
  - `WindowsBuild.Build()` builds the scenes from `EditorBuildSettings` to `Builds/Windows/BOO-TRAYAL.exe` (`StandaloneWindows64`). On a result other than `Succeeded` it calls `EditorApplication.Exit(1)`.
  - `build-windows.ps1 [-SkipSmoke]` runs ProjectSetupBuilder → SceneBuilder → WindowsBuild, then runs `BOO-TRAYAL.exe -batchmode -nographics -smoketest -logFile Builds/smoke.log`, waits ≤ 60 s, and requires `BOOTRAYAL_SMOKE_OK` in that log. With `-Zip` it also writes `Builds/BOO-TRAYAL-v<ver>-win64.zip`.

- [ ] **Step 1:** Implement both.
- [ ] **Step 2:** Run `tools/unity/build-windows.ps1` (background; first build is slow). Expected: `Builds/Windows/BOO-TRAYAL.exe` exists, the script prints `SMOKE OK`, exit 0.
- [ ] **Step 3:** Commit `build: Windows x64 build pipeline with smoke test`.
- [ ] **Step 4:** Update `docs/PROGRESS.md` (M0 done, with log evidence) and commit `docs: M0 complete`.

---

## M1 — Core Rules (pure C#, TDD)

Every M1 task follows the same cycle: write the listed tests → run `run-tests.ps1 -Platform EditMode -Filter <Class>` and see them fail → implement → run and see them pass → commit `feat(core): <thing>`. The steps below list only the tests and the signatures.

### Task 6: GameConfig, RoomRules, IRandom

**Files:** `Core/Random/IRandom.cs`, `SeededRandom.cs`, `Core/Rules/GameConfig.cs`, `RoomRules.cs`; test `RoomRulesTests.cs`

**Interfaces:**
- `public interface IRandom { int Next(int minInclusive, int maxExclusive); double NextDouble(); }`, with `SeededRandom(int seed)` wrapping `System.Random`.
- `public enum Difficulty { Easy, Normal, Hard }`
- `public sealed class GameConfig { int MaxPlayers = 8; bool SpyEnabled = true; int RequestedSpies = 1; int NightMinutes = 10; int Nights = 3; Difficulty Difficulty = Normal; }` (public get/set properties).
- `public static class RoomRules`:
  - `int SpyCount(int players, bool spyEnabled, int requestedSpies)`
  - `int SealCount(int players)`
  - `bool CanToggleSpy(int players)` → players ≥ 5
  - `int MaxSpiesAllowed(int players)` → 0 (<5), 1 (5–6), 2 (7–8)
  - `IReadOnlyList<string> Validate(GameConfig c)` → error keys: `max_players_range`, `night_minutes_invalid`, `nights_invalid`, `spies_invalid`
  - constants `MinPlayers = 2`, `MaxPlayers = 8`, `SpyMinPlayers = 5`, `int[] NightMinuteOptions = {8,10,12,15}`, `int[] NightOptions = {1,3,5}`

- [ ] Tests:
  - `SpyCount_FourPlayersEnabled_Zero` → `SpyCount(4,true,1) == 0`
  - `SpyCount_FivePlayers_One` → `SpyCount(5,true,1) == 1`; `SpyCount(5,true,2) == 1`
  - `SpyCount_Disabled_Zero` → `SpyCount(6,false,1) == 0`
  - `SpyCount_EightPlayersTwoRequested_Two` → `SpyCount(8,true,2) == 2`; `SpyCount(7,true,1) == 1`
  - `SealCount_ByPlayers` → 2→3, 3→4, 4→4, 5→5, 6→5, 7→6, 8→6
  - `Validate_Defaults_NoErrors`
  - `Validate_NineMaxPlayers_Error` → contains `max_players_range`; `MaxPlayers = 1` also errors
  - `Validate_NightMinutes9_Error`, `Validate_Nights2_Error`
  - `Validate_TwoSpiesWithMax6_Error` → contains `spies_invalid`

### Task 7: RoleAssigner

**Files:** `Core/Rules/RoleAssigner.cs`; test `RoleAssignerTests.cs`

**Interfaces:**
- `public enum Role { Crew, Spy }`
- `public sealed class RoleAssignment { IReadOnlyDictionary<ulong, Role> Roles; IReadOnlyList<ulong> Spies; Role RoleOf(ulong id); IReadOnlyList<ulong> SpyPartnersOf(ulong id) /* other spies; empty for crew */ }`
- `public static RoleAssignment RoleAssigner.Assign(IReadOnlyList<ulong> players, int spyCount, IRandom rng)`: throws `ArgumentException` when `spyCount > players.Count - 1` or there are duplicate IDs. Uses a Fisher–Yates shuffle of a copy; the input list is not mutated.

- [ ] Tests:
  - `Assign_ZeroSpies_AllCrew` (4 players)
  - `Assign_OneSpy_ExactlyOneSpy` (5 players)
  - `Assign_TwoSpies_DistinctAndPartnersKnowEachOther` (8 players; `SpyPartnersOf(spyA) == [spyB]`; crew `SpyPartnersOf` empty)
  - `Assign_SameSeed_SameResult`; `Assign_ManySeeds_EveryPlayerCanBeSpy` (1000 seeds × 5 players: every ID was a Spy at least once)
  - `Assign_DoesNotMutateInput`; `Assign_TooManySpies_Throws`; `Assign_DuplicateIds_Throws`

### Task 8: VoteResolver

**Files:** `Core/Rules/VoteResolver.cs`; test `VoteResolverTests.cs`

**Interfaces:**
- `public readonly struct VoteResult { bool Ejected; ulong? Target; IReadOnlyDictionary<ulong,int> Tally; int SkipCount; }`
- `public static VoteResult VoteResolver.Resolve(IReadOnlyCollection<ulong> eligibleVoters, IReadOnlyCollection<ulong> eligibleTargets, IReadOnlyDictionary<ulong, ulong?> ballots)`
  - `ballots[voter] = null` means skip. An eligible voter without a ballot counts as skip.
  - Ballots from non-eligible voters are ignored. A ballot for a non-eligible target counts as skip.
  - Self-votes are allowed.
  - Ejected ⇔ the top target count > `SkipCount` **and** that top is unique.

- [ ] Tests:
  - `AllSkip_NoEjection`
  - `ClearMajority_Ejects` (5 voters: 3→A, 1→B, 1 skip → A)
  - `Tie_NoEjection` (2→A, 2→B, 1 skip)
  - `TopEqualsSkip_NoEjection` (2→A, 2 skip, 1→B)
  - `NonVoters_CountAsSkip` (5 voters, only 2 ballots, both → A: skip = 3 → no ejection)
  - `IneligibleVoter_Ignored`; `IneligibleTarget_CountsAsSkip`; `SelfVote_Counts`
  - `Tally_ReportsCounts`

### Task 9: SabotageTracker

**Files:** `Core/Rules/SabotageTracker.cs`; test `SabotageTrackerTests.cs`

**Interfaces:**
- `public sealed class SabotageTracker { SabotageTracker(int spyCount); int Required /* 3 for 1 spy, 4 for 2 */; int Successes; bool GoalReached; bool Record(ulong spyId, bool hadEffect) /* true when counted */; void MarkCaught(ulong spyId); bool AnyUncaught; IReadOnlyCollection<ulong> Spies; void Register(ulong spyId); }`
- Records from caught spies, from unregistered IDs, or with `hadEffect == false` are not counted. After `GoalReached`, further records still return false.

- [ ] Tests: `Required_OneSpy_3`, `Required_TwoSpies_4`, `NoEffect_NotCounted`, `CaughtSpy_NotCounted`, `ThreeSuccesses_GoalReached`, `TwoSpiesShareCounter` (2 + 2 → reached at 4), `UnregisteredId_NotCounted`, `AnyUncaught_FalseWhenAllCaught`

### Task 10: PlayerStatus + NightJudge

**Files:** `Core/Rules/PlayerStatus.cs`, `NightJudge.cs`; test `NightJudgeTests.cs`

**Interfaces:**
- `public enum LifeState { Alive, MiniGhost, Escaped, Disconnected }`
- `public sealed class NightPlayer { ulong Id; Role Role; LifeState State; bool Exposed /* spy caught */; }`
- `public enum NightWinner { None, Crew, Spies }`
- `public readonly struct NightVerdict { bool Ended; NightWinner Winner; string Reason /* "escaped" | "time_up" | "crew_wiped" | "sabotage_goal" | "spies_gone" */; }`
- `public static NightVerdict NightJudge.Evaluate(IReadOnlyList<NightPlayer> players, bool ritualComplete, double secondsLeft, SabotageTracker sabotage)`. Rules, in this order:
  1. The sabotage goal has been reached and at least one Spy is uncaught → winner `Spies`, reason `sabotage_goal`. The night is **not** ended, because the crew keep playing for escape coins. (`Ended=false`, `Winner=Spies`)
  2. No crew with `Alive` and none with `Escaped` → ended, `Spies` (`crew_wiped`). In co-op (no spies): ended, `None`.
  3. Ritual complete and ≥ 1 crew `Escaped`, and no crew left `Alive` inside → ended. Winner `Crew` unless rule 1 already gave `Spies`.
  4. `secondsLeft <= 0` → ended. `Crew` if the ritual is complete and ≥ 1 crew escaped; otherwise `Spies` (co-op: `None`), reason `time_up`.
  5. Otherwise → not ended. Winner `Spies` if rule 1 applied, else `None`.
- A Spy that disconnects is treated as exposed: the caller calls `sabotage.MarkCaught`. That Spy can never be the winning side's member (handled by scoring in Task 11).

- [ ] Tests: `CrewEscaped_RitualDone_CrewWins`, `AllCrewDead_SpiesWin_crew_wiped`, `LastCrewDisconnects_SpiesWin`, `TimeUp_NoRitual_SpiesWin`, `TimeUp_RitualDoneSomeEscaped_CrewWins`, `SabotageGoal_SpiesWinButNightContinues`, `CoOp_TimeUp_WinnerNone`, `CrewStillInside_AfterSomeEscaped_NotEnded`

### Task 11: ScoreRules + Wallet

**Files:** `Core/Economy/ScoreRules.cs`, `Wallet.cs`; test `EconomyTests.cs`

**Interfaces:**
- `public static class ScoreRules { const int SealClosed=20, RitualItemDelivered=10, Escaped=50, CrewWinEach=50, SpyWin=150, CorrectVoteEach=30; }`
- `public sealed class Wallet { int Coins; int TotalScore; void Earn(int amount) /* amount>0 else ArgumentOutOfRange */; bool TrySpend(int amount); }`
- `public static IReadOnlyDictionary<ulong,int> ScoreRules.NightEndAwards(IReadOnlyList<NightPlayer> players, NightWinner winner, IReadOnlyCollection<ulong> correctVoters)`:
  - Crew who escaped get +50.
  - When `winner == Crew`, every crew member gets +50, dead ones included.
  - When `winner == Spies`, every **uncaught, connected** Spy gets +150.
  - Every ID in `correctVoters` gets +30.

- [ ] Tests: `Spend_DoesNotLowerScore`, `TrySpend_Insufficient_False`, `Earn_NonPositive_Throws`, `CrewWin_AllCrewGetBonus_EscapedGetExtra`, `SpyWin_OnlyUncaughtSpyPaid`, `DisconnectedSpy_NotPaid`, `CorrectVoters_Paid`

### Task 12: FearModel, Stamina, AdrenalineModel

**Files:** `Core/Fear/FearModel.cs`, `Stamina.cs`, `AdrenalineModel.cs`; tests `FearModelTests.cs`, `AdrenalineTests.cs`

**Interfaces:**
- `public readonly struct FearLevels { float Whisper, Heartbeat, Vignette, Flicker, Shake, Chromatic; }` (all values 0..1)
- `public static FearLevels FearModel.Evaluate(float distance)`:
  - `Whisper` ramps 0 → 1 from 25 m to 15 m.
  - `Heartbeat`, `Vignette` and `Flicker` ramp 0 → 1 from 15 m to 8 m.
  - `Shake` and `Chromatic` ramp 0 → 1 from 8 m to 2 m.
  - Use a linear ramp clamped to 0..1, then smoothstep. Values are continuous: no jumps at 25, 15 or 8 m. Distance < 0 or NaN is treated as ∞ (no fear).
- `public sealed class Stamina { const float Max=100, RunDrainPerSec=20, RegenPerSec=15, RegenDelay=1; float Value; bool CanRun /* Value > 0, hysteresis: after hitting 0 needs 25 */; void Tick(float dt, bool running, float drainMultiplier); }`
- `public enum AdrenalineState { Normal, Adrenaline, Winded }`
- `public sealed class AdrenalineModel { AdrenalineState State; float SpeedMultiplier; float StaminaDrainMultiplier; void Tick(float dt, float ghostDistance); }`
  - The speed bonus is 0 beyond 15 m. Inside 15 m it rises linearly from +0.25 at 15 m to +0.40 at ≤ 5 m.
  - The drain multiplier is ×1.5 in `Adrenaline`.
  - From `Adrenaline`, once the distance stays > 25 m for 3 s → `Winded` (speed ×0.8) for 4 s → `Normal`.
  - Coming back within 15 m while `Winded` → `Adrenaline` again.

- [ ] Tests:
  - `Fear_Far_AllZero` (30 m)
  - `Fear_20m_WhisperOnly`
  - `Fear_10m_HeartbeatPartial_NoShake`
  - `Fear_3m_ShakeHigh`
  - `Fear_Continuous_AtBoundaries` (|f(15−ε) − f(15+ε)| < 0.01 per field)
  - `Fear_NaN_NoFear`
  - `Stamina_RunDrains_RegenAfterDelay`
  - `Stamina_Exhausted_NeedsHysteresis`
  - `Adrenaline_At15m_Plus25`, `Adrenaline_At5m_Plus40`, `Adrenaline_DrainX1_5`
  - `Winded_AfterGhostLeaves3s_Lasts4s`
  - `Winded_GhostReturns_BackToAdrenaline`

### Task 13: Puzzle interface + SafeCodePuzzle

**Files:** `Core/Puzzles/IPuzzle.cs`, `SafeCodePuzzle.cs`; test `SafeCodePuzzleTests.cs`

**Interfaces:**
- `public interface IPuzzle { string Kind; bool RequiresTwoPlayers; bool Solved; float Progress01; bool TrySabotage(IRandom rng) /* true only if it removed progress; never on solved */; }`
- `public sealed class SafeCodePuzzle : IPuzzle { SafeCodePuzzle(int[] code /* length 4, digits 0-9 */); IReadOnlyList<int> Code /* what the clue shows */; IReadOnlyList<int> Entered; bool EnterDigit(int d) /* false if solved or digit invalid */; void Clear(); }`
  - Entering the 4th digit checks the code: a match solves the puzzle, a mismatch clears the entry. `Progress01` = (length of the matching prefix of `Entered`) / 4.
  - Sabotage clears `Entered` and swaps two different positions of `Code` that hold different digits, so the clue changes. It returns true only when `Progress01 > 0`.

- [ ] Tests: `CorrectCode_Solves`, `WrongCode_ClearsEntry`, `Progress_CountsCorrectPrefix`, `Sabotage_NoProgress_ReturnsFalse_CodeUnchanged`, `Sabotage_WithProgress_ClearsAndChangesCode`, `Sabotage_Solved_ReturnsFalse_StaysSolved`, `InvalidDigit_Rejected`, `Ctor_BadLength_Throws`

### Task 14: FuseBoxPuzzle + RitualOfferingPuzzle

**Files:** `Core/Puzzles/FuseBoxPuzzle.cs`, `RitualOfferingPuzzle.cs`; tests `FuseBoxPuzzleTests.cs`, `RitualOfferingPuzzleTests.cs`

**Interfaces:**
- `public sealed class FuseBoxPuzzle : IPuzzle { FuseBoxPuzzle(double syncWindow = 1.0); void Pull(int lever /*0|1*/, double time); void Prop(int lever) /* item holds lever down */; void Unprop(int lever); }`
  - It is solved when both levers were pulled within `syncWindow` of each other. A propped lever counts as pulled at every time.
  - `Progress01` = 0.5 when one lever was pulled within the last window or is propped.
  - Sabotage clears the props and the pull times. It returns true if any lever was propped or pulled recently.
- `public sealed class RitualOfferingPuzzle : IPuzzle { RitualOfferingPuzzle(IReadOnlyList<string> requiredItemIds); bool Deliver(string itemId); IReadOnlyCollection<string> Delivered; string LastSabotagedItem; }`
  - `Deliver` returns false for unknown or duplicate items. `Progress01` = delivered / required.
  - Sabotage removes one random delivered item (it goes back to its spawn) and sets `LastSabotagedItem`. It returns false when nothing has been delivered or the puzzle is solved.

- [ ] Tests:
  - `Fuse_BothWithinWindow_Solves`
  - `Fuse_OutsideWindow_NotSolved`
  - `Fuse_PropOneLever_SoloPlayerSolves` (prop 0, then pull 1 at any time → solved)
  - `Fuse_Sabotage_ClearsProps`
  - `Fuse_Sabotage_Solved_False`
  - `Ritual_DeliverAll_Solves`
  - `Ritual_DuplicateOrUnknown_Rejected`
  - `Ritual_Sabotage_RemovesOne_ReturnsTrue`
  - `Ritual_Sabotage_Empty_False`

### Task 15: MeetingRules, ExorciseEffects, JoinPolicy

**Files:** `Core/Rules/MeetingRules.cs`, `ExorciseEffects.cs`, `JoinPolicy.cs`; test `MeetingAndJoinTests.cs`

**Interfaces:**
- `public sealed class MeetingRules { const float DiscussSeconds=45, VoteSeconds=15; bool CanRing(ulong player, LifeState state, bool beingChased, bool meetingActive); void MarkRang(ulong player); void ResetForNight(); }`: the player must be `Alive`, not chased, no meeting active, and must not have rung yet this night.
- `public readonly struct GhostModifiers { float SpeedMul; float HearingMul; double Seconds /* +∞ for rest of night */; }`
- `public static class ExorciseEffects { GhostModifiers Correct /* 0.8, 1, ∞ */; GhostModifiers Wrong /* 1.2, 1.5, 30 */; const double CorrectBonusSeconds = 60; }`
- `public enum SessionPhase { Lobby, InNight, Summary, Shop, GameOver }`
- `public static bool JoinPolicy.CanJoin(SessionPhase phase, int currentPlayers, int maxPlayers, bool isRejoin)`: allowed only in `Lobby`, `Summary` or `Shop`, and only when not full. A rejoin also needs a free slot.

- [ ] Tests: `Ring_OncePerNight`, `Ring_WhileChased_Denied`, `Ring_MiniGhost_Denied`, `Ring_DuringMeeting_Denied`, `ResetForNight_AllowsAgain`, `Exorcise_Values`, `Join_InNight_Denied`, `Join_Shop_Allowed`, `Join_Full_Denied`

### Task 16: M1 wrap-up

- [ ] **Step 1:** Run `tools/unity/run-tests.ps1 -Platform EditMode`. Expected: every test passes (about 75); the XML is saved.
- [ ] **Step 2:** Run `tools/unity/build-windows.ps1`. Expected: `SMOKE OK` (Core compiles into the player).
- [ ] **Step 3:** Update `docs/PROGRESS.md` (M1 done: test count and log paths). Write the M2 detailed plan file stub name in the PLAN.md link list.
- [ ] **Step 4:** Commit `docs: M1 complete`.
