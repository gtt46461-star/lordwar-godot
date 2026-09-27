#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;
using LordWar.World;

namespace LordWar.UnityRuntime {
    /// <summary>Deterministic visual projection of WorldMap. It never changes simulation state.</summary>
    public static class PixelTerrainPainter {
        public const int PixelsPerTile=4;
        static readonly Color32 Deep=new Color32(22,57,90,255), Shallow=new Color32(52,104,138,255);
        static readonly Color32 Sand=new Color32(178,164,109,255), Grass=new Color32(113,151,90,255);
        static readonly Color32 Forest=new Color32(75,116,67,255), Hill=new Color32(126,128,91,255);
        static readonly Color32 Rock=new Color32(114,113,106,255), Snow=new Color32(185,196,195,255);
        static readonly Color32 Marsh=new Color32(93,119,91,255), Desert=new Color32(191,166,107,255);
        static readonly Color32 Road=new Color32(143,112,76,255), River=new Color32(57,113,149,255);

        public static void Paint(WorldMap map,Color32[] result) {
            if(map==null||map.Tiles==null)throw new ArgumentNullException("map");
            int scale=PixelsPerTile,width=map.Width*scale,height=map.Height*scale;
            if(result==null||result.Length!=width*height)throw new ArgumentException("地表像素缓冲大小不匹配","result");
            for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++){
                WorldTile t=map.Get(x,y);
                WorldTile west=map.Get(x-1,y),east=map.Get(x+1,y);
                WorldTile south=map.Get(x,y-1),north=map.Get(x,y+1);
                bool water=IsWater(t), left=IsWater(west),right=IsWater(east);
                bool down=IsWater(south),up=IsWater(north);
                bool roadL=HasRoad(west),roadR=HasRoad(east);
                bool roadD=HasRoad(south),roadU=HasRoad(north);
                bool riverL=HasRiver(west),riverR=HasRiver(east);
                bool riverD=HasRiver(south),riverU=HasRiver(north);
                Color32 baseColor=Base(t);
                for(int py=0;py<scale;py++)for(int px=0;px<scale;px++){
                    uint h=Hash(x*scale+px,y*scale+py,map.Seed);
                    int variation=(int)(h%19)-9;
                    Color32 c=Vary(baseColor,variation);
                    bool edge=(left&&px==0)||(right&&px==scale-1)||(down&&py==0)||(up&&py==scale-1);
                    bool nearLand=(west!=null&&!left&&px==0)||
                                  (east!=null&&!right&&px==scale-1)||
                                  (south!=null&&!down&&py==0)||
                                  (north!=null&&!up&&py==scale-1);
                    if(water){
                        if(nearLand)c=Vary(Shallow,variation);
                        if((h&31)==0)c=Vary(c,14);
                    }else{
                        if(edge&&!t.River)c=Vary(Sand,variation);
                        if(t.Terrain==TerrainKind.Forest&&(h&7)<3)c=Vary(Forest,-18);
                        if((t.Terrain==TerrainKind.Mountain||t.Terrain==TerrainKind.MountainPass)&&
                           ((px+py+(int)(h&3))%3==0))c=Vary(Rock,23);
                        if(t.Terrain==TerrainKind.Desert&&(h&7)==0)c=Vary(Sand,20);
                        if(t.Terrain==TerrainKind.Grass&&(h&15)==0)c=Vary(Grass,19);
                        bool centerX=px==1||px==2,centerY=py==1||py==2;
                        bool riverPixel=t.River&&((centerX&&(riverU||riverD||(!riverL&&!riverR)))||
                            (centerY&&(riverL||riverR)));
                        if(riverPixel)c=Vary(River,variation);
                        if(t.Road){
                            bool roadPixel=(centerX&&(roadU||roadD||(!roadL&&!roadR)))||
                                           (centerY&&(roadL||roadR));
                            if(roadPixel&&((h&7)<2||t.RoadCondition>=.45f))
                                c=Mix(c,Vary(Road,variation/2),Mathf.Clamp01(.45f+t.RoadCondition*.55f));
                        }
                    }
                    if(t.SurfaceMud>.03f)c=Mix(c,new Color32(99,82,62,255),Mathf.Clamp01(t.SurfaceMud*.45f));
                    if(t.SnowDepth>.03f)c=Mix(c,Snow,Mathf.Clamp01(t.SnowDepth*.78f));
                    result[(y*scale+py)*width+x*scale+px]=c;
                }
            }
        }

        static Color32 Base(WorldTile t) {
            if(t==null)return Deep;
            switch(t.Terrain){
                case TerrainKind.DeepWater:return Deep;
                case TerrainKind.Lake:case TerrainKind.Coast:return Shallow;
                case TerrainKind.Forest:return Forest;
                case TerrainKind.Hill:return Hill;
                case TerrainKind.Mountain:case TerrainKind.MountainPass:return Rock;
                case TerrainKind.Snow:return Snow;
                case TerrainKind.Marsh:return Marsh;
                case TerrainKind.Desert:return Desert;
                case TerrainKind.River:return Grass;
                default:return Grass;
            }
        }
        static bool IsWater(WorldTile t){return t!=null&&(t.Lake||t.Terrain==TerrainKind.Lake||t.Terrain==TerrainKind.DeepWater||t.Terrain==TerrainKind.Coast);}
        static bool HasRiver(WorldTile t){return t!=null&&t.River;}
        static bool HasRoad(WorldTile t){return t!=null&&t.Road;}
        static Color32 Vary(Color32 c,int d){return new Color32((byte)Mathf.Clamp(c.r+d,0,255),(byte)Mathf.Clamp(c.g+d,0,255),(byte)Mathf.Clamp(c.b+d,0,255),255);}
        static Color32 Mix(Color32 a,Color32 b,float q){return new Color32((byte)Mathf.RoundToInt(a.r+(b.r-a.r)*q),(byte)Mathf.RoundToInt(a.g+(b.g-a.g)*q),(byte)Mathf.RoundToInt(a.b+(b.b-a.b)*q),255);}
        static uint Hash(int x,int y,int seed){unchecked{uint n=(uint)(x*374761393+y*668265263+seed*69069);n=(n^(n>>13))*1274126177u;return n^(n>>16);}}
    }
}
#endif
