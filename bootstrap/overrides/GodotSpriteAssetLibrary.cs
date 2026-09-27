using System;
using System.Collections.Generic;
using Godot;

namespace LordWar.GodotRuntime {
    /// <summary>Lazy runtime index for the 1,640 migrated PNG assets.</summary>
    public sealed class GodotSpriteAssetLibrary {
        const string Root = "res://Art/LordWarArt";
        readonly List<string> _pngs = new List<string>();
        readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        bool _indexed;

        public int Count { get { EnsureIndex(); return _pngs.Count; } }

        public Texture2D FindContains(params string[] keys) {
            EnsureIndex();
            if (keys != null) {
                for (int k = 0; k < keys.Length; k++) {
                    string key = (keys[k] ?? "").Trim().ToLowerInvariant();
                    if (key.Length == 0) continue;
                    for (int i = 0; i < _pngs.Count; i++) {
                        if (_pngs[i].ToLowerInvariant().Contains(key)) return Load(_pngs[i]);
                    }
                }
            }
            return null;
        }

        public Texture2D FindVariant(int seed, params string[] keys) {
            EnsureIndex();
            var matches = new List<string>();
            if (keys != null) {
                for (int k = 0; k < keys.Length && matches.Count == 0; k++) {
                    string key = (keys[k] ?? "").Trim().ToLowerInvariant();
                    if (key.Length == 0) continue;
                    foreach (string path in _pngs) if (path.ToLowerInvariant().Contains(key)) matches.Add(path);
                }
            }
            if (matches.Count == 0) return FindContains(keys);
            int n = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
            return Load(matches[n % matches.Count]);
        }

        Texture2D Load(string path) {
            Texture2D tex;
            if (_cache.TryGetValue(path, out tex) && tex != null) return tex;
            tex = GD.Load<Texture2D>(path);
            if (tex != null) _cache[path] = tex;
            return tex;
        }

        void EnsureIndex() {
            if (_indexed) return;
            _indexed = true;
            Scan(Root);
            _pngs.Sort(StringComparer.OrdinalIgnoreCase);
        }

        void Scan(string folder) {
            using DirAccess dir = DirAccess.Open(folder);
            if (dir == null) return;
            foreach (string file in dir.GetFiles()) {
                if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) _pngs.Add(folder + "/" + file);
            }
            foreach (string child in dir.GetDirectories()) Scan(folder + "/" + child);
        }
    }
}
