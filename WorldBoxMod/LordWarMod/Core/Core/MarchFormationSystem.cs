using System;
namespace LordWar {
    public static class MarchFormationSystem {
        public static float TerrainSpeed(TerrainKind t){switch(t){case TerrainKind.Road:return 1.25f;case TerrainKind.Grass:return 1f;case TerrainKind.Forest:return .70f;case TerrainKind.Hill:return .75f;case TerrainKind.MountainPass:return .58f;case TerrainKind.Mountain:return .40f;case TerrainKind.Snow:return .62f;case TerrainKind.Desert:return .72f;case TerrainKind.Marsh:return .48f;case TerrainKind.River:return .42f;case TerrainKind.DeepWater:case TerrainKind.Lake:return 0f;default:return .8f;}}
        public static float ColumnSpacing(float baseSpacing,float fatigue,TerrainKind terrain){float tf=terrain==TerrainKind.Road ? .85f:1f;return Math.Max(.45f,baseSpacing*tf*(1f+fatigue*.003f));}
        public static GridPoint FormationSlot(GridPoint anchor,int squadIndex,int soldierIndex,bool marching){int width=marching?2:6;int row=soldierIndex/width,col=soldierIndex%width;int sx=col-width/2+(squadIndex%3)*3;int sy=-(row+1)-(squadIndex/3)*5;return new GridPoint(anchor.X+sx,anchor.Y+sy);}
        public static float FatiguePerTile(TerrainKind t,float weatherPenalty,bool forcedMarch){float baseF=t==TerrainKind.Road ? .15f:(t==TerrainKind.Marsh||t==TerrainKind.Mountain ? .55f:.28f);return baseF*(1f+weatherPenalty)*(forcedMarch?1.65f:1f);}
        public static float WeatherTerrainPenalty(WeatherKind weather,TerrainKind terrain,bool road,int roadLevel){
            float extra=0f;
            if(weather==WeatherKind.Rain){if(terrain==TerrainKind.Marsh)extra=.16f;else if(terrain==TerrainKind.River)extra=.10f;else if(terrain==TerrainKind.Forest)extra=.05f;if(road&&roadLevel<=1)extra+=.08f;}
            else if(weather==WeatherKind.Storm){if(terrain==TerrainKind.Marsh||terrain==TerrainKind.River)extra=.20f;else if(terrain==TerrainKind.Mountain||terrain==TerrainKind.MountainPass)extra=.12f;else extra=.06f;if(road&&roadLevel<=1)extra+=.10f;}
            else if(weather==WeatherKind.Snow){if(terrain==TerrainKind.Snow||terrain==TerrainKind.Mountain||terrain==TerrainKind.MountainPass)extra=.12f;if(road&&roadLevel<=1)extra+=.07f;}
            else if(weather==WeatherKind.Blizzard){if(terrain==TerrainKind.Snow||terrain==TerrainKind.Mountain||terrain==TerrainKind.MountainPass)extra=.22f;else extra=.08f;if(road&&roadLevel<=1)extra+=.09f;}
            else if(weather==WeatherKind.Heat){if(terrain==TerrainKind.Desert)extra=.16f;else if(terrain==TerrainKind.Grass)extra=.05f;}
            else if(weather==WeatherKind.Cold&&(terrain==TerrainKind.River||terrain==TerrainKind.Marsh))extra=.04f;
            if(road&&roadLevel>=2)extra*=roadLevel>=3 ? .32f:.58f;
            return Mathx.Clamp(extra,0f,.38f);
        }
        public static float FoodUsePerTile(TerrainKind terrain,float weatherPenalty,bool forcedMarch,float congestion,float terrainAdapt){
            bool difficult=terrain==TerrainKind.Forest||terrain==TerrainKind.Hill||terrain==TerrainKind.Mountain||terrain==TerrainKind.MountainPass||terrain==TerrainKind.Marsh||terrain==TerrainKind.River||terrain==TerrainKind.Snow||terrain==TerrainKind.Desert;
            float use=.015f*Math.Max(1f,congestion);
            if(difficult)use*=1.28f/Mathx.Clamp(terrainAdapt,.85f,1.45f);
            use*=1f+Mathx.Clamp(weatherPenalty,0f,.9f)*.65f;
            if(forcedMarch)use*=1.18f;
            return use;
        }
    }
}
