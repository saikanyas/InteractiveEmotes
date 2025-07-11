using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Text;

namespace InteractiveEmotes
{
    /// <summary>Handles the logic for emote combos, where repeated emotes trigger special reactions.</summary>
    public class EmoteComboHandler
    {
        private readonly ModConfig _config;
        private readonly ITranslationHelper _i18n;
        private readonly IMonitor _monitor;
        private readonly RuleProcessor _ruleProcessor;
        private readonly Dictionary<string, int> _emoteNameToIdMap;
        private readonly NpcAnimationHandler _animationHandler;
        /// <summary>Stores the combo state for each player and each character they interact with.</summary>
        private readonly Dictionary<long, Dictionary<string, NpcComboState>> _comboStates = new();
        private static readonly Random _random = new();

        public EmoteComboHandler(ModConfig config, ITranslationHelper i18n, IMonitor monitor, RuleProcessor ruleProcessor, Dictionary<string, int> emoteNameToIdMap, NpcAnimationHandler animationHandler)
        {
            _config = config;
            _i18n = i18n;
            _monitor = monitor;
            _ruleProcessor = ruleProcessor;
            _emoteNameToIdMap = emoteNameToIdMap;
            _animationHandler = animationHandler;
        }

        /// <summary>Processes a player's emote to check if it contributes to or triggers a combo.</summary>
        /// <returns>Returns <c>true</c> if a combo was triggered, otherwise <c>false</c>.</returns>
        public bool ProcessEmote(Farmer player, Character character, string emoteString, List<ComboRule> rules)
        {
            if (!_config.EmoteCombo || rules.Count == 0) return false;

            var npcState = GetOrCreateNpcState(player, character);

            // Reset the combo if the timeout has been exceeded.
            if (Game1.ticks - npcState.LastEmoteTime > _config.ComboTimeout)
            {
                npcState.EmoteCounts.Clear();
            }

            // Increment the count for the current emote.
            npcState.EmoteCounts.TryGetValue(emoteString, out int currentCount);
            currentCount++;
            npcState.EmoteCounts[emoteString] = currentCount;
            npcState.LastEmoteTime = Game1.ticks;

            var matchingRule = _ruleProcessor.FindMatchingRule(rules, player, character, _config);
            if (matchingRule == null)
            {
                return false;
            }

            int triggerTarget = GetTriggerTarget(matchingRule);

            if (currentCount >= triggerTarget)
            {
                _ = ExecuteComboAction(npcState, character, matchingRule.Action);
                npcState.EmoteCounts.Remove(emoteString); // Reset combo count after triggering.
                return true;
            }

            return false;
        }

        /// <summary>Determines the required number of emotes for a combo based on the mod's configuration.</summary>
        private int GetTriggerTarget(ComboRule rule)
        {
            if (_config.ComboCountMode.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
            {
                return _config.GlobalComboTarget;
            }
            return rule.TriggerCount ?? _config.GlobalComboTarget;
        }

        /// <summary>Executes the defined action for a successful combo.</summary>
        private async Task ExecuteComboAction(NpcComboState npcState, Character character, ComboAction action)
        {
            if (npcState.IsReacting)
            {
                return;
            }

            try
            {
                npcState.IsReacting = true;

                await Task.Delay(_config.EmoteDelay + Game1.random.Next(0, 300));

                string? emoteToPerform = GetRandomEmote(action.Emote);
                bool emoteWasPerformed = false;

                if (emoteToPerform != null)
                {
                    if (emoteToPerform.StartsWith("anim_") && character is NPC npcForAnim)
                    {
                        _animationHandler.PerformAnimation(npcForAnim, emoteToPerform.Substring(5));
                        emoteWasPerformed = true;
                    }
                    else if (_emoteNameToIdMap.TryGetValue(emoteToPerform, out int emoteId))
                    {
                        character.doEmote(emoteId);
                        emoteWasPerformed = true;
                    }
                }

                string? textToDisplayKey = GetRandomEmote(action.DisplayText);
                string fullTextForLog = "";

                // If an emote was performed and there is text to display, wait a moment for a more natural flow.
                if (emoteWasPerformed && textToDisplayKey != null)
                {
                    await Task.Delay(1200);
                }

                // --- Logic for displaying text, with support for splitting long messages ---
                if (textToDisplayKey != null && character is NPC npcForText)
                {
                    string translatedText = _i18n.Get(textToDisplayKey);

                    if (translatedText.Contains("|"))
                    {
                        string[] parts = translatedText.Split('|');
                        for (int i = 0; i < parts.Length; i++)
                        {
                            string part = parts[i];
                            string parsedPart = ParseTokens(part, npcForText);
                            npcForText.showTextAboveHead(parsedPart);
                            fullTextForLog += parsedPart + " ";

                            if (i < parts.Length - 1)
                            {
                                await Task.Delay(1800);
                            }
                        }
                    }
                    else
                    {
                        string parsedText = ParseTokens(translatedText, npcForText);
                        npcForText.showTextAboveHead(parsedText);
                        fullTextForLog = parsedText;
                    }
                }
                // --- End of text display logic ---

                var logBuilder = new StringBuilder();
                logBuilder.Append($"Emote Combo -> Character: {character.Name}");
                if (emoteToPerform != null) logBuilder.Append($", Reply Emote: {emoteToPerform}");
                if (!string.IsNullOrEmpty(fullTextForLog)) logBuilder.Append($", Text: \"{fullTextForLog.Trim()}\"");

                _monitor.Log(logBuilder.ToString(), LogLevel.Trace);
            }
            finally
            {
                npcState.IsReacting = false;
            }
        }

        /// <summary>Gets or creates the state object for a given player/character pair. Used for tracking combos and reaction states.</summary>
        public NpcComboState GetOrCreateNpcState(Farmer player, Character character)
        {
            if (!_comboStates.TryGetValue(player.UniqueMultiplayerID, out var playerState))
            {
                playerState = new Dictionary<string, NpcComboState>();
                _comboStates[player.UniqueMultiplayerID] = playerState;
            }
            if (!playerState.TryGetValue(character.Name, out var npcState))
            {
                npcState = new NpcComboState();
                playerState[character.Name] = npcState;
            }
            return npcState;
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

    /// <summary>Holds the state of combo progress for a specific NPC and player.</summary>
    public class NpcComboState
    {
        /// <summary>The timestamp (in game ticks) of the last emote in the combo sequence.</summary>
        public long LastEmoteTime { get; set; } = 0;
        /// <summary>A dictionary tracking the number of times each emote has been performed in the current sequence.</summary>
        public Dictionary<string, int> EmoteCounts { get; } = new();
        /// <summary>A flag indicating whether the NPC is currently performing a reaction, to prevent overlapping actions.</summary>
        public bool IsReacting { get; set; } = false;
    }
}