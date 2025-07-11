using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;
using System.Linq;

namespace InteractiveEmotes
{
    /// <summary>Handles the execution of complex, multi-frame body animations for NPCs.</summary>
    public class NpcAnimationHandler
    {
        private readonly IMonitor _monitor;

        public NpcAnimationHandler(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Performs a predefined special animation on an NPC.</summary>
        /// <param name="npc">The target NPC.</param>
        /// <param name="animationName">The name of the animation to perform (e.g., "laugh").</param>
        public void PerformAnimation(NPC npc, string animationName)
        {
            _monitor.Log($"Attempting to perform special animation '{animationName}' for {npc.Name}.", LogLevel.Debug);

            npc.Halt();
            npc.movementPause = 600;

            switch (animationName.ToLower())
            {
                case "sick":
                    npc.shake(500);
                    npc.doEmote(28); // Sad emote
                    break;

                case "laugh":
                    // A laughing animation that cycles between frames 4 and 5.
                    PlayTemporaryAnimation(npc, new List<int> { 4, 5, 4, 5, 4, 0 }, 200);
                    break;

                case "game":
                    npc.shake(200);
                    Game1.playSound("coin");
                    npc.doEmote(52); // Video game emote
                    break;

                case "exclamation":
                    npc.shake(300);
                    npc.doEmote(16); // Exclamation emote
                    break;

                default:
                    _monitor.Log($"Unknown animation name: {animationName}", LogLevel.Warn);
                    break;
            }
        }

        /// <summary>A helper method to play a temporary sequence of animation frames on an NPC, then restore their original state.</summary>
        /// <param name="npc">The target NPC.</param>
        /// <param name="frameIndices">A list of sprite frame indices to play in sequence.</param>
        /// <param name="interval">The duration in milliseconds for each frame.</param>
        /// <param name="holdLastFrame">Whether to hold the last frame of the animation instead of reverting.</param>
        private void PlayTemporaryAnimation(NPC npc, List<int> frameIndices, int interval, bool holdLastFrame = false)
        {
            // 1. Store the NPC's original animation state.
            var originalAnimation = npc.Sprite.CurrentAnimation;
            int originalFrame = npc.Sprite.currentFrame;

            // 2. Create a new animation sequence from the provided frame indices.
            var newAnimationFrames = new List<FarmerSprite.AnimationFrame>();
            foreach (int frame in frameIndices)
            {
                newAnimationFrames.Add(new FarmerSprite.AnimationFrame(frame, interval));
            }

            int totalDuration = newAnimationFrames.Sum(f => f.milliseconds);

            // 3. Play the new animation immediately.
            npc.Sprite.setCurrentAnimation(newAnimationFrames);

            // 4. Schedule an action to restore the original animation after the new one completes.
            DelayedAction.functionAfterDelay(() =>
            {
                // Ensure the NPC still exists in the location before modifying them.
                if (npc != null && npc.currentLocation != null)
                {
                    if (!holdLastFrame)
                    {
                        npc.Sprite.CurrentAnimation = originalAnimation;
                        npc.Sprite.currentFrame = originalFrame;
                    }
                    npc.Sprite.StopAnimation();
                }
            }, totalDuration);
        }
    }
}