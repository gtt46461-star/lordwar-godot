using System;
using System.Collections.Generic;
using Godot;
using LordWar.Simulation;

namespace LordWar.GodotRuntime {
    /// <summary>Touch/mouse map navigation replacing Unity Camera/Input APIs.</summary>
    public sealed partial class WorldCameraController : Camera2D {
        readonly Dictionary<int, Vector2> _touches = new Dictionary<int, Vector2>();
        GameWorld _world;
        bool _mousePan;
        float _lastPinchDistance;
        public float MinZoom = .35f;
        public float MaxZoom = 3.2f;

        public void Bind(GameWorld world) {
            _world = world;
            Enabled = true;
            CenterOnWorld();
        }

        public override void _UnhandledInput(InputEvent e) {
            if (_world == null || _world.Map == null) return;

            InputEventMouseButton mb = e as InputEventMouseButton;
            if (mb != null) {
                if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) ZoomBy(1.12f);
                else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) ZoomBy(1f / 1.12f);
                else if (mb.ButtonIndex == MouseButton.Middle || mb.ButtonIndex == MouseButton.Right) _mousePan = mb.Pressed;
                return;
            }

            InputEventMouseMotion mm = e as InputEventMouseMotion;
            if (mm != null && _mousePan) { PanPixels(mm.Relative); return; }

            InputEventScreenTouch touch = e as InputEventScreenTouch;
            if (touch != null) {
                if (touch.Pressed) _touches[touch.Index] = touch.Position;
                else _touches.Remove(touch.Index);
                _lastPinchDistance = CurrentPinchDistance();
                return;
            }

            InputEventScreenDrag drag = e as InputEventScreenDrag;
            if (drag != null) {
                _touches[drag.Index] = drag.Position;
                if (_touches.Count <= 1) PanPixels(drag.Relative);
                else {
                    float now = CurrentPinchDistance();
                    if (_lastPinchDistance > 0 && now > 0) ZoomBy(now / _lastPinchDistance);
                    _lastPinchDistance = now;
                }
            }
        }

        public void FocusWorldPoint(float worldX, float worldY) {
            Position = WorldView.WorldToCanvas(worldX, worldY);
            ClampToWorld();
        }

        public void CenterOnWorld() {
            if (_world == null || _world.Map == null) return;
            Kingdom capitalOwner; City capital;
            if (!string.IsNullOrEmpty(_world.PlayerKingdomId) && _world.Kingdoms.TryGetValue(_world.PlayerKingdomId,out capitalOwner) &&
                _world.Cities.TryGetValue(capitalOwner.CapitalCityId,out capital))
                Position = WorldView.WorldToCanvas(capital.X,capital.Y);
            else Position = new Vector2(_world.Map.Width * WorldView.TileSize * .5f, _world.Map.Height * WorldView.TileSize * .5f);
            Zoom = new Vector2(1.65f,1.65f);
            ClampToWorld();
        }

        void PanPixels(Vector2 delta) {
            float z = Math.Max(.01f, Zoom.X);
            Position -= delta / z;
            ClampToWorld();
        }

        void ZoomBy(float factor) {
            float z = Mathf.Clamp(Zoom.X * factor, MinZoom, MaxZoom);
            Zoom = new Vector2(z, z);
            ClampToWorld();
        }

        float CurrentPinchDistance() {
            if (_touches.Count < 2) return 0;
            Vector2 a = default, b = default; int i = 0;
            foreach (Vector2 p in _touches.Values) { if (i++ == 0) a = p; else { b = p; break; } }
            return a.DistanceTo(b);
        }

        void ClampToWorld() {
            if (_world == null || _world.Map == null) return;
            Vector2 view = GetViewport().GetVisibleRect().Size;
            float halfW = view.X * .5f / Math.Max(.01f, Zoom.X);
            float halfH = view.Y * .5f / Math.Max(.01f, Zoom.Y);
            float worldW = _world.Map.Width * WorldView.TileSize;
            float worldH = _world.Map.Height * WorldView.TileSize;
            float x = worldW <= halfW * 2 ? worldW * .5f : Mathf.Clamp(Position.X, halfW, worldW - halfW);
            float y = worldH <= halfH * 2 ? worldH * .5f : Mathf.Clamp(Position.Y, halfH, worldH - halfH);
            Position = new Vector2(x, y);
        }
    }
}
