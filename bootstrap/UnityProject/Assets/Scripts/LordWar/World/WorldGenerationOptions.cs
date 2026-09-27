using System;

namespace LordWar.World {
    // The values are percentages so that the UI and the saved map use the same units.
    [Serializable] public sealed class WorldGenerationOptions {
        public int LandPercent=55, ForestPercent=50, MountainPercent=50;
        public int DesertPercent=50, RiverPercent=50, ResourcePercent=50;

        public WorldGenerationOptions CopyClamped() {
            return new WorldGenerationOptions {
                LandPercent=Mathx.Clamp(LandPercent,15,85),
                ForestPercent=Mathx.Clamp(ForestPercent,0,100),
                MountainPercent=Mathx.Clamp(MountainPercent,0,100),
                DesertPercent=Mathx.Clamp(DesertPercent,0,100),
                RiverPercent=Mathx.Clamp(RiverPercent,0,100),
                ResourcePercent=Mathx.Clamp(ResourcePercent,0,100)
            };
        }

        public static WorldGenerationOptions FromMap(WorldMap map) {
            // Maps written before this field existed contain null here.
            return map != null && map.GenerationOptions != null
                ? map.GenerationOptions.CopyClamped() : new WorldGenerationOptions();
        }
    }
}
