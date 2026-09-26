using System;
using System.Collections.Generic;
namespace LordWar.World {
    [Serializable] public sealed class WorldTile {
        public int X,Y; public float Continental,Height,Moisture,Temperature,Fertility,Forest,Ore; public TerrainKind Terrain; public bool River,Lake,Road,Bridge,Ford; public int RoadLevel, RoadCapacity; public string ResourceDeposit; public int ResourceRichness; public int OwnerCityIndex=-1;
        // 可保存的地表状态：天气不是只有一个全局文字，而会在地面留下泥泞、积雪和道路损耗。
        public float SurfaceMud, SnowDepth, RoadCondition=1f; public int WeatherExposureDays;
    }
    [Serializable] public sealed class WorldMap {
        public int Width,Height,Seed; public WorldTile[] Tiles; public List<GridPoint> CitySites=new List<GridPoint>();
        public WorldMap(int w,int h,int seed){Width=w;Height=h;Seed=seed;Tiles=new WorldTile[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)Tiles[y*w+x]=new WorldTile{X=x,Y=y};}
        public WorldTile Get(int x,int y){if(x<0||y<0||x>=Width||y>=Height)return null;return Tiles[y*Width+x];}
        public IEnumerable<WorldTile> Neighbors4(int x,int y){WorldTile t;if((t=Get(x-1,y))!=null)yield return t;if((t=Get(x+1,y))!=null)yield return t;if((t=Get(x,y-1))!=null)yield return t;if((t=Get(x,y+1))!=null)yield return t;}
        // 与陆军寻路使用同一套通行边界。逐次搜索可兼容读档和地形编辑，无需保存容易失效的岛屿缓存。
        public bool CanMarchBetween(int startX,int startY,int goalX,int goalY){
            WorldTile start=Get(startX,startY),goal=Get(goalX,goalY);
            if(!Passable(start)||!Passable(goal))return false;
            int origin=startY*Width+startX,target=goalY*Width+goalX;
            if(origin==target)return true;
            bool[] visited=new bool[Tiles.Length];int[] queue=new int[Tiles.Length];
            int head=0,tail=0;visited[origin]=true;queue[tail++]=origin;
            while(head<tail){
                int current=queue[head++],x=current%Width,y=current/Width;
                if(x>0&&Enqueue(current-1,target,visited,queue,ref tail))return true;
                if(x+1<Width&&Enqueue(current+1,target,visited,queue,ref tail))return true;
                if(y>0&&Enqueue(current-Width,target,visited,queue,ref tail))return true;
                if(y+1<Height&&Enqueue(current+Width,target,visited,queue,ref tail))return true;
            }
            return false;
        }
        static bool Passable(WorldTile tile){return tile!=null&&tile.Terrain!=TerrainKind.DeepWater&&tile.Terrain!=TerrainKind.Lake;}
        bool Enqueue(int index,int target,bool[] visited,int[] queue,ref int tail){
            if(visited[index]||!Passable(Tiles[index]))return false;
            if(index==target)return true;
            visited[index]=true;queue[tail++]=index;return false;
        }
    }
}
