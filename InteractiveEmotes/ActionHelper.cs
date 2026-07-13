using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InteractiveEmotes
{
    /// <summary>Shared utilities for executing NPC actions (emotes, text display, token parsing).</summary>
    internal static class ActionHelper
    {
        private static readonly Random _random = new();

        /// <summary>Shuffle-bag pools keyed by the joined text array content.</summary>
        private static readonly Dictionary<string, Queue<string>> _textPools = new();

        /// <summary>
        /// Picks a single emote string from an action value that is either a plain string or a JSON array of strings.
        /// Returns null when the value is absent or unrecognised.
        /// </summary>
        public static string? GetRandomEmote(object? emoteObject)
        {
            if (emoteObject is null) return null;
            if (emoteObject is string emoteString) return emoteString;
            if (emoteObject is JArray jArray)
            {
                var options = jArray.ToObject<List<string>>();
                if (options?.Count > 0)
                    return options[_random.Next(options.Count)];
            }
            return null;
        }

        /// <summary>
        /// Picks a text key from a shuffle-bag pool so that no entry repeats until all have been shown.
        /// A plain string is returned as-is. Returns null when the value is absent or unrecognised.
        /// </summary>
        public static string? GetPooledText(object? textObject)
        {
            if (textObject is null) return null;
            if (textObject is string plain) return plain;
            if (textObject is JArray jArray)
            {
                var items = jArray.ToObject<List<string>>();
                if (items == null || items.Count == 0) return null;
                if (items.Count == 1) return items[0];

                string poolKey = string.Join("|", items);
                if (!_textPools.TryGetValue(poolKey, out var pool) || pool.Count == 0)
                {
                    // Shuffle all items into a fresh queue (Fisher-Yates).
                    for (int i = items.Count - 1; i > 0; i--)
                    {
                        int j = _random.Next(i + 1);
                        (items[i], items[j]) = (items[j], items[i]);
                    }
                    pool = new Queue<string>(items);
                    _textPools[poolKey] = pool;
                }
                return pool.Dequeue();
            }
            return null;
        }

        /// <summary>
        /// Replaces dialogue tokens inside <paramref name="text"/> with their in-game values.
        /// Supported tokens: <c>^</c> (gender split), <c>@</c> (player name),
        /// <c>%farm</c>, <c>%favorite_thing</c>, <c>%pet</c>, <c>%spouse</c>.
        /// </summary>
        public static string ParseTokens(string text, NPC speaker)
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
                text = text.Replace("%pet", Game1.player.getPetName());
            if (speaker.getSpouse() != null)
                text = text.Replace("%spouse", speaker.getSpouse().displayName);
            return text;
        }

        /// <summary>
        /// Shows translated text above an NPC's head, splitting on <c>|</c> for multi-part messages.
        /// </summary>
        /// <param name="translationKey">The i18n key to look up.</param>
        /// <param name="npc">The NPC who will display the text.</param>
        /// <param name="i18n">The translation helper.</param>
        /// <returns>True if any text was actually displayed.</returns>
        public static async Task<bool> ShowTextAsync(string translationKey, NPC npc, ITranslationHelper i18n)
        {
            string translatedText = i18n.Get(translationKey);

            if (translatedText.Contains('|'))
            {
                string[] parts = translatedText.Split('|');
                for (int i = 0; i < parts.Length; i++)
                {
                    npc.showTextAboveHead(ParseTokens(parts[i], npc));
                    if (i < parts.Length - 1)
                        await Task.Delay(1800);
                }
            }
            else
            {
                npc.showTextAboveHead(ParseTokens(translatedText, npc));
            }

            return true;
        }
    }
}
