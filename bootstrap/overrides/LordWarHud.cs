using System;
using System.Text;
using System.Collections.Generic;
using Godot;
using LordWar.AI;
using LordWar.Data;
using LordWar.Simulation;

namespace LordWar.GodotRuntime {
    /// <summary>
    /// Mobile-first playable strategy HUD. Exposes the actual simulation systems instead of a diagnostics-only panel.
    /// </summary>
    public sealed partial class LordWarHud : CanvasLayer {
        LordWarApp _app;
        Label _summary;
        RichTextLabel _details;
        Label _status;
        VBoxContainer _actionBox;
        PanelContainer _rootPanel;
        Control _tabs1, _tabs2;
        bool _expanded;
        double _refreshClock;
        int _page;
        int _cityIndex;
        int _armyIndex;
        int _enemyIndex;

        public void Bind(LordWarApp app) {
            _app = app;
            BuildUi();
            Refresh();
            GD.Print("LORDWAR_HUD_READY");
        }

        public override void _Process(double delta) {
            _refreshClock += delta;
            if (_refreshClock >= .35) { _refreshClock = 0; Refresh(); }
        }

        void BuildUi() {
            var root = new PanelContainer {
                AnchorLeft = .01f, AnchorTop = .79f, AnchorRight = .99f, AnchorBottom = .995f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0
            };
            _rootPanel = root;
            var frame = new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://Art/LordWarArt/UI_界面/windowBig__resources.assets__852.png") };
            frame.TextureMarginLeft = 12; frame.TextureMarginRight = 12; frame.TextureMarginTop = 12; frame.TextureMarginBottom = 12;
            root.AddThemeStyleboxOverride("panel", frame);
            AddChild(root);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 16); margin.AddThemeConstantOverride("margin_right", 16);
            margin.AddThemeConstantOverride("margin_top", 12); margin.AddThemeConstantOverride("margin_bottom", 12);
            root.AddChild(margin);

            var main = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            main.AddThemeConstantOverride("separation", 8);
            margin.AddChild(main);

            var title = new Label { Text = "领主战争 · " + BuildInfo.BuildId, HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 18); main.AddChild(title);

            _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _summary.AddThemeFontSizeOverride("font_size", 16); main.AddChild(_summary);

            var timeRow = new HFlowContainer(); main.AddChild(timeRow);
            AddButton(timeRow, "暂停/继续", TogglePause);
            AddButton(timeRow, "×1", () => SetSpeed(1)); AddButton(timeRow, "×2", () => SetSpeed(2)); AddButton(timeRow, "×4", () => SetSpeed(4));
            AddButton(timeRow, "推进一日", AdvanceDay);
            AddButton(timeRow, "保存", SaveGame); AddButton(timeRow, "读取", LoadGame);
            AddButton(timeRow, "更多", () => SetExpanded(!_expanded));

            var tabs1 = new HFlowContainer(); main.AddChild(tabs1); _tabs1 = tabs1;
            AddTab(tabs1, "总览", 0); AddTab(tabs1, "城市规划", 1); AddTab(tabs1, "军事", 2); AddTab(tabs1, "人事任命", 3);
            var tabs2 = new HFlowContainer(); main.AddChild(tabs2); _tabs2 = tabs2;
            AddTab(tabs2, "外交", 4); AddTab(tabs2, "政策", 5); AddTab(tabs2, "战争战役", 6); AddTab(tabs2, "战报", 7); AddButton(tabs2, "主菜单", ReturnMenu);

            _actionBox = new VBoxContainer(); _actionBox.AddThemeConstantOverride("separation", 5); main.AddChild(_actionBox);
            BuildActions();

