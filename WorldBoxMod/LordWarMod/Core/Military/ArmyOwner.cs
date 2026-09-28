using System;
using System.Collections.Generic;
using LordWar.Society;
using LordWar.Data;
namespace LordWar.Military {
    public sealed class ArmyOwner {
        readonly Dictionary<string,Army> _armies; readonly Dictionary<string,Person> _people; readonly PopulationOwner _population; readonly GameDataCatalog _data;
        public ArmyOwner(Dictionary<string,Army> armies,Dictionary<string,Person> people,PopulationOwner population,GameDataCatalog data){_armies=armies;_people=people;_population=population;_data=data;}
        public bool CanBeGeneral(Person p,bool barracksSeatAvailable){return p!=null&&p.Alive&&p.Noble&&barracksSeatAvailable&&(p.Class==SocialClass.Noble||p.Class==SocialClass.Officer||p.Class==SocialClass.General);}
        public Army CreateArmy(Kingdom k,City home,Person general,string name){if(k==null||home==null||general==null||!general.Noble)return null;var a=new Army{Id=Ids.Next("ARMY"),Name=name,KingdomId=k.Id,GeneralId=general.Id,X=home.X,Y=home.Y};a.Finance.GeneralId=general.Id;_armies[a.Id]=a;k.ArmyIds.Add(a.Id);home.GarrisonArmyIds.Add(a.Id);general.Class=SocialClass.General;general.Job=JobKind.Officer;return a;}
        string UnitName(string id){UnitDef u;if(_data!=null&&_data.Units.TryGetValue(id,out u))return u.Name;SpecialUnitDef s;if(_data!=null&&_data.SpecialUnits.TryGetValue(id,out s))return s.Name;return "军伍";}

        public int Recruit(Army a,City city,string unitTemplateId,int targetCount,int fallbackPerSoldierCost){
            if(a==null||city==null||targetCount<=0)return 0;
            UnitDef basic=null; SpecialUnitDef special=null;
            if(_data!=null){_data.Units.TryGetValue(unitTemplateId,out basic);_data.SpecialUnits.TryGetValue(unitTemplateId,out special);}
            int horsePerSoldier=basic!=null?Math.Max(0,basic.HorseCost):(IsCavalry(unitTemplateId)?1:0);
            int perSoldierCost=basic!=null&&basic.RecruitCost>0?basic.RecruitCost:Math.Max(1,fallbackPerSoldierCost);
            float monthlyWage=basic!=null&&basic.MonthlyWage>0?basic.MonthlyWage:Math.Max(1f,2f*(special==null?1f:Math.Max(1f,special.Upkeep)));
            float dailyFood=basic!=null&&basic.DailyFood>0?basic.DailyFood:Math.Max(1f,special==null?1f:special.Upkeep);
            if(horsePerSoldier>0)targetCount=Math.Min(targetCount,city.Horses/horsePerSoldier);
            int squadCap=basic!=null&&basic.SquadCap>0?basic.SquadCap:targetCount;
            targetCount=Math.Min(targetCount,Math.Max(1,squadCap));
            Squad squad=new Squad{Id=Ids.Next("SQ"),ArmyId=a.Id,UnitTemplateId=unitTemplateId,Name=UnitName(unitTemplateId)};
            if(basic!=null){squad.Morale=Mathx.Clamp(basic.BaseMorale<=0?70:basic.BaseMorale,20,100);squad.TrainingRemainingDays=Math.Max(0,basic.TrainingDays);squad.FormationName=string.IsNullOrEmpty(basic.Formation)?"横队":basic.Formation;}
            else if(special!=null){SpecialUnitBehaviorProfile sp=SpecialUnitBehaviorEngine.Build(unitTemplateId,_data);squad.Morale=Mathx.Clamp(72+(((special.Category??"").IndexOf("精锐",StringComparison.Ordinal)>=0)?8:0),20,100);squad.TrainingRemainingDays=SpecialUnitBehaviorEngine.TrainingDays(special,sp);squad.FormationName=SpecialFormation(special); }
            int n=0;
            for(int i=0;i<targetCount;i++){
                if(a.Finance.Gold<perSoldierCost)break;
                if(horsePerSoldier>0&&city.Horses<horsePerSoldier)break;
                Person p=_population.RecruitOne(city);if(p==null)break;
                a.Finance.Gold-=perSoldierCost;
                if(horsePerSoldier>0){city.Horses-=horsePerSoldier;a.Finance.HorseCost+=horsePerSoldier*2;}
                ApplyUnitBaseline(p,basic);if(special!=null)ApplySpecialBaseline(p,special);
                squad.SoldierIds.Add(p.Id);p.Battles=0;n++;
            }
            if(n>0){a.Squads.Add(squad);a.Finance.MonthlyWages+=Math.Max(1,(int)Math.Round(n*monthlyWage));a.Finance.MonthlyFoodCost+=Math.Max(1,(int)Math.Round(n*dailyFood*30f));}
            return n;
        }

