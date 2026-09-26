using System;
using System.Globalization;
using LordWar.Data;
using LordWar.Military;

namespace LordWar {
 public sealed class PersonBehaviorProfile {
  public float Risk=1f,Loyalty=1f,Ambition=1f,Commerce=1f,Investment=1f,Work=1f,Social=1f,Health=1f,Growth=1f,Prestige=1f;
 }
 public sealed class SoldierBehaviorProfile {
  public float Melee=1f,Ranged=1f,Riding=1f,Block=1f,Stamina=1f,Morale=1f,Discipline=1f,Scout=1f,Pursuit=1f,Defense=1f,Engineering=1f,Recovery=1f,OfficerPotential=1f;
 }
 public sealed class OfficialRuntimeProfile {
  public float Population=1f,Agriculture=1f,Treasury=1f,Commerce=1f,Construction=1f,Military=1f,Armory=1f,Logistics=1f,Fortification=1f,Oversight=1f,PublicOrder=1f,Horses=1f;
 }

 public static class TraitEffectEngine {
  static float FirstPercent(string s){if(string.IsNullOrEmpty(s))return 0f;for(int i=0;i<s.Length;i++){int start=i;if(s[i]=='+'||s[i]=='-')i++;int j=i;while(j<s.Length&&(char.IsDigit(s[j])||s[j]=='.'))j++;if(j>i&&j<s.Length&&s[j]=='%'){float v;if(float.TryParse(s.Substring(start,j-start),NumberStyles.Float,CultureInfo.InvariantCulture,out v))return v/100f;}i=start;}return 0f;}
  static float Magnitude(string s,float fallback=.05f){float p=Math.Abs(FirstPercent(s));return p>0?Mathx.Clamp(p,.01f,.30f):fallback;}

  public static PersonBehaviorProfile PersonProfile(Person p,GameDataCatalog data){
   var r=new PersonBehaviorProfile();if(p==null||data==null)return r;
   foreach(string id in p.TraitIds){TraitDef d;if(!data.PersonTraits.TryGetValue(id,out d))continue;string cat=d.Category??"",name=d.Name??"",e=d.Effect??"";float mag=Magnitude(e,.04f);float signed=FirstPercent(e);
    if(cat=="性格"){r.Risk*=signed<0?Math.Max(.68f,1f-mag*.65f):1f+mag*.65f;r.Work*=1f+mag*.2f;}
    if(cat=="忠诚"||e.IndexOf("忠诚")>=0||name.IndexOf("守诺")>=0)r.Loyalty*=1f+mag;
    if(cat=="野心"||e.IndexOf("野心")>=0||e.IndexOf("权力")>=0||e.IndexOf("职位")>=0||e.IndexOf("晋升")>=0)r.Ambition*=signed<0?Math.Max(.65f,1f-mag):1f+mag;
    if(cat=="商业"||cat=="财富"||e.IndexOf("贸易")>=0||e.IndexOf("经商")>=0){r.Commerce*=1f+mag;r.Investment*=1f+mag*.7f;}
    if(cat=="风险"||e.IndexOf("风险")>=0||e.IndexOf("冲动")>=0||name.IndexOf("急躁")>=0)r.Risk*=signed<0?Math.Max(.65f,1f-mag):1f+mag;
    if(name.IndexOf("谨慎")>=0)r.Risk*=Math.Max(.65f,1f-mag);
    if(cat=="智识"){r.Growth*=1f+mag;r.Work*=1f+mag*.3f;}
    if(cat=="成长"||e.IndexOf("成长")>=0||e.IndexOf("学习")>=0||e.IndexOf("训练")>=0||e.IndexOf("技能经验")>=0)r.Growth*=1f+mag;
    if(cat=="健康"||cat=="体魄"||e.IndexOf("恢复")>=0||e.IndexOf("健康")>=0)r.Health*=1f+mag;
    if(cat=="社交"||e.IndexOf("协商")>=0||e.IndexOf("人脉")>=0)r.Social*=1f+mag;
    if(cat=="声望"||cat=="政治"||e.IndexOf("声望")>=0)r.Prestige*=signed<0?Math.Max(.7f,1f-mag):1f+mag;
    if(cat=="家庭"){r.Loyalty*=1f+mag*.35f;r.Social*=1f+mag*.35f;}
    if(cat=="农业"||cat=="手艺"||e.IndexOf("效率")>=0||e.IndexOf("制作")>=0)r.Work*=1f+mag;
   }
   r.Loyalty=Mathx.Clamp(r.Loyalty,.7f,1.5f);r.Ambition=Mathx.Clamp(r.Ambition,.7f,1.5f);r.Commerce=Mathx.Clamp(r.Commerce,.7f,1.6f);r.Investment=Mathx.Clamp(r.Investment,.65f,1.6f);r.Risk=Mathx.Clamp(r.Risk,.65f,1.55f);r.Work=Mathx.Clamp(r.Work,.75f,1.5f);r.Health=Mathx.Clamp(r.Health,.75f,1.5f);r.Growth=Mathx.Clamp(r.Growth,.75f,1.5f);r.Social=Mathx.Clamp(r.Social,.75f,1.5f);r.Prestige=Mathx.Clamp(r.Prestige,.75f,1.5f);return r;
  }

