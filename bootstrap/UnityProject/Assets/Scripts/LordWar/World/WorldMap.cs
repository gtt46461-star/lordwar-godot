using System;
using System.Collections.Generic;
namespace LordWar.World {
    [Serializable] public sealed class WorldTile {
        public int X,Y; public float Continental,Height,Moisture,Temperature,Fertility,Forest,Ore; public TerrainKind Terrain; public bool River,Lake,Road,Bridge,Ford; public int RoadLevel, RoadCapacity; public string ResourceDeposit; public int ResourceRichness; public int OwnerCityIndex=-1;
        // 可保存的地表状态：天气不是只有一个全局文字，而会在地面留下泥泞、积雪和道路损耗。
        public float SurfaceMud, SnowDepth, RoadCondition=1f; public int WeatherExposureDays;
    }
    [Serializable] public sealed class WorldMap {
        public int Width,Height,Seed; public float SeaLevel; public bool LegacyGenerator; public WorldGenerationOptions GenerationOptions; public WorldTile[] Tiles; public List<GridPoint> CitySites=new List<GridPoint>();
        public WorldMap(int w,int h,int seed){Width=w;Height=h;Seed=seed;Tiles=new WorldTile[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)Tiles[y*w+x]=new WorldTile{X=x,Y=y};}
        public WorldTile Get(int x,int y){if(x<0||y<0||x>=Width||y>=Height)return null;return Tiles[y*Width+x];}
        public IEnumerable<WorldTile> Neighbors4(int x,int y){WorldTile t;if((t=Get(x-1,y))!=null)yield return t;if((t=Get(x+1,y))!=null)yield return t;if((t=Get(x,y-1))!=null)yield return t;if((t=Get(x,y+1))!=null)yield return t;}
    }
}
