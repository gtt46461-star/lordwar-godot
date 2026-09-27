#if UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using LordWar.Simulation;
using LordWar.Data;
using LordWar.Siege;
using LordWar.AI;
using LordWar.World;

namespace LordWar.UnityRuntime {
    public sealed class ChineseHud : MonoBehaviour {
        Vector2 scroll, armyScroll, specialScroll, warScroll, societyScroll;
        bool showApplications, showArmy, showWar, showPlanning, showSociety, showNewWorldSetup;
        string status="世界已创建", selectedPersonId="", selectedEquipmentId="", newSeedText="";
        int newWorldSize=160, newWorldKingdoms=2; AiDifficulty newDifficulty=AiDifficulty.Hard;
        readonly WorldGenerationOptions newMapOptions=new WorldGenerationOptions();
        readonly HashSet<string> selectedArmyIds=new HashSet<string>();

        void OnGUI(){
            GameWorld w=LordWarBootstrap.World;if(w==null)return;
            CleanupArmySelection(w);
            DrawTop(w);
            if(showApplications)DrawApplications(w);
            if(showWar)DrawWarPanel(w);
            if(showPlanning)DrawPlanning(w);
            if(showSociety)DrawSociety(w);
            if(showArmy)DrawArmyPanel(w);
            if(showNewWorldSetup)DrawNewWorldSetup(w);
            if(!string.IsNullOrEmpty(selectedPersonId))DrawPerson(w,selectedPersonId);
            if(!string.IsNullOrEmpty(selectedEquipmentId))DrawEquipment(w,selectedEquipmentId);
            DrawBattle(w);
            DrawConstruction(w);
        }

        void DrawTop(GameWorld w){
            Kingdom player=null;City capital=null;if(!string.IsNullOrEmpty(w.PlayerKingdomId))w.Kingdoms.TryGetValue(w.PlayerKingdomId,out player);if(player!=null)w.Cities.TryGetValue(player.CapitalCityId,out capital);
            GUI.Box(new Rect(8,8,330,338),"领主战争");
            GUI.Label(new Rect(18,36,305,24),"日期：第 "+w.Day+" 日　季节："+ChineseText.Season(w.Weather.Season)+"　天气："+ChineseText.Weather(w.Weather.Weather)+"　时段："+(w.IsNightTime?"夜间":"白昼"));
            int activeKingdoms=0,vassals=0;foreach(Kingdom k in w.Kingdoms.Values){if(k.Status!=KingdomStatus.Eliminated)activeKingdoms++;if(k.Status==KingdomStatus.Vassal)vassals++;}
            GUI.Label(new Rect(18,58,305,24),"国家："+activeKingdoms+"（附庸 "+vassals+"）　城市："+w.Cities.Count+"　人口："+w.People.Count+"　电脑："+ChineseText.Difficulty(w.ComputerDifficulty));
            if(player!=null)GUI.Label(new Rect(18,80,305,24),"国库："+player.Treasury+" 金币　军队："+player.ArmyIds.Count+"　国势："+ChineseText.KingdomStatusText(player.Status));
            if(capital!=null)GUI.Label(new Rect(18,102,305,24),"都城："+capital.Name+"（"+capital.CultureId+"）　粮 "+capital.Food+"　木 "+capital.Wood+"　石 "+capital.Stone+"　铁 "+capital.Iron+"　马 "+capital.Horses);
            if(GUI.Button(new Rect(18,132,145,32),"推进一日")){w.AdvanceDay();status="时间推进";}
            if(GUI.Button(new Rect(173,132,145,32),"战争 / 出征")){showWar=!showWar;showApplications=false;showPlanning=false;showSociety=false;}
            if(GUI.Button(new Rect(18,172,95,30),"新建世界")){showNewWorldSetup=!showNewWorldSetup;showApplications=false;showWar=false;showPlanning=false;showSociety=false;}
            if(GUI.Button(new Rect(118,172,95,30),"保存游戏"))status=UnitySaveService.Save(w)?"存档成功":"存档失败";
            if(GUI.Button(new Rect(218,172,100,30),"读取存档")){bool ok=UnitySaveService.Load(w);selectedArmyIds.Clear();status=ok?"读档成功":"没有可用存档";if(ok){WorldRenderer vr=FindObjectOfType<WorldRenderer>();if(vr!=null)vr.Refresh();}}
            int pending=w.PlayerPendingProposalCount;
            if(GUI.Button(new Rect(18,210,145,32),"申请 / 奏报（"+pending+"）")){showApplications=!showApplications;showWar=false;showPlanning=false;showSociety=false;}
            if(GUI.Button(new Rect(173,210,145,32),"军队 / 人物"))showArmy=!showArmy;
            if(GUI.Button(new Rect(18,246,145,24),"城市区域规划")){showPlanning=!showPlanning;showWar=false;showApplications=false;showSociety=false;}
            if(GUI.Button(new Rect(173,246,145,24),"政务 / 社会")){showSociety=!showSociety;showWar=false;showApplications=false;showPlanning=false;}
            if(GUI.Button(new Rect(18,274,72,24),w.Paused?"继续":"暂停"))w.Paused=!w.Paused;
            if(GUI.Button(new Rect(96,274,66,24),"×1"))w.TimeScale=1f;if(GUI.Button(new Rect(168,274,66,24),"×2"))w.TimeScale=2f;if(GUI.Button(new Rect(240,274,66,24),"×4"))w.TimeScale=4f;
            GUI.Label(new Rect(18,302,300,20),"本日 "+Mathf.RoundToInt(w.DayProgress01*100f)+"%　速度 ×"+w.TimeScale.ToString("0.#"));
            GUI.Label(new Rect(18,322,300,20),status);
        }

