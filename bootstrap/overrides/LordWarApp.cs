using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Godot;
using LordWar.AI;
using LordWar.Data;
using LordWar.Simulation;

namespace LordWar.GodotRuntime {
    /// <summary>
    /// Mobile-first application shell: show a responsive main menu first, then create worlds asynchronously.
    /// Heavy world generation never blocks the Godot render thread.
    /// </summary>
    public sealed partial class LordWarApp : Node {
        public GameWorld World { get; private set; }
        public WorldView View { get; private set; }
        public WorldCameraController Camera { get; private set; }
        public LordWarHud Hud { get; private set; }
        public GodotSpriteAssetLibrary Art { get; private set; }

        GameDataCatalog _data;
        CanvasLayer _menuLayer;
        CanvasLayer _loadingLayer;
        Label _loadingLabel;
        Task<GameWorld> _worldTask;
        int _pendingWidth;
        int _pendingHeight;
        int _pendingKingdoms;
        AiDifficulty _pendingDifficulty;
        readonly Stopwatch _worldWatch = new Stopwatch();
        bool _ciAutoStart;
        int _menuFrames;

        public override void _Ready() {
            Engine.MaxFps = 60;
            bool forcedSmoke = string.Equals(System.Environment.GetEnvironmentVariable("LORDWAR_SMOKE_TEST"), "1", StringComparison.Ordinal);
            _ciAutoStart = forcedSmoke || (OS.GetName() == "Android" && RuntimeInformation.ProcessArchitecture == Architecture.X64);
            BuildMainMenu();
            GD.Print("LORDWAR_MENU_READY arch=" + RuntimeInformation.ProcessArchitecture + " android=" + (OS.GetName() == "Android"));
        }

        public override void _Process(double delta) {
            if (_ciAutoStart && _worldTask == null && World == null && _menuLayer != null) {
                _menuFrames++;
                if (_menuFrames >= 45) {
                    _ciAutoStart = false;
                    GD.Print("LORDWAR_CI_AUTOSTART quick=80x60 kingdoms=3");
                    BeginWorldGeneration(0, 80, 60, 3, AiDifficulty.Hard, "CI快速开局");
                }
            }

            if (_worldTask != null && _worldTask.IsCompleted) CompleteWorldGeneration();
            if (World != null) World.Tick((float)delta);
        }

