# PROGRESS — BOO-TRAYAL (ชื่อชั่วคราว)

> ไฟล์นี้ให้ session ใหม่อ่านแล้วทำต่อได้ทันที อัปเดตทุกครั้งที่จบงานย่อย

## สถานะปัจจุบัน
- **เฟส:** 2 เสร็จ — GDD อนุมัติแล้ว (2026-10-22), แผน `docs/PLAN.md` (roadmap M0–M18) + แผนละเอียด `docs/plans/2026-10-22-m0-m1-foundation-core-rules.md` เขียนแล้ว
- **เฟส 3 กำลังทำ** — M0–M5 merge เข้า `main` แล้ว; บันทึกการตัดสินใจ (rulings) ของ M0–M1 อยู่ที่ `docs/plans/2026-10-22-m0-m1-ledger.md`
- ⚠️ ห้ามแก้ไฟล์ข้อความภาษาไทยด้วย PowerShell 5.1 `Get-Content`/`Set-Content` (อ่าน UTF-8 ผิดแล้วไฟล์เสีย) — ใช้ Edit tool หรือ bash
- **M0 เสร็จ (2026-10-22):** package ตรึงเวอร์ชันแล้ว (NGO 2.13.3, Multiplayer 2.4.1, Vivox 16.12.1, Localization 1.5.13, Navigation 2.0.16, Test Framework 1.8.0, transport 6.6.0 มากับ editor), สคริปต์ `tools/unity/{compile,run-tests,run-method,build-windows}.ps1`, asmdef 5 ตัว, ProjectSetupBuilder + SceneBuilder (idempotent), build `Builds/Windows/BOO-TRAYAL.exe` + smoke test `BOOTRAYAL_SMOKE_OK` ผ่าน, zip 59 MB
- **M1 เสร็จ (2026-10-22):** กฎเกมทั้งหมดใน `Assets/_Project/Scripts/Core/` (RoomRules, RoleAssigner, VoteResolver, SabotageTracker, NightJudge, ScoreRules/Wallet, FearModel, Stamina, AdrenalineModel, SafeCode/FuseBox/RitualOffering puzzles, MeetingRules, ExorciseEffects, JoinPolicy) — EditMode tests 128/128 ผ่าน (`TestResults/EditMode.xml`), build + smoke ผ่าน
  - ผ่าน final review (opus) แก้ 4 จุด: Spy ที่ถูกจับ/หลุดไม่ชนะ, ป่วนนับเฉพาะความคืบหน้าของคนอื่น (puzzle รับ `actor` id, `TrySabotage(rng, saboteur)`), ชนะด้วยภารกิจลับแล้วล็อกไว้ (`SabotageTracker.GoalWon`, จ่าย 150 ครั้งเดียวตอนจบคืนผ่าน `NightEndAwards(..., sabotageGoalWon)`), กด Shift ค้างตอนหมดแรงยังฟื้นได้
  - งานเล็กที่เลื่อนไว้ (deferred minors): smoke test ไม่เช็ค exit code ของเกม; `Start-Process -Wait` อาจค้างถ้ามี child process; VoteResolver นับ voter id ซ้ำ; FuseBox ไม่บอกว่าของที่ค้ำคันโยกตัวไหนต้องตก; `NightPlayer.Exposed` กับ caught-set ใน tracker ต้อง sync เอง (ให้ NightDirector ใน M5 ดูแล)
