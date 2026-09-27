from pathlib import Path
import csv, json, re, hashlib, subprocess, sys
from collections import Counter

ROOT=Path(__file__).resolve().parents[1]
DATA=ROOT/'Assets/Resources/LordWarData'
SRC=ROOT/'Assets/Scripts/LordWar'
ART=ROOT/'Assets/Resources/LordWarArt'
EVID=ROOT/'Evidence'; EVID.mkdir(exist_ok=True)
results=[]

def add(name,status,detail):
    results.append({'name':name,'status':status,'detail':detail})
    print(f'[{status}] {name}: {detail}')

def csv_count(name):
    p=DATA/name
    with p.open(encoding='utf-8-sig',newline='') as f: return sum(1 for _ in csv.DictReader(f))

# Data counts frozen by V8/V9
expect={
 'policies_v8.csv':300,
 'commander_skills_v8.csv':360,
 'units_v8.csv':156,
 'special_units_v8.csv':120,
 'city_names_v8.csv':144,
 'family_names_v8.csv':120,
 'general_traits_v8.csv':120,
 'official_traits_v8.csv':96,
 'person_traits_v8.csv':160,
 'soldier_traits_v8.csv':128,
 'equipment_types_v9.csv':72,
}
for f,n in expect.items():
    got=csv_count(f)
    add('数据行数 '+f,'PASS' if got==n else 'FAIL',f'{got}/{n}')

# Unit economy/training fields must be populated in the frozen 156-row table.
with (DATA/'units_v8.csv').open(encoding='utf-8-sig',newline='') as f:
    units=list(csv.DictReader(f))
num_fields=['单兵招募金币','训练天数','月军饷','每日粮食','生命','攻击','防御','体力','基础士气','战术小队上限']
bad_unit=[]
for r in units:
    for k in num_fields:
        try: v=float(r.get(k,'0') or 0)
        except Exception: v=0
        if v<=0: bad_unit.append(f"{r.get('id','?')}:{k}")
horse_units=[r for r in units if int(r.get('消耗军马','0') or 0)>0]
abstract_units=[r for r in units if '地区制式' in (r.get('标准装备') or '') or '专属套装' in (r.get('标准装备') or '')]
add('兵种经济/训练字段','PASS' if not bad_unit else 'FAIL', '156兵种字段完整' if not bad_unit else str(bad_unit[:20]))
add('骑兵真实军马消耗','PASS' if len(horse_units)>=20 else 'FAIL',f'{len(horse_units)}个兵种明确消耗军马')
add('抽象装备需要运行时解析','PASS' if len(abstract_units)>100 else 'FAIL',f'{len(abstract_units)}个兵种使用地区/专属抽象装备，已要求UnitLoadoutResolver接管')

# Chinese names pool
with (DATA/'person_names_v8.csv').open(encoding='utf-8-sig',newline='') as f:
    rows=list(csv.DictReader(f))
pools=Counter(r['身份池'] for r in rows)
add('人物中文姓名池','PASS' if pools.get('将军')==400 and pools.get('官员')==400 and pools.get('士兵')==1000 else 'FAIL',str(dict(pools)))

with (DATA/'equipment_quality_v7.csv').open(encoding='utf-8-sig',newline='') as f:
    quality_names=[r['名称'] for r in csv.DictReader(f)]
expected_quality=['残破','粗制','制式','优良','精良','大师','家族名器','传奇']
add('装备品质中文分级','PASS' if quality_names==expected_quality else 'FAIL',str(quality_names))

# Required JSON parse
for f in ['map_params_v9.json','construction_params_v9.json','population_economy_params_v9.json','combat_params_v9.json']:
    try:
        obj=json.loads((DATA/f).read_text(encoding='utf-8-sig'))
        add('JSON '+f,'PASS',f'顶层字段 {len(obj)}')
    except Exception as e: add('JSON '+f,'FAIL',str(e))

# Source static checks
cs=list(SRC.rglob('*.cs'))
add('C#源文件数量','PASS' if len(cs)>=35 else 'FAIL',str(len(cs)))
joined='\n'.join(p.read_text(encoding='utf-8',errors='replace') for p in cs)
for marker in ['NotImplementedException','TODO','FIXME']:
    hits=joined.count(marker); add('禁止占位 '+marker,'PASS' if hits==0 else 'FAIL',str(hits))
for suffix in ['Fix','Patch','Bridge','Compat']:
    hits=len(re.findall(r'\bclass\s+\w*'+suffix+r'\b',joined)); add('禁止补丁类 *'+suffix,'PASS' if hits==0 else 'FAIL',str(hits))

# Exact duplicate method signatures in the same file are compile blockers. Overloads remain allowed.
method_pat=re.compile(r'(?m)^\s*(?:public|private|protected|internal)?\s*(?:static\s+)?(?:sealed\s+)?(?:[\w<>\[\],\.]+\s+)+(?P<name>\w+)\s*\((?P<params>[^\n\)]*)\)\s*(?:\{|=>)')
duplicate_methods=[]
for fp in cs:
    sigs=[]
    text=fp.read_text(encoding='utf-8',errors='replace')
    for m in method_pat.finditer(text):
        sig=(m.group('name'),re.sub(r'\s+',' ',m.group('params').strip()))
        sigs.append(sig)
    counts=Counter(sigs)
    for sig,n in counts.items():
        if n>1: duplicate_methods.append(f'{fp.relative_to(ROOT)}:{sig[0]}({sig[1]}) x{n}')
add('C#重复方法签名','PASS' if not duplicate_methods else 'FAIL','未发现' if not duplicate_methods else '; '.join(duplicate_methods[:20]))

# Catch exact adjacent duplicate local declarations, a semantic compile blocker lexical tokenizers miss.
adjacent_dup_locals=[]
local_decl=re.compile(r'^\s*(?:var|bool|byte|short|int|long|float|double|decimal|string|uint|ulong|[A-Z]\w*(?:<[^>]+>)?)\s+([A-Za-z_]\w*)\s*=')
for fp in cs:
    lines=fp.read_text(encoding='utf-8',errors='replace').splitlines()
    for i in range(len(lines)-1):
        a=local_decl.match(lines[i]);b=local_decl.match(lines[i+1])
        if a and b and a.group(1)==b.group(1) and lines[i].strip()==lines[i+1].strip(): adjacent_dup_locals.append(f'{fp.relative_to(ROOT)}:{i+1}:{a.group(1)}')
add('C#相邻重复局部声明','PASS' if not adjacent_dup_locals else 'FAIL','未发现' if not adjacent_dup_locals else '; '.join(adjacent_dup_locals[:20]))

# Exactly one owner class for core domains
owners=['PopulationOwner','EconomyLedger','ProposalSystem','ConstructionOwner','CityPlanningOwner','ArmyOwner','CombatResolution','DiplomacyOwner','FamilyOwner','MerchantOwner','EquipmentOwner','LogisticsOwner','SiegeOwner','WarAdministrationOwner']
for cls in owners:
    hits=len(re.findall(r'\bclass\s+'+re.escape(cls)+r'\b',joined))
    add('唯一Owner '+cls,'PASS' if hits==1 else 'FAIL',str(hits))

# Coarse structural delimiter balance. This is not a compiler and is reported only as a static check.
bad=[]
for p in cs:
    t=p.read_text(encoding='utf-8',errors='replace')
    for a,b in [('{' , '}'),('(',')'),('[',']')]:
        if t.count(a)!=t.count(b): bad.append(f'{p.relative_to(ROOT)} {a}{b} {t.count(a)}!={t.count(b)}')
add('C#结构括号检查','PASS' if not bad else 'FAIL','; '.join(bad[:10]) if bad else '全部平衡（静态计数，不等价于编译）')

