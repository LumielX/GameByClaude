# M2: Networking Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Friends can create a room, get a 6-character code and join it through Unity Relay. Mismatched versions are refused with a clear reason, host loss returns clients to the menu, and Solo mode runs offline. All of this is proven by two real game builds on one PC.

**Architecture:** Core holds pure rules: join-code format, connection payload, approval decision, player-name rules. Game holds one `ConnectionService` MonoBehaviour that wraps Unity Services (anonymous auth + Multiplayer Sessions with Relay) and the NGO `NetworkManager`. A temporary IMGUI `DebugNetMenu` and command-line flags (`-autohost`, `-autojoin`) let the PowerShell script `net-smoke.ps1` drive two built instances and read their logs. The real UI Toolkit menus come in M6.

**Tech Stack:** NGO 2.13.3, Multiplayer Services 2.4.1 (`MultiplayerService.Instance.CreateSessionAsync / JoinSessionByCodeAsync`, `SessionOptions.WithRelayNetwork()`), Services Core/Authentication, Unity Transport.

**Spec:** `docs/GDD.md` §17 (tech table) and §1.1 goal 1; roadmap `docs/PLAN.md` M2.

## Global Constraints

- Everything from the M0–M1 plan's Global Constraints still applies: batchmode wrappers, English code, commit trailer, and no secrets committed.
- Unity Cloud project: `cloudProjectId 9e9bbbe6-25f3-44da-8070-5b328fd1530b` (already in ProjectSettings), org `phutawansaichol-gmail-com`. These are not secrets.
- Max players per room: 8. Network tick rate: 30.
- Version check: exact match of `GameVersion.Current`, enforced in NGO connection approval.
- Joining is allowed only in `SessionPhase` Lobby/Summary/Shop (Core `JoinPolicy`); M2 host is always in `Lobby`.
- Two instances on one PC need different Authentication profiles. The `-profile <name>` argument selects one; the default is `main`.
- Rejection and disconnect reasons are short keys. Thai/English player text for each key lives in one table (`NetMessages`) until M6 localization.

## Review Focus

1. **Join codes typed sloppily** (lower case, spaces, a dash, `0` vs `O`): a person expects them to still work. → Task 1 tests.
2. **A client on an old version joins a newer host:** it must be refused with "please download the latest version", not hang. → Task 3 test + smoke step.
3. **The host quits mid-lobby:** clients must land on the menu with a message, not freeze. → smoke step in Task 7.
4. **The room is full (9th player):** the player is refused with a reason. → Task 3 test.
5. **Empty or very long player names, or names with odd characters:** a person expects a sane name to be shown. → Task 2 tests.

---

### Task 1: JoinCode rules (Core, TDD)

**Files:** Create `Core/Net/JoinCode.cs`; test `Tests/EditMode/JoinCodeTests.cs`

**Interfaces:** `public static class JoinCode { const int Length = 6; static string Normalize(string raw); static bool IsValid(string normalized); }` (namespace `BooTrayal.Core.Net`)
- Normalize: null → `""`; strip whitespace and `-`; upper-case (invariant).
- IsValid: exactly 6 chars, each `A–Z` or `0–9`.

- [ ] Tests: `Normalize_LowerAndSpaces` (`" ab c-12 3"` → `"ABC123"`), `Normalize_Null_Empty`, `IsValid_SixAlnum_True`, `IsValid_WrongLength_False`, `IsValid_Symbols_False`.
- [ ] RED → implement → GREEN → commit `feat(core): join code normalization`.

### Task 2: PlayerName + ConnectionPayload (Core, TDD)

**Files:** Create `Core/Net/PlayerName.cs`, `Core/Net/ConnectionPayload.cs`; tests `PlayerNameTests.cs`, `ConnectionPayloadTests.cs`

**Interfaces:**
- `public static class PlayerName { const int MaxLength = 16; static string Sanitize(string raw, int fallbackNumber); }`
  - Trim and drop control characters. Collapse runs of whitespace to one space. Cut to 16 *text elements* (do not split Thai combining marks: use `StringInfo`).
  - Empty → `"Player" + fallbackNumber` (number 1–9999).
- `public readonly struct ConnectionPayload { string Version; string PlayerName; static byte[] Encode(string version, string playerName); static bool TryDecode(byte[] data, out ConnectionPayload payload); }`
  - Format: UTF-8 of `"BT1\n{version}\n{name}"`.
  - TryDecode is false on null, more than 256 bytes, a wrong prefix, or a wrong field count.

