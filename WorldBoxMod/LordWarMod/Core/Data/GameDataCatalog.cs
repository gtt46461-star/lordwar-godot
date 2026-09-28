using System;
using System.Collections.Generic;
using System.Globalization;
namespace LordWar.Data {
    [Serializable] public sealed class PolicyDef { public string Id, Name, Stage, Category, DisplayName, EffectText, CostText; public int Ability, Days, Cost; public float Monthly; }
    [Serializable] public sealed class SkillDef { public string Id, Name, Stage, Category, Behavior, NumericJson, AiBehavior, Trigger; }
    [Serializable] public sealed class UnitDef {
        public string Id, Name, Category, Role, CityStyle, Terrain, Weakness, Gear, Recruit, Ai, Formation, Visual, QualityRule;
        public int Level, RecruitCost, TrainingDays, HorseCost, Life, Attack, Defense, Mobility, Stamina, BaseMorale, SquadCap;
        public float MonthlyWage, DailyFood;
    }
    [Serializable] public sealed class SpecialUnitDef { public string Id, Name, CityStyle, Category, Role, Source, GeneralCondition, PolicyCondition, BuildingCondition, Gear, Behavior, Weakness; public int SquadCap, NationalCap; public float Upkeep; }
    [Serializable] public sealed class TraitDef { public string Id, Name, Category, Domain, AppliesTo, Rarity, Behavior, Effect, Unlock, Cost; public int MinIntelligence, MinAdministration, PolicyCap; }
    [Serializable] public sealed class EquipmentQualityDef { public int Level; public string Name, Description, Destination, Rule; public float CombatMultiplier, DurabilityMultiplier, CostMultiplier; }
    [Serializable] public sealed class EquipmentTypeDef { public string Id, Name, Slot, Material, AllowedQuality, AllocationRule, EmptySlotRule, UniqueRule; public int Attack, Defense, Durability; public float Weight; }
    [Serializable] public sealed class PromotionDef { public int Stage, MinQuality, MinCommandPotential; public string Name, Condition, Description; }
    [Serializable] public sealed class GeneralUnlockDef { public string GeneralTrait, SpecialUnit, RequiredSkill, CityCondition, Approval, Description; }
    [Serializable] public sealed class OfficialDoctrineDef { public string OfficialTrait, System, BaseUnit, Policy, Effect, Approval; }
    [Serializable] public sealed class CityCultureDef { public string Id, Name, Terrain, Role, Architecture, Wall, Economy, Military, Weakness, Mechanic, VisualColor; }
    [Serializable] public sealed class FamilyTraditionDef { public string Id, Name, Type, Specialty, Mechanic, PrivateUnit, SuitableCity, CrestRule, MainColor, Inheritance, Rebellion; }

    public interface ITextDataProvider { string Load(string key); }

    public sealed class GameDataCatalog {
        public readonly Dictionary<string,PolicyDef> Policies=new Dictionary<string,PolicyDef>();
        public readonly Dictionary<string,SkillDef> Skills=new Dictionary<string,SkillDef>();
        public readonly Dictionary<string,UnitDef> Units=new Dictionary<string,UnitDef>();
        public readonly Dictionary<string,SpecialUnitDef> SpecialUnits=new Dictionary<string,SpecialUnitDef>();
        public readonly Dictionary<string,TraitDef> PersonTraits=new Dictionary<string,TraitDef>();
        public readonly Dictionary<string,TraitDef> SoldierTraits=new Dictionary<string,TraitDef>();
        public readonly Dictionary<string,TraitDef> GeneralTraits=new Dictionary<string,TraitDef>();
        public readonly Dictionary<string,TraitDef> OfficialTraits=new Dictionary<string,TraitDef>();
        public readonly List<EquipmentQualityDef> EquipmentQualities=new List<EquipmentQualityDef>();
        public readonly Dictionary<string,EquipmentTypeDef> EquipmentTypes=new Dictionary<string,EquipmentTypeDef>();
        public readonly List<PromotionDef> Promotions=new List<PromotionDef>();
        public readonly List<GeneralUnlockDef> GeneralUnlocks=new List<GeneralUnlockDef>();
        public readonly List<OfficialDoctrineDef> OfficialDoctrines=new List<OfficialDoctrineDef>();
        public readonly List<CityCultureDef> CityCultures=new List<CityCultureDef>(); public readonly List<FamilyTraditionDef> FamilyTraditions=new List<FamilyTraditionDef>();
        public readonly List<string> CityNames=new List<string>(); public readonly List<string> FamilyNames=new List<string>();
        public readonly List<string> GeneralNames=new List<string>(); public readonly List<string> OfficialNames=new List<string>(); public readonly List<string> SoldierNames=new List<string>();
        public readonly List<string> SkillIds=new List<string>(); public readonly List<string> UnitIds=new List<string>(); public readonly List<string> PersonTraitIds=new List<string>(); public readonly List<string> SoldierTraitIds=new List<string>(); public readonly List<string> GeneralTraitIds=new List<string>(); public readonly List<string> OfficialTraitIds=new List<string>();

