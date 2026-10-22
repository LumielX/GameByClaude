# PROGRESS — BOO-TRAYAL (ชื่อชั่วคราว)

> ไฟล์นี้ให้ session ใหม่อ่านแล้วทำต่อได้ทันที อัปเดตทุกครั้งที่จบงานย่อย

## สถานะปัจจุบัน
- **เฟส:** 2 เสร็จ — GDD อนุมัติแล้ว (2026-10-22), แผน `docs/PLAN.md` (roadmap M0–M18) + แผนละเอียด `docs/plans/2026-10-22-m0-m1-foundation-core-rules.md` เขียนแล้ว
- **เฟส 3 กำลังทำ** — M0+M1+M2 merge เข้า `main` แล้ว; บันทึกการตัดสินใจ (rulings) ของ M0–M1 อยู่ที่ `docs/plans/2026-10-22-m0-m1-ledger.md`
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
- **ถัดไป: M3 ผู้เล่น** (FP controller, stamina, หยิบ/แบก, ผลัก, ragdoll-lite, ไฟฉาย) — เขียนแผนละเอียดก่อน
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
