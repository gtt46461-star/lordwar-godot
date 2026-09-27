using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LordWar;
using LordWar.AI;
using LordWar.Data;
using LordWar.Simulation;
using LordWar.World;
using NeoModLoader.api;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using UnityEngine;

namespace LordWar.AndroidMod
{
    // NeoModLoader compiles the .cs files in this folder on the device.
    // World generation and CSV parsing use the unmodified R30 simulation core.
    public sealed class LordWarMod : BasicMod<LordWarMod>
    {
        private GameWorld _world;
        private Task<GameWorld> _creation;
        private string _status = "等待创建世界";
        private bool _panel;
        private bool _showMap = true;
        private bool _showInbox;
        private Vector2 _inboxScroll;
        private string _selectedProposalId;
        private Texture2D _mapTexture;

        protected override void OnModLoad()
        {
            LogInfo("LordWar R30 core source loaded into NML");
            try
            {
                var icon = SpriteTextureLoader.getSprite("ui/Icons/iconKingdom");
                var tab = TabManager.CreateTab("lordwar", "领主战争", "领主战争", icon);
                var open = PowerButtonCreator.CreateSimpleButton(
                    "lordwar_open", (Action)(() => _panel = true), icon, tab.transform);
                PowerButtonCreator.AddButtonToTab(open, tab);
                var inbox = PowerButtonCreator.CreateSimpleButton(
                    "lordwar_inbox", (Action)(() => { _panel = true; _showInbox = true; }), icon, tab.transform);
                PowerButtonCreator.AddButtonToTab(inbox, tab);
            }
            catch (Exception error)
            {
                LogInfo("LordWar native tab unavailable: " + error);
            }
        }

        private void StartWorld(bool fromWorldBox)
        {
            if (_creation != null && !_creation.IsCompleted) return;
            WorldMap imported = null;
            if (fromWorldBox)
            {
                try { imported = CaptureWorldBoxMap(); }
                catch (Exception error) { _status = "读取当前地图失败: " + error.Message; LogInfo(_status); return; }
            }
            _world = null;
            _showInbox = false;
            _selectedProposalId = null;
            if (_mapTexture != null) UnityEngine.Object.Destroy(_mapTexture);
            _mapTexture = null;
            _status = imported == null ? "正在后台生成 160×120 / 4 国世界" : "正在当前地图上创建 4 国世界";
            string folder = GetDeclaration().FolderPath;
            WorldMap mapSnapshot = imported;
            _creation = Task.Run(() =>
            {
                var data = new GameDataCatalog();
                data.LoadAll(new FolderDataProvider(folder));
                var world = new GameWorld(Environment.TickCount, data, AiDifficulty.Hard);
                if (mapSnapshot == null) world.CreateNewWorld(160, 120, 4);
                else world.CreateNewWorld(mapSnapshot, 4);
                return world;
            });
        }

        private static WorldMap CaptureWorldBoxMap()
        {
            MapBox native = MapBox.instance;
            if (native == null || MapBox.width < 16 || MapBox.height < 16)
                throw new InvalidOperationException("请先在 WorldBox 中打开一张地图");
            // Bound memory and simulation costs for large WorldBox worlds. Read IL2CPP tiles on Unity's main thread.
            int width = Math.Min(160, MapBox.width);
            int height = Math.Min(120, MapBox.height);
            WorldMap map = new WorldMap(width, height, Environment.TickCount);
            int land = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int nativeX = (int)((x + .5) * MapBox.width / width);
                int nativeY = (int)((y + .5) * MapBox.height / height);
                global::WorldTile source = native.GetTileSimple(nativeX, nativeY);
                LordWar.World.WorldTile target = map.Get(x, y);
                TileTypeBase type = source == null ? null : source.Type;
                bool water = source == null || source.is_liquid;
                float altitude = source == null ? 0f : Math.Max(0f, Math.Min(1f, source.Height / 255f));
                string biome = type == null ? "" : (type.biome_id ?? "").ToLowerInvariant();
                target.Height = water ? .10f : Math.Max(.30f, altitude);
                target.Continental = target.Height;
                target.Temperature = biome.Contains("snow") || biome.Contains("ice") ? .15f : .55f;
                target.Moisture = biome.Contains("desert") ? .18f : .62f;
                target.Fertility = biome.Contains("desert") ? .22f : .70f;
                target.Forest = biome.Contains("forest") || biome.Contains("jungle") ? .82f : .40f;
                target.Ore = type != null && type.mountains ? .86f : .42f;
                target.Road = type != null && type.road;
                target.Terrain = water ? TerrainKind.DeepWater :
                    type != null && type.mountains ? TerrainKind.Mountain :
                    biome.Contains("snow") || biome.Contains("ice") ? TerrainKind.Snow :
                    biome.Contains("desert") ? TerrainKind.Desert :
                    biome.Contains("forest") || biome.Contains("jungle") ? TerrainKind.Forest : TerrainKind.Grass;
                if (!water) land++;
            }
            if (land < 16) throw new InvalidOperationException("地图陆地太少，无法建立领地");
            return map;
        }

