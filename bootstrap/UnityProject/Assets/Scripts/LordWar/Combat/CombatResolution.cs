using System;
using System.Collections.Generic;
using LordWar.Military;
using LordWar.Data;

namespace LordWar.Combat {
    public sealed class CombatResolution {
        sealed class Combatant {
            public Person Person;
            public bool Ranged, Cavalry;
            public string SquadId;
            public SpecialUnitBehaviorProfile Special;
        }

        readonly Dictionary<string,Person> _people;
        readonly Dictionary<string,EquipmentInstance> _equipment;
        readonly DeterministicRandom _rng;
        readonly GameDataCatalog _data;

        public CombatResolution(Dictionary<string,Person> people, Dictionary<string,EquipmentInstance> equipment, int seed, GameDataCatalog data=null) {
            _people=people; _equipment=equipment; _data=data; _rng=new DeterministicRandom(seed^0x4b1d);
        }

        float EquipmentAttack(Person p) {
            float a=0;
            foreach(string id in p.EquipmentIds) {
                EquipmentInstance e;
                if(_equipment.TryGetValue(id,out e)) a+=e.Attack*EquipmentDurabilitySystem.Effectiveness(e);
            }
            return a;
        }

        float EquipmentDefense(Person p) {
            float d=0;
            foreach(string id in p.EquipmentIds) {
                EquipmentInstance e;
                if(_equipment.TryGetValue(id,out e)) d+=e.Defense*EquipmentDurabilitySystem.Effectiveness(e);
            }
            return d;
        }

        public int Exchange(Person attacker, Person defender, bool charge, bool ranged, float rangedAccuracy=1f) {
            return Exchange(attacker,defender,charge,ranged,rangedAccuracy,1f,1f,1f,1f,1f,1f,null);
        }

        EquipmentInstance FindEquipment(Person p,string slot,string nameContains=null) {
            if(p==null||p.EquipmentIds==null) return null;
            foreach(string id in p.EquipmentIds) {
                EquipmentInstance e;
                if(!_equipment.TryGetValue(id,out e)||e==null||e.Durability<=0) continue;
                if(!string.IsNullOrEmpty(slot)&&e.Slot!=slot) continue;
                if(!string.IsNullOrEmpty(nameContains)) {
                    string n=(e.DisplayName??"")+(e.TemplateId??"");
                    if(n.IndexOf(nameContains,StringComparison.Ordinal)<0) continue;
                }
                return e;
            }
            return null;
        }

        bool HasThrustWeapon(Person p) {
            EquipmentInstance e=FindEquipment(p,"主手");
            if(e==null) return false;
            string n=(e.DisplayName??"")+(e.TemplateId??"");
            return n.IndexOf("枪",StringComparison.Ordinal)>=0||n.IndexOf("矛",StringComparison.Ordinal)>=0||n.IndexOf("槊",StringComparison.Ordinal)>=0||n.IndexOf("戟",StringComparison.Ordinal)>=0||n.IndexOf("剑",StringComparison.Ordinal)>=0;
        }

        CombatAttackMode ChooseAttackMode(Person attacker,bool charge,bool ranged,float stamina) {
            if(ranged) return CombatAttackMode.Ranged;
            if(charge) return CombatAttackMode.CavalryCharge;
            float fatigue=Mathx.Clamp(attacker.Stats.Fatigue/100f,0f,1f);
            float heavyChance=Mathx.Clamp(.10f+attacker.Stats.Martial*.0022f+stamina*.08f-fatigue*.12f,.08f,.34f);
            float thrustChance=HasThrustWeapon(attacker) ? .34f : .12f;
            float roll=_rng.Next01();
            if(roll<heavyChance) return CombatAttackMode.Heavy;
            if(roll<heavyChance+thrustChance) return CombatAttackMode.Thrust;
            return CombatAttackMode.Light;
        }

        CombatDefenseReaction ChooseDefenseReaction(Person defender,bool ranged,SoldierBehaviorProfile traits,VeteranBehaviorProfile veteran,out EquipmentInstance reactionEquipment) {
            reactionEquipment=null;
            float ready=Mathx.Clamp((defender.Stats.Stamina-defender.Stats.Fatigue)/100f,.05f,1f);
            EquipmentInstance shield=FindEquipment(defender,"副手","盾");
            EquipmentInstance weapon=FindEquipment(defender,"主手");
            float blockSkill=TraitEffectEngine.SoldierBlock(defender,_data)*veteran.Block;
            float dodgeChance=Mathx.Clamp(.035f+ready*.12f+(traits.Stamina-1f)*.09f,.025f,.24f);
            float shieldChance=shield==null?0f:Mathx.Clamp(.12f+(defender.Stats.Defense+shield.Defense)*.0028f*blockSkill,.10f,.55f);
            float parryChance=(ranged||weapon==null)?0f:Mathx.Clamp(.06f+(defender.Stats.Defense+defender.Stats.Martial)*.0015f*blockSkill,.05f,.31f);
            if(shield!=null&&_rng.Chance(shieldChance)){reactionEquipment=shield;return CombatDefenseReaction.ShieldBlock;}
            if(_rng.Chance(dodgeChance)) return CombatDefenseReaction.Dodge;
            if(weapon!=null&&!ranged&&_rng.Chance(parryChance)){reactionEquipment=weapon;return CombatDefenseReaction.Parry;}
            return CombatDefenseReaction.None;
        }