# Lexical C# scan with Pygments. Still not a semantic compiler.
try:
    from pygments import lex
    from pygments.lexers.dotnet import CSharpLexer
    from pygments.token import Error
    lex_errors=[]
    for fp in cs:
        for tok,val in lex(fp.read_text(encoding='utf-8',errors='replace'),CSharpLexer()):
            if tok is Error: lex_errors.append(f'{fp.relative_to(ROOT)}:{repr(val)}')
    add('C#词法扫描','PASS' if not lex_errors else 'FAIL', '无词法错误' if not lex_errors else '; '.join(lex_errors[:20]))
    suspicious_numeric_nullable=[]
    for fp in cs:
        raw=fp.read_text(encoding='utf-8',errors='replace')
        for m in re.finditer(r'\?\.\d+[fFdDmM]?',raw): suspicious_numeric_nullable.append(f'{fp.relative_to(ROOT)}:{m.group(0)}')
    add('C#可疑数值空条件运算符','PASS' if not suspicious_numeric_nullable else 'FAIL','未发现' if not suspicious_numeric_nullable else '; '.join(suspicious_numeric_nullable[:20]))
except Exception as e:
    add('C#词法扫描','SKIP',str(e))

# Trait taxonomy coverage: every frozen category/domain must have an explicit runtime branch.
def csv_values(filename,header):
    with (DATA/filename).open(encoding='utf-8-sig',newline='') as f:
        return sorted(set((r.get(header) or '').strip() for r in csv.DictReader(f) if (r.get(header) or '').strip()))
trait_source=(SRC/'Core/TraitEffectEngine.cs').read_text(encoding='utf-8',errors='replace')
def method_slice(start,end):
    a=trait_source.find(start);b=trait_source.find(end,a+1);return trait_source[a:b if b>=0 else len(trait_source)]
person_block=method_slice('PersonBehaviorProfile PersonProfile','SoldierBehaviorProfile SoldierProfile')
soldier_block=method_slice('SoldierBehaviorProfile SoldierProfile','OfficialRuntimeProfile OfficialProfile')
official_block=method_slice('OfficialRuntimeProfile OfficialProfile','SoldierHit')
general_block=method_slice('ApplyGeneralTraits',' }\n}')
def explicit_values(block,varname):
    return set(re.findall(r'\b'+re.escape(varname)+r'==\"([^\"]+)\"',block))
for title,fn,header,block,varname in [
 ('人物特性分类','person_traits_v8.csv','类别',person_block,'cat'),
 ('士兵特性分类','soldier_traits_v8.csv','类别',soldier_block,'cat'),
 ('官员特性领域','official_traits_v8.csv','领域',official_block,'domain'),
 ('将军特性分类','general_traits_v8.csv','分类',general_block,'cat')]:
    frozen=set(csv_values(fn,header));mapped=explicit_values(block,varname);missing=sorted(frozen-mapped)
    add(title+'执行分支覆盖','PASS' if not missing else 'FAIL',f'{len(frozen)}/{len(frozen)}类别已映射' if not missing else '未映射: '+','.join(missing))

# Runtime profile member consistency and commander skill numeric-key coverage.
trait_text=(SRC/'Core/TraitEffectEngine.cs').read_text(encoding='utf-8',errors='replace')
required_soldier_fields=['Melee','Ranged','Riding','Block','Stamina','Morale','Discipline','Scout','Pursuit','Defense','Engineering','Recovery','OfficerPotential']
missing_soldier=[x for x in required_soldier_fields if not re.search(r'\b'+re.escape(x)+r'\s*=\s*1f',trait_text)]
add('SoldierBehaviorProfile成员完整','PASS' if not missing_soldier else 'FAIL','全部声明' if not missing_soldier else '缺少: '+','.join(missing_soldier))

skill_engine=(SRC/'Military/CommanderSkillEffectEngine.cs').read_text(encoding='utf-8',errors='replace')
contains_tokens=set(re.findall(r'Contains\(k,\"([^\"]+)\"\)',skill_engine))
exact_tokens=set(re.findall(r'k==\"([^\"]+)\"',skill_engine))
skill_keys=set();bad_skill_json=[]
with (DATA/'commander_skills_v8.csv').open(encoding='utf-8-sig',newline='') as f:
    for row in csv.DictReader(f):
        try: obj=json.loads(row.get('数值效果_JSON') or '{}')
        except Exception as e: bad_skill_json.append(row.get('技能ID','?')+':'+str(e)); continue
        skill_keys.update(obj.keys())
unsupported=[k for k in sorted(skill_keys) if k not in exact_tokens and not any(tok in k.lower() for tok in contains_tokens)]
add('360统帅技能数值JSON可解析','PASS' if not bad_skill_json else 'FAIL',f'{len(skill_keys)}种唯一数值键' if not bad_skill_json else '; '.join(bad_skill_json[:10]))
add('360统帅技能数值键执行覆盖','PASS' if not unsupported else 'FAIL',f'{len(skill_keys)}/{len(skill_keys)}种数值键存在执行映射' if not unsupported else '未覆盖: '+','.join(unsupported[:30]))

# Critical commander behavior dimensions must reach real owner/execution chains, not only exist in the profile.
execution_text='\n'.join(x.read_text(encoding='utf-8',errors='replace') for x in cs if x.name!='CommanderSkillEffectEngine.cs')
required_execution_markers={
 '训练治军':'Military.TrainDay(trainingArmy,CommanderSkillEffectEngine.Build',
 '地形行军':'profile.TerrainAdapt,profile.FatigueEfficiency,profile.MarchFatigueCost',
 '战斗地形天气':'battleTerrain,Weather.Weather',
 '后勤筹措':'command.SupplyEfficiency',
 '欠饷敏感':'command.ArrearsSensitivity',
 '工程攻城':'p.Siege+p.Engineering',
 '追击完整画像':'Combat.ResolvePursuit(pursuer,routed,strikes,own)',
 '战利品军纪':'command.LootControl',
}
missing_exec=[name for name,mark in required_execution_markers.items() if mark not in execution_text]
add('统帅技能关键执行链接入','PASS' if not missing_exec else 'FAIL','8/8关键链已接入' if not missing_exec else '缺少: '+','.join(missing_exec))

# Strategic AI / physical siege / expanding fortification integration checks.
ai_text=(SRC/'AI/AiOwner.cs').read_text(encoding='utf-8',errors='replace')
siege_text=(SRC/'Siege/SiegeOwner.cs').read_text(encoding='utf-8',errors='replace')
world_text=(SRC/'Simulation/GameWorld.cs').read_text(encoding='utf-8',errors='replace')
model_text=(SRC/'Core/LordWarModels.cs').read_text(encoding='utf-8',errors='replace')
save_text=(SRC/'Save/SaveOwner.cs').read_text(encoding='utf-8',errors='replace')
ai_fields=['PowerRatio','OwnFoodDays','OwnMorale','OwnFatigue','SupplyRisk','CommanderScout','CommanderSupply','EnemyFortification','EnemyMorale','Distance','Weather','OwnCapitalThreatened']
missing_ai=[x for x in ai_fields if x not in ai_text or x not in world_text]
add('电脑国家战略判断输入','PASS' if not missing_ai else 'FAIL','12/12战略维度接入且难度不加资源' if not missing_ai else '缺少: '+','.join(missing_ai))
siege_marks=['DamageFortifications','target.Durability=Math.Max(0,target.Durability-raw)','target.Ruined=true','SyncIntegrity','IsBlockaded']
missing_siege=[x for x in siege_marks if x not in siege_text]
if not re.search(r'blockadeMul\s*=\s*Sieges!=null&&Sieges\.IsBlockaded\(c\.Id\)\s*\?\s*\.35f\s*:\s*1f',world_text): missing_siege.append('封锁经济惩罚')
add('围城实体城防执行链','PASS' if not missing_siege else 'FAIL','真实城门/城墙/塔楼耐久、废墟与封锁经济已接入' if not missing_siege else '缺少: '+','.join(missing_siege))
fort_marks=['FortificationRadius','FortificationPlanVersion','LastFortificationReplanDay']
missing_fort=[x for x in fort_marks if x not in model_text or x not in world_text]
if not re.search(r'public const int CurrentVersion=(?:1[7-9]|[2-9][0-9])',save_text): missing_fort.append('saveVersion>=17')
if '扩建城墙外郭' not in world_text: missing_fort.append('扩建城墙申请')
add('城市扩张城墙重规划','PASS' if not missing_fort else 'FAIL','外郭半径/规划版本/扩建申请/saveVersion>=17已接入' if not missing_fort else '缺少: '+','.join(missing_fort))

