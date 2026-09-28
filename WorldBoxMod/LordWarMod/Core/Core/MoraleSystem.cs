using System;
namespace LordWar {
    public static class MoraleSystem {
        public static float Update(float morale,float casualtyRate,float hunger,bool flankBroken,bool generalAlive,float veteranRatio,bool lastStand,float paidRatio,float fatigue){float delta=-casualtyRate*35f-hunger*4f-fatigue*.035f+(veteranRatio*7f)+(paidRatio-.5f)*4f;if(flankBroken)delta-=12f;if(!generalAlive)delta-=18f;if(lastStand)return Math.Max(3f,Math.Min(100,morale+delta*.7f));return Mathx.Clamp(morale+delta,0,100);}
        public static MoraleState State(float m){if(m>=75)return MoraleState.Exalted;if(m>=45)return MoraleState.Steady;if(m>=25)return MoraleState.Shaken;if(m>=10)return MoraleState.Breaking;return MoraleState.Routed;}
        public static bool Rout(float morale,bool lastStand){return !lastStand&&morale<10f;}
        public static float Spread(float own,float neighbor,float discipline){if(neighbor>=25)return own;float fear=(25-neighbor)*(.18f*(1f-Mathx.Clamp(discipline/100f,0,.85f)));return Math.Max(0,own-fear);}
    }
}