        void RecordAttack(BattleReport report,CombatAttackMode mode) {
            if(report==null)return;
            if(mode==CombatAttackMode.Light)report.LightAttacks++;
            else if(mode==CombatAttackMode.Heavy)report.HeavyAttacks++;
            else if(mode==CombatAttackMode.Thrust)report.ThrustAttacks++;
            else if(mode==CombatAttackMode.Ranged)report.RangedAttacks++;
            else if(mode==CombatAttackMode.CavalryCharge)report.CavalryCharges++;
        }

        void RecordDefense(BattleReport report,CombatDefenseReaction reaction) {
            if(report==null)return;
            if(reaction==CombatDefenseReaction.Dodge)report.Dodges++;
            else if(reaction==CombatDefenseReaction.Parry)report.Parries++;
            else if(reaction==CombatDefenseReaction.ShieldBlock)report.ShieldBlocks++;
        }

        string AttackText(CombatAttackMode mode) {
            if(mode==CombatAttackMode.Heavy)return "重击";
            if(mode==CombatAttackMode.Thrust)return "刺击";
            if(mode==CombatAttackMode.Ranged)return "远程射击";
            if(mode==CombatAttackMode.CavalryCharge)return "骑兵冲锋";
            return "轻击";
        }

        string DefenseText(CombatDefenseReaction reaction) {
            if(reaction==CombatDefenseReaction.Dodge)return "闪避";
            if(reaction==CombatDefenseReaction.Parry)return "武器格挡";
            if(reaction==CombatDefenseReaction.ShieldBlock)return "盾挡";
            return "承受攻击";
        }

        int Exchange(Person attacker, Person defender, bool charge, bool ranged, float rangedAccuracy,
                     float attackerCombat, float defenderCombat, float attackerDiscipline,
                     float defenderMedical, float attackerCapture, float fatigueEfficiency,
                     BattleReport report) {
            if(attacker==null||defender==null||!attacker.Alive||!defender.Alive) return 0;
            SoldierBehaviorProfile attackerTraits=TraitEffectEngine.SoldierProfile(attacker,_data);
            SoldierBehaviorProfile defenderTraits=TraitEffectEngine.SoldierProfile(defender,_data);
            VeteranBehaviorProfile attackerVeteran=VeteranEffectEngine.Build(attacker);
            VeteranBehaviorProfile defenderVeteran=VeteranEffectEngine.Build(defender);
            float stamina=Mathx.Clamp((attacker.Stats.Stamina-attacker.Stats.Fatigue)*attackerTraits.Stamina,15,120)/100f;
            attackerDiscipline*=attackerTraits.Discipline;
            defenderCombat*=Mathx.Clamp(defenderTraits.Defense,.8f,1.45f)*Mathx.Clamp(defenderVeteran.Cohesion,.95f,1.22f);
            fatigueEfficiency*=Mathx.Clamp(attackerTraits.Stamina,.75f,1.45f)*attackerVeteran.FatigueEfficiency;
            CombatAttackMode mode=ChooseAttackMode(attacker,charge,ranged,stamina);
            RecordAttack(report,mode);
            if(report!=null){report.LastActorId=attacker.Id;report.LastTargetId=defender.Id;report.LastAttackAction=AttackText(mode);report.LastDefenseAction="";report.LastDamage=0;report.ActionSequence++;report.LastActionText=attacker.Name+"向"+defender.Name+"发动"+AttackText(mode);}
            float accuracyMul=1f,damageMul=1f,armorPenetration=1f,fatigueCost=3f;
            if(mode==CombatAttackMode.Light){accuracyMul=1.08f;damageMul=.82f;fatigueCost=2f;}
            else if(mode==CombatAttackMode.Heavy){accuracyMul=.82f;damageMul=1.42f;armorPenetration=.70f;fatigueCost=6f;}
            else if(mode==CombatAttackMode.Thrust){accuracyMul=.96f;damageMul=1.16f;armorPenetration=.60f;fatigueCost=4f;}
            else if(mode==CombatAttackMode.Ranged){accuracyMul=Mathx.Clamp(rangedAccuracy*attackerTraits.Ranged,.35f,1.28f);damageMul=1.02f;armorPenetration=.82f;fatigueCost=2.5f;}
            else if(mode==CombatAttackMode.CavalryCharge){accuracyMul=.90f;damageMul=1.62f*Mathx.Clamp(attackerTraits.Riding,.82f,1.35f);armorPenetration=.72f;fatigueCost=8f;}
            float atk=(attacker.Stats.Martial*.45f+EquipmentAttack(attacker)*.75f)*attackerCombat;
            float def=(defender.Stats.Defense*.42f+EquipmentDefense(defender)*.9f)*defenderCombat;
            float hit=(.58f+(attacker.Stats.Martial-defender.Stats.Defense)*.003f)*TraitEffectEngine.SoldierHit(attacker,_data)*attackerVeteran.Hit;
            hit*=Mathx.Clamp(.92f+.08f*attackerDiscipline,.86f,1.08f)*accuracyMul;
            if(!_rng.Chance(Mathx.Clamp(hit,.10f,.94f))) {
                attacker.Stats.Fatigue=Math.Min(100,attacker.Stats.Fatigue+Math.Max(1f,fatigueCost*.55f)/Math.Max(.7f,fatigueEfficiency));
                if(report!=null){report.LastDefenseAction="未命中";report.LastDamage=0;report.LastActionText=attacker.Name+"的"+AttackText(mode)+"未能命中"+defender.Name;}
                return 0;
            }
            EquipmentInstance reactionEquipment;
            CombatDefenseReaction reaction=ChooseDefenseReaction(defender,mode==CombatAttackMode.Ranged,defenderTraits,defenderVeteran,out reactionEquipment);
            RecordDefense(report,reaction);
            if(report!=null){report.LastDefenseAction=reaction==CombatDefenseReaction.None?"承受攻击":DefenseText(reaction);if(reaction!=CombatDefenseReaction.None)report.LastActionText=defender.Name+"以"+DefenseText(reaction)+"应对"+attacker.Name+"的"+AttackText(mode);}
            if(reaction==CombatDefenseReaction.Dodge) {
                defender.Stats.Fatigue=Math.Min(100,defender.Stats.Fatigue+1.5f);
                attacker.Stats.Fatigue=Math.Min(100,attacker.Stats.Fatigue+fatigueCost/Math.Max(.7f,fatigueEfficiency));
                return 0;
            }
            float raw=(4f+atk*.10f+_rng.Next01()*5f)*damageMul;
            if(reaction==CombatDefenseReaction.ShieldBlock) raw*=.28f;
            else if(reaction==CombatDefenseReaction.Parry) raw*=.46f;
            raw*=.75f+.25f*stamina;
            int damage=Math.Max(1,(int)Math.Round(raw-def*.018f*armorPenetration));
            defender.Stats.Life-=damage;
            if(report!=null){report.LastDamage=damage;report.LastActionText=attacker.Name+"以"+AttackText(mode)+"命中"+defender.Name+"，造成"+damage+"点伤害"+(reaction==CombatDefenseReaction.None?"":"（"+DefenseText(reaction)+"减伤）");}
            attacker.Stats.Fatigue=Math.Min(100,attacker.Stats.Fatigue+fatigueCost/Math.Max(.7f,fatigueEfficiency));
            defender.Stats.Fatigue=Math.Min(100,defender.Stats.Fatigue+(reaction==CombatDefenseReaction.None?2f:3f));
            WearRandomEquipment(attacker,mode==CombatAttackMode.Heavy||mode==CombatAttackMode.CavalryCharge?2:1);
            if(reactionEquipment!=null) EquipmentDurabilitySystem.Wear(reactionEquipment,reaction==CombatDefenseReaction.ShieldBlock?3:2,1f);
            else WearRandomEquipment(defender,1);
            if(defender.Stats.Life<=0) ResolveDowned(attacker,defender,defenderMedical,attackerCapture);
            return damage;
        }