- [ ] Tests:
  - `Sanitize_Trims`
  - `Sanitize_Empty_Fallback` → `"Player42"`
  - `Sanitize_TooLong_Cut16`
  - `Sanitize_ThaiCombining_NotSplit`: `"ก่"` × 10 → 16 text elements, and the last char is not a lone combining mark
  - `Sanitize_ControlChars_Removed`
  - `Payload_RoundTrip` (Thai name)
  - `Payload_Garbage_False`, `Payload_Null_False`, `Payload_TooBig_False`
- [ ] RED → implement → GREEN → commit `feat(core): player name rules and connection payload`.

### Task 3: ApprovalRules (Core, TDD)

**Files:** Create `Core/Net/ApprovalRules.cs`; test `ApprovalRulesTests.cs`

**Interfaces:**
- `public readonly struct ApprovalDecision { bool Approved; string Reason /* "" | "bad_payload" | "version_mismatch" | "room_full" | "game_in_progress" */; }`
- `public static ApprovalDecision ApprovalRules.Evaluate(byte[] payload, string hostVersion, SessionPhase phase, int connectedPlayers, int maxPlayers)`
  - Checks in this order: payload decodes → versions compatible (`GameVersion.IsCompatible`) → `JoinPolicy.CanJoin` (full → `room_full`, otherwise wrong phase → `game_in_progress`).

- [ ] Tests: `ValidPayload_Approved`, `BadPayload_Rejected`, `OldVersion_VersionMismatch`, `Full_RoomFull` (8 connected, max 8), `InNight_GameInProgress`, `VersionCheckedBeforeFull` (bad version + full → `version_mismatch`).
- [ ] RED → implement → GREEN → commit `feat(core): connection approval rules`.

### Task 4: NetMessages table (Game)

**Files:** Create `Game/Networking/NetMessages.cs`; test `Tests/EditMode/NetMessagesTests.cs` (the test asmdef gains a `BooTrayal.Game` reference)

**Interfaces:** `public static class NetMessages { static string Thai(string key); static string English(string key); static IReadOnlyCollection<string> Keys; }`. Keys: `version_mismatch`, `room_full`, `game_in_progress`, `bad_payload`, `host_left`, `code_invalid`, `room_not_found`, `services_unavailable`, `connect_failed`. Unknown keys fall back to `connect_failed` text.
- `version_mismatch` Thai text must include the Releases URL `https://github.com/LumielX/GameByClaude/releases/latest`.

- [ ] Tests: `EveryKey_HasThaiAndEnglish`, `VersionMismatch_HasReleaseLink`, `UnknownKey_FallsBack`.
- [ ] RED → implement → GREEN → commit `feat(game): human-readable network messages`.

### Task 5: ConnectionService (Game)

**Files:** Create `Game/Networking/ConnectionService.cs`, `Game/Networking/LaunchArgs.cs`; test `Tests/EditMode/LaunchArgsTests.cs`

**Interfaces:**
- `public sealed class LaunchArgs { static LaunchArgs Parse(string[] args); string Profile /* default "main" */; bool AutoHost; string AutoJoinCode; bool AutoHostLocal; bool AutoJoinLocal; string FakeVersion; string CodeFile; string PlayerName; }`. Flags: `-profile x`, `-autohost`, `-autojoin CODE`, `-autohost-local`, `-autojoin-local`, `-fakeversion 0.0.9`, `-codefile path`, `-name X`.
- `public enum NetStatus { Offline, SigningIn, Hosting, Joining, Connected, Failed }`
- `public sealed class ConnectionService : MonoBehaviour`:
  - `static ConnectionService Instance`; `NetStatus Status`; `string LastMessageKey`; `string RoomCode`; `int ConnectedCount` (host only); `event Action Changed`.
  - `Task<bool> CreateRoomAsync(int maxPlayers)`: init services → `CreateSessionAsync(new SessionOptions{ MaxPlayers, IsPrivate = true, SessionProperties = { "ver" } }.WithRelayNetwork())` → `RoomCode = session.Code`.
  - `Task<bool> JoinRoomAsync(string rawCode)`: `JoinCode.Normalize/IsValid` (else `code_invalid`) → set `NetworkConfig.ConnectionData = ConnectionPayload.Encode(version, name)` → `JoinSessionByCodeAsync`. Lobby "not found" → `room_not_found`.
  - `bool StartLocalHost()` / `bool StartLocalClient()`: plain NGO on `127.0.0.1:7777` with no services. These are Solo mode and the offline smoke test.
  - `Task LeaveAsync()`: leaves the session and shuts NGO down.
  - Host side: `ConnectionApprovalCallback` → `ApprovalRules.Evaluate(..., GameVersion.Current, SessionPhase.Lobby, connected, maxPlayers)`; set `response.Reason`, `CreatePlayerObject = true`.
  - Client side: `OnClientDisconnectCallback` for the local client → `LastMessageKey = DisconnectReason` when non-empty, else `host_left`; status `Offline`; leave the session.
  - Log markers (one line each, for the smoke script):
    - `NET_HOSTING code=<CODE>` (also written to `CodeFile` if set)
    - `NET_CLIENT_JOINED id=<n> count=<k>` (host)
    - `NET_CONNECTED` (client)
    - `NET_REJECTED reason=<key>`
    - `NET_HOST_LOST`
    - `NET_FAILED reason=<key>`
  - Services init uses `new InitializationOptions().SetProfile(LaunchArgs.Profile)`; anonymous sign-in. If it fails → `services_unavailable`.
  - `-fakeversion` overrides only the version sent in the payload (test hook).