  public static SoldierBehaviorProfile SoldierProfile(Person p,GameDataCatalog data){
   var r=new SoldierBehaviorProfile();if(p==null||data==null)return r;
   foreach(string id in p.TraitIds){TraitDef d;if(!data.SoldierTraits.TryGetValue(id,out d))continue;string cat=d.Category??"",e=d.Effect??"",n=d.Name??"";float mag=Magnitude(e,.04f);
    if(cat=="近战"||cat=="枪矛")r.Melee*=1f+mag;
    if(cat=="弓射"||cat=="弩射")r.Ranged*=1f+mag;
    if(cat=="骑术")r.Riding*=1f+mag;
    if(cat=="盾战"||e.IndexOf("格挡")>=0||e.IndexOf("防御")>=0||e.IndexOf("举盾")>=0)r.Block*=1f+mag;
    if(cat=="体力"||e.IndexOf("体力")>=0||e.IndexOf("疲劳")>=0)r.Stamina*=1f+mag;
    if(cat=="士气"||e.IndexOf("士气")>=0||e.IndexOf("恐惧")>=0)r.Morale*=1f+mag;
    if(cat=="纪律"||e.IndexOf("纪律")>=0||e.IndexOf("阵型")>=0||e.IndexOf("补位")>=0)r.Discipline*=1f+mag;
    if(cat=="侦察"||e.IndexOf("侦察")>=0||e.IndexOf("视野")>=0)r.Scout*=1f+mag;
    if(cat=="追击"||e.IndexOf("追击")>=0)r.Pursuit*=1f+mag;
    if(cat=="守城")r.Defense*=1f+mag;
    if(cat=="工程")r.Engineering*=1f+mag;
    if(cat=="伤病"||e.IndexOf("恢复")>=0||e.IndexOf("存活")>=0)r.Recovery*=1f+mag;
    if(cat=="老兵"){r.Discipline*=1f+mag*.7f;r.Morale*=1f+mag*.7f;r.Stamina*=1f+mag*.5f;}
    if(cat=="军官潜质"||n.IndexOf("统帅")>=0||e.IndexOf("晋升")>=0)r.OfficerPotential*=1f+mag;
   }
   r.Melee=Mathx.Clamp(r.Melee,.75f,1.45f);r.Ranged=Mathx.Clamp(r.Ranged,.75f,1.45f);r.Riding=Mathx.Clamp(r.Riding,.75f,1.45f);r.Block=Mathx.Clamp(r.Block,.75f,1.45f);r.Stamina=Mathx.Clamp(r.Stamina,.75f,1.45f);r.Morale=Mathx.Clamp(r.Morale,.75f,1.45f);r.Discipline=Mathx.Clamp(r.Discipline,.75f,1.5f);r.Scout=Mathx.Clamp(r.Scout,.75f,1.5f);r.Pursuit=Mathx.Clamp(r.Pursuit,.75f,1.5f);r.Defense=Mathx.Clamp(r.Defense,.75f,1.45f);r.Engineering=Mathx.Clamp(r.Engineering,.75f,1.45f);r.Recovery=Mathx.Clamp(r.Recovery,.75f,1.55f);r.OfficerPotential=Mathx.Clamp(r.OfficerPotential,.75f,1.65f);return r;
  }

  public static OfficialRuntimeProfile OfficialProfile(Person p,GameDataCatalog data){
   var r=new OfficialRuntimeProfile();if(p==null||data==null)return r;
   foreach(string id in p.TraitIds){TraitDef d;if(!data.OfficialTraits.TryGetValue(id,out d))continue;float mag=Magnitude(d.Effect,.06f);string domain=d.Domain??"";
    if(domain=="户政")r.Population*=1f+mag;else if(domain=="农政")r.Agriculture*=1f+mag;else if(domain=="财政")r.Treasury*=1f+mag;else if(domain=="商政")r.Commerce*=1f+mag;else if(domain=="工务")r.Construction*=1f+mag;else if(domain=="军政")r.Military*=1f+mag;else if(domain=="军械")r.Armory*=1f+mag;else if(domain=="后勤")r.Logistics*=1f+mag;else if(domain=="城防")r.Fortification*=1f+mag;else if(domain=="监察")r.Oversight*=1f+mag;else if(domain=="民政")r.PublicOrder*=1f+mag;else if(domain=="马政")r.Horses*=1f+mag;
   }
   return r;
  }

