using System;
using System.Collections.Generic;
namespace LordWar.Military {
    public sealed class VeteranBehaviorProfile {
        public int Tier;
        public float Hit=1f,Block=1f,MoraleResistance=1f,FatigueEfficiency=1f,Cohesion=1f,CommandResponse=1f,PursuitDiscipline=1f;
        public string Label="新兵";
    }

    public static class VeteranEffectEngine {
        public static int Tier(Person p){
            if(p==null)return 0;
            if(p.Battles>=20||p.Experience>=650)return 4;
            if(p.Battles>=12||p.Experience>=360)return 3;
            if(p.Battles>=6||p.Experience>=180)return 2;
            if(p.Battles>=3||p.Experience>=90)return 1;
            return 0;
        }

        public static VeteranBehaviorProfile Build(Person p){
            int tier=Tier(p);var v=new VeteranBehaviorProfile{Tier=tier};
            v.Hit=1f+tier*.018f;
            v.Block=1f+tier*.035f;
            v.MoraleResistance=1f+tier*.055f;
            v.FatigueEfficiency=1f+tier*.040f;
            v.Cohesion=1f+tier*.045f;
            v.CommandResponse=1f+tier*.035f;
            v.PursuitDiscipline=1f+tier*.025f;
            v.Label=tier==0?"新兵":(tier==1?"熟练兵":(tier==2?"老兵":(tier==3?"资深老兵":"百战精锐")));
            return v;
        }

        public static float SquadVeteranRatio(Squad s,Dictionary<string,Person> people){
            if(s==null||people==null)return 0f;int total=0,veterans=0;
            foreach(string id in s.SoldierIds){Person p;if(!people.TryGetValue(id,out p)||!p.Alive)continue;total++;if(Tier(p)>=1)veterans++;}
            if(!string.IsNullOrEmpty(s.OfficerId)){Person o;if(people.TryGetValue(s.OfficerId,out o)&&o.Alive){total++;if(Tier(o)>=1)veterans++;}}
            return total==0?0f:veterans/(float)total;
        }

        public static float SquadCohesionMultiplier(Squad s,Dictionary<string,Person> people){
            if(s==null||people==null)return 1f;float sum=0f;int n=0;
            foreach(string id in s.SoldierIds){Person p;if(people.TryGetValue(id,out p)&&p.Alive){sum+=Build(p).Cohesion;n++;}}
            if(!string.IsNullOrEmpty(s.OfficerId)){Person o;if(people.TryGetValue(s.OfficerId,out o)&&o.Alive){sum+=Build(o).CommandResponse;n++;}}
            return n==0?1f:Mathx.Clamp(sum/n,.95f,1.22f);
        }
    }
}
