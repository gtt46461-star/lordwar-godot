using System;
using LordWar.Data;
namespace LordWar.Military {
 public sealed class UnlockContext {
  public string CityStyle;
  public int BarracksLevel,Population,Horses,ArmyGold,IronStock,WoodStock,AvailableGearQuality;
  public bool HasStable,HasArmory,HasTrainingGround,HasDefenseCamp,PolicyApproved,FamilyTradition,DoctrineAuthorized,GeneralUnlockSatisfied=true,SpecialTraitAuthorized=true;
  public int MatchingBattles,SkillTier;
  public string GeneralTraitText="",GeneralSkillText="",ActivePolicyText="",OfficialDoctrineText="";
 }
 public static class UnlockResolver {
  public static int RequiredGearQuality(string text){if(string.IsNullOrEmpty(text))return 0;if(text.IndexOf("传奇",StringComparison.Ordinal)>=0)return 7;if(text.IndexOf("家族名器",StringComparison.Ordinal)>=0)return 6;if(text.IndexOf("大师",StringComparison.Ordinal)>=0)return 5;if(text.IndexOf("精良",StringComparison.Ordinal)>=0)return 4;if(text.IndexOf("优良",StringComparison.Ordinal)>=0)return 3;if(text.IndexOf("制式",StringComparison.Ordinal)>=0)return 2;return 0;}
  public static bool CanRecruit(SpecialUnitDef u,UnlockContext c,out string reason){
   reason="";if(u==null||c==null){reason="缺少兵种或城市条件";return false;}
   if(!string.IsNullOrEmpty(u.CityStyle)&&u.CityStyle!="通用"&&c.CityStyle!=u.CityStyle){reason="城市文化不符";return false;}
   string source=u.Source??"";
   if(source.IndexOf("家族传统",StringComparison.Ordinal)>=0&&!c.FamilyTradition){reason="缺少对应家族军事传统";return false;}
   if(source.IndexOf("将军特性",StringComparison.Ordinal)>=0&&string.IsNullOrEmpty(c.GeneralTraitText)){reason="缺少对应将军特性";return false;}
   if((source.IndexOf("家族传统",StringComparison.Ordinal)>=0||source.IndexOf("复合条件",StringComparison.Ordinal)>=0)&&!c.GeneralUnlockSatisfied){reason="指定将军特性、统帅技能或城市条件尚未满足";return false;}

   string g=u.GeneralCondition??"";
   if(g.IndexOf("3次",StringComparison.Ordinal)>=0&&c.MatchingBattles<3){reason="对应兵类实战不足3次";return false;}
   if(g.IndexOf("二阶",StringComparison.Ordinal)>=0&&c.SkillTier<2){reason="统帅技能尚未达到二阶";return false;}
   if(g.IndexOf("特性",StringComparison.Ordinal)>=0){if(string.IsNullOrEmpty(c.GeneralTraitText)){reason="缺少对应将军特性";return false;}if(!c.SpecialTraitAuthorized){reason="现有将军特性与该特殊兵种战场定位不匹配";return false;}}

   string b=u.BuildingCondition??"";
   if(b.IndexOf("高级军营",StringComparison.Ordinal)>=0&&c.BarracksLevel<2){reason="军营尚未达到2级";return false;}
   if(b.IndexOf("军营",StringComparison.Ordinal)>=0&&c.BarracksLevel<=0){reason="缺少军营";return false;}
   if(b.IndexOf("马厩",StringComparison.Ordinal)>=0&&!c.HasStable){reason="缺少马厩";return false;}
   if(b.IndexOf("军械库",StringComparison.Ordinal)>=0&&!c.HasArmory){reason="缺少军械库";return false;}
   if(b.IndexOf("训练场",StringComparison.Ordinal)>=0&&!c.HasTrainingGround){reason="缺少训练场";return false;}
   if(b.IndexOf("城防营地",StringComparison.Ordinal)>=0&&!c.HasDefenseCamp){reason="缺少城防营地条件";return false;}

   string p=u.PolicyCondition??"";
   if(p.IndexOf("军营等级",StringComparison.Ordinal)>=0&&c.BarracksLevel<2){reason="城市军营等级不足";return false;}
   if(p.IndexOf("军械标准化",StringComparison.Ordinal)>=0||p.IndexOf("轮训制度",StringComparison.Ordinal)>=0){if(!ContainsAny(c.ActivePolicyText,"军械","制式","轮训")){reason="军械标准化/轮训制度尚未批准";return false;}}
   else if((p.IndexOf("军制",StringComparison.Ordinal)>=0||p.IndexOf("制度",StringComparison.Ordinal)>=0)&&!c.PolicyApproved){reason="对应军制尚未批准";return false;}
   if(source.IndexOf("官员军制",StringComparison.Ordinal)>=0&&!c.DoctrineAuthorized){reason="本城官员尚未以自身军政特性推动并获批对应军制";return false;}
   if(source.IndexOf("复合条件",StringComparison.Ordinal)>=0){int met=(c.PolicyApproved?1:0)+(!string.IsNullOrEmpty(c.GeneralTraitText)?1:0)+(c.FamilyTradition?1:0)+(c.DoctrineAuthorized?1:0);if(met<2){reason="复合解锁条件尚未形成";return false;}}

   int requiredQuality=RequiredGearQuality(u.Gear);if(requiredQuality>0&&c.AvailableGearQuality<requiredQuality){reason="城市军械工艺与库存达不到"+u.Gear+"要求";return false;}
   int materialNeed=Math.Max(2,u.SquadCap/5);if(requiredQuality>=3&&c.IronStock<materialNeed){reason="优良以上装备所需铁料不足";return false;}if((u.Category??"").IndexOf("远程",StringComparison.Ordinal)>=0&&c.WoodStock<materialNeed){reason="远程兵装备所需木料不足";return false;}
   if((u.Category??"").IndexOf("骑",StringComparison.Ordinal)>=0&&c.Horses<u.SquadCap){reason="可用军马不足以组成完整特殊骑兵小队";return false;}
   if(c.Population<u.SquadCap){reason="兵员不足";return false;}
   if(c.ArmyGold<(int)Math.Ceiling(u.SquadCap*8*Math.Max(1f,u.Upkeep))){reason="军资不足";return false;}
   return true;
  }
  static bool ContainsAny(string text,params string[] words){if(string.IsNullOrEmpty(text))return false;for(int i=0;i<words.Length;i++)if(text.IndexOf(words[i],StringComparison.Ordinal)>=0)return true;return false;}
 }
}
