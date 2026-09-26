using System;
using Godot;
using LordWar.AI;
using LordWar.Data;
using LordWar.Simulation;

namespace LordWar.GodotRuntime {
    /// <summary>
    /// Android-safe staged startup. Startup exceptions are displayed instead of terminating the app.
    /// </summary>
    public sealed partial class LordWarApp : Node {
        public GameWorld World { get; private set; }
        public WorldView View { get; private set; }
        public WorldCameraController Camera { get; private set; }
        public LordWarHud Hud { get; private set; }
        public GodotSpriteAssetLibrary Art { get; private set; }
        GameDataCatalog _data;
        CanvasLayer _startupLayer;
        Label _startupLabel;
        bool _booting;
        bool _bootComplete;
        int _startupFrames;

        public override void _Ready() {
            Engine.MaxFps = 60;
            BuildStartupScreen();
            GD.Print("LORDWAR_STARTUP_PHASE=READY");
        }

        public override void _Process(double delta) {
            if (!_bootComplete && !_booting) {
                _startupFrames++;
                if (_startupFrames >= 2) BootstrapRuntime();
                return;
            }
            if (_bootComplete && World != null) World.Tick((float)delta);
        }

        void BuildStartupScreen() {
            _startupLayer = new CanvasLayer { Name = "启动界面", Layer = 100 };
            AddChild(_startupLayer);
            var panel = new PanelContainer { Position = new Vector2(20, 20), Size = new Vector2(700, 180) };
            _startupLayer.AddChild(panel);
            _startupLabel = new Label {
                Text = "领主战争正在初始化……\n正在载入数据并生成世界",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _startupLabel.AddThemeFontSizeOverride("font_size", 22);
            panel.AddChild(_startupLabel);
        }

        void BootstrapRuntime() {
            _booting = true;
            try {
                if (_startupLabel != null) _startupLabel.Text = "领主战争正在初始化……\n正在建立地图、国家与城市";
                GD.Print("LORDWAR_STARTUP_PHASE=BOOTSTRAP_BEGIN");

                View = new WorldView { Name = "世界渲染" }; AddChild(View);
                Camera = new WorldCameraController { Name = "世界相机" }; AddChild(Camera);
                Hud = new LordWarHud { Name = "中文HUD" }; AddChild(Hud);
                Art = new GodotSpriteAssetLibrary();

                CreateFreshWorld(0, 96, 96, 2, AiDifficulty.Hard);
                Hud.Bind(this);

                if (_startupLayer != null) {
                    _startupLayer.QueueFree();
                    _startupLayer = null;
                    _startupLabel = null;
                }
                _bootComplete = true;
                GD.Print("LORDWAR_STARTUP_OK map=96x96 kingdoms=2");
            } catch (Exception ex) {
                _bootComplete = false;
                ShowStartupFailure(ex);
            } finally {
                _booting = false;
            }
        }

        void ShowStartupFailure(Exception ex) {
            string detail = ex == null ? "未知启动异常" : ex.ToString();
            GD.PushError("LORDWAR_STARTUP_FATAL\n" + detail);
            try {
                using Godot.FileAccess f = Godot.FileAccess.Open("user://lordwar_startup_error.txt", Godot.FileAccess.ModeFlags.Write);
                if (f != null) f.StoreString(detail);
            } catch { }

            if (_startupLayer == null || !GodotObject.IsInstanceValid(_startupLayer)) BuildStartupScreen();
            if (_startupLabel != null) {
                _startupLabel.Text = "启动失败，但程序已阻止闪退。\n" +
                    (ex == null ? "未知错误" : ex.GetType().Name + ": " + ex.Message) +
                    "\n诊断已写入 user://lordwar_startup_error.txt";
            }
        }

        public void CreateFreshWorld(int requestedSeed, int requestedWidth, int requestedHeight, int requestedKingdoms, AiDifficulty difficulty) {
            int width = Math.Max(72, Math.Min(192, requestedWidth));
            int height = Math.Max(72, Math.Min(192, requestedHeight));
            int kingdoms = Math.Max(2, Math.Min(4, requestedKingdoms));
            int seed = requestedSeed == 0 ? NewSeed() : requestedSeed;

            if (_data == null) {
                GD.Print("LORDWAR_STARTUP_PHASE=LOAD_DATA");
                _data = new GameDataCatalog();
                _data.LoadAll(new GodotDataProvider());
                GD.Print("LORDWAR_STARTUP_PHASE=DATA_OK skills=" + _data.Skills.Count + " units=" + _data.Units.Count + " specials=" + _data.SpecialUnits.Count);
            }
            GD.Print("LORDWAR_STARTUP_PHASE=CREATE_WORLD " + width + "x" + height + " kingdoms=" + kingdoms);
            World = new GameWorld(seed, _data, difficulty);
            World.CreateNewWorld(width, height, kingdoms);
            GD.Print("LORDWAR_STARTUP_PHASE=WORLD_OK cities=" + World.Cities.Count + " people=" + World.People.Count);
            RebindViews();
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
