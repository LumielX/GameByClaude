# PROGRESS — BOO-TRAYAL (ชื่อชั่วคราว)

> ไฟล์นี้ให้ session ใหม่อ่านแล้วทำต่อได้ทันที อัปเดตทุกครั้งที่จบงานย่อย

## สถานะปัจจุบัน
- **เฟส:** 1 — GDD (`docs/GDD.md`) เขียนเสร็จ ผ่าน builder check แล้ว **รอผู้ใช้อนุมัติ** → จากนั้นเฟส 2 (writing-plans → `docs/PLAN.md`)
- **อัปเดตล่าสุด:** 2026-10-22

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