        static void ApplyUnitBaseline(Person p,UnitDef u){
            if(p==null||u==null)return;
            if(u.Life>0)p.Stats.Life=Math.Max(p.Stats.Life,u.Life);
            if(u.Attack>0)p.Stats.Martial=Math.Max(p.Stats.Martial,20+u.Attack*2);
            if(u.Defense>0)p.Stats.Defense=Math.Max(p.Stats.Defense,18+u.Defense*2);
            if(u.Stamina>0)p.Stats.Stamina=Math.Max(p.Stats.Stamina,u.Stamina);
        }
        static void ApplySpecialBaseline(Person p,SpecialUnitDef u){if(p==null||u==null)return;string t=(u.Category??"")+" "+(u.Role??"");int martial=34,defense=30,stamina=75;if(t.IndexOf("精锐",StringComparison.Ordinal)>=0){martial+=10;defense+=10;stamina+=10;}if(t.IndexOf("骑",StringComparison.Ordinal)>=0){martial+=7;stamina+=9;}if(t.IndexOf("远程",StringComparison.Ordinal)>=0){martial+=6;defense-=3;}if(t.IndexOf("守城",StringComparison.Ordinal)>=0||t.IndexOf("反骑",StringComparison.Ordinal)>=0)defense+=8;if(t.IndexOf("辅助",StringComparison.Ordinal)>=0){martial-=5;defense-=3;}p.Stats.Martial=Math.Max(p.Stats.Martial,martial);p.Stats.Defense=Math.Max(p.Stats.Defense,defense);p.Stats.Stamina=Math.Max(p.Stats.Stamina,stamina);p.Stats.Life=Math.Max(p.Stats.Life,90+defense/2);}
        static string SpecialFormation(SpecialUnitDef u){string t=(u==null?"":(u.Role??"")+" "+(u.Category??""));if(t.IndexOf("反骑",StringComparison.Ordinal)>=0)return "枪阵";if(t.IndexOf("远程",StringComparison.Ordinal)>=0)return "弓弩轮射阵";if(t.IndexOf("骑",StringComparison.Ordinal)>=0)return "楔形骑阵";if(t.IndexOf("守城",StringComparison.Ordinal)>=0)return "盾墙";if(t.IndexOf("侦察",StringComparison.Ordinal)>=0)return "疏散斥候队形";return "横队";}
        bool IsCavalry(string unitTemplateId){UnitDef u;SpecialUnitDef s;string text="";if(_data!=null&&_data.Units.TryGetValue(unitTemplateId,out u))text=(u.Category??"")+(u.Role??"")+(u.Name??"");else if(_data!=null&&_data.SpecialUnits.TryGetValue(unitTemplateId,out s))text=(s.Category??"")+(s.Role??"")+(s.Name??"");return text.IndexOf("骑")>=0;}
        bool PresentForDuty(string id){Person p;return !string.IsNullOrEmpty(id)&&_people.TryGetValue(id,out p)&&p.Alive&&p.Injury!=InjuryState.Dead&&p.Injury!=InjuryState.Captured;}
        bool CombatReady(string id){Person p;return !string.IsNullOrEmpty(id)&&_people.TryGetValue(id,out p)&&p.Alive&&(p.Injury==InjuryState.None||p.Injury==InjuryState.Light);}
        public int SoldierCount(Army a){int n=0;if(a!=null)foreach(var s in a.Squads){foreach(string id in s.SoldierIds)if(PresentForDuty(id))n++;if(PresentForDuty(s.OfficerId))n++;}return n;}
        public int ReadySoldierCount(Army a){int n=0;if(a!=null)foreach(var s in a.Squads)if(s.TrainingRemainingDays<=0){foreach(string id in s.SoldierIds)if(CombatReady(id))n++;if(CombatReady(s.OfficerId))n++;}return n;}
        public int UnitCount(Army a,string unitId){int n=0;if(a!=null)foreach(var s in a.Squads)if(s.UnitTemplateId==unitId)n+=s.SoldierIds.Count;return n;}
        public void TrainDay(Army a,CommanderBehaviorProfile profile,float policyTraining=1f,float cavalryTraining=1f){
            if(a==null)return;if(profile==null)profile=new CommanderBehaviorProfile();
            float baseTraining=Mathx.Clamp((profile.Training*.7f+profile.Discipline*.3f)*Mathx.Clamp(policyTraining,.70f,1.65f),.55f,2.25f);
            foreach(Squad s in a.Squads){
                float veteranCohesion=VeteranEffectEngine.SquadCohesionMultiplier(s,_people);
                float training=baseTraining*(IsCavalry(s.UnitTemplateId)?Mathx.Clamp(cavalryTraining,.75f,1.6f):1f);
                if(s.TrainingRemainingDays<=0){s.Cohesion=Math.Min(100f,s.Cohesion+.35f*veteranCohesion*profile.Cohesion*Mathx.Clamp(policyTraining,.8f,1.35f));continue;}
                s.TrainingProgress+=training*Mathx.Clamp(.94f+.06f*veteranCohesion,.94f,1.08f);
                int completed=0;
                while(s.TrainingProgress>=1f&&s.TrainingRemainingDays>0){s.TrainingProgress-=1f;s.TrainingRemainingDays--;completed++;}
                if(completed<=0)continue;
                s.Cohesion=Math.Min(100f,s.Cohesion+completed*3f*profile.Cohesion*veteranCohesion);
                foreach(string id in s.SoldierIds){Person p;if(_people.TryGetValue(id,out p)){p.Experience+=Math.Max(1,(int)Math.Round(2f*training*completed));p.Stats.Stamina=Math.Min(140,p.Stats.Stamina+completed);}}
            }
        }
        public void TrainDay(Army a){TrainDay(a,new CommanderBehaviorProfile(),1f,1f);}
        public void PromoteVeterans(Army a){if(a==null)return;foreach(var s in a.Squads){float veteranRatio=VeteranEffectEngine.SquadVeteranRatio(s,_people);s.Cohesion=Math.Min(100f,s.Cohesion+veteranRatio*2.5f);foreach(string id in s.SoldierIds){Person p;if(!_people.TryGetValue(id,out p))continue;_population.UpdateMilitaryPotential(p,TraitEffectEngine.OfficerPotentialMultiplier(p,_data));VeteranBehaviorProfile v=VeteranEffectEngine.Build(p);if(v.Tier>=2)p.Stats.Command=Math.Max(p.Stats.Command,12+v.Tier*3);}}}
        public float VeteranRatio(Squad s){return VeteranEffectEngine.SquadVeteranRatio(s,_people);}
    }
}
