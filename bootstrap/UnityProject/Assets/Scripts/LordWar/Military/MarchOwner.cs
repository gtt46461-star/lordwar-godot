using System;
using LordWar.World;

namespace LordWar.Military {
    public sealed class MarchOwner {
        readonly WorldGenerator _pathfinder;
        readonly WorldMap _map;

        public MarchOwner(WorldGenerator pathfinder,WorldMap map){_pathfinder=pathfinder;_map=map;}

        public bool SetDestination(Army a,int x,int y){return SetDestination(a,x,y,true);}
        public bool SetDestination(Army a,int x,int y,bool beginMarch){
            if(a==null||_map.Get(x,y)==null)return false;
            var route=_pathfinder.FindPath(_map,new GridPoint(a.X,a.Y),new GridPoint(x,y),false);
            if(route.Count==0)return false;
            a.TargetX=x;a.TargetY=y;a.StrategicRoute=route;a.RouteIndex=0;a.MarchProgress=0f;
            if(beginMarch)a.Order=ArmyOrder.March;else{a.Order=ArmyOrder.Muster;a.MusterProgress=0f;}
            return true;
        }

        public bool Step(Army a,float hours,bool forcedMarch,float weatherPenalty,int roadOccupancy,float terrainAdapt,float fatigueEfficiency,float fatigueCost,WeatherKind weather,float bridgeSpeed=1f){
            if(a==null||a.Order!=ArmyOrder.March||a.StrategicRoute==null||a.StrategicRoute.Count==0)return false;
            float budget=Math.Max(0f,a.MarchProgress)+hours;bool moved=false;
            terrainAdapt=Mathx.Clamp(terrainAdapt,.8f,1.8f);
            fatigueEfficiency=Mathx.Clamp(fatigueEfficiency,.65f,1.8f);
            fatigueCost=Mathx.Clamp(fatigueCost,1f,1.5f);
            while(a.RouteIndex<a.StrategicRoute.Count-1&&budget>0){
                var nxt=a.StrategicRoute[a.RouteIndex+1];
                var tile=_map.Get(nxt.X,nxt.Y);bool road=tile.Road;
                float speed=MarchFormationSystem.TerrainSpeed(road?TerrainKind.Road:tile.Terrain);
                bool difficult=!road&&(tile.Terrain==TerrainKind.Forest||tile.Terrain==TerrainKind.Hill||tile.Terrain==TerrainKind.Mountain||tile.Terrain==TerrainKind.MountainPass||tile.Terrain==TerrainKind.Marsh||tile.Terrain==TerrainKind.River||tile.Terrain==TerrainKind.Snow||tile.Terrain==TerrainKind.Desert);
                if(difficult)speed*=Mathx.Clamp(.9f+.1f*terrainAdapt,.88f,1.08f);
                if(tile.River&&tile.Ford&&!tile.Bridge&&!road)speed*=1.30f;
                if(road){float condition=tile.RoadCondition<=0f?1f:Mathx.Clamp(tile.RoadCondition,.25f,1f);speed*=(1f+Math.Max(0,tile.RoadLevel-1)*.12f)*(.68f+.32f*condition);if(tile.Bridge)speed*=Mathx.Clamp(bridgeSpeed,.75f,1.55f);}
                float localWeatherPenalty=MarchFormationSystem.WeatherTerrainPenalty(weather,tile.Terrain,road,tile.RoadLevel);
                float surfacePenalty=Mathx.Clamp(tile.SurfaceMud*.22f+tile.SnowDepth*.30f,0f,.42f);if(road)surfacePenalty*=tile.RoadLevel>=3 ? .28f:(tile.RoadLevel==2 ? .52f:.78f);
                speed*=Mathx.Clamp(1f-weatherPenalty-localWeatherPenalty-surfacePenalty,.20f,1f);
                if(speed<=0)break;
                int capacity=road?Math.Max(1,tile.RoadCapacity):1;
                float congestion=road?1f+Math.Max(0,roadOccupancy-capacity)*.18f:1f;
                float cost=congestion/speed;
                if(cost>budget)break;
                budget-=cost;a.X=nxt.X;a.Y=nxt.Y;a.RouteIndex++;
                float combinedWeather=Mathx.Clamp(weatherPenalty+localWeatherPenalty+surfacePenalty,0f,.9f);
                float fatigue=MarchFormationSystem.FatiguePerTile(tile.Terrain,combinedWeather,forcedMarch)*congestion;if(tile.River&&tile.Ford&&!tile.Bridge)fatigue*=.78f;
                if(difficult)fatigue/=Mathx.Clamp(terrainAdapt,.85f,1.6f);
                fatigue*=fatigueCost/Math.Max(.65f,fatigueEfficiency);
                a.Fatigue=Mathx.Clamp(a.Fatigue+fatigue,0,100);
                a.FoodDays=Math.Max(0,a.FoodDays-MarchFormationSystem.FoodUsePerTile(tile.Terrain,combinedWeather,forcedMarch,congestion,terrainAdapt));
                moved=true;
            }
            a.MarchProgress=a.RouteIndex>=a.StrategicRoute.Count-1?0f:Math.Min(16f,budget);
            if(a.RouteIndex>=a.StrategicRoute.Count-1)a.Order=ArmyOrder.Hold;
            return moved;
        }

        public bool Step(Army a,float hours,bool forcedMarch,float weatherPenalty,int roadOccupancy,float terrainAdapt,float fatigueEfficiency,float fatigueCost){return Step(a,hours,forcedMarch,weatherPenalty,roadOccupancy,terrainAdapt,fatigueEfficiency,fatigueCost,WeatherKind.Clear);}
        public bool Step(Army a,float hours,bool forcedMarch,float weatherPenalty,int roadOccupancy,float terrainAdapt){return Step(a,hours,forcedMarch,weatherPenalty,roadOccupancy,terrainAdapt,1f,1f,WeatherKind.Clear);}
        public bool Step(Army a,float hours,bool forcedMarch,float weatherPenalty,int roadOccupancy){return Step(a,hours,forcedMarch,weatherPenalty,roadOccupancy,1f,1f,1f,WeatherKind.Clear);}
        public bool Step(Army a,float hours,bool forcedMarch,float weatherPenalty){return Step(a,hours,forcedMarch,weatherPenalty,1,1f,1f,1f,WeatherKind.Clear);}
    }
}
