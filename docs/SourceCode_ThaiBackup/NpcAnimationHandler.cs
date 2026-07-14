using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace InteractiveEmotes
{
    // ============================================================
    // NpcAnimationHandler.cs — จัดการ animation พิเศษของ NPC
    //
    // ใช้เมื่อ Emote ใน Action ขึ้นต้นด้วย "anim_"
    // เช่น "anim_game", "anim_laugh", "anim_sick"
    //
    // ต่างจาก doEmote() ปกติตรงที่สามารถเล่น frame หลายๆ frame
    // ต่อเนื่องกันได้ และคืนสถานะ animation เดิมให้หลังเล่นจบ
    // ============================================================

    public static class NpcAnimationHandler
    {
        /// <summary>เล่น animation พิเศษตามชื่อที่กำหนด
        /// animName คือชื่อหลังตัด "anim_" ออก เช่น "game", "laugh"</summary>
        public static void PerformAnimation(NPC npc, string animName)
        {
            ModEntry.Instance.Monitor.Log($"[NpcAnimation] {npc.Name} performing animation: {animName}", LogLevel.Trace);

            // หยุดเดินชั่วคราว 600ms เพื่อให้ animation เล่นได้เต็มที่
            // เกมจะคืน schedule ให้อัตโนมัติหลังหมดเวลา
            npc.Halt();
            npc.movementPause = 600;

            switch (animName.ToLower())
            {
                case "sick":
                    // NPC สั่นเล็กน้อยแล้วแสดง emote เศร้า
                    npc.shake(500);
                    npc.doEmote(28); // sad emote
                    break;

                case "laugh":
                    // เล่น frame 4, 5 สลับกันเพื่อจำลองการหัวเราะ
                    // ลำดับ: frame4 → frame5 → frame4 → frame5 → frame0 (กลับปกติ)
                    PlayTemporaryAnimation(npc, new List<int> { 4, 5, 4, 5, 4, 0 }, 200);
                    break;

                case "game":
                    // NPC สั่นเล็กน้อย + เสียงเหรียญ + emote video game
                    npc.shake(200);
                    Game1.playSound("coin");
                    npc.doEmote(52); // video game emote
                    break;

                case "exclamation":
                    // NPC สั่นแล้วแสดง emote อุทาน
                    npc.shake(300);
                    npc.doEmote(16); // exclamation emote
                    break;

                default:
                    ModEntry.Instance.Monitor.Log($"[NpcAnimation] Unknown animation name: '{animName}'", LogLevel.Warn);
                    break;
            }
        }

        /// <summary>เล่น animation ชั่วคราวจาก list ของ frame index
        /// หลังเล่นจบจะคืนสถานะ animation เดิมให้ NPC</summary>
        /// <param name="npc">NPC ที่ต้องการเล่น animation</param>
        /// <param name="frameIndices">รายการ frame index ที่จะเล่นตามลำดับ</param>
        /// <param name="intervalMs">ระยะเวลาแต่ละ frame เป็น milliseconds</param>
        private static void PlayTemporaryAnimation(NPC npc, List<int> frameIndices, int intervalMs)
        {
            // บันทึกสถานะ animation เดิมไว้ก่อน เพื่อคืนค่าหลังเล่นจบ
            var originalAnimation = npc.Sprite.CurrentAnimation;
            int originalFrame = npc.Sprite.currentFrame;

            // สร้าง animation ใหม่จาก frame index ที่รับมา
            var newFrames = new List<FarmerSprite.AnimationFrame>();
            foreach (int frame in frameIndices)
            {
                newFrames.Add(new FarmerSprite.AnimationFrame(frame, intervalMs));
            }

            // คำนวณเวลารวมทั้งหมดของ animation
            int totalDuration = frameIndices.Count * intervalMs;

            // สั่งเล่น animation ทันที
            npc.Sprite.setCurrentAnimation(newFrames);

            // หลังเล่นจบ: คืนสถานะเดิมให้ NPC
            DelayedAction.functionAfterDelay(() =>
            {
                // ตรวจสอบว่า NPC ยังอยู่ในฉากอยู่ก่อนแก้ไข
                if (npc != null && npc.currentLocation != null)
                {
                    npc.Sprite.CurrentAnimation = originalAnimation;
                    npc.Sprite.currentFrame = originalFrame;
                    npc.Sprite.StopAnimation();
                }
            }, totalDuration);
        }
    }
}
