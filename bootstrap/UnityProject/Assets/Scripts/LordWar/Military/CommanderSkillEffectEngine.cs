using System;
using System.Collections.Generic;
using System.Globalization;
using LordWar.Data;

namespace LordWar.Military {
    /// <summary>
    /// 将军技能的唯一运行时行为画像。字段不是面板装饰，必须由行军、战斗、后勤、训练、攻城等执行链消费。
    /// </summary>
    public sealed class CommanderBehaviorProfile {
        public float MarchSpeed=1f, ReformSpeed=1f, Cohesion=1f, CommandDelay=1f, Scout=1f, Ambush=1f;
        public float ReserveUse=1f, FlankSearch=1f, Pursuit=1f, RetreatDiscipline=1f, Morale=1f, SupplyEfficiency=1f;
        public float Siege=1f, Defense=1f, InfantryCombat=1f, RangedCombat=1f, CavalryControl=1f, TerrainAdapt=1f;
        public float WeatherAdapt=1f, Engineering=1f, Training=1f, Medical=1f, Discipline=1f, Capture=1f;
        public float LootControl=1f, FatigueEfficiency=1f, RoutResistance=1f, NightCombat=1f;

        // 明确的“技能代价/风险”单独建模，不能把正数副作用误当成增益。
        public float MarchFatigueCost=1f, PursuitFatigueCost=1f, SupplyGoldCost=1f, ArrearsSensitivity=1f;
        public float LastStandCasualty=1f, RetreatSupplyLoss, CasualtyAversion=1f, RangedContactAvoidance=1f;
    }

    public static class CommanderSkillEffectEngine {
        static Dictionary<string,float> ParseLooseJson(string s) {
            var d=new Dictionary<string,float>();
            if(string.IsNullOrEmpty(s)) return d;
            s=s.Trim().Trim('{','}');
            string[] parts=s.Split(',');
            foreach(string part in parts) {
                string[] kv=part.Split(':');
                if(kv.Length!=2) continue;
                string k=kv[0].Trim().Trim('"');
                float v;
                if(float.TryParse(kv[1].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out v)) d[k]=v;
            }
            return d;
        }

        static bool Contains(string k,string token){return k.IndexOf(token,StringComparison.Ordinal)>=0;}
        static float Benefit(float raw){return Math.Abs(raw)/100f;}

        static bool ApplyTradeoff(CommanderBehaviorProfile p,string k,float raw) {
            float v=Math.Abs(raw)/100f;
            if(k=="fatigue_rate_pct") { p.MarchFatigueCost*=1f+v; return true; }
            if(k=="pursuit_fatigue_pct") { p.PursuitFatigueCost*=1f+v; return true; }
            if(k=="gold_supply_cost_pct") { p.SupplyGoldCost*=1f+v; return true; }
            if(k=="arrears_penalty_pct") { p.ArrearsSensitivity*=1f+v; return true; }
            if(k=="last_stand_casualty_pct") { p.LastStandCasualty*=1f+v; return true; }
            if(k=="supply_loss_pct") { p.RetreatSupplyLoss+=v; return true; }
            if(k=="casualty_aversion_pct") { p.CasualtyAversion*=1f+v; p.RetreatDiscipline*=1f+v*.55f; return true; }
            if(k=="melee_contact_delay_pct") { p.RangedContactAvoidance*=1f+v; p.RangedCombat*=1f+v*.18f; return true; }
            return false;
        }

