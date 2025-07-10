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
        // Configuration loaded from mod config file
        private ModConfig Config = null!;

        // Translation helper for localization
        private ITranslationHelper i18n = null!;

        // Stores the last processed emote to avoid repeat triggering
        private int lastProcessedEmote = 0;

        public override void Entry(IModHelper helper)
        {
            this.i18n = helper.Translation;
            this.Config = helper.ReadConfig<ModConfig>();

            // Subscribe to necessary SMAPI events
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.GameLoop.SaveLoaded += (_, _) => lastProcessedEmote = 0;

            // Log mod load with config info
            this.Monitor.Log(i18n.Get("log.mod_loaded", new { dist = Config.EventDistance, delay = Config.EmoteDelay }), LogLevel.Info);
        }

        // Initialize the GMCM menu with config options
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
        }

        // Called every second to check and react to player emotes
        private async void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsPlayerFree) return; // Don't process if player is in a menu or cutscene

            try
            {
                int currentEmote = Game1.player.CurrentEmote;

                // Skip if no emote or already processed
                if (currentEmote <= 1 || currentEmote == lastProcessedEmote) return;

                var npcs = GetNearbyNpcs().ToList();
                if (!npcs.Any())
                {
                    Game1.player.CurrentEmote = 0;
                    lastProcessedEmote = 0;
                    return;
                }

                var tasks = new List<Task>();

                // Emote mapping logic: depending on player emote, make nearby NPCs react
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

                // Reset emote state
                Game1.player.CurrentEmote = 0;
                lastProcessedEmote = 0;
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"{i18n.Get("log.error")} {ex}", LogLevel.Error);
            }
        }

        // Returns NPCs within a defined distance from the player
        private IEnumerable<NPC> GetNearbyNpcs()
        {
            var chars = Game1.player.currentLocation?.characters;
            if (chars is null) yield break;

            foreach (var npc in chars.OfType<NPC>())
                if (Vector2.Distance(npc.Position, Game1.player.Position) < Config.EventDistanceInPixels)
                    yield return npc;
        }

        // Makes the NPC perform a specific emote
        private async Task HandleEmote(NPC npc, int id)
        {
            if (!IsNpcClose(npc)) return;
            await Task.Delay(Config.EmoteDelay);
            npc.doEmote(id);
        }

        // Makes the NPC perform a conditional emote based on friendship value
        private async Task HandleConditionalEmote(NPC npc, int low, int high, int thr)
        {
            if (!IsNpcClose(npc)) return;
            await Task.Delay(Config.EmoteDelay);
            npc.doEmote((GetRelationship(npc) > thr) ? high : low);
        }

        // Makes the NPC perform a romantic emote based on relationship level
        private async Task HandleRomanticEmote(NPC npc, int partnerE)
        {
            if (!IsNpcClose(npc)) return;
            await Task.Delay(Config.EmoteDelay);

            if (!IsRomanticPartner(npc)) { npc.doEmote(8); return; }

            int rel = GetRelationship(npc);
            if (rel > 2500) npc.doEmote(20);
            else if (rel > 2000) npc.doEmote(partnerE);
            else npc.doEmote(32);
        }

        // Determines if the NPC is within the allowed range from player
        private bool IsNpcClose(NPC npc) => Vector2.Distance(npc.Position, Game1.player.Position) < Config.EventDistanceInPixels;

        // Gets the current friendship level between player and NPC
        private int GetRelationship(NPC npc) => Game1.player.getFriendshipLevelForNPC(npc.Name);

        // Checks if the NPC is the player's spouse
        private bool IsRomanticPartner(NPC npc) => !string.IsNullOrEmpty(Game1.player.spouse) && npc.Name.Equals(Game1.player.spouse, StringComparison.OrdinalIgnoreCase);

        // Config class holding mod settings
        private sealed class ModConfig
        {
            public int EventDistance { get; set; } = 3; // Distance in tiles
            public int EmoteDelay { get; set; } = 600;  // Delay before NPC emotes
            public int EventDistanceInPixels => EventDistance * 64; // Convert tile to pixel
        }
    }
}
