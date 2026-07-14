using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;

namespace InteractiveEmotes
{
    // ============================================================
    // ComboHandler.cs — ระบบนับ emote ซ้ำแล้วปลดล็อค Combo
    //
    // กลไก:
    //   1. ผู้เล่นทำ emote → ProcessCombo() เพิ่ม count
    //   2. ถ้า count ถึง TriggerCount → เล่น ComboAction
    //   3. ถ้าหยุดทำ emote นานเกิน ComboTimeout ticks → reset count
    //
    // State เก็บแยกต่อ Farmer (UniqueMultiplayerID) ต่อ NPC
    // เพื่อรองรับ multiplayer
    // ============================================================

    /// <summary>เก็บสถานะ combo ของ NPC คนหนึ่งต่อ Farmer คนหนึ่ง</summary>
    public class NpcComboState
    {
        /// <summary>Game tick ของ emote ล่าสุด ใช้คำนวณ timeout</summary>
        public long LastEmoteTime { get; set; } = 0;

        /// <summary>จำนวนครั้งที่ทำ emote แต่ละตัว
        /// Key = ชื่อ emote เช่น "happy", Value = จำนวนครั้ง</summary>
        public Dictionary<string, int> EmoteCounts { get; } = new Dictionary<string, int>();

        /// <summary>NPC กำลัง react อยู่ไหม ถ้าใช่จะไม่รับ combo ซ้อน</summary>
        public bool IsReacting { get; set; } = false;
    }

    public static class ComboHandler
    {
        // State ทั้งหมด: Farmer ID → (NPC Name → NpcComboState) (internal ให้ console command เข้าถึงได้)
        internal static readonly Dictionary<long, Dictionary<string, NpcComboState>> _comboStates
            = new Dictionary<long, Dictionary<string, NpcComboState>>();

        private static readonly Random _random = new Random();

        // ============================================================
        // ProcessCombo — จุดเริ่มต้น เรียกก่อน immediate reaction
        // ============================================================

        /// <summary>ประมวลผล emote สำหรับระบบ Combo
        /// คืน true ถ้า Combo ถูก trigger (ให้ caller ข้าม immediate reaction)</summary>
        public static bool ProcessCombo(
            Farmer player,
            NPC npc,
            string emoteString,
            List<ComboRule> rules,
            ModConfig config,
            RuleProcessor ruleProcessor,  // [เพิ่มใหม่] รับจากภายนอก ไม่สร้างใหม่ทุกครั้ง
            ITranslationHelper i18n)
        {
            // ถ้า config ปิด Combo หรือไม่มี Rule → ไม่ทำอะไร
            if (!config.EmoteCombo || rules.Count == 0)
            {
                return false;
            }

            NpcComboState state = GetOrCreateState(player, npc);

            // ถ้าหยุดทำ emote นานเกิน timeout → reset count ของ NPC คนนี้
            if (Game1.ticks - state.LastEmoteTime > config.ComboTimeout)
            {
                state.EmoteCounts.Clear();
            }

            // เพิ่ม count ของ emote นี้
            state.EmoteCounts.TryGetValue(emoteString, out int currentCount);
            currentCount++;
            state.EmoteCounts[emoteString] = currentCount;
            state.LastEmoteTime = Game1.ticks;

            // หา Rule ที่เงื่อนไขผ่าน
            // [หมายเหตุ] RuleProcessor ใช้ instance จาก ModEntry ที่สร้างไว้แล้ว
            // ถ้าต้องการ inject แทนให้แก้ตรงนี้ในอนาคต
            ComboRule? matchingRule = FindMatchingComboRule(rules, player, npc, config, ruleProcessor);

            if (matchingRule == null)
            {
                return false;
            }

            // คำนวณจำนวน emote ที่ต้องทำเพื่อ trigger combo
            int triggerTarget = GetTriggerTarget(matchingRule, config);

            if (currentCount >= triggerTarget)
            {
                // Trigger! reset count แล้วเล่น action
                state.EmoteCounts.Remove(emoteString);

                // ป้องกัน combo ซ้อนถ้า NPC กำลัง react อยู่
                if (!state.IsReacting)
                {
                    ExecuteComboAction(state, npc, matchingRule.Action, config, i18n);
                }

                return true;
            }

            return false;
        }

        // ============================================================
        // ExecuteComboAction — เล่น action ของ Combo
        // ============================================================

