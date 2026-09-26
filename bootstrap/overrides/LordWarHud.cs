using System;
using System.Text;
using System.Collections.Generic;
using Godot;
using LordWar.AI;
using LordWar.Simulation;

namespace LordWar.GodotRuntime {
    /// <summary>Godot Control-based Chinese HUD baseline replacing Unity IMGUI.</summary>
    public sealed partial class LordWarHud : CanvasLayer {
        LordWarApp _app;
        Label _summary;
        RichTextLabel _details;
        Label _status;
        PanelContainer _detailPanel;
        bool _detailsVisible = true;
        double _refreshClock;
        int _page;

        public void Bind(LordWarApp app) {
            _app = app;
            BuildUi();
            Refresh();
        }

        public override void _Process(double delta) {
            _refreshClock += delta;
            if (_refreshClock >= .25) { _refreshClock = 0; Refresh(); }
        }

        void BuildUi() {
            var root = new Control {
                Name = "HUD根节点",
                AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(root);

            var topPanel = new PanelContainer {
                AnchorLeft = 0.01f, AnchorTop = 0.015f, AnchorRight = 0.99f, AnchorBottom = 0.34f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0
            };
            root.AddChild(topPanel);
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12); margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_top", 8); margin.AddThemeConstantOverride("margin_bottom", 8);
            topPanel.AddChild(margin);
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 6);
            margin.AddChild(v);

            var title = new Label { Text = "领主战争｜战争与城市经营", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 20); v.AddChild(title);
            _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _summary.AddThemeFontSizeOverride("font_size", 15); v.AddChild(_summary);

            var row1 = new HFlowContainer(); row1.AddThemeConstantOverride("h_separation", 5); row1.AddThemeConstantOverride("v_separation", 5); v.AddChild(row1);
            AddButton(row1, "推进一日", () => { if (_app.World != null) { _app.World.AdvanceDay(); SetStatus("时间推进"); } });
            AddButton(row1, "暂停/继续", () => { if (_app.World != null) _app.World.Paused = !_app.World.Paused; });
            AddButton(row1, "×1", () => SetSpeed(1)); AddButton(row1, "×2", () => SetSpeed(2)); AddButton(row1, "×4", () => SetSpeed(4));
            AddButton(row1, "聚焦都城", FocusCapital);
            AddButton(row1, "主菜单", () => _app.ReturnToMainMenu());
            AddButton(row1, "隐藏/显示详情", ToggleDetails);

            var row2 = new HFlowContainer(); row2.AddThemeConstantOverride("h_separation", 5); row2.AddThemeConstantOverride("v_separation", 5); v.AddChild(row2);
            AddButton(row2, "保存游戏", () => { string m; GodotSaveService.Save(_app.World, out m); SetStatus(m); });
            AddButton(row2, "读取存档", () => { string m; bool ok = GodotSaveService.Load(_app.World, out m); if (ok) _app.RebindViews(); SetStatus(m); });
            AddButton(row2, "总览", () => { _page=0; Refresh(); });
            AddButton(row2, "人事任命", () => { _page=1; Refresh(); });
            AddButton(row2, "城市规划", () => { _page=2; Refresh(); });
            AddButton(row2, "战争战役", () => { _page=3; Refresh(); });

            var row3 = new HFlowContainer(); row3.AddThemeConstantOverride("h_separation", 5); row3.AddThemeConstantOverride("v_separation", 5); v.AddChild(row3);
            AddButton(row3, "提名最强官员", NominateTopOfficial);
            AddButton(row3, "提名最强将军", NominateTopGeneral);
            AddButton(row3, "批准首项", ApproveFirstProposal);
            AddButton(row3, "拒绝首项", RejectFirstProposal);
            AddButton(row3, "指派战役官员", AssignBestWarOfficial);

