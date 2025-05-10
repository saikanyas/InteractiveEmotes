using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;

namespace InteractiveEmotes
{
    /// <summary>
    /// The main entry point for the InteractiveEmotes mod.
    /// Handles initialization, events, Harmony patching, and core logic.
    /// </summary>
    public sealed class ModEntry : Mod
    {
        /// <summary>Configuration data loaded from the config file.</summary>
        private ModConfig Config = null!;
        /// <summary>Translation helper for i18n (internationalization).</summary>
        private ITranslationHelper i18n = null!;
        /// <summary>
        /// Tracks NPCs who have already received friendship rewards today, per unique multiplayer player ID.
        /// This ensures each player can only reward each NPC once per day.
        /// </summary>
        private readonly Dictionary<long, HashSet<string>> rewardedNpcsToday = new();

        /// <summary>
        /// The system responsible for handling emote combos and combo reactions.
        /// </summary>
        private readonly EmoteComboBubbleSystem ComboSystem;

        /// <summary>
        /// Exposes the translation helper for use in other classes.
        /// </summary>
        public ITranslationHelper I18n => this.i18n;

        /// <summary>
        /// Singleton instance for access in static patches or helpers.
        /// </summary>
        internal static ModEntry? Instance { get; private set; }

        /// <summary>
        /// Initializes the ComboSystem to avoid nullable warnings.
        /// </summary>
        public ModEntry()
        {
            // Initialize readonly ComboSystem here to avoid nullable warning
            ComboSystem = new EmoteComboBubbleSystem(new ModConfig(), null!);
        }

        /// <summary>
        /// Entry point for the mod. Sets up events, loads config, and applies Harmony patches.
        /// </summary>
        /// <param name="helper">The mod helper provided by SMAPI.</param>
        public override void Entry(IModHelper helper)
        {
            Instance = this;
            this.i18n = helper.Translation;
            this.Config = helper.ReadConfig<ModConfig>();

            // Register event handlers
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += (_, _) => rewardedNpcsToday.Clear();

            // Harmony patch for NPC emote reactions
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(
                    typeof(StardewValley.Character),
                    nameof(StardewValley.Character.doEmote),
                    new[] { typeof(int), typeof(bool) }
                ),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(DoEmote_Postfix))
            );

