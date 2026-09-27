using System;
using Godot;
using LordWar;
using LordWar.Simulation;
using LordWar.World;

namespace LordWar.GodotRuntime {
    /// <summary>N01 world canvas: saved map tiles and people, drawn with explicit existing sprite IDs.</summary>
    public sealed partial class WorldView : Node2D {
        public const float TileSize = 10f;
        const int Detail = 8;
        const string Root = "res://Art/LordWarArt/";
        const string HouseAssetId = "小住宅";
        const string VillaAssetId = "别墅";
        const string TreeAssetId = "tree#0__4b53b25ac82f6834d8e48815e8b61851__46";
        const string WalkerAssetId = "unit_warrior__resources.assets__504";
        GameWorld _world;
        Texture2D _terrain, _house, _villa, _tree, _walker;
        double _redrawClock;

        public void Bind(GameWorld world) {
            _world = world;
            TextureFilter = TextureFilterEnum.Nearest;
            _house = GD.Load<Texture2D>(Root + "V31新增/城市建筑扩展/" + HouseAssetId + ".png");
            _villa = GD.Load<Texture2D>(Root + "V31新增/城市建筑扩展/" + VillaAssetId + ".png");
            _tree = GD.Load<Texture2D>(Root + "植物_资源/" + TreeAssetId + ".png");
            _walker = GD.Load<Texture2D>(Root + "人物_动作/" + WalkerAssetId + ".png");
            if (_house == null || _villa == null || _tree == null || _walker == null)
                GD.PushError("N01_REQUIRED_ART_MISSING house=" + (_house != null) + " villa=" + (_villa != null) + " tree=" + (_tree != null) + " walker=" + (_walker != null));
            _terrain = world?.Map == null ? null : BuildTerrain(world.Map);
            QueueRedraw();
        }

        public override void _Process(double delta) {
            if (_world == null) return;
            _redrawClock += delta;
            if (_redrawClock >= .12) { _redrawClock = 0; QueueRedraw(); }
        }

