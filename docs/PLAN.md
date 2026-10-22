# BOO-TRAYAL — แผนพัฒนา (Roadmap)

- **Spec:** [`docs/GDD.md`](GDD.md) (อนุมัติแล้ว 2026-10-22)
- **อัปเดต:** 2026-10-22
- เอกสารนี้คือแผนภาพรวม ทุก milestone มี "เสร็จเมื่อ" (Definition of Done) และวิธีทดสอบ
- แผนละเอียดระดับงานย่อย (สำหรับลงมือเขียนโค้ด) อยู่ใน `docs/plans/` เขียนทีละ milestone ก่อนลงมือ เพื่อใช้สิ่งที่เรียนรู้จาก milestone ก่อนหน้า
  - M0 + M1: [`docs/plans/2026-10-22-m0-m1-foundation-core-rules.md`](plans/2026-10-22-m0-m1-foundation-core-rules.md)

---

## สแต็กและเวอร์ชันที่ตรึงไว้ (ตรวจจาก packages.unity.com 2026-10-22)

| ส่วน | Package | เวอร์ชัน |
|---|---|---|
| Editor | Unity | 6000.6.7f1 (URP 17.6.0 มีแล้ว) |
| เน็ต | `com.unity.netcode.gameobjects` | 2.13.3 (3.x ต้องการ Unity 6000.7 — ใช้ไม่ได้) |
| ห้อง (Lobby+Relay) | `com.unity.services.multiplayer` | 2.4.1 (ดึง transport 2.7.4 มาเอง) |
| เสียงคุย | `com.unity.services.vivox` | 16.12.1 |
| ภาษา | `com.unity.localization` | 1.5.13 |
| NavMesh | `com.unity.ai.navigation` | 2.0.16 |
| เทสต์ | `com.unity.test-framework` | ตามที่ Editor มากับตัว (ถ้าไม่มี 1.4.6) |
| Input | `com.unity.inputsystem` | 1.20.1 (มีแล้ว) |

ถอดออก: `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.pipeline`, `com.unity.collab-proxy` (ไม่ใช้)

## โครงสร้างโปรเจกต์

```
Assets/_Project/
  Scripts/
    Core/        BooTrayal.Core      — กฎเกม C# ล้วน ไม่พึ่ง Unity (noEngineReferences) → เทสต์เร็ว
    Game/        BooTrayal.Game      — MonoBehaviour + Netcode: Networking/ Player/ Ghost/ Spy/ Puzzles/ Items/ Audio/ Meta/
    UI/          BooTrayal.UI        — UI Toolkit controllers
  Editor/
    Builders/    BooTrayal.Editor    — สร้าง Scene/Prefab/Material/UI/Settings ด้วยโค้ด
    BuildPipeline/
  Prefabs/ Scenes/ Art/{Models,Materials,Textures,VFX} Audio/ UI/{UXML,USS,Fonts} Config/
  Tests/EditMode/  BooTrayal.Tests.EditMode
  Tests/PlayMode/  BooTrayal.Tests.PlayMode
tools/unity/   — สคริปต์ PowerShell: compile, test, build, release
tools/blender/ — สคริปต์สร้างโมเดล
art_source/    — ไฟล์ .blend ต้นฉบับ
docs/
```

ทิศทางการพึ่งพา: `Core ← Game ← UI`, `Editor → ทุกตัว`, Tests → Core/Game

## หลักการที่ใช้ทุก milestone
- กฎเกม (โหวต สุ่ม Spy ชนะ/แพ้ ปริศนา แต้ม Fear/Adrenaline) อยู่ใน `Core` และเขียนแบบ TDD
- ฝั่ง `Game` เป็นแค่ตัวเชื่อมกฎกับ Netcode: server ถือ state จริง client แสดงผล
- ทุก Scene/Prefab/UI สร้างจาก `Editor/Builders` ด้วย `-executeMethod` ห้ามแก้ในฉากด้วยมือ
- **เสร็จ = มีหลักฐาน:** log compile ไม่มี `error CS`, ผลเทสต์ XML ผ่านหมด, build ได้ exe จริง
- commit ทีละเรื่อง อัปเดต `docs/PROGRESS.md` ทุกครั้งที่จบ milestone

---

## เฟส 3 — Vertical Slice (VS)

### M0 — ฐานโปรเจกต์ (Foundation)
**ทำ:** ล้าง package/ไฟล์ template, ติดตั้ง package ตามตาราง, โฟลเดอร์ + Assembly Definitions, สคริปต์ `tools/unity/` (compile/test/build), ตั้งค่าโปรเจกต์ด้วยโค้ด (ชื่อเกม เวอร์ชัน Windows-only), ฉาก Bootstrap, build Windows ได้
**เสร็จเมื่อ:** compile ผ่าน, smoke test ผ่าน, ได้ `Builds/Windows/BOO-TRAYAL.exe` ที่รันด้วย `-smoketest` แล้วพิมพ์ `BOOTRAYAL_SMOKE_OK` และปิดเอง
**ทดสอบ:** `tools/unity/run-tests.ps1 -Platform EditMode`, `tools/unity/build-windows.ps1`, รัน exe smoke