        void WearRandomEquipment(Person p,int wear) {
            if(p.EquipmentIds.Count==0) return;
            string id=p.EquipmentIds[_rng.Range(0,p.EquipmentIds.Count)];
            EquipmentInstance e;
            if(_equipment.TryGetValue(id,out e)) EquipmentDurabilitySystem.Wear(e,wear,1f);
        }

        void ResolveDowned(Person killer, Person victim, float defenderMedical, float attackerCapture) {
            float medical=Mathx.Clamp(defenderMedical,.75f,1.8f);
            float capture=Mathx.Clamp(attackerCapture,.75f,1.8f);
            float fatalThreshold=Mathx.Clamp(.32f-(medical-1f)*.20f,.16f,.38f);
            float captureShare=Mathx.Clamp(.14f+(capture-1f)*.16f,.08f,.27f);
            float incapacitatedThreshold=Mathx.Clamp(.86f-captureShare+.14f,.68f,.88f);
            float heavyThreshold=Mathx.Clamp(fatalThreshold+.26f,.40f,.66f);
            float roll=_rng.Next01();
            victim.InjuryDays=0;
            if(roll<fatalThreshold) {
                victim.Alive=false; victim.Injury=InjuryState.Dead; killer.Kills++;
            } else if(roll<heavyThreshold) {
                victim.Injury=InjuryState.Heavy; victim.Stats.Life=1;
            } else if(roll<incapacitatedThreshold) {
                victim.Injury=InjuryState.Incapacitated; victim.Stats.Life=1;
            } else {
                victim.Injury=InjuryState.Captured; victim.Stats.Life=1;
            }
            killer.Experience+=10;
        }

        bool IsGeneralAlive(Army a) {
            Person g;
            return a!=null&&_people.TryGetValue(a.GeneralId,out g)&&g.Alive&&g.Injury!=InjuryState.Incapacitated&&g.Injury!=InjuryState.Captured;
        }

        void UnitFlags(string unitId,out bool ranged,out bool cavalry) {
            ranged=false; cavalry=false;
            if(_data==null) return;
            UnitDef u; SpecialUnitDef sp; string t="";
            if(_data.Units.TryGetValue(unitId,out u)) t=(u.Category??"")+(u.Role??"");
            else if(_data.SpecialUnits.TryGetValue(unitId,out sp)) t=(sp.Category??"")+(sp.Role??"")+(sp.Behavior??"");
            ranged=t.IndexOf("远程")>=0||t.IndexOf("弓")>=0||t.IndexOf("弩")>=0||t.IndexOf("射")>=0;
            cavalry=t.IndexOf("骑")>=0;
        }

