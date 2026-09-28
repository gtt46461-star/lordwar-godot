using System;
using LordWar.Data;
namespace LordWar.Governance {
 public sealed class PolicyRuntimeProfile {
  // Core multipliers already consumed by the simulation.
  public float Agriculture=1f,PopulationGrowth=1f,MerchantTax=1f,TreasuryEfficiency=1f,FamilyStability=1f,Administration=1f,Training=1f,EquipmentDurability=1f,HorseGrowth=1f,Supply=1f,Fortification=1f,RoadSpeed=1f,PublicOrder=1f,Diplomacy=1f,Promotion=1f;
  // Policy semantics that must not collapse into a single generic category bonus.
  public float FarmlandCapacity=1f,DisasterResilience=1f,FoodStorage=1f,JobMatching=1f,HousingCapacity=1f,ConstructionSpeed=1f;
  public float MerchantInvestmentCapacity=1f,MerchantLoyalty=1f,ArmyFinanceReliability=1f,DebtGrace=1f,ArmyUpkeepCost=1f;
  public float RecruitmentSpeed=1f,VeteranRetention=1f,CommandResponse=1f,MilitaryCapacity=1f,SoldierMorale=1f;
  public float EquipmentRecovery=1f,RepairEfficiency=1f,CavalryTraining=1f,HorseSurvival=1f;
  public float ConvoySafety=1f,BridgeSpeed=1f,FatigueRecovery=1f,SiegeDefense=1f,WallRepair=1f,EarlyWarning=1f,BuildingDurability=1f;
  public float MedicalRecovery=1f,RebuildSpeed=1f,CaptiveRecovery=1f,Intelligence=1f,AlliedResponse=1f,LootToTreasury=1f;
  public int EquipmentQualityBonus;
  public float MonthlyMaintenance;

  static bool Has(string text,string key){return !string.IsNullOrEmpty(text)&&text.IndexOf(key,StringComparison.Ordinal)>=0;}
  static bool Any(string text,params string[] keys){if(string.IsNullOrEmpty(text))return false;for(int i=0;i<keys.Length;i++)if(Has(text,keys[i]))return true;return false;}
  static float LargestPercent(string text){
   if(string.IsNullOrEmpty(text))return .05f;int best=0;
   for(int i=0;i<text.Length;i++){
    if(text[i]<'0'||text[i]>'9')continue;int n=0,j=i;
    while(j<text.Length&&text[j]>='0'&&text[j]<='9'){n=n*10+(text[j]-'0');j++;}
    if(j<text.Length&&text[j]=='%')best=Math.Max(best,n);i=j;
   }
   return Mathx.Clamp((best<=0?5:best)/100f,.01f,.50f);
  }
  static void Add(ref float target,float amount,float weight=1f){target+=amount*weight;}
  static void ReduceCost(ref float target,float amount,float weight=1f){target*=Math.Max(.65f,1f-amount*weight);}
  static void IncreaseCost(ref float target,float amount,float weight=1f){target*=Math.Min(1.60f,1f+amount*weight);}

