using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace InteractiveEmotes
{
    public class ConditionGetter
    {
        /// <summary>Returns a list of NPCs within the specified maxDistance from the player.
        /// Returns List<NPC> directly to avoid redundant lookups in EmoteReactionHandler.</summary>
        public static List<NPC> GetNearbyNpcs(float maxDistance, Farmer player)
        {
            // Always check currentLocation first, as it might be null during transitions
            if (Game1.currentLocation == null)
            {
                return new List<NPC>();
            }

            var nearbyNpcs = new List<NPC>();

            // Calculate maxDistanceSq once outside the loop
            // Use DistanceSquared instead of Distance to avoid square root calculations (saves CPU)
            float maxDistanceSq = maxDistance * maxDistance;

            foreach (NPC npc in Game1.currentLocation.characters)
            {
                if (Vector2.DistanceSquared(player.Tile, npc.Tile) <= maxDistanceSq)
                {
                    nearbyNpcs.Add(npc);
                }
            }

            return nearbyNpcs;
        }
    }
}