        List<Combatant> Flatten(Army a) {
            var x=new List<Combatant>();
            foreach(var s in a.Squads) {
                if(s.TrainingRemainingDays>0) continue;
                bool ranged,cavalry; UnitFlags(s.UnitTemplateId,out ranged,out cavalry);SpecialUnitBehaviorProfile special=SpecialUnitBehaviorEngine.Build(s.UnitTemplateId,_data);
                foreach(string id in s.SoldierIds) {
                    Person p; if(_people.TryGetValue(id,out p)) x.Add(new Combatant{Person=p,Ranged=ranged,Cavalry=cavalry,SquadId=s.Id,Special=special});
                }
                if(!string.IsNullOrEmpty(s.OfficerId)) {
                    Person o; if(_people.TryGetValue(s.OfficerId,out o)) x.Add(new Combatant{Person=o,Ranged=ranged,Cavalry=cavalry,SquadId=s.Id,Special=special});
                }
            }
            return x;
        }

        bool IsDown(Person p) {
            return p==null||!p.Alive||p.Injury==InjuryState.Incapacitated||p.Injury==InjuryState.Captured||p.Injury==InjuryState.Dead;
        }

        float CombatScale(Combatant c, CommanderBehaviorProfile p, TerrainKind terrain, WeatherKind weather) {
            float role=c.Ranged?p.RangedCombat:(c.Cavalry?p.CavalryControl:p.InfantryCombat);SpecialUnitBehaviorProfile sp=c.Special??new SpecialUnitBehaviorProfile();
            return Mathx.Clamp(role*sp.FrontalCombat*CommanderSkillEffectEngine.TerrainCombatFactor(p,terrain)*CommanderSkillEffectEngine.WeatherCombatFactor(p,weather),.55f,1.95f);
        }

        int NextUsable(List<Combatant> list,int start,CommanderBehaviorProfile profile,int sec) {
            int i=start;
            while(i<list.Count&&IsDown(list[i].Person)) i++;
            if(i>=list.Count) return i;
            // Skilled reserve commanders rotate a near-exhausted front-line soldier before collapse.
            if(profile.ReserveUse>1.08f&&list[i].Person.Stats.Fatigue>82&&sec%6==0) {
                int look=Math.Min(list.Count,i+Math.Max(2,(int)Math.Round(profile.ReserveUse*3)));
                for(int j=i+1;j<look;j++) {
                    if(!IsDown(list[j].Person)&&list[j].Person.Stats.Fatigue+12<list[i].Person.Stats.Fatigue) return j;
                }
            }
            return i;
        }

        float ReserveRatio(List<Combatant> list,int current) {
            if(list.Count==0) return 0;
            return Mathx.Clamp((list.Count-current-1)/(float)list.Count,0f,.5f);
        }

        bool RoutByProfile(float morale,bool lastStand,CommanderBehaviorProfile profile) {
            if(lastStand) return false;
            float threshold=Mathx.Clamp(10f/Math.Max(.8f,profile.RoutResistance),5f,12f);
            return morale<threshold;
        }

        bool OrderlyWithdraw(float morale,bool lastStand,CommanderBehaviorProfile profile,float casualtyRate) {
            if(lastStand||profile.RetreatDiscipline<1.08f) return false;
            float threshold=Mathx.Clamp(18f+(profile.RetreatDiscipline-1f)*8f+(profile.CasualtyAversion-1f)*12f,18f,31f);
            float casualtyTrigger=Mathx.Clamp(.18f/Math.Max(1f,profile.CasualtyAversion),.10f,.18f);
            return morale<threshold&&casualtyRate>casualtyTrigger;
        }

        public BattleSession StartSession(Army a, Army b, int day, int maxSeconds,
                                          CommanderBehaviorProfile ap, CommanderBehaviorProfile bp,
                                          float rangedAccuracy=1f, bool aLastStand=false, bool bLastStand=false,
                                          TerrainKind terrain=TerrainKind.Grass, WeatherKind weather=WeatherKind.Clear,
                                          string location="", bool night=false) {
            if(a==null||b==null)throw new ArgumentNullException("battle army");
            if(ap==null)ap=new CommanderBehaviorProfile();
            if(bp==null)bp=new CommanderBehaviorProfile();
            var session=new BattleSession{
                Id=Ids.Next("SESSION"),AttackerArmyId=a.Id,DefenderArmyId=b.Id,LocationName=location??"",
                StartDay=day,MaxSeconds=Math.Max(0,maxSeconds),RangedAccuracy=rangedAccuracy,
                AttackerLastStand=aLastStand,DefenderLastStand=bLastStand,Night=night,Terrain=terrain,Weather=weather,
                AttackerFlankDelay=Math.Max(4,(int)Math.Round(10f*ap.CommandDelay)),
                DefenderFlankDelay=Math.Max(4,(int)Math.Round(10f*bp.CommandDelay))
            };
            session.Report=new BattleReport{Id=Ids.Next("BATTLE"),AttackerArmyId=a.Id,DefenderArmyId=b.Id,StartDay=day,LocationName=location??"",ResultText="交战中"};
            a.Morale=Mathx.Clamp(a.Morale+(ap.Morale-1f)*6f+(ap.Discipline-1f)*3f,0,100);
            b.Morale=Mathx.Clamp(b.Morale+(bp.Morale-1f)*6f+(bp.Discipline-1f)*3f,0,100);
            SpecialUnitBehaviorProfile aSpecial=SpecialUnitBehaviorEngine.ArmyProfile(a,_data),bSpecial=SpecialUnitBehaviorEngine.ArmyProfile(b,_data);
            float nightAmbushA=night?Mathx.Clamp(.92f+.12f*ap.NightCombat,.95f,1.25f):1f;float nightAmbushB=night?Mathx.Clamp(.92f+.12f*bp.NightCombat,.95f,1.25f):1f;
            bool aAmbush=_rng.Chance(Mathx.Clamp(CommanderSkillEffectEngine.AmbushChance(ap,bp,terrain)*aSpecial.ScoutAmbush/Math.Max(.85f,bSpecial.ScoutAmbush)*nightAmbushA,.01f,.48f));
            bool bAmbush=_rng.Chance(Mathx.Clamp(CommanderSkillEffectEngine.AmbushChance(bp,ap,terrain)*bSpecial.ScoutAmbush/Math.Max(.85f,aSpecial.ScoutAmbush)*nightAmbushB,.01f,.48f));
            if(aAmbush&&bAmbush){aAmbush=false;bAmbush=false;}
            if(aAmbush){b.Morale=Math.Max(0,b.Morale-3f*Mathx.Clamp(ap.Ambush,.8f,1.8f));b.Fatigue=Math.Min(100,b.Fatigue+4f);session.Report.Highlights.Add("进攻方侦察与伏击组织成功，守军进入接战时出现迟滞");}
            if(bAmbush){a.Morale=Math.Max(0,a.Morale-3f*Mathx.Clamp(bp.Ambush,.8f,1.8f));a.Fatigue=Math.Min(100,a.Fatigue+4f);session.Report.Highlights.Add("守方利用地形完成伏击，进攻军先头部队受扰");}
            a.Order=ArmyOrder.Engage;b.Order=ArmyOrder.Engage;
            session.RandomState=_rng.State;
            return session;
        }

