using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;

namespace InteractiveEmotes
{
    public class EmoteReactionHandler
    {
        // Stores NPCs that have already received friendship points today (internal for console command access)
        internal static readonly HashSet<string> NpcsAwardedToday = new HashSet<string>();

        // Random instance used for shuffling the Pool
        private static readonly Random _random = new Random();

        // ============================================================
        // DisplayText Pool — Shuffle Bag for non-repeating texts
        //
        // Key  = "npcName|text1,text2,text3" (NPC + array contents)
        //        Separates Pools per NPC per action without needing an ID
        // Value = Shuffled Queue<string> (drawn one by one from the front)
        //
        // When Queue is empty → refilled and reshuffled (loops)
        // Resets when session ends (since it's only kept in memory)
        // ============================================================
        internal static readonly Dictionary<string, Queue<string>> _textPools = new Dictionary<string, Queue<string>>();

        // ============================================================
        // HandleReaction — Entry point for reactions
        // ============================================================

        public static void HandleReaction(
            List<NPC> npcs,                                    // Receives List<NPC> directly to avoid redundant searches
            string emoteString,
            Dictionary<string, EmoteReactionData> rules,
            Farmer player,
            RuleProcessor ruleProcessor,
            Dictionary<string, int> emoteNameToId,            // map name→ID for doEmote()
            ITranslationHelper i18n)
        {
            if (npcs.Count == 0)
            {
                return;
            }

            if (!rules.TryGetValue(emoteString, out EmoteReactionData? emoteData))
            {
                ModEntry.Instance.Monitor.Log($"No reaction rules for emote: {emoteString}", LogLevel.Trace);
                return;
            }

            // Shuffle the NPCs so they don't react in the exact same order every time
            List<NPC> shuffledNpcs = new List<NPC>(npcs);
            for (int i = shuffledNpcs.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                NPC temp = shuffledNpcs[i];
                shuffledNpcs[i] = shuffledNpcs[j];
                shuffledNpcs[j] = temp;
            }

            // First NPC delay
            int currentDelay = ModEntry.Instance.Config.EmoteDelay + _random.Next(-200, 201);
            bool isFirstReactor = true;

            foreach (NPC npc in shuffledNpcs)
            {
                // Always check Combo first. If triggered → skip immediate reaction for this NPC
                if (emoteData.ComboReactions.Count > 0 &&
                    ComboHandler.ProcessCombo(player, npc, emoteString, emoteData.ComboReactions, ModEntry.Instance.Config, ruleProcessor, i18n))
                {
                    if (!isFirstReactor)
                    {
                        currentDelay += _random.Next(1, 201);
                    }
                    isFirstReactor = false;
                    continue;
                }

                ReactionRule? matchingRule = ruleProcessor.FindMatchingRule(emoteData.Reactions, player, npc, ModEntry.Instance.Config);

                if (matchingRule == null)
                {
                    ModEntry.Instance.Monitor.Log($"No matching rule for {npc.Name}.", LogLevel.Trace);
                    continue;
                }

                // If not first, add sequential fixed random delay between 1-200ms
                if (!isFirstReactor)
                {
                    currentDelay += _random.Next(1, 201);
                }
                isFirstReactor = false;

                NPC capturedNpc = npc;
                ReactionRule capturedRule = matchingRule;
                Farmer capturedPlayer = player;
                int capturedDelay = currentDelay;

                if (capturedDelay <= 0)
                {
                    ReactToNpc(capturedNpc, capturedRule, capturedPlayer, emoteNameToId, i18n);
                }
                else
                {
                    DelayedAction.functionAfterDelay(
                        () => ReactToNpc(capturedNpc, capturedRule, capturedPlayer, emoteNameToId, i18n),
                        capturedDelay
                    );
                }
            }
        }

        // ============================================================
        // ReactToNpc — Makes a single NPC react based on the Rule
        // ============================================================

