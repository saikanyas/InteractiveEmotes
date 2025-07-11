using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InteractiveEmotes
{
    /// <summary>The main entry point for the mod.</summary>
    public sealed class ModEntry : Mod
    {
        /// <summary>Handles the logic for triggering NPC reactions.</summary>
        private EmoteReactionHandler _reactionHandler = null!;

        /// <summary>Stores all loaded reaction rules from the JSON files.</summary>
        private Dictionary<string, EmoteReactionData> _reactionRules = new();

        /// <summary>The mod's configuration settings.</summary>
        private ModConfig _config = null!;

        /// <summary>Processes the logic for matching reaction rules against conditions.</summary>
        private RuleProcessor _ruleProcessor = null!;

        /// <summary>A static instance of this mod entry class for easy access.</summary>
        internal static ModEntry? Instance { get; private set; }

        /// <summary>The mod's main entry method, called once when the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Instance = this;
            _config = helper.ReadConfig<ModConfig>();

            LoadReactionRules();

            // Initialize core components of the mod
            _ruleProcessor = new RuleProcessor();
            var animationHandler = new NpcAnimationHandler(this.Monitor);
            var emoteNameToActionId = new Dictionary<string, int>();

            // Defer handler initialization until the game is launched and all data is available
            helper.Events.GameLoop.GameLaunched += (s, e) =>
            {
                // Populate the emote map from the game's data
                foreach (var emote in Farmer.EMOTES)
                {
                    if (emote.emoteString != null && !emoteNameToActionId.ContainsKey(emote.emoteString))
                    {
                        emoteNameToActionId.Add(emote.emoteString, emote.emoteIconIndex);
                    }
                }

                // Initialize the main handlers with all necessary dependencies
                var comboHandler = new EmoteComboHandler(_config, helper.Translation, this.Monitor, _ruleProcessor, emoteNameToActionId, animationHandler);
                _reactionHandler = new EmoteReactionHandler(_config, this.Monitor, helper.Translation, _ruleProcessor, _reactionRules, emoteNameToActionId, animationHandler, comboHandler);

                SetUpConfigMenu(_config, helper.Translation);
            };

            // Hook into game events
            helper.Events.GameLoop.DayStarted += (_, _) => _reactionHandler?.ClearDailyRewards();

            // Apply Harmony patches
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.performPlayerEmote), new[] { typeof(string) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(PerformPlayerEmote_Postfix))
            );

            // Register console commands
            RegisterConsoleCommands(helper);
        }

        /// <summary>A Harmony postfix patch that triggers the mod's logic after a player performs an emote.</summary>
        public static void PerformPlayerEmote_Postfix(Farmer __instance, string emote_string)
        {
            // Ensure the mod is ready and the emote is performed by the local player in a valid context
            if (Instance == null || !__instance.IsLocalPlayer || !Context.CanPlayerMove || Instance._reactionHandler == null)
                return;
            Instance._reactionHandler.ProcessPlayerEmote(__instance, emote_string);
        }

        /// <summary>Loads reaction rules by reading and merging 'reactions.json' and 'combos.json'.</summary>
        private void LoadReactionRules()
        {
            // 1. Load immediate reactions
            var immediateReactions = this.Helper.Data.ReadJsonFile<Dictionary<string, EmoteReactionData>>("assets/reactions.json");
            if (immediateReactions == null)
            {
                this.Monitor.Log("Could not find 'assets/reactions.json'. A default one will be created.", LogLevel.Warn);
                immediateReactions = new Dictionary<string, EmoteReactionData>();
                this.Helper.Data.WriteJsonFile("assets/reactions.json", immediateReactions);
            }

            // 2. Load combo reactions
            var comboReactions = this.Helper.Data.ReadJsonFile<Dictionary<string, EmoteReactionData>>("assets/combos.json");
            if (comboReactions == null)
            {
                this.Monitor.Log("Could not find 'assets/combos.json'. A default one will be created.", LogLevel.Warn);
                comboReactions = new Dictionary<string, EmoteReactionData>();
                this.Helper.Data.WriteJsonFile("assets/combos.json", comboReactions);
            }

            // 3. Merge data from both files into a single dictionary
            _reactionRules = new Dictionary<string, EmoteReactionData>();

            foreach (var entry in immediateReactions)
            {
                if (!_reactionRules.ContainsKey(entry.Key))
                {
                    _reactionRules[entry.Key] = new EmoteReactionData();
                }
                _reactionRules[entry.Key].Reactions = entry.Value.Reactions;
            }

            foreach (var entry in comboReactions)
            {
                if (!_reactionRules.ContainsKey(entry.Key))
                {
                    _reactionRules[entry.Key] = new EmoteReactionData();
                }
                _reactionRules[entry.Key].ComboReactions = entry.Value.ComboReactions;
            }

            this.Monitor.Log("Successfully loaded and merged 'reactions.json' and 'combos.json'.", LogLevel.Info);
        }

        /// <summary>Registers all console commands for the mod.</summary>
        private void RegisterConsoleCommands(IModHelper helper)
        {
            helper.ConsoleCommands.Add("reactions_reload", "Reloads the reaction and combo JSON files.", (cmd, args) =>
            {
                LoadReactionRules();
                _reactionHandler.UpdateRules(_reactionRules);
                this.Monitor.Log("Reaction and combo rules have been reloaded and applied.", LogLevel.Info);
            });

            helper.ConsoleCommands.Add("reactions_reset", "Resets the reaction and combo JSON files to default (empty).", (cmd, args) =>
            {
                var reactionsFile = new FileInfo(Path.Combine(this.Helper.DirectoryPath, "assets", "reactions.json"));
                if (reactionsFile.Exists) reactionsFile.Delete();

                var combosFile = new FileInfo(Path.Combine(this.Helper.DirectoryPath, "assets", "combos.json"));
                if (combosFile.Exists) combosFile.Delete();

                LoadReactionRules();
                _reactionHandler.UpdateRules(_reactionRules);
                this.Monitor.Log("Reaction and combo rules have been reset and applied.", LogLevel.Info);
            });

            helper.ConsoleCommands.Add("reactions_inspect", "Inspects the character in front of the player to predict reaction outcomes.\n\nUsage: reactions_inspect <emote_name>", (cmd, args) =>
            {
                if (!Context.IsWorldReady)
                {
                    this.Monitor.Log("This command can only be used in-game.", LogLevel.Error);
                    return;
                }

                if (args.Length == 0)
                {
                    this.Monitor.Log("Please provide an emote name to inspect. Usage: reactions_inspect <emote_name>", LogLevel.Error);
                    return;
                }

                string emoteString = args[0];
                Farmer player = Game1.player;

                var facingDirection = player.FacingDirection;
                var tileInFront = player.Tile;
                switch (facingDirection)
                {
                    case 0: tileInFront.Y -= 1; break; // Up
                    case 1: tileInFront.X += 1; break; // Right
                    case 2: tileInFront.Y += 1; break; // Down
                    case 3: tileInFront.X -= 1; break; // Left
                }
                Vector2 targetPixel = tileInFront * 64f + new Vector2(32f, 32f);

                List<Character> allCharactersInArea = new List<Character>();
                allCharactersInArea.AddRange(player.currentLocation.characters);

                if (player.currentLocation is Farm farm)
                {
                    foreach (var animal in farm.animals.Values) { allCharactersInArea.Add(animal); }
                }
                else if (player.currentLocation is AnimalHouse animalHouse)
                {
                    foreach (var animal in animalHouse.animals.Values) { allCharactersInArea.Add(animal); }
                }

                Character? targetCharacter = null;
                foreach (var character in allCharactersInArea)
                {
                    if (character == player) continue;
                    if (character.GetBoundingBox().Contains(targetPixel))
                    {
                        targetCharacter = character;
                        break;
                    }
                }

                if (targetCharacter == null)
                {
                    this.Monitor.Log($"No character found in front of you.", LogLevel.Info);
                    return;
                }

                this.Monitor.Log("----------------------------------------", LogLevel.Info);
                this.Monitor.Log($"Inspecting: {targetCharacter.Name}", LogLevel.Info);
                this.Monitor.Log($"- CharacterType: {_ruleProcessor.GetCharacterType(targetCharacter, player)}", LogLevel.Info);

                if (targetCharacter is NPC targetNpc)
                {
                    if (player.friendshipData.TryGetValue(targetNpc.Name, out var friendship))
                    {
                        this.Monitor.Log($"- Friendship: {friendship.Points} ({friendship.Points / 250} hearts)", LogLevel.Info);
                    }
                    else
                    {
                        this.Monitor.Log($"- Friendship: 0 (0 hearts)", LogLevel.Info);
                    }
                    this.Monitor.Log($"- IsSpouse: {player.spouse == targetNpc.Name}", LogLevel.Info);
                    this.Monitor.Log($"- IsDateable: {targetNpc.datable.Value}", LogLevel.Info);
                }

                this.Monitor.Log("----------------------------------------", LogLevel.Info);

                if (!_reactionRules.TryGetValue(emoteString, out var emoteData))
                {
                    this.Monitor.Log($"No rules found for emote '{emoteString}' in JSON files.", LogLevel.Warn);
                    return;
                }

                var matchingReaction = _ruleProcessor.FindMatchingRule(emoteData.Reactions, player, targetCharacter, _config);
                if (matchingReaction != null)
                {
                    this.Monitor.Log($"[MATCH FOUND] Immediate reaction would be triggered.", LogLevel.Info);
                    this.Monitor.Log($"  - Action: {Newtonsoft.Json.JsonConvert.SerializeObject(matchingReaction.Action)}", LogLevel.Info);
                }
                else
                {
                    this.Monitor.Log("[NO MATCH] No immediate reaction rule matched.", LogLevel.Info);
                }

                var matchingCombo = _ruleProcessor.FindMatchingRule(emoteData.ComboReactions, player, targetCharacter, _config);
                if (matchingCombo != null)
                {
                    this.Monitor.Log($"[MATCH FOUND] Combo reaction rule is available.", LogLevel.Info);
                    this.Monitor.Log($"  - TriggerCount: {matchingCombo.TriggerCount ?? _config.GlobalComboTarget}", LogLevel.Info);
                }
                else
                {
                    this.Monitor.Log("[NO MATCH] No combo reaction rule matched.", LogLevel.Info);
                }
            });

            helper.ConsoleCommands.Add("debug_dialogue", "Forces a dialogue debug on the NPC in front of the player.", (cmd, args) =>
            {
                if (!Context.IsWorldReady)
                {
                    this.Monitor.Log("This command must be used in-game.", LogLevel.Error);
                    return;
                }

                Farmer player = Game1.player;
                var facingDirection = player.FacingDirection;
                var tileInFront = player.Tile;
                switch (facingDirection)
                {
                    case 0: tileInFront.Y -= 1; break;
                    case 1: tileInFront.X += 1; break;
                    case 2: tileInFront.Y += 1; break;
                    case 3: tileInFront.X -= 1; break;
                }

                NPC? targetNpc = null;
                Vector2 targetPixel = tileInFront * 64f + new Vector2(32f, 32f);
                foreach (var character in player.currentLocation.characters)
                {
                    if (character is NPC npc && npc.GetBoundingBox().Contains(targetPixel))
                    {
                        targetNpc = npc;
                        break;
                    }
                }

                if (targetNpc != null)
                {
                    DebugDialogueProperties(targetNpc);
                    this.Monitor.Log($"Debug command executed on {targetNpc.Name}. Check the log above for properties.", LogLevel.Info);
                }
                else
                {
                    this.Monitor.Log("No NPC found in front of you.", LogLevel.Warn);
                }
            });
        }

        /// <summary>A developer utility method to inspect the properties of a dialogue object for a given NPC.</summary>
        private void DebugDialogueProperties(NPC npc)
        {
            string testText = "Hello, @! How is %farm farm today?";
            this.Monitor.Log($"Running debug for NPC: {npc.Name} with test text: '{testText}'", LogLevel.Info);

            try
            {
                npc.CurrentDialogue.Push(new StardewValley.Dialogue(npc, testText));
                var tempDialogue = npc.CurrentDialogue.Peek();

                this.Monitor.Log("----------------------------------------------------------------", LogLevel.Alert);
                this.Monitor.Log("--- [DEBUG] Properties of StardewValley.Dialogue ---", LogLevel.Alert);
                foreach (var prop in tempDialogue.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    try
                    {
                        var value = prop.GetValue(tempDialogue, null);
                        this.Monitor.Log($"[PROPERTY] Name: {prop.Name,-25} | Value: '{value}'", LogLevel.Alert);
                    }
                    catch (Exception) { /* Some properties may not be readable. */ }
                }
                this.Monitor.Log("--- END DEBUG ---", LogLevel.Alert);

                npc.CurrentDialogue.Pop();
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"An error occurred during the debug process: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>Sets up the Generic Mod Config Menu (GMCM) for this mod.</summary>
        private void SetUpConfigMenu(ModConfig config, ITranslationHelper i18n)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this._config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this._config)
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