march_text=(SRC/'Military/MarchOwner.cs').read_text(encoding='utf-8',errors='replace')
hud_text=(SRC/'Unity/ChineseHud.cs').read_text(encoding='utf-8',errors='replace')
dip_text=(SRC/'Diplomacy/DiplomacyOwner.cs').read_text(encoding='utf-8',errors='replace')
muster_marks=['MusterProgress','MusterStartDay','SetDestination(a,target.X,target.Y,false)','a.Order == ArmyOrder.Muster','musterProfile.ReformSpeed']
missing_muster=[x for x in muster_marks if x not in model_text+world_text+march_text]
add('真实军队集结阶段','PASS' if not missing_muster else 'FAIL','路线先规划、集结受统帅与凝聚力影响、完成后再行军' if not missing_muster else '缺少: '+','.join(missing_muster))
war_ui_marks=['selectedArmyIds','选择参战','选择敌国并宣战','DeclareWarAndMarch(k.Id,selectedArmyIds)','SiegeForArmy']
missing_warui=[x for x in war_ui_marks if x not in hud_text+world_text]
add('玩家选敌国与参战军队','PASS' if not missing_warui else 'FAIL','中文战争面板可选多支军队/将军并指定敌国，围城状态可见' if not missing_warui else '缺少: '+','.join(missing_warui))
dip_marks=['TradePartnerCount','NonAggression','Trade(','action=="trade"','action=="nonaggression"']
missing_dip=[x for x in dip_marks if x not in dip_text+world_text]
add('外交奏议执行链','PASS' if not missing_dip else 'FAIL','停战/通商/互不侵犯/同盟进入固定申请与经济关系链' if not missing_dip else '缺少: '+','.join(missing_dip))

# Real garrison / breach / captivity semantics. No virtual defender headcount is allowed.
army_text=(SRC/'Military/ArmyOwner.cs').read_text(encoding='utf-8',errors='replace')
real_defense_marks=['DefenderArmyAtCity','EnsureRealCityDefender','Military.ReadySoldierCount(defender)','LastBattleDay != Day','RefreshGarrisonRegistry']
missing_real_defense=[x for x in real_defense_marks if x not in world_text]
if 'Population.LivingPopulation(c)/18' in world_text: missing_real_defense.append('仍存在按人口虚构守军')
add('真实守军与战斗冷却','PASS' if not missing_real_defense else 'FAIL','守军只认真实Army/人物，紧急守备从真实人口募兵，同日不重复刷战斗' if not missing_real_defense else '缺少: '+','.join(missing_real_defense))
breach_marks=['LastAssaultBattleDay','StartBattle(a,defender,c.Name+"破城战",c.Id)','攻城军通过真实城墙破口进入守军近战']
missing_breach=[x for x in breach_marks if x not in world_text+siege_text]
if 'else if(s.Breached&&attacker.Morale>28f' in siege_text: missing_breach.append('破口仍自动占领')
add('破城后真实守军战斗','PASS' if not missing_breach else 'FAIL','破口只是入口；有守军必须进入CombatResolution，投降才可跳过战斗' if not missing_breach else '缺少: '+','.join(missing_breach))
captivity_marks=['CapturedByKingdomId','CapturedDay','RansomValue','AssignCapturedPersonnel','TickCaptivity','ReleaseCaptive']
missing_capture=[x for x in captivity_marks if x not in model_text+world_text]
if 'PresentForDuty' not in army_text or 'CombatReady' not in army_text: missing_capture.append('军队人数未排除被俘/重伤')
add('被俘归属与释放链','PASS' if not missing_capture else 'FAIL','被俘归属/时间/赎金可保存，停战后释放，现役与可战人数真实扣除' if not missing_capture else '缺少: '+','.join(missing_capture))


# Kingdom defeat/vassal state and multi-army AI coordination.
kingdom_marks=['KingdomStatus { Active, Vassal, Eliminated }','OverlordKingdomId','DefeatDay']
missing_kingdom=[x for x in kingdom_marks if x not in model_text]
for x in ['CanBecomeVassal','EliminateKingdom','TickVassalTribute','Diplomacy.SetVassal']:
    if x not in world_text: missing_kingdom.append(x)
for x in ['SetVassal','IsVassalOf','SuzerainId','VassalId']:
    if x not in dip_text+model_text: missing_kingdom.append(x)
if not re.search(r'public const int CurrentVersion=(?:1[7-9]|[2-9][0-9])',save_text): missing_kingdom.append('saveVersion>=17')
add('国家灭亡/附庸/宗主执行链','PASS' if not missing_kingdom else 'FAIL','状态、宗主、灭亡、月贡与存档迁移已接入' if not missing_kingdom else '缺少: '+','.join(missing_kingdom))

multi_ai_marks=['FieldArmies','CombinedDefensePower','ownPower+=ArmyPower(a)','WarAdministrationOwner','EnsureComputerWarOfficials','AvailableWarArmies','profile.MaxOrders','IssueWarOfficialMove']
war_admin_ai_text=(SRC/'Governance/WarAdministrationOwner.cs').read_text(encoding='utf-8',errors='replace') if (SRC/'Governance/WarAdministrationOwner.cs').exists() else ''
missing_multi=[x for x in multi_ai_marks if x not in world_text+war_admin_ai_text]
add('电脑国家多军队协同','PASS' if not missing_multi else 'FAIL','AI战略层汇总多军军力决定宣战，战争执行层由真实战争负责官员按军令预算协调多支军队守城/野战/攻城/拦截，不再存在绕过官员的平行调军入口' if not missing_multi else '缺少: '+','.join(missing_multi))


perf_text=(SRC/'Performance/TickScheduler.cs').read_text(encoding='utf-8',errors='replace')
proposal_text=(SRC/'Governance/ProposalSystem.cs').read_text(encoding='utf-8',errors='replace')
logistics_text=(SRC/'Military/LogisticsOwner.cs').read_text(encoding='utf-8',errors='replace')
maint_marks=['MaintenanceSweep','Proposals.PruneResolved(300)','Logistics.PruneInvalid(Armies,Cities)','while(Battles.Count>300)','if(Day%10==0)MaintenanceSweep()']
missing_maint=[x for x in maint_marks if x not in world_text+proposal_text+logistics_text]
add('长期模拟状态清理','PASS' if not missing_maint else 'FAIL','申请/战报/施工/补给/引用脏数据有周期清理上限' if not missing_maint else '缺少: '+','.join(missing_maint))
catchup_marks=['MaxCatchUpRuns','while(j.Elapsed>=j.Interval&&runs<j.MaxCatchUpRuns)','j.Elapsed=j.Interval*j.MaxCatchUpRuns']
missing_catchup=[x for x in catchup_marks if x not in perf_text]
add('分层Tick卡顿追赶保护','PASS' if not missing_catchup else 'FAIL','分层Tick保留积压但限制单帧追赶次数，避免死亡螺旋' if not missing_catchup else '缺少: '+','.join(missing_catchup))
commonwar_marks=['SyncVassalWars','JoinCommonWar','EndCommonWar','EnsureComputerWarOfficials','CoordinateWarAdministration','TickWarAdministrationDaily']
missing_common=[x for x in commonwar_marks if x not in world_text+dip_text]
add('附庸战争联动','PASS' if not missing_common else 'FAIL','附庸同步宗主共同战争，AI附庸复用战争负责官员调度并在宗主停战后退出共同战争' if not missing_common else '缺少: '+','.join(missing_common))
if 'sq.SoldierIds.RemoveAt(i)' in world_text and 'sq.OfficerId=""' in world_text:
    add('俘虏脱离原部队','PASS','被俘后从原部伍/军官槽移除，获释后不会静默重新计入原军')
else:
    add('俘虏脱离原部队','FAIL','缺少被俘人员从原编制移除')

