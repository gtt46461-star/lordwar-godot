using System;
using System.Collections.Generic;
namespace LordWar.Society {
    public sealed class PopulationOwner {
        readonly Dictionary<string,Person> _people; readonly Dictionary<string,City> _cities; readonly DeterministicRandom _rng;
        public PopulationOwner(Dictionary<string,Person> people,Dictionary<string,City> cities,int seed){_people=people;_cities=cities;_rng=new DeterministicRandom(seed^0x51f15e);}
        public Person CreatePerson(City c,string name,SocialClass cls,int age){var p=new Person{Id=Ids.Next("P"),Name=name,CityId=c.Id,KingdomId=c.KingdomId,Class=cls,Age=age,Noble=cls==SocialClass.Noble||cls==SocialClass.General||cls==SocialClass.Official};p.Stats.Loyalty=_rng.Range(42,86);p.Stats.Ambition=_rng.Range(15,76);p.Stats.Martial=_rng.Range(20,61);p.Stats.Intelligence=_rng.Range(20,61);_people[p.Id]=p;c.PersonIds.Add(p.Id);return p;}
        public int LivingPopulation(City c){int n=0;foreach(string id in c.PersonIds){Person p;if(_people.TryGetValue(id,out p)&&p.Alive)n++;}return n;}
        public int CivilianPopulation(City c){int n=0;foreach(string id in c.PersonIds){Person p;if(_people.TryGetValue(id,out p)&&p.Alive&&p.Job!=JobKind.Soldier&&p.Job!=JobKind.Officer)n++;}return n;}
        public void AssignJobs(City c,float jobMatching){
            float match=Mathx.Clamp(jobMatching,.70f,1.45f);
            int farms=Math.Max(0,(int)Math.Round(c.FarmJobs*match)),mines=Math.Max(0,(int)Math.Round(c.MineJobs*match)),logs=Math.Max(0,(int)Math.Round(c.LoggingJobs*match)),shops=Math.Max(0,(int)Math.Round(c.MarketJobs*match)),works=Math.Max(0,(int)Math.Round(c.WorkshopJobs*match));
            foreach(string id in c.PersonIds){
                Person p;if(!_people.TryGetValue(id,out p)||!p.Alive||p.Injury==InjuryState.Heavy||p.Injury==InjuryState.Incapacitated||p.Injury==InjuryState.Captured||p.Job==JobKind.Soldier||p.Job==JobKind.Officer||p.Job==JobKind.Builder||p.Job==JobKind.Porter||p.Class==SocialClass.Official||p.Class==SocialClass.General)continue;
                if(p.Merchant){p.Job=JobKind.Trader;continue;}
                if(farms-->0)p.Job=JobKind.Farmer;else if(mines-->0)p.Job=JobKind.Miner;else if(logs-->0)p.Job=JobKind.Logger;else if(works-->0)p.Job=JobKind.Smith;else if(shops-->0)p.Job=JobKind.Trader;else p.Job=JobKind.Unemployed;
            }
        }
        public void AssignJobs(City c){AssignJobs(c,1f);}
        public int Grow(City c,float foodSecurity,float housingRatio,float elapsedMinutes,float policyGrowth=1f){int pop=LivingPopulation(c);if(pop<=1||pop>=700)return 0;float capacity=Math.Max(1,c.PopulationCapacity);float capFactor=Mathx.Clamp(1f-pop/capacity,0f,1f);float per4Min=.04f*foodSecurity*Mathx.Clamp(housingRatio,0f,1.1f)*(.25f+.75f*capFactor)*Mathx.Clamp(policyGrowth,.65f,1.8f);float expected=pop*per4Min*(elapsedMinutes/4f);int births=(int)Math.Floor(expected);if(_rng.Next01()<expected-births)births++;births=Math.Min(births,Math.Max(0,700-pop));for(int i=0;i<births;i++)CreatePerson(c,"新民"+Ids.Next("名"),SocialClass.Commoner,_rng.Range(16,31));return births;}
        public Person RecruitOne(City c){Person best=null;foreach(string id in c.PersonIds){Person p;if(!_people.TryGetValue(id,out p)||!p.Alive||p.Injury!=InjuryState.None||p.Age<17||p.Age>45||p.Class==SocialClass.Official||p.Class==SocialClass.General||p.Job==JobKind.Soldier)continue;if(best==null||p.Stats.Martial>best.Stats.Martial)best=p;}if(best!=null){best.Job=JobKind.Soldier;best.Experience+=2;}return best;}
        public bool PromoteToNoble(Person p,string reason){if(p==null||!p.Alive||p.Noble)return false;if(p.Kills<3&&p.Experience<120&&p.Wealth<900&&p.CivicMerit<100&&!p.HasCommandPotential)return false;p.Noble=true;p.Class=SocialClass.Noble;p.Promotions++;p.Stats.Prestige+=12;p.LastMeritReason=reason??p.LastMeritReason;return true;}
        public void UpdateMilitaryPotential(Person p,float traitMultiplier=1f){if(p==null||!p.Alive)return;float m=Mathx.Clamp(traitMultiplier,.75f,1.65f);int xp=(int)Math.Round(90f/m);int martial=(int)Math.Round(55f/Math.Min(1.18f,m));if(p.Job==JobKind.Soldier&&p.Experience>=xp&&p.Battles>=2&&p.Stats.Martial>=martial&&p.Stats.Loyalty>=40)p.HasCommandPotential=true;}
    }
}