            // Update ComboSystem config and i18n after loading config/translation
            ComboSystem.UpdateConfigAndI18n(this.Config, this.I18n);
        }


        /// <summary>
        /// Provides access to the current configuration.
        /// </summary>
        internal ModConfig GetConfig() => Config;

        /// <summary>
        /// Harmony postfix: triggers when any character (including player) performs an emote.
        /// Handles multiplayer and emote combo logic.
        /// </summary>
        /// <param name="__instance">The character who emotes.</param>
        /// <param name="whichEmote">The emote ID.</param>
        /// <param name="nextEventCommand">Unused, provided by Harmony signature.</param>
        public static void DoEmote_Postfix(Character __instance, int whichEmote, bool nextEventCommand)
        {
            if (__instance is Farmer farmer && Instance != null)
            {
                // Ensure we're handling only online farmers and meaningful emotes (> 1)
                if (Game1.getOnlineFarmers().Contains(farmer) && whichEmote > 1)
                {
                    _ = Instance.TriggerNpcReactions(farmer, whichEmote);

                    // Trigger emote combo system for all nearby NPCs
                    foreach (var npc in Instance.GetNearbyNpcs(farmer))
                    {
                        // **FIXED bug for multiplayer combo:**  
                        // Pass the correct farmer (who performed the emote) to the combo system,  
                        // so the combo and relationship checks use the right player in multiplayer.
                        Instance.ComboSystem.OnPlayerEmoteNPC(farmer, npc, whichEmote);
                    }
                }
            }
        }

        /// <summary>
        /// Triggers appropriate NPC reactions for a given emote, asynchronously.
        /// Handles friendship gain, emote combos, and special relationship logic.
        /// </summary>
        private async Task TriggerNpcReactions(Farmer farmer, int currentEmote)
        {
            if (!Context.CanPlayerMove)
                return;

            try
            {
                var npcs = GetNearbyNpcs(farmer).ToList();
                if (!npcs.Any())
                    return;

                var tasks = new List<Task>();
                switch (currentEmote)
                {
                    case 8:
                        foreach (var npc in npcs)
                            tasks.Add(HandleEmote(farmer, npc, 8));
                        break;
                    case 12:
                        foreach (var npc in npcs)
                            tasks.Add(HandleConditionalEmote(farmer, npc, 12, 16, 500));
                        break;
                    case 16:
                        foreach (var npc in npcs)
                            tasks.Add(HandleConditionalEmote(farmer, npc, 8, 16, 500));
                        break;
                    case 20:
                        foreach (var npc in npcs)
                            tasks.Add(HandleRomanticEmote(farmer, npc, 60));
                        break;
                    case 24:
                        foreach (var npc in npcs)
                            tasks.Add(HandleEmote(farmer, npc, 32));
                        break;
                    case 28:
                        foreach (var npc in npcs)
                            tasks.Add(HandleConditionalEmote(farmer, npc, 8, 28, 500));
                        break;
                    case 32:
                        foreach (var npc in npcs)
                            tasks.Add(HandleConditionalEmote(farmer, npc, 8, 32, 500));
                        break;
                    case 36:
                        foreach (var npc in npcs)
                            tasks.Add(HandleEmote(farmer, npc, 8));
                        break;
                    case 40:
                        foreach (var npc in npcs)
                            tasks.Add(HandleEmote(farmer, npc, 40));
                        break;
                    case 52:
                        foreach (var npc in npcs)
                            tasks.Add(HandleEmote(farmer, npc, 32));
                        break;
                    case 56:
                        foreach (var npc in npcs)
                            tasks.Add(HandleConditionalEmote(farmer, npc, 8, 32, 500));
                        break;
                    case 60:
                        foreach (var npc in npcs)
                            tasks.Add(HandleRomanticEmote(farmer, npc, 60));
                        break;
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"{i18n.Get("log.error")} {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Finds all NPCs within a defined distance from the farmer.
        /// </summary>
        private IEnumerable<NPC> GetNearbyNpcs(Farmer farmer)
        {
            var chars = farmer.currentLocation?.characters;
            if (chars is null) yield break;

            foreach (var npc in chars.OfType<NPC>())
                if (IsNpcClose(farmer, npc))
                    yield return npc;
        }

        /// <summary>
        /// Makes the specified NPC perform an emote and possibly grants friendship.
        /// </summary>
        private async Task HandleEmote(Farmer farmer, NPC npc, int id)
        {
            if (!IsNpcClose(farmer, npc)) return;
            await Task.Delay(Config.EmoteDelay + Game1.random.Next(0, 300));
            npc.doEmote(id, false);
            TryAddFriendship(farmer, npc);
            PlayNpcReplySound();
        }

        /// <summary>
        /// Handles conditional emote reactions based on friendship threshold.
        /// </summary>
        private async Task HandleConditionalEmote(Farmer farmer, NPC npc, int low, int high, int thr)
        {
            if (!IsNpcClose(farmer, npc)) return;
            await Task.Delay(Config.EmoteDelay + Game1.random.Next(0, 300));
            npc.doEmote(GetRelationship(farmer, npc) > thr ? high : low, false);
            TryAddFriendship(farmer, npc);
            PlayNpcReplySound();
        }

        /// <summary>
        /// Handles special romantic emote reactions if the NPC is the player's spouse.
        /// </summary>
        private async Task HandleRomanticEmote(Farmer farmer, NPC npc, int partnerE)
        {
            if (!IsNpcClose(farmer, npc)) return;
            await Task.Delay(Config.EmoteDelay + Game1.random.Next(0, 300));

            if (!IsRomanticPartner(farmer, npc))
            {
                npc.doEmote(32, false);
                TryAddFriendship(farmer, npc);
                PlayNpcReplySound();
                return;
            }

            int rel = GetRelationship(farmer, npc);
            if (rel > 2500) npc.doEmote(20, false);
            else if (rel > 2000) npc.doEmote(partnerE, false);
            else npc.doEmote(32, false);

            TryAddFriendship(farmer, npc);
            PlayNpcReplySound();
        }

        /// <summary>
        /// Attempts to add friendship to the NPC, ensuring only once per day per player.
        /// Also displays a HUD message if enabled.
        /// </summary>
        private void TryAddFriendship(Farmer farmer, NPC npc)
        {
            if (!farmer.friendshipData.ContainsKey(npc.Name)) return;

            if (!rewardedNpcsToday.TryGetValue(farmer.UniqueMultiplayerID, out var set))
            {
                set = new HashSet<string>();
                rewardedNpcsToday[farmer.UniqueMultiplayerID] = set;
            }
            if (!set.Add(npc.Name)) return; // Already rewarded today

            farmer.changeFriendship(Config.FriendshipGainAmount, npc);

            if (npc.Name == null)
            {
                this.Monitor.Log("Error: The NPC name is null.", LogLevel.Alert);
                return;
            }
                
            // Show HUD message only for local player
            if (Config.ShowFriendshipGainMessage && farmer.IsLocalPlayer)
                Game1.addHUDMessage(new HUDMessage($"+{Config.FriendshipGainAmount} {npc.displayName}", HUDMessage.newQuest_type));
        }

        /// <summary>
        /// Returns true if the NPC is within the configured event distance (in pixels) from the farmer.
        /// </summary>
        private static bool IsNpcClose(Farmer farmer, NPC npc)
        {
            int eventDistanceInPixels = ModEntry.Instance?.GetConfig()?.EventDistanceInPixels ?? 192;
            return Vector2.Distance(npc.Position, farmer.Position) < eventDistanceInPixels;
        }

        /// <summary>
        /// Gets the farmer's friendship level with the given NPC.
        /// </summary>
        private static int GetRelationship(Farmer farmer, NPC npc) => farmer.getFriendshipLevelForNPC(npc.Name);

        /// <summary>
        /// Returns true if the NPC is the farmer's spouse.
        /// </summary>
        private static bool IsRomanticPartner(Farmer farmer, NPC npc) => !string.IsNullOrEmpty(farmer.spouse) && npc.Name.Equals(farmer.spouse, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Play a Sound after Interact Emote.
        /// </summary>
        private void PlayNpcReplySound()
        {
            if (Config.PlayReplySound)
            {
                Game1.playSound("pickUpItem");
            }
        }

        /// <summary>
        /// Registers this mod's config with the Generic Mod Config Menu, if installed.
        /// Adds all configurable options for the user.
        /// </summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => i18n.Get("config.title.interactiveEmote.name"),
                tooltip: () => "" 
                );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.delay.name"),
                tooltip: () => i18n.Get("config.delay.tooltip"),
                getValue: () => Config.EmoteDelay,
                setValue: val => Config.EmoteDelay = val,
                min: 400, max: 1200, interval: 100,
                fieldId: "EmoteDelay"
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.distance.name"),
                tooltip: () => i18n.Get("config.distance.tooltip"),
                getValue: () => Config.EventDistance,
                setValue: val => Config.EventDistance = val,
                min: 1, max: 10, interval: 1,
                fieldId: "EventDistance"
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.friendship.amount.name"),
                tooltip: () => i18n.Get("config.friendship.amount.tooltip"),
                getValue: () => Config.FriendshipGainAmount,
                setValue: val => Config.FriendshipGainAmount = val,
                min: 1, max: 100, interval: 1,
                fieldId: "FriendshipGainAmount"
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.friendship.notify.name"),
                tooltip: () => i18n.Get("config.friendship.notify.tooltip"),
                getValue: () => Config.ShowFriendshipGainMessage,
                setValue: val => Config.ShowFriendshipGainMessage = val,
                fieldId: "ShowFriendshipGainMessage"
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => i18n.Get("config.title.emotecombo.name"),
                tooltip: () => ""
                );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.emotecombo.name"),
                tooltip: () => i18n.Get("config.emotecombo.tooltip"),
                getValue: () => Config.EmoteCombo,
                setValue: val => Config.EmoteCombo = val,
                fieldId: "EmoteCombo"
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.emotecombo.target.name"),
                tooltip: () => i18n.Get("config.emotecombo.target.tooltip"),
                getValue: () => Config.EmoteComboTarget,
                setValue: val => Config.EmoteComboTarget = val,
                min: 1, max: 10, interval: 1,
                fieldId: "EmoteComboTriggerCount"
            );
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => i18n.Get("config.title.playsound.name"),
                tooltip: () => ""
                );
            
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => i18n.Get("config.sound.name"),
                tooltip: () => i18n.Get("config.sound.tooltip"),
                getValue: () => Config.PlayReplySound,
                setValue: val => Config.PlayReplySound = val,
                fieldId: "PlaySound"
            );
        }

        /// <summary>
        /// Holds all configurable settings for this mod.
        /// </summary>
        public sealed class ModConfig
        {
            /// <summary>The base distance (in tiles) to check for nearby NPCs.</summary>
            public int EventDistance { get; set; } = 3;
            /// <summary>Delay (in ms) before NPC reacts to an emote.</summary>
            public int EmoteDelay { get; set; } = 600;
            /// <summary>Amount of friendship given per successful emote interaction.</summary>
            public int FriendshipGainAmount { get; set; } = 10;
            /// <summary>Whether to display a HUD message when friendship is gained.</summary>
            public bool ShowFriendshipGainMessage { get; set; } = true;
            /// <summary>
            /// Distance in pixels (calculated from EventDistance).
            /// 1 tile = 64 pixels.
            /// </summary>
            public int EventDistanceInPixels => EventDistance * 64;
            /// <summary>Whether to enable the emote combo system.</summary>
            public bool EmoteCombo { get; set; } = true;
            /// <summary>How many consecutive emotes are required for a combo reaction.</summary>
            public int EmoteComboTarget { get; set; } = 3;
            /// <summary> Play a sound after interact emote </summary>
            public bool PlayReplySound { get; set; } = true;
        }
    }

    /// <summary>
    /// Handles the emote combo system.
    /// Tracks emote streaks per player per NPC and triggers special combo reactions.
    /// </summary>
    public class EmoteComboBubbleSystem
    {
        /// <summary>
        /// Stores combo states: for each player, for each NPC, the current combo info.
        /// </summary>
        private readonly Dictionary<long, Dictionary<string, ComboInfo>> comboStates = new();
        /// <summary>
        /// Mod configuration (for delays, combo target, etc.).
        /// </summary>
        private ModEntry.ModConfig config;
        /// <summary>
        /// Translation helper for dynamic combo texts.
        /// </summary>
        private ITranslationHelper i18n;

        /// <summary>
        /// Constructor for the combo system.
        /// </summary>
        public EmoteComboBubbleSystem(ModEntry.ModConfig config, ITranslationHelper i18n)
        {
            this.config = config;
            this.i18n = i18n;
        }

        /// <summary>
        /// Returns friendship level between player and NPC.
        /// </summary>
        private static int GetRelationship(Farmer farmer, NPC npc)
            => farmer.getFriendshipLevelForNPC(npc.Name);

        /// <summary>
        /// Checks if the NPC is the player's spouse.
        /// </summary>
        private static bool IsRomanticPartner(Farmer farmer, NPC npc)
        {
            return !string.IsNullOrEmpty(farmer.spouse)
                && npc.Name.Equals(farmer.spouse, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Updates the config and translation helper after reloading config or translations.
        /// </summary>
        public void UpdateConfigAndI18n(ModEntry.ModConfig config, ITranslationHelper i18n)
        {
            this.config = config;
            this.i18n = i18n;
        }

        /// <summary>
        /// Called whenever the player emotes at an NPC.
        /// Updates or resets the combo counter and triggers combo reactions.
        /// </summary>
        /// <param name="player">The farmer who performed the emote (important for multiplayer correctness).</param>
        /// <param name="npc">The NPC who received the emote.</param>
        /// <param name="emoteId">The emote ID performed by the player.</param>
        public void OnPlayerEmoteNPC(Farmer player, NPC npc, int emoteId)
        {
            if (!config.EmoteCombo)
                return;
            if (!Context.CanPlayerMove)
                return;

            var playerId = player.UniqueMultiplayerID;
            if (!comboStates.TryGetValue(playerId, out var npcCombos))
            {
                npcCombos = new Dictionary<string, ComboInfo>();
                comboStates[playerId] = npcCombos;
            }

            if (!npcCombos.TryGetValue(npc.Name, out var combo))
            {
                combo = new ComboInfo
                {
                    LastEmote = emoteId,
                    Count = 1,
                    ComboTarget = config.EmoteComboTarget > 1 ? config.EmoteComboTarget : 3,
                    LastTime = Game1.ticks
                };
                npcCombos[npc.Name] = combo;
            }
            else
            {
                // If a different emote or too much time has passed, reset the combo
                if (combo.LastEmote != emoteId || Game1.ticks - combo.LastTime > 2100) // 30 seconds (60 ticks = 1 second)
                {
                    combo.LastEmote = emoteId;
                    combo.Count = 1;
                    combo.ComboTarget = config.EmoteComboTarget > 1 ? config.EmoteComboTarget : 3;
                    combo.LastTime = Game1.ticks;
                }
                else
                {
                    combo.Count++;
                    combo.LastTime = Game1.ticks;
                }
            }

            // If the combo threshold is met, trigger the special combo reaction
            if (combo.Count >= combo.ComboTarget)
            {
                // **FIXED bug for multiplayer combo:**  
                // Pass the correct farmer (who performed the emote) to the combo reaction method,
                // so relationship and spouse checks use the right player in multiplayer.
                _ = TriggerNpcComboReaction(player, npc, emoteId); // fire and forget

                // Reset combo info for next streak
                npcCombos[npc.Name] = new ComboInfo
                {
                    LastEmote = emoteId,
                    Count = 0,
                    ComboTarget = config.EmoteComboTarget > 1 ? config.EmoteComboTarget : 3,
                    LastTime = Game1.ticks
                };
            }
        }

        /// <summary>
        /// Triggers a special combo reaction for the NPC, including emote and speech bubble.
        /// The reaction and text depend on the emote ID and relationship status.
        /// Use the correct player (not just Game1.player) for relationship checks.
        /// </summary>
        /// <param name="player">The farmer who performed the emote (important for multiplayer correctness).</param>
        /// <param name="npc">The NPC who reacts to the combo.</param>
        /// <param name="emoteId">The emote ID performed by the player.</param>
        private async Task TriggerNpcComboReaction(Farmer player, NPC npc, int emoteId)
        {
            string text;
            int friendship = player.getFriendshipLevelForNPC(npc.Name);
            switch (emoteId)
            {
                case 8:
                    await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                    npc.doEmote(8, false);
                    text = i18n != null ? i18n.Get("combo.npc.Question") : "...";
                    break;
                case 12:
                    await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                    npc.doEmote(16, false);
                    text = i18n != null ? i18n.Get("combo.npc.manyAngry") : "...";
                    break;
                case 16:
                    await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                    npc.doEmote(8, false);
                    text = i18n != null ? i18n.Get("combo.npc.many!") : "...";
                    break;
                case 20:
                    // **FIXED: Use relationship and spouse check for the correct player**
                    if (IsRomanticPartner(player, npc))
                    {
                        await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                        npc.doEmote(60, false); // emote love
                        string[] lovewithloverTexts = i18n != null
                            ? new string[] {
                                i18n.Get("combo.npc.manylove.withlover"),
                                i18n.Get("combo.npc.manylove.withlover2"),
                                i18n.Get("combo.npc.manylove.withlover3")
                            }
                            : new string[] { "...", "...","?" };
                        text = lovewithloverTexts[Game1.random.Next(lovewithloverTexts.Length)];
                    }
                    else if (friendship >= 1500)
                    {
                        await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                        npc.doEmote(32, false); // emote heart (likes a lot)
                        text = i18n != null ? i18n.Get("combo.npc.withnotlover.lowerfriendship.enoughfriendship") : "...";
                        string[] lovewithNotloverTexts = i18n != null
                            ? new string[] {
                                i18n.Get("combo.npc.withnotlover.enoughfriendship"),
                                i18n.Get("combo.npc.withnotlover.enoughfriendship2"),
                            }
                            : new string[] { "...", "..." };
                        text = lovewithNotloverTexts[Game1.random.Next(lovewithNotloverTexts.Length)];
                    }
                    else
                    {
                        await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                        npc.doEmote(8, false); // sweat/embarrassed
                        text = i18n != null ? i18n.Get("combo.npc.manylove.withnotlover.lowerfriendship") : "...";
                    }
                    break;
                case 24:
                    await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                    npc.doEmote(8, false);
                    string[] sleepTexts = i18n != null
                        ? new string[] {
                            i18n.Get("combo.npc.manySleep"),
                            i18n.Get("combo.npc.manySleep2")
                        }
                        : new string[] { "...", "..." };
                    text = sleepTexts[Game1.random.Next(sleepTexts.Length)];
                    break;
                case 28:
                    await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                    npc.doEmote(28, false);
                    string[] sadTexts = i18n != null
                        ? new string[] {
                            i18n.Get("combo.npc.manySad"),
                            i18n.Get("combo.npc.manySad2")
                        }
                        : new string[] { "...", "..." };
                    text = sadTexts[Game1.random.Next(sadTexts.Length)];
                    break;
                case 32:
                    await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                    npc.doEmote(32, false);
                    if (friendship > 1250)
                    {
                        string[] happyText = i18n != null
                       ? new string[] {
                            i18n.Get("combo.npc.manyHappy"),
                            i18n.Get("combo.npc.manyHappy2"),
                            i18n.Get("combo.npc.manyHappy3")
                       }
                       : new string[] { "...", "...", "..."};
                        text = happyText[Game1.random.Next(happyText.Length)];
                    }
                    else
                    {
                        string[] happyText = i18n != null
                       ? new string[] {
                            i18n.Get("combo.npc.manyHappy"),
                            i18n.Get("combo.npc.manyHappy3")
                       }
                       : new string[] { "...", "..." };
                        text = happyText[Game1.random.Next(happyText.Length)];
                    }
                    break;
                case 36:
                    await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                    npc.doEmote(8, false);
                    text = i18n != null ? i18n.Get("combo.npc.manySayNo") : "...";
                    break;
                case 40:
                    await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                    npc.doEmote(8, false);
                    text = i18n != null ? i18n.Get("combo.npc.manyPause") : "...";
                    break;
                case 56:
                    await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                    npc.doEmote(20, false);
                    if (friendship > 1250)
                    {
                        string[] happyText = i18n != null
                       ? new string[] {
                            i18n.Get("combo.npc.manyHappyNote"),
                            i18n.Get("combo.npc.manyHappyNote2"),
                            i18n.Get("combo.npc.manyHappyNote3")
                       }
                       : new string[] { "...", "...","..." };
                        text = happyText[Game1.random.Next(happyText.Length)];
                    }
                    else
                    {
                        string[] happyText = i18n != null
                       ? new string[] {
                            i18n.Get("combo.npc.manyHappyNote"),
                            i18n.Get("combo.npc.manyHappyNote2")
                       }
                       : new string[] { "...", "..." };
                        text = happyText[Game1.random.Next(happyText.Length)];
                    }
                    break;
                default:
                    await Task.Delay(config.EmoteDelay + Game1.random.Next(0, 300));
                    npc.doEmote(8, false);
                    text = i18n != null ? i18n.Get("combo.npc.dosomethingManyTimes") : "...";
                    break;
            }
            await Task.Delay(1350);
            npc.showTextAboveHead(text);
        }

        /// <summary>
        /// Holds combo streak state for one NPC for one player.
        /// </summary>
        private class ComboInfo
        {
            public int LastEmote;      // The last emote performed
            public int Count;          // How many times the streak has been continued
            public int ComboTarget;    // How many times needed for combo
            public long LastTime;      // The last tick the emote was used
        }
    }

    /// <summary>
    /// API interface for integrating with Generic Mod Config Menu.
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
        void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string>? tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string>? formatValue = null, string? fieldId = null);
    }
}