        static bool ApplyNumeric(CommanderBehaviorProfile p,string key,float raw) {
            string k=(key??"").ToLowerInvariant();
            if(string.IsNullOrEmpty(k)) return false;
            if(ApplyTradeoff(p,k,raw)) return true;
            float v=Benefit(raw), m=1f+v;
            bool applied=false;

            if(Contains(k,"march_speed")||Contains(k,"cross_speed")){p.MarchSpeed*=m;applied=true;}
            if(Contains(k,"reform")||Contains(k,"gap_fill")||Contains(k,"replacement")){p.ReformSpeed*=m;applied=true;}
            if(Contains(k,"cohesion")||Contains(k,"compactness")||Contains(k,"gap")||Contains(k,"rotation")){p.Cohesion*=m;applied=true;}
            if(Contains(k,"order_delay")||(Contains(k,"command")&&Contains(k,"delay"))){p.CommandDelay*=Math.Max(.55f,1f-v);applied=true;}
            if(Contains(k,"scout")||Contains(k,"warning")||Contains(k,"detect")||Contains(k,"map_memory")||Contains(k,"enemy_size")){p.Scout*=m;applied=true;}
            if(Contains(k,"ambush")||Contains(k,"feign")||Contains(k,"night_raid")||Contains(k,"bait")||Contains(k,"concealment")){p.Ambush*=m;applied=true;}if(Contains(k,"night")){p.NightCombat*=m;p.Scout*=1f+v*.35f;applied=true;}
            if(Contains(k,"reserve")||Contains(k,"intercept_reaction")){p.ReserveUse*=m;applied=true;}
            if(Contains(k,"flank")||Contains(k,"encirclement")){p.FlankSearch*=m;applied=true;}
            if(Contains(k,"pursuit")||Contains(k,"intercept_rout")){p.Pursuit*=m;applied=true;}
            if(Contains(k,"capture")){p.Capture*=m;applied=true;}
            if(Contains(k,"withdraw")||Contains(k,"rearguard")||Contains(k,"straggler")){p.RetreatDiscipline*=m;applied=true;}
            if(Contains(k,"morale")||Contains(k,"fear")||Contains(k,"rally")||Contains(k,"banner")||Contains(k,"last_stand")){p.Morale*=m;p.RoutResistance*=m;applied=true;}
            if(Contains(k,"supply")||Contains(k,"food")||Contains(k,"animal_fatigue")){p.SupplyEfficiency*=m;applied=true;}
            if(Contains(k,"engineer")||Contains(k,"fieldwork")||Contains(k,"pontoon")||Contains(k,"camp_build")||Contains(k,"obstacle")){p.Engineering*=m;applied=true;}
            if(Contains(k,"siege")||Contains(k,"sapping")||Contains(k,"wall_suppression")||Contains(k,"assault_wave")||Contains(k,"gate_damage")){p.Siege*=m;applied=true;}
            if(Contains(k,"defense")||Contains(k,"breach")||Contains(k,"urban_fallback")||Contains(k,"gate_defense")){p.Defense*=m;applied=true;}
            if(Contains(k,"melee")||Contains(k,"shield_break")||Contains(k,"push_weight")||Contains(k,"frontline")||Contains(k,"brace")){p.InfantryCombat*=m;applied=true;}
            if(Contains(k,"ranged")||Contains(k,"volley")||Contains(k,"crossbow")||Contains(k,"highground")||Contains(k,"dense_target")){p.RangedCombat*=m;applied=true;}
            if(Contains(k,"cavalry")||Contains(k,"mounted")||Contains(k,"charge")){p.CavalryControl*=m;applied=true;}
            if(Contains(k,"mountain")||Contains(k,"river")||Contains(k,"forest")||Contains(k,"marsh")||Contains(k,"desert")||Contains(k,"snow_fatigue")||Contains(k,"urban_path")||Contains(k,"bad_terrain")){p.TerrainAdapt*=m;applied=true;}
            if(Contains(k,"rain")||Contains(k,"wind")||Contains(k,"fog")||Contains(k,"heat")||Contains(k,"cold")||Contains(k,"frost")){p.WeatherAdapt*=m;applied=true;}
            if(Contains(k,"training")||Contains(k,"drill")||Contains(k,"recruit_cohesion")||Contains(k,"veteran_training")||Contains(k,"officer_xp")||Contains(k,"adaptive_xp")){p.Training*=m;applied=true;}
            if(Contains(k,"wounded")||Contains(k,"aid")||Contains(k,"recovery")||Contains(k,"disease")||Contains(k,"frostbite")){p.Medical*=m;applied=true;}
            if(Contains(k,"discipline")||Contains(k,"paid_morale")||Contains(k,"promotion_quality")){p.Discipline*=m;applied=true;}
            if(Contains(k,"loot")||Contains(k,"unauthorized")){p.LootControl*=m;applied=true;}
            if(Contains(k,"fatigue")||Contains(k,"forced_march")){p.FatigueEfficiency*=m;applied=true;}

            // 决策质量键不转成简单攻击加成，而是改变状态机阈值/反应能力。
            if(Contains(k,"aggression")){p.FlankSearch*=m;p.Pursuit*=m;applied=true;}
            if(Contains(k,"command_radius")){p.Cohesion*=m;p.ReserveUse*=m;applied=true;}
            if(Contains(k,"cut_retreat")){p.Pursuit*=m;p.Capture*=m;applied=true;}
            if(Contains(k,"dismount_decision")){p.CavalryControl*=m;p.TerrainAdapt*=m;applied=true;}
            if(Contains(k,"false_merit")){p.Discipline*=m;p.Morale*=m;applied=true;}
            if(Contains(k,"friendly_fire")){p.RangedCombat*=m;p.Discipline*=m;applied=true;}
            if(Contains(k,"heavy_move_penalty")){p.MarchSpeed*=m;p.FatigueEfficiency*=m;applied=true;}
            if(Contains(k,"local_rout_threshold")){p.RoutResistance*=m;applied=true;}
            if(Contains(k,"multi_army_coordination")){p.Cohesion*=m;p.CommandDelay*=Math.Max(.55f,1f-v*.5f);p.ReserveUse*=m;applied=true;}
            if(Contains(k,"opportunity_reaction")){p.ReserveUse*=m;p.FlankSearch*=m;applied=true;}
            if(Contains(k,"order_loss")){p.CommandDelay*=Math.Max(.55f,1f-v);p.Discipline*=m;applied=true;}
            if(Contains(k,"overextend_risk")){p.RetreatDiscipline*=m;p.Cohesion*=m;applied=true;}
            if(Contains(k,"political_power")){p.Morale*=1f+v*.5f;p.Discipline*=1f+v*.5f;applied=true;}
            if(Contains(k,"premature_commit")){p.ReserveUse*=m;applied=true;}
            if(Contains(k,"retreat_to_screen")){p.RetreatDiscipline*=m;p.ReformSpeed*=m;applied=true;}
            if(Contains(k,"subcommander_autonomy")){p.CommandDelay*=Math.Max(.55f,1f-v*.65f);p.ReserveUse*=m;applied=true;}
            if(Contains(k,"sustained_rate")){p.RangedCombat*=m;p.FatigueEfficiency*=m;applied=true;}
            if(Contains(k,"temporary_bridge_durability")){p.Engineering*=m;applied=true;}
            if(Contains(k,"urban_small_unit_ai")){p.TerrainAdapt*=m;p.InfantryCombat*=m;applied=true;}
            return applied;
        }

