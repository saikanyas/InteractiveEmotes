using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace InteractiveEmotes
{
    /// <summary>The "brain" of the mod. Processes lists of rules to find the first one that matches the current game state.</summary>
    public class RuleProcessor
    {
        /// <summary>Finds the first matching immediate reaction rule from a list.</summary>
        public ReactionRule? FindMatchingRule(List<ReactionRule> rules, Farmer farmer, Character character, ModConfig config)
        {
            foreach (var rule in rules)
            {
                if (AreAllConditionsMet(rule.Conditions, farmer, character, config))
                {
                    return rule;
                }
            }
            return null;
        }

        /// <summary>Finds the first matching combo reaction rule from a list.</summary>
        public ComboRule? FindMatchingRule(List<ComboRule> rules, Farmer farmer, Character character, ModConfig config)
        {
            foreach (var rule in rules)
            {
                if (AreAllConditionsMet(rule.Conditions, farmer, character, config))
                {
                    return rule;
                }
            }
            return null;
        }

        /// <summary>Checks if a given set of conditions are met for the current player and character.</summary>
        /// <returns>Returns <c>true</c> if all specified conditions are met, otherwise <c>false</c>.</returns>
        private bool AreAllConditionsMet(Condition? conditions, Farmer farmer, Character character, ModConfig config)
        {
            // A null conditions block always matches.
            if (conditions == null)
            {
                return true;
            }

            // --- Character Type Conditions ---
            string charType = GetCharacterType(character, farmer);
            if (conditions.CharacterType != null)
            {
                // The JSON can provide a single string or an array of strings for CharacterType.
                if (conditions.CharacterType is string typeString)
                {
                    if (charType != typeString)
                        return false;
                }
                else if (conditions.CharacterType is JArray typeArray)
                {
                    var allowedTypes = typeArray.ToObject<List<string>>();
                    if (allowedTypes == null || !allowedTypes.Contains(charType))
                        return false;
                }
            }

            if (conditions.PetType != null && GetPetType(character) != conditions.PetType)
                return false;

            // --- NPC-Specific Conditions ---
            // These conditions only apply if the character is an NPC.
            if (character is NPC npc)
            {
                if (conditions.Name != null && npc.Name != conditions.Name)
                    return false;
                if (conditions.IsSpouse != null && (farmer.spouse == npc.Name) != conditions.IsSpouse)
                    return false;
                if (conditions.IsDateable != null && npc.datable.Value != conditions.IsDateable)
                    return false;

                if (config.EnableFriendshipConditions)
                {
                    if (conditions.FriendshipGreaterThanOrEqualTo != null && farmer.getFriendshipLevelForNPC(npc.Name) < conditions.FriendshipGreaterThanOrEqualTo)
                        return false;
                    if (conditions.FriendshipLessThan != null && farmer.getFriendshipLevelForNPC(npc.Name) >= conditions.FriendshipLessThan)
                        return false;
                }
            }
            else
            {
                // If a rule requires an NPC-specific condition, but the character is not an NPC, the rule fails.
                if (conditions.Name != null || conditions.IsSpouse != null || conditions.IsDateable != null || conditions.FriendshipGreaterThanOrEqualTo != null || conditions.FriendshipLessThan != null)
                    return false;
            }

            // This condition is also NPC-specific but handled separately as Child is not an NPC.
            if (conditions.IsBaby != null && (charType == "Baby") != conditions.IsBaby)
                return false;

            // --- World State Conditions ---
            if (config.EnableSeasonConditions && conditions.Season != null && !Game1.currentSeason.Equals(conditions.Season, StringComparison.OrdinalIgnoreCase))
                return false;

            if (config.EnableWeatherConditions && conditions.Weather != null && !GetWeatherName().Equals(conditions.Weather, StringComparison.OrdinalIgnoreCase))
                return false;

            // If all checks passed, the conditions are met.
            return true;
        }

        /// <summary>Determines the general type of a character (Villager, Pet, FarmAnimal, etc.).</summary>
        public string GetCharacterType(Character character, Farmer farmer)
        {
            if (character is FarmAnimal) return "FarmAnimal";
            if (character is Horse) return "Pet";
            if (character is Pet) return "Pet";
            if (character is Child) return "Baby";
            if (character is NPC npc && npc.IsVillager) return "Villager";
            return "Other";
        }

        /// <summary>Determines the specific type of a pet (Dog, Cat, etc.).</summary>
        private string GetPetType(Character character)
        {
            if (character is Horse) return "Horse";
            if (character is Pet pet)
            {
                // Includes compatibility for mods that add custom pet types like turtles.
                if (pet.GetType().Name == "Turtle") return "Turtle";
                return pet.petType.Value;
            }
            return "NotAPet";
        }

        /// <summary>Gets the current weather as a simple string name.</summary>
        private string GetWeatherName()
        {
            if (Game1.isRaining) return "Rainy";
            if (Game1.isLightning) return "Stormy";
            if (Game1.isDebrisWeather) return "Windy";
            if (Game1.isSnowing) return "Snowy";
            return "Sunny";
        }
    }
}