        void BuildMainMenu() {
            FreeLayer(ref _loadingLayer);
            FreeLayer(ref _menuLayer);
            DestroyGameplayNodes();
            World = null;

            _menuLayer = new CanvasLayer { Name = "主菜单", Layer = 100 };
            AddChild(_menuLayer);

            var shade = new ColorRect {
                Color = new Color(0.035f, 0.04f, 0.055f, 1f),
                Position = Vector2.Zero,
                Size = new Vector2(1920, 1080),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _menuLayer.AddChild(shade);

            var panel = new PanelContainer {
                Position = new Vector2(470, 120),
                Size = new Vector2(980, 820)
            };
            _menuLayer.AddChild(panel);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 56);
            margin.AddThemeConstantOverride("margin_right", 56);
            margin.AddThemeConstantOverride("margin_top", 42);
            margin.AddThemeConstantOverride("margin_bottom", 42);
            panel.AddChild(margin);

            var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            box.AddThemeConstantOverride("separation", 18);
            margin.AddChild(box);

            var title = new Label { Text = "领 主 战 争", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 54);
            box.AddChild(title);

            var sub = new Label {
                Text = "经营城市 · 任命官员 · 统率军队 · 攻城略地",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            sub.AddThemeFontSizeOverride("font_size", 24);
            box.AddChild(sub);

            var info = new Label {
                Text = "选择开局规模。快速开局适合手机直接游玩；大型世界保留完整 160×120 四国规模。",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(760, 70)
            };
            info.AddThemeFontSizeOverride("font_size", 20);
            box.AddChild(info);

            AddMenuButton(box, "快速开局｜80×60｜三国", () => BeginWorldGeneration(0, 80, 60, 3, AiDifficulty.Hard, "快速开局"));
            AddMenuButton(box, "标准战役｜112×84｜四国", () => BeginWorldGeneration(0, 112, 84, 4, AiDifficulty.Hard, "标准战役"));
            AddMenuButton(box, "大型世界｜160×120｜四国", () => BeginWorldGeneration(0, 160, 120, 4, AiDifficulty.Hard, "大型世界"));

            var note = new Label {
                Text = "战争采用软克制：兵种、将军、官员、地形、士气和补给共同决定结果，不存在单项碾压。",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(760, 90)
            };
            note.AddThemeFontSizeOverride("font_size", 18);
            box.AddChild(note);

            GD.Print("LORDWAR_MAIN_MENU_VISIBLE");
        }

        static void AddMenuButton(Container parent, string text, Action action) {
            var button = new Button {
                Text = text,
                CustomMinimumSize = new Vector2(760, 76)
            };
            button.AddThemeFontSizeOverride("font_size", 26);
            button.Pressed += action;
            parent.AddChild(button);
        }

        void BuildLoadingScreen(string title, int width, int height, int kingdoms) {
            FreeLayer(ref _menuLayer);
            FreeLayer(ref _loadingLayer);
            _loadingLayer = new CanvasLayer { Name = "世界生成", Layer = 100 };
            AddChild(_loadingLayer);

            var bg = new ColorRect { Color = new Color(0.03f, 0.035f, 0.05f, 1f), Position = Vector2.Zero, Size = new Vector2(1920, 1080) };
            _loadingLayer.AddChild(bg);
            var panel = new PanelContainer { Position = new Vector2(480, 320), Size = new Vector2(960, 390) };
            _loadingLayer.AddChild(panel);
            _loadingLabel = new Label {
                Text = title + "\n正在后台生成 " + width + "×" + height + " 世界与 " + kingdoms + " 国势力……\n地图生成期间界面保持响应，请稍候。",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _loadingLabel.AddThemeFontSizeOverride("font_size", 28);
            panel.AddChild(_loadingLabel);
        }

        void BeginWorldGeneration(int requestedSeed, int width, int height, int kingdoms, AiDifficulty difficulty, string label) {
            if (_worldTask != null) return;
            width = Math.Max(64, Math.Min(192, width));
            height = Math.Max(48, Math.Min(160, height));
            kingdoms = Math.Max(2, Math.Min(4, kingdoms));
            int seed = requestedSeed == 0 ? NewSeed() : requestedSeed;

            try {
                if (_data == null) {
                    _data = new GameDataCatalog();
                    _data.LoadAll(new GodotDataProvider());
                    GD.Print("LORDWAR_DATA_OK skills=" + _data.Skills.Count + " units=" + _data.Units.Count + " specials=" + _data.SpecialUnits.Count);
                }

                DestroyGameplayNodes();
                World = null;
                _pendingWidth = width;
                _pendingHeight = height;
                _pendingKingdoms = kingdoms;
                _pendingDifficulty = difficulty;
                BuildLoadingScreen(label, width, height, kingdoms);
                _worldWatch.Restart();
                GD.Print("LORDWAR_GAME_CREATE_BEGIN map=" + width + "x" + height + " kingdoms=" + kingdoms);

                GameDataCatalog data = _data;
                _worldTask = Task.Run(() => {
                    var world = new GameWorld(seed, data, difficulty);
                    world.CreateNewWorld(width, height, kingdoms);
                    return world;
                });
            } catch (Exception ex) {
                ShowStartupFailure(ex);
            }
        }

        void CompleteWorldGeneration() {
            Task<GameWorld> task = _worldTask;
            _worldTask = null;
            if (task == null) return;
            try {
                if (task.IsCanceled) throw new InvalidOperationException("世界生成任务被取消");
                if (task.IsFaulted) throw task.Exception?.GetBaseException() ?? new InvalidOperationException("世界生成失败");

                World = task.Result;
                GD.Print("LORDWAR_WORLD_OK cities=" + World.Cities.Count + " people=" + World.People.Count);

                Art = new GodotSpriteAssetLibrary();
                View = new WorldView { Name = "世界渲染" }; AddChild(View);
                Camera = new WorldCameraController { Name = "世界相机" }; AddChild(Camera);
                Hud = new LordWarHud { Name = "中文HUD" }; AddChild(Hud);
                RebindViews();
                Hud.Bind(this);
                FreeLayer(ref _loadingLayer);

                _worldWatch.Stop();
                GD.Print("LORDWAR_GAME_READY map=" + World.Map.Width + "x" + World.Map.Height + " kingdoms=" + World.Kingdoms.Count + " ms=" + _worldWatch.ElapsedMilliseconds);
            } catch (Exception ex) {
                ShowStartupFailure(ex);
            }
        }

        void ShowStartupFailure(Exception ex) {
            _worldTask = null;
            string detail = ex == null ? "未知启动异常" : ex.ToString();
            GD.PushError("LORDWAR_STARTUP_FATAL\n" + detail);
            try {
                using Godot.FileAccess f = Godot.FileAccess.Open("user://lordwar_startup_error.txt", Godot.FileAccess.ModeFlags.Write);
                if (f != null) f.StoreString(detail);
            } catch { }

            FreeLayer(ref _loadingLayer);
            FreeLayer(ref _menuLayer);
            _menuLayer = new CanvasLayer { Name = "错误界面", Layer = 120 };
            AddChild(_menuLayer);
            var panel = new PanelContainer { Position = new Vector2(410, 250), Size = new Vector2(1100, 560) };
            _menuLayer.AddChild(panel);
            var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            panel.AddChild(box);
            var msg = new Label {
                Text = "游戏初始化失败，但程序已阻止闪退。\n" + (ex == null ? "未知错误" : ex.GetType().Name + ": " + ex.Message) + "\n诊断：user://lordwar_startup_error.txt",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(980, 300)
            };
            msg.AddThemeFontSizeOverride("font_size", 24);
            box.AddChild(msg);
            AddMenuButton(box, "返回主菜单", BuildMainMenu);
        }

        void DestroyGameplayNodes() {
            if (Hud != null && GodotObject.IsInstanceValid(Hud)) Hud.QueueFree();
            if (View != null && GodotObject.IsInstanceValid(View)) View.QueueFree();
            if (Camera != null && GodotObject.IsInstanceValid(Camera)) Camera.QueueFree();
            Hud = null; View = null; Camera = null; Art = null;
        }

        static void FreeLayer(ref CanvasLayer layer) {
            if (layer != null && GodotObject.IsInstanceValid(layer)) layer.QueueFree();
            layer = null;
        }

        public void CreateFreshWorld(int requestedSeed, int requestedWidth, int requestedHeight, int requestedKingdoms, AiDifficulty difficulty) {
            BeginWorldGeneration(requestedSeed, requestedWidth, requestedHeight, requestedKingdoms, difficulty, "新建世界");
        }

        public void RebindViews() {
            if (View != null) View.Bind(World);
            if (Camera != null) Camera.Bind(World);
        }

        static int NewSeed() {
            unchecked { return (int)(DateTime.UtcNow.Ticks ^ System.Environment.TickCount64); }
        }
    }
}