  public void Apply(PolicyDef d){
   if(d==null)return;string cat=d.Category??"",e=d.EffectText??"",cost=d.CostText??"";float v=LargestPercent(e);MonthlyMaintenance+=Math.Max(0,d.Monthly);
   // Broad policy family effects provide continuity even for policies whose wording is qualitative.
   if(Has(cat,"农业")){Add(ref Agriculture,v,.35f);Add(ref DisasterResilience,v,.08f);}
   else if(Has(cat,"人口")){Add(ref PopulationGrowth,v,.28f);Add(ref JobMatching,v,.08f);}
   else if(Any(cat,"商业","市场")){Add(ref MerchantTax,v,.32f);Add(ref MerchantInvestmentCapacity,v,.10f);}
   else if(Has(cat,"财政")){Add(ref TreasuryEfficiency,v,.30f);Add(ref ArmyFinanceReliability,v,.10f);}
   else if(Has(cat,"贵族"))Add(ref FamilyStability,v,.30f);
   else if(Has(cat,"官员"))Add(ref Administration,v,.30f);
   else if(Has(cat,"基础军制")){Add(ref Training,v,.28f);Add(ref CommandResponse,v,.10f);}
   else if(Has(cat,"军械")){Add(ref EquipmentDurability,v,.28f);Add(ref RepairEfficiency,v,.10f);}
   else if(Has(cat,"骑兵")){Add(ref HorseGrowth,v,.34f);Add(ref CavalryTraining,v,.12f);}
   else if(Any(cat,"军粮","后勤")){Add(ref Supply,v,.36f);Add(ref ConvoySafety,v,.10f);}
   else if(Has(cat,"城防")){Add(ref Fortification,v,.32f);Add(ref SiegeDefense,v,.14f);}
   else if(Has(cat,"道路")){Add(ref RoadSpeed,v,.32f);Add(ref BuildingDurability,v,.08f);}
   else if(Has(cat,"治安")){Add(ref PublicOrder,v,.32f);Add(ref MedicalRecovery,v,.08f);}
   else if(Has(cat,"外交")){Add(ref Diplomacy,v,.30f);Add(ref Intelligence,v,.08f);}
   else if(Has(cat,"军功")){Add(ref Promotion,v,.32f);Add(ref SoldierMorale,v,.08f);}

   // Agriculture and population.
   if(Has(e,"可耕地容量"))Add(ref FarmlandCapacity,v);
   if(Any(e,"旱灾减产","灾年死亡","战乱流民死亡","灾民死亡","饥荒民心损失"))Add(ref DisasterResilience,v);
   if(Any(e,"亩产","均产","农事效率","边城粮食自给"))Add(ref Agriculture,v,.75f);
   if(Any(e,"粮食腐坏","库存周转","仓储容量","仓容"))Add(ref FoodStorage,v,.75f);
   if(Any(e,"就业","岗位匹配","劳力可用"))Add(ref JobMatching,v,.70f);
   if(Any(e,"移民吸引","生育倾向","流民"))Add(ref PopulationGrowth,v,.65f);
   if(Has(e,"住房上限"))Add(ref HousingCapacity,v);
   if(Any(e,"大型工程劳力","工程疲劳"))Add(ref ConstructionSpeed,v,.70f);

   // Economy, merchants and military finance.
   if(Any(e,"商税收入","税籍完整","税基识别","封地税收","奢侈品税收"))Add(ref MerchantTax,v,.85f);
   if(Any(e,"贸易","商路流量","大宗交易","商户恢复"))Add(ref MerchantTax,v,.45f);
   if(Any(e,"商人投资军队上限","商人军资融资","紧急可借款上限"))Add(ref MerchantInvestmentCapacity,v);
   if(Any(e,"军费拖欠概率","预算偏差","政策超支","工程超支"))Add(ref ArmyFinanceReliability,v,.80f);
   if(Any(e,"债务违约","欠款展期"))Add(ref DebtGrace,v);
   if(Has(e,"战利品入库"))Add(ref LootToTreasury,v);

   // Military organisation and personnel.
   if(Any(e,"训练等级","基础纪律","训练经验"))Add(ref Training,v,.95f);
   if(Any(e,"集结","补员速度"))Add(ref RecruitmentSpeed,v,.90f);
   if(Any(e,"老兵退伍率","老兵留城"))Add(ref VeteranRetention,v,.90f);
   if(Has(e,"命令延迟"))Add(ref CommandResponse,v);
   if(Has(e,"军队软上限"))Add(ref MilitaryCapacity,v);
   if(Any(e,"军旗士气","特殊兵种士气","军纪优秀部队士气","阵亡家属忠诚","重伤兵忠诚"))Add(ref SoldierMorale,v,.80f);
   if(Any(e,"晋升质量","军功入爵","军功晋升"))Add(ref Promotion,v,.85f);

   // Equipment and horses.
   if(Any(e,"枪兵补装","盾甲覆盖率","缺装率","装备获得率","装备上限","配发优先级")){EquipmentQualityBonus=Math.Min(2,EquipmentQualityBonus+1);}
   if(Any(e,"耐久损耗","马具耐久","农具损耗"))Add(ref EquipmentDurability,v,.90f);
   if(Has(e,"可回收装备"))Add(ref EquipmentRecovery,v);
   if(Any(e,"维修","军械维修"))Add(ref RepairEfficiency,v,.75f);
   if(Any(e,"军马繁育","军马市场供给","骑兵补马","贵族提供军马","牲畜产出"))Add(ref HorseGrowth,v,.85f);
   if(Any(e,"冬季马匹死亡","伤马存活"))Add(ref HorseSurvival,v);
   if(Has(e,"骑兵训练"))Add(ref CavalryTraining,v);

   // Logistics, roads and siege.
   if(Any(e,"口粮浪费","补给半径","道路补给速度","采购效率","驮运能力","驻军补给"))Add(ref Supply,v,.85f);
   if(Any(e,"辎重拥堵","单仓被袭损失","辎重遇袭存活","商队遇袭损失"))Add(ref ConvoySafety,v,.85f);
   if(Has(e,"渡河等待"))Add(ref BridgeSpeed,v);
   if(Has(e,"远征疲劳恢复"))Add(ref FatigueRecovery,v);
   if(Any(e,"主路行军","商旅速度","雪地道路速度","森林行军惩罚","山地绕路","泥泞道路惩罚"))Add(ref RoadSpeed,v,.90f);
   if(Any(e,"桥梁耐久","建筑耐久"))Add(ref BuildingDurability,v,.90f);
   if(Any(e,"城墙覆盖率","塔楼射界","桥头防御","内城退守")){Add(ref Fortification,v,.75f);Add(ref SiegeDefense,v,.55f);}
   if(Any(e,"战时修复","城墙破损发现"))Add(ref WallRepair,v);
   if(Any(e,"敌军预警提前","边境预警"))Add(ref EarlyWarning,v);

   // Public order, medicine, diplomacy and intelligence.
   if(Any(e,"抢劫","犯罪","民心惩罚","商民冲突","反叛风险"))Add(ref PublicOrder,v,.75f);
   if(Any(e,"疾病恢复","伤兵"))Add(ref MedicalRecovery,v,.85f);
   if(Any(e,"战后建筑恢复","战后商户恢复"))Add(ref RebuildSpeed,v,.85f);
   if(Any(e,"高价值俘虏回收","战俘"))Add(ref CaptiveRecovery,v);
   if(Any(e,"敌境情报","边境预警"))Add(ref Intelligence,v,.90f);
   if(Any(e,"盟国援军响应","盟军补给效率"))Add(ref AlliedResponse,v,.90f);

   // Explicit risks/costs must also change simulation rather than living only in UI text.
   float cv=LargestPercent(cost);
   if(Any(cost,"军费固定支出","军费与粮耗同步提高","军械开支增加","荣誉津贴增加","军官维护费增加"))IncreaseCost(ref ArmyUpkeepCost,cv,.75f);
   if(Any(cost,"国库收入下降","地方税收下降","短期减税"))ReduceCost(ref TreasuryEfficiency,cv,.55f);
   if(Any(cost,"商人忠诚","富商忠诚","商人议价权"))ReduceCost(ref MerchantLoyalty,cv,.75f);
   if(Any(cost,"贵族不满","豪族忠诚","大族抵触","其他贵族恐惧"))ReduceCost(ref FamilyStability,cv,.70f);
   if(Any(cost,"训练时间","晋升速度放缓"))ReduceCost(ref Training,cv,.45f);
   if(Any(cost,"劳动力短缺","民间生产","内地兵力相对减少"))ReduceCost(ref JobMatching,cv,.45f);
   if(Any(cost,"行军会多停留","交通拥堵"))ReduceCost(ref RoadSpeed,cv,.35f);
   if(Any(cost,"马匹库存下降","育种周期更长"))ReduceCost(ref HorseGrowth,cv,.35f);
  }
 }
}