        static void ApplyCategory(CommanderBehaviorProfile p,string category,string behavior) {
            string c=(category??"")+(behavior??"");
            if(c.IndexOf("行军统御")>=0){p.MarchSpeed+=.02f;p.Cohesion+=.03f;}
            if(c.IndexOf("阵线控制")>=0){p.Cohesion+=.06f;p.Defense+=.04f;}
            if(c.IndexOf("步战指挥")>=0)p.InfantryCombat+=.06f;
            if(c.IndexOf("射阵指挥")>=0)p.RangedCombat+=.06f;
            if(c.IndexOf("骑兵指挥")>=0)p.CavalryControl+=.06f;
            if(c.IndexOf("侦察情报")>=0)p.Scout+=.08f;
            if(c.IndexOf("伏击奇袭")>=0)p.Ambush+=.08f;
            if(c.IndexOf("士气统御")>=0){p.Morale+=.06f;p.RoutResistance+=.05f;}
            if(c.IndexOf("撤退组织")>=0)p.RetreatDiscipline+=.08f;
            if(c.IndexOf("追击控制")>=0)p.Pursuit+=.08f;
            if(c.IndexOf("后勤筹措")>=0)p.SupplyEfficiency+=.08f;
            if(c.IndexOf("工程营造")>=0)p.Engineering+=.08f;
            if(c.IndexOf("攻城统御")>=0)p.Siege+=.08f;
            if(c.IndexOf("守城统御")>=0)p.Defense+=.08f;
            if(c.IndexOf("地形战法")>=0)p.TerrainAdapt+=.08f;
            if(c.IndexOf("天候应战")>=0)p.WeatherAdapt+=.08f;
            if(c.IndexOf("军令指挥")>=0){p.CommandDelay*=.96f;p.ReserveUse+=.05f;}
            if(c.IndexOf("训练治军")>=0)p.Training+=.08f;
            if(c.IndexOf("医护恢复")>=0)p.Medical+=.08f;
            if(c.IndexOf("纪律军心")>=0){p.Discipline+=.08f;p.Morale+=.03f;}
        }

