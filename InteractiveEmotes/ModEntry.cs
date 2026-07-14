using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using HarmonyLib;
using System.Linq;

namespace InteractiveEmotes
{
    public sealed class ModEntry : Mod
    {
        
        public static ModEntry Instance { get; private set; } = null!;
        public ModConfig Config { get; private set; } = null!;

        // Stores all Rules from reactions.json + combos.json
        private static Dictionary<string, EmoteReactionData> _reactionRules = new();

        // Rule condition checker — Created once and shared across the mod
        internal static readonly RuleProcessor _ruleProcessor = new();

        // emote name → emote ID mapping for doEmote()
        // Created once on GameLaunched to avoid redundant looping every time an NPC reacts
        internal static Dictionary<string, int> _emoteNameToId = new();

        public override void Entry(IModHelper helper)
        {
            //Harmony
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.performPlayerEmote), new[] { typeof(string) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(PerformPlayerEmote_Postfix))
            );
            // On day start method

            this.Config = this.Helper.ReadConfig<ModConfig>();
            Instance = this;

            LoadReactionRules();

            // Create emote name→ID map and Setup GMCM on GameLaunched
            helper.Events.GameLoop.GameLaunched += (_, _) =>
            {
                foreach (var emote in Farmer.EMOTES)
                {
                    if (emote.emoteString != null && !_emoteNameToId.ContainsKey(emote.emoteString))
                    {
                        _emoteNameToId[emote.emoteString] = emote.emoteIconIndex;
                    }
                }

                // Initialize the Generic Mod Config Menu
                SetUpConfigMenu(this.Config, this.Helper.Translation);
            };

            // Register Console Commands
            ConsoleCommandHandler.RegisterCommands(this.Helper.ConsoleCommands);