        public bool StepSession(BattleSession session, Army a, Army b, int battleSeconds,
                                CommanderBehaviorProfile ap, CommanderBehaviorProfile bp) {
            if(session==null||a==null||b==null)return true;
            if(session.Completed)return true;
            _rng.State=session.RandomState;
            if(ap==null)ap=new CommanderBehaviorProfile();
            if(bp==null)bp=new CommanderBehaviorProfile();
            BattleReport report=session.Report??(session.Report=new BattleReport{Id=Ids.Next("BATTLE"),AttackerArmyId=a.Id,DefenderArmyId=b.Id,StartDay=session.StartDay,LocationName=session.LocationName});
            var aa=Flatten(a);var bb=Flatten(b);
            int ai=session.AttackerIndex,bi=session.DefenderIndex,sec=session.ElapsedSeconds;
            SpecialUnitBehaviorProfile aArmySpecial=SpecialUnitBehaviorEngine.ArmyProfile(a,_data),bArmySpecial=SpecialUnitBehaviorEngine.ArmyProfile(b,_data);
            bool aFlank=session.AttackerFlank,bFlank=session.DefenderFlank,aWithdraw=session.AttackerWithdraw,bWithdraw=session.DefenderWithdraw;
            int simulated=0,limit=Math.Max(1,battleSeconds);
            while(simulated<limit&&sec<session.MaxSeconds&&ai<aa.Count&&bi<bb.Count) {
                ai=NextUsable(aa,ai,ap,sec);bi=NextUsable(bb,bi,bp,sec);
                if(ai>=aa.Count||bi>=bb.Count)break;
                Combatant ca=aa[ai],cb=bb[bi];
                float aExposure=bb.Count==0?0f:bi/(float)bb.Count;
                float bExposure=aa.Count==0?0f:ai/(float)aa.Count;
                if(!aFlank&&sec>session.AttackerFlankDelay)aFlank=CommanderSkillEffectEngine.ShouldAttemptFlank(ap,aExposure*aArmySpecial.FlankMobility,a.Fatigue*aArmySpecial.FatigueCost,ReserveRatio(aa,ai));
                if(!bFlank&&sec>session.DefenderFlankDelay)bFlank=CommanderSkillEffectEngine.ShouldAttemptFlank(bp,bExposure*bArmySpecial.FlankMobility,b.Fatigue*bArmySpecial.FatigueCost,ReserveRatio(bb,bi));
                bool aCharge=ca.Cavalry&&((sec<Math.Round(6f+6f*ap.CavalryControl))||(aFlank&&sec%Math.Max(5,(int)Math.Round(10f/ap.CavalryControl))==0));
                bool bCharge=cb.Cavalry&&((sec<Math.Round(6f+6f*bp.CavalryControl))||(bFlank&&sec%Math.Max(5,(int)Math.Round(10f/bp.CavalryControl))==0));
                float aCombat=CombatScale(ca,ap,session.Terrain,session.Weather);
                float bCombat=CombatScale(cb,bp,session.Terrain,session.Weather);
                SpecialUnitBehaviorProfile caSpecial=ca.Special??new SpecialUnitBehaviorProfile(),cbSpecial=cb.Special??new SpecialUnitBehaviorProfile();
                if(cb.Cavalry)aCombat*=caSpecial.AntiCavalry;if(ca.Cavalry)bCombat*=cbSpecial.AntiCavalry;
                float aDefense=aCombat*caSpecial.MeleeDefense*(ca.Ranged?Mathx.Clamp(1f+(ap.RangedContactAvoidance-1f)*.35f,1f,1.22f):1f)*(IsGeneralAlive(a)?caSpecial.GeneralGuard:1f);
                float bDefense=bCombat*cbSpecial.MeleeDefense*cbSpecial.KeyDefense*(cb.Ranged?Mathx.Clamp(1f+(bp.RangedContactAvoidance-1f)*.35f,1f,1.22f):1f)*(IsGeneralAlive(b)?cbSpecial.GeneralGuard:1f);
                if(session.AttackerLastStand)aDefense/=Mathx.Clamp(ap.LastStandCasualty,1f,1.6f);
                if(session.DefenderLastStand)bDefense/=Mathx.Clamp(bp.LastStandCasualty,1f,1.6f);
                float aNight=session.Night?Mathx.Clamp(.62f+.32f*ap.NightCombat,.68f,1.08f):1f;float bNight=session.Night?Mathx.Clamp(.62f+.32f*bp.NightCombat,.68f,1.08f):1f;
                if(session.Night){aCombat*=Mathx.Clamp(.78f+.20f*ap.NightCombat,.82f,1.08f);bCombat*=Mathx.Clamp(.78f+.20f*bp.NightCombat,.82f,1.08f);}
                float aAccuracy=ca.Ranged?session.RangedAccuracy*Mathx.Clamp(ap.RangedCombat,.75f,1.45f)*caSpecial.RangedSafety*aNight:1f;
                float bAccuracy=cb.Ranged?session.RangedAccuracy*Mathx.Clamp(bp.RangedCombat,.75f,1.45f)*cbSpecial.RangedSafety*bNight:1f;
                Exchange(ca.Person,cb.Person,aCharge,ca.Ranged,aAccuracy,aCombat,bDefense,ap.Discipline,bp.Medical,ap.Capture,ap.FatigueEfficiency/Math.Max(.75f,caSpecial.FatigueCost),report);
                if(!IsDown(cb.Person))Exchange(cb.Person,ca.Person,bCharge,cb.Ranged,bAccuracy,bCombat,aDefense,bp.Discipline,ap.Medical,bp.Capture,bp.FatigueEfficiency/Math.Max(.75f,cbSpecial.FatigueCost),report);
                sec++;simulated++;
                if(IsDown(ca.Person))ai++;
                if(IsDown(cb.Person))bi++;
                float aCas=aa.Count==0?0:ai/(float)aa.Count;
                float bCas=bb.Count==0?0:bi/(float)bb.Count;
                a.Morale=MoraleSystem.Update(a.Morale,aCas,a.FoodDays<=0?1:0,bFlank,IsGeneralAlive(a),VeteranRatio(aa),session.AttackerLastStand,1f,a.Fatigue/Math.Max(.8f,ap.FatigueEfficiency));
                b.Morale=MoraleSystem.Update(b.Morale,bCas,b.FoodDays<=0?1:0,aFlank,IsGeneralAlive(b),VeteranRatio(bb),session.DefenderLastStand,1f,b.Fatigue/Math.Max(.8f,bp.FatigueEfficiency));
                a.Morale=Mathx.Clamp(a.Morale+(TraitMorale(aa)-1f)*2.5f+(ap.Morale-1f)*.55f+(ap.Discipline-1f)*.35f,0,100);
                b.Morale=Mathx.Clamp(b.Morale+(TraitMorale(bb)-1f)*2.5f+(bp.Morale-1f)*.55f+(bp.Discipline-1f)*.35f,0,100);
                if(sec%4==0){int ar,aw,br,bw;UpdateSquadMorale(a,a.Morale,session.AttackerLastStand,ap,out ar,out aw);UpdateSquadMorale(b,b.Morale,session.DefenderLastStand,bp,out br,out bw);a.Morale=Mathx.Clamp(a.Morale-ar*.9f-aw*.8f,0,100);b.Morale=Mathx.Clamp(b.Morale-br*.9f-bw*.8f,0,100);report.MoraleCollapseWaves+=aw+bw;}
                aWithdraw=OrderlyWithdraw(a.Morale,session.AttackerLastStand,ap,aCas);
                bWithdraw=OrderlyWithdraw(b.Morale,session.DefenderLastStand,bp,bCas);
                if(aWithdraw||bWithdraw||RoutByProfile(a.Morale,session.AttackerLastStand,ap)||RoutByProfile(b.Morale,session.DefenderLastStand,bp))break;
            }
            session.AttackerIndex=ai;session.DefenderIndex=bi;session.ElapsedSeconds=sec;
            session.AttackerFlank=aFlank;session.DefenderFlank=bFlank;session.AttackerWithdraw=aWithdraw;session.DefenderWithdraw=bWithdraw;
            UpdateProgressReport(session,a,b,aa,bb);
            bool ended=sec>=session.MaxSeconds||ai>=aa.Count||bi>=bb.Count||aWithdraw||bWithdraw||RoutByProfile(a.Morale,session.AttackerLastStand,ap)||RoutByProfile(b.Morale,session.DefenderLastStand,bp);
            if(ended)CompleteSession(session,a,b,ap,bp,aa,bb);
            session.RandomState=_rng.State;
            return session.Completed;
        }