        private static void ReactToNpc(NPC npc, ReactionRule rule, Farmer player, Dictionary<string, int> emoteNameToId, ITranslationHelper i18n)
        {
            ComboAction? action = ParseAction(rule.Action);

            if (action == null)
            {
                return;
            }

            bool didSomething = false;

            // --- NPC shows Emote or Animation ---
            string? emoteName = GetRandomString(action.Emote);

            if (emoteName != null)
            {
                if (emoteName.StartsWith("anim_"))
                {
                    string animName = emoteName.Substring(5);
                    NpcAnimationHandler.PerformAnimation(npc, animName);
                    didSomething = true;
                }
                else
                {
                    // Use dictionary O(1) instead of looping Farmer.EMOTES every time
                    if (emoteNameToId.TryGetValue(emoteName, out int emoteId))
                    {
                        npc.doEmote(emoteId);
                        didSomething = true;
                    }
                    else
                    {
                        ModEntry.Instance.Monitor.Log($"[ReactToNpc] Unknown emote: '{emoteName}'", LogLevel.Warn);
                    }
                }
            }

            // --- Show text above NPC's head ---
            if (action.DisplayText != null)
            {
                // If just performed an emote, wait 1200ms before showing text
                // to prevent both from showing simultaneously and looking cluttered
                int textDelay = didSomething ? 1200 : 0;

                NPC capturedNpc = npc;
                object capturedDisplayText = action.DisplayText;

                DelayedAction.functionAfterDelay(
                    () => ShowDisplayText(capturedNpc, capturedDisplayText, i18n),
                    textDelay
                );
            }

            // --- Add Friendship Point and Sound ---
            if (didSomething)
            {
                bool gainedPoint = TryAddFriendship(npc, player, i18n);

                if (ModEntry.Instance.Config.PlayReplySound)
                {
                    if (gainedPoint)
                    {
                        Game1.playSound("yoba");
                    }
                    else
                    {
                        Game1.playSound("coin");
                    }
                }
            }
        }

        // ============================================================
        // DisplayText — Shows text above NPC's head using the Pool
        // ============================================================

