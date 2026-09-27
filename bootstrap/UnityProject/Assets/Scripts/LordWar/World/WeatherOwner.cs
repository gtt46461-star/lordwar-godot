using System;
namespace LordWar.World {
    public sealed class WeatherOwner {
        readonly DeterministicRandom _rng; public SeasonKind Season=SeasonKind.Spring; public WeatherKind Weather=WeatherKind.Clear; public int Day;
        public WeatherOwner(int seed){_rng=new DeterministicRandom(seed^0x7733);}
        public void TickDay(){Day++;int d=Day%120;Season=d<30?SeasonKind.Spring:(d<60?SeasonKind.Summer:(d<90?SeasonKind.Autumn:SeasonKind.Winter));float r=_rng.Next01();if(Season==SeasonKind.Winter)Weather=r<.18f?WeatherKind.Blizzard:(r<.48f?WeatherKind.Snow:(r<.58f?WeatherKind.Fog:WeatherKind.Cold));else if(Season==SeasonKind.Summer)Weather=r<.12f?WeatherKind.Storm:(r<.27f?WeatherKind.Rain:(r>.9f?WeatherKind.Heat:WeatherKind.Clear));else Weather=r<.08f?WeatherKind.Storm:(r<.32f?WeatherKind.Rain:(r<.42f?WeatherKind.Fog:WeatherKind.Clear));}
        public void TickDay(WorldMap map){TickDay();ApplySurfaceWeather(map);}
        public void ApplySurfaceWeather(WorldMap map){
            if(map==null||map.Tiles==null)return;
            foreach(WorldTile t in map.Tiles){
                if(t==null)continue;t.WeatherExposureDays++;
                float wet=0f,snow=0f,dry=.025f,melt=.018f,roadWear=0f;
                switch(Weather){
                    case WeatherKind.Rain:wet=.14f;dry=0f;melt=.035f;roadWear=.006f;break;
                    case WeatherKind.Storm:wet=.24f;dry=0f;melt=.05f;roadWear=.014f;break;
                    case WeatherKind.Snow:snow=.13f;dry=0f;melt=0f;roadWear=.005f;break;
                    case WeatherKind.Blizzard:snow=.25f;dry=0f;melt=0f;roadWear=.013f;break;
                    case WeatherKind.Heat:dry=.16f;melt=.18f;break;
                    case WeatherKind.Clear:dry=Season==SeasonKind.Summer ? .09f:.05f;melt=Season==SeasonKind.Winter ? .01f:.06f;break;
                    case WeatherKind.Cold:dry=.025f;melt=0f;break;
                    case WeatherKind.Fog:wet=.025f;dry=.01f;melt=.015f;break;
                }
                float wetness=t.Terrain==TerrainKind.Marsh?1.45f:(t.Terrain==TerrainKind.Forest?1.16f:(t.River||t.Lake?1.25f:1f));
                t.SurfaceMud=Mathx.Clamp(t.SurfaceMud+wet*wetness-dry,0f,1f);
                t.SnowDepth=Mathx.Clamp(t.SnowDepth+snow*(t.Temperature<.35f?1.25f:1f)-melt*(t.Temperature>.18f?1f:.45f),0f,1f);
                if(t.Road){float gradeProtection=t.RoadLevel>=3 ? .38f:(t.RoadLevel==2 ? .65f:1f);float terrainWear=t.Terrain==TerrainKind.Marsh?1.3f:(t.River&&!t.Bridge?1.2f:1f);t.RoadCondition=Mathx.Clamp((t.RoadCondition<=0f?1f:t.RoadCondition)-roadWear*gradeProtection*terrainWear,.25f,1f);}
                else t.RoadCondition=0f;
            }
        }
        public float MarchPenalty(){switch(Weather){case WeatherKind.Storm:return .35f;case WeatherKind.Blizzard:return .55f;case WeatherKind.Snow:return .25f;case WeatherKind.Rain:return .18f;case WeatherKind.Fog:return .12f;case WeatherKind.Heat:return .15f;case WeatherKind.Cold:return .1f;default:return 0;}}
        public float RangedAccuracyMultiplier(){return Weather==WeatherKind.Storm ? .68f:(Weather==WeatherKind.Blizzard ? .55f:(Weather==WeatherKind.Fog ? .62f:(Weather==WeatherKind.Rain ? .82f:1f)));}
        public float VisibilityMultiplier(){switch(Weather){case WeatherKind.Fog:return .48f;case WeatherKind.Storm:return .66f;case WeatherKind.Blizzard:return .42f;case WeatherKind.Snow:return .78f;case WeatherKind.Rain:return .84f;default:return 1f;}}
        public float AgricultureMultiplier(){float s=Season==SeasonKind.Spring?1.08f:(Season==SeasonKind.Summer?1f:(Season==SeasonKind.Autumn?1.16f:.55f));switch(Weather){case WeatherKind.Rain:s*=1.06f;break;case WeatherKind.Storm:s*=.82f;break;case WeatherKind.Blizzard:s*=.45f;break;case WeatherKind.Snow:s*=.72f;break;case WeatherKind.Heat:s*=.76f;break;case WeatherKind.Cold:s*=.82f;break;}return Mathx.Clamp(s,.25f,1.3f);}
    }
}
