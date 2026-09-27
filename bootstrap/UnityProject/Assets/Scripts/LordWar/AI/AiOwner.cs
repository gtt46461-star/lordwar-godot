using System;
using LordWar.Governance;
namespace LordWar.AI {
    public enum AiDifficulty { Easy, Normal, Hard, Nightmare }

    public sealed class AiWarAssessment {
        public float PowerRatio = 1f;
        public float OwnFoodDays = 0f;
        public float OwnMorale = 0f;
        public float OwnFatigue = 0f;
        public float SupplyRisk = 0f;
        public float CommanderScout = 1f;
        public float CommanderSupply = 1f;
        public float EnemyFortification = 0f;
        public float EnemyMorale = 100f;
        public int Distance;
        public WeatherKind Weather = WeatherKind.Clear;
        public bool EnemyCapital;
        public bool OwnCapitalThreatened;
    }

    public sealed class AiOwner {
        public AiDifficulty Difficulty;
        readonly DeterministicRandom _rng;
        public AiOwner(AiDifficulty d,int seed){Difficulty=d;_rng=new DeterministicRandom(seed^0x113355);}
        public float DecisionQuality(){switch(Difficulty){case AiDifficulty.Easy:return .38f;case AiDifficulty.Normal:return .58f;case AiDifficulty.Hard:return .76f;default:return .9f;}}
        public bool ShouldProposeFoodPolicy(City c,Kingdom k){float foodDays=c.Food/(float)Math.Max(1,c.PersonIds.Count);return foodDays<5f&&_rng.Next01()<DecisionQuality();}
        public bool ShouldRecruit(City c,Kingdom k,int existingSoldiers){float civilian=Math.Max(1,c.PersonIds.Count-existingSoldiers);float ratio=existingSoldiers/(civilian+existingSoldiers);return k.Treasury>250&&ratio<(.08f+DecisionQuality()*.12f);}
        public int ConstructionPriority(City c){if(c.Food<c.PersonIds.Count*3)return 100;if(c.PopulationCapacity<c.PersonIds.Count*12/10)return 90;if(c.MarketJobs<c.PersonIds.Count/6)return 65;return 40;}

        public float WarScore(AiWarAssessment a){
            if(a==null)return -999f;
            float score=0f;
            score+=(a.PowerRatio-1f)*95f;
            score+=(a.OwnFoodDays-4f)*5f;
            score+=(a.OwnMorale-50f)*.55f;
            score-=Math.Max(0f,a.OwnFatigue-28f)*.55f;
            score-=Mathx.Clamp(a.SupplyRisk,0f,1f)*32f;
            score+=(Mathx.Clamp(a.CommanderScout,.65f,1.65f)-1f)*18f;
            score+=(Mathx.Clamp(a.CommanderSupply,.65f,1.65f)-1f)*24f;
            score-=Mathx.Clamp(a.EnemyFortification,0f,1.5f)*26f;
            score+=(55f-a.EnemyMorale)*.22f;
            score-=Math.Max(0,a.Distance-8)*1.35f;
            if(a.Weather==WeatherKind.Storm||a.Weather==WeatherKind.Blizzard)score-=14f;
            else if(a.Weather==WeatherKind.Snow||a.Weather==WeatherKind.Rain)score-=5f;
            if(a.EnemyCapital)score+=6f;
            if(a.OwnCapitalThreatened)score-=45f;
            return score;
        }

        public bool ShouldAttack(AiWarAssessment a){
            if(a==null||a.OwnFoodDays<=3f||a.OwnMorale<=42f||a.OwnFatigue>=78f)return false;
            // Difficulty only improves decision quality/tolerance; it never grants resources or combat stats.
            float threshold=32f-(DecisionQuality()*16f);
            float uncertainty=(1f-DecisionQuality())*18f;
            float noise=(_rng.Next01()-.5f)*uncertainty;
            return WarScore(a)+noise>=threshold;
        }

        public bool ShouldAttack(float powerRatio,float ownFoodDays,float ownMorale){return ShouldAttack(new AiWarAssessment{PowerRatio=powerRatio,OwnFoodDays=ownFoodDays,OwnMorale=ownMorale});}

        public bool ShouldAssaultSiege(float gateIntegrity,float wallIntegrity,float ownMorale,float ownFoodDays,float powerRatio,float engineeringFactor){
            if(ownMorale<38f||ownFoodDays<1.5f)return false;
            float breach=(100f-Math.Min(gateIntegrity,wallIntegrity))*.65f;
            float score=breach+(powerRatio-1f)*34f+(engineeringFactor-1f)*24f+(ownMorale-50f)*.35f;
            float threshold=42f-DecisionQuality()*12f;
            return score>=threshold;
        }
    }
}
