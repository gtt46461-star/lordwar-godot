using System;

namespace LordWar {
    /// <summary>城市文化的唯一运行画像。文化必须改变生产、建设、兵员和马政，而不只是显示名/贴图。</summary>
    public sealed class CityCultureProfile {
        public float Agriculture=1f, Mining=1f, Logging=1f, Commerce=1f, Smithing=1f, Construction=1f, Recruitment=1f, HorseGrowth=1f, Scout=1f, Repair=1f;
        public BuildingKind PreferredA=BuildingKind.Market, PreferredB=BuildingKind.Road;
    }

    public static class CityCultureEffectEngine {
        public static CityCultureProfile Build(City c) {
            var r=new CityCultureProfile(); string id=c==null?"":(c.CultureId??"");
            if(id.IndexOf("平原",StringComparison.Ordinal)>=0){r.Agriculture=1.16f;r.Recruitment=1.10f;r.PreferredA=BuildingKind.Granary;r.PreferredB=BuildingKind.Barracks;}
            else if(id.IndexOf("山岭",StringComparison.Ordinal)>=0){r.Mining=1.18f;r.Repair=1.12f;r.Construction=1.06f;r.PreferredA=BuildingKind.Wall;r.PreferredB=BuildingKind.Armory;}
            else if(id.IndexOf("河谷",StringComparison.Ordinal)>=0){r.Commerce=1.18f;r.Agriculture=1.08f;r.PreferredA=BuildingKind.Market;r.PreferredB=BuildingKind.Road;}
            else if(id.IndexOf("森林",StringComparison.Ordinal)>=0){r.Logging=1.20f;r.Scout=1.12f;r.PreferredA=BuildingKind.Workshop;r.PreferredB=BuildingKind.Road;}
            else if(id.IndexOf("草原",StringComparison.Ordinal)>=0){r.HorseGrowth=1.25f;r.Recruitment=1.12f;r.PreferredA=BuildingKind.Stable;r.PreferredB=BuildingKind.Barracks;}
            else if(id.IndexOf("海港",StringComparison.Ordinal)>=0){r.Commerce=1.22f;r.Logging=1.05f;r.PreferredA=BuildingKind.Market;r.PreferredB=BuildingKind.Granary;}
            else if(id.IndexOf("寒原",StringComparison.Ordinal)>=0){r.Logging=1.08f;r.Recruitment=1.10f;r.Repair=1.08f;r.PreferredA=BuildingKind.Granary;r.PreferredB=BuildingKind.Wall;}
            else if(id.IndexOf("绿洲",StringComparison.Ordinal)>=0){r.Commerce=1.15f;r.Agriculture=1.10f;r.PreferredA=BuildingKind.Market;r.PreferredB=BuildingKind.Granary;}
            else if(id.IndexOf("湖沼",StringComparison.Ordinal)>=0){r.Commerce=1.10f;r.Agriculture=1.08f;r.PreferredA=BuildingKind.Road;r.PreferredB=BuildingKind.Market;}
            else if(id.IndexOf("矿山",StringComparison.Ordinal)>=0||id.IndexOf("铸",StringComparison.Ordinal)>=0){r.Mining=1.24f;r.Smithing=1.22f;r.Repair=1.15f;r.PreferredA=BuildingKind.Smithy;r.PreferredB=BuildingKind.Armory;}
            else if(id.IndexOf("王都",StringComparison.Ordinal)>=0){r.Commerce=1.10f;r.Construction=1.10f;r.Recruitment=1.08f;r.PreferredA=BuildingKind.Government;r.PreferredB=BuildingKind.TrainingGround;}
            else if(id.IndexOf("边境",StringComparison.Ordinal)>=0){r.Recruitment=1.12f;r.Scout=1.10f;r.Repair=1.08f;r.PreferredA=BuildingKind.Barracks;r.PreferredB=BuildingKind.Wall;}
            return r;
        }
    }
}
