using System;
using StardewModdingAPI;

namespace InteractiveEmotes
{
    public static class ConsoleCommandHandler
    {
        public static void RegisterCommands(ICommandHelper commandHelper)
        {
            // Command: emote_reload
            commandHelper.Add("emote_reload", 
                "Reloads the reactions.json and combos.json files without restarting the game.\n\nUsage: emote_reload", 
                ReloadRulesCommand);

            // Command: emote_reset
            commandHelper.Add("emote_reset", 
                "Resets the daily friendship limits and combo states, allowing you to earn points again today.\n\nUsage: emote_reset", 
                ResetStateCommand);
            // Command: emote_status
            commandHelper.Add("emote_status", 
                "Shows which NPCs have received friendship points today and current combo state.\n\nUsage: emote_status", 
                StatusStateCommand);

            // Command: emote_force
            commandHelper.Add("emote_force", 
                "Forces an NPC to perform a specific emote (useful for testing animations).\n\nUsage: emote_force <npc_name> <emote_name>\nExample: emote_force Abigail anim_sick", 
                ForceEmoteCommand);

            // Command: emote_bag
            commandHelper.Add("emote_bag", 
                "Shows the current state of the Shuffle Bag (text pools).\n\nUsage: emote_bag", 
                BagStateCommand);
        }

        private static void ReloadRulesCommand(string command, string[] args)
        {
            ModEntry.Instance.Monitor.Log("Reloading reaction rules...", LogLevel.Info);
            ModEntry.Instance.LoadReactionRules();
            ModEntry.Instance.Monitor.Log("Rules reloaded successfully!", LogLevel.Info);
        }

        private static void ResetStateCommand(string command, string[] args)
        {
            EmoteReactionHandler.ClearDailyNpcLimits();
            ComboHandler.ClearAllStates();
            ModEntry.Instance.Monitor.Log("Daily friendship limits and combo states have been reset. You can now earn points from emotes again today.", LogLevel.Info);
        }

        private static void StatusStateCommand(string command, string[] args)
        {
            // Print friendship point awarded status
            string awarded = string.Join(", ", EmoteReactionHandler.NpcsAwardedToday);
            if (string.IsNullOrEmpty(awarded)) awarded = "None";
            ModEntry.Instance.Monitor.Log($"NPCs awarded friendship today: {awarded}", LogLevel.Info);

            // Print Combo status for the player running the command (or all players)
            foreach (var playerState in ComboHandler._comboStates)
            {
                StardewValley.Farmer player = StardewValley.Game1.GetPlayer(playerState.Key);
                string playerName = player != null ? player.Name : $"Unknown({playerState.Key})";
                
                ModEntry.Instance.Monitor.Log($"--- Combo State for {playerName} ---", LogLevel.Info);
                
                foreach (var npcState in playerState.Value)
                {
                    if (npcState.Value.EmoteCounts.Count > 0)
                    {
                        var counts = new System.Collections.Generic.List<string>();
                        foreach (var kvp in npcState.Value.EmoteCounts)
                        {
                            counts.Add($"{kvp.Key}: {kvp.Value}");
                        }
                        ModEntry.Instance.Monitor.Log($"  {npcState.Key} -> {string.Join(", ", counts)}", LogLevel.Info);
                    }
                }
            }
        }

        private static void ForceEmoteCommand(string command, string[] args)
        {
            if (args.Length < 2)
            {
                ModEntry.Instance.Monitor.Log("Usage: emote_force <npc_name> <emote_name>", LogLevel.Warn);
                return;
            }

            string npcName = args[0];
            string emoteName = args[1];

            StardewValley.NPC npc = StardewValley.Game1.currentLocation?.getCharacterFromName(npcName);

            if (npc == null)
            {
                ModEntry.Instance.Monitor.Log($"Cannot find NPC '{npcName}' in current location.", LogLevel.Warn);
                return;
            }

            if (emoteName.StartsWith("anim_"))
            {
                string animName = emoteName.Substring(5);
                ModEntry.Instance.Monitor.Log($"Forcing '{npcName}' to play animation '{animName}'.", LogLevel.Info);
                NpcAnimationHandler.PerformAnimation(npc, animName);
            }
            else
            {
                if (ModEntry._emoteNameToId.TryGetValue(emoteName, out int emoteId))
                {
                    ModEntry.Instance.Monitor.Log($"Forcing '{npcName}' to play emote '{emoteName}' (ID {emoteId}).", LogLevel.Info);
                    npc.doEmote(emoteId);
                }
                else
                {
                    ModEntry.Instance.Monitor.Log($"Unknown emote '{emoteName}'.", LogLevel.Warn);
                }
            }
        }

        private static void BagStateCommand(string command, string[] args)
        {
            ModEntry.Instance.Monitor.Log("--- Current Shuffle Bag State ---", LogLevel.Info);
            
            if (EmoteReactionHandler._textPools.Count == 0)
            {
                ModEntry.Instance.Monitor.Log("No pools created yet.", LogLevel.Info);
                return;
            }

            foreach (var kvp in EmoteReactionHandler._textPools)
            {
                string poolKey = kvp.Key;
                var queue = kvp.Value;
                
                EmoteReactionHandler._lastTextShown.TryGetValue(poolKey, out string? lastText);
                string lastTextStr = lastText ?? "None";

                ModEntry.Instance.Monitor.Log($"Pool: {poolKey}", LogLevel.Info);
                ModEntry.Instance.Monitor.Log($"  Remaining Items: {queue.Count}", LogLevel.Info);
                ModEntry.Instance.Monitor.Log($"  Contents: [{string.Join(", ", queue)}]", LogLevel.Info);
                ModEntry.Instance.Monitor.Log($"  Last Drawn: {lastTextStr}", LogLevel.Info);
                ModEntry.Instance.Monitor.Log("--------------------------------", LogLevel.Info);
            }
        }
    }
}