        public static bool IsNumericEffectSupported(string key) {
            return ApplyNumeric(new CommanderBehaviorProfile(),key,10f);
        }

        public static CommanderBehaviorProfile Build(Person general,GameDataCatalog data) {
            var p=new CommanderBehaviorProfile();
            if(general==null||data==null)return p;
            foreach(string id in general.CommanderSkillIds) {
                SkillDef s;
                if(!data.Skills.TryGetValue(id,out s))continue;
                foreach(var kv in ParseLooseJson(s.NumericJson))ApplyNumeric(p,kv.Key,kv.Value);
                ApplyCategory(p,s.Category,s.Behavior);
                string c=(s.Category??"")+(s.Behavior??"");
                if(c.IndexOf("侧翼")>=0||c.IndexOf("包围")>=0)p.FlankSearch+=.12f;
                if(c.IndexOf("预备队")>=0)p.ReserveUse+=.12f;
            }
            p=TraitEffectEngine.ApplyGeneralTraits(general,data,p);
            Clamp(p);
            return p;
        }

        static void Clamp(CommanderBehaviorProfile p) {
            p.MarchSpeed=Mathx.Clamp(p.MarchSpeed,.65f,1.5f);p.ReformSpeed=Mathx.Clamp(p.ReformSpeed,.65f,1.6f);
            p.Cohesion=Mathx.Clamp(p.Cohesion,.65f,1.6f);p.CommandDelay=Mathx.Clamp(p.CommandDelay,.5f,1.2f);
            p.Scout=Mathx.Clamp(p.Scout,.65f,1.8f);p.Ambush=Mathx.Clamp(p.Ambush,.65f,1.8f);p.ReserveUse=Mathx.Clamp(p.ReserveUse,.65f,1.8f);
            p.FlankSearch=Mathx.Clamp(p.FlankSearch,.65f,1.8f);p.Pursuit=Mathx.Clamp(p.Pursuit,.65f,1.8f);p.RetreatDiscipline=Mathx.Clamp(p.RetreatDiscipline,.65f,1.8f);
            p.Morale=Mathx.Clamp(p.Morale,.65f,1.8f);p.SupplyEfficiency=Mathx.Clamp(p.SupplyEfficiency,.65f,1.8f);p.Siege=Mathx.Clamp(p.Siege,.65f,1.8f);
            p.Defense=Mathx.Clamp(p.Defense,.65f,1.8f);p.InfantryCombat=Mathx.Clamp(p.InfantryCombat,.65f,1.8f);p.RangedCombat=Mathx.Clamp(p.RangedCombat,.65f,1.8f);
            p.CavalryControl=Mathx.Clamp(p.CavalryControl,.65f,1.8f);p.TerrainAdapt=Mathx.Clamp(p.TerrainAdapt,.65f,1.8f);p.WeatherAdapt=Mathx.Clamp(p.WeatherAdapt,.65f,1.8f);
            p.Engineering=Mathx.Clamp(p.Engineering,.65f,1.8f);p.Training=Mathx.Clamp(p.Training,.65f,1.8f);p.Medical=Mathx.Clamp(p.Medical,.65f,1.8f);
            p.Discipline=Mathx.Clamp(p.Discipline,.65f,1.8f);p.Capture=Mathx.Clamp(p.Capture,.65f,1.8f);p.LootControl=Mathx.Clamp(p.LootControl,.65f,1.8f);
            p.FatigueEfficiency=Mathx.Clamp(p.FatigueEfficiency,.65f,1.8f);p.RoutResistance=Mathx.Clamp(p.RoutResistance,.65f,1.8f);
            p.MarchFatigueCost=Mathx.Clamp(p.MarchFatigueCost,1f,1.5f);p.PursuitFatigueCost=Mathx.Clamp(p.PursuitFatigueCost,1f,1.5f);
            p.SupplyGoldCost=Mathx.Clamp(p.SupplyGoldCost,1f,1.5f);p.ArrearsSensitivity=Mathx.Clamp(p.ArrearsSensitivity,1f,1.6f);
            p.LastStandCasualty=Mathx.Clamp(p.LastStandCasualty,1f,1.6f);p.RetreatSupplyLoss=Mathx.Clamp(p.RetreatSupplyLoss,0f,.45f);
            p.CasualtyAversion=Mathx.Clamp(p.CasualtyAversion,1f,1.6f);p.RangedContactAvoidance=Mathx.Clamp(p.RangedContactAvoidance,1f,1.6f);
        }

