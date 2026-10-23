## BOO-TRAYAL v0.1.0-alpha — เวอร์ชันทดลองเล่นแรก

เกมผีไทยตลกหลอนสำหรับเพื่อน 2–8 คน: ช่วยกันปิดผนึก ขนของพิธีไปศาล แล้ววิ่งหนีผีกระสือออกประตูรั้วให้ทันก่อนรุ่งสาง ถ้าเล่น 5 คนขึ้นไป อาจมีเพื่อนเป็น **ร่างทรง** แอบป่วนอยู่ ตีระฆังเรียกประชุมแล้วโหวตไล่ได้

### มีอะไรในเวอร์ชันนี้
- เรือนไทยใต้ถุนสูง 1 หลัง + ผีกระสือ (ได้ยินเสียง ไล่ จับ กลัวไฟฉาย)
- ปริศนา 3 แบบ: ตู้เซฟ (รหัสอยู่อีกห้อง), กล่องฟิวส์ 2 คันโยก, ขนโกศ + กระถางธูปไปแท่นบูชา
- ร่างทรง (5 คนขึ้นไป): ป่วนปริศนา (Q), ดับไฟทั้งโซน (X), ไอเย็นออกปากเป็นร่องรอย
- ตายแล้วเป็นผีจิ๋ว ลอยไปบู้เพื่อนได้ เพื่อนถือดวงวิญญาณไปศาลเพื่อชุบได้
- เสียงคุยในเกมตามระยะ, ประชุมแล้วได้ยินทั้งห้อง, ผีจิ๋วมีช่องคุยของตัวเอง
- เมนูไทย/อังกฤษ, ซ้อมคนเดียวแบบออฟไลน์

### ติดตั้ง
1. ดาวน์โหลด `BOO-TRAYAL-v0.1.0-win64.zip` ด้านล่าง แล้วแตกไฟล์ทั้งโฟลเดอร์
2. เปิด `BOO-TRAYAL.exe` — ถ้าขึ้น "Windows protected your PC" (ไทย: "Windows ได้ป้องกันพีซีของคุณ") กด **More info → Run anyway** (ข้อมูลเพิ่มเติม → เรียกใช้ต่อไป); ถ้า Smart App Control บล็อก ดูวิธีใน README.txt
3. Firewall ถาม ให้กด **Allow**
4. คนหนึ่ง **สร้างห้อง** แล้วส่งรหัส 6 ตัว ให้เพื่อน **เข้าห้อง** (ทุกคนต้องใช้เวอร์ชันเดียวกัน)

ต้องใช้ Windows 10/11 64 บิต; ไมค์ไม่บังคับ

### ปัญหาที่รู้แล้ว
- ภาพยังเป็นงานชั่วคราว (บ้านเป็นทรงเรขาคณิต, เส้นขอบโมเดลแตกตรงมุมบางจุด)
- ท่าถือของ ขาไม่ขยับ
- ความยากในหน้าสร้างห้องยังไม่มีผล
- เตะคนออกแล้วเข้ากลับได้ทันที
- ถ้าเครื่องเพื่อนเด้งออกจากเกม ช่องของเขาอาจค้างในล็อบบี้; คนที่ออกไปแล้วยังอยู่ในตารางอันดับ
- หยิบของทะลุกำแพงบางมุมได้

### เจอบั๊ก
แนบไฟล์ `%USERPROFILE%\AppData\LocalLow\LumielX\BOO-TRAYAL\Player.log` แล้วแจ้งที่ [Issues](https://github.com/LumielX/GameByClaude/issues) — รายการทดสอบอยู่ใน [docs/PLAYTEST_CHECKLIST.md](https://github.com/LumielX/GameByClaude/blob/main/docs/PLAYTEST_CHECKLIST.md)

---

## English

First playtest build of a funny-spooky Thai ghost party game for 2–8 friends. Close the seals, carry the ritual items to the shrine and outrun the Krasue through the gate before dawn. With 5+ players a friend may be a secret **Spy**. Ring the bell to call a meeting and vote them out.

**Install:**
1. Unzip `BOO-TRAYAL-v0.1.0-win64.zip`.
2. Run `BOO-TRAYAL.exe`. On a SmartScreen warning, click **More info → Run anyway**.
3. Allow the firewall prompt.
4. One player creates a room and shares the 6-character code.

Everyone must run the same version. Windows 10/11 64-bit is required; a microphone is optional.

**Known issues:**
- Placeholder art.
- Legs freeze while carrying.
- The difficulty setting has no effect yet.
- Kicked players can rejoin at once.

**Bugs:** attach `Player.log` (path above) to a GitHub issue.