- **M2 เสร็จ (2026-10-23):** เล่นออนไลน์ผ่าน Relay ได้จริง — Unity Cloud project `9e9bbbe6-25f3-44da-8070-5b328fd1530b` (org slug `phutawansaichol-gmail-com`) ลิงก์แล้ว, Relay/Lobby/Vivox เปิดแล้ว
  - Core/Net: JoinCode, PlayerName (กันวรรณยุกต์ซ้อน, ≤96 bytes), ConnectionPayload, ApprovalRules, DisconnectClassifier, ConnectionTracker, NetErrorText
  - Game/Networking: `ConnectionService` (สร้าง/เข้าห้องด้วยรหัส 6 ตัว, ตรวจเวอร์ชันตอน approval, host หลุด → กลับเมนู, Leave ยกเลิกงานค้างได้, โหมด Solo/local 127.0.0.1:7777), `NetMessages` (ข้อความไทย/อังกฤษ), `LaunchArgs`, `DebugNetMenu` (IMGUI ชั่วคราวจนถึง M6), `NetPlayerStub`
  - Editor: `NetworkPrefabBuilder` (NetworkManager prefab tick 30 + approval), SceneBuilder ใส่ NetworkManager + Services ใน Bootstrap
  - หลักฐาน: EditMode 173/173, `tools/unity/net-smoke.ps1 -Mode Local` และ `-Mode Relay` ผ่าน (2 instance: เข้าห้อง, เวอร์ชันเก่าถูกปฏิเสธ, host ปิด → client ได้ NET_HOST_LOST)
  - ทดสอบหลาย instance ในเครื่องเดียว: ต้องใส่ `-profile <ชื่อ>` คนละชื่อ
  - deferred minors: ไม่มี e2e test ห้องเต็ม; ข้อความ "สูงสุด 8 คน" ตายตัว; approval หลายคำขอใน frame เดียวเห็นจำนวนเท่ากัน (local เท่านั้น); host ที่ใช้ `-fakeversion` ปฏิเสธตัวเอง; `session.LeaveAsync` ไม่มี timeout; ไม่ unsubscribe callback; `NetStatus.Failed` ไม่ได้ใช้; host ไม่ลบ client ที่ crash ออกจาก lobby
  - บันทึกการตัดสินใจ: `docs/plans/2026-10-22-m2-ledger.md`
- **M3 เสร็จ (2026-10-23):** ผู้เล่นมุมมองบุคคลที่ 1 บนเน็ต
  - Core/Player: MovementModel (เดิน 3.2 / วิ่ง 5.6 / ย่อ 1.6 m/s, กระโดด 4.8, g −18), CarryRules, PushRules, Knockdown, KnockdownLedger, NoiseRules
  - Game/Player: PlayerInputReader (ปุ่มตาม GDD สร้างในโค้ด, Esc ปล่อยเมาส์), BotInput, PlayerMotor (owner-authoritative NT, crouch sync), PlayerLook (pitch sync), PlayerFlashlight (F), PlayerSpawnPlacer, PlayerAvatar (สีตาม id, ล้มเอียง 80°), PlayerHands (E หยิบ/วาง, คลิกซ้ายขว้าง, G วาง), PlayerPush (คลิกขวา, server ตรวจ)
  - Items: GrabbableItem — **ของทุกชิ้น server-authoritative** (ของเบาตามมือ, โกศหนักต้อง 2 คนยก/คนเดียวลากช้า+ดัง), กันทะลุกำแพง, NoiseBus สำหรับผีใน M4
  - Flow: Bootstrap → Menu → (host) Sandbox → Menu เมื่อหลุด; ฉาก Sandbox graybox สร้างด้วย SandboxBuilder
  - หลักฐาน: EditMode 213/213; `tools/unity/player-smoke.ps1` (6 instance: เดิน, หยิบ/ขว้าง/เสียง, ย่อเห็นบน host, 2 คนยกโกศ, หลุดขณะถือ, ของไม่ทะลุกำแพง, ผลัก+cooldown, ไม่มี exception) ผ่าน; Local/Relay smoke ผ่าน
  - บั๊กที่เจอระหว่างทาง: CharacterController.minMoveDistance ทำให้ไม่ขยับที่ FPS สูง (ตั้ง 0), in-scene NetworkObject hash = 0 ตอน save ครั้งแรก (ResaveScene เรียก OnValidate), owner-authority item ไม่ sync หลัง ChangeOwnership (เปลี่ยนเป็น server-authoritative)
  - deferred minors: ของที่ถือชนตัวคนถือ; server ไม่หมุนของตามมือ; NaN จาก client ดัดแปลง; spawn ซ้ำเมื่อ id%4 ชน; cursor ค้างถ้า host ปิดก่อนโหลด Sandbox; input ไม่กลับมาหลัง OnDisable; หยิบทะลุกำแพงได้ (ไม่เช็ค LOS); smoke ยังไม่พิสูจน์ความเร็วขว้าง/โกศเคลื่อนที่; hash ค้างเก่าไม่ถูกจับ
  - บันทึกการตัดสินใจ: `docs/plans/2026-10-23-m3-ledger.md`