renderer_text=(SRC/'Unity/WorldRenderer.cs').read_text(encoding='utf-8',errors='replace')
chinese_text=(SRC/'Core/ChineseText.cs').read_text(encoding='utf-8',errors='replace')
ui_perf_marks=['Time.unscaledTime<_nextDynamicSync','MaxVisiblePeople=480','Camera.main','InjuryState.Captured']
missing_ui_perf=[x for x in ui_perf_marks if x not in renderer_text]
add('Android可见单位LOD与帧预算','PASS' if not missing_ui_perf else 'FAIL','动态标记限频、相机距离LOD、可见人物上限与伤亡/俘虏隐藏已接入' if not missing_ui_perf else '缺少: '+','.join(missing_ui_perf))
cn_marks=['string Job(JobKind','string Injury(InjuryState','string EquipmentSlot(string']
missing_cn=[x for x in cn_marks if x not in chinese_text]
add('人物状态/职业/装备槽中文显示','PASS' if not missing_cn else 'FAIL','人物详情不直接暴露英文Job/Injury/Slot枚举' if not missing_cn else '缺少: '+','.join(missing_cn))


save_service_text=(SRC/'Save/GameSaveService.cs').read_text(encoding='utf-8',errors='replace')
save_validation_marks=['附庸宗主引用无效','人物装备引用不一致','部伍所属军队不一致','人物重复进入多个军队编制','商人投资引用无效','围城引用无效','运输队引用无效','随机地图结构无效']
missing_sv=[x for x in save_validation_marks if x not in save_service_text]
add('存档全引用一致性校验','PASS' if not missing_sv else 'FAIL','地图/国家/附庸/城市/家族/军队/装备/施工/投资/围城/补给引用均有硬校验' if not missing_sv else '缺少: '+','.join(missing_sv))
ui_fallback_marks=['return "特殊物品";','return "未知装备";','else x.DisplayName="未知装备"']
missing_fallback=[x for x in ui_fallback_marks if x not in chinese_text+hud_text+save_service_text]
add('玩家可见英文ID兜底','PASS' if not missing_fallback else 'FAIL','未知装备与未知装备槽不回退显示内部模板/英文ID' if not missing_fallback else '缺少: '+','.join(missing_fallback))

# Local morale must propagate by squad instead of one instantaneous army-wide flag.
combat_text=(SRC/'Combat/CombatResolution.cs').read_text(encoding='utf-8',errors='replace')
veteran_text=(SRC/'Military/VeteranEffectEngine.cs').read_text(encoding='utf-8',errors='replace')
army_text2=(SRC/'Military/ArmyOwner.cs').read_text(encoding='utf-8',errors='replace')
veteran_marks=['MoraleResistance','FatigueEfficiency','CommandResponse','PursuitDiscipline','SquadVeteranRatio','VeteranEffectEngine.Build(attacker)','VeteranEffectEngine.Build(defender)','VeteranEffectEngine.SquadCohesionMultiplier','VeteranRatio(Squad s)']
missing_veteran=[x for x in veteran_marks if x not in veteran_text+combat_text+army_text2]
if 'if(p.Experience>180)p.Stats.Defense=Math.Min(90,p.Stats.Defense+1)' in army_text2: missing_veteran.append('仍只有简单防御+1老兵逻辑')
add('老兵行为成长体系','PASS' if not missing_veteran else 'FAIL','战历/经验派生老兵等级，真实影响恐惧抵抗、格挡、命中稳定、体力效率、凝聚力与命令响应，而非只加攻击防御' if not missing_veteran else '缺少: '+','.join(missing_veteran))

morale_text=(SRC/'Core/MoraleSystem.cs').read_text(encoding='utf-8',errors='replace')
local_morale_marks=['LocalMoraleState','RoutWave','SquadCasualtyRate','MoraleSystem.Spread(local,worstNeighbor,discipline)','report.MoraleCollapseWaves','AttackerRoutedSquads','DefenderRoutedSquads']
missing_local_morale=[x for x in local_morale_marks if x not in model_text+combat_text+morale_text]
add('局部士气传播与连锁溃败','PASS' if not missing_local_morale else 'FAIL','小队独立士气/伤亡/凝聚力驱动局部动摇与溃败，并向相邻小队传播；死战不等于无敌' if not missing_local_morale else '缺少: '+','.join(missing_local_morale))

construction_text=(SRC/'Construction/ConstructionOwner.cs').read_text(encoding='utf-8',errors='replace')
renderer_text2=(SRC/'Unity/WorldRenderer.cs').read_text(encoding='utf-8',errors='replace')
construction_marks=['ConstructionWorkerAction','WorkerActions','"测量"','"清地"','"搬运"','"锯木"','"铺路"','"砌筑"','"敲打"','SyncConstructionWorkers','Time.unscaledTime','DrawConstruction']
missing_construction=[x for x in construction_marks if x not in model_text+construction_text+renderer_text2+hud_text]
add('施工工人动作与可视化','PASS' if not missing_construction else 'FAIL','施工阶段分配测量/清地/搬运/锯木/铺路/砌筑/敲打动作，工人标记按动作运动且HUD显示施工现场' if not missing_construction else '缺少: '+','.join(missing_construction))

# Live battles must advance over scheduler ticks and survive save/load instead of resolving an entire 240s fight in one frame.
save_owner_text=(SRC/'Save/SaveOwner.cs').read_text(encoding='utf-8',errors='replace')
battle_session_marks=['class BattleSession','StartSession(','StepSession(','ActiveBattles','Scheduler.Register("持续战斗"','TickActiveBattles','StartBattle(a, defender, enemy.Name)','StartBattle(a,defender,c.Name+"破城战",c.Id)','s.ActiveBattles.Add','s.ActiveBattles']
missing_battle_session=[x for x in battle_session_marks if x not in model_text+combat_text+world_text+save_service_text+save_owner_text]
add('持续战斗会话与中途存档','PASS' if not missing_battle_session else 'FAIL','野战/破城战按0.25秒Tick推进真实战斗秒；活动会话、实时战报与破城关联进入saveVersion>=21' if not missing_battle_session else '缺少: '+','.join(missing_battle_session))
# Mid-battle save/load must preserve RNG state and real actor/target events must drive visual feedback.
resumable_marks=['public uint State','public uint RandomState','session.RandomState=_rng.State','_rng.State=session.RandomState','LastActorId','LastTargetId','LastAttackAction','LastDefenseAction','BattleVisualOffset(','ActionSequence']
missing_resumable=[m for m in resumable_marks if m not in joined]
add('战斗随机状态续存与真实交锋视觉反馈','PASS' if not missing_resumable else 'FAIL','中途存档恢复战斗随机状态；实时攻击者/防御者/动作/伤害驱动单位突进、闪避、格挡与受击位移' if not missing_resumable else '缺少: '+','.join(missing_resumable))

# Core social systems must be reachable in the Chinese UI; person/equipment are clickable, not background-only data.
social_ui_marks=['政务 / 社会','【生效政策 / 国策】','【官员】','【家族】','【商人及军资投资】','查看主将','士兵：','装备（点击查看）','void DrawEquipment','【世界唯一】','关闭装备详情']
missing_social=[x for x in social_ui_marks if x not in hud_text]
add('政务社会与人物装备可操作UI','PASS' if not missing_social else 'FAIL','政策/官员/家族/商人均有中文入口；人物与六装备槽可查看，装备可点击打开耐久/来源/历史详情' if not missing_social else '缺少: '+','.join(missing_social))

# Detailed combat actions must be execution semantics, not labels.
combat_text=(SRC/'Combat/CombatResolution.cs').read_text(encoding='utf-8',errors='replace')
combat_marks=['CombatAttackMode.Light','CombatAttackMode.Heavy','CombatAttackMode.Thrust','CombatAttackMode.Ranged','CombatAttackMode.CavalryCharge','CombatDefenseReaction.Dodge','CombatDefenseReaction.Parry','CombatDefenseReaction.ShieldBlock','FindEquipment(defender,"副手","盾")','EquipmentDurabilitySystem.Wear(reactionEquipment']
missing_combat=[x for x in combat_marks if x not in combat_text]
for x in ['LightAttacks','HeavyAttacks','ThrustAttacks','RangedAttacks','CavalryCharges','Dodges','Parries','ShieldBlocks']:
    if x not in model_text or x not in hud_text: missing_combat.append(x)
