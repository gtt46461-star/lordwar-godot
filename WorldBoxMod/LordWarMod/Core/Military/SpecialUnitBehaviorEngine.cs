using System;
using LordWar.Data;

namespace LordWar.Military {
    public sealed class SpecialUnitBehaviorProfile {
        public bool IsSpecial;
        public string Behavior="",Weakness="";
        public float LineCohesion=1f,AntiCavalry=1f,RangedSafety=1f,FlankMobility=1f,ScoutAmbush=1f,KeyDefense=1f;
        public float Engineering=1f,SupplyGuard=1f,GeneralGuard=1f,PursuitCut=1f;
        public float FrontalCombat=1f,MeleeDefense=1f,FatigueCost=1f,ReplacementSpeed=1f,PursuitRisk=1f;
    }

    public static class SpecialUnitBehaviorEngine {
        static bool Has(string text,string key){return !string.IsNullOrEmpty(text)&&text.IndexOf(key,StringComparison.Ordinal)>=0;}

        public static SpecialUnitBehaviorProfile Build(string unitTemplateId,GameDataCatalog data) {
            var p=new SpecialUnitBehaviorProfile();if(data==null||string.IsNullOrEmpty(unitTemplateId))return p;SpecialUnitDef u;if(!data.SpecialUnits.TryGetValue(unitTemplateId,out u)||u==null)return p;
            p.IsSpecial=true;p.Behavior=u.Behavior??"";p.Weakness=u.Weakness??"";
            if(Has(p.Behavior,"保持阵线并自动补位")){p.LineCohesion=1.22f;p.MeleeDefense=1.08f;}
            else if(Has(p.Behavior,"遭遇骑兵时自动收束枪阵")){p.AntiCavalry=1.34f;p.LineCohesion=1.10f;}
            else if(Has(p.Behavior,"优先寻找安全射界并轮射")){p.RangedSafety=1.22f;p.MeleeDefense=.94f;}
            else if(Has(p.Behavior,"沿侧翼高速机动但不过度脱队")){p.FlankMobility=1.24f;p.FatigueCost=1.06f;}
            else if(Has(p.Behavior,"扩大侦察扇面并提高伏击权重")){p.ScoutAmbush=1.28f;p.FrontalCombat=.96f;}
            else if(Has(p.Behavior,"士气低时仍优先守住关键点")){p.KeyDefense=1.28f;p.LineCohesion=1.12f;}
            else if(Has(p.Behavior,"优先破坏门、桥、路障和城防设施")){p.Engineering=1.35f;p.FrontalCombat=.95f;}
            else if(Has(p.Behavior,"保持辎重与主力距离，优先护粮")){p.SupplyGuard=1.30f;p.FrontalCombat=.92f;}
            else if(Has(p.Behavior,"紧随主将，优先保护军旗与将军")){p.GeneralGuard=1.25f;p.LineCohesion=1.10f;}
            else if(Has(p.Behavior,"敌军溃败后截断撤退路线")){p.PursuitCut=1.32f;p.PursuitRisk=1.10f;}

            if(Has(p.Weakness,"补员慢")){p.ReplacementSpeed*=.76f;}
            if(Has(p.Weakness,"转向慢")){p.MeleeDefense*=.91f;p.FlankMobility*=.86f;}
            if(Has(p.Weakness,"近身后明显脆弱")){p.MeleeDefense*=.78f;}
            if(Has(p.Weakness,"体力消耗大")){p.FatigueCost*=1.24f;}
            if(Has(p.Weakness,"正面硬战能力较弱")){p.FrontalCombat*=.84f;}
            if(Has(p.Weakness,"离开防御目标后价值下降")){p.FrontalCombat*=.88f;}
            if(Has(p.Weakness,"人数少、正面作战一般")){p.FrontalCombat*=.90f;}
            if(Has(p.Weakness,"作战属性低于主战兵")){p.FrontalCombat*=.84f;}
            if(Has(p.Weakness,"伤亡后补员困难")){p.ReplacementSpeed*=.70f;}
            if(Has(p.Weakness,"反包围风险")){p.PursuitRisk*=1.35f;}
            return p;
        }

        public static SpecialUnitBehaviorProfile ArmyProfile(Army army,GameDataCatalog data) {
            var result=new SpecialUnitBehaviorProfile();if(army==null||army.Squads==null||army.Squads.Count==0)return result;
            float total=0f;int specialCount=0;float line=0,anti=0,ranged=0,flank=0,scout=0,key=0,engineering=0,supply=0,guard=0,pursuit=0,front=0,melee=0,fatigue=0,replacement=0,risk=0;
            foreach(Squad s in army.Squads){SpecialUnitBehaviorProfile q=Build(s.UnitTemplateId,data);if(!q.IsSpecial)continue;float w=Math.Max(1,s.SoldierIds==null?1:s.SoldierIds.Count);total+=w;specialCount++;line+=(q.LineCohesion-1f)*w;anti+=(q.AntiCavalry-1f)*w;ranged+=(q.RangedSafety-1f)*w;flank+=(q.FlankMobility-1f)*w;scout+=(q.ScoutAmbush-1f)*w;key+=(q.KeyDefense-1f)*w;engineering+=(q.Engineering-1f)*w;supply+=(q.SupplyGuard-1f)*w;guard+=(q.GeneralGuard-1f)*w;pursuit+=(q.PursuitCut-1f)*w;front+=(q.FrontalCombat-1f)*w;melee+=(q.MeleeDefense-1f)*w;fatigue+=(q.FatigueCost-1f)*w;replacement+=(q.ReplacementSpeed-1f)*w;risk+=(q.PursuitRisk-1f)*w;}
            if(total<=0f)return result;result.IsSpecial=true;float damp=Mathx.Clamp(total/Math.Max(total,Math.Max(1,army.Squads.Count)*12f),.25f,1f);result.LineCohesion=1f+line/total*damp;result.AntiCavalry=1f+anti/total*damp;result.RangedSafety=1f+ranged/total*damp;result.FlankMobility=1f+flank/total*damp;result.ScoutAmbush=1f+scout/total*damp;result.KeyDefense=1f+key/total*damp;result.Engineering=1f+engineering/total*damp;result.SupplyGuard=1f+supply/total*damp;result.GeneralGuard=1f+guard/total*damp;result.PursuitCut=1f+pursuit/total*damp;result.FrontalCombat=1f+front/total*damp;result.MeleeDefense=1f+melee/total*damp;result.FatigueCost=1f+fatigue/total*damp;result.ReplacementSpeed=1f+replacement/total*damp;result.PursuitRisk=1f+risk/total*damp;return result;
        }

        public static int TrainingDays(SpecialUnitDef u,SpecialUnitBehaviorProfile p) {
            if(u==null)return 0;int days=5;if(Has(u.Category,"骑"))days+=2;if(Has(u.Category,"精锐"))days+=3;if(Has(u.Category,"工程"))days+=1;if(Has(u.Category,"侦察"))days=Math.Max(4,days-1);days+=(int)Math.Ceiling(Math.Max(0f,u.Upkeep-1f)*3f);return Mathx.Clamp((int)Math.Ceiling(days/Mathx.Clamp(p==null?1f:p.ReplacementSpeed,.55f,1.2f)),4,16);
        }
    }
}
