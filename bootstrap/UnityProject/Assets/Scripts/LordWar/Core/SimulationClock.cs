using System;

namespace LordWar {
    /// <summary>Single saved simulation timeline; rendering time never advances this clock directly.</summary>
    [Serializable] public sealed class SimulationClock {
        public const float FixedStep = .05f;
        public long TickIndex { get; private set; }
        public float PendingSeconds { get; private set; }
        public float DaySeconds { get; private set; }

        public void Accumulate(float realSeconds,float speed) {
            if(realSeconds<=0f)return;
            // A resumed Android process must not simulate all the time spent in the background.
            PendingSeconds+=Math.Min(.25f,realSeconds)*Mathx.Clamp(speed,.1f,8f);
        }
        public bool HasStep { get { return PendingSeconds+0.000001f>=FixedStep; } }
        public int Step(float secondsPerDay) {
            PendingSeconds=Math.Max(0f,PendingSeconds-FixedStep);
            TickIndex++;
            DaySeconds+=FixedStep;
            int days=0;
            while(DaySeconds+0.000001f>=secondsPerDay){DaySeconds-=secondsPerDay;days++;}
            return days;
        }
        public void Restore(long tick,float pending,float daySeconds,float secondsPerDay) {
            if(tick<0||pending<0f||pending>2f||daySeconds<0f||daySeconds>=secondsPerDay)
                throw new ArgumentOutOfRangeException("simulation clock");
            TickIndex=tick;PendingSeconds=pending;DaySeconds=daySeconds;
        }
    }
}