- **M4 เสร็จ (2026-10-23):** เรือนไทย graybox + ผีกระสือบนเน็ต — แผน `docs/plans/2026-10-23-m4-house-ghost.md`
  - Core/Ghosts: `GhostBrain` (Wander → Investigate → Alert 1 วิ → Chase → Grab 2 วิ → Cooldown 10 วิ, Flee เมื่อโดนไฟฉายส่องหน้า 2 วิ แล้วไม่กลัวไฟ 8 วิ, เลิกไล่เมื่อมองไม่เห็น 6 วิ, ไม่จับ Spy ที่ยังไม่ถูกเปิดโปง → ปล่อยแล้วเมิน 8 วิ), `GhostPerception` (มอง 14 m / 120°, ไฟฉาย 15 m / 12°), `GhostTuning` (ไล่ 6.0 m/s: เร็วกว่าวิ่งปกติ 5.6 แต่ช้ากว่าวิ่ง+Adrenaline 7.0), `NoiseRules.FromVoice`
  - ฉาก `House` (ค่าเริ่มต้น; `-playscene Sandbox` ใช้ฉากทดสอบเดิม): ใต้ถุน เสา ลังของ, ชั้นบน ห้องนอน/ห้องพระ/ชาน/ครัว, บันไดหน้า+หลัง (วิ่งวนหนีได้), ลานบ้าน ศาลพระภูมิ รั้ว ประตูหนี (ยังปิด), bake NavMesh ด้วยโค้ด + ตรวจว่าผีเดินถึงทุกจุด
  - Game: `GhostController` (server: มอง/ได้ยิน/ไฟฉาย → brain → NavMeshAgent), `GhostView` (ลอยขึ้นลง, ไส้เรืองแสงแรงขึ้นตอนไล่, เสียงกรีดตอนเตือน), `FearDirector` (vignette/chromatic/กล้องสั่น/เสียงลม-หัวใจ-เสียงหลอนแบบ cross-fade/ไฟกะพริบ + ส่งระยะผีให้ Adrenaline), `PlayerLife` (ถูกจับ/ตาย; **ชั่วคราว** เกิดใหม่ใน 5 วิ จนกว่า M5 จะทำผีจิ๋ว), `PlayerNoise` (วิ่งมีเสียงเท้า, Z กรี๊ด cooldown 3 วิ)
  - เสียงทั้งหมดสังเคราะห์ด้วยโค้ด (`ProceduralClips`) ยังไม่มีไฟล์เสียงจากภายนอก
  - หลักฐาน: EditMode 252/252; `tools/unity/ghost-smoke.ps1` (ผีได้ยินเสียงกรี๊ด → เตือน 1 วิ → ไล่ → จับ → ตาย 2 วิต่อมา → cooldown → เกิดใหม่; client ได้ FEAR band=near + Adrenaline; ไฟฉายส่องจนผีหนี) ผ่าน; player-smoke, net-smoke Local + Relay ผ่าน; builder รันซ้ำไม่มี diff
  - ผ่าน final review (opus) แก้ 2 จุด: ผีเลิกสืบเสียงกลางทาง (วัดถึงจุดเสียงจริง), ไฟฉายตอนผีพักทำให้พักสั้นลง (หนีแล้วกลับไปพักต่อจนครบ 10 วิ)
  - deferred minors: ไม่ null-check HeadPivot ของไฟฉาย; scream รับทุก 2.8 วิ + log SCREAM_DENIED ไม่จำกัด; GhostBrain สร้าง list ทุก tick ตอนมีคนถูกเมิน; static ไม่ reset เมื่อปิด domain reload (เฉพาะ editor); NavMeshAgent ใน prefab เปิดไว้; ผีค้างเงียบถ้าไม่อยู่บน NavMesh; Teleport เปิด collider ของคนตายชั่วครู่; log Spared อ่าน TargetNet
  - บั๊กที่เจอ: โกศบนชานตกกระทบพื้นตอนโหลดฉากทำให้ผีได้ยิน (ของเพิ่งเกิดไม่ส่งเสียง 1.5 วิ)
  - บันทึกการตัดสินใจ: `docs/plans/2026-10-23-m4-ledger.md`