        void DrawNewWorldSetup(GameWorld w){
            Rect area=new Rect(Math.Max(350,(Screen.width-500)/2),Math.Max(8,(Screen.height-600)/2),500,Math.Min(600,Screen.height-16));GUI.Box(area,"创建新世界");GUILayout.BeginArea(new Rect(area.x+18,area.y+34,area.width-36,area.height-48));
            GUILayout.Label("世界种子（留空随机；输入0是固定种子）");newSeedText=GUILayout.TextField(newSeedText,24);
            GUILayout.Space(8);GUILayout.Label("地图规模："+newWorldSize+"×"+newWorldSize);GUILayout.BeginHorizontal();if(GUILayout.Button("160"))newWorldSize=160;if(GUILayout.Button("224"))newWorldSize=224;if(GUILayout.Button("320"))newWorldSize=320;GUILayout.EndHorizontal();
            newMapOptions.LandPercent=MapSlider("陆地比例",newMapOptions.LandPercent,15,85);
            newMapOptions.ForestPercent=MapSlider("森林",newMapOptions.ForestPercent,0,100);
            newMapOptions.MountainPercent=MapSlider("山地",newMapOptions.MountainPercent,0,100);
            newMapOptions.DesertPercent=MapSlider("沙漠",newMapOptions.DesertPercent,0,100);
            newMapOptions.RiverPercent=MapSlider("河流",newMapOptions.RiverPercent,0,100);
            newMapOptions.ResourcePercent=MapSlider("资源",newMapOptions.ResourcePercent,0,100);
            GUILayout.Space(8);GUILayout.Label("国家数量："+newWorldKingdoms);GUILayout.BeginHorizontal();for(int count=2;count<=6;count++)if(GUILayout.Button(count+"国"))newWorldKingdoms=count;GUILayout.EndHorizontal();
            GUILayout.Space(8);GUILayout.Label("电脑难度："+ChineseText.Difficulty(newDifficulty)+"（只改变决策质量，不额外加金币、人口、粮食或战斗属性）");GUILayout.BeginHorizontal();if(GUILayout.Button("简单"))newDifficulty=AiDifficulty.Easy;if(GUILayout.Button("中等"))newDifficulty=AiDifficulty.Normal;if(GUILayout.Button("困难"))newDifficulty=AiDifficulty.Hard;if(GUILayout.Button("噩梦"))newDifficulty=AiDifficulty.Nightmare;GUILayout.EndHorizontal();
            GUILayout.Space(12);GUILayout.BeginHorizontal();if(GUILayout.Button("取消")){showNewWorldSetup=false;}if(GUILayout.Button("创建世界")){int seedValue;if(!string.IsNullOrEmpty(newSeedText)&&!int.TryParse(newSeedText,out seedValue)){status="种子必须是32位整数";}else{seedValue=string.IsNullOrEmpty(newSeedText)?Guid.NewGuid().GetHashCode():int.Parse(newSeedText);try{LordWarBootstrap.CreateConfiguredWorld(seedValue,newWorldSize,newWorldSize,newWorldKingdoms,newDifficulty,newMapOptions);selectedArmyIds.Clear();selectedPersonId="";selectedEquipmentId="";showNewWorldSetup=false;status="新世界已创建：Seed "+seedValue+"，"+newWorldSize+"×"+newWorldSize+"，陆地"+newMapOptions.LandPercent+"%";}catch(Exception e){status="创建失败："+e.Message;}}}GUILayout.EndHorizontal();
            GUILayout.Space(8);GUILayout.Label("当前世界：Seed "+w.Seed+"　电脑"+ChineseText.Difficulty(w.ComputerDifficulty));GUILayout.EndArea();
        }

        static int MapSlider(string label,int value,int min,int max){GUILayout.Label(label+"："+value+"%");return Mathf.RoundToInt(GUILayout.HorizontalSlider(value,min,max));}

