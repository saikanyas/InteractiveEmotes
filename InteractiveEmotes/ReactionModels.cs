using System.Collections.Generic;

namespace InteractiveEmotes
{
    // ============================================================
    // ReactionModels.cs — Data structures for the Rule & Data system
    //
    // Data Hierarchy:
    //   EmoteReactionData
    //     └─ List<ReactionRule>
    //          └─ ReactionRule
    //               ├─ Condition   ← Required conditions (null = always pass)
    //               └─ object Action ← String shorthand OR ComboAction object
    // ============================================================

    /// <summary>Complete data for a single emote (1 key in reactions.json)</summary>
    public class EmoteReactionData
    {
        public List<ReactionRule> Reactions { get; set; } = new();
        public List<ComboRule> ComboReactions { get; set; } = new();
    }

    /// <summary>A single rule for Immediate Reaction</summary>
    public class ReactionRule
    {
        public Condition? Conditions { get; set; }

        // Action can be a shorthand string (e.g. "happy")
        // OR a ComboAction object (e.g. { "Emote": "happy", "DisplayText": "..." })
        public object Action { get; set; } = "";
    }

    /// <summary>A single rule for Combo Reaction</summary>
    public class ComboRule
    {
        public Condition? Conditions { get; set; }
        public int? TriggerCount { get; set; }
        public ComboAction Action { get; set; } = new();
    }

    /// <summary>The action the NPC will perform — Shared between Reaction and Combo</summary>
    public class ComboAction
    {
        // Emote accepts both single string and array of strings for random selection
        // e.g. "happy" or ["happy", "question", "happy"]
        public object? Emote { get; set; }

        // DisplayText accepts both single string and array of strings for random selection
        // The value should be a translation key, e.g. "reaction.npc.angry.friend1"
        public object? DisplayText { get; set; }
    }

    /// <summary>All supported conditions. Every field is nullable (null = skip check)</summary>
    public class Condition
    {
        // --- Character Conditions ---

        /// <summary>The required NPC Name, e.g. "Sam", "Abigail"</summary>
        public string? Name { get; set; }

        /// <summary>Must be the player's spouse (true/false)</summary>
        public bool? IsSpouse { get; set; }

        /// <summary>Must be a dateable NPC (true/false)</summary>
        public bool? IsDateable { get; set; }

        /// <summary>Character type. Accepts single string or array.
        /// e.g. "Villager", "Pet", "FarmAnimal", "Baby"
        /// or ["Baby", "Pet", "FarmAnimal"]</summary>
        public object? CharacterType { get; set; }

        /// <summary>Pet type, e.g. "Dog", "Cat", "Horse", "Turtle"</summary>
        public string? PetType { get; set; }

        // --- Relationship Conditions ---

        /// <summary>Minimum friendship points (must be >= this value)</summary>
        public int? FriendshipGreaterThanOrEqualTo { get; set; }

        /// <summary>Maximum friendship points (must be < this value)</summary>
        public int? FriendshipLessThan { get; set; }

        // --- Environmental Conditions ---

        /// <summary>Season, e.g. "spring", "summer", "fall", "winter"</summary>
        public string? Season { get; set; }

        /// <summary>Weather, e.g. "Sunny", "Rainy", "Snowy", "Stormy", "Windy"</summary>
        public string? Weather { get; set; }
    }
}
