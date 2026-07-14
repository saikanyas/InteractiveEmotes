using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Characters;

namespace InteractiveEmotes
{
    // ============================================================
    // RuleProcessor.cs — ตรวจเงื่อนไข Rule
    //
    // หน้าที่เดียว: รับ List<ReactionRule> + ตัวละคร + farmer
    // แล้วคืน Rule แรกที่เงื่อนไขผ่านครบ
    //
    // การโหลด JSON ย้ายไปอยู่ที่ ModEntry.cs แล้ว
    // ============================================================

    public class RuleProcessor
    {
        /// <summary>วนหา Rule แรกที่เงื่อนไขผ่านครบ
        /// คืน null ถ้าไม่มี Rule ใดผ่าน</summary>
        public ReactionRule? FindMatchingRule(List<ReactionRule> rules, Farmer farmer, Character character, ModConfig config)
        {
            foreach (ReactionRule rule in rules)
            {
                if (AreConditionsMet(rule.Conditions, farmer, character, config))
                {
                    return rule;
                }
            }
            return null;
        }

        // ============================================================
        // ตรวจเงื่อนไข
        // ============================================================

        private bool AreConditionsMet(Condition? conditions, Farmer farmer, Character character, ModConfig config)
        {
            // null = ไม่มีเงื่อนไข ผ่านเสมอ (Default Rule)
            if (conditions == null)
            {
                return true;
            }

            // --- เช็คประเภทตัวละคร ---
            if (conditions.CharacterType != null)
            {
                string charType = GetCharacterType(character, farmer);

                // CharacterType รับได้ทั้ง string เดี่ยวและ array
                if (conditions.CharacterType is string singleType)
                {
                    if (charType != singleType)
                    {
                        return false;
                    }
                }
                else if (conditions.CharacterType is JArray typeArray)
                {
                    // แปลง JArray เป็น List<string> แล้วเช็คว่า charType อยู่ในนั้นไหม
                    List<string>? allowedTypes = typeArray.ToObject<List<string>>();
                    if (allowedTypes == null || !allowedTypes.Contains(charType))
                    {
                        return false;
                    }
                }
            }

            // --- เช็คประเภทสัตว์เลี้ยง ---
            if (conditions.PetType != null && GetPetType(character) != conditions.PetType)
            {
                return false;
            }

            // --- เงื่อนไขเฉพาะ NPC ---
            // ถ้าตัวละครไม่ใช่ NPC แต่ Rule ต้องการเงื่อนไข NPC → ไม่ผ่าน
            if (character is NPC npc)
            {
                if (conditions.Name != null && npc.Name != conditions.Name)
                {
                    return false;
                }

                if (conditions.IsSpouse.HasValue && (farmer.spouse == npc.Name) != conditions.IsSpouse.Value)
                {
                    return false;
                }

                if (conditions.IsDateable.HasValue && npc.datable.Value != conditions.IsDateable.Value)
                {
                    return false;
                }

                if (config.EnableFriendshipConditions)
                {
                    // ดึงคะแนน friendship ของ NPC คนนี้ (0 ถ้ายังไม่รู้จัก)
                    int friendshipPoints = farmer.getFriendshipLevelForNPC(npc.Name);

                    if (conditions.FriendshipGreaterThanOrEqualTo.HasValue && friendshipPoints < conditions.FriendshipGreaterThanOrEqualTo.Value)
                    {
                        return false;
                    }

                    if (conditions.FriendshipLessThan.HasValue && friendshipPoints >= conditions.FriendshipLessThan.Value)
                    {
                        return false;
                    }
                }
            }
            else
            {
                // ตัวละครไม่ใช่ NPC แต่ Rule ต้องการเงื่อนไข NPC-only → ไม่ผ่าน
                bool hasNpcOnlyCondition =
                    conditions.Name != null ||
                    conditions.IsSpouse.HasValue ||
                    conditions.IsDateable.HasValue ||
                    conditions.FriendshipGreaterThanOrEqualTo.HasValue ||
                    conditions.FriendshipLessThan.HasValue;

                if (hasNpcOnlyCondition)
                {
                    return false;
                }
            }

            // --- เช็คว่าเป็นเด็ก (Child) ไหม ---
            // Child ไม่ใช่ NPC class จึงต้องเช็คแยก
            if (conditions.IsDateable.HasValue || conditions.Name != null)
            {
                // ถ้า character เป็น Child แต่มี NPC condition → จัดการแล้วข้างบน
            }

            // --- เช็คสภาพแวดล้อม ---
            if (config.EnableSeasonConditions && conditions.Season != null)
            {
                if (!Game1.currentSeason.Equals(conditions.Season, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (config.EnableWeatherConditions && conditions.Weather != null)
            {
                if (!GetCurrentWeather().Equals(conditions.Weather, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        // ============================================================
        // Helper methods
        // ============================================================

        /// <summary>แปลงตัวละครเป็น string ประเภท ที่ตรงกับค่าใน reactions.json</summary>
        public string GetCharacterType(Character character, Farmer farmer)
        {
            if (character is FarmAnimal)   return "FarmAnimal";
            if (character is Horse)        return "Pet";
            if (character is Pet)          return "Pet";
            if (character is Child)        return "Baby";
            if (character is NPC npc && npc.IsVillager) return "Villager";
            return "Other";
        }

        private string GetPetType(Character character)
        {
            if (character is Horse)  return "Horse";
            if (character is Pet pet)
            {
                // รองรับ mod ที่เพิ่มสัตว์เลี้ยงใหม่ เช่น Turtle
                if (pet.GetType().Name == "Turtle") return "Turtle";
                return pet.petType.Value;
            }
            return "NotAPet";
        }

        private string GetCurrentWeather()
        {
            // เช็ค Lightning ก่อนเสมอ เพราะวันที่ฝนฟ้าคะนองจะ set isRaining = true ด้วย
            if (Game1.isLightning)      return "Stormy";
            if (Game1.isRaining)        return "Rainy";
            if (Game1.isDebrisWeather)  return "Windy";
            if (Game1.isSnowing)        return "Snowy";
            return "Sunny";
        }
    }
}
