using System;
using System.Collections.Generic;
using LordWar.World;

namespace LordWar.Construction {
    /// <summary>唯一城市规划Owner：领主划区、官员/居民按区域选址，避免住宅商铺随机散点。</summary>
    public sealed class CityPlanningOwner {
        readonly WorldMap _map;
        readonly Dictionary<string,Building> _buildings;
        readonly DeterministicRandom _rng;
        int _siteCursor;

        public CityPlanningOwner(WorldMap map,Dictionary<string,Building> buildings,int seed){_map=map;_buildings=buildings;_rng=new DeterministicRandom(seed^0x52A71);}

        public void EnsureStarterZones(City city,int day){
            if(city==null)return;if(city.Zones==null)city.Zones=new List<CityZone>();if(city.Zones.Count>0)return;
            AddSuggested(city,ZoneKind.Residential,false,day);AddSuggested(city,ZoneKind.Commercial,false,day);AddSuggested(city,ZoneKind.Workshop,false,day);AddSuggested(city,ZoneKind.Agriculture,false,day);AddSuggested(city,ZoneKind.Military,false,day);
        }

        public CityZone AddSuggested(City city,ZoneKind kind,bool playerPlanned,int day){
            if(city==null)return null;int ordinal=city.Zones==null?0:city.Zones.Count;int ring=5+(ordinal/8)*4;int sector=ordinal%8;int[] dx={1,1,0,-1,-1,-1,0,1};int[] dy={0,1,1,1,0,-1,-1,-1};
            int x=city.X+dx[sector]*ring,y=city.Y+dy[sector]*ring;GridPoint pt=NearestBuildable(x,y,city.X,city.Y,6);return DesignateZone(city,kind,pt.X,pt.Y,3,playerPlanned,day);
        }

        public CityZone DesignateZone(City city,ZoneKind kind,int x,int y,int radius,bool playerPlanned,int day){
            if(city==null||_map==null)return null;if(city.Zones==null)city.Zones=new List<CityZone>();radius=Mathx.Clamp(radius,2,6);GridPoint pt=NearestBuildable(x,y,city.X,city.Y,5);LordWar.World.WorldTile tile=_map.Get(pt.X,pt.Y);if(tile==null||!Buildable(tile))return null;
            foreach(CityZone old in city.Zones){if(old==null)continue;int dist=Mathx.Manhattan(old.CenterX,old.CenterY,pt.X,pt.Y);if(dist<Math.Max(2,Math.Min(old.Radius,radius)))return null;}
            CityZone z=new CityZone{Id=Ids.Next("ZONE"),CityId=city.Id,Kind=kind,Name=ChineseText.Zone(kind),CenterX=pt.X,CenterY=pt.Y,Radius=radius,PlayerPlanned=playerPlanned,PlannedDay=day};city.Zones.Add(z);Recount(city);return z;
        }

        public GridPoint FindBuildSite(City city,BuildingKind kind){
            if(city==null)return new GridPoint(0,0);EnsureStarterZones(city,0);ZoneKind desired=ZoneFor(kind);var zones=new List<CityZone>();foreach(CityZone z in city.Zones)if(z!=null&&z.Kind==desired)zones.Add(z);if(zones.Count==0)foreach(CityZone z in city.Zones)if(z!=null)zones.Add(z);
            if(zones.Count>0){int start=Math.Abs(_siteCursor++)%zones.Count;for(int zi=0;zi<zones.Count;zi++){CityZone z=zones[(start+zi)%zones.Count];GridPoint site=FindFreeInZone(city,z);if(site.X>=0)return site;}}
            GridPoint fallback=NearestBuildable(city.X+_rng.Range(-6,7),city.Y+_rng.Range(-6,7),city.X,city.Y,8);return fallback;
        }

        public void NotifyBuildingCompleted(City city,Building building){if(city==null||building==null)return;Recount(city);}
        public void Recount(City city){if(city==null||city.Zones==null)return;foreach(CityZone z in city.Zones)if(z!=null)z.BuildingCount=0;foreach(string id in city.BuildingIds){Building b;if(!_buildings.TryGetValue(id,out b)||b==null)continue;CityZone z=ZoneAt(city,b.X,b.Y);if(z!=null)z.BuildingCount++;}}
        public CityZone ZoneAt(City city,int x,int y){if(city==null||city.Zones==null)return null;CityZone best=null;int bestDist=int.MaxValue;foreach(CityZone z in city.Zones){if(z==null)continue;int dx=Math.Abs(x-z.CenterX),dy=Math.Abs(y-z.CenterY);if(dx>z.Radius||dy>z.Radius)continue;int d=dx+dy;if(d<bestDist){bestDist=d;best=z;}}return best;}
        public static ZoneKind ZoneFor(BuildingKind kind){switch(kind){case BuildingKind.House:return ZoneKind.Residential;case BuildingKind.Shop:case BuildingKind.Market:return ZoneKind.Commercial;case BuildingKind.Workshop:case BuildingKind.Smithy:case BuildingKind.Armory:return ZoneKind.Workshop;case BuildingKind.Granary:case BuildingKind.Stable:return ZoneKind.Agriculture;case BuildingKind.Barracks:case BuildingKind.TrainingGround:return ZoneKind.Military;default:return ZoneKind.Civic;}}

        GridPoint FindFreeInZone(City city,CityZone z){int span=z.Radius*2+1,total=span*span,start=Math.Abs(_siteCursor*17+z.Id.GetHashCode())%Math.Max(1,total);for(int i=0;i<total;i++){int n=(start+i)%total;int ox=n%span-z.Radius,oy=n/span-z.Radius;int x=z.CenterX+ox,y=z.CenterY+oy;LordWar.World.WorldTile t=_map.Get(x,y);if(t==null||!Buildable(t)||Occupied(city,x,y))continue;return new GridPoint(x,y);}return new GridPoint(-1,-1);}
        bool Occupied(City city,int x,int y){foreach(string id in city.BuildingIds){Building b;if(_buildings.TryGetValue(id,out b)&&b!=null&&b.X==x&&b.Y==y&&!b.Ruined)return true;}return false;}
        bool Buildable(LordWar.World.WorldTile t){return t!=null&&t.Terrain!=TerrainKind.DeepWater&&t.Terrain!=TerrainKind.Lake&&t.Terrain!=TerrainKind.Mountain&&t.Terrain!=TerrainKind.River;}
        GridPoint NearestBuildable(int x,int y,int fallbackX,int fallbackY,int maxRadius){LordWar.World.WorldTile direct=_map.Get(x,y);if(Buildable(direct))return new GridPoint(x,y);for(int r=1;r<=maxRadius;r++)for(int oy=-r;oy<=r;oy++)for(int ox=-r;ox<=r;ox++){if(Math.Abs(ox)!=r&&Math.Abs(oy)!=r)continue;LordWar.World.WorldTile t=_map.Get(x+ox,y+oy);if(Buildable(t))return new GridPoint(t.X,t.Y);}LordWar.World.WorldTile fallback=_map.Get(fallbackX,fallbackY);return fallback==null?new GridPoint(Mathx.Clamp(x,0,_map.Width-1),Mathx.Clamp(y,0,_map.Height-1)):new GridPoint(fallback.X,fallback.Y);}
    }
}
