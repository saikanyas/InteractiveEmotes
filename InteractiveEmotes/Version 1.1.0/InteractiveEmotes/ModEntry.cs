using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Microsoft.Xna.Framework;
using GenericModConfigMenu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InteractiveEmotes
{
    internal sealed class ModEntry : Mod
    {
        // Config loaded from JSON
        private ModConfig Config = null!;
        private ITranslationHelper i18n = null!;
        private int lastProcessedEmote = 0;
        private const string LastGainKey = "saikanyass.InteractiveEmotes/lastGainDay";

        public override void Entry(IModHelper helper)
        {
            i18n = helper.Translation;
            Config = helper.ReadConfig<ModConfig>();
            // Subscribe events
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted; // clear stale gain flags
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.GameLoop.SaveLoaded += (_, _) => lastProcessedEmote = 0;

            Monitor.Log(i18n.Get("log.mod_loaded", new { dist = Config.EventDistance, delay = Config.EmoteDelay, gain = Config.RelationshipGain }), LogLevel.Info);
        }

        // Clear previous day's gain flags so CanGainToday works correctly
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            foreach (var location in Game1.locations)
            {
                foreach (var npc in location.characters.OfType<NPC>())
                    npc.modData.Remove(LastGainKey);
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null) return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.delay.name"),
                tooltip: () => i18n.Get("config.delay.tooltip"),
                getValue: () => Config.EmoteDelay,
                setValue: val => Config.EmoteDelay = val,
                min: 300, max: 1000, interval: 100,
                fieldId: "EmoteDelay"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.distance.name"),
                tooltip: () => i18n.Get("config.distance.tooltip"),
                getValue: () => Config.EventDistance,
                setValue: val => Config.EventDistance = val,
                min: 1, max: 10, interval: 1,
                fieldId: "EventDistance"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.gain.name"),
                tooltip: () => i18n.Get("config.gain.tooltip"),
                getValue: () => Config.RelationshipGain,
                setValue: val => Config.RelationshipGain = val,
                min: 0, max: 100, interval: 5,
                fieldId: "RelationshipGain"
            );
        }

        private async void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsPlayerFree) return;
            try
            {
                int emote = Game1.player.CurrentEmote;
                if (emote <= 1 || emote == lastProcessedEmote) return;

                var npcs = GetNearbyNpcs().ToList();

                if (!npcs.Any())
                {
                    int resetDelayNoNpc = Math.Max(Config.EmoteDelay - 100, 0);
                    await Task.Delay(resetDelayNoNpc);
                    Game1.player.CurrentEmote = 0;
                    lastProcessedEmote = 0;
                    return;
                }


                var tasks = new List<Task>();
                switch (emote)
                {
                    case 8: tasks.AddRange(npcs.Select(npc => HandleEmote(npc, 8))); break;
                    case 12: tasks.AddRange(npcs.Select(npc => HandleConditionalEmote(npc, 12, 16, 500))); break;
                    case 16: tasks.AddRange(npcs.Select(npc => HandleConditionalEmote(npc, 8, 16, 500))); break;
                    case 20: tasks.AddRange(npcs.Select(npc => HandleRomanticEmote(npc, 60))); break;
                    case 24: tasks.AddRange(npcs.Select(npc => HandleEmote(npc, 32))); break;
                    case 28: tasks.AddRange(npcs.Select(npc => HandleConditionalEmote(npc, 8, 28, 500))); break;
                    case 32: tasks.AddRange(npcs.Select(npc => HandleConditionalEmote(npc, 8, 32, 500))); break;
                    case 36: tasks.AddRange(npcs.Select(npc => HandleEmote(npc, 8))); break;
                    case 40: tasks.AddRange(npcs.Select(npc => HandleEmote(npc, 40))); break;
                    case 52: tasks.AddRange(npcs.Select(npc => HandleEmote(npc, 32))); break;
                    case 56: tasks.AddRange(npcs.Select(npc => HandleConditionalEmote(npc, 8, 32, 500))); break;
                    case 60: tasks.AddRange(npcs.Select(npc => HandleRomanticEmote(npc, 60))); break;
                }
                await Task.WhenAll(tasks);
                // Delay before resetting emote for smoother UX
                int resetDelay = Math.Max(Config.EmoteDelay - 100, 0);
                await Task.Delay(resetDelay);
                Game1.player.CurrentEmote = 0;
                lastProcessedEmote = 0;
            }
            catch (Exception ex)
            {
                Monitor.Log($"{i18n.Get("log.error")} {ex}", LogLevel.Error);
            }
        }

        private IEnumerable<NPC> GetNearbyNpcs()
        {
            var chars = Game1.player.currentLocation?.characters;
            if (chars is null) yield break;
            foreach (var npc in chars.OfType<NPC>())
                if (Vector2.Distance(npc.Position, Game1.player.Position) < Config.EventDistanceInPixels)
                    yield return npc;
        }

        private bool CanGainToday(NPC npc)
        {
            int today = GetTodayKey();
            string? stored = npc.modData.TryGetValue(LastGainKey, out var val) ? val : null;
            return !(int.TryParse(stored, out var last) && last == today);
        }

        private void RecordGainDay(NPC npc)
        {
            npc.modData[LastGainKey] = GetTodayKey().ToString();
        }

        private int GetTodayKey()
        {
            int seasonIndex = Array.IndexOf(new[] { "spring", "summer", "fall", "winter" }, Game1.currentSeason);
            return Game1.year * 10000 + seasonIndex * 100 + Game1.dayOfMonth;
        }

        private async Task HandleEmote(NPC npc, int id)
        {
            if (!IsNpcClose(npc)) return;
            await Task.Delay(Config.EmoteDelay);
            npc.doEmote(id);
            // Only grant if NPC supports friendship and hasn't gained today
            if (Game1.player.friendshipData.ContainsKey(npc.Name) && CanGainToday(npc))
            {
                try { Game1.player.changeFriendship(Config.RelationshipGain, npc); }
                catch (Exception ex) { Monitor.Log($"Friendship grant failed: {ex}", LogLevel.Warn); }
                RecordGainDay(npc);
            }
        }

        private async Task HandleConditionalEmote(NPC npc, int low, int high, int thr)
        {
            if (!IsNpcClose(npc)) return;
            int rel = GetRelationship(npc);
            await Task.Delay(Config.EmoteDelay);
            npc.doEmote(rel > thr ? high : low);
            if (Game1.player.friendshipData.ContainsKey(npc.Name) && CanGainToday(npc))
            {
                try { Game1.player.changeFriendship(Config.RelationshipGain, npc); }
                catch (Exception ex) { Monitor.Log($"Friendship grant failed: {ex}", LogLevel.Warn); }
                RecordGainDay(npc);
            }
        }

        private async Task HandleRomanticEmote(NPC npc, int partnerE)
        {
            if (!IsNpcClose(npc)) return;
            int rel = GetRelationship(npc);
            await Task.Delay(Config.EmoteDelay);
            if (!IsRomanticPartner(npc))
            {
                npc.doEmote(8);
                if (Game1.player.friendshipData.ContainsKey(npc.Name) && CanGainToday(npc))
                {
                    try { Game1.player.changeFriendship(Config.RelationshipGain, npc); }
                    catch (Exception ex) { Monitor.Log($"Friendship grant failed: {ex}", LogLevel.Warn); }
                    RecordGainDay(npc);
                }
                return;
            }
            if (rel > 2500) npc.doEmote(20);
            else if (rel > 2000) npc.doEmote(partnerE);
            else npc.doEmote(32);
            if (Game1.player.friendshipData.ContainsKey(npc.Name) && CanGainToday(npc))
            {
                try { Game1.player.changeFriendship(Config.RelationshipGain, npc); }
                catch (Exception ex) { Monitor.Log($"Friendship grant failed: {ex}", LogLevel.Warn); }
                RecordGainDay(npc);
            }
        }

        private bool IsNpcClose(NPC npc)
            => Vector2.Distance(npc.Position, Game1.player.Position) < Config.EventDistanceInPixels;
        private int GetRelationship(NPC npc)
            => Game1.player.getFriendshipLevelForNPC(npc.Name);
        private bool IsRomanticPartner(NPC npc)
            => !string.IsNullOrEmpty(Game1.player.spouse) && npc.Name.Equals(Game1.player.spouse, StringComparison.OrdinalIgnoreCase);

        private sealed class ModConfig
        {
            public int EventDistance { get; set; } = 3;
            public int EmoteDelay { get; set; } = 600;
            public int RelationshipGain { get; set; } = 5;
            public int EventDistanceInPixels => EventDistance * 64;
        }
    }
}