  public static float SoldierHit(Person p,GameDataCatalog data){return SoldierProfile(p,data).Melee;}
  public static float SoldierRanged(Person p,GameDataCatalog data){return SoldierProfile(p,data).Ranged;}
  public static float SoldierRiding(Person p,GameDataCatalog data){return SoldierProfile(p,data).Riding;}
  public static float SoldierBlock(Person p,GameDataCatalog data){return SoldierProfile(p,data).Block;}
  public static float MoraleResistance(Person p,GameDataCatalog data){float m=SoldierProfile(p,data).Morale;if(p!=null&&data!=null){PersonBehaviorProfile pp=PersonProfile(p,data);m*=Mathx.Clamp((pp.Loyalty+pp.Health)*.5f,.85f,1.25f);}return Mathx.Clamp(m,.85f,1.55f);}
  public static float RecoveryMultiplier(Person p,GameDataCatalog data){if(p==null||data==null)return 1f;return Mathx.Clamp(SoldierProfile(p,data).Recovery*PersonProfile(p,data).Health,.7f,1.7f);}
  public static float OfficerPotentialMultiplier(Person p,GameDataCatalog data){return SoldierProfile(p,data).OfficerPotential;}

  public static int OfficialProposalBonus(Person p,GameDataCatalog data,string category){if(p==null||data==null)return 0;int b=0;foreach(string id in p.TraitIds){TraitDef d;if(!data.OfficialTraits.TryGetValue(id,out d))continue;if(!string.IsNullOrEmpty(d.Domain)&&!string.IsNullOrEmpty(category)&&(d.Domain.IndexOf(category)>=0||category.IndexOf(d.Domain)>=0))b+=4;if(d.PolicyCap>0)b+=Math.Max(0,(d.PolicyCap-p.Stats.Administration)/8);}return Math.Min(12,b);}
  public static CommanderBehaviorProfile ApplyGeneralTraits(Person p,GameDataCatalog data,CommanderBehaviorProfile profile){if(p==null||data==null||profile==null)return profile;foreach(string id in p.TraitIds){TraitDef d;if(!data.GeneralTraits.TryGetValue(id,out d))continue;string t=(d.Behavior??"")+(d.Effect??"")+(d.Category??"");string cat=d.Category??"";float pct=Math.Abs(FirstPercent(d.Effect));if(pct<=0)pct=.04f;
    if(cat=="治军"){profile.Discipline+=pct;profile.Cohesion+=pct*.8f;profile.CommandDelay*=Math.Max(.72f,1f-pct*.65f);}
    else if(cat=="决断"){profile.ReserveUse+=pct;profile.FlankSearch+=pct*.55f;profile.CommandDelay*=Math.Max(.75f,1f-pct*.5f);}
    else if(cat=="进攻"){profile.InfantryCombat+=pct*.65f;profile.FlankSearch+=pct*.65f;profile.Pursuit+=pct*.45f;}
    else if(cat=="防守"){profile.Defense+=pct;profile.RoutResistance+=pct*.7f;profile.ReformSpeed+=pct*.5f;}
    else if(cat=="骑战"){profile.CavalryControl+=pct;profile.FlankSearch+=pct*.45f;}
    else if(cat=="远程"){profile.RangedCombat+=pct;profile.Discipline+=pct*.3f;}
    else if(cat=="侦察"){profile.Scout+=pct;profile.Ambush+=pct*.35f;}
    else if(cat=="奇袭"){profile.Ambush+=pct;profile.FlankSearch+=pct*.45f;}
    else if(cat=="后勤"){profile.SupplyEfficiency+=pct;profile.FatigueEfficiency+=pct*.55f;}
    else if(cat=="地形"){profile.TerrainAdapt+=pct;profile.MarchSpeed+=pct*.25f;}
    else if(cat=="政治"){profile.Morale+=pct*.5f;profile.Discipline+=pct*.35f;}
    else if(cat=="个人风格"){profile.RetreatDiscipline+=pct*.55f;profile.ReserveUse+=pct*.35f;}
    if(t.IndexOf("命令")>=0||t.IndexOf("军纪")>=0){profile.CommandDelay*=Math.Max(.72f,1f-pct);profile.Cohesion+=pct;}if(t.IndexOf("侧翼")>=0||t.IndexOf("包围")>=0)profile.FlankSearch+=pct;if(t.IndexOf("追击")>=0)profile.Pursuit+=pct;if(t.IndexOf("伏击")>=0)profile.Ambush+=pct;if(t.IndexOf("后勤")>=0||t.IndexOf("粮")>=0)profile.SupplyEfficiency+=pct;if(t.IndexOf("士气")>=0||t.IndexOf("军心")>=0)profile.Morale+=pct;if(t.IndexOf("城")>=0)profile.Siege+=pct;if(t.IndexOf("侦察")>=0)profile.Scout+=pct;if(t.IndexOf("预备队")>=0||t.IndexOf("战机")>=0)profile.ReserveUse+=pct;if(t.IndexOf("骑")>=0)profile.CavalryControl+=pct;if(t.IndexOf("远程")>=0||t.IndexOf("射")>=0)profile.RangedCombat+=pct;if(t.IndexOf("守")>=0)profile.Defense+=pct;if(t.IndexOf("撤退")>=0)profile.RetreatDiscipline+=pct;}
   PersonBehaviorProfile baseProfile=PersonProfile(p,data);profile.Discipline*=Mathx.Clamp((baseProfile.Loyalty+baseProfile.Work)*.5f,.85f,1.25f);profile.FlankSearch*=baseProfile.Risk>1f?Mathx.Clamp(baseProfile.Risk,.9f,1.2f):1f;return profile;}
 }
}
