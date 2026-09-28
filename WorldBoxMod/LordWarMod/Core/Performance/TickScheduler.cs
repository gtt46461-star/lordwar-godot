using System;
using System.Collections.Generic;
namespace LordWar.Performance {
    public sealed class TickScheduler {
        sealed class Job { public string Name; public float Interval,Elapsed; public int MaxCatchUpRuns; public Action<float> Action; }
        readonly List<Job> _jobs=new List<Job>();
        public void Register(string name,float interval,Action<float> action){Register(name,interval,4,action);}
        public void Register(string name,float interval,int maxCatchUpRuns,Action<float> action){_jobs.Add(new Job{Name=name,Interval=Math.Max(.02f,interval),MaxCatchUpRuns=Math.Max(1,maxCatchUpRuns),Action=action});}
        public void Tick(float dt){
            if(dt<=0)return;
            for(int i=0;i<_jobs.Count;i++){
                Job j=_jobs[i];j.Elapsed+=dt;int runs=0;
                while(j.Elapsed>=j.Interval&&runs<j.MaxCatchUpRuns){j.Elapsed-=j.Interval;runs++;j.Action(j.Interval);}
                if(runs>=j.MaxCatchUpRuns&&j.Elapsed>j.Interval*j.MaxCatchUpRuns)j.Elapsed=j.Interval*j.MaxCatchUpRuns;
            }
        }
        public static int LodLevel(float distanceFromCamera){if(distanceFromCamera<40)return 0;if(distanceFromCamera<90)return 1;if(distanceFromCamera<180)return 2;return 3;}
        public static float ActorTickInterval(int lod){return lod<=0 ? .08f:(lod==1 ? .25f:(lod==2?1f:4f));}
    }
}
