using System;
using System.Collections.Generic;
namespace LordWar.Construction {
    public sealed class ConstructionOwner {
        readonly Dictionary<string,ConstructionProject> _projects;
        readonly Dictionary<string,Building> _buildings;
        readonly Dictionary<string,Person> _people;
        readonly Dictionary<string,City> _cities;
        public string LastCompletedBuildingId="";

        public ConstructionOwner(Dictionary<string,ConstructionProject> projects,Dictionary<string,Building> buildings,Dictionary<string,Person> people,Dictionary<string,City> cities){_projects=projects;_buildings=buildings;_people=people;_cities=cities;}

        public ConstructionProject Start(City city,Proposal proposal,BuildingKind kind,int x,int y,int baseSeconds){
            var p=new ConstructionProject{Id=Ids.Next("BUILD"),CityId=city.Id,ProposalId=proposal==null?"":proposal.Id,Kind=kind,Name=ChineseText.Building(kind),X=x,Y=y,RequiredWork=Math.Max(20,baseSeconds*10),GoldCommitted=proposal==null?0:proposal.CostGold};
            _projects[p.Id]=p;return p;
        }

        public ConstructionProject StartRepair(City city,Proposal proposal,Building building,int baseSeconds){
            if(city==null||building==null)return null;
            var p=new ConstructionProject{Id=Ids.Next("REPAIR"),CityId=city.Id,ProposalId=proposal==null?"":proposal.Id,Kind=building.Kind,Name="维修"+building.Name,X=building.X,Y=building.Y,RequiredWork=Math.Max(18,baseSeconds*10),GoldCommitted=proposal==null?0:proposal.CostGold,TargetBuildingId=building.Id,IsRepair=true};
            _projects[p.Id]=p;return p;
        }

        public void AssignWorkers(ConstructionProject p,City city,int desired){
            if(p==null)return;
            foreach(string id in city.PersonIds){
                if(p.WorkerIds.Count>=desired)break;
                Person person;
                if(!_people.TryGetValue(id,out person)||!person.Alive||person.Job==JobKind.Soldier||person.Class==SocialClass.Official||person.Class==SocialClass.General)continue;
                if(!p.WorkerIds.Contains(id)){person.Job=JobKind.Builder;p.WorkerIds.Add(id);}
            }
            SyncWorkerActions(p);
        }

        string WorkerActionFor(ConstructionProject p,int index){
            if(p.Stage==ConstructionStage.Survey)return "测量";
            if(p.Stage==ConstructionStage.Foundation)return index%2==0?"清地":"搬运";
            if(p.Stage==ConstructionStage.Frame)return index%3==0?"锯木":"搬运";
            if(p.Stage==ConstructionStage.Inspection)return "验收";
            if(p.Stage==ConstructionStage.Complete)return "完工";
            if(p.Kind==BuildingKind.Road||p.Kind==BuildingKind.Bridge)return "铺路";
            if(p.Kind==BuildingKind.Wall||p.Kind==BuildingKind.Gate||p.Kind==BuildingKind.Tower)return index%2==0?"砌筑":"敲打";
            return index%3==0?"搬运":"敲打";
        }

        void SyncWorkerActions(ConstructionProject p){
            if(p.WorkerActions==null)p.WorkerActions=new List<ConstructionWorkerAction>();
            for(int i=p.WorkerActions.Count-1;i>=0;i--)if(!p.WorkerIds.Contains(p.WorkerActions[i].WorkerId))p.WorkerActions.RemoveAt(i);
            for(int i=0;i<p.WorkerIds.Count;i++){
                string workerId=p.WorkerIds[i];ConstructionWorkerAction state=null;
                foreach(ConstructionWorkerAction x in p.WorkerActions)if(x.WorkerId==workerId){state=x;break;}
                if(state==null){state=new ConstructionWorkerAction{WorkerId=workerId};p.WorkerActions.Add(state);}
                state.Action=WorkerActionFor(p,i);state.Cycle=(state.Cycle+1)%8;
            }
        }

