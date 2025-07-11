using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;
using System.Text;

namespace InteractiveEmotes
{
    /// <summary>Handles the main logic for processing player emotes and triggering character reactions.</summary>
    public class EmoteReactionHandler
    {
        private readonly ModConfig _config;
        private readonly IMonitor _monitor;
        private readonly ITranslationHelper _i18n;
        private readonly RuleProcessor _ruleProcessor;
        private Dictionary<string, EmoteReactionData> _reactionRules;
        private readonly Dictionary<string, int> _emoteNameToIdMap;
        private readonly NpcAnimationHandler _animationHandler;
        private readonly EmoteComboHandler _comboHandler;
        private readonly Dictionary<long, HashSet<string>> _rewardedNpcsToday = new();
        private static readonly Random _random = new();

        public EmoteReactionHandler(ModConfig config, IMonitor monitor, ITranslationHelper i18n, RuleProcessor ruleProcessor, Dictionary<string, EmoteReactionData> reactionRules, Dictionary<string, int> emoteNameToIdMap, NpcAnimationHandler animationHandler, EmoteComboHandler comboHandler)
        {
            _config = config;
            _monitor = monitor;
            _i18n = i18n;
            _ruleProcessor = ruleProcessor;
            _reactionRules = reactionRules;
            _emoteNameToIdMap = emoteNameToIdMap;
            _animationHandler = animationHandler;
            _comboHandler = comboHandler;
        }

        /// <summary>Updates the internal ruleset when reloaded by the user.</summary>
        public void UpdateRules(Dictionary<string, EmoteReactionData> newRules)
        {
            _reactionRules = newRules;
        }

        /// <summary>Clears the record of NPCs who have received a friendship reward today. Called at the start of each day.</summary>
        public void ClearDailyRewards() => _rewardedNpcsToday.Clear();

        /// <summary>The main entry point for processing a player's emote. Finds nearby characters and determines their reactions.</summary>
        public void ProcessPlayerEmote(Farmer farmer, string emoteString)
        {
            if (!_reactionRules.TryGetValue(emoteString, out var emoteData))
            {
                return;
            }

            // 1. Gather all interactable characters in the current location, including farm animals.
            List<Character> allCharactersInArea = new List<Character>();
            allCharactersInArea.AddRange(farmer.currentLocation.characters);

            if (farmer.currentLocation is Farm farm)
            {
                foreach (var animal in farm.animals.Values) { allCharactersInArea.Add(animal); }
            }
            else if (farmer.currentLocation is AnimalHouse animalHouse)
            {
                foreach (var animal in animalHouse.animals.Values) { allCharactersInArea.Add(animal); }
            }

            bool interactionOccurred = false;
            foreach (Character character in allCharactersInArea)
            {
                if (Vector2.Distance(character.Position, farmer.Position) > _config.EventDistanceInPixels)
                    continue;

                // 2. Prioritize checking for combos first.
                if (character is not Farmer)
                {
                    bool comboWasTriggered = _comboHandler.ProcessEmote(farmer, character, emoteString, emoteData.ComboReactions);
                    if (comboWasTriggered)
                    {
                        interactionOccurred = true;
                        continue; // If a combo was triggered, skip the immediate reaction for this character.
                    }
                }

                // 3. If no combo was triggered, check for an immediate reaction.
                if (ProcessImmediateReaction(farmer, character, emoteData.Reactions))
                {
                    interactionOccurred = true;
                }
            }

            if (!interactionOccurred)
            {
                _monitor.Log($"Emote '{emoteString}': No one in range or no matching reaction.", LogLevel.Trace);
            }
        }

        /// <summary>Finds and executes a matching immediate reaction rule for a character.</summary>
        private bool ProcessImmediateReaction(Farmer farmer, Character character, List<ReactionRule> rules)
        {
            var matchingRule = _ruleProcessor.FindMatchingRule(rules, farmer, character, _config);
            if (matchingRule == null)
            {
                return false;
            }
            _ = ExecuteAction(farmer, character, matchingRule.Action);
            return true;
        }