        static int Noise(int x,int y,int salt) { unchecked { uint n=(uint)(x*374761393+y*668265263+salt*1442695041);n=(n^(n>>13))*1274126177u;return (int)((n^(n>>16))&0x7fffffff); } }
        static Color Ground(TerrainKind t) {
            switch(t){
                case TerrainKind.DeepWater:return new Color("27476b");
                case TerrainKind.Lake:return new Color("356b87");
                case TerrainKind.Coast:return new Color("c9bb7d");
                case TerrainKind.Forest:return new Color("47734c");
                case TerrainKind.Hill:return new Color("798358");
                case TerrainKind.Mountain:return new Color("83858a");
                case TerrainKind.MountainPass:return new Color("9a9076");
                case TerrainKind.Snow:return new Color("d6d9c4");
                case TerrainKind.Desert:return new Color("bc9b64");
                case TerrainKind.Marsh:return new Color("587665");
                case TerrainKind.River:return new Color("3f85a1");
                default:return new Color("80a36a");
            }
        }
        static bool Water(TerrainKind t){return t==TerrainKind.DeepWater||t==TerrainKind.Lake||t==TerrainKind.River;}
        static Image LoadTerrainArt(string assetId) {
            Texture2D texture=GD.Load<Texture2D>(Root+"地图_地形/"+assetId+".png");
            if(texture==null)throw new InvalidOperationException("N01 terrain art missing: "+assetId);
            Image pixels=texture.GetImage();
            if(pixels==null||pixels.GetWidth()!=28||pixels.GetHeight()!=28)
                throw new InvalidOperationException("N01 terrain art dimensions invalid: "+assetId);
            return pixels;
        }
        static string TerrainAsset(TerrainKind terrain) {
            switch(terrain){
                case TerrainKind.DeepWater:return "iconTileDeepOcean__17c9c48fa3ef13e408331d51f9c7f071__1";
                case TerrainKind.Lake:
                case TerrainKind.River:return "iconTileCloseOcean__644572b67e771194493c4a640a9faa1a__1";
                case TerrainKind.Coast:
                case TerrainKind.Desert:return "iconTileSand__37c8aad1e967dd946ae7a7b69d3e6d01__1";
                case TerrainKind.Forest:return "iconTileForest__57e0886d0ce48d34399e37d04c32d888__1";
                case TerrainKind.Hill:return "iconTileHills__716312abde6b3cb4fba15f03da0c9345__1";
                case TerrainKind.Mountain:
                case TerrainKind.MountainPass:return "iconTileMountains__ff1cce5cd6cce9545b921d8f49779b71__1";
                case TerrainKind.Snow:return "iconTileHighSoil__6e1abd836d40cf84d98d419bb0c29c01__1";
                case TerrainKind.Marsh:return "iconTileSwamp__46987c74113bca648a7b006c6ca60838__1";
                default:return "iconTileSoilGreen__adfec68add4eb0c46954bde705c3269b__1";
            }
        }
        static Texture2D BuildTerrain(WorldMap map) {
            var samples=new System.Collections.Generic.Dictionary<string,Image>();
            foreach(TerrainKind terrain in Enum.GetValues<TerrainKind>()){
                string asset=TerrainAsset(terrain);
                if(!samples.ContainsKey(asset))samples[asset]=LoadTerrainArt(asset);
            }
            GD.Print("LORDWAR_N01_TERRAIN_ASSETS loaded="+samples.Count);
            var image=Image.Create(map.Width*Detail,map.Height*Detail,false,Image.Format.Rgba8);
            for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++){
                WorldTile t=map.Get(x,y);if(t==null)continue;
                Color baseColor=Ground(t.Terrain);
                Image sample=samples[TerrainAsset(t.Terrain)];
                int variant=Noise(x,y,map.Seed)%Detail;
                for(int py=0;py<Detail;py++)for(int px=0;px<Detail;px++){
                    Color art=sample.GetPixel(10+(px+variant)%Detail,10+(py+variant)%Detail);
                    Color c=baseColor.Lerp(art,.55f*art.A);
                    if(!Water(t.Terrain)&&t.River&&(px==3||px==4))c=c.Lerp(Ground(TerrainKind.River),.75f);
                    else if(!Water(t.Terrain)&&t.Road&&(py==3||py==4))c=c.Lerp(new Color("a88c61"),.65f);
                    image.SetPixel(x*Detail+px,y*Detail+py,c);
                }
            }
            return ImageTexture.CreateFromImage(image);
        }
        public override void _Draw() {
            if (_world?.Map == null || _terrain == null) return;
            WorldMap map=_world.Map;
            DrawTextureRect(_terrain,new Rect2(0,0,map.Width*TileSize,map.Height*TileSize),false);
            // Static foliage is a deterministic decoration of actual forest tiles.
            if(_tree!=null)for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++){
                WorldTile t=map.Get(x,y);
                if(t!=null&&t.Terrain==TerrainKind.Forest&&Noise(x,y,map.Seed)%7==0){
                    Vector2 foot=WorldToCanvas(x,y);
                    DrawTextureRect(_tree,new Rect2(foot.X-4,foot.Y-8,8,9),false);
                }
            }
            foreach(City city in _world.Cities.Values){
                Vector2 foot=WorldToCanvas(city.X,city.Y);
                if(_house!=null){
                    DrawTextureRectRegion(_house,new Rect2(foot.X-9,foot.Y-18,18,18),new Rect2(7,8,18,18));
                    // A city's inhabited block is rendered from its real population, never a circle marker.
                    if(city.PersonIds.Count>24){
                        if(_villa!=null)DrawTextureRectRegion(_villa,new Rect2(foot.X-28,foot.Y-16,22,18),new Rect2(5,8,22,18));
                        DrawTextureRectRegion(_house,new Rect2(foot.X+8,foot.Y-16,18,18),new Rect2(7,8,18,18));
                    }
                }
                // A small town garden uses the tree sheet at the city's actual position.
                if(_tree!=null)DrawTextureRect(_tree,new Rect2(foot.X+10,foot.Y-11,8,9),false);
            }
            if(_walker!=null){
                foreach(Person p in _world.People.Values){
                    if(!p.IsWorldWalker||!p.Alive||p.WalkRoute==null||p.WalkRoute.Count<2)continue;
                    Vector2 foot=WorldToCanvas(p.X,p.Y);
                    int next=p.WalkRouteIndex+(p.WalkForward?1:-1);
                    if(next>=0&&next<p.WalkRoute.Count){GridPoint target=p.WalkRoute[next];foot=foot.Lerp(WorldToCanvas(target.X,target.Y),Mathf.Clamp(p.WalkProgress,0f,1f));}
                    int frame=(int)((_world.Clock.TickIndex/3)%4);
                    DrawTextureRectRegion(_walker,new Rect2(foot.X-5,foot.Y-18,10,22),new Rect2(frame*10,0,10,22));
                }
                foreach(Army army in _world.Armies.Values){
                    Vector2 foot=WorldToCanvas(army.X,army.Y);int frame=(int)((_world.Clock.TickIndex/4)%4);
                    DrawTextureRectRegion(_walker,new Rect2(foot.X-5,foot.Y-20,10,22),new Rect2(frame*10,0,10,22));
                }
            }
        }
        public static Vector2 WorldToCanvas(float x,float y){return new Vector2((x+.5f)*TileSize,(y+.5f)*TileSize);}
    }
}
