using System.Collections.Generic;

namespace InteractiveEmotes
{
    /// <summary>Represents all reaction data for a single player emote.</summary>
    public class EmoteReactionData
    {
        /// <summary>Gets or sets the list of rules for immediate, one-time reactions.</summary>
        public List<ReactionRule> Reactions { get; set; } = new();
        /// <summary>Gets or sets the list of rules for combo reactions, triggered by repetition.</summary>
        public List<ComboRule> ComboReactions { get; set; } = new();
    }

    /// <summary>Defines a single rule for an immediate reaction.</summary>
    public class ReactionRule
    {
        /// <summary>Gets or sets the conditions that must be met for this rule to trigger.</summary>
        public Condition? Conditions { get; set; }
        /// <summary>Gets or sets the action to perform. Can be a simple string (emote name) or a complex action object.</summary>
        public object Action { get; set; } = "";
    }

    /// <summary>Defines a single rule for a combo reaction.</summary>
    public class ComboRule
    {
        /// <summary>Gets or sets the conditions that must be met for this rule to trigger.</summary>
        public Condition? Conditions { get; set; }
        /// <summary>Gets or sets the number of times the emote must be performed to trigger this combo. If null, uses the global setting.</summary>
        public int? TriggerCount { get; set; }
        /// <summary>Gets or sets the complex action to perform.</summary>
        public ComboAction Action { get; set; } = new();
    }

    /// <summary>Represents a complex action, allowing for randomized emotes and text.</summary>
    public class ComboAction
    {
        /// <summary>Gets or sets the emote(s) to perform. Can be a single string or an array of strings for a random choice.</summary>
        public object? Emote { get; set; }
        /// <summary>Gets or sets the translation key(s) for text to display. Can be a single string or an array of strings for a random choice.</summary>
        public object? DisplayText { get; set; }
    }

    /// <summary>Represents a set of conditions that must be met for a rule to be triggered.</summary>
    public class Condition
    {
        /// <summary>Gets or sets the name of a specific NPC to match.</summary>
        public string? Name { get; set; }
        /// <summary>Gets or sets a value indicating whether the target must be the player's spouse.</summary>
        public bool? IsSpouse { get; set; }
        /// <summary>Gets or sets a value indicating whether the target must be a dateable NPC.</summary>
        public bool? IsDateable { get; set; }
        /// <summary>Gets or sets the season required for the rule to trigger (e.g., "spring", "summer").</summary>
        public string? Season { get; set; }
        /// <summary>Gets or sets the weather required for the rule to trigger (e.g., "Rainy", "Sunny").</summary>
        public string? Weather { get; set; }
        /// <summary>Gets or sets the required character type(s). Can be a single string or an array of strings (e.g., "Villager", ["Pet", "FarmAnimal"]).</summary>
        public object? CharacterType { get; set; }
        /// <summary>Gets or sets the required pet type (e.g., "Dog", "Cat", "Horse").</summary>
        public string? PetType { get; set; }
        /// <summary>Gets or sets the minimum friendship points required.</summary>
        public int? FriendshipGreaterThanOrEqualTo { get; set; }
        /// <summary>Gets or sets the exclusive maximum friendship points.</summary>
        public int? FriendshipLessThan { get; set; }
        /// <summary>Gets or sets a value indicating whether the target must be the player's child.</summary>
        public bool? IsBaby { get; set; }
    }
}