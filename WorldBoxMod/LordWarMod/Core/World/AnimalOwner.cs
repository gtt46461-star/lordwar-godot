using System;
using System.Collections.Generic;
namespace LordWar.World {
 public sealed class AnimalOwner {
  readonly Dictionary<string,AnimalHerd> _herds; readonly WorldMap _map; readonly DeterministicRandom _rng;
  public AnimalOwner(Dictionary<string,AnimalHerd> herds,WorldMap map,int seed){_herds=herds;_map=map;_rng=new DeterministicRandom(seed^0x62A17);}
  public void Seed(int target){if(_map==null||_herds.Count>0)return;string[] species={"鹿群","野猪","狼群","野牛","野马"};int attempts=0;while(_herds.Count<target&&attempts++<target*30){int x=_rng.Range(2,_map.Width-2),y=_rng.Range(2,_map.Height-2);LordWar.World.WorldTile t=_map.Get(x,y);if(t==null||(t.Terrain==TerrainKind.DeepWater||t.Terrain==TerrainKind.Lake)||t.Terrain==TerrainKind.Mountain)continue;string sp=species[_rng.Range(0,species.Length)];if(sp=="野马"&&(t.Terrain!=TerrainKind.Grass&&t.Terrain!=TerrainKind.Hill))continue;var h=new AnimalHerd{Id=Ids.Next("HERD"),Species=sp,X=x,Y=y,Count=_rng.Range(4,18)};_herds[h.Id]=h;}}
  bool Suitable(AnimalHerd h,LordWar.World.WorldTile t){if(t==null||(t.Terrain==TerrainKind.DeepWater||t.Terrain==TerrainKind.Lake)||t.Terrain==TerrainKind.Mountain)return false;if(h.Species=="野马")return t.Terrain==TerrainKind.Grass||t.Terrain==TerrainKind.Hill;return true;}
  public void TickDay(){foreach(var h in _herds.Values){if(h.Count<=0)continue;if(_rng.Chance(.35f)){var options=new List<LordWar.World.WorldTile>();foreach(var n in _map.Neighbors4(h.X,h.Y))if(Suitable(h,n))options.Add(n);if(options.Count>0){var n=options[_rng.Range(0,options.Count)];h.X=n.X;h.Y=n.Y;}}if(_rng.Chance(.06f))h.Count=Math.Min(30,h.Count+1);if(_rng.Chance(.015f))h.Count=Math.Max(0,h.Count-1);}}
  public int HuntNear(City c,int hunters){if(c==null||hunters<=0)return 0;AnimalHerd best=null;int dist=999;foreach(var h in _herds.Values){if(h.Count<=0||h.Species=="狼群")continue;int d=Mathx.Manhattan(c.X,c.Y,h.X,h.Y);if(d<=8&&d<dist){best=h;dist=d;}}if(best==null)return 0;int take=Math.Min(best.Count,Math.Max(1,hunters/3));best.Count-=take;int food=take*5;c.Food+=food;return food;}
  public int DomesticateHorses(City c,bool hasStable){if(c==null||!hasStable)return 0;AnimalHerd best=null;int dist=999;foreach(var h in _herds.Values){if(h.Count<=2||h.Species!="野马")continue;int d=Mathx.Manhattan(c.X,c.Y,h.X,h.Y);if(d<=10&&d<dist){best=h;dist=d;}}if(best==null)return 0;int take=Math.Min(2,Math.Max(1,best.Count/6));best.Count-=take;c.Horses+=take;return take;}
 }
}