- **M5 เสร็จ (2026-10-23):** เล่นครบ 1 เกม (หลายคืน) ได้จริงในเรือนไทย — แผน `docs/plans/2026-10-23-m5-night-loop.md`
  - Core/Night: `NightSession` (บทบาท, สถานะชีวิต Alive/MiniGhost/Escaped/Disconnected, ผนึก+ของพิธี+เหรียญ, ระฆังประชุม, พลัง Spy + cooldown, ผลโหวตถูก/ผิด → ผีช้า/ผีโกรธ, +60 วิ), `Meeting` (คุย 45 + โหวต 15, ปิดเร็วเมื่อโหวตครบ, โหวตล่าสุดนับ), `SpyPowers`, `GameSession` (กระเป๋าเงิน + อันดับ), `SealPlanner` (สุ่มผนึก; 2 คนได้ปริศนา 2 คนไม่เกิน 1)
  - ในเรือน: ตู้เซฟ 3 ตู้ (โน้ตรหัสอยู่อีกห้อง), กล่องฟิวส์ 3 ชุด (คันโยก 2 จุดคนละห้อง ดึงห่างกัน ≤ 1 วิ หรือวางของค้างคันโยก), แท่นบูชาข้างศาล (โกศ + ธูป), ระฆังที่ศาลพระภูมิ, ยันต์เรืองแสงบอกผนึกที่ต้องปิด, 5 โซนสำหรับดับไฟ, 8 จุดยืนประชุม
  - `NightDirector`: Lobby → คืน → สรุป 12 วิ → … → จบเกม 15 วิ → Lobby; บทบาทส่งเฉพาะเจ้าตัว (targeted RPC); ผีเกิดตอนเริ่มคืน หยุดตอนประชุม; ประตูเปิดเมื่อพิธีครบ แล้วเดินออกนอกรั้ว = หนีรอด; ตาย → ผีจิ๋ว (ลอยได้ ทะลุคนแต่ไม่ทะลุกำแพง, Z บู้) + ดวงวิญญาณ → เพื่อนถือไปศาลค้าง 3 วิ = ชุบ
  - พลัง Spy ชั่วคราว (ก่อนมีวงล้อใน M6): Q ป่วนปริศนาใกล้ ≤ 3 m, X ดับไฟทั้งโซน 10 วิ; ใช้แล้วมีไอเย็นออกปาก 3 วิ
  - HUD ชั่วคราว (IMGUI): เวลา, ผนึก, ของพิธี, บทบาท, ปุ่มเริ่มเกมของ host, ปุ่มโหวต, หน้าสรุปคืน/อันดับ
  - หลักฐาน: EditMode 293/293; `tools/unity/night-smoke.ps1 -Scenario Coop` (2 คน: ปิดผนึก 3, ขนโกศ+ธูป, ประตูเปิด, หนีรอด → ทีมชนะคืน 1; คืน 2 หมดเวลา; จบเกม; กลับล็อบบี้) และ `-Scenario Spy` (5 คน: Spy ป่วนตู้เซฟ + ดับไฟ นับสำเร็จ, ทุกคนเห็นไอเย็น, ตีระฆัง โหวตไล่ Spy ถูก; คืน 2 โหวตผิด → ชุบที่ศาล → Spy ชนะเพราะหมดเวลา +150; บทบาทไม่รั่วไปเครื่องอื่น) ผ่าน; ghost/player/net smoke ผ่าน; builder รันซ้ำไม่มี diff
  - บั๊กที่เจอ: NavMesh ให้ก้าวขึ้น 0.75 m แต่ตัวละครก้าวได้ 0.4 m (บอทติดข้างบันได — ตั้ง agentClimb 0.4), ของที่ถือ/ลากชนตัวคนถือเองจนเดินไม่ได้ (โกศลากไม่ไป — ตอนนี้ไม่ชนคนที่ถืออยู่)
  - ผ่าน final review (opus) แก้ 3 จุด: คนที่ผีจับอยู่ตอนตีระฆังไม่ตายหลังประชุมแล้ว (ผีปล่อย), ระหว่างประชุมทุกคนยืนนิ่ง ทำภารกิจ/หยิบของ/หนี/ชุบไม่ได้ (กันใช้ระฆังแช่แข็งผี), คนที่หนีหรือตายระหว่างประชุมไม่ถูกนับโหวต/ไม่เกิดดวงวิญญาณซ้ำ
  - deferred minors: โหวตคนที่ตายหลังตีระฆังยังนับได้ (ตอนนี้เกิดไม่ได้เพราะผีหยุด); NameOf ค้นทุกครั้ง; แท่นบูชายังรับของช่วงสรุป; คนที่ออกไปแล้วยังอยู่ในอันดับ; ขาดเทสต์ผู้ตีระฆังหลุด; เครื่องที่ดัดแปลงอ่าน id ร่างทรงจากไอเย็นได้ทุกระยะ
  - บันทึกการตัดสินใจ: `docs/plans/2026-10-23-m5-ledger.md`