- LaunchArgs tests: `Parse_Defaults`, `Parse_AllFlags`, `Parse_MissingValue_Ignored`.

- [ ] LaunchArgs: RED → implement → GREEN.
- [ ] Implement ConnectionService. Run `tools/unity/compile.ps1`. Expected: Compile OK.
- [ ] Commit `feat(game): connection service with relay sessions, approval and local mode`.

### Task 6: Network prefabs, debug menu and scene wiring (Editor builder)

**Files:** Create `Game/Networking/NetPlayerStub.cs`, `Game/Networking/DebugNetMenu.cs`, `Editor/Builders/NetworkPrefabBuilder.cs`; modify `Editor/Builders/SceneBuilder.cs`, `tools/unity/build-windows.ps1` (run the new builder first)

**Interfaces:**
- `NetworkPrefabBuilder.Build()` (idempotent) creates `Assets/_Project/Prefabs/Network/NetPlayerStub.prefab` (NetworkObject + `NetPlayerStub` which logs spawn/owner) and registers it in the default network prefab list. It also creates `Assets/_Project/Prefabs/Network/NetworkManager.prefab` with `NetworkManager` (`TickRate 30`, `ConnectionApproval true`, `PlayerPrefab = NetPlayerStub`) and `UnityTransport`.
- `SceneBuilder.BuildBootstrap()` also puts a `NetworkManager` prefab instance plus a `Services` GameObject (`ConnectionService`, `DebugNetMenu`) into Bootstrap.
- `DebugNetMenu`: IMGUI with Create / Join (code field) / Solo / Leave buttons, status, room code, connection count and the last message (Thai). On start it obeys `LaunchArgs` auto flags.

- [ ] Run both builders twice. Expected: second run leaves `git status` clean.
- [ ] Build (`build-windows.ps1`). Expected: SMOKE OK.
- [ ] Commit `feat(game): network prefabs, debug network menu, bootstrap wiring`.

### Task 7: Two-instance smoke test (local + Relay)

**Files:** Create `tools/unity/net-smoke.ps1`

**Interfaces:** `net-smoke.ps1 -Mode Local|Relay` launches the built exe twice in `-batchmode -nographics` with logs under `Builds/net/`, then waits up to 60 s for each marker.
- **Local:**
  1. Host `-autohost-local`, client `-autojoin-local -profile b` → host `NET_CLIENT_JOINED`, client `NET_CONNECTED`.
  2. A second client with `-fakeversion 0.0.9` → `NET_REJECTED reason=version_mismatch`.
  3. Kill the host → the first client logs `NET_HOST_LOST`.
- **Relay:** host `-autohost -profile a -codefile Builds/net/code.txt` → read the code → client `-autojoin <code> -profile b` → same markers. Then a mismatched client → `version_mismatch`. Then kill the host → `NET_HOST_LOST`.
- Every launched process is killed at the end, whatever the outcome.

- [ ] Run `net-smoke.ps1 -Mode Local`. Expected: `LOCAL SMOKE OK`.
- [ ] Run `net-smoke.ps1 -Mode Relay`. Expected: `RELAY SMOKE OK`. (If services fail: read the log, check the Unity Cloud setup, and ask the user only for things in the dashboard.)
- [ ] Commit `test: two-instance network smoke script`.
- [ ] Update `docs/PROGRESS.md`; commit `docs: M2 complete`.
