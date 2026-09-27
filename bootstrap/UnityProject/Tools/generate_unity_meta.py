"""Create deterministic GUID metadata for the reconstructed N01 Unity source tree.

Run once before opening the project in Unity; existing .meta files are never replaced.
"""
from pathlib import Path
from uuid import NAMESPACE_URL, uuid5

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"


def guid(path: Path) -> str:
    return uuid5(NAMESPACE_URL, "lordwar-unity-r29/" + path.relative_to(ROOT).as_posix()).hex


def metadata(path: Path) -> str:
    prefix = f"fileFormatVersion: 2\nguid: {guid(path)}\n"
    if path.is_dir():
        return prefix + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    if path.suffix == ".cs":
        return prefix + "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    if path.suffix == ".png":
        return prefix + "TextureImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 12\n  textureType: 8\n  spritePixelsPerUnit: 16\n  spritePivot: {x: 0.5, y: 0}\n  spriteAlignment: 9\n  isReadable: 1\n  mipmapEnabled: 0\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    importer = "AssemblyDefinitionImporter" if path.suffix == ".asmdef" else "DefaultImporter"
    return prefix + f"{importer}:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"


if __name__ == "__main__":
    count = 0
    for item in sorted(ASSETS.rglob("*")):
        if item.name.endswith(".meta"):
            continue
        target = Path(str(item) + ".meta")
        if not target.exists():
            target.write_text(metadata(item), encoding="utf-8")
            count += 1
    print(f"created {count} Unity metadata files")