        public static bool ShouldAttemptFlank(CommanderBehaviorProfile p,float enemyFlankExposure,float ownFatigue,float reserveRatio) {
            if(p==null)return false;
            float caution=Mathx.Clamp(p.CasualtyAversion,1f,1.6f);
            float score=enemyFlankExposure*p.FlankSearch+reserveRatio*p.ReserveUse-ownFatigue*.006f/p.FatigueEfficiency;
            return score>.62f*caution;
        }

        public static bool ShouldPursue(CommanderBehaviorProfile p,float enemyRoutRatio,float ownCohesion,float ambushRisk) {
            if(p==null)return false;
            float score=enemyRoutRatio*p.Pursuit+ownCohesion*.35f-ambushRisk/Math.Max(.5f,p.Scout);
            score/=Mathx.Clamp(p.CasualtyAversion*p.PursuitFatigueCost,1f,2.0f);
            return score>.74f;
        }

        public static float AmbushChance(CommanderBehaviorProfile attacker,CommanderBehaviorProfile defender,TerrainKind terrain) {
            if(attacker==null)attacker=new CommanderBehaviorProfile();if(defender==null)defender=new CommanderBehaviorProfile();
            float conceal=terrain==TerrainKind.Forest||terrain==TerrainKind.Mountain||terrain==TerrainKind.MountainPass?1.28f:(terrain==TerrainKind.Marsh?1.12f:.9f);
            float chance=.08f*attacker.Ambush*conceal/Math.Max(.65f,defender.Scout);
            return Mathx.Clamp(chance,.015f,.32f);
        }

        public static float TerrainCombatFactor(CommanderBehaviorProfile p,TerrainKind t) {
            if(p==null)return 1f;
            bool difficult=t==TerrainKind.Mountain||t==TerrainKind.MountainPass||t==TerrainKind.Forest||t==TerrainKind.Marsh||t==TerrainKind.River||t==TerrainKind.Snow||t==TerrainKind.Desert;
            return difficult?Mathx.Clamp(.86f+.14f*p.TerrainAdapt,.82f,1.12f):1f;
        }

        public static float WeatherCombatFactor(CommanderBehaviorProfile p,WeatherKind w) {
            if(p==null)return 1f;
            bool bad=w==WeatherKind.Rain||w==WeatherKind.Storm||w==WeatherKind.Snow||w==WeatherKind.Blizzard||w==WeatherKind.Fog||w==WeatherKind.Heat||w==WeatherKind.Cold;
            return bad?Mathx.Clamp(.84f+.16f*p.WeatherAdapt,.8f,1.12f):1f;
        }
    }
}