        private static void ExecuteComboAction(NpcComboState state, NPC npc, ComboAction action, ModConfig config, ITranslationHelper i18n)
        {
            state.IsReacting = true;

            // หน่วงเวลาก่อนตอบสนอง (EmoteDelay + สุ่มเพิ่ม 0-300ms เพื่อความเป็นธรรมชาติ)
            int delay = config.EmoteDelay + _random.Next(0, 300);

            NPC capturedNpc = npc;
            NpcComboState capturedState = state;

            DelayedAction.functionAfterDelay(() =>
            {
                bool didSomething = false;

                // --- เล่น Emote หรือ Animation ---
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
                        // [แก้ไข] ใช้ dict O(1) แทนการวนลูป Farmer.EMOTES
                        if (ModEntry._emoteNameToId.TryGetValue(emoteName, out int emoteId))
                        {
                            capturedNpc.doEmote(emoteId);
                            didSomething = true;
                        }
                    }
                }

                // --- แสดง DisplayText ---
                if (action.DisplayText != null)
                {
                    int textDelay = didSomething ? 1200 : 0;
                    object capturedDisplayText = action.DisplayText;

                    DelayedAction.functionAfterDelay(
                        () => ShowComboText(capturedNpc, capturedDisplayText, i18n),
                        textDelay
                    );
                }

                // เสร็จแล้วคืนสถานะ IsReacting
                capturedState.IsReacting = false;

                ModEntry.Instance.Monitor.Log($"[Combo] {capturedNpc.Name} triggered combo: {emoteName}", LogLevel.Trace);

            }, delay);
        }

        /// <summary>แสดงข้อความเหนือหัว NPC สำหรับ Combo (ใช้ Pool เดียวกับ EmoteReactionHandler ไม่ได้เพราะ static)
        /// ดังนั้นใช้การสุ่มธรรมดาสำหรับ Combo ก่อน</summary>
        private static void ShowComboText(NPC npc, object displayTextObj, ITranslationHelper i18n)
        {
            List<string>? options = GetAllStrings(displayTextObj);

            if (options == null || options.Count == 0)
            {
                return;
            }

            // สุ่มเลือก 1 key จาก options
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

        /// <summary>หา ComboRule แรกที่เงื่อนไขผ่าน ใช้ RuleProcessor เดียวกันกับ immediate reaction</summary>
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
            // ใช้ FindMatchingRule ไม่ได้โดยตรงเพราะ ComboRule ไม่ใช่ ReactionRule
            // จึง delegate การเช็คเงื่อนไขผ่าน ReactionRule wrapper ชั่วคราว
            // [หมายเหตุ] ถ้าอยากให้สะอาดกว่านี้ ย้าย AreConditionsMet ไปใน RuleProcessor แบบ public
            var tempRule = new ReactionRule { Conditions = conditions, Action = "" };
            var tempList = new List<ReactionRule> { tempRule };
            return processor.FindMatchingRule(tempList, farmer, npc, config) != null;
        }

        private static int GetTriggerTarget(ComboRule rule, ModConfig config)
        {
            // ComboCountMode "Fixed" → ทุก Rule ใช้ GlobalComboTarget
            // "PerCombo" → ใช้ TriggerCount ของ Rule นั้น (ถ้าไม่มีใช้ GlobalComboTarget)
            if (config.ComboCountMode.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
            {
                return config.GlobalComboTarget;
            }

            return rule.TriggerCount ?? config.GlobalComboTarget;
        }

        private static NpcComboState GetOrCreateState(Farmer player, NPC npc)
        {
            // ดึง dict ของ player นี้ (หรือสร้างใหม่)
            if (!_comboStates.TryGetValue(player.UniqueMultiplayerID, out var playerStates))
            {
                playerStates = new Dictionary<string, NpcComboState>();
                _comboStates[player.UniqueMultiplayerID] = playerStates;
            }

            // ดึง state ของ NPC นี้ (หรือสร้างใหม่)
            if (!playerStates.TryGetValue(npc.Name, out var npcState))
            {
                npcState = new NpcComboState();
                playerStates[npc.Name] = npcState;
            }

            return npcState;
        }

        /// <summary>Reset state ทั้งหมด เรียกตอนข้ามวัน</summary>
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
            // [แก้ไข] null safety
            if (Game1.player.hasPet()) text = text.Replace("%pet", Game1.player.getPetName() ?? "");
            Farmer? spouse = speaker.getSpouse();
            if (spouse != null) text = text.Replace("%spouse", spouse.displayName ?? "");
            return text;
        }
    }
}
