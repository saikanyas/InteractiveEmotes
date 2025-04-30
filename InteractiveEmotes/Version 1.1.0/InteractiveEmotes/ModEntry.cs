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
    /// <summary>
    /// The main entry point of the InteractiveEmotes mod. This mod allows player emotes to trigger reactions from nearby NPCs.
    /// </summary>
    internal sealed class ModEntry : Mod
    {
        private ModConfig Config = null!;
        private ITranslationHelper i18n = null!;
        private int lastProcessedEmote = 0;
        private bool justLoaded = false; // Prevents false emote detection right after save is loaded
        private HashSet<string> rewardedNpcsToday = new();

        /// <summary>
        /// Called when the mod is first loaded.
        /// Registers game event handlers and initializes configuration.
        /// </summary>
        public override void Entry(IModHelper helper)
        {
            this.i18n = helper.Translation;
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.GameLoop.SaveLoaded += (_, _) => {
                lastProcessedEmote = Game1.player.CurrentEmote;
                justLoaded = true;
            };

            helper.Events.GameLoop.UpdateTicked += (s, e) => {
                if (justLoaded && e.Ticks >= 60)
                    justLoaded = false;
            };
            helper.Events.GameLoop.DayStarted += (_, _) => rewardedNpcsToday.Clear();

            this.Monitor.Log(i18n.Get("log.mod_loaded", new { dist = Config.EventDistance, delay = Config.EmoteDelay }), LogLevel.Info);
        }

        /// <summary>
        /// Register mod config menu options with Generic Mod Config Menu (GMCM).
        /// </summary>
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
                min: 400, max: 1200, interval: 100,
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
                name: () => i18n.Get("config.friendship.amount.name"),
                tooltip: () => i18n.Get("config.friendship.amount.tooltip"),
                getValue: () => Config.FriendshipGainAmount,
                setValue: val => Config.FriendshipGainAmount = val,
                min: 1, max: 100, interval: 1,
                fieldId: "FriendshipGainAmount"
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.friendship.notify.name"),
                tooltip: () => i18n.Get("config.friendship.notify.tooltip"),
                getValue: () => Config.ShowFriendshipGainMessage,
                setValue: val => Config.ShowFriendshipGainMessage = val,
                fieldId: "ShowFriendshipGainMessage"
            );
        }

        /// <summary>
        /// Checks for player emotes every second, and triggers appropriate NPC responses.
        /// </summary>
        private async void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            if (justLoaded) return;
            if (!Context.IsPlayerFree) return;

            try
            {
                int currentEmote = Game1.player.CurrentEmote;
                if (currentEmote <= 1 || currentEmote == lastProcessedEmote) return;
                if (!GetNearbyNpcs().Any()) return;

                var npcs = GetNearbyNpcs().ToList();
                if (!npcs.Any())
                {
                    await Task.Delay(Config.EmoteDelay + 200);
                    Game1.player.CurrentEmote = 0;
                    lastProcessedEmote = 0;
                    return;
                }

                var tasks = new List<Task>();
                switch (currentEmote)
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
                await Task.Delay(Config.EmoteDelay + 200);
                Game1.player.CurrentEmote = 0;
                lastProcessedEmote = 0;
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"{i18n.Get("log.error")} {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Returns NPCs within the configured pixel distance from the player.
        /// </summary>
        private IEnumerable<NPC> GetNearbyNpcs()
        {
            var chars = Game1.player.currentLocation?.characters;
            if (chars is null) yield break;

            foreach (var npc in chars.OfType<NPC>())
                if (Vector2.Distance(npc.Position, Game1.player.Position) < Config.EventDistanceInPixels)
                    yield return npc;
        }

        private async Task HandleEmote(NPC npc, int id)
        {
            if (!IsNpcClose(npc)) return;
            await Task.Delay(Config.EmoteDelay);
            npc.doEmote(id);
            TryAddFriendship(npc);
        }

        private async Task HandleConditionalEmote(NPC npc, int low, int high, int thr)
        {
            if (!IsNpcClose(npc)) return;
            await Task.Delay(Config.EmoteDelay);
            npc.doEmote((GetRelationship(npc) > thr) ? high : low);
            TryAddFriendship(npc);
        }

        private async Task HandleRomanticEmote(NPC npc, int partnerE)
        {
            if (!IsNpcClose(npc)) return;
            await Task.Delay(Config.EmoteDelay);

            if (!IsRomanticPartner(npc)) { npc.doEmote(8); TryAddFriendship(npc); return; }

            int rel = GetRelationship(npc);
            if (rel > 2500) npc.doEmote(20);
            else if (rel > 2000) npc.doEmote(partnerE);
            else npc.doEmote(32);

            TryAddFriendship(npc);
        }

        /// <summary>
        /// Adds friendship points if not already added today. Optionally shows a HUD message.
        /// </summary>
        private void TryAddFriendship(NPC npc)
        {
            if (!Game1.player.friendshipData.ContainsKey(npc.Name)) return;
            if (!rewardedNpcsToday.Add(npc.Name)) return;

            Game1.player.changeFriendship(Config.FriendshipGainAmount, npc);

            if (Config.ShowFriendshipGainMessage)
                Game1.addHUDMessage(new HUDMessage($"+{Config.FriendshipGainAmount} {npc.displayName}", HUDMessage.newQuest_type));
        }

        private bool IsNpcClose(NPC npc) => Vector2.Distance(npc.Position, Game1.player.Position) < Config.EventDistanceInPixels;
        private int GetRelationship(NPC npc) => Game1.player.getFriendshipLevelForNPC(npc.Name);
        private bool IsRomanticPartner(NPC npc) => !string.IsNullOrEmpty(Game1.player.spouse) && npc.Name.Equals(Game1.player.spouse, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Configuration class for the mod. Handles distance, delay, and friendship settings.
        /// </summary>
        private sealed class ModConfig
        {
            public int EventDistance { get; set; } = 3;
            public int EmoteDelay { get; set; } = 600;
            public int FriendshipGainAmount { get; set; } = 10;
            public bool ShowFriendshipGainMessage { get; set; } = true;
            public int EventDistanceInPixels => EventDistance * 64;
        }
    }
}