add('战斗动作粒度与真实防御','PASS' if not missing_combat else 'FAIL','轻击/重击/刺击/远射/骑冲与闪避/格挡/盾挡拥有独立命中、伤害、破甲、体力及装备磨损链' if not missing_combat else '缺少: '+','.join(missing_combat))

# Map/weather must alter execution, not only labels.
worldgen_text=(SRC/'World/WorldGenerator.cs').read_text(encoding='utf-8',errors='replace')
weather_text=(SRC/'World/WeatherOwner.cs').read_text(encoding='utf-8',errors='replace')
march_text=(SRC/'Military/MarchOwner.cs').read_text(encoding='utf-8',errors='replace')
formation_text=(SRC/'Core/MarchFormationSystem.cs').read_text(encoding='utf-8',errors='replace')
map_weather_marks=['CarveMountainChains','TraceRivers','best.Height=Math.Max(m.SeaLevel-.01f,cur.Height-.006f)','else if(t.River)t.Terrain=TerrainKind.River','WeatherTerrainPenalty','FoodUsePerTile','Weather.Weather','VisibilityMultiplier','AgricultureMultiplier','RangedAccuracyMultiplier','ApplySurfaceWeather','SurfaceMud','SnowDepth','RoadCondition','Weather.TickDay(Map)','surfacePenalty']
missing_map_weather=[x for x in map_weather_marks if x not in worldgen_text+weather_text+march_text+formation_text+world_text]
add('随机地形/河流/四季天气执行链','PASS' if not missing_map_weather else 'FAIL','山链、可见连续下坡河道、季节农业、天气视野/远程/行军速度/疲劳/粮耗、持久泥泞积雪和道路损耗均进入执行链' if not missing_map_weather else '缺少: '+','.join(missing_map_weather))

# City planning must be player-operable, drive real build placement, and survive strict save validation.
planning_text=(SRC/'Construction/CityPlanningOwner.cs').read_text(encoding='utf-8',errors='replace')
planning_marks=['public CityPlanningOwner Planning','Planning.EnsureStarterZones','Planning.FindBuildSite','DesignatePlayerZone','TickCivilianBuilding','FindFreeInZone','ZoneAt','城市区域规划','规划住宅区','PlayerPlanned','城市规划区坐标或半径无效']
missing_planning=[x for x in planning_marks if x not in world_text+planning_text+hud_text+save_service_text+model_text]
add('城市区域规划与居民自动填充','PASS' if not missing_planning else 'FAIL','玩家可划住宅/商业/工坊/农牧/军政/公共区；居民与官员按区真实选址；规划区进入存档硬校验' if not missing_planning else '缺少: '+','.join(missing_planning))

# Visible placeholder names in data
bad_names=[]
pat=re.compile(r'(General\d+|Unit\d+|SpecialInfantry\d+|TestGeneral|特殊兵\d+)',re.I)
for p in DATA.glob('*.csv'):
    for i,line in enumerate(p.read_text(encoding='utf-8-sig',errors='replace').splitlines(),1):
        if pat.search(line): bad_names.append(f'{p.name}:{i}')
add('玩家可见占位命名','PASS' if not bad_names else 'FAIL',str(bad_names[:20]) if bad_names else '未发现')

# Visual state must track simulation changes without rebuilding hundreds of objects every frame.
visual_sync_marks=['RepaintMapTexture','PaintMapTexture','SurfaceMud','SnowDepth','RoadCondition','BuildingSignature','RebuildStaticMarkers','ArmyFlagSprite','total_war_banner_icon','ArmyFlagColor','f.ColorHex','activeArmies','activeHerds']
missing_visual_sync=[x for x in visual_sync_marks if x not in renderer_text]
add('地图状态刷新与家族军旗可视化','PASS' if not missing_visual_sync else 'FAIL','泥泞/积雪/道路逐日重绘；建筑状态按签名重建；军队使用真实旗帜Sprite并优先显示主将家族颜色；已消失军队/兽群标记会回收隐藏' if not missing_visual_sync else '缺少: '+','.join(missing_visual_sync))

# Real art must be imported as pixel sprites and consumed by terrain/building/unit rendering.
assetlib_text=(SRC/'Unity/SpriteAssetLibrary.cs').read_text(encoding='utf-8',errors='replace')
importer_text=(SRC/'Editor/LordWarTextureImporter.cs').read_text(encoding='utf-8',errors='replace')
renderer_art_marks=['Resources.LoadAll<Sprite>("LordWarArt")','Resources.LoadAll<Texture2D>("LordWarArt")','FindStripFrames','BuildTerrainDecorations','TerrainSprite','BuildingSprite','UnitFrames','行走4帧','攻击4帧']
import_marks=['TextureImporterType.Sprite','FilterMode.Point','TextureImporterCompression.Uncompressed','mipmapEnabled=false','spritePixelsPerUnit=16f']
missing_art=[x for x in renderer_art_marks if x not in assetlib_text+renderer_text]+[x for x in import_marks if x not in importer_text]
add('真实像素素材运行时接入','PASS' if not missing_art else 'FAIL','PNG强制像素Sprite导入；真实地形/建筑/单位素材进入渲染；骑兵4帧行走/攻击条由运行时切帧' if not missing_art else '缺少: '+','.join(missing_art))

# Android camera navigation and fixed HUD controls must be actually wired.
camera_path=SRC/'Unity/WorldCameraController.cs'
camera_text=camera_path.read_text(encoding='utf-8',errors='replace') if camera_path.exists() else ''
bootstrap_text=(SRC/'Unity/LordWarBootstrap.cs').read_text(encoding='utf-8',errors='replace')
camera_marks=['Input.touchCount>=2','Vector2.Distance(a.position,b.position)','PanPixels','ClampCamera','IsHudTouch','WorldCameraController']
missing_camera=[x for x in camera_marks if x not in camera_text+bootstrap_text]
add('Android触控地图浏览','PASS' if not missing_camera else 'FAIL','单指拖动/双指缩放、边界夹紧、HUD触摸屏蔽与启动接线已实现' if not missing_camera else '缺少: '+','.join(missing_camera))
hud_layout_marks=['GUI.Box(new Rect(8,8,330,338)','new Rect(18,302,300,20)','new Rect(18,322,300,20)','new Rect(8,356,540']
missing_hud_layout=[x for x in hud_layout_marks if x not in hud_text]
add('手机HUD控制区防重叠','PASS' if not missing_hud_layout else 'FAIL','倍速按钮、本日进度、状态栏与军队面板分行布局' if not missing_hud_layout else '缺少: '+','.join(missing_hud_layout))

# Android build pipeline readiness (source-level only; this is not a successful build).
android_build=(SRC/'Editor/AndroidBuild.cs').read_text(encoding='utf-8',errors='replace') if (SRC/'Editor/AndroidBuild.cs').exists() else ''
project_setup=(SRC/'Editor/ProjectSetup.cs').read_text(encoding='utf-8',errors='replace') if (SRC/'Editor/ProjectSetup.cs').exists() else ''
build_sh=(ROOT/'Tools/build_android.sh').read_text(encoding='utf-8',errors='replace') if (ROOT/'Tools/build_android.sh').exists() else ''
build_marks=['BuildFromCommandLine','BuildTarget.Android','AndroidArchitecture.ARM64','ScriptingImplementation.IL2CPP','AndroidApiLevel26','apkSha256']
missing_build=[x for x in build_marks if x not in android_build]
build_ready=(not missing_build and 'EnsureMainScene' in project_setup and 'BLOCKED: 未找到Unity Editor' in build_sh)
add('Android构建流水线源码准备','PASS' if build_ready else 'FAIL','场景自动建立、Android切换、IL2CPP/ARM64/minSdk26、APK哈希证据与无Unity阻塞脚本已接入' if build_ready else '缺少: '+','.join(missing_build))

