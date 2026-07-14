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

        // เก็บ Rule ทั้งหมดจาก reactions.json + combos.json
        private static Dictionary<string, EmoteReactionData> _reactionRules = new();

        // ตรวจเงื่อนไข Rule — สร้างครั้งเดียว ใช้ร่วมกันทั้ง mod
        internal static readonly RuleProcessor _ruleProcessor = new();

        // [เพิ่มใหม่] ชื่อ emote → emote ID ที่ใช้กับ doEmote()
        // สร้างครั้งเดียวตอน GameLaunched เพื่อหลีกเลี่ยงการวนลูปซ้ำทุกครั้งที่ NPC ตอบสนอง
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

            // [เพิ่มใหม่] สร้าง emote name→ID map และ Setup GMCM ตอน GameLaunched
            helper.Events.GameLoop.GameLaunched += (_, _) =>
            {
                foreach (var emote in Farmer.EMOTES)
                {
                    if (emote.emoteString != null && !_emoteNameToId.ContainsKey(emote.emoteString))
                    {
                        _emoteNameToId[emote.emoteString] = emote.emoteIconIndex;
                    }
                }

                // เรียกใช้ GMCM ที่เขียนไว้ด้านล่างแต่ลืมเรียก
                SetUpConfigMenu(this.Config, this.Helper.Translation);
            };

            // [เพิ่มใหม่] ลงทะเบียนคำสั่ง Console
            ConsoleCommandHandler.RegisterCommands(this.Helper.ConsoleCommands);

            helper.Events.GameLoop.DayStarted += OnDayStarted;

        }

        private static void PerformPlayerEmote_Postfix(Farmer __instance, string emote_string)
        {
            if (!Context.IsWorldReady) return;
            if (__instance == null || !__instance.IsLocalPlayer || !__instance.canMove) return;
            Instance.Monitor.Log($"Emote performed: {emote_string}", LogLevel.Trace);
            var player = __instance;

            // ConditionGetter คืน List<NPC> โดยตรง ไม่ต้องค้นหาซ้ำใน HandleReaction
            var nearbyNpcs = ConditionGetter.GetNearbyNpcs(Instance.Config.EventDistance, player);

            EmoteReactionHandler.HandleReaction(nearbyNpcs, emote_string, _reactionRules, player, _ruleProcessor, _emoteNameToId, Instance.Helper.Translation);
            
        }

        //Clear awarded Npcs list method

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            EmoteReactionHandler.ClearDailyNpcLimits();
            // [เพิ่มใหม่] reset combo state ทั้งหมดตอนข้ามวัน
            ComboHandler.ClearAllStates();
        }

        // [เพิ่มใหม่] โหลดข้อมูล Rule จาก reactions.json และ combos.json แล้ว merge เข้า _reactionRules
        // ปรับเป็น internal เพื่อให้ ConsoleCommandHandler เรียกใช้ได้ตอนพิมพ์ emote_reload
        internal void LoadReactionRules()
        {
            // --- โหลด reactions.json ---
            var reactions = this.Helper.Data.ReadJsonFile<Dictionary<string, EmoteReactionData>>("assets/reactions.json");

            if (reactions == null)
            {
                this.Monitor.Log("Could not load assets/reactions.json — no reactions will trigger.", LogLevel.Warn);
                reactions = new Dictionary<string, EmoteReactionData>();
            }

            _reactionRules = reactions;

            // --- โหลด combos.json แล้ว merge ComboReactions เข้า _reactionRules ---
            // combos.json มีโครงสร้างเดียวกันกับ reactions.json
            // แต่ใช้ key "ComboReactions" แทน "Reactions"
            var combos = this.Helper.Data.ReadJsonFile<Dictionary<string, EmoteReactionData>>("assets/combos.json");

            if (combos == null)
            {
                this.Monitor.Log("Could not load assets/combos.json — combos will not work.", LogLevel.Warn);
            }
            else
            {
                foreach (var entry in combos)
                {
                    // ถ้ายังไม่มี key นี้ใน _reactionRules ให้สร้างใหม่
                    if (!_reactionRules.ContainsKey(entry.Key))
                    {
                        _reactionRules[entry.Key] = new EmoteReactionData();
                    }

                    // copy ComboReactions จาก combos.json เข้าไปใน EmoteReactionData ที่มีอยู่
                    _reactionRules[entry.Key].ComboReactions = entry.Value.ComboReactions;
                }
            }

            this.Monitor.Log($"Loaded rules for {_reactionRules.Count} emotes (reactions + combos).", LogLevel.Debug);
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
