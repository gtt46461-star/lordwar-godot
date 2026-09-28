using System;
using System.Collections.Generic;
namespace LordWar.Siege {
 [Serializable] public sealed class SiegeState {
  public string CityId,AttackerArmyId;
  public int Days;
  public float WallIntegrity=100,GateIntegrity=100,GarrisonMorale=100,CivilianFoodDays=10;
  public bool Blockaded,Surrendered,Captured,Breached;
  public int LastWallDamage,LastGateDamage,LastAssaultBattleDay=-1;
 }

 public sealed class SiegeOwner {
  readonly Dictionary<string,SiegeState> _sieges=new Dictionary<string,SiegeState>();
  public IEnumerable<SiegeState> All(){return _sieges.Values;}
  public void Restore(IEnumerable<SiegeState> source){_sieges.Clear();if(source==null)return;foreach(var s in source)if(s!=null&&!string.IsNullOrEmpty(s.CityId))_sieges[s.CityId]=s;}
  public SiegeState Get(string cityId){SiegeState s;return !string.IsNullOrEmpty(cityId)&&_sieges.TryGetValue(cityId,out s)?s:null;}
  public bool IsBlockaded(string cityId){SiegeState s=Get(cityId);return s!=null&&s.Blockaded&&!s.Captured;}
  public void End(string cityId){if(!string.IsNullOrEmpty(cityId))_sieges.Remove(cityId);}

  public SiegeState Begin(Army attacker,City city,IEnumerable<Building> fortifications,float garrisonMorale){
   if(attacker==null||city==null)return null;
   SiegeState s;
   if(!_sieges.TryGetValue(city.Id,out s)){
    s=new SiegeState{CityId=city.Id,AttackerArmyId=attacker.Id,Blockaded=true,CivilianFoodDays=Math.Max(1,city.Food/(float)Math.Max(1,city.PersonIds.Count)),GarrisonMorale=Mathx.Clamp(garrisonMorale,10f,100f)};
    _sieges[city.Id]=s;
   } else s.AttackerArmyId=attacker.Id;
   SyncIntegrity(s,fortifications);
   return s;
  }
  public SiegeState Begin(Army attacker,City city){return Begin(attacker,city,null,100f);}

  public void TickDay(SiegeState s,Army attacker,City city,bool assault,int engineering,IList<Building> fortifications,int attackerSoldiers,int defenderSoldiers){
   if(s==null||attacker==null||city==null||s.Captured)return;
   s.Days++;s.LastGateDamage=0;s.LastWallDamage=0;
   if(s.Blockaded){
    float consumption=1f+Math.Min(.55f,Math.Max(0,city.PersonIds.Count-80)/400f);
    s.CivilianFoodDays=Math.Max(0,s.CivilianFoodDays-consumption);
   }
   if(s.CivilianFoodDays<=0)s.GarrisonMorale=Math.Max(0,s.GarrisonMorale-6f);
   else if(s.CivilianFoodDays<2)s.GarrisonMorale=Math.Max(0,s.GarrisonMorale-2f);
   if(attacker.FoodDays<=1f){attacker.Morale=Math.Max(0,attacker.Morale-3f);assault=false;}

   if(assault){
    float numbers=(float)Math.Max(1,attackerSoldiers)/Math.Max(1,defenderSoldiers);
    float damage=3f+engineering*.08f+attacker.Squads.Count*.15f+Math.Min(5f,Math.Max(0f,numbers-1f)*2f);
    if(DamageFortifications(fortifications,true,damage,out s.LastGateDamage)){}
    else DamageFortifications(fortifications,false,damage*.55f,out s.LastWallDamage);
    attacker.Morale=Math.Max(0,attacker.Morale-1.5f);
    attacker.Fatigue=Math.Min(100,attacker.Fatigue+3f);
    s.GarrisonMorale=Math.Max(0,s.GarrisonMorale-(1.5f+Math.Max(0f,numbers-1f)));
   }

   SyncIntegrity(s,fortifications);
   bool noFortification=s.GateIntegrity<=0f&&s.WallIntegrity<=0f;
   s.Breached=noFortification||s.GateIntegrity<=0f||s.WallIntegrity<28f;
   float powerRatio=(float)Math.Max(1,attackerSoldiers)/Math.Max(1,defenderSoldiers);
   if(s.GarrisonMorale<12f&&s.CivilianFoodDays<=0)s.Surrendered=true;
   // A breached wall is only an opening. Occupation after a breach must be decided by
   // a real field/garrison battle in GameWorld. Only an explicit surrender can skip combat.
   if(s.Surrendered&&attacker.Morale>20f)s.Captured=true;
  }
  public void TickDay(SiegeState s,Army attacker,City city,bool assault,int engineering){TickDay(s,attacker,city,assault,engineering,null,Math.Max(1,attacker==null?1:attacker.Squads.Count*8),1);}
  public bool ShouldOfferSurrender(SiegeState s){return s!=null&&!s.Captured&&(s.GarrisonMorale<25||s.CivilianFoodDays<=1)&&s.Days>=2;}

  static bool DamageFortifications(IList<Building> forts,bool gate,float damage,out int dealt){
   dealt=0;if(forts==null||forts.Count==0)return false;
   Building target=null;float lowest=999f;
   for(int i=0;i<forts.Count;i++){
    Building b=forts[i];if(b==null||b.Ruined||b.Durability<=0)continue;
    bool match=gate?b.Kind==BuildingKind.Gate:(b.Kind==BuildingKind.Wall||b.Kind==BuildingKind.Tower);
    if(!match)continue;float pct=b.Durability/(float)Math.Max(1,b.MaxDurability);if(pct<lowest){lowest=pct;target=b;}
   }
   if(target==null)return false;
   int raw=Math.Max(1,(int)Math.Round(damage*Math.Max(1,target.MaxDurability)/100f));
   int before=target.Durability;target.Durability=Math.Max(0,target.Durability-raw);dealt=before-target.Durability;
   if(target.Durability<=0){target.Ruined=true;target.Complete=false;target.Name="废墟·"+ChineseText.Building(target.Kind);}
   return true;
  }
  static void SyncIntegrity(SiegeState s,IEnumerable<Building> forts){
   if(s==null||forts==null)return;
   float wall=0f,gate=0f;int wn=0,gn=0;
   foreach(Building b in forts){if(b==null)continue;float pct=100f*b.Durability/Math.Max(1,b.MaxDurability);if(b.Kind==BuildingKind.Gate){gate+=pct;gn++;}else if(b.Kind==BuildingKind.Wall||b.Kind==BuildingKind.Tower){wall+=pct;wn++;}}
   s.GateIntegrity=gn==0?0f:Mathx.Clamp(gate/gn,0f,100f);
   s.WallIntegrity=wn==0?0f:Mathx.Clamp(wall/wn,0f,100f);
  }
 }
}
