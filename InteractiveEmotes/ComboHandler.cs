using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;

namespace InteractiveEmotes
{
    // ============================================================
    // ComboHandler.cs — Handles repetitive emotes to unlock Combos
    //
    // Mechanism:
    //   1. Player performs emote → ProcessCombo() increments count
    //   2. If count reaches TriggerCount → Plays ComboAction
    //   3. If emote stops longer than ComboTimeout ticks → resets count
    //
    // State is tracked per Farmer (UniqueMultiplayerID) per NPC
    // to support multiplayer.
    // ============================================================

    /// <summary>Stores the combo state of a specific NPC per specific Farmer</summary>
    public class NpcComboState
    {
        /// <summary>Game tick of the last emote, used to calculate timeout</summary>
        public long LastEmoteTime { get; set; } = 0;

        /// <summary>Number of times each emote was performed
        /// Key = emote name (e.g. "happy"), Value = count</summary>
        public Dictionary<string, int> EmoteCounts { get; } = new Dictionary<string, int>();

        /// <summary>Is the NPC currently reacting? If so, prevents overlapping combos</summary>
        public bool IsReacting { get; set; } = false;
    }

    public static class ComboHandler
    {
        // All states: Farmer ID → (NPC Name → NpcComboState) (internal for console commands access)
        internal static readonly Dictionary<long, Dictionary<string, NpcComboState>> _comboStates
            = new Dictionary<long, Dictionary<string, NpcComboState>>();

        private static readonly Random _random = new Random();

        // ============================================================
        // ProcessCombo — Entry point, called before immediate reaction
        // ============================================================

        /// <summary>Processes emotes for the Combo system
        /// Returns true if Combo is triggered (tells caller to skip immediate reaction)</summary>
        public static bool ProcessCombo(
            Farmer player,
            NPC npc,
            string emoteString,
            List<ComboRule> rules,
            ModConfig config,
            RuleProcessor ruleProcessor,  // Passed from outside to avoid recreating every time
            ITranslationHelper i18n)
        {
            // If Combo is disabled in config or no rules → do nothing
            if (!config.EmoteCombo || rules.Count == 0)
            {
                return false;
            }

            NpcComboState state = GetOrCreateState(player, npc);

            // If emote stopped longer than timeout → reset count for this NPC
            if (Game1.ticks - state.LastEmoteTime > config.ComboTimeout)
            {
                state.EmoteCounts.Clear();
            }

            // Increment count for this emote
            state.EmoteCounts.TryGetValue(emoteString, out int currentCount);
            currentCount++;
            state.EmoteCounts[emoteString] = currentCount;
            state.LastEmoteTime = Game1.ticks;

            // Find a passing Rule
            // [Note] RuleProcessor uses the instance created in ModEntry
            // If injection is preferred, change it here in the future
            ComboRule? matchingRule = FindMatchingComboRule(rules, player, npc, config, ruleProcessor);

            if (matchingRule == null)
            {
                return false;
            }

            // Calculate the target number of emotes required to trigger the combo
            int triggerTarget = GetTriggerTarget(matchingRule, config);

            if (currentCount >= triggerTarget)
            {
                // Trigger! Reset count and play action
                state.EmoteCounts.Remove(emoteString);

                // Prevent overlapping combos if the NPC is currently reacting
                if (!state.IsReacting)
                {
                    ExecuteComboAction(state, npc, matchingRule.Action, config, i18n);
                }

                return true;
            }

            return false;
        }

        // ============================================================
        // ExecuteComboAction — Plays the Combo action
        // ============================================================

        private static void ExecuteComboAction(NpcComboState state, NPC npc, ComboAction action, ModConfig config, ITranslationHelper i18n)
        {
            state.IsReacting = true;

            // Delay before responding (EmoteDelay + random 0-300ms for natural feel)
            int delay = config.EmoteDelay + _random.Next(0, 300);

            NPC capturedNpc = npc;
            NpcComboState capturedState = state;

            DelayedAction.functionAfterDelay(() =>
            {
                bool didSomething = false;

                // --- Play Emote or Animation ---
                string? emoteName = GetRandomString(action.Emote);

                if (emoteName != null)
                {
                    if (emoteName.StartsWith("anim_"))
                    {
                        string animName = emoteName.Substring(5);
                        NpcAnimationHandler.PerformAnimation(capturedNpc, animName);
                        didSomething = true;
                    }
                    else
                    {
                        // Use dictionary O(1) instead of looping Farmer.EMOTES
                        if (ModEntry._emoteNameToId.TryGetValue(emoteName, out int emoteId))
                        {
                            capturedNpc.doEmote(emoteId);
                            didSomething = true;
                        }
                    }
                }

                // --- Show DisplayText ---
                if (action.DisplayText != null)
                {
                    int textDelay = didSomething ? 1200 : 0;
                    object capturedDisplayText = action.DisplayText;

                    DelayedAction.functionAfterDelay(
                        () => ShowComboText(capturedNpc, capturedDisplayText, i18n),
                        textDelay
                    );
                }

                // Restore IsReacting state when finished
                capturedState.IsReacting = false;

                ModEntry.Instance.Monitor.Log($"[Combo] {capturedNpc.Name} triggered combo: {emoteName}", LogLevel.Trace);

            }, delay);
        }

