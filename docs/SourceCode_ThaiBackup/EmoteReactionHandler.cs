using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;

namespace InteractiveEmotes
{
    public class EmoteReactionHandler
    {
        // เก็บชื่อ NPC ที่ได้รับ friendship point วันนี้แล้ว (internal เพื่อให้คำสั่ง console อ่านได้)
        internal static readonly HashSet<string> NpcsAwardedToday = new HashSet<string>();

        // Random ใช้สำหรับ shuffle Pool
        private static readonly Random _random = new Random();

        // ============================================================
        // DisplayText Pool — Shuffle Bag สำหรับข้อความที่ไม่ซ้ำ
        //
        // Key  = "npcName|text1,text2,text3" (NPC + เนื้อหา array)
        //        ทำให้แยก Pool ได้ต่อ NPC ต่อ action โดยไม่ต้องใช้ ID
        // Value = Queue<string> ที่ถูก shuffle แล้ว (ดึงจากหน้าคิวทีละ 1)
        //
        // เมื่อ Queue ว่าง → เติมและ shuffle ใหม่ (วนรอบ)
        // Reset เมื่อ session จบ (เพราะเก็บใน memory เท่านั้น)
        // ============================================================
        private static readonly Dictionary<string, Queue<string>> _textPools = new Dictionary<string, Queue<string>>();

        // ============================================================
        // HandleReaction — จุดเริ่มต้นของการตอบสนอง
        // ============================================================

        public static void HandleReaction(
            List<NPC> npcs,                                    // [แก้ไข] รับ List<NPC> โดยตรง ไม่ต้องค้นหาซ้ำ
            string emoteString,
            Dictionary<string, EmoteReactionData> rules,
            Farmer player,
            RuleProcessor ruleProcessor,
            Dictionary<string, int> emoteNameToId,            // [เพิ่มใหม่] map ชื่อ→ID สำหรับ doEmote()
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

            int index = 0;

            foreach (NPC npc in npcs)                         // [แก้ไข] วนลูปจาก List<NPC> ตรงๆ
            {
                // เช็ค Combo ก่อนเสมอ ถ้า combo ถูก trigger → ข้าม immediate reaction ของ NPC คนนี้
                if (emoteData.ComboReactions.Count > 0 &&
                    ComboHandler.ProcessCombo(player, npc, emoteString, emoteData.ComboReactions, ModEntry.Instance.Config, ruleProcessor, i18n))
                {
                    index++;
                    continue;
                }

                ReactionRule? matchingRule = ruleProcessor.FindMatchingRule(emoteData.Reactions, player, npc, ModEntry.Instance.Config);

                if (matchingRule == null)
                {
                    ModEntry.Instance.Monitor.Log($"No matching rule for {npc.Name}.", LogLevel.Trace);
                    continue;
                }

                int delay = index * ModEntry.Instance.Config.EmoteDelay;

                if (delay == 0)
                {
                    ReactToNpc(npc, matchingRule, player, emoteNameToId, i18n);
                }
                else
                {
                    NPC capturedNpc = npc;
                    ReactionRule capturedRule = matchingRule;
                    Farmer capturedPlayer = player;

                    DelayedAction.functionAfterDelay(
                        () => ReactToNpc(capturedNpc, capturedRule, capturedPlayer, emoteNameToId, i18n),
                        delay
                    );
                }

                index++;
            }
        }

        // ============================================================
        // ReactToNpc — ให้ NPC คนเดียวตอบสนองตาม Rule
        // ============================================================

