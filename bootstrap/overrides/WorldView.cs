using System;
using Godot;
using LordWar;
using LordWar.Simulation;
using LordWar.World;

namespace LordWar.GodotRuntime {
    /// <summary>N01 world canvas: saved map tiles and people, drawn with explicit existing sprite IDs.</summary>
    public sealed partial class WorldView : Node2D {
        public const float TileSize = 10f;
        const int Detail = 4;
        const string Root = "res://Art/LordWarArt/";
        const string HouseAssetId = "house_human__resources.assets__755";
        const string TreeAssetId = "tree#0__4b53b25ac82f6834d8e48815e8b61851__46";
        const string WalkerAssetId = "unit_warrior__resources.assets__504";
        GameWorld _world;
        Texture2D _terrain, _house, _tree, _walker;
        double _redrawClock;

        public void Bind(GameWorld world) {
            _world = world;
            TextureFilter = TextureFilterEnum.Nearest;
            _house = GD.Load<Texture2D>(Root + "恢复_同源替代/" + HouseAssetId + ".png");
            _tree = GD.Load<Texture2D>(Root + "植物_资源/" + TreeAssetId + ".png");
            _walker = GD.Load<Texture2D>(Root + "人物_动作/" + WalkerAssetId + ".png");
            if (_house == null || _tree == null || _walker == null)
                GD.PushError("N01_REQUIRED_ART_MISSING house=" + (_house != null) + " tree=" + (_tree != null) + " walker=" + (_walker != null));
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
        static Texture2D BuildTerrain(WorldMap map) {
            var image=Image.Create(map.Width*Detail,map.Height*Detail,false,Image.Format.Rgba8);
            for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++){
                WorldTile t=map.Get(x,y);if(t==null)continue;
                Color baseColor=Ground(t.Terrain);
                for(int py=0;py<Detail;py++)for(int px=0;px<Detail;px++){
                    int n=Noise(x*Detail+px,y*Detail+py,map.Seed);
                    float shift=((n%13)-6)*.006f;
                    Color c=baseColor.Lightened(Math.Max(0f,shift)).Darkened(Math.Max(0f,-shift));
                    if(Water(t.Terrain)){
                        if(n%19==0)c=c.Lightened(.16f);
                    }else{
                        if(t.River&&(px==1||px==2))c=Ground(TerrainKind.River);
                        else if(t.Road&&(py==1||py==2))c=new Color("a88c61");
                        else if(t.Terrain==TerrainKind.Snow&&n%13==0)c=new Color("a7bdd0");
                        else if(t.Terrain==TerrainKind.Forest&&n%9<2)c=c.Darkened(.15f);
                        else if(t.Terrain==TerrainKind.Hill&&px==py)c=c.Lightened(.10f);
                    }
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
                    DrawTextureRect(_house,new Rect2(foot.X-7,foot.Y-13,14,13),false);
                    // A city's inhabited block is rendered from its real population, never a circle marker.
                    if(city.PersonIds.Count>24){
                        DrawTextureRect(_house,new Rect2(foot.X-17,foot.Y-8,11,10),false);
                        DrawTextureRect(_house,new Rect2(foot.X+6,foot.Y-7,11,10),false);
                    }
                }
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