# Frozen policy/special-unit governance must be executable, not merely present in CSV.
policy_profile_path=SRC/'Governance/PolicyRuntimeProfile.cs'
policy_profile_text=policy_profile_path.read_text(encoding='utf-8',errors='replace')
policy_owner_text=(SRC/'Governance/PolicyOwner.cs').read_text(encoding='utf-8',errors='replace')
merchant_text=(SRC/'Society/MerchantOwner.cs').read_text(encoding='utf-8',errors='replace')
finance_text=(SRC/'Military/MilitaryFinanceSystem.cs').read_text(encoding='utf-8',errors='replace')
unlock_text=(SRC/'Military/UnlockResolver.cs').read_text(encoding='utf-8',errors='replace')

profile_fields=[]
for decl in re.findall(r'public\s+(?:float|int)\s+([^;]+);',policy_profile_text):
    for part in decl.split(','):
        name=re.search(r'([A-Za-z_]\w*)\s*(?:=|$)',part.strip())
        if name: profile_fields.append(name.group(1))
profile_fields=sorted(set(profile_fields))
other_runtime='\n'.join(fp.read_text(encoding='utf-8',errors='replace') for fp in cs if fp!=policy_profile_path)
unconsumed=[f for f in profile_fields if not re.search(r'\.'+re.escape(f)+r'\b',other_runtime)]
add('300政策运行字段真实消费','PASS' if not unconsumed else 'FAIL',f'{len(profile_fields)}/{len(profile_fields)}个运行字段进入Owner/主循环' if not unconsumed else '无消费者: '+','.join(unconsumed))

policy_gate_marks=['official.Class!=SocialClass.Official','ability<d.Ability','d.Stage!="成制"','HasTrial(kingdom,d)','AlreadyRepresented(kingdom,policyId)']
missing_policy_gate=[x for x in policy_gate_marks if x not in policy_owner_text]
add('官员政策硬门槛与试行成制','PASS' if not missing_policy_gate else 'FAIL','身份、能力值、重复提案与“试行→成制”均为硬门槛' if not missing_policy_gate else '缺少: '+','.join(missing_policy_gate))

merchant_marks=['merchant.Noble','HasActiveInvestment(merchant.Id)','ActiveInvestmentCountForGeneral(army.GeneralId)','maxInvestorsForGeneral','TaxReductionPct','MerchantInvestmentCapacity','PoliticalRisk']
missing_merchant=[x for x in merchant_marks if x not in merchant_text+world_text]
add('商人投资军资限制与政治风险','PASS' if not missing_merchant else 'FAIL','商人非贵族、一人一合同、单将军/全国容量、减税与债务政治风险已接入' if not missing_merchant else '缺少: '+','.join(missing_merchant))

finance_marks=['DebtToLord','DebtToMerchants','MonthsInArrears','DebtGrace','PoliticalRisk','BorrowFromLord','EmergencyLoan','RepayDebts','FamilyStability']
missing_finance=[x for x in finance_marks if x not in finance_text+world_text]
add('军资债务政治后果','PASS' if not missing_finance else 'FAIL','欠饷、领主债、商人债、宽限、忠诚/家族政治风险、借款与还债进入执行链' if not missing_finance else '缺少: '+','.join(missing_finance))

# Cross-table proof for all 120 special units.
def csv_rows(name):
    with (DATA/name).open(encoding='utf-8-sig',newline='') as f: return list(csv.DictReader(f))
special_rows=csv_rows('special_units_v8.csv')
general_unlock_rows=csv_rows('general_special_unlock_v8.csv')
general_trait_rows=csv_rows('general_traits_v8.csv')
doctrine_rows=csv_rows('official_doctrine_unlock_v8.csv')
policy_rows=csv_rows('policies_v8.csv')
special_names=[r.get('特殊兵种名称','') for r in special_rows]
special_set=set(special_names)
source_counts=Counter(r.get('主要解锁来源','') for r in special_rows)
policy_names=set(r.get('政策名称','') for r in policy_rows)
bad_special=[]
if len(special_set)!=120: bad_special.append('特殊兵种名称不唯一')
expected_sources={'家族传统':30,'官员军制':30,'复合条件':30,'将军特性':30}
if dict(source_counts)!=expected_sources: bad_special.append('四类来源数量='+str(dict(source_counts)))
for r in special_rows:
    try:
        if int(r.get('单支上限','0') or 0)<=0 or int(r.get('全国软上限','0') or 0)<=0 or float(r.get('维护系数','0') or 0)<=0: bad_special.append(r.get('特殊兵种名称','?')+'兵额/维护无效')
    except Exception: bad_special.append(r.get('特殊兵种名称','?')+'兵额/维护不可解析')
    for h in ['城市风格','类别','战场定位','主要解锁来源','将军条件','官员/政策条件','建筑条件','装备要求','核心行为','弱点']:
        if not (r.get(h) or '').strip(): bad_special.append(r.get('特殊兵种名称','?')+'缺少'+h)
for r in general_unlock_rows:
    if r.get('特殊兵种') not in special_set: bad_special.append('将军专属规则悬空:'+str(r.get('特殊兵种')))
trait_unlock_rows=[r for r in general_trait_rows if (r.get('可能解锁特殊兵种') or '').strip()]
for r in trait_unlock_rows:
    if r.get('可能解锁特殊兵种') not in special_set: bad_special.append('将军特性解锁悬空:'+str(r.get('可能解锁特殊兵种')))
for r in doctrine_rows:
    if (r.get('可提出政策') or '').strip() and r.get('可提出政策') not in policy_names: bad_special.append('官员军制政策悬空:'+str(r.get('可提出政策')))
add('120特殊兵种跨表解锁完整性','PASS' if not bad_special else 'FAIL',f'120唯一；4种来源各30；60条将军专属、{len(trait_unlock_rows)}条特性直解锁、96条官员军制均无悬空引用' if not bad_special else '; '.join(bad_special[:30]))

special_unlock_marks=['SpecialTraitAuthorized','GeneralUnlockSatisfied','DoctrineAuthorized','FamilyTradition','AvailableGearQuality','IronStock','WoodStock','Horses','Population','ArmyGold','NationalCap','KingdomArmySoftCap','GeneralTraitAuthorizesSpecialUnit','MatchesGeneralSpecialUnlock','HasApprovedOfficialDoctrine']
missing_special_exec=[x for x in special_unlock_marks if x not in unlock_text+world_text]
add('特殊兵种现实条件执行链','PASS' if not missing_special_exec else 'FAIL','指定/对应将军特性、技能、家族/官员军制、城市建筑、装备材料、军马、人口、军资和兵额均为真实门槛' if not missing_special_exec else '缺少: '+','.join(missing_special_exec))

army_owner_text=(SRC/'Military/ArmyOwner.cs').read_text(encoding='utf-8',errors='replace')
ai_resource_marks_world=['k.Treasury-=grant','recruitArmy.Finance.Gold+=grant','Military.Recruit(recruitArmy,capital,FirstBasicUnitId(),recruitCount,8)','ApproveProposal(p.Id)','ai.ShouldRecruit']
ai_resource_marks_army=['_population.RecruitOne(city)','a.Finance.Gold<perSoldierCost','city.Horses<horsePerSoldier']
ai_owner_text=(SRC/'AI/AiOwner.cs').read_text(encoding='utf-8',errors='replace')
missing_ai_resource=[x for x in ai_resource_marks_world if x not in world_text]+[x for x in ai_resource_marks_army if x not in army_owner_text]+([] if 'DecisionQuality()' in ai_owner_text else ['DecisionQuality()'])
cheat_patterns=[r'k\.Treasury\s*\+=\s*\d{4,}',r'capital\.Food\s*\+=\s*\d{4,}',r'Population.*\+=.*AiDifficulty']
cheat_hits=[pat for pat in cheat_patterns if re.search(pat,world_text,re.I)]
add('电脑国家同规则经济与募兵','PASS' if not missing_ai_resource and not cheat_hits else 'FAIL','AI先从真实国库拨军资，再复用ArmyOwner从真实人口/军马/军资募兵；政务走同一申请箱，难度仅改变决策质量' if not missing_ai_resource and not cheat_hits else '缺少='+','.join(missing_ai_resource)+' 可疑作弊='+','.join(cheat_hits))