        /// <summary>Selects text from the Pool and displays it above the NPC's head.
        /// Supports "|" to split and display sequential texts.</summary>
        private static void ShowDisplayText(NPC npc, object displayTextObj, ITranslationHelper i18n)
        {
            // Extract all text options from the object (single string or array)
            List<string>? textOptions = GetAllStrings(displayTextObj);

            if (textOptions == null || textOptions.Count == 0)
            {
                return;
            }

            // Generate pool key from NPC + array contents
            // to isolate Pools per NPC per action without special IDs
            string poolKey = npc.Name + "|" + string.Join(",", textOptions);

            // Draw a translation key from the Pool (non-repeating until empty)
            string textKey = DrawFromPool(poolKey, textOptions);

            // Translate the key into actual text
            string translatedText = i18n.Get(textKey);

            // Replace special tokens like @, %farm, %pet
            string parsedText = ParseTokens(translatedText, npc);

            // Check for "|" (separator for multi-part messages)
            if (parsedText.Contains("|"))
            {
                string[] parts = parsedText.Split('|');

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    int partIndex = i;
                    NPC capturedNpc = npc;

                    // Show sequentially, delayed by 1800ms per part
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
        // Pool Management — Shuffle Bag
        // ============================================================

        // Keeps track of the last text shown for each pool to prevent back-to-back repeats on refill
        internal static readonly Dictionary<string, string> _lastTextShown = new Dictionary<string, string>();

        /// <summary>Draws 1 string from the Pool.
        /// If Pool is empty or missing → creates and shuffles a new one</summary>
        private static string DrawFromPool(string poolKey, List<string> allOptions)
        {
            if (!_textPools.ContainsKey(poolKey) || _textPools[poolKey].Count == 0)
            {
                _textPools[poolKey] = CreateShuffledQueue(allOptions, poolKey);
            }

            string drawnText = _textPools[poolKey].Dequeue();
            _lastTextShown[poolKey] = drawnText;
            return drawnText;
        }

        /// <summary>Copies the list, randomizes order using Fisher-Yates Shuffle,
        /// and enqueues it for FIFO retrieval.</summary>
        private static Queue<string> CreateShuffledQueue(List<string> options, string poolKey)
        {
            List<string> shuffled = new List<string>(options);

            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                string temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            // Prevent back-to-back repeats across pool refills
            if (shuffled.Count > 1 && _lastTextShown.TryGetValue(poolKey, out string? lastText))
            {
                if (shuffled[0] == lastText)
                {
                    // Swap the first item with the second item
                    string temp = shuffled[0];
                    shuffled[0] = shuffled[1];
                    shuffled[1] = temp;
                }
            }

            return new Queue<string>(shuffled);
        }

        // ============================================================
        // Token Parser — Replaces special tokens in text
        // ============================================================

        /// <summary>Replaces special tokens with actual player/game values</summary>
        private static string ParseTokens(string text, NPC speaker)
        {
            // "^" separates text by player gender. Format: "MaleText^FemaleText"
            if (text.Contains("^"))
            {
                string[] parts = text.Split('^');
                text = (parts.Length >= 2 && !Game1.player.IsMale) ? parts[1] : parts[0];
            }

            text = text.Replace("@", Game1.player.Name);
            text = text.Replace("%farm", Game1.player.farmName.Value);
            text = text.Replace("%favorite_thing", Game1.player.favoriteThing.Value);

            // Check for null before replacing to prevent NullReferenceException
            // from other mods that might cause petName to be null
            if (Game1.player.hasPet())
            {
                string petName = Game1.player.getPetName() ?? "";
                text = text.Replace("%pet", petName);
            }

            Farmer? spouse = speaker.getSpouse();
            if (spouse != null)
            {
                text = text.Replace("%spouse", spouse.displayName ?? "");
            }

            return text;
        }

        // ============================================================
        // Helper Methods
        // ============================================================

        /// <summary>Parses an Action object (string or JObject) into a ComboAction</summary>
        private static ComboAction? ParseAction(object actionObj)
        {
            // Handle string shorthand e.g. "Action": "happy"
            if (actionObj is string actionString)
            {
                return new ComboAction { Emote = actionString };
            }

            // Handle object e.g. "Action": { "Emote": "happy", "DisplayText": "..." }
            if (actionObj is JObject jObject)
            {
                try
                {
                    return jObject.ToObject<ComboAction>();
                }
                catch (Exception ex)
                {
                    ModEntry.Instance.Monitor.Log($"Failed to parse Action: {ex.Message}", LogLevel.Warn);
                    return null;
                }
            }

            return null;
        }

        /// <summary>Takes an object (string or string[]) and returns 1 string (randomized if array).
        /// Used for Emotes that only require 1 value.</summary>
        private static string? GetRandomString(object? obj)
        {
            if (obj == null)
            {
                return null;
            }

            if (obj is string singleString)
            {
                return singleString;
            }

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

        /// <summary>Takes an object (string or string[]) and returns the full List.
        /// Used for DisplayText which requires all options to build the Pool.</summary>
        private static List<string>? GetAllStrings(object? obj)
        {
            if (obj == null)
            {
                return null;
            }

            if (obj is string singleString)
            {
                return new List<string> { singleString };
            }

            if (obj is JArray jArray)
            {
                return jArray.ToObject<List<string>>();
            }

            return null;
        }

        /// <summary>Grants friendship points if not awarded today, and shows HUD message.
        /// Returns true if successfully gained points, false otherwise.</summary>
        private static bool TryAddFriendship(NPC npc, Farmer player, ITranslationHelper i18n)
        {
            int gainAmount = ModEntry.Instance.Config.FriendshipGainAmount;
            if (gainAmount <= 0)
            {
                return false;
            }

            if (NpcsAwardedToday.Contains(npc.Name))
            {
                return false;
            }

            player.changeFriendship(gainAmount, npc);
            NpcsAwardedToday.Add(npc.Name);

            if (ModEntry.Instance.Config.ShowFriendshipGainMessage)
            {
                // Try to get text from i18n, fallback to default if not found
                string msgKey = "message.friendship.gain";
                string translatedMsg = i18n.Get(msgKey, new { npcName = npc.displayName, amount = gainAmount });
                
                // If translation file is missing, it returns the key (or contains key format)
                if (translatedMsg == msgKey || translatedMsg.Contains("message.friendship"))
                {
                    translatedMsg = $"{npc.displayName} +{gainAmount}";
                }

                Game1.addHUDMessage(new HUDMessage(translatedMsg, HUDMessage.newQuest_type));
            }

            ModEntry.Instance.Monitor.Log($"[Friendship] +{gainAmount} with {npc.Name}", LogLevel.Trace);
            
            return true;
        }

        /// <summary>Called overnight to clear the list of NPCs that were awarded points today.</summary>
        public static void ClearDailyNpcLimits()
        {
            NpcsAwardedToday.Clear();
        }
    }
}
