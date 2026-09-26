using System;
using System.Collections.Generic;
using Godot;
using LordWar;
using LordWar.Simulation;

namespace LordWar.GodotRuntime {
    /// <summary>Sprite-backed map using the included game's terrain, building, tree and unit art.</summary>
    public sealed partial class WorldView : Node2D {
        public const float TileSize = 10f;
        GameWorld _world;
        GodotSpriteAssetLibrary _art;
        ImageTexture _ground;
        Texture2D _tree;
        Texture2D _mountain;
        Texture2D _road;
        Texture2D _hall;
        Texture2D _house;
        Texture2D _warrior;
        readonly List<Vector2> _forestPositions = new List<Vector2>();
        readonly List<Vector2> _mountainPositions = new List<Vector2>();
        readonly List<Vector2> _roadPositions = new List<Vector2>();
        double _redrawClock;

        public void Bind(GameWorld world) { Bind(world, _art); }

        public void Bind(GameWorld world, GodotSpriteAssetLibrary art) {
            _world = world;
            _art = art;
            LoadShellArt();
            BuildGroundTexture();
            QueueRedraw();
        }

        void LoadShellArt() {
            if (_art == null) return;
            _tree = _art.FindVariant(17, "tree#");
            _mountain = _art.FindVariant(29, "mountains_0");
            _road = _art.FindVariant(31, "road_0");
            _hall = _art.FindVariant(41, "hall_human#0");
            _house = _art.FindVariant(43, "house_human#0");
            _warrior = _art.FindVariant(47, "unit_warrior_");
        }

        void BuildGroundTexture() {
            _ground = null;
            _forestPositions.Clear();
            _mountainPositions.Clear();
            _roadPositions.Clear();
            if (_world == null || _world.Map == null) return;

            World.WorldMap map = _world.Map;
            Image image = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.Rgba8);
            for (int y = 0; y < map.Height; y++) {
                for (int x = 0; x < map.Width; x++) {
                    World.WorldTile tile = map.Get(x, y);
                    if (tile == null) continue;
                    Color baseColor = tile.River ? new Color(.14f, .39f, .61f)
                        : tile.Road ? new Color(.47f, .37f, .24f)
                        : TerrainColor(tile.Terrain);
                    image.SetPixel(x, y, baseColor);
                    if (tile.Road) _roadPositions.Add(new Vector2(x * TileSize, y * TileSize));

                    // Deterministic sparse decoration keeps the map legible while reusing the original pixel-art assets.
                    int decoration = Math.Abs((x * 73856093) ^ (y * 19349663) ^ _world.Seed);
                    if (tile.Terrain == TerrainKind.Forest && decoration % 7 == 0)
                        _forestPositions.Add(WorldToCanvas(x, y));
                    if ((tile.Terrain == TerrainKind.Mountain || tile.Terrain == TerrainKind.MountainPass) && decoration % 5 == 0)
                        _mountainPositions.Add(WorldToCanvas(x, y));
                }
            }
            _ground = ImageTexture.CreateFromImage(image);
        }

        public override void _Ready() {
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        }

        public override void _Process(double delta) {
            if (_world == null) return;
            _redrawClock += delta;
            if (_redrawClock >= 0.25) { _redrawClock = 0; QueueRedraw(); }
        }

        public override void _Draw() {
            if (_world == null || _world.Map == null) return;
            World.WorldMap map = _world.Map;
            Rect2 mapRect = new Rect2(Vector2.Zero, new Vector2(map.Width * TileSize, map.Height * TileSize));
            if (_ground != null) DrawTextureRect(_ground, mapRect, false, Colors.White);

            foreach (Vector2 p in _forestPositions) {
                if (_tree != null) DrawTextureRect(_tree, new Rect2(p - new Vector2(10, 13), new Vector2(20, 20)), false);
            }
            foreach (Vector2 p in _mountainPositions) {
                if (_mountain != null) DrawTextureRect(_mountain, new Rect2(p - new Vector2(12, 14), new Vector2(24, 22)), false);
            }

            // Road tile positions are cached at bind time so armies animate without rescanning the map.
            if (_road != null) {
                foreach (Vector2 p in _roadPositions)
                    DrawTextureRect(_road, new Rect2(p, new Vector2(TileSize, TileSize)), false, new Color(1f, 1f, 1f, .72f));
            }

            foreach (City city in _world.Cities.Values) {
                Kingdom kingdom = null;
                _world.Kingdoms.TryGetValue(city.KingdomId, out kingdom);
                Color color = kingdom == null ? Colors.White : ParseHex(kingdom.ColorHex, Colors.White);
                Vector2 center = WorldToCanvas(city.X, city.Y);
                DrawCircle(center, 8f, new Color(color.R, color.G, color.B, .45f));
                if (_hall != null) DrawTextureRect(_hall, new Rect2(center - new Vector2(17, 19), new Vector2(34, 34)), false);
                else DrawCircle(center, 6f, color);
                if (_house != null) DrawTextureRect(_house, new Rect2(center + new Vector2(6, 3), new Vector2(19, 19)), false);
                DrawCircle(center, 7.5f, Colors.Black, false, 1.2f);
            }

            foreach (Army army in _world.Armies.Values) {
                Color color = army.KingdomId == _world.PlayerKingdomId ? new Color(.98f, .83f, .24f) : new Color(.94f, .25f, .23f);
                Vector2 center = WorldToCanvas(army.X, army.Y);
                if (_warrior != null) DrawTextureRect(_warrior, new Rect2(center - new Vector2(10, 11), new Vector2(20, 20)), false, color);
                else DrawRect(new Rect2(center - new Vector2(4, 4), new Vector2(8, 8)), color, true);
            }
        }

        public static Vector2 WorldToCanvas(float x, float y) {
            return new Vector2((x + .5f) * TileSize, (y + .5f) * TileSize);
        }

        static Color TerrainColor(TerrainKind t) {
            switch (t) {
                case TerrainKind.DeepWater: return new Color(.05f, .17f, .34f);
                case TerrainKind.Lake: return new Color(.08f, .28f, .5f);
                case TerrainKind.Coast: return new Color(.62f, .62f, .4f);
                case TerrainKind.Forest: return new Color(.12f, .35f, .16f);
                case TerrainKind.Hill: return new Color(.42f, .46f, .26f);
                case TerrainKind.Mountain: return new Color(.38f, .38f, .4f);
                case TerrainKind.MountainPass: return new Color(.5f, .46f, .42f);
                case TerrainKind.Snow: return new Color(.82f, .87f, .9f);
                case TerrainKind.Desert: return new Color(.72f, .62f, .32f);
                case TerrainKind.Marsh: return new Color(.24f, .4f, .28f);
                case TerrainKind.River: return new Color(.16f, .42f, .62f);
                default: return new Color(.28f, .5f, .24f);
            }
        }

        static Color ParseHex(string hex, Color fallback) {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            string s = hex.Trim().TrimStart('#');
            if (s.Length != 6) return fallback;
            try {
                byte r = Convert.ToByte(s.Substring(0, 2), 16);
                byte g = Convert.ToByte(s.Substring(2, 2), 16);
                byte b = Convert.ToByte(s.Substring(4, 2), 16);
                return new Color(r / 255f, g / 255f, b / 255f, 1f);
            } catch { return fallback; }
        }
    }
}
