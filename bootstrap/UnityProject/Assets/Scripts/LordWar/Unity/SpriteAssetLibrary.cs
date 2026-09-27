#if UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
namespace LordWar.UnityRuntime {
 public sealed class SpriteAssetLibrary {
  readonly List<Sprite> _sprites=new List<Sprite>();
  readonly List<Texture2D> _textures=new List<Texture2D>();
  readonly Dictionary<string,Sprite> _textureSprites=new Dictionary<string,Sprite>();
  readonly Dictionary<string,Sprite[]> _stripFrames=new Dictionary<string,Sprite[]>();
  public SpriteAssetLibrary(){_sprites.AddRange(Resources.LoadAll<Sprite>("LordWarArt"));_textures.AddRange(Resources.LoadAll<Texture2D>("LordWarArt"));}
  public int Count{get{return Math.Max(_sprites.Count,_textures.Count);}}
  public int ImportedSpriteCount{get{return _sprites.Count;}}
  public int ImportedTextureCount{get{return _textures.Count;}}
  public Sprite FindExact(string assetId){
   if(string.IsNullOrEmpty(assetId))return null;
   return Resources.Load<Sprite>("LordWarArt/"+assetId);
  }
  public Sprite[] FindExactFrames(string assetId,int count,int pixelsPerUnit){
   if(string.IsNullOrEmpty(assetId)||count<=0)return new Sprite[0];
   Sprite[] cached;if(_stripFrames.TryGetValue(assetId,out cached))return cached;
   Texture2D tex=Resources.Load<Texture2D>("LordWarArt/"+assetId);
   if(tex==null||tex.width%count!=0)return new Sprite[0];
   int width=tex.width/count;cached=new Sprite[count];
   for(int i=0;i<count;i++)cached[i]=Sprite.Create(tex,new Rect(i*width,0,width,tex.height),new Vector2(.5f,0f),pixelsPerUnit,0,SpriteMeshType.FullRect);
   _stripFrames[assetId]=cached;return cached;
  }

  public Sprite FindContains(params string[] keys){
   Sprite found=FindSpriteByPriority(keys);if(found!=null)return found;
   Texture2D tex=FindTextureByPriority(keys);return SpriteFromTexture(tex);
  }

  public Sprite FindVariant(int seed,params string[] keys){
   var matches=new List<Sprite>();
   for(int ki=0;ki<keys.Length;ki++){string key=(keys[ki]??"").ToLowerInvariant();if(key.Length==0)continue;for(int i=0;i<_sprites.Count;i++){Sprite s=_sprites[i];if(s!=null&&s.name.ToLowerInvariant().Contains(key)&&!matches.Contains(s))matches.Add(s);}if(matches.Count>0)break;}
   if(matches.Count>0)return matches[Positive(seed)%matches.Count];
   var texMatches=new List<Texture2D>();
   for(int ki=0;ki<keys.Length;ki++){string key=(keys[ki]??"").ToLowerInvariant();if(key.Length==0)continue;for(int i=0;i<_textures.Count;i++){Texture2D t=_textures[i];if(t!=null&&t.name.ToLowerInvariant().Contains(key)&&!texMatches.Contains(t))texMatches.Add(t);}if(texMatches.Count>0)break;}
   return texMatches.Count==0?FindContains(keys):SpriteFromTexture(texMatches[Positive(seed)%texMatches.Count]);
  }

  /// <summary>读取总包中“行走4帧/攻击4帧”的真实横向像素条；不复制纹理，只创建四个Sprite视窗。</summary>
  public Sprite[] FindStripFrames(int seed,string required,params string[] keys){
   required=required??"";var matches=new List<Texture2D>();
   for(int i=0;i<_textures.Count;i++){Texture2D t=_textures[i];if(t==null||t.width<4||t.width%4!=0)continue;string n=t.name.ToLowerInvariant();if(required.Length>0&&!n.Contains(required.ToLowerInvariant()))continue;bool any=keys==null||keys.Length==0;for(int k=0;!any&&k<keys.Length;k++)if(!string.IsNullOrEmpty(keys[k])&&n.Contains(keys[k].ToLowerInvariant()))any=true;if(any)matches.Add(t);}
   if(matches.Count==0)return new Sprite[0];Texture2D chosen=matches[Positive(seed)%matches.Count];Sprite[] cached;if(_stripFrames.TryGetValue(chosen.name,out cached))return cached;
   int fw=chosen.width/4;cached=new Sprite[4];for(int i=0;i<4;i++)cached[i]=Sprite.Create(chosen,new Rect(i*fw,0,fw,chosen.height),new Vector2(.5f,.5f),16f,0,SpriteMeshType.FullRect);_stripFrames[chosen.name]=cached;return cached;
  }

  Sprite FindSpriteByPriority(string[] keys){if(keys!=null)for(int k=0;k<keys.Length;k++){string key=(keys[k]??"").ToLowerInvariant();if(key.Length==0)continue;for(int i=0;i<_sprites.Count;i++){Sprite s=_sprites[i];if(s!=null&&s.name.ToLowerInvariant().Contains(key))return s;}}return null;}
  Texture2D FindTextureByPriority(string[] keys){if(keys!=null)for(int k=0;k<keys.Length;k++){string key=(keys[k]??"").ToLowerInvariant();if(key.Length==0)continue;for(int i=0;i<_textures.Count;i++){Texture2D t=_textures[i];if(t!=null&&t.name.ToLowerInvariant().Contains(key))return t;}}return null;}
  Sprite SpriteFromTexture(Texture2D tex){if(tex==null)return null;Sprite s;if(_textureSprites.TryGetValue(tex.name,out s))return s;float ppu=16f;s=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),ppu,0,SpriteMeshType.FullRect);_textureSprites[tex.name]=s;return s;}
  static int Positive(int n){if(n==int.MinValue)return int.MaxValue;return Math.Abs(n);}
  public static int StableHash(string value){unchecked{int h=23;if(value!=null)for(int i=0;i<value.Length;i++)h=h*31+value[i];return h;}}
 }
}
#endif
