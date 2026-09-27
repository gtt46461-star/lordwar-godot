#if UNITY_EDITOR
using UnityEditor;
namespace LordWar.EditorBuild {
 public sealed class LordWarTextureImporter : AssetPostprocessor {
  void OnPreprocessTexture(){if(!assetPath.Contains("/Resources/LordWarArt/"))return;var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.filterMode=UnityEngine.FilterMode.Point;t.textureCompression=TextureImporterCompression.Uncompressed;t.mipmapEnabled=false;t.alphaIsTransparency=true;t.spritePixelsPerUnit=16f;t.wrapMode=UnityEngine.TextureWrapMode.Clamp;t.anisoLevel=0;t.isReadable=assetPath.Contains("/LordWarArt/N01/");if(assetPath.Contains("/LordWarArt/N01/")){var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);settings.spriteAlignment=9;settings.spritePivot=new UnityEngine.Vector2(.5f,0f);t.SetTextureSettings(settings);}}
 }
}
#endif