- **ถัดไป: M6 UI เวอร์ชันแรก + 2 ภาษา** — ต้องทำ mockup HTML ให้ผู้ใช้ดูก่อน (ต้องใช้ความเห็นผู้ใช้)
- หมายเหตุ: process `X:\TheUnity\Unity Hub\resources\unity.exe serve` คือ helper ของ Hub ไม่ใช่ Editor (สคริปต์ข้ามให้แล้ว)
- ก่อนรัน batchmode ทุกครั้ง: ตรวจว่า Unity Editor ปิดอยู่
- **อัปเดตล่าสุด:** 2026-10-23

## เฟส 0 — สภาพแวดล้อม (เสร็จ)
| รายการ | ผล |
|---|---|
| Unity Hub | 3.22.4 ติดตั้งแล้ว |
| Unity Editor | 6000.6.7f1 — `C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe` |
| Windows Build Support | มี (Mono, win64) — ไม่มี IL2CPP (ไม่จำเป็น) |
| Unity license | มี `C:\ProgramData\Unity\UnityEntitlementLicense.xml` |
| Git | 2.56.0 |
| Git LFS | 3.8.0, `.gitattributes` มีกฎ LFS แล้ว |
| GitHub CLI (gh) | **ยังไม่ติดตั้ง** — ต้องขออนุญาตติดตั้งผ่าน winget |
| git user.name/email | ยังไม่ตั้งค่า (ตั้งเฉพาะ repo ตอน commit) |
| Blender | 5.2.2 LTS — `X:\TheBlender\blender.exe` |
| ดิสก์ | C: ว่าง 58.7 GB, X: ว่าง 286 GB |
| เครื่อง | RTX 5050 Laptop, RAM 15.6 GB |

