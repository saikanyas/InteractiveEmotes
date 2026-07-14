using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace InteractiveEmotes
{
    public class ConditionGetter
    {
        /// <summary>คืนรายชื่อ NPC ที่อยู่ในระยะ maxDistance ของผู้เล่น
        /// คืน List<NPC> โดยตรงเพื่อไม่ต้องค้นหาซ้ำใน EmoteReactionHandler</summary>
        public static List<NPC> GetNearbyNpcs(float maxDistance, Farmer player)
        {
            // เช็ค currentLocation ก่อนเสมอ เพราะอาจเป็น null ระหว่างโหลดข้ามฉาก
            if (Game1.currentLocation == null)
            {
                return new List<NPC>();
            }

            var nearbyNpcs = new List<NPC>();

            // คำนวณ maxDistanceSq ครั้งเดียวนอก loop
            // ใช้ DistanceSquared แทน Distance เพื่อหลีกเลี่ยงการถอดรากที่สอง (ประหยัด CPU)
            float maxDistanceSq = maxDistance * maxDistance;

            foreach (NPC npc in Game1.currentLocation.characters)
            {
                if (Vector2.DistanceSquared(player.Tile, npc.Tile) <= maxDistanceSq)
                {
                    nearbyNpcs.Add(npc);
                }
            }

            return nearbyNpcs;
        }
    }
}