        void DrawApplications(GameWorld w){
            Rect area=new Rect(348,8,440,Screen.height-16);GUI.Box(area,"固定申请箱");
            GUILayout.BeginArea(new Rect(360,40,414,Screen.height-62));scroll=GUILayout.BeginScrollView(scroll);
            foreach(Proposal p in w.Proposals.Queue){if(p.State!=ProposalState.Pending||!w.IsPlayerProposal(p))continue;GUILayout.BeginVertical(GUI.skin.box);GUILayout.Label(p.Title);GUILayout.Label(p.Description);GUILayout.Label("类型："+ChineseText.Proposal(p.Kind)+"　门槛："+p.RequiredAbility+"　费用："+p.CostGold+"　工期："+p.DurationDays+"日");GUILayout.BeginHorizontal();if(GUILayout.Button("同意"))status=w.ApproveProposal(p.Id)?"已批准并进入真实执行链":"批准后执行失败/条件不足";if(GUILayout.Button("拒绝")){w.RejectProposal(p.Id);status="已拒绝";}GUILayout.EndHorizontal();GUILayout.EndVertical();GUILayout.Space(8);}
            GUILayout.EndScrollView();GUILayout.EndArea();
        }

        void DrawWarPanel(GameWorld w){
            Rect area=new Rect(348,8,500,Math.Min(Screen.height-16,620));GUI.Box(area,"战争与出征");
            GUILayout.BeginArea(new Rect(360,40,476,area.height-52));warScroll=GUILayout.BeginScrollView(warScroll);
            GUILayout.Label("第一步：选择参战将军 / 军队");
            foreach(Army a in w.Armies.Values){
                if(a.KingdomId!=w.PlayerKingdomId)continue;Person g=null;w.People.TryGetValue(a.GeneralId,out g);bool selected=selectedArmyIds.Contains(a.Id);
                GUILayout.BeginHorizontal(GUI.skin.box);GUILayout.Label((g==null?a.Name:g.Name+"｜"+a.Name)+"　兵力 "+w.Military.SoldierCount(a)+"　粮 "+a.FoodDays.ToString("0.0")+"日　士气 "+Mathf.RoundToInt(a.Morale));
                if(GUILayout.Button(selected?"取消参战":"选择参战",GUILayout.Width(92))){if(selected)selectedArmyIds.Remove(a.Id);else selectedArmyIds.Add(a.Id);}GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);GUILayout.Label("第二步：选择敌国并宣战");
            Kingdom player=null;w.Kingdoms.TryGetValue(w.PlayerKingdomId,out player);
            foreach(Kingdom k in w.Kingdoms.Values){
                if(player==null||k.Id==player.Id||k.Status==KingdomStatus.Eliminated)continue;DiplomacyRelation rel=w.Diplomacy.Get(player.Id,k.Id);City city=null;w.Cities.TryGetValue(k.CapitalCityId,out city);
                GUILayout.BeginVertical(GUI.skin.box);string polity="　国势："+ChineseText.KingdomStatusText(k.Status);if(k.Status==KingdomStatus.Vassal&&!string.IsNullOrEmpty(k.OverlordKingdomId)){Kingdom overlord;if(w.Kingdoms.TryGetValue(k.OverlordKingdomId,out overlord))polity+="（宗主 "+overlord.Name+"）";}GUILayout.Label(k.Name+polity+"　关系："+ChineseText.Diplomacy(rel.State)+"　态度 "+rel.Opinion+(rel.TruceUntilDay>w.Day?"　停战至第"+rel.TruceUntilDay+"日":""));
                if(city!=null)GUILayout.Label("都城："+city.Name+"　人口 "+city.PersonIds.Count+"　城防外郭 "+city.FortificationRadius);
                if(rel.State==DiplomacyState.War){
                    WarAdministration wa=w.WarAdministrationFor(player.Id,k.Id);
                    if(wa==null){
                        GUILayout.Label("战争负责官员：未指派。当前只有领主手动军令会继续执行，守城、野战、攻城和敌军拦截不会自动重调兵力。");
                        GUILayout.Label("可指派在任官员：");int shownOfficials=0;foreach(Person o in w.People.Values){if(o.KingdomId!=player.Id||!o.Alive||o.Injury==InjuryState.Captured||o.Class!=SocialClass.Official||o.Job!=JobKind.Official)continue;if(shownOfficials++>=6)break;if(GUILayout.Button("指派 "+o.Name+" 负责此战｜行政 "+o.Stats.Administration+" 组织 "+o.Stats.Organization+" 后勤 "+o.Stats.Logistics+" 军政 "+o.Stats.Military)){string reason;status=w.AssignWarOfficial(k.Id,o.Id,out reason)?"已指派 "+o.Name+" 总理对 "+k.Name+" 战事":reason;}}
                    }else{
                        Person officer=null;w.People.TryGetValue(wa.OfficialId,out officer);var wp=w.WarAdministration.Profile(wa);GUILayout.Label("战争负责官员："+(officer==null?"任命记录失效":officer.Name)+"　重点："+ChineseText.WarFocus(wa.Focus));GUILayout.Label("战务能力：协调 "+Mathf.RoundToInt(wp.Coordination*100f)+"　守城 "+Mathf.RoundToInt(wp.Defense*100f)+"　野战 "+Mathf.RoundToInt(wp.FieldBattle*100f)+"　攻城 "+Mathf.RoundToInt(wp.Siege*100f)+"　拦截 "+Mathf.RoundToInt(wp.Interception*100f)+"　后勤 "+Mathf.RoundToInt(wp.Logistics*100f));
                        GUILayout.Label("战务记录：军令 "+wa.OrdersIssued+"｜守城 "+wa.DefenseOrders+"｜野战 "+wa.FieldOrders+"｜攻城 "+wa.SiegeOrders+"｜拦截 "+wa.InterceptOrders+"\n最近："+wa.LastAction);
                        GUILayout.BeginHorizontal();if(GUILayout.Button("综合")){string reason;status=w.SetWarOfficialFocus(k.Id,WarAdministrationFocus.Balanced,out reason)?"战务重点改为综合战务":reason;}if(GUILayout.Button("守城")){string reason;status=w.SetWarOfficialFocus(k.Id,WarAdministrationFocus.Defense,out reason)?"战务重点改为守城":reason;}if(GUILayout.Button("野战")){string reason;status=w.SetWarOfficialFocus(k.Id,WarAdministrationFocus.FieldBattle,out reason)?"战务重点改为野战":reason;}GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();if(GUILayout.Button("攻城")){string reason;status=w.SetWarOfficialFocus(k.Id,WarAdministrationFocus.Siege,out reason)?"战务重点改为攻城":reason;}if(GUILayout.Button("拦截")){string reason;status=w.SetWarOfficialFocus(k.Id,WarAdministrationFocus.Interception,out reason)?"战务重点改为拦截":reason;}if(GUILayout.Button("撤销负责官员")){string reason;status=w.RevokeWarOfficial(k.Id,out reason)?"已撤销该战争的负责官员；既有军令继续，新的自动调度停止":reason;}GUILayout.EndHorizontal();
                    }
                }
                bool blocked=k.Status==KingdomStatus.Vassal||player.Status!=KingdomStatus.Active||rel.State==DiplomacyState.Vassal||rel.State==DiplomacyState.Alliance||rel.State==DiplomacyState.NonAggression||rel.TruceUntilDay>w.Day;bool old=GUI.enabled;GUI.enabled=!blocked&&selectedArmyIds.Count>0;
                if(GUILayout.Button(rel.State==DiplomacyState.War?"领主手动命令所选军队继续进军":"宣战并命令所选军队出征")){int moved=w.DeclareWarAndMarch(k.Id,selectedArmyIds);status=moved>0?"已命令 "+moved+" 支军队向 "+k.Name+" 出征":"没有符合出征条件的所选军队";}
                GUI.enabled=old;GUILayout.EndVertical();
            }
            if(selectedArmyIds.Count==0)GUILayout.Label("请先选择至少一支有兵力、有粮草的军队。\n");
            GUILayout.EndScrollView();GUILayout.EndArea();
        }