        public bool Tick(ConstructionProject p,float seconds){
            if(p==null||p.Stage==ConstructionStage.Complete)return false;
            int workers=Math.Max(1,p.WorkerIds.Count);float factor=workers==1?1f:(workers==2?1.75f:(workers<=4?2.7f:4f));
            p.WorkDone+=Math.Max(1,(int)(seconds*10f*factor));float q=p.WorkDone/(float)Math.Max(1,p.RequiredWork);
            if(q<.12f){p.Stage=ConstructionStage.Survey;p.CurrentAction="测量";}
            else if(q<.32f){p.Stage=ConstructionStage.Foundation;p.CurrentAction="清地、搬运";}
            else if(q<.55f){p.Stage=ConstructionStage.Frame;p.CurrentAction="搬运、锯木";}
            else if(q<.9f){p.Stage=ConstructionStage.MainWork;p.CurrentAction=(p.Kind==BuildingKind.Road||p.Kind==BuildingKind.Bridge)?"铺路":((p.Kind==BuildingKind.Wall||p.Kind==BuildingKind.Gate||p.Kind==BuildingKind.Tower)?"砌筑、敲打":"搬运、敲打");}
            else {p.Stage=ConstructionStage.Inspection;p.CurrentAction="完工验收";}
            SyncWorkerActions(p);
            if(p.WorkDone<p.RequiredWork)return false;
            p.Stage=ConstructionStage.Complete;p.CurrentAction="完工验收";SyncWorkerActions(p);
            if(p.IsRepair&&!string.IsNullOrEmpty(p.TargetBuildingId)){
                Building repaired;if(_buildings.TryGetValue(p.TargetBuildingId,out repaired)){repaired.Durability=repaired.MaxDurability;repaired.Ruined=false;repaired.Complete=true;LastCompletedBuildingId=repaired.Id;return true;}
            }
            City city;
            if(_cities.TryGetValue(p.CityId,out city)&&IsUpgradable(p.Kind)){
                foreach(string id in city.BuildingIds){Building existing;if(_buildings.TryGetValue(id,out existing)&&existing.Kind==p.Kind&&existing.Complete){existing.Level=Math.Min(5,existing.Level+1);existing.Durability=existing.MaxDurability;existing.Name=ChineseText.Building(p.Kind)+"（"+existing.Level+"级）";LastCompletedBuildingId=existing.Id;return true;}}
            }
            var b=new Building{Id=Ids.Next("B"),CityId=p.CityId,Name=p.Name,Kind=p.Kind,X=p.X,Y=p.Y,Durability=100,MaxDurability=100,Complete=true};
            _buildings[b.Id]=b;LastCompletedBuildingId=b.Id;if(_cities.TryGetValue(b.CityId,out city)&&!city.BuildingIds.Contains(b.Id))city.BuildingIds.Add(b.Id);return true;
        }

        bool IsUpgradable(BuildingKind k){return k==BuildingKind.Market||k==BuildingKind.Granary||k==BuildingKind.Barracks||k==BuildingKind.Stable||k==BuildingKind.Workshop||k==BuildingKind.Smithy||k==BuildingKind.Armory||k==BuildingKind.TrainingGround||k==BuildingKind.Government;}
        public void WeatherWear(Building b,WeatherKind weather,float durabilityResistance=1f){if(b==null)return;int raw=(weather==WeatherKind.Storm||weather==WeatherKind.Blizzard)?2:0;if(b.Kind==BuildingKind.Road&&(weather==WeatherKind.Rain||weather==WeatherKind.Snow))raw++;float resistance=Mathx.Clamp(durabilityResistance,.7f,1.8f);int loss=raw<=0?0:Math.Max(1,(int)Math.Ceiling(raw/resistance));b.Durability=Math.Max(0,b.Durability-loss);if(b.Durability<=0){b.Ruined=true;b.Complete=false;}}
    }
}