        void UpdateProgressReport(BattleSession session,Army a,Army b,List<Combatant> aa,List<Combatant> bb) {
            BattleReport report=session.Report;report.DurationSeconds=session.ElapsedSeconds;
            Count(aa,out report.AttackerDead,out report.AttackerWounded,out report.AttackerCaptured);
            Count(bb,out report.DefenderDead,out report.DefenderWounded,out report.DefenderCaptured);
            report.Captured=report.AttackerCaptured+report.DefenderCaptured;report.AttackerFinalMorale=a.Morale;report.DefenderFinalMorale=b.Morale;
            report.AttackerOrderlyRetreat=session.AttackerWithdraw;report.DefenderOrderlyRetreat=session.DefenderWithdraw;
            if(!session.Completed)report.ResultText="交战中：进攻方士气 "+Math.Round(a.Morale)+" / 守方士气 "+Math.Round(b.Morale);
        }

        void CompleteSession(BattleSession session,Army a,Army b,CommanderBehaviorProfile ap,CommanderBehaviorProfile bp,List<Combatant> aa,List<Combatant> bb) {
            if(session.Completed)return;
            int aRouted,aWaves,bRouted,bWaves;UpdateSquadMorale(a,a.Morale,session.AttackerLastStand,ap,out aRouted,out aWaves);UpdateSquadMorale(b,b.Morale,session.DefenderLastStand,bp,out bRouted,out bWaves);
            BattleReport report=session.Report;report.AttackerRoutedSquads=aRouted;report.DefenderRoutedSquads=bRouted;report.MoraleCollapseWaves+=aWaves+bWaves;
            session.Completed=true;UpdateProgressReport(session,a,b,aa,bb);
            report.ResultText=a.Morale>b.Morale?"进攻方占优":"守方占优";
            if(session.AttackerFlank)report.Highlights.Add("进攻方统帅把预备队投入侧翼机动");
            if(session.DefenderFlank)report.Highlights.Add("守方统帅组织侧翼反击");
            if(session.AttackerWithdraw)report.Highlights.Add("进攻方在战线崩溃前组织有序撤退");
            if(session.DefenderWithdraw)report.Highlights.Add("守方在战线崩溃前组织有序撤退");
            if(report.AttackerRoutedSquads+report.DefenderRoutedSquads>0)report.Highlights.Add("战线出现局部溃败；低士气会向相邻小队传播并形成连锁崩溃");
            if(session.AttackerLastStand||session.DefenderLastStand)report.Highlights.Add("守城/绝境条件触发死战，部队极难正常溃败");
            if(session.Terrain==TerrainKind.Mountain||session.Terrain==TerrainKind.MountainPass||session.Terrain==TerrainKind.Forest||session.Terrain==TerrainKind.Marsh||session.Terrain==TerrainKind.River||session.Terrain==TerrainKind.Snow)report.Highlights.Add("地形适应与队形组织影响了交锋效率");
            if(session.Weather!=WeatherKind.Clear)report.Highlights.Add("天气适应影响射击、体力与战斗组织");if(session.Night)report.Highlights.Add("夜战降低视野与远程命中；夜袭、侦察和夜战统御会减轻组织惩罚");
            if(report.CavalryCharges>0)report.Highlights.Add("骑兵进行了真实冲锋接触，冲击造成更高体力与装备消耗");
            if(report.HeavyAttacks+report.ThrustAttacks>0)report.Highlights.Add("近战中出现重击与刺击，破甲、命中和体力代价各不相同");
            if(report.ShieldBlocks+report.Parries+report.Dodges>0)report.Highlights.Add("士兵根据真实装备和体力执行盾挡、格挡或闪避");
        }