        private void Update()
        {
            if (_creation != null && _creation.IsCompleted)
            {
                Task<GameWorld> completed = _creation;
                _creation = null;
                if (completed.IsFaulted)
                {
                    Exception error = completed.Exception == null ? null : completed.Exception.GetBaseException();
                    _status = "创建失败: " + (error == null ? "未知错误" : error.Message);
                    LogInfo(_status);
                }
                else if (completed.IsCanceled)
                {
                    _status = "创建已取消";
                }
                else
                {
                    _world = completed.Result;
                    BuildMapTexture();
                    _status = "运行中";
                    LogInfo("LordWar world initialized: " + _world.Map.Width + "×" + _world.Map.Height);
                }
            }
            if (_world == null || _world.Paused) return;
            try { _world.Tick(Math.Min(Time.deltaTime, 0.25f)); }
            catch (Exception error)
            {
                _world.Paused = true;
                _status = "运行暂停: " + error.Message;
                LogInfo(_status);
            }
        }

        private void BuildMapTexture()
        {
            WorldMap map = _world.Map;
            _mapTexture = new Texture2D(map.Width, map.Height);
            _mapTexture.filterMode = FilterMode.Point;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                LordWar.World.WorldTile tile = map.Get(x, y);
                Color color;
                switch (tile.Terrain)
                {
                    case TerrainKind.DeepWater: case TerrainKind.Lake: color = new Color(.10f, .22f, .46f); break;
                    case TerrainKind.Coast: color = new Color(.75f, .69f, .48f); break;
                    case TerrainKind.Forest: color = new Color(.13f, .37f, .18f); break;
                    case TerrainKind.Hill: color = new Color(.39f, .42f, .26f); break;
                    case TerrainKind.Mountain: case TerrainKind.MountainPass: color = new Color(.48f, .48f, .47f); break;
                    case TerrainKind.Snow: color = new Color(.83f, .89f, .91f); break;
                    case TerrainKind.Desert: color = new Color(.75f, .65f, .39f); break;
                    case TerrainKind.Marsh: color = new Color(.26f, .39f, .29f); break;
                    default: color = new Color(.34f, .56f, .28f); break;
                }
                if (tile.River) color = new Color(.16f, .40f, .69f);
                if (tile.Road || tile.Bridge) color = new Color(.66f, .53f, .35f);
                _mapTexture.SetPixel(x, y, color);
            }
            _mapTexture.Apply();
        }

        private void OnGUI()
        {
            float scale = Math.Max(1f, Screen.width / 1100f);
            if (GUI.Button(new Rect(12f, 12f, 150f * scale, 54f * scale), "领主战争")) _panel = !_panel;
            if (!_panel) return;

            float width = Math.Min(Screen.width - 24f, 700f * scale);
            float height = Math.Min(Screen.height - 85f, 640f * scale);
            float x = 12f, y = 75f * scale;
            GUI.Box(new Rect(x, y, width, height), "领主战争 · R30 核心接入");
            float line = 34f * scale;
            float top = y + 40f * scale;
            GUI.Label(new Rect(x + 16f, top, width - 32f, line), _status);
            top += line;

            if (GUI.Button(new Rect(x + 16f, top, 165f * scale, 48f * scale), "创建新世界")) StartWorld(false);
            if (GUI.Button(new Rect(x + 190f * scale, top, 220f * scale, 48f * scale), "使用当前地图")) StartWorld(true);
            if (_world == null) return;
            if (GUI.Button(new Rect(x + width - 195f * scale, top, 178f * scale, 48f * scale),
                "提交箱 (" + _world.PlayerPendingProposalCount + ")")) _showInbox = !_showInbox;
            top += 56f * scale;
            if (_showInbox) { DrawInbox(x, y, width, height, top, scale); return; }
            if (GUI.Button(new Rect(x + 16f, top, 140f * scale, 48f * scale), _world.Paused ? "继续" : "暂停"))
                _world.Paused = !_world.Paused;
            if (GUI.Button(new Rect(x + 166f * scale, top, 140f * scale, 48f * scale), "推进一天"))
            {
                try { _world.AdvanceDay(); }
                catch (Exception error) { _status = "推进失败: " + error.Message; }
            }
            top += 60f * scale;
            GUI.Label(new Rect(x + 16f, top, width - 32f, line),
                "第 " + _world.Day + " 天  |  国家 " + _world.Kingdoms.Count +
                "  城市 " + _world.Cities.Count + "  人物 " + _world.People.Count +
                "  军队 " + _world.Armies.Count);
            top += line;
            if (GUI.Button(new Rect(x + 16f, top, 130f * scale, line), _showMap ? "查看国家" : "查看地图"))
                _showMap = !_showMap;
            top += line + 5f * scale;

            if (_showMap && _mapTexture != null)
            {
                float mapWidth = Math.Min(width - 32f, (height - (top - y) - 55f * scale) *
                    _world.Map.Width / (float)_world.Map.Height);
                float mapHeight = mapWidth * _world.Map.Height / (float)_world.Map.Width;
                Rect mapRect = new Rect(x + 16f, top, mapWidth, mapHeight);
                GUI.DrawTexture(mapRect, _mapTexture);
                foreach (City city in _world.Cities.Values)
                {
                    float cx = mapRect.x + city.X / (float)_world.Map.Width * mapRect.width;
                    float cy = mapRect.y + (1f - city.Y / (float)_world.Map.Height) * mapRect.height;
                    GUI.Label(new Rect(cx, cy, 115f * scale, line), "● " + city.Name);
                }
                return;
            }

            Kingdom player;
            if (!string.IsNullOrEmpty(_world.PlayerKingdomId) &&
                _world.Kingdoms.TryGetValue(_world.PlayerKingdomId, out player))
            {
                GUI.Label(new Rect(x + 16f, top, width - 32f, line),
                    "领地: " + player.Name + "  国库: " + player.Treasury);
                top += line;
                foreach (Kingdom rival in _world.Kingdoms.Values)
                {
                    if (rival.Id == player.Id || top + line > y + height - 95f * scale) continue;
                    GUI.Label(new Rect(x + 16f, top, width - 200f * scale, line),
                        rival.Name + " | " + _world.Diplomacy.Get(player.Id, rival.Id).State);
                    if (GUI.Button(new Rect(x + width - 178f * scale, top, 155f * scale, line), "宣战并出征"))
                    {
                        var selected = _world.Armies.Values.Where(a => a.KingdomId == player.Id)
                            .Select(a => a.Id).ToArray();
                        int moved = _world.DeclareWarAndMarch(rival.Id, selected);
                        _status = "对 " + rival.Name + " 宣战，出征军队 " + moved;
                    }
                    top += line + 6f * scale;
                }
            }
            if (_world.Events.Count > 0)
            {
                WorldEvent last = _world.Events[_world.Events.Count - 1];
                GUI.Label(new Rect(x + 16f, y + height - 48f * scale, width - 32f, line),
                    "最新事件: " + last.Title);
            }
        }

        private void DrawInbox(float x, float y, float width, float height, float top, float scale)
        {
            GUI.Label(new Rect(x + 16f, top, width - 32f, 34f * scale), "待处理申请：" + _world.PlayerPendingProposalCount);
            top += 38f * scale;
            float viewportHeight = Math.Max(100f * scale, height - (top - y) - 120f * scale);
            var pending = _world.Proposals.Queue.Where(p => p.State == ProposalState.Pending &&
                _world.IsPlayerProposal(p)).ToArray();
            float row = 49f * scale;
            _inboxScroll = GUI.BeginScrollView(new Rect(x + 16f, top, width - 32f, viewportHeight),
                _inboxScroll, new Rect(0f, 0f, width - 58f, Math.Max(viewportHeight, pending.Length * row)));
            for (int i = 0; i < pending.Length; i++)
            {
                Proposal proposal = pending[i];
                if (GUI.Button(new Rect(0f, i * row, width - 60f, row - 3f * scale),
                    proposal.Title + " · 第 " + proposal.SubmittedDay + " 天"))
                    _selectedProposalId = proposal.Id;
            }
            GUI.EndScrollView();
            top += viewportHeight + 8f * scale;
            Proposal selected = pending.FirstOrDefault(p => p.Id == _selectedProposalId);
            if (selected == null && pending.Length > 0) selected = pending[0];
            if (selected == null) { GUI.Label(new Rect(x + 16f, top, width - 32f, row), "暂无待处理申请"); return; }
            GUI.Label(new Rect(x + 16f, top, width - 32f, 44f * scale),
                selected.Title + " | 金币 " + selected.CostGold + " | " + selected.Description);
            top += 47f * scale;
            if (GUI.Button(new Rect(x + 16f, top, 120f * scale, 43f * scale), "同意"))
            {
                _status = _world.ApproveProposal(selected.Id) ? "已同意：" + selected.Title : "申请未能执行：" + selected.Title;
                _selectedProposalId = null;
            }
            if (GUI.Button(new Rect(x + 148f * scale, top, 120f * scale, 43f * scale), "拒绝"))
            {
                _status = _world.RejectProposal(selected.Id) ? "已拒绝：" + selected.Title : "拒绝失败：" + selected.Title;
                _selectedProposalId = null;
            }
        }

        private sealed class FolderDataProvider : ITextDataProvider
        {
            private readonly string _folder;
            public FolderDataProvider(string folder) { _folder = folder; }
            public string Load(string key)
            {
                string path = Path.Combine(_folder, "Data", key + ".csv");
                if (!File.Exists(path)) throw new FileNotFoundException("缺少领主战争数据", path);
                return File.ReadAllText(path);
            }
        }
    }
}