            _status = new Label { Text = "选择操作。所有建造/任命仍走申请与批准链。", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _status.AddThemeFontSizeOverride("font_size", 15); main.AddChild(_status);

            _details = new RichTextLabel { BbcodeEnabled = false, FitContent = false, ScrollActive = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            _details.AddThemeFontSizeOverride("normal_font_size", 15);
            main.AddChild(_details);
            SetExpanded(false);
        }

        void SetExpanded(bool expanded) {
            _expanded = expanded;
            if (_rootPanel != null) _rootPanel.AnchorTop = expanded ? .28f : .79f;
            if (_tabs1 != null) _tabs1.Visible = expanded;
            if (_tabs2 != null) _tabs2.Visible = expanded;
            if (_actionBox != null) _actionBox.Visible = expanded;
            if (_details != null) _details.Visible = expanded;
        }

        void AddButton(Container parent, string text, Action action) {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(84, 34), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var frame = new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://Art/LordWarArt/UI_界面/buttonLong__e2122512b478b784b9d820a3167ca52e__1.png") };
            frame.TextureMarginLeft = 8; frame.TextureMarginRight = 8; frame.TextureMarginTop = 6; frame.TextureMarginBottom = 6;
            b.AddThemeStyleboxOverride("normal", frame);
            b.AddThemeFontSizeOverride("font_size", 15); b.Pressed += action; parent.AddChild(b);
        }
        void AddTab(Container parent,string text,int page){AddButton(parent,text,()=>{_page=page;BuildActions();Refresh();});}

        void BuildActions(){
            if(_actionBox==null)return;
            foreach(Node n in _actionBox.GetChildren())n.QueueFree();
            if(_page==1)BuildCityActions();
            else if(_page==2)BuildMilitaryActions();
            else if(_page==3)BuildPersonnelActions();
            else if(_page==4)BuildDiplomacyActions();
            else if(_page==5)BuildPolicyActions();
            else if(_page==6)BuildWarActions();
            else if(_page==7)BuildReportActions();
            else BuildOverviewActions();
        }

        void BuildOverviewActions(){var r=new HFlowContainer();_actionBox.AddChild(r);AddButton(r,"聚焦都城",FocusCapital);AddButton(r,"批准首项",ApproveFirstProposal);AddButton(r,"拒绝首项",RejectFirstProposal);}
        void BuildCityActions(){
            var nav=new HFlowContainer();_actionBox.AddChild(nav);AddButton(nav,"上一城",()=>CycleCity(-1));AddButton(nav,"下一城",()=>CycleCity(1));AddButton(nav,"聚焦当前城",FocusSelectedCity);
            var r1=new HFlowContainer();_actionBox.AddChild(r1);AddButton(r1,"住宅区",()=>Zone(ZoneKind.Residential));AddButton(r1,"商业区",()=>Zone(ZoneKind.Commercial));AddButton(r1,"工坊区",()=>Zone(ZoneKind.Workshop));AddButton(r1,"农牧区",()=>Zone(ZoneKind.Agriculture));AddButton(r1,"军政区",()=>Zone(ZoneKind.Military));
            var r2=new HFlowContainer();_actionBox.AddChild(r2);AddButton(r2,"建田地",()=>QueueBuilding(BuildingKind.Farm));AddButton(r2,"伐木场",()=>QueueBuilding(BuildingKind.Lumberyard));AddButton(r2,"建仓库",()=>QueueBuilding(BuildingKind.Warehouse));AddButton(r2,"建市集",()=>QueueBuilding(BuildingKind.Market));
            var r3=new HFlowContainer();_actionBox.AddChild(r3);AddButton(r3,"建住宅",()=>QueueBuilding(BuildingKind.House));AddButton(r3,"建楼房",()=>QueueBuilding(BuildingKind.Apartment));AddButton(r3,"建磨坊",()=>QueueBuilding(BuildingKind.Mill));AddButton(r3,"建军营",()=>QueueBuilding(BuildingKind.Barracks));AddButton(r3,"扩城墙",()=>QueueBuilding(BuildingKind.Wall));
            var r4=new HFlowContainer();_actionBox.AddChild(r4);AddButton(r4,"包子铺",()=>QueueBuilding(BuildingKind.BunShop));AddButton(r4,"面包坊",()=>QueueBuilding(BuildingKind.Bakery));AddButton(r4,"铁匠铺",()=>QueueBuilding(BuildingKind.Smithy));AddButton(r4,"军械库",()=>QueueBuilding(BuildingKind.Armory));AddButton(r4,"训练场",()=>QueueBuilding(BuildingKind.TrainingGround));
        }
        void BuildMilitaryActions(){
            var nav=new HFlowContainer();_actionBox.AddChild(nav);AddButton(nav,"上一军",()=>CycleArmy(-1));AddButton(nav,"下一军",()=>CycleArmy(1));AddButton(nav,"聚焦军队",FocusSelectedArmy);
            var r1=new HFlowContainer();_actionBox.AddChild(r1);AddButton(r1,"征募基础兵",RecruitBasic);AddButton(r1,"招募特殊兵",RecruitSpecial);AddButton(r1,"当前军驻守",HoldSelectedArmy);AddButton(r1,"当前军撤退",RetreatSelectedArmy);
            var r2=new HFlowContainer();_actionBox.AddChild(r2);AddButton(r2,"有限追击",()=>SetPursuit(PursuitMode.Limited));AddButton(r2,"停止追击",()=>SetPursuit(PursuitMode.Stop));AddButton(r2,"全军驻守",HoldAllArmies);
        }
        void BuildPersonnelActions(){var r=new HFlowContainer();_actionBox.AddChild(r);AddButton(r,"提名最强官员",NominateTopOfficial);AddButton(r,"提名最强将军",NominateTopGeneral);AddButton(r,"批准任命",ApproveFirstProposal);AddButton(r,"拒绝申请",RejectFirstProposal);}
        void BuildDiplomacyActions(){
            var nav=new HFlowContainer();_actionBox.AddChild(nav);AddButton(nav,"上个目标",()=>CycleEnemy(-1));AddButton(nav,"下个目标",()=>CycleEnemy(1));
            var r=new HFlowContainer();_actionBox.AddChild(r);AddButton(r,"尝试通商",TryTrade);AddButton(r,"互不侵犯",TryNonAggression);AddButton(r,"尝试结盟",TryAlliance);AddButton(r,"议和",TryTruce);AddButton(r,"宣战并进军",DeclareSelectedWar);
        }
        void BuildPolicyActions(){
            var r=new HFlowContainer();_actionBox.AddChild(r);AddButton(r,"提议可行政策",ProposeAvailablePolicy);AddButton(r,"批准首项",ApproveFirstProposal);AddButton(r,"拒绝首项",RejectFirstProposal);
        }
        void BuildWarActions(){
            var nav=new HFlowContainer();_actionBox.AddChild(nav);AddButton(nav,"上个敌国",()=>CycleEnemy(-1));AddButton(nav,"下个敌国",()=>CycleEnemy(1));AddButton(nav,"当前军出征",DeclareSelectedWar);
            var r1=new HFlowContainer();_actionBox.AddChild(r1);AddButton(r1,"指派最佳战役官",AssignBestWarOfficial);AddButton(r1,"均衡",()=>SetWarFocus(WarAdministrationFocus.Balanced));AddButton(r1,"守城",()=>SetWarFocus(WarAdministrationFocus.Defense));AddButton(r1,"野战",()=>SetWarFocus(WarAdministrationFocus.FieldBattle));
            var r2=new HFlowContainer();_actionBox.AddChild(r2);AddButton(r2,"攻城",()=>SetWarFocus(WarAdministrationFocus.Siege));AddButton(r2,"拦截",()=>SetWarFocus(WarAdministrationFocus.Interception));AddButton(r2,"安抚占领",()=>ResolveOccupation(OccupationPolicy.Conciliate));AddButton(r2,"有限征发",()=>ResolveOccupation(OccupationPolicy.LimitedPlunder));AddButton(r2,"全面掠夺",()=>ResolveOccupation(OccupationPolicy.FullPlunder));
        }
        void BuildReportActions(){var r=new HFlowContainer();_actionBox.AddChild(r);AddButton(r,"聚焦都城",FocusCapital);AddButton(r,"推进一日",AdvanceDay);}

        GameWorld W(){return _app==null?null:_app.World;}
        Kingdom PlayerKingdom(){GameWorld w=W();Kingdom k;if(w==null||string.IsNullOrEmpty(w.PlayerKingdomId)||!w.Kingdoms.TryGetValue(w.PlayerKingdomId,out k))return null;return k;}
        City PlayerCapital(){GameWorld w=W();Kingdom k=PlayerKingdom();City c;if(w==null||k==null||!w.Cities.TryGetValue(k.CapitalCityId,out c))return null;return c;}
        List<City> PlayerCities(){var list=new List<City>();GameWorld w=W();if(w==null)return list;foreach(City c in w.Cities.Values)if(c!=null&&c.KingdomId==w.PlayerKingdomId)list.Add(c);list.Sort((a,b)=>string.CompareOrdinal(a.Name,b.Name));return list;}
        City SelectedCity(){var list=PlayerCities();if(list.Count==0)return null;_cityIndex=((_cityIndex%list.Count)+list.Count)%list.Count;return list[_cityIndex];}
        List<Army> PlayerArmies(){var list=new List<Army>();GameWorld w=W();if(w==null)return list;foreach(Army a in w.Armies.Values)if(a!=null&&a.KingdomId==w.PlayerKingdomId)list.Add(a);list.Sort((a,b)=>string.CompareOrdinal(a.Name,b.Name));return list;}
        Army SelectedArmy(){var list=PlayerArmies();if(list.Count==0)return null;_armyIndex=((_armyIndex%list.Count)+list.Count)%list.Count;return list[_armyIndex];}
        List<Kingdom> OtherKingdoms(bool requireWar){var list=new List<Kingdom>();GameWorld w=W();if(w==null)return list;foreach(Kingdom k in w.Kingdoms.Values){if(k==null||k.Id==w.PlayerKingdomId||k.Status==KingdomStatus.Eliminated)continue;if(requireWar&&w.Diplomacy.Get(w.PlayerKingdomId,k.Id).State!=DiplomacyState.War)continue;list.Add(k);}list.Sort((a,b)=>string.CompareOrdinal(a.Name,b.Name));return list;}
        Kingdom SelectedOtherKingdom(bool requireWar){var list=OtherKingdoms(requireWar);if(list.Count==0)return null;_enemyIndex=((_enemyIndex%list.Count)+list.Count)%list.Count;return list[_enemyIndex];}
        Army FirstPlayerArmy(){return SelectedArmy();}
        Kingdom FirstOtherKingdom(bool requireWar){return SelectedOtherKingdom(requireWar);}

        void SetStatus(string text){if(_status!=null)_status.Text=text??"";}
        void ClockAction(WorldCommandKind kind,float value=0f){GameWorld w=W();if(w==null)return;string reason;bool ok=w.ExecuteClockCommand(new WorldCommand(Guid.NewGuid().ToString("N"),kind,value),out reason);SetStatus(ok?(kind==WorldCommandKind.Pause?"已暂停":kind==WorldCommandKind.Resume?"继续运行":kind==WorldCommandKind.AdvanceDay?"已推进一日":"速度 ×"+value.ToString("0.#")):reason);Refresh();}
        void TogglePause(){GameWorld w=W();if(w!=null)ClockAction(w.Paused?WorldCommandKind.Resume:WorldCommandKind.Pause);}
        void SetSpeed(float speed){ClockAction(WorldCommandKind.SetSpeed,speed);}
        void AdvanceDay(){ClockAction(WorldCommandKind.AdvanceDay);}
        void SaveGame(){GameWorld w=W();if(w==null)return;string m;GodotSaveService.Save(w,out m);SetStatus(m);}
        void LoadGame(){GameWorld w=W();if(w==null)return;string m;bool ok=GodotSaveService.Load(w,out m);if(ok)_app.RebindViews();SetStatus(m);Refresh();}
        void ReturnMenu(){if(_app!=null)_app.ReturnToMainMenu();}
        void FocusCapital(){City c=PlayerCapital();if(c!=null&&_app.Camera!=null)_app.Camera.FocusWorldPoint(c.X,c.Y);}
        void FocusSelectedCity(){City c=SelectedCity();if(c!=null&&_app.Camera!=null){_app.Camera.FocusWorldPoint(c.X,c.Y);SetStatus("已聚焦城市："+c.Name);}}
        void FocusSelectedArmy(){Army a=SelectedArmy();if(a!=null&&_app.Camera!=null){_app.Camera.FocusWorldPoint(a.X,a.Y);SetStatus("已聚焦军队："+a.Name);}}
        void CycleCity(int d){var list=PlayerCities();if(list.Count==0)return;_cityIndex=(_cityIndex+d+list.Count)%list.Count;SetStatus("当前城市："+SelectedCity().Name);Refresh();}
        void CycleArmy(int d){var list=PlayerArmies();if(list.Count==0)return;_armyIndex=(_armyIndex+d+list.Count)%list.Count;SetStatus("当前军队："+SelectedArmy().Name);Refresh();}
        void CycleEnemy(int d){var list=OtherKingdoms(false);if(list.Count==0)return;_enemyIndex=(_enemyIndex+d+list.Count)%list.Count;SetStatus("当前外交目标："+SelectedOtherKingdom(false).Name);Refresh();}

        void Zone(ZoneKind kind){GameWorld w=W();if(w==null)return;City selected=SelectedCity();if(selected==null)return;CityZone z=(selected.Id==PlayerCapital()?.Id)?w.DesignatePlayerZone(kind):w.Planning.AddSuggested(selected,kind,true,w.Day);SetStatus(z==null?"无法规划该地块":"已规划："+z.Name);Refresh();}
        void QueueBuilding(BuildingKind kind){
            GameWorld w=W();City c=SelectedCity();Kingdom k=PlayerKingdom();if(w==null||c==null||k==null){SetStatus("暂无可建设都城");return;}
            List<Person> officials=w.RankedServingOfficials(k.Id);if(officials.Count==0){SetStatus("需要先任命至少一名正式官员");return;}
            int seconds=70,cost=160;switch(kind){case BuildingKind.Wall:seconds=130;cost=420;break;case BuildingKind.Barracks:seconds=95;cost=260;break;case BuildingKind.Warehouse:case BuildingKind.Mill:seconds=80;cost=190;break;case BuildingKind.Market:seconds=75;cost=180;break;case BuildingKind.Farm:case BuildingKind.Lumberyard:seconds=65;cost=140;break;case BuildingKind.Apartment:seconds=90;cost=200;break;case BuildingKind.BunShop:case BuildingKind.Bakery:seconds=65;cost=150;break;case BuildingKind.Smithy:seconds=80;cost=190;break;case BuildingKind.Armory:case BuildingKind.TrainingGround:seconds=90;cost=230;break;}
            Proposal p=w.SubmitConstructionProposal(c,officials[0],kind,"修建"+ChineseText.Building(kind),seconds,cost);
            SetStatus(p==null?"官员能力不足或申请无法提交":"已提交申请："+p.Title+"，请批准后施工");Refresh();
        }

        void RecruitBasic(){
            GameWorld w=W();Army a=SelectedArmy();City c=SelectedCity();Kingdom k=PlayerKingdom();if(w==null||a==null||c==null||k==null){SetStatus("需要都城和现役军队");return;}
            if(a.Finance.Gold<100&&k.Treasury>0){int grant=Math.Min(180,k.Treasury);k.Treasury-=grant;a.Finance.Gold+=grant;}
            UnitDef u=null;foreach(UnitDef x in w.Data.Units.Values){u=x;break;}if(u==null){SetStatus("没有基础兵种数据");return;}
            int n=w.Military.Recruit(a,c,u.Id,10,Math.Max(1,u.RecruitCost));SetStatus(n>0?"征募 "+u.Name+" × "+n:"人口/军资/马匹不足");Refresh();
        }
        void RecruitSpecial(){
            GameWorld w=W();Army a=SelectedArmy();if(w==null||a==null){SetStatus("没有可用军队");return;}
            string last="暂无符合条件的特殊兵种";foreach(SpecialUnitDef s in w.Data.SpecialUnits.Values){string reason;if(w.TryRecruitSpecial(a.Id,s.Id,out reason)){SetStatus("已招募特殊兵："+s.Name);Refresh();return;}if(!string.IsNullOrEmpty(reason))last=s.Name+"："+reason;}SetStatus(last);
        }
        void HoldSelectedArmy(){Army a=SelectedArmy();if(a==null){SetStatus("没有可用军队");return;}a.Order=ArmyOrder.Hold;SetStatus(a.Name+" 已转为驻守");}
        void RetreatSelectedArmy(){Army a=SelectedArmy();if(a==null){SetStatus("没有可用军队");return;}a.Order=ArmyOrder.Retreat;SetStatus(a.Name+" 已下达撤退命令");}
        void HoldAllArmies(){GameWorld w=W();if(w==null)return;int n=0;foreach(Army a in w.Armies.Values)if(a.KingdomId==w.PlayerKingdomId){a.Order=ArmyOrder.Hold;n++;}SetStatus("已令 "+n+" 支军队驻守");}
        void SetPursuit(PursuitMode mode){GameWorld w=W();if(w==null)return;foreach(Army a in w.Armies.Values)if(a.KingdomId==w.PlayerKingdomId)a.PursuitMode=mode;SetStatus(mode==PursuitMode.Stop?"全军停止追击":"全军采用有限追击");}

        void ProposeAvailablePolicy(){
            GameWorld w=W();Kingdom k=PlayerKingdom();City c=SelectedCity();if(w==null||k==null||c==null){SetStatus("暂无国家或城市");return;}
            List<Person> officials=w.RankedServingOfficials(k.Id);if(officials.Count==0){SetStatus("需要正式官员才能提出政策");return;}
            Person official=officials[0];
            foreach(PolicyDef p in w.Data.Policies.Values){if(w.Policies.Propose(p.Id,official,k,w.Day)){SetStatus("已由"+official.Name+"提出政策：《"+p.DisplayName+"》，等待批准");Refresh();return;}}
            SetStatus("当前没有该官员能够提出的新政策，可能能力不足或政策已在实施/排队");
        }

        void NominateTopOfficial(){GameWorld w=W();City c=PlayerCapital();if(w==null||c==null){SetStatus("暂无可用都城");return;}List<Person> list=w.RankedOfficialCandidates(c);if(list.Count==0){SetStatus("当前没有可提名官员候选");return;}string reason;w.SubmitPlayerAppointment(list[0].Id,"official",out reason);SetStatus(reason);Refresh();}
        void NominateTopGeneral(){GameWorld w=W();City c=PlayerCapital();if(w==null||c==null){SetStatus("暂无可用都城");return;}List<Person> list=w.RankedGeneralCandidates(c);if(list.Count==0){SetStatus("当前没有可提名将军候选");return;}string reason;w.SubmitPlayerAppointment(list[0].Id,"general",out reason);SetStatus(reason);Refresh();}
        void ApproveFirstProposal(){GameWorld w=W();if(w==null)return;Proposal p=w.FirstPlayerPendingProposal();if(p==null){SetStatus("当前没有待批申请");return;}bool ok=w.ApproveProposal(p.Id);SetStatus(ok?"已批准："+p.Title:"批准失败/条件不足："+p.Title);Refresh();}
        void RejectFirstProposal(){GameWorld w=W();if(w==null)return;Proposal p=w.FirstPlayerPendingProposal();if(p==null){SetStatus("当前没有待批申请");return;}bool ok=w.RejectProposal(p.Id);SetStatus(ok?"已拒绝："+p.Title:"拒绝失败："+p.Title);Refresh();}

        void TryTrade(){GameWorld w=W();Kingdom o=SelectedOtherKingdom(false);if(w==null||o==null)return;bool ok=w.Diplomacy.Trade(w.PlayerKingdomId,o.Id);SetStatus(ok?"已与"+o.Name+"建立通商":"通商条件尚未满足");Refresh();}
        void TryNonAggression(){GameWorld w=W();Kingdom o=SelectedOtherKingdom(false);if(w==null||o==null)return;bool ok=w.Diplomacy.NonAggression(w.PlayerKingdomId,o.Id,w.Day,45);SetStatus(ok?"已与"+o.Name+"签订互不侵犯":"互不侵犯条件尚未满足");Refresh();}
        void TryAlliance(){GameWorld w=W();Kingdom o=SelectedOtherKingdom(false);if(w==null||o==null)return;bool ok=w.Diplomacy.Alliance(w.PlayerKingdomId,o.Id);SetStatus(ok?"已与"+o.Name+"结盟":"结盟条件尚未满足");Refresh();}
        void TryTruce(){GameWorld w=W();Kingdom o=SelectedOtherKingdom(false);if(w==null||o==null)return;bool ok=w.Diplomacy.MakeTruce(w.PlayerKingdomId,o.Id,w.Day,30);SetStatus(ok?"已与"+o.Name+"议和，停战30日":"当前不处于可议和战争状态");Refresh();}
        void DeclareSelectedWar(){GameWorld w=W();Kingdom o=SelectedOtherKingdom(false);Army a=SelectedArmy();if(w==null||o==null||a==null){SetStatus("缺少敌国或可用军队");return;}int moved=w.DeclareWarAndMarch(o.Id,new string[]{a.Id});if(moved>0){string reason;w.AssignBestAvailableWarOfficial(out reason);SetStatus("已向"+o.Name+"宣战，"+a.Name+"开始集结。"+reason);}else SetStatus("当前无法宣战/进军：检查条约、兵力和粮草");Refresh();}
        void AssignBestWarOfficial(){GameWorld w=W();if(w==null)return;string reason;bool ok=w.AssignBestAvailableWarOfficial(out reason);SetStatus((ok?"已指派：":"未调整：")+reason);Refresh();}
        void SetWarFocus(WarAdministrationFocus focus){GameWorld w=W();Kingdom e=SelectedOtherKingdom(true);if(w==null||e==null){SetStatus("当前没有正式战争");return;}string reason;bool ok=w.SetWarOfficialFocus(e.Id,focus,out reason);SetStatus(ok?"战务重点已调整为 "+ChineseText.WarFocus(focus):reason);Refresh();}
        void ResolveOccupation(OccupationPolicy policy){GameWorld w=W();if(w==null)return;foreach(City c in w.Cities.Values)if(c.KingdomId==w.PlayerKingdomId&&c.Occupation==OccupationPolicy.Pending){bool ok=w.ResolveOccupation(c.Id,policy);SetStatus(ok?"已处理占领城市："+c.Name:"占领处理失败");Refresh();return;}SetStatus("当前没有待处理占领城市");}

        void Refresh(){
            GameWorld w=W();if(w==null||_summary==null||_details==null||w.Weather==null)return;
            Kingdom player=PlayerKingdom();City capital=SelectedCity();
            _summary.Text="第 "+w.Day+" 日｜"+ChineseText.Season(w.Weather.Season)+"｜"+ChineseText.Weather(w.Weather.Weather)+"｜"+(w.Paused?"暂停":"运行")+" ×"+w.TimeScale.ToString("0.#")+"\n国家 "+w.Kingdoms.Count+"｜城市 "+w.Cities.Count+"｜人口 "+w.People.Count+"｜待批 "+w.PlayerPendingProposalCount;
            var sb=new StringBuilder();
            if(_page==1)BuildCity(sb,w,capital);else if(_page==2)BuildMilitary(sb,w,player,capital);else if(_page==3)BuildPersonnel(sb,w,player,capital);else if(_page==4)BuildDiplomacy(sb,w,player);else if(_page==5)BuildPolicies(sb,w,player);else if(_page==6)BuildWar(sb,w,player);else if(_page==7)BuildReports(sb,w);else BuildOverview(sb,w,player,capital);
            _details.Text=sb.ToString();
        }

        void BuildOverview(StringBuilder sb,GameWorld w,Kingdom p,City c){if(p!=null)sb.AppendLine("【国家】"+p.Name+"｜国库 "+p.Treasury+"｜威望 "+p.Prestige+"｜军队 "+p.ArmyIds.Count);if(c!=null)sb.AppendLine("【当前城市】"+c.Name+"｜粮 "+c.Food+" 木 "+c.Wood+" 石 "+c.Stone+" 铁 "+c.Iron+" 马 "+c.Horses);sb.AppendLine();sb.AppendLine("【当前目标】发展城市 → 任命人才 → 扩军备战 → 宣战/防御 → 占领与治理。所有系统持续在同一个世界时间线上运行。");}
        void BuildCity(StringBuilder sb,GameWorld w,City c){if(c==null){sb.AppendLine("暂无都城");return;}sb.AppendLine("【"+c.Name+" 城市】人口容量 "+c.PopulationCapacity+"｜农务 "+c.FarmJobs+"｜伐木 "+c.LoggingJobs+"｜工坊 "+c.WorkshopJobs+"｜市场 "+c.MarketJobs);var counts=new Dictionary<BuildingKind,int>();int wall=0,gate=0,tower=0;foreach(string id in c.BuildingIds){Building b;if(!w.Buildings.TryGetValue(id,out b)||b==null||b.Ruined)continue;counts[b.Kind]=counts.ContainsKey(b.Kind)?counts[b.Kind]+1:1;if(b.Kind==BuildingKind.Wall)wall++;else if(b.Kind==BuildingKind.Gate)gate++;else if(b.Kind==BuildingKind.Tower)tower++;}sb.AppendLine("外郭半径 "+c.FortificationRadius+"｜墙 "+wall+"｜门 "+gate+"｜塔 "+tower);foreach(var kv in counts)sb.AppendLine("• "+ChineseText.Building(kv.Key)+" × "+kv.Value);sb.AppendLine("\n【地块】");foreach(CityZone z in c.Zones)if(z!=null)sb.AppendLine("• "+z.Name+"｜半径"+z.Radius+"｜建筑"+z.BuildingCount);}
        void BuildMilitary(StringBuilder sb,GameWorld w,Kingdom p,City c){Army selected=SelectedArmy();sb.AppendLine("【当前军队】"+(selected==null?"无":selected.Name)+"｜当前补员城市 "+(c==null?"无":c.Name));sb.AppendLine("【全部军队】");foreach(Army a in w.Armies.Values){if(a.KingdomId!=w.PlayerKingdomId)continue;Person g=null;w.People.TryGetValue(a.GeneralId,out g);sb.AppendLine("• "+a.Name+"｜主将 "+(g==null?"无":g.Name)+"｜兵力 "+w.Military.SoldierCount(a)+"｜士气 "+Math.Round(a.Morale)+"｜疲劳 "+Math.Round(a.Fatigue)+"｜粮 "+a.FoodDays.ToString("0.0")+"日｜军令 "+ChineseText.ArmyOrderText(a.Order));foreach(Squad sq in a.Squads)sb.AppendLine("  - "+sq.Name+" × "+sq.SoldierIds.Count+"｜阵型 "+sq.FormationName+"｜凝聚 "+Math.Round(sq.Cohesion));}}
        void BuildPersonnel(StringBuilder sb,GameWorld w,Kingdom p,City c){if(p==null)return;sb.AppendLine("【官员｜智慧与统帅/组织决定战役规划，但单项加成有上限】");var os=w.RankedServingOfficials(p.Id);for(int i=0;i<Math.Min(10,os.Count);i++){Person x=os[i];sb.AppendLine((i+1)+". "+x.Name+"｜智"+x.Stats.Intelligence+" 统"+x.Stats.Command+" 组"+x.Stats.Organization+" 后"+x.Stats.Logistics+"｜评分"+Math.Round(w.OfficialCandidateScore(x)));}sb.AppendLine("\n【将军｜武力第一、统帅第二】");var gs=w.RankedServingGenerals(p.Id);for(int i=0;i<Math.Min(10,gs.Count);i++){Person x=gs[i];sb.AppendLine((i+1)+". "+x.Name+"｜武"+x.Stats.Martial+" 统"+x.Stats.Command+" 生命"+x.Stats.Life+"/"+x.Stats.MaxLife+"｜评分"+Math.Round(w.GeneralCandidateScore(x)));}}
        void BuildDiplomacy(StringBuilder sb,GameWorld w,Kingdom p){if(p==null)return;Kingdom target=SelectedOtherKingdom(false);sb.AppendLine("【当前外交目标】"+(target==null?"无":target.Name));sb.AppendLine("【外交】");foreach(Kingdom k in w.Kingdoms.Values){if(k.Id==p.Id)continue;DiplomacyRelation r=w.Diplomacy.Get(p.Id,k.Id);sb.AppendLine("• "+k.Name+"｜"+r.State+"｜关系 "+r.Opinion+(r.TruceUntilDay>w.Day?"｜停战至"+r.TruceUntilDay+"日":""));}}
        void BuildPolicies(StringBuilder sb,GameWorld w,Kingdom p){if(p==null)return;sb.AppendLine("【已生效政策】");if(p.ActivePolicyIds.Count==0)sb.AppendLine("暂无");foreach(string id in p.ActivePolicyIds){PolicyDef d;if(w.Data.Policies.TryGetValue(id,out d))sb.AppendLine("• "+d.DisplayName+"｜"+d.Category+"｜月维护 "+d.Monthly);}sb.AppendLine("\n【待批/执行中申请】");foreach(Proposal x in w.Proposals.Queue)if(x!=null&&w.IsPlayerProposal(x)&&(x.State==ProposalState.Pending||x.State==ProposalState.Approved||x.State==ProposalState.Running))sb.AppendLine("• "+x.Title+"｜"+x.Kind+"｜"+x.State+"｜成本 "+x.CostGold);}
        void BuildWar(StringBuilder sb,GameWorld w,Kingdom p){if(p==null)return;Kingdom target=SelectedOtherKingdom(false);sb.AppendLine("【当前战争目标】"+(target==null?"无":target.Name));sb.AppendLine("【战争与战役】");bool any=false;foreach(Kingdom e in w.Kingdoms.Values){if(e.Id==p.Id||w.Diplomacy.Get(p.Id,e.Id).State!=DiplomacyState.War)continue;any=true;WarAdministration wa=w.WarAdministrationFor(p.Id,e.Id);sb.AppendLine("• 对 "+e.Name+"｜"+(wa==null?"未指派战役官员":"重点 "+ChineseText.WarFocus(wa.Focus)+"｜"+wa.LastAction));}if(!any)sb.AppendLine("当前无正式战争。可从外交页宣战并集结最近军队。");sb.AppendLine("\n【进行中战斗】");var seen=new HashSet<string>();foreach(Army a in w.Armies.Values){BattleSession b=w.ActiveBattleForArmy(a.Id);if(b==null||!seen.Add(b.Id))continue;sb.AppendLine("• "+b.LocationName+"｜"+b.ElapsedSeconds+"/"+b.MaxSeconds+"秒｜"+b.Report.LastActionText);}}
        void BuildReports(StringBuilder sb,GameWorld w){sb.AppendLine("【最近战报/世界事件】");int start=Math.Max(0,w.Events.Count-30);for(int i=w.Events.Count-1;i>=start;i--){WorldEvent e=w.Events[i];if(e!=null)sb.AppendLine("第"+e.Day+"日｜"+e.Category+"｜"+e.Title+"｜"+e.Detail);}}
    }
}