        public BattleReport ResolveSkirmish(Army a, Army b, int day, int maxSeconds,
                                            CommanderBehaviorProfile ap, CommanderBehaviorProfile bp,
                                            float rangedAccuracy=1f, bool aLastStand=false, bool bLastStand=false,
                                            TerrainKind terrain=TerrainKind.Grass, WeatherKind weather=WeatherKind.Clear) {
            BattleSession session=StartSession(a,b,day,maxSeconds,ap,bp,rangedAccuracy,aLastStand,bLastStand,terrain,weather,"");
            while(!session.Completed)StepSession(session,a,b,Math.Max(1,maxSeconds),ap,bp);
            return session.Report;
        }

        public BattleReport ResolveSkirmish(Army a,Army b,int day,int maxSeconds) {
            return ResolveSkirmish(a,b,day,maxSeconds,new CommanderBehaviorProfile(),new CommanderBehaviorProfile());
        }

        public int ResolvePursuit(Army pursuer, Army routed, int strikes, CommanderBehaviorProfile profile) {
            var p=Flatten(pursuer); var r=Flatten(routed);
            if(p.Count==0||r.Count==0) return 0;
            if(profile==null) profile=new CommanderBehaviorProfile();
            int before=DeadCount(r);SpecialUnitBehaviorProfile armySpecial=SpecialUnitBehaviorEngine.ArmyProfile(pursuer,_data);
            int hits=Math.Min(strikes,Math.Max(3,(int)Math.Round(p.Count/3f*Mathx.Clamp(profile.Pursuit*armySpecial.PursuitCut,.7f,1.85f))));
            for(int i=0;i<hits;i++) {
                Combatant at=p[_rng.Range(0,p.Count)],df=r[_rng.Range(0,r.Count)];
                if(IsDown(at.Person)||IsDown(df.Person)) continue;
                SoldierBehaviorProfile pursuerTraits=TraitEffectEngine.SoldierProfile(at.Person,_data);
                bool charge=at.Cavalry&&_rng.Chance(Mathx.Clamp((.45f+.25f*(profile.CavalryControl-1f))*pursuerTraits.Pursuit*pursuerTraits.Riding,.30f,.82f));
                float role=(at.Cavalry?profile.CavalryControl:profile.InfantryCombat)*Mathx.Clamp(.9f+.1f*pursuerTraits.Pursuit,.9f,1.06f)*(at.Special==null?1f:at.Special.PursuitCut);
                Exchange(at.Person,df.Person,charge,false,1f,role,1f,profile.Discipline,1f,profile.Capture,profile.FatigueEfficiency,null);
                float pursuitChance=Mathx.Clamp((profile.Pursuit*pursuerTraits.Pursuit-1f)*.42f,0,.32f);
                if(charge&&_rng.Chance(pursuitChance)) Exchange(at.Person,df.Person,true,false,1f,role,1f,profile.Discipline,1f,profile.Capture,profile.FatigueEfficiency,null);
            }
            pursuer.Fatigue=Mathx.Clamp(pursuer.Fatigue+hits*.18f*profile.PursuitFatigueCost*armySpecial.FatigueCost/Math.Max(.7f,profile.FatigueEfficiency),0,100);
            return Math.Max(0,DeadCount(r)-before);
        }

