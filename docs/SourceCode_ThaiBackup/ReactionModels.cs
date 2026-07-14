using System.Collections.Generic;

namespace InteractiveEmotes
{
    // ============================================================
    // ReactionModels.cs — โครงสร้างข้อมูลสำหรับระบบ Rule & Data
    //
    // ลำดับชั้นข้อมูล:
    //   EmoteReactionData
    //     └─ List<ReactionRule>
    //          └─ ReactionRule
    //               ├─ Condition   ← เงื่อนไขที่ต้องผ่าน (null = ผ่านเสมอ)
    //               └─ object Action ← string ย่อ หรือ ComboAction object
    // ============================================================

    /// <summary>ข้อมูลทั้งหมดของ emote หนึ่งตัว (1 key ใน reactions.json)</summary>
    public class EmoteReactionData
    {
        public List<ReactionRule> Reactions { get; set; } = new();
        public List<ComboRule> ComboReactions { get; set; } = new();
    }

    /// <summary>Rule หนึ่งอัน สำหรับ Immediate Reaction</summary>
    public class ReactionRule
    {
        public Condition? Conditions { get; set; }

        // object รับได้ทั้ง string ย่อ (เช่น "happy")
        // และ ComboAction object (เช่น { "Emote": "happy", "DisplayText": "..." })
        public object Action { get; set; } = "";
    }

    /// <summary>Rule หนึ่งอัน สำหรับ Combo Reaction (ยังไม่ implement แต่โหลด JSON ได้)</summary>
    public class ComboRule
    {
        public Condition? Conditions { get; set; }
        public int? TriggerCount { get; set; }
        public ComboAction Action { get; set; } = new();
    }

    /// <summary>Action ที่ NPC จะทำ — ใช้ร่วมกันระหว่าง Reaction และ Combo</summary>
    public class ComboAction
    {
        // Emote รับได้ทั้ง string เดี่ยว และ string[] สำหรับสุ่ม
        // เช่น "happy" หรือ ["happy","question","happy"]
        public object? Emote { get; set; }

        // DisplayText รับได้ทั้ง string เดี่ยว และ string[] สำหรับสุ่ม
        // ค่าที่ส่งมาคือ translation key เช่น "reaction.npc.angry.friend1"
        public object? DisplayText { get; set; }
    }

    /// <summary>เงื่อนไขทั้งหมดที่รองรับ ทุก field เป็น nullable = ไม่เช็คถ้าเป็น null</summary>
    public class Condition
    {
        // --- เงื่อนไขตัวละคร ---

        /// <summary>ชื่อ NPC ที่ต้องเป็น เช่น "Sam", "Abigail"</summary>
        public string? Name { get; set; }

        /// <summary>ต้องเป็นคู่สมรสของผู้เล่น (true/false)</summary>
        public bool? IsSpouse { get; set; }

        /// <summary>ต้องเป็น NPC ที่ dateable (true/false)</summary>
        public bool? IsDateable { get; set; }

        /// <summary>ประเภทตัวละคร รับได้ทั้ง string เดี่ยวและ array
        /// เช่น "Villager", "Pet", "FarmAnimal", "Baby"
        /// หรือ ["Baby", "Pet", "FarmAnimal"]</summary>
        public object? CharacterType { get; set; }

        /// <summary>ประเภทสัตว์เลี้ยง เช่น "Dog", "Cat", "Horse", "Turtle"</summary>
        public string? PetType { get; set; }

        // --- เงื่อนไขความสัมพันธ์ ---

        /// <summary>คะแนน friendship ขั้นต่ำ (ต้อง >= ค่านี้)</summary>
        public int? FriendshipGreaterThanOrEqualTo { get; set; }

        /// <summary>คะแนน friendship สูงสุด (ต้อง < ค่านี้)</summary>
        public int? FriendshipLessThan { get; set; }

        // --- เงื่อนไขสภาพแวดล้อม ---

        /// <summary>ฤดู เช่น "spring", "summer", "fall", "winter"</summary>
        public string? Season { get; set; }

        /// <summary>สภาพอากาศ เช่น "Sunny", "Rainy", "Snowy", "Stormy", "Windy"</summary>
        public string? Weather { get; set; }
    }
}
