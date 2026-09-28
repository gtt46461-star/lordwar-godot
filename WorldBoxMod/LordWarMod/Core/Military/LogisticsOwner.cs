using System;
using System.Collections.Generic;
using LordWar.World;
namespace LordWar.Military {
    /// <summary>唯一后勤Owner：真实补给路线、运输队、损耗、延误、粮草与药品交付。</summary>
    public sealed class LogisticsOwner {
        readonly Dictionary<string,SupplyRoute> _routes;
        readonly Dictionary<string,SupplyConvoy> _convoys;
        readonly WorldGenerator _pathfinder;
        readonly WorldMap _map;
        readonly DeterministicRandom _rng;
        public LogisticsOwner(Dictionary<string,SupplyRoute> routes,Dictionary<string,SupplyConvoy> convoys,WorldGenerator pathfinder,WorldMap map,int seed){_routes=routes;_convoys=convoys;_pathfinder=pathfinder;_map=map;_rng=new DeterministicRandom(seed^0x554411);}
        public IEnumerable<SupplyRoute> Routes{get{return _routes.Values;}}
        public IEnumerable<SupplyConvoy> Convoys{get{return _convoys.Values;}}
        public void Restore(IEnumerable<SupplyRoute> routes,IEnumerable<SupplyConvoy> convoys){_routes.Clear();_convoys.Clear();if(routes!=null)foreach(SupplyRoute r in routes)if(r!=null&&!string.IsNullOrEmpty(r.Id))_routes[r.Id]=r;if(convoys!=null)foreach(SupplyConvoy c in convoys)if(c!=null&&!string.IsNullOrEmpty(c.Id))_convoys[c.Id]=c;}
        public SupplyRoute RouteForArmy(string armyId){foreach(SupplyRoute r in _routes.Values)if(r.ArmyId==armyId&&r.Active)return r;return null;}
        public int PruneInvalid(Dictionary<string,Army> armies,Dictionary<string,City> cities){int removed=0;var deadConvoys=new List<string>();foreach(SupplyConvoy c in _convoys.Values){SupplyRoute r;if(c==null||!c.Active||!armies.ContainsKey(c.ArmyId)||!cities.ContainsKey(c.SourceCityId)||!_routes.TryGetValue(c.RouteId,out r)||r==null||!r.Active)deadConvoys.Add(c==null?null:c.Id);}foreach(string id in deadConvoys)if(!string.IsNullOrEmpty(id)&&_convoys.Remove(id))removed++;var deadRoutes=new List<string>();foreach(SupplyRoute r in _routes.Values)if(r==null||!r.Active||!armies.ContainsKey(r.ArmyId)||!cities.ContainsKey(r.SourceCityId))deadRoutes.Add(r==null?null:r.Id);foreach(string id in deadRoutes)if(!string.IsNullOrEmpty(id)&&_routes.Remove(id))removed++;return removed;}
        public SupplyRoute EnsureRoute(Army army,City source,int day){if(army==null||source==null||_map==null)return null;SupplyRoute r=RouteForArmy(army.Id);bool repath=r==null||r.SourceCityId!=source.Id||day-r.LastRepathDay>=5||r.Path==null||r.Path.Count==0;if(!repath)return r;List<GridPoint> path=_pathfinder.FindPath(_map,new GridPoint(source.X,source.Y),new GridPoint(army.X,army.Y),false);if(path.Count==0&&Mathx.Manhattan(source.X,source.Y,army.X,army.Y)>1){if(r!=null)r.Intact=false;return r;}if(r==null){r=new SupplyRoute{Id=Ids.Next("SUPR"),ArmyId=army.Id,KingdomId=army.KingdomId};_routes[r.Id]=r;}r.SourceCityId=source.Id;r.Path=path;r.LastRepathDay=day;r.Intact=true;r.Active=true;r.Capacity=RouteCapacity(path);r.Risk=RouteRisk(path);return r;}
        int RouteCapacity(List<GridPoint> path){if(path==null||path.Count==0)return 1;int sum=0;foreach(GridPoint p in path){LordWar.World.WorldTile t=_map.Get(p.X,p.Y);sum+=t!=null&&t.Road?Math.Max(1,t.RoadCapacity):1;}return Math.Max(1,sum/path.Count);}
        float RouteRisk(List<GridPoint> path){if(path==null||path.Count==0)return .1f;float risk=0;foreach(GridPoint p in path){LordWar.World.WorldTile t=_map.Get(p.X,p.Y);if(t==null)continue;if(t.Terrain==TerrainKind.Mountain||t.Terrain==TerrainKind.Marsh)risk+=.035f;if(t.River&&!t.Bridge)risk+=.025f;if(!t.Road)risk+=.008f;}return Mathx.Clamp(risk/Math.Max(1,path.Count)*6f,.02f,.45f);}
        public SupplyConvoy Dispatch(SupplyRoute route,Army army,City city,int soldierCount,int officialLogistics,int day){if(route==null||army==null||city==null||!route.Active||!route.Intact||soldierCount<=0)return null;int active=0;foreach(SupplyConvoy x in _convoys.Values)if(x.Active&&x.RouteId==route.Id)active++;if(active>=route.Capacity)return null;int foodNeed=Math.Max(4,soldierCount/5);int medNeed=Math.Max(0,soldierCount/40);int food=Math.Min(city.Food,foodNeed);if(food<=0)return null;city.Food-=food;int medicine=Math.Min(Math.Max(0,city.Iron/20),medNeed);var c=new SupplyConvoy{Id=Ids.Next("SUPC"),RouteId=route.Id,SourceCityId=city.Id,ArmyId=army.Id,FoodCargo=food,MedicineCargo=medicine,Escorts=Math.Max(2,soldierCount/50),DepartDay=day};_convoys[c.Id]=c;return c;}
        public void TickDay(Dictionary<string,Army> armies,Dictionary<string,City> cities,Dictionary<string,int> logisticsByKingdom,WeatherKind weather){List<string> completed=new List<string>();foreach(SupplyConvoy c in new List<SupplyConvoy>(_convoys.Values)){if(c==null||!c.Active)continue;SupplyRoute route;Army army;if(!_routes.TryGetValue(c.RouteId,out route)||!route.Active||!route.Intact||!armies.TryGetValue(c.ArmyId,out army)){c.Active=false;completed.Add(c.Id);continue;}int officialLogistics=20;logisticsByKingdom.TryGetValue(army.KingdomId,out officialLogistics);int steps=2+Mathx.Clamp(officialLogistics,0,100)/20;float weatherDelay=(weather==WeatherKind.Storm||weather==WeatherKind.Blizzard) ? .35f:(weather==WeatherKind.Rain||weather==WeatherKind.Snow||weather==WeatherKind.Fog ? .15f:0f);if(_rng.Next01()<weatherDelay+route.Risk*.25f){c.DelayedDays++;continue;}c.PathIndex=Math.Min(Math.Max(0,route.Path.Count-1),c.PathIndex+steps);if(c.PathIndex<route.Path.Count-1)continue;float efficiency=.68f+Mathx.Clamp(officialLogistics,0,100)*.0032f-Math.Min(.2f,route.Risk*.25f);int delivered=Math.Max(0,(int)Math.Round(c.FoodCargo*Mathx.Clamp(efficiency,.45f,.98f)));int soldiers=0;foreach(Squad s in army.Squads)soldiers+=s.SoldierIds.Count;float foodDays=delivered/(float)Math.Max(1,soldiers/8);army.FoodDays=Math.Min(12f,army.FoodDays+foodDays);army.MedicalSupplies+=c.MedicineCargo;c.Active=false;completed.Add(c.Id);}foreach(string id in completed)_convoys.Remove(id);}
        public int LoadFood(Army army,City city,int days,int soldierCount,int officialLogistics,float commanderSupplyEfficiency){
            if(army==null||city==null||soldierCount<=0)return 0;
            int need=Math.Max(1,soldierCount*days/8);int moved=Math.Min(city.Food,need);
            float command=Mathx.Clamp(commanderSupplyEfficiency,.65f,1.8f);
            float efficiency=(.65f+Mathx.Clamp(officialLogistics,0,100)*.004f)*Mathx.Clamp(.82f+.18f*command,.75f,1.12f);
            int delivered=(int)(moved*Mathx.Clamp(efficiency,.40f,.99f));city.Food-=moved;
            army.FoodDays=Math.Min(12f,army.FoodDays+days*(delivered/(float)Math.Max(1,need)));return delivered;
        }
        public int LoadFood(Army army,City city,int days,int soldierCount,int officialLogistics){return LoadFood(army,city,days,soldierCount,officialLogistics,1f);}
        public float DailySupplyTick(Army a,int soldiers,bool routeIntact,int officialLogistics,float commanderSupplyEfficiency){
            if(a==null)return 0;float command=Mathx.Clamp(commanderSupplyEfficiency,.65f,1.8f);
            float consumption=Math.Max(.1f,soldiers/80f)/Mathx.Clamp(.82f+.18f*command,.75f,1.12f);
            a.FoodDays=Math.Max(0,a.FoodDays-consumption);
            if(a.FoodDays<=0){float failure=routeIntact?3f:7f;failure/=Mathx.Clamp(.8f+.2f*command,.75f,1.15f);a.Morale=Math.Max(0,a.Morale-failure);a.Fatigue=Math.Min(100,a.Fatigue+4f/Mathx.Clamp(command,.8f,1.5f));}
            return a.FoodDays;
        }
        public float DailySupplyTick(Army a,int soldiers,bool routeIntact,int officialLogistics){return DailySupplyTick(a,soldiers,routeIntact,officialLogistics,1f);}
    }
}