        void DrawPlanning(GameWorld w){
            Kingdom k;City c;if(string.IsNullOrEmpty(w.PlayerKingdomId)||!w.Kingdoms.TryGetValue(w.PlayerKingdomId,out k)||!w.Cities.TryGetValue(k.CapitalCityId,out c))return;
            Rect area=new Rect(348,8,500,Math.Min(Screen.height-16,620));GUI.Box(area,"城市区域规划");GUILayout.BeginArea(new Rect(360,40,476,area.height-52));GUILayout.Label("领主划分城市功能区域；居民住宅和商铺、官员工程会优先在对应区域真实选址。\n都城："+c.Name);
            GUILayout.BeginHorizontal();if(GUILayout.Button("规划住宅区"))Plan(w,ZoneKind.Residential);if(GUILayout.Button("规划商业区"))Plan(w,ZoneKind.Commercial);if(GUILayout.Button("规划工坊区"))Plan(w,ZoneKind.Workshop);GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button("规划农牧区"))Plan(w,ZoneKind.Agriculture);if(GUILayout.Button("规划军政区"))Plan(w,ZoneKind.Military);if(GUILayout.Button("规划公共区"))Plan(w,ZoneKind.Civic);GUILayout.EndHorizontal();
            GUILayout.Space(8);GUILayout.Label("现有规划区：");foreach(CityZone z in c.Zones){if(z==null)continue;GUILayout.Label("· "+z.Name+"　中心("+z.CenterX+","+z.CenterY+")　半径 "+z.Radius+"　建筑 "+z.BuildingCount+(z.PlayerPlanned?"　领主规划":"　初始规划"));}GUILayout.EndArea();
        }
        void Plan(GameWorld w,ZoneKind kind){CityZone z=w.DesignatePlayerZone(kind);status=z==null?"该方向没有可用地块或与现有规划区冲突":"已划定"+z.Name+"，后续居民与官员将按区建设";WorldRenderer vr=FindObjectOfType<WorldRenderer>();if(z!=null&&vr!=null)vr.Refresh();}