            var row4 = new HFlowContainer(); row4.AddThemeConstantOverride("h_separation", 5); row4.AddThemeConstantOverride("v_separation", 5); v.AddChild(row4);
            AddButton(row4, "建住宅", () => QuickBuild(BuildingKind.House, 120, 55));
            AddButton(row4, "建田地", () => QuickBuild(BuildingKind.Farm, 150, 70));
            AddButton(row4, "建伐木场", () => QuickBuild(BuildingKind.Lumberyard, 150, 70));
            AddButton(row4, "建仓库", () => QuickBuild(BuildingKind.Warehouse, 180, 75));
            AddButton(row4, "建市集", () => QuickBuild(BuildingKind.Market, 180, 70));
            AddButton(row4, "建军营", () => QuickBuild(BuildingKind.Barracks, 260, 90));
            AddButton(row4, "扩建城墙", () => QuickBuild(BuildingKind.Wall, 420, 120));

            var row5 = new HFlowContainer(); row5.AddThemeConstantOverride("h_separation", 5); row5.AddThemeConstantOverride("v_separation", 5); v.AddChild(row5);
            AddButton(row5, "宣战出征", DeclareWarAndMarch);
            AddButton(row5, "征募特殊兵", RecruitFirstSpecial);
            AddButton(row5, "安抚占领", () => ResolveFirstOccupation(OccupationPolicy.Conciliate));
            AddButton(row5, "有限掠夺", () => ResolveFirstOccupation(OccupationPolicy.LimitedPlunder));

