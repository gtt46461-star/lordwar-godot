using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Godot;
using LordWar.AI;
using LordWar.Data;
using LordWar.Simulation;
using LordWar.Save;

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
        Label _menuStatus;
        Task<GameWorld> _worldTask;
        int _pendingWidth;
        int _pendingHeight;
        int _pendingKingdoms;
        AiDifficulty _pendingDifficulty;
        readonly Stopwatch _worldWatch = new Stopwatch();
        bool _ciAutoStart;
        bool _smokeRequested;
        int _menuFrames;
        double _runtimeProbeSeconds;
        bool _ciCapturePending;
        int _ciCaptureFrames;

        public override void _Ready() {
            Engine.MaxFps = 60;
            bool forcedSmoke = string.Equals(System.Environment.GetEnvironmentVariable("LORDWAR_SMOKE_TEST"), "1", StringComparison.Ordinal);
            _ciAutoStart = forcedSmoke || (OS.GetName() == "Android" && RuntimeInformation.ProcessArchitecture == Architecture.X64);
            _smokeRequested = _ciAutoStart;
            BuildMainMenu();
            GD.Print("LORDWAR_MENU_READY arch=" + RuntimeInformation.ProcessArchitecture + " android=" + (OS.GetName() == "Android"));
        }

        public override void _Process(double delta) {
            if (_ciAutoStart && _worldTask == null && World == null && _menuLayer != null) {
                _menuFrames++;
                if (_menuFrames >= 45) {
                    _ciAutoStart = false;
                    GameSave candidate; string state;
                    if (GodotSaveService.TryRead(out candidate,out state)) {
                        GD.Print("LORDWAR_CI_AUTOLOAD existing-save");
                        LoadSavedWorld();
                    } else {
                        GD.Print("LORDWAR_CI_AUTOSTART quick=80x60 kingdoms=3");
                        BeginWorldGeneration(0, 80, 60, 3, AiDifficulty.Hard, "CI快速开局");
                    }
                }
            }

            if (_worldTask != null && _worldTask.IsCompleted) CompleteWorldGeneration();
            if (World != null) {
                World.Tick((float)delta);
                _runtimeProbeSeconds += delta;
                if (_runtimeProbeSeconds >= 5.0) {
                    _runtimeProbeSeconds = 0.0;
                    GD.Print("LORDWAR_RUNTIME_ALIVE day=" + World.Day + " cities=" + World.Cities.Count + " people=" + World.People.Count);
                }
            }

            if (_ciCapturePending) {
                _ciCaptureFrames++;
                if (_ciCaptureFrames >= 30) {
                    _ciCapturePending = false;
                    try {
                        Image image = GetViewport().GetTexture().GetImage();
                        Error err = image.SavePng("user://lordwar_ci_game.png");
                        GD.Print("LORDWAR_CI_CAPTURE_OK error=" + err);
                    } catch (Exception ex) {
                        GD.PushError("LORDWAR_CI_CAPTURE_FAIL " + ex);
                    }
                }
            }
        }

        void BuildMainMenu() {
            FreeLayer(ref _loadingLayer);
            FreeLayer(ref _menuLayer);
            DestroyGameplayNodes();
            World = null;

            _menuLayer = new CanvasLayer { Name = "主菜单", Layer = 100 };
            AddChild(_menuLayer);

            var shade = new ColorRect {
                Name = "主菜单背景",
                Color = new Color(0.035f, 0.04f, 0.055f, 1f),
                AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _menuLayer.AddChild(shade);

            var panel = new PanelContainer {
                Name = "主菜单面板",
                AnchorLeft = 0.08f, AnchorTop = 0.06f, AnchorRight = 0.92f, AnchorBottom = 0.94f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0
            };
            var menuFrame = new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://Art/LordWarArt/UI_界面/windowBig__resources.assets__852.png") };
            menuFrame.TextureMarginLeft = 12; menuFrame.TextureMarginRight = 12;
            menuFrame.TextureMarginTop = 12; menuFrame.TextureMarginBottom = 12;
            panel.AddThemeStyleboxOverride("panel", menuFrame);
            _menuLayer.AddChild(panel);

            var scroll = new ScrollContainer {
                AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0
            };
            panel.AddChild(scroll);

            var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            margin.AddThemeConstantOverride("margin_left", 42);
            margin.AddThemeConstantOverride("margin_right", 42);
            margin.AddThemeConstantOverride("margin_top", 28);
            margin.AddThemeConstantOverride("margin_bottom", 28);
            scroll.AddChild(margin);

            var box = new VBoxContainer {
                Alignment = BoxContainer.AlignmentMode.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            box.AddThemeConstantOverride("separation", 14);
            margin.AddChild(box);

            var title = new Label { Text = "领 主 战 争", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 48);
            box.AddChild(title);

            var sub = new Label {
                Text = "经营城市 · 任命官员 · 统率军队 · 攻城略地",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            sub.AddThemeFontSizeOverride("font_size", 23);
            box.AddChild(sub);

            var info = new Label {
                Text = "选择战役规模。快速开局适合直接游玩；大型世界保留 160×120 四国完整规模。",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 58)
            };
            info.AddThemeFontSizeOverride("font_size", 19);
            box.AddChild(info);

            AddMenuButton(box, "快速开局｜80×60｜三国", () => BeginWorldGeneration(0, 80, 60, 3, AiDifficulty.Hard, "快速开局"));
            AddMenuButton(box, "标准战役｜112×84｜四国", () => BeginWorldGeneration(0, 112, 84, 4, AiDifficulty.Hard, "标准战役"));
            AddMenuButton(box, "大型世界｜160×120｜四国", () => BeginWorldGeneration(0, 160, 120, 4, AiDifficulty.Hard, "大型世界"));
            AddMenuButton(box, "继续已有世界", LoadSavedWorld);
            _menuStatus = new Label { Text = "N01 · " + BuildInfo.BuildId, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _menuStatus.AddThemeFontSizeOverride("font_size", 16);
            box.AddChild(_menuStatus);

            var note = new Label {
                Text = "软克制体系：兵种、将军、官员、地形、士气、体力与补给共同决定战果；单个神将或单一兵种不能直接碾压。",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 74)
            };
            note.AddThemeFontSizeOverride("font_size", 17);
            box.AddChild(note);

            GD.Print("LORDWAR_MAIN_MENU_VISIBLE");
        }

        static void AddMenuButton(Container parent, string text, Action action) {
            var button = new Button {
                Text = text,
                CustomMinimumSize = new Vector2(0, 68),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            button.AddThemeFontSizeOverride("font_size", 24);
            var frame = new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://Art/LordWarArt/UI_界面/buttonLong__e2122512b478b784b9d820a3167ca52e__1.png") };
            frame.TextureMarginLeft = 8; frame.TextureMarginRight = 8; frame.TextureMarginTop = 6; frame.TextureMarginBottom = 6;
            button.AddThemeStyleboxOverride("normal", frame);
            button.Pressed += action;
            parent.AddChild(button);
        }

        void BuildLoadingScreen(string title, int width, int height, int kingdoms) {
            FreeLayer(ref _menuLayer);
            FreeLayer(ref _loadingLayer);
            _loadingLayer = new CanvasLayer { Name = "世界生成", Layer = 100 };
            AddChild(_loadingLayer);

            var bg = new ColorRect {
                Color = new Color(0.03f, 0.035f, 0.05f, 1f),
                AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0
            };
            _loadingLayer.AddChild(bg);

            var panel = new PanelContainer {
                AnchorLeft = 0.14f, AnchorTop = 0.28f, AnchorRight = 0.86f, AnchorBottom = 0.72f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0
            };
            _loadingLayer.AddChild(panel);

            _loadingLabel = new Label {
                Text = title + "\n正在后台生成 " + width + "×" + height + " 世界与 " + kingdoms + " 国势力……\n地图生成期间界面保持响应，请稍候。",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _loadingLabel.AddThemeFontSizeOverride("font_size", 26);
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

                InstallWorld(task.Result);
                if (_smokeRequested) VerifyN01Smoke();

                _worldWatch.Stop();
                GD.Print("LORDWAR_GAME_READY map=" + World.Map.Width + "x" + World.Map.Height + " kingdoms=" + World.Kingdoms.Count + " ms=" + _worldWatch.ElapsedMilliseconds);
                if (OS.GetName() == "Android" && RuntimeInformation.ProcessArchitecture == Architecture.X64) {
                    _ciCaptureFrames = 0;
                    _ciCapturePending = true;
                }
            } catch (Exception ex) {
                ShowStartupFailure(ex);
            }
        }

        void VerifyN01Smoke() {
            try {
                Person walker = null;
                foreach (Person p in World.People.Values) if (p.IsWorldWalker) { walker = p; break; }
                if (walker == null || walker.WalkRoute.Count < 2) throw new InvalidOperationException("缺少真实人口行走路线");
                World.Tick(.2f);
                long tick = World.Clock.TickIndex;
                if (tick == 0) throw new InvalidOperationException("时钟未推进");
                string reason;
                var pause = new WorldCommand("n01-pause-check", WorldCommandKind.Pause);
                if (!World.ExecuteClockCommand(pause,out reason) || World.ExecuteClockCommand(pause,out reason) || !World.Paused)
                    throw new InvalidOperationException("暂停命令去重失败");
                World.Tick(.2f);
                if (World.Clock.TickIndex != tick) throw new InvalidOperationException("暂停期间时钟推进");
                string msg;
                if (!GodotSaveService.Save(World,out msg)) throw new InvalidOperationException(msg);
                GameSave save;
                if (!GodotSaveService.TryRead(out save,out msg)) throw new InvalidOperationException(msg);
                var restored = new GameWorld(save.Seed,_data);
                GameSaveService.Restore(restored,save);
                Person restoredWalker;
                if (!restored.People.TryGetValue(walker.Id,out restoredWalker) || !restoredWalker.IsWorldWalker ||
                    restoredWalker.WalkRoute.Count != walker.WalkRoute.Count || restored.Events.Count != World.Events.Count ||
                    restored.Clock.TickIndex != tick || !restored.Paused)
                    throw new InvalidOperationException("存档恢复丢失世界状态");
                if (restored.ExecuteClockCommand(pause,out reason)) throw new InvalidOperationException("重载后重复命令被执行");
                if (!World.ExecuteClockCommand(new WorldCommand("n01-resume-check",WorldCommandKind.Resume),out reason))
                    throw new InvalidOperationException("恢复运行失败："+reason);
                GD.Print("LORDWAR_N01_SMOKE_PASS save=roundtrip clock="+tick+" person="+walker.Id);
            } catch (Exception ex) {
                GD.PushError("LORDWAR_N01_SMOKE_FAIL " + ex);
                throw;
            }
        }

        void LoadSavedWorld() {
            GameSave save; string message;
            if (!GodotSaveService.TryRead(out save, out message)) { if (_menuStatus != null) _menuStatus.Text = message; return; }
            try {
                if (_data == null) { _data = new GameDataCatalog(); _data.LoadAll(new GodotDataProvider()); }
                var restored = new GameWorld(save.Seed, _data);
                GameSaveService.Restore(restored, save);
                InstallWorld(restored);
                GD.Print("LORDWAR_SAVE_RESTORED day=" + World.Day + " tick=" + World.Clock.TickIndex);
                GD.Print("LORDWAR_GAME_READY restored map=" + World.Map.Width + "x" + World.Map.Height + " kingdoms=" + World.Kingdoms.Count);
            } catch (Exception ex) {
                if (_menuStatus != null) _menuStatus.Text = "读档失败：" + ex.Message;
                GD.PushError("LORDWAR_SAVE_RESTORE_FAIL " + ex);
            }
        }

        void InstallWorld(GameWorld world) {
            DestroyGameplayNodes();
            World = world;
            GD.Print("LORDWAR_WORLD_OK cities=" + World.Cities.Count + " people=" + World.People.Count);
            Art = new GodotSpriteAssetLibrary();
            View = new WorldView { Name = "世界渲染" }; AddChild(View);
            Camera = new WorldCameraController { Name = "世界相机" }; AddChild(Camera);
            Hud = new LordWarHud { Name = "中文HUD" }; AddChild(Hud);
            RebindViews();
            Hud.Bind(this);
            FreeLayer(ref _loadingLayer);
            FreeLayer(ref _menuLayer);
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

            var bg = new ColorRect {
                Color = new Color(0.045f, 0.025f, 0.03f, 1f),
                AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0
            };
            _menuLayer.AddChild(bg);

            var panel = new PanelContainer {
                AnchorLeft = 0.1f, AnchorTop = 0.16f, AnchorRight = 0.9f, AnchorBottom = 0.84f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0
            };
            _menuLayer.AddChild(panel);

            var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            panel.AddChild(box);
            var msg = new Label {
                Text = "游戏初始化失败，但程序已阻止闪退。\n" +
                    (ex == null ? "未知错误" : ex.GetType().Name + ": " + ex.Message) +
                    "\n诊断：user://lordwar_startup_error.txt",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 280)
            };
            msg.AddThemeFontSizeOverride("font_size", 22);
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

        public void ReturnToMainMenu() {
            if (_worldTask != null && !_worldTask.IsCompleted) return;
            BuildMainMenu();
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