### โปรเจกต์ Unity ที่มีอยู่แล้ว
- Path: `X:\GameByClaude\GameByClaude` (URP template, Input System)
- Git remote: `https://github.com/LumielX/GameByClaude.git`, มี commit `Initial check-in`
- มีไฟล์ค้าง uncommitted จาก template (ลบ `Assets/Editor/HubForceResolve.cs` ฯลฯ) — ยังไม่แตะ
- Packages มี `com.unity.ai.assistant` (pre), `com.unity.pipeline` (exp) — พิจารณาถอดตอนเฟส 2

## ทำอะไรไปแล้ว
- ตรวจเครื่อง (เฟส 0)

## กำลังทำ
- เฟส 1: ถามคำถามชุด A (ชื่อเกม, ธีม, ผี, ระดับความน่ากลัว)

## ต่อไป
- ชุด B (การเล่น), C (Spy), D (สไตล์/repo) → เขียน `docs/GDD.md` → ผู้ใช้อนุมัติ → เฟส 2 PLAN

## คำตอบของผู้ใช้ (บันทึกการตัดสินใจ) — 2026-10-22
| หัวข้อ | เลือก |
|---|---|
| ชื่อเกม | BOO-TRAYAL (ซับไทย "ผีป่วน เพื่อนแปรพักตร์") |
| ฉาก | บ้านผีไทยโบราณ (เรือนไม้ใต้ถุนสูง ศาลพระภูมิ) |
| ผี | ผีไทยแบบตลก (กระสือ ปอบ แม่นาค กุมารทอง ...) |
| ความหลอน | ตลก 70 / หลอน 30 |
| กล้อง | First-person |
| เสียงคุย | ในเกมตามระยะ (Vivox proximity, ผีได้ยินไมค์) |
| ความยาว | คืนละ 10 นาที × 3 คืน (host ปรับได้) |
| ตายแล้ว | ผีจิ๋วป่วน + ชุบที่ศาลพระภูมิได้ |
| พลัง Spy | วงล้อพลังตามร่างแรก (ข้อ 1.5) + ร่องรอยให้จับผิด |
| จับ Spy | ตีระฆังเรียกประชุม คนละ 1 ครั้ง/คืน, คุย 45 วิ, โหวต |
| โหวตผิด | ผีโกรธ 30 วิ + คนโดนไล่เป็นผีจิ๋ว (ชุบได้) |
| Spy ชนะ | สุ่ม Spy ใหม่ทุกคืน; ชนะถ้าทีมหนีไม่ทัน หรือป่วนสำเร็จ 3 ครั้งไม่ถูกจับ; แต้มสะสมรายคน |
| ร้านค้า v1.0 | 8 ไอเท็ม + 12 หมวก/สี |
| ภาษา | ไทย (ค่าเริ่มต้น) + อังกฤษ สลับได้ |
| Repo | เปลี่ยน LumielX/GameByClaude เป็น Public ตอนปล่อย pre-release แรก |
| สไตล์ภาพ | Low-poly toon + เส้นขอบ (ดู `docs/art_previews/style_lowpoly_toon.png`) |
| ระบบออนไลน์ | Unity ทั้งชุด: NGO + Multiplayer Services (Lobby+Relay) + Vivox |
| จำนวนผู้เล่น | 2–8 คน |
| Spy เปิดได้ | 5 คนขึ้นไปเท่านั้น (2–4 = co-op ล้วน); 7–8 คน host ตั้ง 2 Spy ได้ (รู้จักกัน) |
| หลุดแล้วกลับ | กลับห้องเดิมได้เฉพาะช่วงระหว่างคืน |

- gh 2.103.0 ติดตั้งแล้ว (winget) ที่ `C:\Program Files\GitHub CLI\gh.exe`
- สคริปต์ตัวอย่างสไตล์: `tools/blender/style_previews.py`

## ปัญหาค้าง
- ผู้ใช้ต้องรัน `gh auth login` (ก่อนเฟส 3 release)
- ผู้ใช้ต้องสร้าง Unity Cloud project + เปิด Relay/Lobby/Vivox (ก่อนทดสอบออนไลน์ในเฟส 3)