        static string V(Dictionary<string,string> r,string k){string v;return r.TryGetValue(k,out v)?v:"";}
        static int I(string s){int v;return int.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:0;}
        static float F(string s){float v;return float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:0f;}
        public void LoadAll(ITextDataProvider p) {
            Policies.Clear(); Skills.Clear(); Units.Clear(); SpecialUnits.Clear(); PersonTraits.Clear(); SoldierTraits.Clear(); GeneralTraits.Clear(); OfficialTraits.Clear();
            EquipmentQualities.Clear(); EquipmentTypes.Clear(); Promotions.Clear(); GeneralUnlocks.Clear(); OfficialDoctrines.Clear(); CityCultures.Clear(); FamilyTraditions.Clear(); CityNames.Clear(); FamilyNames.Clear(); GeneralNames.Clear(); OfficialNames.Clear(); SoldierNames.Clear(); SkillIds.Clear(); UnitIds.Clear(); PersonTraitIds.Clear(); SoldierTraitIds.Clear(); GeneralTraitIds.Clear(); OfficialTraitIds.Clear();
            LoadPolicies(p.Load("policies_v8")); LoadSkills(p.Load("commander_skills_v8")); LoadUnits(p.Load("units_v8")); LoadSpecialUnits(p.Load("special_units_v8"));
            LoadTraits(p.Load("person_traits_v8"),PersonTraits,PersonTraitIds,"人物特性","类别","适用身份","效果说明","具体效果");
            LoadTraits(p.Load("soldier_traits_v8"),SoldierTraits,SoldierTraitIds,"士兵特性","类别","","行为说明","具体效果");
            LoadTraits(p.Load("general_traits_v8"),GeneralTraits,GeneralTraitIds,"特性名称","分类","","行为改变","具体效果");
            LoadOfficialTraits(p.Load("official_traits_v8")); LoadCityCultures(p.Load("city_cultures_v7")); LoadFamilyTraditions(p.Load("family_traditions_v7")); LoadEquipmentQualities(p.Load("equipment_quality_v7")); LoadEquipmentTypes(p.Load("equipment_types_v9")); LoadPromotions(p.Load("soldier_promotion_v7")); LoadGeneralUnlocks(p.Load("general_special_unlock_v8")); LoadOfficialDoctrines(p.Load("official_doctrine_unlock_v8"));
            LoadNames(p.Load("city_names_v8"),p.Load("family_names_v8"),p.Load("person_names_v8"));
            if(Policies.Count!=300) throw new InvalidOperationException("政策数据未完整加载"); if(Skills.Count!=360) throw new InvalidOperationException("统帅技能数据未完整加载"); if(Units.Count!=156) throw new InvalidOperationException("基础兵种数据未完整加载"); if(SpecialUnits.Count!=120) throw new InvalidOperationException("特殊兵种数据未完整加载");
            if(PersonTraits.Count!=160||SoldierTraits.Count!=128||GeneralTraits.Count!=120||OfficialTraits.Count!=96) throw new InvalidOperationException("人物/士兵/将军/官员特性数据未完整加载");
            if(EquipmentTypes.Count!=72) throw new InvalidOperationException("装备基础类型72类未完整加载");
        }
        void LoadPolicies(string s){var t=CsvTable.Parse(s); foreach(var r in t.Rows){var d=new PolicyDef{Id=V(r,"政策ID"),Name=V(r,"政策名称"),Stage=V(r,"阶段"),Category=V(r,"分类"),DisplayName=V(r,"提案显示名"),Ability=I(V(r,"官员能力门槛")),Days=I(V(r,"执行天数")),Cost=I(V(r,"一次国库成本")),Monthly=F(V(r,"月维护金币")),EffectText=V(r,"具体效果"),CostText=V(r,"明确代价/风险")}; if(d.Id!="") Policies[d.Id]=d;}}
        void LoadSkills(string s){var t=CsvTable.Parse(s); foreach(var r in t.Rows){var d=new SkillDef{Id=V(r,"技能ID"),Name=V(r,"技能名称"),Stage=V(r,"技能阶段"),Category=V(r,"分类"),Behavior=V(r,"核心行为"),NumericJson=V(r,"数值效果_JSON"),AiBehavior=V(r,"AI行为改变"),Trigger=V(r,"触发条件")}; if(d.Id!=""){Skills[d.Id]=d;SkillIds.Add(d.Id);}}}
        void LoadUnits(string s){var t=CsvTable.Parse(s); foreach(var r in t.Rows){var d=new UnitDef{
            Id=V(r,"id"),Name=V(r,"兵种名称"),Level=I(V(r,"等级")),Category=V(r,"类别"),CityStyle=V(r,"城市风格"),Role=V(r,"定位"),
            Recruit=V(r,"解锁条件"),Gear=V(r,"标准装备"),RecruitCost=I(V(r,"单兵招募金币")),TrainingDays=I(V(r,"训练天数")),MonthlyWage=F(V(r,"月军饷")),DailyFood=F(V(r,"每日粮食")),HorseCost=I(V(r,"消耗军马")),
            Life=I(V(r,"生命")),Attack=I(V(r,"攻击")),Defense=I(V(r,"防御")),Mobility=I(V(r,"机动")),Stamina=I(V(r,"体力")),BaseMorale=I(V(r,"基础士气")),SquadCap=I(V(r,"战术小队上限")),Formation=V(r,"默认阵型"),
            QualityRule=V(r,"装备品质规则"),Terrain=V(r,"强项"),Weakness=V(r,"弱点"),Ai=V(r,"AI使用"),Visual=V(r,"视觉组合")};
            if(d.Id=="") d.Id=V(r,"ID"); if(d.Name=="")d.Name=V(r,"中文兵种名"); if(d.Id!=""){Units[d.Id]=d;UnitIds.Add(d.Id);}}}
        void LoadSpecialUnits(string s){var t=CsvTable.Parse(s); foreach(var r in t.Rows){var d=new SpecialUnitDef{Id=V(r,"ID"),Name=V(r,"特殊兵种名称"),CityStyle=V(r,"城市风格"),Category=V(r,"类别"),Role=V(r,"战场定位"),Source=V(r,"主要解锁来源"),GeneralCondition=V(r,"将军条件"),PolicyCondition=V(r,"官员/政策条件"),BuildingCondition=V(r,"建筑条件"),Gear=V(r,"装备要求"),SquadCap=I(V(r,"单支上限")),NationalCap=I(V(r,"全国软上限")),Upkeep=F(V(r,"维护系数")),Behavior=V(r,"核心行为"),Weakness=V(r,"弱点")}; if(d.Id!="")SpecialUnits[d.Id]=d;}}
        void LoadTraits(string s,Dictionary<string,TraitDef> dst,List<string> ids,string nameHeader,string catHeader,string appliesHeader,string behaviorHeader,string effectHeader){var t=CsvTable.Parse(s);foreach(var r in t.Rows){var d=new TraitDef{Id=V(r,"ID"),Name=V(r,nameHeader),Category=V(r,catHeader),AppliesTo=appliesHeader==""?"":V(r,appliesHeader),Rarity=V(r,"稀有度"),Behavior=V(r,behaviorHeader),Effect=V(r,effectHeader),Unlock=V(r,"可能解锁特殊兵种"),Cost=V(r,"明确代价/限制")};if(d.Id!=""){dst[d.Id]=d;ids.Add(d.Id);}}}
        void LoadOfficialTraits(string s){var t=CsvTable.Parse(s);foreach(var r in t.Rows){var d=new TraitDef{Id=V(r,"ID"),Name=V(r,"特性名称"),Domain=V(r,"领域"),Rarity=V(r,"稀有度"),MinIntelligence=I(V(r,"最低智力")),MinAdministration=I(V(r,"最低行政")),PolicyCap=I(V(r,"政策能力上限")),Behavior=V(r,"执行效果"),Unlock=V(r,"可解锁/强化"),Effect=V(r,"具体效果"),Cost=V(r,"忠诚与政治")};if(d.Id!=""){OfficialTraits[d.Id]=d;OfficialTraitIds.Add(d.Id);}}}
        void LoadEquipmentQualities(string s){var t=CsvTable.Parse(s);foreach(var r in t.Rows)EquipmentQualities.Add(new EquipmentQualityDef{Level=I(V(r,"等级")),Name=V(r,"名称"),Description=V(r,"说明"),CombatMultiplier=F(V(r,"战斗性能倍率")),DurabilityMultiplier=F(V(r,"耐久倍率")),CostMultiplier=F(V(r,"造价倍率")),Destination=V(r,"主要流向"),Rule=V(r,"实例化规则")});}
        void LoadEquipmentTypes(string s){var t=CsvTable.Parse(s);foreach(var r in t.Rows){var d=new EquipmentTypeDef{Id=V(r,"id"),Name=V(r,"名称"),Slot=V(r,"槽位"),Attack=I(V(r,"基础攻击")),Defense=I(V(r,"基础防御")),Durability=I(V(r,"基础耐久")),Weight=F(V(r,"重量")),Material=V(r,"制造材料"),AllowedQuality=V(r,"允许品质等级"),AllocationRule=V(r,"配发优先级"),EmptySlotRule=V(r,"不是人人拥有"),UniqueRule=V(r,"唯一物品规则")};if(d.Id!="")EquipmentTypes[d.Id]=d;}}
        public EquipmentTypeDef FindEquipmentType(string token){if(string.IsNullOrWhiteSpace(token))return null;string t=token.Trim();foreach(EquipmentTypeDef d in EquipmentTypes.Values)if(d.Name==t)return d;string alias=t.Replace("大盾","重塔盾").Replace("中甲","鳞甲").Replace("轻甲","皮甲").Replace("重甲","重板甲").Replace("头盔","铁盔").Replace("弩","强弩");foreach(EquipmentTypeDef d in EquipmentTypes.Values)if(d.Name==alias)return d;if(t.IndexOf("弓")>=0)return FindByName("长弓");if(t.IndexOf("弩")>=0)return FindByName("强弩");if(t.IndexOf("盾")>=0)return FindByName("木塔盾");if(t.IndexOf("甲")>=0)return FindByName(t.IndexOf("重")>=0?"重板甲":"皮甲");if(t.IndexOf("盔")>=0)return FindByName("铁盔");if(t.IndexOf("枪")>=0||t.IndexOf("槊")>=0)return FindByName("长枪");if(t.IndexOf("矛")>=0)return FindByName("长矛");if(t.IndexOf("剑")>=0)return FindByName("长剑");if(t.IndexOf("刀")>=0)return FindByName("军刀");if(t.IndexOf("斧")>=0)return FindByName("战斧");return null;}
        EquipmentTypeDef FindByName(string name){foreach(EquipmentTypeDef d in EquipmentTypes.Values)if(d.Name==name)return d;return null;}
        void LoadPromotions(string s){var t=CsvTable.Parse(s);foreach(var r in t.Rows)Promotions.Add(new PromotionDef{Stage=I(V(r,"阶段")),Name=V(r,"名称"),Condition=V(r,"条件摘要"),MinQuality=I(V(r,"最低装备优先级")),MinCommandPotential=I(V(r,"最低统帅潜力等级")),Description=V(r,"说明")});}
        void LoadGeneralUnlocks(string s){var t=CsvTable.Parse(s);foreach(var r in t.Rows)GeneralUnlocks.Add(new GeneralUnlockDef{GeneralTrait=V(r,"将军特性"),SpecialUnit=V(r,"特殊兵种"),RequiredSkill=V(r,"必要统帅技能"),CityCondition=V(r,"城市/建筑条件"),Approval=V(r,"领主审批"),Description=V(r,"说明")});}
        void LoadOfficialDoctrines(string s){var t=CsvTable.Parse(s);foreach(var r in t.Rows)OfficialDoctrines.Add(new OfficialDoctrineDef{OfficialTrait=V(r,"官员特性"),System=V(r,"主要强化系统"),BaseUnit=V(r,"影响基础兵种"),Policy=V(r,"可提出政策"),Effect=V(r,"效果"),Approval=V(r,"审批")});}
        void LoadCityCultures(string s){var t=CsvTable.Parse(s);foreach(var r in t.Rows)CityCultures.Add(new CityCultureDef{Id=V(r,"id"),Name=V(r,"名称"),Terrain=V(r,"地形"),Role=V(r,"定位"),Architecture=V(r,"建筑风格"),Wall=V(r,"城墙"),Economy=V(r,"经济"),Military=V(r,"军事"),Weakness=V(r,"弱点"),Mechanic=V(r,"专属机制"),VisualColor=V(r,"视觉色")});}
        void LoadFamilyTraditions(string s){var t=CsvTable.Parse(s);foreach(var r in t.Rows)FamilyTraditions.Add(new FamilyTraditionDef{Id=V(r,"id"),Name=V(r,"传统名称"),Type=V(r,"类型"),Specialty=V(r,"核心专长"),Mechanic=V(r,"家族机制"),PrivateUnit=V(r,"默认家兵"),SuitableCity=V(r,"适合城市"),CrestRule=V(r,"家徽生成规则"),MainColor=V(r,"主色建议"),Inheritance=V(r,"继承规则"),Rebellion=V(r,"反叛倾向")});}
        void LoadNames(string cities,string families,string persons){LoadColumn(cities,"城市名称",CityNames);LoadColumn(families,"家族名称",FamilyNames);var t=CsvTable.Parse(persons);foreach(var r in t.Rows){string pool=V(r,"身份池"),name=V(r,"姓名");if(string.IsNullOrWhiteSpace(name))continue;if(pool=="将军")GeneralNames.Add(name);else if(pool=="官员")OfficialNames.Add(name);else if(pool=="士兵")SoldierNames.Add(name);}if(GeneralNames.Count==0||OfficialNames.Count==0||SoldierNames.Count==0)throw new InvalidOperationException("中文人物姓名库未完整加载");}
        void LoadColumn(string s,string h,List<string> target){var t=CsvTable.Parse(s);foreach(var r in t.Rows){string v=V(r,h);if(!string.IsNullOrWhiteSpace(v))target.Add(v);}}
        public string TraitName(string id){TraitDef d;if(PersonTraits.TryGetValue(id,out d)||SoldierTraits.TryGetValue(id,out d)||GeneralTraits.TryGetValue(id,out d)||OfficialTraits.TryGetValue(id,out d))return d.Name;return id;}
    }
}