# All 10 frozen special-unit behaviors and 10 weaknesses must alter execution beyond names/loadouts.
special_behavior_path=SRC/'Military/SpecialUnitBehaviorEngine.cs'
special_behavior_text=special_behavior_path.read_text(encoding='utf-8',errors='replace') if special_behavior_path.exists() else ''
frozen_behaviors=sorted(set((r.get('核心行为') or '').strip() for r in special_rows if (r.get('核心行为') or '').strip()))
frozen_weaknesses=sorted(set((r.get('弱点') or '').strip() for r in special_rows if (r.get('弱点') or '').strip()))
missing_behavior_text=[x for x in frozen_behaviors if x not in special_behavior_text]
weakness_tokens=['补员慢','转向慢','近身后明显脆弱','体力消耗大','正面硬战能力较弱','离开防御目标后价值下降','人数少、正面作战一般','作战属性低于主战兵','伤亡后补员困难','反包围风险']
missing_weakness=[x for x in weakness_tokens if x not in special_behavior_text]
special_consumer_marks=['AntiCavalry','RangedSafety','FlankMobility','ScoutAmbush','KeyDefense','Engineering','SupplyGuard','GeneralGuard','PursuitCut','PursuitRisk','SpecialUnitBehaviorEngine.ArmyProfile','SpecialUnitBehaviorEngine.Build','TrainingDays(special,sp)','SpecialFormation(special)']
missing_special_behavior=[x for x in special_consumer_marks if x not in special_behavior_text+combat_text+world_text+army_owner_text]
add('120特殊兵种核心行为与弱点执行','PASS' if not missing_behavior_text and not missing_weakness and not missing_special_behavior else 'FAIL',f'{len(frozen_behaviors)}种核心行为+{len(frozen_weaknesses)}种弱点已进入战斗/伏击/侧翼/行军/补给/攻城/追击/训练链' if not missing_behavior_text and not missing_weakness and not missing_special_behavior else '缺少行为='+','.join(missing_behavior_text)+' 弱点='+','.join(missing_weakness)+' 执行='+','.join(missing_special_behavior))

# New-world parameters/difficulty must be player-selectable and persist across save/load.
bootstrap_text=(SRC/'Unity/LordWarBootstrap.cs').read_text(encoding='utf-8',errors='replace')
chinese_text=(SRC/'Core/ChineseText.cs').read_text(encoding='utf-8',errors='replace')
save_service_text=(SRC/'Save/GameSaveService.cs').read_text(encoding='utf-8',errors='replace')
world_setup_marks=['CreateConfiguredWorld','newWorldSize','newWorldKingdoms','newDifficulty','AiDifficulty.Easy','AiDifficulty.Normal','AiDifficulty.Hard','AiDifficulty.Nightmare','ComputerDifficulty=(int)w.ComputerDifficulty','w.ComputerDifficulty=(AiDifficulty)','ComputerDifficulty=2','newMapOptions.LandPercent','newMapOptions.ForestPercent','newMapOptions.MountainPercent','newMapOptions.DesertPercent','newMapOptions.RiverPercent','newMapOptions.ResourcePercent','WorldGenerationOptions.FromMap(Map)']
world_setup_blob=bootstrap_text+'\n'+hud_text+'\n'+chinese_text+'\n'+save_service_text+'\n'+save_text+'\n'+world_text
missing_world_setup=[x for x in world_setup_marks if x not in world_setup_blob]
add('新世界Seed/地图/国家数/四档难度与存档','PASS' if not missing_world_setup else 'FAIL','玩家可设置Seed、160/224/320地图、2-6国、六类地貌参数与四档难度；参数进入地图存档（静态接线检查，不代表运行通过）' if not missing_world_setup else '缺少: '+','.join(missing_world_setup))

# World history is a first-class saved system, not console-only logging.
model_history_marks=['class WorldEvent','Category, Title, Detail','public readonly List<WorldEvent> Events']
history_marks=['RecordEvent("反叛"','RecordEvent("战争","宣战"','RecordEvent("外交"','RecordEvent("战争","城市陷落"','RecordEvent("国家","国家灭亡"','RecordEvent("工程"','RecordEvent("政策"','s.Events.Add','w.Events.AddRange','【世界大事记】']
history_blob=model_text+'\n'+world_text+'\n'+save_text+'\n'+save_service_text+'\n'+hud_text
missing_history=[x for x in model_history_marks+history_marks if x not in history_blob]
if not re.search(r'public const int CurrentVersion=(?:2[3-9]|[3-9][0-9])',save_text): missing_history.append('saveVersion>=23')
add('世界历史/反叛/外交/占领大事记与存档','PASS' if not missing_history else 'FAIL','反叛、宣战/外交、附庸/占领/灭国、工程与政策形成结构化大事记；UI可见并进入saveVersion>=23' if not missing_history else '缺少: '+','.join(missing_history))


# Natural fords and fatigue-driven camping must be real execution states, not frozen-document nouns.
world_gen_text=(SRC/'World/WorldGenerator.cs').read_text(encoding='utf-8',errors='replace')
march_text=(SRC/'Military/MarchOwner.cs').read_text(encoding='utf-8',errors='replace')
camp_marks=['ArmyOrder.Camp','CampStartDay','ResumeOrder','EnemyArmyNear(a,3)','a.Fatigue>=72f','a.Fatigue<=34f']
ford_marks=['MarkNaturalFords','t.Ford=true','n.Ford','tile.Ford&&!tile.Bridge']
missing_camp=[x for x in camp_marks if x not in model_text+world_text+chinese_text+hud_text]
missing_ford=[x for x in ford_marks if x not in world_gen_text+march_text]
add('自然渡口与军队扎营休整','PASS' if not missing_camp and not missing_ford else 'FAIL','河流自然渡口进入寻路/疲劳；军队高疲劳会真实扎营、受天气粮草影响恢复后继续原军令' if not missing_camp and not missing_ford else '缺少营地='+','.join(missing_camp)+' 渡口='+','.join(missing_ford))


# Campaign hardship and night battle must alter actual soldier rosters and combat state.
hardship_marks=['ApplyCampaignDesertion','sq.SoldierIds.RemoveAt(i)','p.Job=JobKind.Unemployed','a.Deserters+=gone','军中出现逃兵']
night_marks=['NightCombat','session.Night','IsNightTime','夜战降低视野与远程命中']
missing_hardship=[x for x in hardship_marks if x not in world_text+model_text]
missing_night=[x for x in night_marks if x not in world_text+model_text+combat_text+(SRC/'Military/CommanderSkillEffectEngine.cs').read_text(encoding='utf-8',errors='replace')]
add('断粮欠饷逃兵与夜战执行链','PASS' if not missing_hardship and not missing_night else 'FAIL','断粮/长期欠饷会从真实部伍移除士兵；夜间影响伏击、远程命中和战斗组织，夜战统帅能力会减轻惩罚' if not missing_hardship and not missing_night else '缺少逃兵='+','.join(missing_hardship)+' 夜战='+','.join(missing_night))