            helper.Events.GameLoop.DayStarted += OnDayStarted;

        }

        private static void PerformPlayerEmote_Postfix(Farmer __instance, string emote_string)
        {
            if (!Context.IsWorldReady) return;
            if (__instance == null || !__instance.IsLocalPlayer || !__instance.canMove) return;
            Instance.Monitor.Log($"Emote performed: {emote_string}", LogLevel.Trace);
            var player = __instance;

            // ConditionGetter returns List<NPC> directly, preventing redundant lookups in HandleReaction
            var nearbyNpcs = ConditionGetter.GetNearbyNpcs(Instance.Config.EventDistance, player);

            EmoteReactionHandler.HandleReaction(nearbyNpcs, emote_string, _reactionRules, player, _ruleProcessor, _emoteNameToId, Instance.Helper.Translation);
            
        }

        //Clear awarded Npcs list method

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            EmoteReactionHandler.ClearDailyNpcLimits();
            // Reset all combo states overnight
            ComboHandler.ClearAllStates();
        }

        private void MigrateOldJsonFiles()
        {
            string emotesDirPath = System.IO.Path.Combine(this.Helper.DirectoryPath, "assets", "emotes");
            
            // If the emotes folder already exists, migration has already happened or user created it manually.
            if (System.IO.Directory.Exists(emotesDirPath))
            {
                return;
            }

            System.IO.Directory.CreateDirectory(emotesDirPath);
            this.Monitor.Log("Created assets/emotes/ directory for the new folder-based config structure.", LogLevel.Info);

            var oldReactions = this.Helper.Data.ReadJsonFile<Dictionary<string, EmoteReactionData>>("assets/reactions.json");
            var oldCombos = this.Helper.Data.ReadJsonFile<Dictionary<string, EmoteReactionData>>("assets/combos.json");
            
            if (oldReactions == null && oldCombos == null)
            {
                return; // Nothing to migrate
            }

            var mergedData = new Dictionary<string, EmoteReactionData>();

            if (oldReactions != null)
            {
                foreach (var kvp in oldReactions)
                {
                    mergedData[kvp.Key] = kvp.Value;
                }
            }

            if (oldCombos != null)
            {
                foreach (var kvp in oldCombos)
                {
                    if (!mergedData.ContainsKey(kvp.Key))
                    {
                        mergedData[kvp.Key] = new EmoteReactionData();
                    }
                    mergedData[kvp.Key].ComboReactions = kvp.Value.ComboReactions;
                }
            }

            // Write each merged entry to its own file in assets/emotes/
            foreach (var kvp in mergedData)
            {
                // WriteJsonFile saves relative to the mod folder
                this.Helper.Data.WriteJsonFile($"assets/emotes/{kvp.Key}.json", kvp.Value);
            }

            this.Monitor.Log($"Successfully migrated {mergedData.Count} emotes into individual files inside assets/emotes/.", LogLevel.Info);

            // Rename old files to .bak
            string oldReactionsPath = System.IO.Path.Combine(this.Helper.DirectoryPath, "assets", "reactions.json");
            if (System.IO.File.Exists(oldReactionsPath))
            {
                System.IO.File.Move(oldReactionsPath, oldReactionsPath + ".bak");
            }

            string oldCombosPath = System.IO.Path.Combine(this.Helper.DirectoryPath, "assets", "combos.json");
            if (System.IO.File.Exists(oldCombosPath))
            {
                System.IO.File.Move(oldCombosPath, oldCombosPath + ".bak");
            }
        }

        // Load Rule data from assets/emotes/*.json
        // Internal access to allow ConsoleCommandHandler to reload rules
        internal void LoadReactionRules()
        {
            MigrateOldJsonFiles();

            _reactionRules.Clear();
            string emotesDirPath = System.IO.Path.Combine(this.Helper.DirectoryPath, "assets", "emotes");
            
            if (!System.IO.Directory.Exists(emotesDirPath))
            {
                this.Monitor.Log("assets/emotes directory not found. No reactions will trigger.", LogLevel.Warn);
                return;
            }

            string[] files = System.IO.Directory.GetFiles(emotesDirPath, "*.json");

            foreach (string file in files)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                var emoteData = this.Helper.Data.ReadJsonFile<EmoteReactionData>($"assets/emotes/{System.IO.Path.GetFileName(file)}");

                if (emoteData != null)
                {
                    _reactionRules[fileName] = emoteData;
                }
            }

            this.Monitor.Log($"Loaded rules for {_reactionRules.Count} emotes from assets/emotes/.", LogLevel.Debug);
        }



        // GMCM Register
        private void SetUpConfigMenu(ModConfig config, ITranslationHelper i18n)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => i18n.Get("config.title.interactiveEmote.name")
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.delay.name"),
                tooltip: () => i18n.Get("config.delay.tooltip"),
                getValue: () => config.EmoteDelay,
                setValue: val => config.EmoteDelay = val,
                min: 0,
                max: 5000,
                interval: 100
            );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.distance.name"),
                tooltip: () => i18n.Get("config.distance.tooltip"),
                getValue: () => config.EventDistance,
                setValue: val => config.EventDistance = val,
                min: 1,
                max: 15
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => i18n.Get("config.title.friendship.name")
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.friendship.amount.name"),
                tooltip: () => i18n.Get("config.friendship.amount.tooltip"),
                getValue: () => config.FriendshipGainAmount,
                setValue: val => config.FriendshipGainAmount = val,
                min: 0,
                max: 250
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.friendship.notify.name"),
                tooltip: () => i18n.Get("config.friendship.notify.tooltip"),
                getValue: () => config.ShowFriendshipGainMessage,
                setValue: val => config.ShowFriendshipGainMessage = val
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => i18n.Get("config.title.playsound.name")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.sound.name"),
                tooltip: () => i18n.Get("config.sound.tooltip"),
                getValue: () => config.PlayReplySound,
                setValue: val => config.PlayReplySound = val
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => i18n.Get("config.title.emotecombo.name")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.emotecombo.name"),
                tooltip: () => i18n.Get("config.emotecombo.tooltip"),
                getValue: () => config.EmoteCombo,
                setValue: val => config.EmoteCombo = val
            );

            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.combo.mode.name"),
                tooltip: () => i18n.Get("config.combo.mode.tooltip"),
                getValue: () => config.ComboCountMode,
                setValue: val => config.ComboCountMode = val,
                allowedValues: new string[] { "PerCombo", "Fixed" }
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.combo.global_target.name"),
                tooltip: () => i18n.Get("config.combo.global_target.tooltip"),
                getValue: () => config.GlobalComboTarget,
                setValue: val => config.GlobalComboTarget = val,
                min: 2,
                max: 10
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.combo.timeout.name"),
                tooltip: () => i18n.Get("config.combo.timeout.tooltip"),
                getValue: () => config.ComboTimeout,
                setValue: val => config.ComboTimeout = val,
                min: 600,
                max: 6000,
                interval: 100,
                formatValue: val => $"{val / 60.0f:F1} second"
             );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => i18n.Get("config.title.rules.name")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.rules.weather.name"),
                tooltip: () => i18n.Get("config.rules.weather.tooltip"),
                getValue: () => config.EnableWeatherConditions,
                setValue: val => config.EnableWeatherConditions = val
            );
            configMenu.AddBoolOption(
               mod: this.ModManifest,
               name: () => i18n.Get("config.rules.season.name"),
               tooltip: () => i18n.Get("config.rules.season.tooltip"),
               getValue: () => config.EnableSeasonConditions,
               setValue: val => config.EnableSeasonConditions = val
           );
            configMenu.AddBoolOption(
               mod: this.ModManifest,
               name: () => i18n.Get("config.rules.friendship.name"),
               tooltip: () => i18n.Get("config.rules.friendship.tooltip"),
               getValue: () => config.EnableFriendshipConditions,
               setValue: val => config.EnableFriendshipConditions = val
           );
        }


    }


}