        void DrawSociety(GameWorld w){
            Kingdom k;if(string.IsNullOrEmpty(w.PlayerKingdomId)||!w.Kingdoms.TryGetValue(w.PlayerKingdomId,out k))return;Rect area=new Rect(348,8,520,Math.Min(Screen.height-16,700));GUI.Box(area,"政务 / 家族 / 商人");GUILayout.BeginArea(new Rect(360,40,496,area.height-52));societyScroll=GUILayout.BeginScrollView(societyScroll);
            GUILayout.Label("【生效政策 / 国策】");if(k.ActivePolicyIds.Count==0)GUILayout.Label("暂无生效政策");foreach(string id in k.ActivePolicyIds){PolicyDef d;if(w.Data.Policies.TryGetValue(id,out d))GUILayout.Label("· "+d.DisplayName+"｜"+d.Category+"｜月维护 "+d.Monthly.ToString("0.#"));}if(k.SuspendedPolicyIds.Count>0){GUILayout.Label("【因财政暂停】");foreach(string id in k.SuspendedPolicyIds){PolicyDef d;if(w.Data.Policies.TryGetValue(id,out d))GUILayout.Label("· "+d.DisplayName);}}
            GUILayout.Space(6);GUILayout.Label("【官员】");int officialCount=0;foreach(Person p in w.People.Values){if(p.KingdomId!=k.Id||!p.Alive||p.Class!=SocialClass.Official)continue;officialCount++;if(GUILayout.Button(p.Name+"｜行政 "+p.Stats.Administration+"｜组织 "+p.Stats.Organization+"｜后勤 "+p.Stats.Logistics+"｜工程 "+p.Stats.Engineering))selectedPersonId=p.Id;}if(officialCount==0)GUILayout.Label("暂无正式官员，任命必须经过申请箱批准");
            GUILayout.Space(6);GUILayout.Label("【家族】");foreach(Family f in w.Families.Values){City home;if(string.IsNullOrEmpty(f.HomeCityId)||!w.Cities.TryGetValue(f.HomeCityId,out home)||home.KingdomId!=k.Id)continue;GUILayout.BeginVertical(GUI.skin.box);GUILayout.Label(f.Name+"｜"+f.FamilyType+"｜"+f.MilitaryTradition);GUILayout.Label("财富 "+f.Wealth+"　声望 "+f.Prestige+"　忠诚 "+f.Loyalty+"　野心 "+f.Ambition+"　影响力 "+f.Influence);int members=0;foreach(string pid in f.MemberIds){if(members++>=3)break;Person member;if(w.People.TryGetValue(pid,out member)&&GUILayout.Button("成员："+member.Name+"｜"+ChineseText.Social(member.Class)))selectedPersonId=member.Id;}GUILayout.EndVertical();}
            GUILayout.Space(6);GUILayout.Label("【商人及军资投资】");int merchants=0;foreach(Person m in w.People.Values){if(m.KingdomId!=k.Id||!m.Alive||!m.Merchant)continue;merchants++;bool invested=w.Merchants.HasActiveInvestment(m.Id);if(GUILayout.Button(m.Name+"｜财富 "+m.Wealth+(invested?"｜已投资军队":"｜未投资")))selectedPersonId=m.Id;}if(merchants==0)GUILayout.Label("暂无达到财富门槛的商人");foreach(MerchantInvestment inv in w.Merchants.Investments){if(inv==null||!inv.Active)continue;Person m,g;if(!w.People.TryGetValue(inv.MerchantId,out m)||m.KingdomId!=k.Id)continue;w.People.TryGetValue(inv.GeneralId,out g);GUILayout.Label("投资："+m.Name+" → "+(g==null?"未知将军":g.Name)+"　月供 "+inv.MonthlySupport+"　减税 "+Mathf.RoundToInt(inv.TaxReductionPct*100f)+"%");}
            GUILayout.Space(6);GUILayout.Label("【占领城市处置】");int occupations=0;foreach(City occupied in w.Cities.Values){if(occupied.KingdomId!=k.Id||occupied.Occupation!=OccupationPolicy.Pending)continue;occupations++;GUILayout.BeginVertical(GUI.skin.box);Kingdom former=null;if(!string.IsNullOrEmpty(occupied.PreviousKingdomId))w.Kingdoms.TryGetValue(occupied.PreviousKingdomId,out former);GUILayout.Label(occupied.Name+"｜第"+occupied.LastCapturedDay+"日攻占｜原属 "+(former==null?"未知国家":former.Name));GUILayout.Label("必须选择占领政策：安抚保护民产、有限军需征发，或全面掠夺；不同选择真实改变国库、资源、建筑与民众忠诚。");GUILayout.BeginHorizontal();if(GUILayout.Button("安抚"))status=w.ResolveOccupation(occupied.Id,OccupationPolicy.Conciliate)?"已在"+occupied.Name+"实施安抚":"处置失败";if(GUILayout.Button("有限掠夺"))status=w.ResolveOccupation(occupied.Id,OccupationPolicy.LimitedPlunder)?"已在"+occupied.Name+"有限征发":"处置失败";if(GUILayout.Button("全面掠夺"))status=w.ResolveOccupation(occupied.Id,OccupationPolicy.FullPlunder)?"已在"+occupied.Name+"全面掠夺":"处置失败";GUILayout.EndHorizontal();GUILayout.EndVertical();}if(occupations==0)GUILayout.Label("暂无待处置的占领城市");
            GUILayout.Space(8);GUILayout.Label("【世界大事记】");if(w.Events.Count==0)GUILayout.Label("暂无历史事件");else {int start=Math.Max(0,w.Events.Count-16);for(int i=w.Events.Count-1;i>=start;i--){WorldEvent e=w.Events[i];if(e==null)continue;GUILayout.Label("第"+e.Day+"日｜"+e.Category+"｜"+e.Title+"
"+e.Detail);}}
            GUILayout.EndScrollView();GUILayout.EndArea();
        }

        void DrawArmyPanel(GameWorld w){
            Rect area=new Rect(8,356,540,Math.Max(120,Screen.height-364));GUI.Box(area,"军队与人物");
            GUILayout.BeginArea(new Rect(18,360,520,Screen.height-372));armyScroll=GUILayout.BeginScrollView(armyScroll);
            foreach(Army a in w.Armies.Values){
                if(a.KingdomId!=w.PlayerKingdomId)continue;GUILayout.BeginVertical(GUI.skin.box);Person g=null;w.People.TryGetValue(a.GeneralId,out g);
                GUILayout.Label(a.Name+"　主将："+(g==null?"无":g.Name)+"　现役："+w.Military.SoldierCount(a)+"　可战："+w.Military.ReadySoldierCount(a)+"　军资："+a.Finance.Gold);
                GUILayout.Label("军令："+ChineseText.ArmyOrderText(a.Order)+"　士气："+Mathf.RoundToInt(a.Morale)+"　疲劳："+Mathf.RoundToInt(a.Fatigue)+"　粮草："+a.FoodDays.ToString("0.0")+"日");if(a.Order==ArmyOrder.Muster)GUILayout.Label("集结进度："+Mathf.RoundToInt(a.MusterProgress*100f)+"%　完成后自动进入战略行军");
                GUILayout.Label("军费：军饷 "+a.Finance.MonthlyWages+"　欠领主 "+a.Finance.DebtToLord+"　欠商人 "+a.Finance.DebtToMerchants+"　补给费 "+a.Finance.SupplyCost+"　累计逃兵 "+a.Deserters);if(a.Order==ArmyOrder.Camp)GUILayout.Label("营地：第 "+Math.Max(1,w.Day-a.CampStartDay+1)+" 日｜疲劳 "+Mathf.RoundToInt(a.Fatigue)+"｜粮期 "+a.FoodDays.ToString("0.0")+" 日");
                GUILayout.Label("战绩："+a.Wins+"胜 "+a.Losses+"负 / "+a.BattleCount+"战　成立：第"+a.FormedDay+"日");
                SiegeState siege=w.SiegeForArmy(a.Id);if(siege!=null)GUILayout.Label("围城：第"+siege.Days+"日　城门 "+Mathf.RoundToInt(siege.GateIntegrity)+"%　城墙 "+Mathf.RoundToInt(siege.WallIntegrity)+"%　城内粮期 "+siege.CivilianFoodDays.ToString("0.0")+"日　守军士气 "+Mathf.RoundToInt(siege.GarrisonMorale));
                if(g!=null&&GUILayout.Button("查看主将 "+g.Name))selectedPersonId=g.Id;
                foreach(Squad sq in a.Squads){GUILayout.Label("部伍："+sq.Name+"　士兵："+sq.SoldierIds.Count+"　士气："+Mathf.RoundToInt(sq.Morale)+"（"+ChineseText.Morale(sq.LocalMoraleState)+"）　老兵 "+Mathf.RoundToInt(w.Military.VeteranRatio(sq)*100f)+"%　队形："+sq.FormationName);int shown=0;foreach(string id in sq.SoldierIds){if(shown++>=4)break;Person p;if(w.People.TryGetValue(id,out p)&&GUILayout.Button("士兵："+p.Name))selectedPersonId=p.Id;}}
                GUILayout.Label("特殊兵种招募：");specialScroll=GUILayout.BeginScrollView(specialScroll,GUILayout.Height(110));int displayed=0;foreach(SpecialUnitDef u in w.Data.SpecialUnits.Values){if(displayed++>=12)break;if(GUILayout.Button(u.Name+"（"+u.Role+"）")){string reason;status=w.TryRecruitSpecial(a.Id,u.Id,out reason)?"已按真实条件招募 "+u.Name:reason;}}GUILayout.EndScrollView();GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();GUILayout.EndArea();
        }

        void DrawPerson(GameWorld w,string id){
            Person p;if(!w.People.TryGetValue(id,out p)){selectedPersonId="";return;}Rect r=new Rect(Screen.width-360,130,352,Math.Min(460,Screen.height-138));GUI.Box(r,"人物详情");GUILayout.BeginArea(new Rect(r.x+10,r.y+28,r.width-20,r.height-38));
            GUILayout.Label(p.Name+"　"+ChineseText.Social(p.Class)+"　"+p.Age+"岁　职业："+ChineseText.Job(p.Job));GUILayout.Label("状态："+ChineseText.Injury(p.Injury)+(p.Injury==InjuryState.Captured?"　被俘第"+p.CapturedDay+"日　赎金 "+p.RansomValue+" 金币":""));GUILayout.Label("武力 "+p.Stats.Martial+"　防御 "+p.Stats.Defense+"　智力 "+p.Stats.Intelligence+"　体力 "+p.Stats.Stamina);GUILayout.Label("行政 "+p.Stats.Administration+"　组织 "+p.Stats.Organization+"　后勤 "+p.Stats.Logistics+"　工程 "+p.Stats.Engineering);GUILayout.Label("忠诚 "+p.Stats.Loyalty+"　野心 "+p.Stats.Ambition+"　威望 "+p.Stats.Prestige+"　经验 "+p.Experience);GUILayout.Label("国家功勋 "+p.CivicMerit+(string.IsNullOrEmpty(p.LastMeritReason)?"":"｜"+p.LastMeritReason));
            GUILayout.Label("人物特性：");foreach(string tid in p.TraitIds)GUILayout.Label("· "+w.Data.TraitName(tid));GUILayout.Label("统帅技能：");foreach(string sid in p.CommanderSkillIds){SkillDef d;if(w.Data.Skills.TryGetValue(sid,out d))GUILayout.Label("· "+d.Name+"："+d.Behavior);}
            GUILayout.Label("装备（点击查看）：");string[] slots={"主手","副手","头盔","身体护甲","鞋靴","特殊物品"};foreach(string slot in slots){EquipmentInstance e=EquippedInSlot(w,p,slot);if(e==null)GUILayout.Label("· "+slot+"｜空槽");else {string itemName=EquipmentName(w,e);if(GUILayout.Button(slot+"｜"+e.QualityName+" "+itemName+"｜耐久 "+e.Durability+"/"+e.MaxDurability))selectedEquipmentId=e.InstanceId;}}if(GUILayout.Button("关闭人物详情"))selectedPersonId="";GUILayout.EndArea();
        }

        EquipmentInstance EquippedInSlot(GameWorld w,Person p,string slot){foreach(string eid in p.EquipmentIds){EquipmentInstance e;if(w.Equipment.TryGetValue(eid,out e)&&ChineseText.EquipmentSlot(e.Slot)==slot)return e;}return null;}
        string EquipmentName(GameWorld w,EquipmentInstance e){if(e==null)return "未知装备";if(!string.IsNullOrEmpty(e.DisplayName))return e.DisplayName;EquipmentTypeDef type;if(w.Data.EquipmentTypes.TryGetValue(e.TemplateId,out type))return type.Name;return "未知装备";}
        void DrawEquipment(GameWorld w,string id){EquipmentInstance e;if(!w.Equipment.TryGetValue(id,out e)){selectedEquipmentId="";return;}Rect r=new Rect(Math.Max(8,Screen.width-720),130,344,Math.Min(390,Screen.height-138));GUI.Box(r,"装备详情");GUILayout.BeginArea(new Rect(r.x+10,r.y+28,r.width-20,r.height-38));GUILayout.Label(e.QualityName+"　"+EquipmentName(w,e)+(e.Unique?"　【世界唯一】":""));GUILayout.Label("槽位："+ChineseText.EquipmentSlot(e.Slot)+"　材料："+(string.IsNullOrEmpty(e.Material)?"不详":e.Material));GUILayout.Label("攻击 "+e.Attack+"　防御 "+e.Defense+"　重量 "+(e.WeightKg>0?e.WeightKg.ToString("0.0")+"kg":e.Weight.ToString()));GUILayout.Label("耐久："+e.Durability+" / "+e.MaxDurability+"　价值："+e.BaseValue);Person maker;if(!string.IsNullOrEmpty(e.MakerId)&&w.People.TryGetValue(e.MakerId,out maker))GUILayout.Label("制造者："+maker.Name);City origin;if(!string.IsNullOrEmpty(e.OriginCityId)&&w.Cities.TryGetValue(e.OriginCityId,out origin))GUILayout.Label("来源城市："+origin.Name);GUILayout.Label("来源："+(string.IsNullOrEmpty(e.Source)?"记录不详":e.Source));if(e.History!=null&&e.History.Count>0){GUILayout.Label("历史：");int start=Math.Max(0,e.History.Count-6);for(int i=start;i<e.History.Count;i++)GUILayout.Label("· "+e.History[i]);}if(GUILayout.Button("关闭装备详情"))selectedEquipmentId="";GUILayout.EndArea();}

        void DrawConstruction(GameWorld w){int count=0;foreach(ConstructionProject cp in w.Projects.Values)if(cp!=null&&cp.Stage!=ConstructionStage.Complete)count++;if(count==0)return;Rect r=new Rect(Screen.width-390,176,382,Math.Min(180,56+count*42));GUI.Box(r,"施工现场");GUILayout.BeginArea(new Rect(r.x+10,r.y+28,r.width-20,r.height-36));int shown=0;foreach(ConstructionProject cp in w.Projects.Values){if(cp==null||cp.Stage==ConstructionStage.Complete)continue;if(shown++>=3){GUILayout.Label("另有 "+(count-3)+" 项工程施工中");break;}City c=null;w.Cities.TryGetValue(cp.CityId,out c);int pct=Mathf.RoundToInt(cp.WorkDone*100f/Mathf.Max(1,cp.RequiredWork));string workers="";if(cp.WorkerActions!=null)foreach(ConstructionWorkerAction x in cp.WorkerActions){if(x==null)continue;Person p;if(w.People.TryGetValue(x.WorkerId,out p))workers+=(workers.Length==0?"":"、")+p.Name+"·"+x.Action;}GUILayout.Label((c==null?"城市":c.Name)+"｜"+cp.Name+"　"+pct+"%\n"+cp.CurrentAction+(workers.Length>0?"　"+workers:""));}GUILayout.EndArea();}

        void DrawBattle(GameWorld w){BattleReport r=null;BattleSession active=null;foreach(BattleSession session in w.ActiveBattles.Values){if(session==null||session.Report==null)continue;Army aa,bb;if(w.Armies.TryGetValue(session.AttackerArmyId,out aa)&&aa.KingdomId==w.PlayerKingdomId){active=session;break;}if(w.Armies.TryGetValue(session.DefenderArmyId,out bb)&&bb.KingdomId==w.PlayerKingdomId){active=session;break;}}if(active==null&&w.ActiveBattles.Count>0)foreach(BattleSession session in w.ActiveBattles.Values){active=session;break;}if(active!=null)r=active.Report;else if(w.Battles.Count>0)r=w.Battles[w.Battles.Count-1];if(r==null)return;string title=active==null?"最新战报":"交战中";float h=active==null?160:188;GUI.Box(new Rect(Screen.width-390,8,382,h),title);string eventDetail=string.IsNullOrEmpty(r.LastAttackAction)?"":("\n动作："+r.LastAttackAction+" / "+(string.IsNullOrEmpty(r.LastDefenseAction)?"无防御":r.LastDefenseAction)+(r.LastDamage>0?" / 伤害 "+r.LastDamage:""));string live=active==null?"":("\n战斗推进："+active.ElapsedSeconds+" / "+active.MaxSeconds+" 秒"+(string.IsNullOrEmpty(r.LastActionText)?"":"\n当前交锋："+r.LastActionText)+eventDetail);GUI.Label(new Rect(Screen.width-378,34,358,h-42),r.LocationName+"\n"+r.ResultText+live+"\n阵亡："+r.AttackerDead+" / "+r.DefenderDead+"　伤员："+r.AttackerWounded+" / "+r.DefenderWounded+"　被俘："+r.AttackerCaptured+" / "+r.DefenderCaptured+"\n攻击：轻击 "+r.LightAttacks+"　重击 "+r.HeavyAttacks+"　刺击 "+r.ThrustAttacks+"　远射 "+r.RangedAttacks+"　骑冲 "+r.CavalryCharges+"\n防御：闪避 "+r.Dodges+"　格挡 "+r.Parries+"　盾挡 "+r.ShieldBlocks);}
        void CleanupArmySelection(GameWorld w){var remove=new List<string>();foreach(string id in selectedArmyIds){Army a;if(!w.Armies.TryGetValue(id,out a)||a.KingdomId!=w.PlayerKingdomId)remove.Add(id);}foreach(string id in remove)selectedArmyIds.Remove(id);}
    }
}
#endif