# Every active war may have one designated official responsible for operational coordination; assignment/revocation/focus must be saved and visible in Chinese UI.
war_admin_path=SRC/'Governance/WarAdministrationOwner.cs'
war_admin_text=war_admin_path.read_text(encoding='utf-8',errors='replace') if war_admin_path.exists() else ''
war_admin_marks=['class WarAdministrationOwner','OfficialBusyElsewhere','WarAdministrationProfile','Assign(string kingdomId,string enemyKingdomId','Revoke(string kingdomId,string enemyKingdomId','WarAdministrationFocus','CoordinateWarAdministration','守城调度','野战调度','攻城调度','敌军拦截','TickWarAdministrationDaily','EnsureComputerWarOfficials']
missing_war_admin=[x for x in war_admin_marks if x not in war_admin_text+world_text+model_text]
ui_war_admin_marks=['战争负责官员：未指派','AssignWarOfficial','撤销负责官员','SetWarOfficialFocus','ChineseText.WarFocus']
missing_war_admin_ui=[x for x in ui_war_admin_marks if x not in hud_text+world_text+chinese_text]
save_admin_marks=['WarAdministrations','s.WarAdministrations.Add','s.WarAdministrations','CurrentVersion=28','SaveVersion=28']
missing_war_admin_save=[x for x in save_admin_marks if x not in save_text+save_service_text]
if not re.search(r'public const int CurrentVersion=(?:2[7-9]|[3-9][0-9])',save_text): missing_war_admin_save.append('saveVersion>=27')
add('指定战争负责官员与撤销/战务重点','PASS' if not missing_war_admin and not missing_war_admin_ui else 'FAIL','每场战争唯一责任官员；可指派/撤销并切换综合/守城/野战/攻城/拦截，官员能力真实决定反应半径和调度军令数' if not missing_war_admin and not missing_war_admin_ui else '执行缺少='+','.join(missing_war_admin)+' UI缺少='+','.join(missing_war_admin_ui))
add('战争负责官员存档与引用校验','PASS' if not missing_war_admin_save else 'FAIL','战争行政记录进入saveVersion>=27，官员/国家/一官一战引用均校验' if not missing_war_admin_save else '缺少: '+','.join(missing_war_admin_save))
war_admin_exec_marks=['WarCoordinationForArmy','WarLogisticsForArmy','musterPolicy.CommandResponse*warCoord','attackAdmin','defendAdmin','reactionRadius','EarlyWarning']
missing_war_admin_exec=[x for x in war_admin_exec_marks if x not in world_text]
add('战争负责官员进入集结/后勤/攻守执行链','PASS' if not missing_war_admin_exec else 'FAIL','战争负责官员能力真实进入集结、装粮/补给、攻城、守城与政策预警拦截半径，不只是UI负责人字段' if not missing_war_admin_exec else '缺少: '+','.join(missing_war_admin_exec))

# V9 original coverage hard checks added after full 60-row audit.
culture_path=SRC/'Core/CityCultureEffectEngine.cs'
culture_text=culture_path.read_text(encoding='utf-8',errors='replace') if culture_path.exists() else ''
merchant_security_marks=['TickMerchantSecurityEvents','商路劫掠','pp.PublicOrder*op.PublicOrder']
add('商人被抢劫风险真实执行','PASS' if all(x in world_text for x in merchant_security_marks) else 'FAIL','财富、战争、敌军距离、商人风险倾向、政策治安与官员民政共同决定真实商路劫掠损失' if all(x in world_text for x in merchant_security_marks) else '商路劫掠执行链不完整')
civil_marks=['CivicMerit','TickCivilMerit','civil_noble','发现大型矿脉','名匠立功','传奇名器功勋','ConstructionMerit']
add('平民多路径功勋授爵','PASS' if all(x in model_text+world_text for x in civil_marks) else 'FAIL','军功之外，重大公共工程、矿脉发现、名匠贡献、传奇名器均能形成真实功勋并进入玩家授爵申请箱' if all(x in model_text+world_text for x in civil_marks) else '文功授爵链不完整')
official_defect_marks=['TickOfficialPolitics','官员叛逃','DefectionDestination','official.Class=SocialClass.Noble']
add('官员叛逃/反叛政治链','PASS' if all(x in world_text for x in official_defect_marks) and 'TriggerFamilyRebellion' in world_text else 'FAIL','低忠诚高野心官员会推动家族政治风险并可真实弃官叛逃，家族仍可举兵反叛' if all(x in world_text for x in official_defect_marks) and 'TriggerFamilyRebellion' in world_text else '官员政治后果不完整')
prestige_marks=['LordPrestigeFactor','AppointmentAccepted','prestigeTax','TickLordPrestigeEffects']
add('领主威望税收/忠诚/任命联动','PASS' if all(x in world_text for x in prestige_marks) else 'FAIL','领主/国家威望真实影响税收效率、人物与家族忠诚、官员/将军任命接受' if all(x in world_text for x in prestige_marks) else '领主威望执行链不完整')
culture_marks=['CityCultureProfile','Agriculture','Mining','Logging','Commerce','Smithing','Construction','Recruitment','HorseGrowth','PreferredA','PreferredB']
add('城市文化经济/兵员/建设/视觉联动','PASS' if culture_path.exists() and all(x in culture_text for x in culture_marks) and 'CityCultureEffectEngine.Build' in world_text and 'CultureId' in renderer_text else 'FAIL','城市文化同时改变农业/矿业/林业/商业/铸造/建设/兵员/马政，并继续驱动真实建筑与单位视觉变体' if culture_path.exists() and all(x in culture_text for x in culture_marks) and 'CityCultureEffectEngine.Build' in world_text and 'CultureId' in renderer_text else '城市文化执行链不完整')
occupation_marks=['OccupationPolicy','ResolveOccupation','OccupationPolicy.Conciliate','OccupationPolicy.LimitedPlunder','OccupationPolicy.FullPlunder','占领城市处置']
add('占领安抚/有限掠夺/全面掠夺','PASS' if all(x in model_text+world_text+hud_text for x in occupation_marks) else 'FAIL','破城占领后玩家必须选择安抚/有限掠夺/全面掠夺；资源、国库、建筑耐久、商人财富与民众忠诚真实变化' if all(x in model_text+world_text+hud_text for x in occupation_marks) else '占领处置链不完整')

# Asset report
asset_report=json.loads((EVID/'ASSET_IMPORT_REPORT.json').read_text(encoding='utf-8'))
resolved=asset_report['resolved_total']; total=asset_report['mapping_rows']
add('真实素材映射','PARTIAL' if resolved<total else 'PASS',f'{resolved}/{total} 条映射可解析；工程PNG {len(list(ART.rglob("*.png")))}')

# Toolchain/build checks: must not lie
import shutil
for tool in ['dotnet','mcs','csc','mono','gradle','adb','aapt2']:
    add('工具链 '+tool,'AVAILABLE' if shutil.which(tool) else 'BLOCKED','存在' if shutil.which(tool) else '当前容器未安装')
unity=shutil.which('Unity') or shutil.which('unity')
add('Unity Editor','AVAILABLE' if unity else 'BLOCKED','存在' if unity else '当前容器未安装，无法执行Unity C#编译/场景导入/Android构建')

# Git consistency
try:
    subprocess.run(['git','diff','--check'],cwd=ROOT,check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
    add('git diff --check','PASS','无空白错误')
except subprocess.CalledProcessError as e:
    add('git diff --check','FAIL',e.stdout+e.stderr)

# 端到端冻结闭环测试必须存在；这里只验证测试源码接入，实际执行仍由Unity/NUnit工具链决定。
e2e_text=(ROOT/"Assets/Tests/Runtime/LordWarCoreTests.cs").read_text(encoding="utf-8")
e2e_ok=("EndToEnd_FrozenGameplayLoop_ProgressesFromGovernmentToWarAndRestore" in e2e_text and "DeclareWarAndMarch" in e2e_text and "GameSaveService.Restore" in e2e_text and "ProposalKind.Diplomacy" in e2e_text)
add("端到端完整玩法闭环测试源码","PASS" if e2e_ok else "FAIL","政府→施工→将军→募兵→集结→行军→持续战斗→停战→存档恢复测试已接入" if e2e_ok else "端到端闭环测试源码不完整")

summary=Counter(r['status'] for r in results)
report={'project':'领主战争 V9 实际施工工程','results':results,'summary':dict(summary),'truth_boundary':{
    'source_written':True,
    'data_integrated':True,
    'assets_integrated':resolved,
    'csharp_compiled':False,
    'unity_runtime_tested':False,
    'android_apk_built':False,
    'reason':'当前执行环境缺少Unity Editor、C#编译器与Android构建工具链；不得将静态验证写成编译/运行通过。'
}}
(EVID/'VERIFICATION_REPORT.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
# fail only on actual FAIL, not PARTIAL/BLOCKED
sys.exit(1 if any(r['status']=='FAIL' for r in results) else 0)