### M1 — กฎเกมแกนกลาง (Core rules, TDD)
**ทำ:** ตั้งค่าห้อง + จำนวน Spy/ผนึก, สุ่มบทบาท, โหวต, ตัวนับป่วน, ตัดสินจบคืน, แต้ม/เหรียญ, Fear Proximity, Stamina + Adrenaline, ปริศนา 3 แบบ (ตู้เซฟ ฟิวส์ ของพิธี), กฎประชุม/ระฆัง, ผลไล่ผีถูก/ผิด, ตรวจเวอร์ชัน, กฎกลับเข้าห้อง
**เสร็จเมื่อ:** EditMode tests ของ Core ผ่านทั้งหมด ครอบคลุมทุกกฎใน GDD ข้อ 4.1–4.3, 6.2–6.3, 7.1–7.4, 8 (#1–3), 17
**ทดสอบ:** `run-tests.ps1 -Platform EditMode` (เทสต์ราว 60+ ข้อ)

### M2 — เน็ตเวิร์กโครงกระดูก
**ทำ:** `NetworkManager` prefab (tick 30), เริ่ม Unity Services + ล็อกอินนิรนาม, สร้าง/เข้าห้องด้วยรหัส (Sessions: Lobby+Relay, 8 คน), ตรวจเวอร์ชันตอนเชื่อมต่อ (connection approval), host หลุด → กลับเมนูพร้อมข้อความ, client หลุด → เล่นต่อ, กลับเข้าห้องได้เฉพาะระหว่างคืน, โหมด Solo (ออฟไลน์ localhost ไม่ต้องล็อกอิน)
**เสร็จเมื่อ:** build 2 ตัวในเครื่องเดียวเชื่อมกันผ่าน Relay ด้วยรหัส; เวอร์ชันไม่ตรงถูกปฏิเสธพร้อมข้อความ; ปิด host แล้ว client กลับเมนู
**ทดสอบ:** PlayMode test เชื่อม host+client ในโปรเซสเดียว (NGO test helpers / local transport); ทดสอบมือด้วย 2 instance
**ต้องการจากคุณ:** สร้าง Unity Cloud project + เปิด Relay/Lobby/Vivox แล้วส่ง Project ID (มีคู่มือทีละขั้นตอนถึงเวลา)

### M3 — ผู้เล่น
**ทำ:** FP controller (owner-authoritative + interpolation), เดิน/วิ่ง/กระโดด/ย่อ, stamina, หยิบ/ถือ/ขว้าง/ทิ้ง (server-authoritative), แบกของหนัก 2 คน (คนเดียวลากช้า+ดัง), ผลัก/ตบ, ล้มแบบ ragdoll-lite 1.5 วิ, ไฟฉาย
**เสร็จเมื่อ:** 2 instance เห็นกันเดินลื่น, แบกของหนักร่วมกันได้, ของตกส่ง "เสียงดัง" event
**ทดสอบ:** PlayMode: ส่งต่อ ownership ของที่ถือ, ล็อกของหนักต้อง 2 คน; ทดสอบมือ

### M4 — แผนที่ graybox + ผีกระสือ
**ทำ:** builder สร้างเรือนไทย graybox (ใต้ถุน ชั้นบน ลานศาล ประตูหนี) + bake NavMesh ด้วยโค้ด, ผี state machine บน server (Wander→Investigate→Chase→Grab→Cooldown, เตือน 1 วิก่อนไล่), ได้ยินเสียงของตก/เสียงไมค์, กลัวไฟฉายส่อง 2 วิ, ไม่ทำร้าย Spy ที่ยังไม่ถูกเปิดโปง, เอฟเฟกต์ Fear (URP Volume + เสียงชั้น) และ Adrenaline ฝั่ง client
**เสร็จเมื่อ:** ผีไล่และจับผู้เล่นได้บนเน็ต, ไล่หนีทันเมื่อวิ่ง+Adrenaline, เอฟเฟกต์ไล่ระดับตาม 25/15/8 m
**ทดสอบ:** EditMode (state machine ใน Core), PlayMode: ผีเดินถึงจุดเสียงบน NavMesh

### M5 — วงจรหนึ่งคืน: ปริศนา, Spy, ประชุม, ชนะ/แพ้
**ทำ:** `NightDirector` (server) เริ่มคืน → สุ่ม Spy ส่งเฉพาะเจ้าตัว → วางผนึก/ปริศนา 3 แบบ → พิธี → ประตูเปิด → หนี; พลัง Spy 2 อย่าง (ป่วนปริศนา, ดับไฟ) + ไอเย็นออกปาก; ระฆังประชุม → วาร์ป → คุย 45 วิ → โหวต 15 วิ → ผลไล่ถูก/ผิด; ตาย → ผีจิ๋วพื้นฐาน; จบคืน → หน้าสรุป + เฉลย Spy; 3 คืนต่อเกม → อันดับรวม
**เสร็จเมื่อ:** เล่นครบ 1 เกม (3 คืน) ใน 2–5 instance ได้ไม่ค้าง ทั้งกรณีทีมชนะและ Spy ชนะ
**ทดสอบ:** PlayMode: บทบาท Spy ไม่ถูกส่งไป client อื่น, โหวตครบรอบ; ทดสอบมือตาม checklist

### M6 — UI เวอร์ชันแรก + 2 ภาษา
**ทำ:** mockup HTML ให้คุณดูก่อน → UXML/USS: Title, Play (Create/Join/Solo), Create Room (2–8, Spy, ความยาว, จำนวนคืน, ความยาก, Copy code), Lobby 8 ช่อง + Ready + เตะ + แชท, HUD, Vote, Summary, Pause, Error popup ภาษาคน, Settings ขั้นต้น (เสียง, ไมค์, sensitivity, ภาษา); ฟอนต์ไทย OFL; Localization TH/EN
**เสร็จเมื่อ:** เดินจากเมนูถึงจบเกมได้ด้วยเมาส์ล้วน, สลับภาษาแล้วทุกหน้าเปลี่ยน
**ทดสอบ:** EditMode: ทุก key ในตาราง TH มีใน EN; PlayMode: เปิดทุกหน้าไม่มี exception

### M7 — เสียงคุย Vivox + ภาพ/เสียงชุดแรก
**ทำ:** Vivox positional (ระยะ), ช่อง global ตอนประชุม, ช่องผีจิ๋ว, ส่งระดับเสียงไมค์ให้ผีได้ยิน, Push-to-talk/Open mic; โมเดล Blender: ผู้เล่น (rig + idle/walk/run/carry/fall), กระสือ, ของพิธี, ระฆัง, ศาล; toon shader + outline ใน URP; เสียง: ambience, หัวใจ, หลอน 3 ชั้น, ระฆัง, UI
**เสร็จเมื่อ:** 2 เครื่องคุยกันได้ เสียงเบาลงตามระยะ, กรี๊ดแล้วผีหันมา; preview PNG ทุกโมเดลผ่านการตรวจ, tris อยู่ใน budget
**ทดสอบ:** ทดสอบมือ 2 เครื่อง; script ตรวจ tris; ตรวจ CREDITS.md

### M8 — Build + Pre-release + Playtest
**ทำ:** `build-windows.ps1` → zip, `release.ps1` → `gh release create v0.1.0-alpha --prerelease`, เปลี่ยน repo เป็น Public, README ฉบับแรก, `docs/PLAYTEST_CHECKLIST.md`
**เสร็จเมื่อ:** เพื่อนดาวน์โหลด zip จาก GitHub แตกแล้วเข้าห้องคุณได้จากต่างเน็ต
**ต้องการจากคุณ:** `gh auth login`, ทดลองเล่นกับเพื่อนตาม checklist แล้วส่งผล

---

## เฟส 4 — Content (แผนละเอียดเขียนหลังได้ผล playtest)
- M9 ผีอีก 3 (ปอบ แม่นาค กุมารทอง) + สุ่มผีต่อคืน
- M10 ปริศนาครบ 11 แบบ + กันติดตายเมื่อคนน้อย
- M11 ห้องสุ่มจาก room modules + modifier 6 แบบ
- M12 ร้านค้า + ไอเท็ม 8 ชิ้น (โพลารอยด์ พระเครื่อง น้ำมนต์/น้ำปลา ฯลฯ) + พลัง Spy ครบ 5
- M13 ผีจิ๋วเต็มรูปแบบ + ชุบที่ศาล + รางวัลตลก 7 แบบ + แต้มบุญ + cosmetic 12 ชิ้น (เซฟในเครื่อง)
- M14 ปรับสมดุลจาก playtest รอบ 2

## เฟส 5 — Polish
- M15 UI เต็มรูปแบบ + แอนิเมชัน + เมนู 3D "ต้องปั่น", How to Play แบบสไลด์มีภาพ, Settings ครบ (keybinds, กราฟิก preset, ลดเอฟเฟกต์ภาพ)
- M16 VFX, เสียงครบ, Audio Mixer snapshots (Meeting, GhostNear), เพลงเมนู/ในด่าน
- M17 Performance (เป้า 60 FPS GTX 1060 low), LOD/occlusion/baked light, แก้บั๊กทั้งหมด

## เฟส 6 — Release
- M18 Build สุดท้าย, README + ภาพ/GIF, GitHub Release v1.0.0, ข้อความชวนเพื่อนภาษาไทย

---

## สิ่งที่คุณต้องทำเอง (สรุป)
| เมื่อไหร่ | อะไร |
|---|---|
| ก่อน M2 เสร็จ | สร้าง Unity Cloud project, เปิด Relay + Lobby + Vivox, ส่ง Project ID |
| ก่อน M8 | `gh auth login` |
| หลัง M8 | เล่นกับเพื่อนตาม checklist แล้วส่งผล |
