using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace InteractiveEmotes
{
    // ============================================================
    // NpcAnimationHandler.cs — Handles special NPC animations
    //
    // Used when Emote in Action starts with "anim_"
    // e.g. "anim_game", "anim_laugh", "anim_sick"
    //
    // Differs from normal doEmote() as it can play multiple frames
    // continuously and restores the original animation state when done.
    // ============================================================

    public static class NpcAnimationHandler
    {
        /// <summary>Plays a special animation based on the provided name.
        /// animName is the string after removing "anim_" prefix (e.g. "game", "laugh").</summary>
        public static void PerformAnimation(NPC npc, string animName)
        {
            ModEntry.Instance.Monitor.Log($"[NpcAnimation] {npc.Name} performing animation: {animName}", LogLevel.Trace);

            // Pause movement temporarily for 600ms to let animation play
            // The game will automatically restore the schedule after the timeout
            npc.Halt();
            npc.movementPause = 600;

            switch (animName.ToLower())
            {
                case "sick":
                    // NPC shakes slightly, then shows sad emote
                    npc.shake(500);
                    npc.doEmote(28); // sad emote
                    break;

                case "laugh":
                    // Alternate frames 4 and 5 to simulate laughing
                    // Sequence: frame4 → frame5 → frame4 → frame5 → frame0 (normal)
                    PlayTemporaryAnimation(npc, new List<int> { 4, 5, 4, 5, 4, 0 }, 200);
                    break;

                case "game":
                    // NPC shakes slightly + coin sound + video game emote
                    npc.shake(200);
                    Game1.playSound("coin");
                    npc.doEmote(52); // video game emote
                    break;

                case "exclamation":
                    // NPC shakes, then shows exclamation emote
                    npc.shake(300);
                    npc.doEmote(16); // exclamation emote
                    break;

                default:
                    ModEntry.Instance.Monitor.Log($"[NpcAnimation] Unknown animation name: '{animName}'", LogLevel.Warn);
                    break;
            }
        }

        /// <summary>Plays a temporary animation from a list of frame indices.
        /// Will restore the original animation state after finishing.</summary>
        /// <param name="npc">NPC performing the animation</param>
        /// <param name="frameIndices">List of frame indices to play in order</param>
        /// <param name="intervalMs">Duration of each frame in milliseconds</param>
        private static void PlayTemporaryAnimation(NPC npc, List<int> frameIndices, int intervalMs)
        {
            // Save original animation state to restore later
            var originalAnimation = npc.Sprite.CurrentAnimation;
            int originalFrame = npc.Sprite.currentFrame;

            // Create new animation from the provided frame indices
            var newFrames = new List<FarmerSprite.AnimationFrame>();
            foreach (int frame in frameIndices)
            {
                newFrames.Add(new FarmerSprite.AnimationFrame(frame, intervalMs));
            }

            // Calculate total duration of the animation
            int totalDuration = frameIndices.Count * intervalMs;

            // Start playing the animation immediately
            npc.Sprite.setCurrentAnimation(newFrames);

            // After completion: restore the NPC's original state
            DelayedAction.functionAfterDelay(() =>
            {
                // Ensure NPC is still in the location before modifying
                if (npc != null && npc.currentLocation != null)
                {
                    npc.Sprite.CurrentAnimation = originalAnimation;
                    npc.Sprite.currentFrame = originalFrame;
                    npc.Sprite.StopAnimation();
                }
            }, totalDuration);
        }
    }
}