        public int ResolvePursuit(Army pursuer,Army routed,int strikes,float pursuitSkill) {
            var p=new CommanderBehaviorProfile{Pursuit=pursuitSkill};
            return ResolvePursuit(pursuer,routed,strikes,p);
        }

        float SquadCasualtyRate(Squad s) {
            if(s==null)return 0f;int total=s.SoldierIds.Count+(string.IsNullOrEmpty(s.OfficerId)?0:1),down=0;
            foreach(string id in s.SoldierIds){Person p;if(!_people.TryGetValue(id,out p)||IsDown(p))down++;}
            if(!string.IsNullOrEmpty(s.OfficerId)){Person o;if(!_people.TryGetValue(s.OfficerId,out o)||IsDown(o))down++;}
            return total<=0?1f:Mathx.Clamp(down/(float)total,0f,1f);
        }

        void UpdateSquadMorale(Army a,float baseMorale,bool lastStand,CommanderBehaviorProfile profile,out int routedSquads,out int newWaves) {
            routedSquads=0;newWaves=0;
            for(int i=0;i<a.Squads.Count;i++) {
                Squad s=a.Squads[i];bool wasRouted=s.Routed;
                float discipline=50f+(profile.Discipline-1f)*45f+(profile.Cohesion-1f)*35f;int n=0;
                foreach(string id in s.SoldierIds){Person p;if(_people.TryGetValue(id,out p)){VeteranBehaviorProfile v=VeteranEffectEngine.Build(p);discipline+=p.Stats.Defense*.15f+(v.CommandResponse-1f)*18f;n++;}}
                if(n>0)discipline/=1f+n*.05f;
                SpecialUnitBehaviorProfile special=SpecialUnitBehaviorEngine.Build(s.UnitTemplateId,_data);
                float local=Mathx.Clamp(baseMorale+(s.Cohesion-50f)*.10f-SquadCasualtyRate(s)*34f+(special.LineCohesion-1f)*18f+(lastStand?(special.KeyDefense-1f)*15f:0f)+(IsGeneralAlive(a)?(special.GeneralGuard-1f)*12f:0f),0,100);
                float worstNeighbor=100f;
                if(i>0)worstNeighbor=Math.Min(worstNeighbor,a.Squads[i-1].Morale);
                if(i+1<a.Squads.Count)worstNeighbor=Math.Min(worstNeighbor,a.Squads[i+1].Morale);
                if(worstNeighbor<25f)local=MoraleSystem.Spread(local,worstNeighbor,discipline);
                s.Morale=Mathx.Clamp(local,0,100);s.LocalMoraleState=MoraleSystem.State(s.Morale);
                float routThreshold=Mathx.Clamp(10f/Math.Max(.8f,profile.RoutResistance),5f,12f);
                s.Routed=!lastStand&&s.Morale<routThreshold;
                if(s.Routed){routedSquads++;s.Cohesion=Math.Max(5f,s.Cohesion-18f/Math.Max(.8f,profile.ReformSpeed));if(!wasRouted){s.RoutWave++;newWaves++;}}
                else {s.Cohesion=Math.Min(100f,s.Cohesion+2.5f*profile.ReformSpeed*special.LineCohesion);if(s.Morale>28f)s.RoutWave=0;}
            }
        }

        float VeteranRatio(List<Combatant> list) {
            if(list.Count==0) return 0;
            int n=0; foreach(var c in list) if(c.Person.Battles>=3) n++;
            return n/(float)list.Count;
        }

        float TraitMorale(List<Combatant> list) {
            if(list.Count==0) return 1f;
            float sum=0; int n=0;
            foreach(var c in list) { if(!c.Person.Alive) continue; sum+=TraitEffectEngine.MoraleResistance(c.Person,_data)*VeteranEffectEngine.Build(c.Person).MoraleResistance; n++; }
            return n==0?1f:sum/n;
        }

        int DeadCount(List<Combatant> list) {
            int n=0; foreach(var c in list) if(!c.Person.Alive||c.Person.Injury==InjuryState.Dead) n++;
            return n;
        }

        void Count(List<Combatant> list,out int dead,out int wounded,out int captured) {
            dead=0; wounded=0; captured=0;
            foreach(var c in list) {
                Person p=c.Person;
                if(!p.Alive||p.Injury==InjuryState.Dead) dead++;
                else if(p.Injury==InjuryState.Captured) captured++;
                else if(p.Injury!=InjuryState.None) wounded++;
            }
        }
    }
}
