using System;
namespace LordWar {
    public static class EquipmentDurabilitySystem {
        public static int Wear(EquipmentInstance e,int baseWear,float intensity){if(e==null||e.Durability<=0)return 0;int loss=Math.Max(1,(int)Math.Round(baseWear*Math.Max(.1f,intensity)));e.Durability=Math.Max(0,e.Durability-loss);if(e.Unique&&loss>0)e.History.Add("战损：耐久-"+loss);return loss;}
        public static int MarchWear(EquipmentInstance e,TerrainKind terrain,float distance){float mul=(terrain==TerrainKind.Marsh||terrain==TerrainKind.Snow||terrain==TerrainKind.Mountain)?1.4f:.55f;return Wear(e,1,Math.Max(.1f,distance*.02f*mul));}
        public static int RepairCost(EquipmentInstance e,int baseValue){if(e==null||e.MaxDurability<=0)return 0;float missing=1f-(float)e.Durability/e.MaxDurability;float qualityMul=.7f+e.Quality*.35f;return Math.Max(0,(int)Math.Ceiling(baseValue*missing*qualityMul));}
        public static float Effectiveness(EquipmentInstance e){if(e==null||e.MaxDurability<=0)return 0f;float r=e.Durability/(float)e.MaxDurability;return .45f+.55f*Mathx.Clamp(r,0,1);}
    }
}
