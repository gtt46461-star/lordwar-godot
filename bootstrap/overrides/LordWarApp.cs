using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using LordWar.AI;
using LordWar.Data;
using LordWar.Simulation;

namespace LordWar.GodotRuntime {
    /// <summary>
    /// Android-safe staged startup. Heavy pure-C# world generation runs off the main thread;
    /// startup failures are rendered in-app instead of terminating the process.
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
        Task<GameWorld> _worldTask;
        bool _bootStarted;
        bool _bootComplete;
        int _startupFrames;
        readonly Stopwatch _startupWatch = new Stopwatch();

        public override void _Ready() {
            Engine.MaxFps = 60;
            BuildStartupScreen();
            _startupWatch.Start();
            GD.Print("LORDWAR_STARTUP_PHASE=READY");
        }

        public override void _Process(double delta) {
            if (!_bootStarted) {
                _startupFrames++;
                if (_startupFrames >= 2) BeginBootstrap();
                return;
            }

            if (!_bootComplete && _worldTask != null && _worldTask.IsCompleted) {
                CompleteBootstrap();
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
                Text = "领主战争正在初始化……\n正在载入战争数据库",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _startupLabel.AddThemeFontSizeOverride("font_size", 22);
            panel.AddChild(_startupLabel);
        }

        void BeginBootstrap() {
            _bootStarted = true;
            try {
                GD.Print("LORDWAR_STARTUP_PHASE=BOOTSTRAP_BEGIN");
                if (_startupLabel != null) _startupLabel.Text = "领主战争正在初始化……\n正在载入兵种、将军、官员与城市资料";

                _data = new GameDataCatalog();
                _data.LoadAll(new GodotDataProvider());
                GD.Print("LORDWAR_STARTUP_PHASE=DATA_OK skills=" + _data.Skills.Count + " units=" + _data.Units.Count + " specials=" + _data.SpecialUnits.Count);

                int seed = NewSeed();
                const int width = 160;
                const int height = 120;
                const int kingdoms = 4;
                GD.Print("LORDWAR_STARTUP_PHASE=CREATE_WORLD_ASYNC " + width + "x" + height + " kingdoms=" + kingdoms);
                if (_startupLabel != null) _startupLabel.Text = "领主战争正在初始化……\n正在后台生成 160×120 世界与四国势力";

                GameDataCatalog data = _data;
                _worldTask = Task.Run(() => {
                    var world = new GameWorld(seed, data, AiDifficulty.Hard);
                    world.CreateNewWorld(width, height, kingdoms);
                    return world;
                });
            } catch (Exception ex) {
                ShowStartupFailure(ex);
            }
        }

        void CompleteBootstrap() {
            Task<GameWorld> task = _worldTask;
            _worldTask = null;
            if (task == null) return;

            try {
                if (task.IsCanceled) throw new InvalidOperationException("世界生成任务被取消");
                if (task.IsFaulted) throw task.Exception?.GetBaseException() ?? new InvalidOperationException("世界生成失败");

                World = task.Result;
                GD.Print("LORDWAR_STARTUP_PHASE=WORLD_OK cities=" + World.Cities.Count + " people=" + World.People.Count);

                Art = new GodotSpriteAssetLibrary();
                View = new WorldView { Name = "世界渲染" }; AddChild(View);
                Camera = new WorldCameraController { Name = "世界相机" }; AddChild(Camera);
                Hud = new LordWarHud { Name = "中文HUD" }; AddChild(Hud);
                RebindViews();
                Hud.Bind(this);

                if (_startupLayer != null && GodotObject.IsInstanceValid(_startupLayer)) {
                    _startupLayer.QueueFree();
                    _startupLayer = null;
                    _startupLabel = null;
                }

                _startupWatch.Stop();
                _bootComplete = true;
                GD.Print("LORDWAR_STARTUP_OK map=" + World.Map.Width + "x" + World.Map.Height + " kingdoms=" + World.Kingdoms.Count + " ms=" + _startupWatch.ElapsedMilliseconds);

                if (System.Environment.GetEnvironmentVariable("LORDWAR_SMOKE_TEST") == "1") {
                    GetTree().Quit(0);
                }
            } catch (Exception ex) {
                ShowStartupFailure(ex);
            }
        }

        void ShowStartupFailure(Exception ex) {
            _startupWatch.Stop();
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
                _data = new GameDataCatalog();
                _data.LoadAll(new GodotDataProvider());
            }
            World = new GameWorld(seed, _data, difficulty);
            World.CreateNewWorld(width, height, kingdoms);
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