        /// <summary>Executes the defined action (emote, text, sound, etc.) for an immediate reaction.</summary>
        private async Task ExecuteAction(Farmer farmer, Character character, object actionObject)
        {
            // Use the combo handler's state object to ensure actions don't overlap.
            NpcComboState? npcState = null;
            if (character is NPC npcForStateCheck)
            {
                npcState = _comboHandler.GetOrCreateNpcState(farmer, npcForStateCheck);
                if (npcState.IsReacting)
                {
                    return; // NPC is busy, ignore this action.
                }
            }

            try
            {
                if (npcState != null)
                {
                    npcState.IsReacting = true;
                }

                await Task.Delay(_config.EmoteDelay + Game1.random.Next(0, 300));

                // Parse the action object, which can be a simple string or a complex object.
                ComboAction? action = null;
                if (actionObject is string actionString)
                {
                    action = new ComboAction { Emote = actionString };
                }
                else if (actionObject is JObject jObject)
                {
                    try { action = jObject.ToObject<ComboAction>(); }
                    catch (System.Exception ex)
                    {
                        _monitor.Log($"Failed to parse complex Action object for immediate reaction. Error: {ex.Message}", LogLevel.Error);
                        return;
                    }
                }
                if (action == null) return;

                bool actionWasPerformed = false;
                string? emoteToPerform = GetRandomEmote(action.Emote);

                if (emoteToPerform != null)
                {
                    if (emoteToPerform.StartsWith("anim_") && character is NPC npcForAnim)
                    {
                        _animationHandler.PerformAnimation(npcForAnim, emoteToPerform.Substring(5));
                        actionWasPerformed = true;
                    }
                    else if (_emoteNameToIdMap.TryGetValue(emoteToPerform, out int emoteId))
                    {
                        character.doEmote(emoteId);
                        actionWasPerformed = true;
                    }
                }

                string? textToDisplayKey = GetRandomEmote(action.DisplayText);

                // If an emote was performed and there is text to display, wait a moment for a more natural flow.
                if (actionWasPerformed && textToDisplayKey != null)
                {
                    await Task.Delay(1200); // Wait 1.2 seconds for the emote to finish.
                }

                // --- Logic for displaying text, with support for splitting long messages ---
                if (textToDisplayKey != null && character is NPC npcForText)
                {
                    string translatedText = _i18n.Get(textToDisplayKey);

                    // Check for the special '|' token to split the text.
                    if (translatedText.Contains("|"))
                    {
                        string[] parts = translatedText.Split('|');
                        for (int i = 0; i < parts.Length; i++)
                        {
                            string part = parts[i];
                            string parsedPart = ParseTokens(part, npcForText);
                            npcForText.showTextAboveHead(parsedPart);
                            actionWasPerformed = true;

                            // If this is not the last part, wait before showing the next one.
                            if (i < parts.Length - 1)
                            {
                                await Task.Delay(1800); // Delay between text parts for readability.
                            }
                        }
                    }
                    else
                    {
                        // If no token is found, display the text normally.
                        string parsedText = ParseTokens(translatedText, npcForText);
                        npcForText.showTextAboveHead(parsedText);
                        actionWasPerformed = true;
                    }
                }
                // --- End of text display logic ---

                if (actionWasPerformed)
                {
                    bool friendshipGranted = TryAddFriendship(farmer, character);
                    if (_config.PlayReplySound) Game1.playSound("pickUpItem");

                    var logBuilder = new StringBuilder();
                    logBuilder.Append($"Emote Reaction -> Character: {character.Name}");
                    if (emoteToPerform != null) logBuilder.Append($", Reply Emote: {emoteToPerform}");
                    if (friendshipGranted) logBuilder.Append($", Friendship: +{_config.FriendshipGainAmount}");

                    _monitor.Log(logBuilder.ToString(), LogLevel.Trace);
                }
            }
            finally
            {
                if (npcState != null)
                {
                    npcState.IsReacting = false;
                }
            }
        }

        /// <summary>A helper method to get a single string from an object that can be either a string or an array of strings.</summary>
        private string? GetRandomEmote(object? emoteObject)
        {
            if (emoteObject is null) return null;
            if (emoteObject is string emoteString) return emoteString;
            if (emoteObject is JArray jArray)
            {
                var emoteOptions = jArray.ToObject<List<string>>();
                if (emoteOptions?.Count > 0)
                {
                    return emoteOptions[_random.Next(emoteOptions.Count)];
                }
            }
            return null;
        }

        /// <summary>Tries to add friendship points to an NPC, respecting the once-per-day limit per player.</summary>
        private bool TryAddFriendship(Farmer farmer, Character character)
        {
            if (character is not NPC npc) return false;
            if (_config.FriendshipGainAmount <= 0) return false;
            // Can't gain friendship with someone you haven't met, except for pets.
            if (npc is not Pet && !farmer.friendshipData.ContainsKey(npc.Name)) return false;

            // Check if this player has already been rewarded for this NPC today.
            if (!_rewardedNpcsToday.TryGetValue(farmer.UniqueMultiplayerID, out var rewardedSet))
            {
                rewardedSet = new HashSet<string>();
                _rewardedNpcsToday[farmer.UniqueMultiplayerID] = rewardedSet;
            }
            if (!rewardedSet.Add(npc.Name))
            {
                return false; // Already rewarded today.
            }

            farmer.changeFriendship(_config.FriendshipGainAmount, npc);

            // Show a notification message only to the local player.
            if (_config.ShowFriendshipGainMessage && farmer.IsLocalPlayer)
            {
                Game1.addHUDMessage(new HUDMessage($"+{_config.FriendshipGainAmount} {npc.displayName}", HUDMessage.newQuest_type));
            }
            return true;
        }

        /// <summary>Parses dialogue tokens like @ and %spouse% from a string.</summary>
        private string ParseTokens(string text, NPC speaker)
        {
            if (text.Contains('^'))
            {
                string[] parts = text.Split('^');
                text = parts.Length >= 2 && !Game1.player.IsMale ? parts[1] : parts[0];
            }
            text = text.Replace("@", Game1.player.Name);
            text = text.Replace("%farm", Game1.player.farmName.Value);
            text = text.Replace("%favorite_thing", Game1.player.favoriteThing.Value);
            if (Game1.player.hasPet())
            {
                text = text.Replace("%pet", Game1.player.getPetName());
            }
            if (speaker.getSpouse() != null)
            {
                text = text.Replace("%spouse", speaker.getSpouse().displayName);
            }
            return text;
        }
    }
}