            _status = new Label { Text = "软克制、兵种协同、官员战役规划已接入", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _status.AddThemeFontSizeOverride("font_size", 14); v.AddChild(_status);

            _detailPanel = new PanelContainer {
                AnchorLeft = 0.01f, AnchorTop = 0.355f, AnchorRight = 0.56f, AnchorBottom = 0.985f,
                OffsetLeft = 0, OffsetTop = 0, OffsetRight = 0, OffsetBottom = 0
            };
            root.AddChild(_detailPanel);
            _details = new RichTextLabel {
                BbcodeEnabled = false,
                FitContent = false,
                ScrollActive = true,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _details.AddThemeFontSizeOverride("normal_font_size", 15);
            _detailPanel.AddChild(_details);
            GD.Print("LORDWAR_HUD_READY");
        }

        void ToggleDetails() {
            _detailsVisible = !_detailsVisible;
            if (_detailPanel != null) _detailPanel.Visible = _detailsVisible;
        }

        void AddButton(Container parent, string text, Action action) {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(116, 42), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
            b.Pressed += action;
            parent.AddChild(b);
        }

        void SetSpeed(float speed) { if (_app.World != null) { _app.World.TimeScale = speed; SetStatus("速度 ×" + speed.ToString("0.#")); } }
        void SetStatus(string text) { if (_status != null) _status.Text = text ?? ""; }
        void QuickBuild(BuildingKind kind, int cost, int seconds) {
            GameWorld w = _app == null ? null : _app.World;
            City city = PlayerCapital();
            if (w == null || city == null) { SetStatus("暂无可用都城"); return; }
            List<Person> officials = w.RankedServingOfficials(w.PlayerKingdomId);
            Person official = officials.Count > 0 ? officials[0] : null;
            if (official == null) {
                List<Person> candidates = w.RankedOfficialCandidates(city);
                if (candidates.Count > 0) official = candidates[0];
            }
            if (official == null) { SetStatus("没有可主持工程的官员"); return; }
            Proposal p = w.SubmitConstructionProposal(city, official, kind, "修建" + ChineseText.Building(kind), seconds, cost);
            if (p == null) { SetStatus("工程申请未能提交：" + ChineseText.Building(kind)); return; }
            bool ok = w.ApproveProposal(p.Id);
            SetStatus(ok ? "已开工：" + ChineseText.Building(kind) : "资源/条件不足：" + ChineseText.Building(kind));
            _page = 2; Refresh();
        }

        void DeclareWarAndMarch() {
            GameWorld w = _app == null ? null : _app.World;
            if (w == null) return;
            bool ok = w.DeclareWarAndMarch();
            SetStatus(ok ? "已向最近敌国宣战并命令首支军队出征" : "当前无法宣战：检查军队、粮草或外交状态");
            _page = 3; Refresh();
        }

        void RecruitFirstSpecial() {
            GameWorld w = _app == null ? null : _app.World;
            if (w == null) return;
            Army army = null;
            foreach (Army a in w.Armies.Values) if (a.KingdomId == w.PlayerKingdomId) { army = a; break; }
            if (army == null) { SetStatus("没有可用军队"); return; }
            foreach (var kv in w.Data.SpecialUnits) {
                string reason;
                if (w.TryRecruitSpecial(army.Id, kv.Key, out reason)) {
                    SetStatus("已征募特殊兵：" + kv.Value.Name + " → " + army.Name);
                    _page = 3; Refresh(); return;
                }
            }
            SetStatus("当前没有满足建筑/装备/将军条件的特殊兵种");
        }

        void ResolveFirstOccupation(OccupationPolicy policy) {
            GameWorld w = _app == null ? null : _app.World;
            if (w == null) return;
            foreach (City c in w.Cities.Values) {
                if (c.KingdomId == w.PlayerKingdomId && c.Occupation == OccupationPolicy.Pending) {
                    bool ok = w.ResolveOccupation(c.Id, policy);
                    SetStatus(ok ? "已处理占领城市：" + c.Name : "占领处理失败");
                    _page = 3; Refresh(); return;
                }
            }
            SetStatus("当前没有待处理的占领城市");
        }

        City PlayerCapital(){if(_app==null||_app.World==null)return null;Kingdom k;if(!_app.World.Kingdoms.TryGetValue(_app.World.PlayerKingdomId,out k))return null;City c;return _app.World.Cities.TryGetValue(k.CapitalCityId,out c)?c:null;}
        void NominateTopOfficial(){GameWorld w=_app==null?null:_app.World;City c=PlayerCapital();if(w==null||c==null){SetStatus("暂无可用都城");return;}List<Person> list=w.RankedOfficialCandidates(c);if(list.Count==0){SetStatus("当前没有可提名官员候选");return;}string reason;w.SubmitPlayerAppointment(list[0].Id,"official",out reason);SetStatus(reason);_page=1;Refresh();}
        void NominateTopGeneral(){GameWorld w=_app==null?null:_app.World;City c=PlayerCapital();if(w==null||c==null){SetStatus("暂无可用都城");return;}List<Person> list=w.RankedGeneralCandidates(c);if(list.Count==0){SetStatus("当前没有可提名将军候选");return;}string reason;w.SubmitPlayerAppointment(list[0].Id,"general",out reason);SetStatus(reason);_page=1;Refresh();}
        void ApproveFirstProposal(){GameWorld w=_app==null?null:_app.World;if(w==null)return;Proposal p=w.FirstPlayerPendingProposal();if(p==null){SetStatus("当前没有待批申请");return;}bool ok=w.ApproveProposal(p.Id);SetStatus(ok?"已批准："+p.Title:"批准失败/条件尚未满足："+p.Title);Refresh();}
        void RejectFirstProposal(){GameWorld w=_app==null?null:_app.World;if(w==null)return;Proposal p=w.FirstPlayerPendingProposal();if(p==null){SetStatus("当前没有待批申请");return;}bool ok=w.RejectProposal(p.Id);SetStatus(ok?"已拒绝："+p.Title:"拒绝失败："+p.Title);Refresh();}
        void AssignBestWarOfficial(){GameWorld w=_app==null?null:_app.World;if(w==null)return;string reason;bool ok=w.AssignBestAvailableWarOfficial(out reason);SetStatus((ok?"已完成：":"未调整：")+reason);_page=3;Refresh();}

        void FocusCapital() {
            if (_app.World == null) return;
            Kingdom k; if (!_app.World.Kingdoms.TryGetValue(_app.World.PlayerKingdomId, out k)) return;
            City c; if (!_app.World.Cities.TryGetValue(k.CapitalCityId, out c)) return;
            _app.Camera.FocusWorldPoint(c.X, c.Y);
        }

        void Refresh() {
            GameWorld w = _app == null ? null : _app.World;
            if (w == null || _summary == null || _details == null || w.Weather == null) return;
            Kingdom player = null; City capital = null;
            if (!string.IsNullOrEmpty(w.PlayerKingdomId)) w.Kingdoms.TryGetValue(w.PlayerKingdomId, out player);
            if (player != null) w.Cities.TryGetValue(player.CapitalCityId, out capital);

            _summary.Text = "第 " + w.Day + " 日｜" + ChineseText.Season(w.Weather.Season) + "｜" + ChineseText.Weather(w.Weather.Weather)
                + "｜" + (w.IsNightTime ? "夜间" : "白昼") + "｜速度 ×" + w.TimeScale.ToString("0.#")
                + "\n国家 " + w.Kingdoms.Count + "｜城市 " + w.Cities.Count + "｜人口 " + w.People.Count + "｜待批申请 " + w.PlayerPendingProposalCount;

            var sb = new StringBuilder();
            if(_page==1)BuildPersonnel(sb,w,player,capital);
            else if(_page==2)BuildCity(sb,w,player,capital);
            else if(_page==3)BuildWar(sb,w,player);
            else BuildOverview(sb,w,player,capital);
            _details.Text = sb.ToString();
        }


        void BuildOverview(StringBuilder sb,GameWorld w,Kingdom player,City capital){
            if(player!=null)sb.AppendLine("【玩家国家】"+player.Name+"　国库 "+player.Treasury+"　军队 "+player.ArmyIds.Count+"　国势 "+ChineseText.KingdomStatusText(player.Status));
            if(capital!=null)sb.AppendLine("【都城】"+capital.Name+"　粮 "+capital.Food+"　木 "+capital.Wood+"　石 "+capital.Stone+"　铁 "+capital.Iron+"　马 "+capital.Horses);
            sb.AppendLine();sb.AppendLine("【玩家军队】");int shown=0;foreach(Army a in w.Armies.Values){if(a.KingdomId!=w.PlayerKingdomId)continue;Person g=null;w.People.TryGetValue(a.GeneralId,out g);sb.AppendLine("• "+a.Name+"｜主将 "+(g==null?"无":g.Name)+"｜兵力 "+w.Military.SoldierCount(a)+"｜军令 "+ChineseText.ArmyOrderText(a.Order)+"｜士气 "+Math.Round(a.Morale)+"｜粮 "+a.FoodDays.ToString("0.0")+"日");if(++shown>=12)break;}
            sb.AppendLine();sb.AppendLine("【最近世界事件】");int start=Math.Max(0,w.Events.Count-12);for(int i=w.Events.Count-1;i>=start;i--){WorldEvent ev=w.Events[i];if(ev==null)continue;sb.AppendLine("第"+ev.Day+"日｜"+ev.Category+"｜"+ev.Title+"｜"+ev.Detail);}
        }

        void BuildPersonnel(StringBuilder sb,GameWorld w,Kingdom player,City capital){
            if(player==null){sb.AppendLine("尚无玩家国家");return;}sb.AppendLine("【官员列表｜智慧优先，强者在前】");List<Person> officials=w.RankedServingOfficials(player.Id);for(int i=0;i<Math.Min(12,officials.Count);i++){Person p=officials[i];sb.AppendLine((i+1)+". "+p.Name+"｜智"+p.Stats.Intelligence+" 统"+p.Stats.Command+" 组"+p.Stats.Organization+" 后"+p.Stats.Logistics+" 行"+p.Stats.Administration+"｜战役评分 "+Math.Round(w.OfficialCandidateScore(p)));}
            sb.AppendLine();sb.AppendLine("【将军列表｜武力第一、统帅第二】");List<Person> generals=w.RankedServingGenerals(player.Id);for(int i=0;i<Math.Min(12,generals.Count);i++){Person p=generals[i];sb.AppendLine((i+1)+". "+p.Name+"｜武"+p.Stats.Martial+" 统"+p.Stats.Command+" 军"+p.Stats.Military+"｜生命 "+p.Stats.Life+"/"+p.Stats.MaxLife+"｜战场评分 "+Math.Round(w.GeneralCandidateScore(p)));}
            if(capital!=null){sb.AppendLine();sb.AppendLine("【都城官员候选】");List<Person> oc=w.RankedOfficialCandidates(capital);for(int i=0;i<Math.Min(6,oc.Count);i++)sb.AppendLine((i+1)+". "+oc[i].Name+"｜智"+oc[i].Stats.Intelligence+" 统"+oc[i].Stats.Command+"｜"+Math.Round(w.OfficialCandidateScore(oc[i])));sb.AppendLine("【都城将军候选】");List<Person> gc=w.RankedGeneralCandidates(capital);for(int i=0;i<Math.Min(6,gc.Count);i++)sb.AppendLine((i+1)+". "+gc[i].Name+"｜武"+gc[i].Stats.Martial+" 统"+gc[i].Stats.Command+"｜"+Math.Round(w.GeneralCandidateScore(gc[i])));}
            sb.AppendLine();sb.AppendLine("说明：官员和将军分榜、不混排。官员管战役规划，将军管战场执行；单项优势都有限，不会因为少一个顶级人才就快速崩盘。");
        }

        void BuildCity(StringBuilder sb,GameWorld w,Kingdom player,City capital){
            if(capital==null){sb.AppendLine("暂无都城");return;}sb.AppendLine("【"+capital.Name+" 城市规划】");sb.AppendLine("人口容量 "+capital.PopulationCapacity+"｜市场岗位 "+capital.MarketJobs+"｜农务 "+capital.FarmJobs+"｜伐木 "+capital.LoggingJobs+"｜工坊 "+capital.WorkshopJobs+"｜建造 "+capital.BuildJobs);int wall=0,gate=0,tower=0;var counts=new Dictionary<BuildingKind,int>();foreach(string id in capital.BuildingIds){Building b;if(!w.Buildings.TryGetValue(id,out b)||b==null||b.Ruined)continue;counts[b.Kind]=counts.ContainsKey(b.Kind)?counts[b.Kind]+1:1;if(b.Kind==BuildingKind.Wall)wall++;else if(b.Kind==BuildingKind.Gate)gate++;else if(b.Kind==BuildingKind.Tower)tower++;}sb.AppendLine("外郭半径 "+capital.FortificationRadius+"｜城墙段 "+wall+"｜城门 "+gate+"｜塔楼 "+tower);sb.AppendLine();sb.AppendLine("【建筑】");foreach(var kv in counts)sb.AppendLine("• "+ChineseText.Building(kv.Key)+" × "+kv.Value);sb.AppendLine();sb.AppendLine("【地块分区】");foreach(CityZone z in capital.Zones)if(z!=null)sb.AppendLine("• "+z.Name+"｜中心("+z.CenterX+","+z.CenterY+")｜半径"+z.Radius+"｜建筑"+z.BuildingCount);sb.AppendLine();sb.AppendLine("规划原则：农田/伐木在外围，仓库接生产区，住宅靠商业，工坊避开密集住宅，军营训练场靠近城门；小城2门、中城3门、大城4门。");
        }

        void BuildWar(StringBuilder sb,GameWorld w,Kingdom player){
            if(player==null){sb.AppendLine("暂无玩家国家");return;}sb.AppendLine("【战争与战役规划】");bool any=false;foreach(Kingdom enemy in w.Kingdoms.Values){if(enemy.Id==player.Id)continue;var rel=w.Diplomacy.Get(player.Id,enemy.Id);if(rel.State!=DiplomacyState.War)continue;any=true;var wa=w.WarAdministrationFor(player.Id,enemy.Id);if(wa==null){sb.AppendLine("• 对 "+enemy.Name+"｜未指派战役官员｜只损失轻微协调优势，不会直接判输");continue;}Person o=null;w.People.TryGetValue(wa.OfficialId,out o);var pf=w.WarAdministration.Profile(wa);sb.AppendLine("• 对 "+enemy.Name+"｜战役官员 "+(o==null?"失效":o.Name)+"｜重点 "+ChineseText.WarFocus(wa.Focus));if(o!=null)sb.AppendLine("  智"+o.Stats.Intelligence+" 统"+o.Stats.Command+" 组"+o.Stats.Organization+" 后"+o.Stats.Logistics+"｜协调"+pf.Coordination.ToString("0.00")+" 野战"+pf.FieldBattle.ToString("0.00")+" 攻城"+pf.Siege.ToString("0.00")+" 后勤"+pf.Logistics.ToString("0.00"));sb.AppendLine("  最近战务："+wa.LastAction);}
            if(!any)sb.AppendLine("当前没有正式战争。AI会继续根据兵力、补给、城防和外交态势评估开战。");sb.AppendLine();sb.AppendLine("【战斗设计】枪克骑、骑扰远程、远程压重装都是软克制；枪+弓、步+骑、侦察+远射有小幅协同。普通士兵通常需多轮有效命中才失能，单场模拟上限约160秒。");
        }
    }
}