        private static void ReactToNpc(NPC npc, ReactionRule rule, Farmer player, Dictionary<string, int> emoteNameToId, ITranslationHelper i18n)
        {
            ComboAction? action = ParseAction(rule.Action);

            if (action == null)
            {
                return;
            }

            bool didSomething = false;

            // --- ให้ NPC แสดง Emote หรือ Animation ---
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
                    // [แก้ไข] ใช้ dict O(1) แทนการวนลูป Farmer.EMOTES ทุกครั้ง
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

            // --- แสดงข้อความเหนือหัว NPC ---
            if (action.DisplayText != null)
            {
                // ถ้าเพิ่งทำ emote ไป ให้รอ 1200ms ก่อนแสดงข้อความ
                // เพื่อให้ emote กับข้อความไม่แสดงพร้อมกันจนดูรก
                int textDelay = didSomething ? 1200 : 0;

                NPC capturedNpc = npc;
                object capturedDisplayText = action.DisplayText;

                DelayedAction.functionAfterDelay(
                    () => ShowDisplayText(capturedNpc, capturedDisplayText, i18n),
                    textDelay
                );
            }

            // --- เพิ่ม Friendship Point และเสียง ---
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
        // DisplayText — แสดงข้อความเหนือหัว NPC พร้อม Pool
        // ============================================================

        /// <summary>เลือกข้อความจาก Pool แล้วแสดงเหนือหัว NPC
        /// รองรับ "|" สำหรับแสดงข้อความหลายชุดทีละชุด</summary>
        private static void ShowDisplayText(NPC npc, object displayTextObj, ITranslationHelper i18n)
        {
            // ดึง text options ทั้งหมดจาก object (string เดี่ยวหรือ array)
            List<string>? textOptions = GetAllStrings(displayTextObj);

            if (textOptions == null || textOptions.Count == 0)
            {
                return;
            }

            // สร้าง pool key จาก NPC + เนื้อหาของ array
            // เพื่อให้แยก Pool ต่อ NPC ต่อ action ได้โดยไม่ต้องมี ID พิเศษ
            string poolKey = npc.Name + "|" + string.Join(",", textOptions);

            // ดึง translation key ออกจาก Pool (ไม่ซ้ำจนกว่า Pool จะหมด)
            string textKey = DrawFromPool(poolKey, textOptions);

            // แปล key เป็นข้อความจริง
            string translatedText = i18n.Get(textKey);

            // แทนที่ token พิเศษ เช่น @, %farm, %pet
            string parsedText = ParseTokens(translatedText, npc);

            // เช็คว่ามี "|" ไหม (สัญลักษณ์แบ่งข้อความยาวเป็นชุด)
            if (parsedText.Contains("|"))
            {
                string[] parts = parsedText.Split('|');

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    int partIndex = i;
                    NPC capturedNpc = npc;

                    // แสดงทีละชุด โดยหน่วงเวลา 1800ms ต่อชุด
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

        /// <summary>ดึง 1 string จาก Pool
        /// ถ้า Pool ว่างหรือยังไม่มี Pool สำหรับ key นี้ → สร้างและ shuffle ใหม่</summary>
        private static string DrawFromPool(string poolKey, List<string> allOptions)
        {
            // ถ้าไม่มี Pool สำหรับ key นี้ หรือ Pool ว่างแล้ว → สร้างใหม่
            if (!_textPools.ContainsKey(poolKey) || _textPools[poolKey].Count == 0)
            {
                _textPools[poolKey] = CreateShuffledQueue(allOptions);
            }

            // ดึงจากหน้าคิว (FIFO) 1 อัน
            return _textPools[poolKey].Dequeue();
        }

        /// <summary>คัดลอก list แล้วสุ่มลำดับด้วย Fisher-Yates Shuffle
        /// แล้วใส่เข้า Queue เพื่อให้ดึงแบบ FIFO ได้</summary>
        private static Queue<string> CreateShuffledQueue(List<string> options)
        {
            // คัดลอกก่อนเพื่อไม่แก้ list ต้นฉบับ
            List<string> shuffled = new List<string>(options);

            // Fisher-Yates Shuffle: สลับตำแหน่งแบบสุ่มจากหลังไปหน้า
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                string temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            return new Queue<string>(shuffled);
        }

        // ============================================================
        // Token Parser — แทนที่ token พิเศษในข้อความ
        // ============================================================

        /// <summary>แทนที่ token พิเศษด้วยค่าจริงของผู้เล่น/เกม</summary>
        private static string ParseTokens(string text, NPC speaker)
        {
            // "^" แยกข้อความตามเพศผู้เล่น รูปแบบ: "ข้อความชาย^ข้อความหญิง"
            if (text.Contains("^"))
            {
                string[] parts = text.Split('^');
                text = (parts.Length >= 2 && !Game1.player.IsMale) ? parts[1] : parts[0];
            }

            text = text.Replace("@", Game1.player.Name);
            text = text.Replace("%farm", Game1.player.farmName.Value);
            text = text.Replace("%favorite_thing", Game1.player.favoriteThing.Value);

            // [แก้ไข] เช็ค null ก่อน replace เพื่อป้องกัน NullReferenceException
            // จาก mod อื่นที่อาจทำให้ petName เป็น null ได้
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

        /// <summary>แปลง Action object (string หรือ JObject) เป็น ComboAction</summary>
        private static ComboAction? ParseAction(object actionObj)
        {
            // กรณี string ย่อ เช่น "Action": "happy"
            if (actionObj is string actionString)
            {
                return new ComboAction { Emote = actionString };
            }

            // กรณี object เช่น "Action": { "Emote": "happy", "DisplayText": "..." }
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

        /// <summary>รับ object ที่อาจเป็น string หรือ string[] แล้วคืน 1 string (สุ่มถ้าเป็น array)
        /// ใช้สำหรับ Emote ที่ต้องการแค่ 1 ค่า</summary>
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

        /// <summary>รับ object ที่อาจเป็น string หรือ string[] แล้วคืน List ทั้งหมด
        /// ใช้สำหรับ DisplayText ที่ต้องการ options ทั้งหมดเพื่อสร้าง Pool</summary>
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

        /// <summary>เพิ่ม friendship point ให้ NPC ถ้ายังไม่ได้รับในวันนี้ และแสดง HUD
        /// คืนค่า true ถ้าได้ point สำเร็จ, คืนค่า false ถ้าไม่ได้</summary>
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
                // ลองดึง text จาก i18n ถ้าไม่มีให้ใช้ค่า default (fallback)
                string msgKey = "message.friendship.gain";
                string translatedMsg = i18n.Get(msgKey, new { npcName = npc.displayName, amount = gainAmount });
                
                // ถ้าหาไฟล์แปลไม่เจอ มันจะคืนค่า key คืนมา (หรือมีวงเล็บ)
                if (translatedMsg == msgKey || translatedMsg.Contains("message.friendship"))
                {
                    translatedMsg = $"+{gainAmount} Friendship with {npc.displayName}";
                }

                Game1.addHUDMessage(new HUDMessage(translatedMsg, HUDMessage.newQuest_type));
            }

            ModEntry.Instance.Monitor.Log($"[Friendship] +{gainAmount} with {npc.Name}", LogLevel.Trace);
            
            return true;
        }

        /// <summary>เรียกตอนข้ามวันเพื่อล้างรายชื่อ NPC ที่ได้คะแนนไปแล้ว</summary>
        public static void ClearDailyNpcLimits()
        {
            NpcsAwardedToday.Clear();
        }
    }
}