        /// <summary>Displays text above NPC's head for Combos
        /// Standard randomization is used here for Combos instead of the Shuffle Bag.</summary>
        private static void ShowComboText(NPC npc, object displayTextObj, ITranslationHelper i18n)
        {
            List<string>? options = GetAllStrings(displayTextObj);

            if (options == null || options.Count == 0)
            {
                return;
            }

            // Randomly select 1 key from the options
            string textKey = options[_random.Next(options.Count)];
            string translatedText = i18n.Get(textKey);
            string parsedText = ParseTokens(translatedText, npc);

            if (parsedText.Contains("|"))
            {
                string[] parts = parsedText.Split('|');
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    int partIndex = i;
                    NPC capturedNpc = npc;

                    DelayedAction.functionAfterDelay(
                        () => capturedNpc.showTextAboveHead(part),
                        partIndex * 1800
                    );
                }
            }
            else
            {
                npc.showTextAboveHead(parsedText);
            }
        }

        // ============================================================
        // Helper Methods
        // ============================================================

        /// <summary>Finds the first matching ComboRule, sharing the RuleProcessor with immediate reactions.</summary>
        private static ComboRule? FindMatchingComboRule(List<ComboRule> rules, Farmer farmer, NPC npc, ModConfig config, RuleProcessor processor)
        {
            foreach (ComboRule rule in rules)
            {
                if (IsComboConditionMet(rule.Conditions, farmer, npc, config, processor))
                {
                    return rule;
                }
            }

            return null;
        }

        private static bool IsComboConditionMet(Condition? conditions, Farmer farmer, NPC npc, ModConfig config, RuleProcessor processor)
        {
            // Cannot use FindMatchingRule directly because ComboRule isn't ReactionRule
            // Delegate condition checking via temporary ReactionRule wrapper
            // [Note] For cleaner implementation, move AreConditionsMet to public in RuleProcessor
            var tempRule = new ReactionRule { Conditions = conditions, Action = "" };
            var tempList = new List<ReactionRule> { tempRule };
            return processor.FindMatchingRule(tempList, farmer, npc, config) != null;
        }

        private static int GetTriggerTarget(ComboRule rule, ModConfig config)
        {
            // ComboCountMode "Fixed" → All rules use GlobalComboTarget
            // "PerCombo" → Use the Rule's TriggerCount (fallback to GlobalComboTarget if missing)
            if (config.ComboCountMode.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
            {
                return config.GlobalComboTarget;
            }

            return rule.TriggerCount ?? config.GlobalComboTarget;
        }

        private static NpcComboState GetOrCreateState(Farmer player, NPC npc)
        {
            // Get this player's dictionary (or create new)
            if (!_comboStates.TryGetValue(player.UniqueMultiplayerID, out var playerStates))
            {
                playerStates = new Dictionary<string, NpcComboState>();
                _comboStates[player.UniqueMultiplayerID] = playerStates;
            }

            // Get this NPC's state (or create new)
            if (!playerStates.TryGetValue(npc.Name, out var npcState))
            {
                npcState = new NpcComboState();
                playerStates[npc.Name] = npcState;
            }

            return npcState;
        }

        /// <summary>Resets all states, called overnight.</summary>
        public static void ClearAllStates()
        {
            _comboStates.Clear();
        }

        private static string? GetRandomString(object? obj)
        {
            if (obj == null) return null;
            if (obj is string s) return s;
            if (obj is JArray jArray)
            {
                List<string>? options = jArray.ToObject<List<string>>();
                if (options != null && options.Count > 0)
                {
                    return options[_random.Next(options.Count)];
                }
            }
            return null;
        }

        private static List<string>? GetAllStrings(object? obj)
        {
            if (obj == null) return null;
            if (obj is string s) return new List<string> { s };
            if (obj is JArray jArray) return jArray.ToObject<List<string>>();
            return null;
        }

        private static string ParseTokens(string text, NPC speaker)
        {
            if (text.Contains("^"))
            {
                string[] parts = text.Split('^');
                text = (parts.Length >= 2 && !Game1.player.IsMale) ? parts[1] : parts[0];
            }
            text = text.Replace("@", Game1.player.Name);
            text = text.Replace("%farm", Game1.player.farmName.Value);
            text = text.Replace("%favorite_thing", Game1.player.favoriteThing.Value);
            // null safety
            if (Game1.player.hasPet()) text = text.Replace("%pet", Game1.player.getPetName() ?? "");
            Farmer? spouse = speaker.getSpouse();
            if (spouse != null) text = text.Replace("%spouse", spouse.displayName ?? "");
            return text;
        }
    }
}
