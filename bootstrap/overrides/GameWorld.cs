using System;
using System.Collections.Generic;
using LordWar.AI;
using LordWar.Combat;
using LordWar.Construction;
using LordWar.Data;
using LordWar.Diplomacy;
using LordWar.Economy;
using LordWar.Governance;
using LordWar.Military;
using LordWar.Performance;
using LordWar.Siege;
using LordWar.Society;
using LordWar.World;

namespace LordWar.Simulation {
    /// <summary>
    /// 《领主战争》唯一世界运行入口。所有系统通过这里持有唯一Owner，避免平行Manager抢执行权。
    /// </summary>
    public sealed class GameWorld {
        public int Seed, Day;
        public WorldMap Map;
        public readonly Dictionary<string,Person> People = new Dictionary<string,Person>();
        public readonly Dictionary<string,Family> Families = new Dictionary<string,Family>();
        public readonly Dictionary<string,City> Cities = new Dictionary<string,City>();
        public readonly Dictionary<string,Kingdom> Kingdoms = new Dictionary<string,Kingdom>();
        public readonly Dictionary<string,Army> Armies = new Dictionary<string,Army>();
        public readonly Dictionary<string,Building> Buildings = new Dictionary<string,Building>();
        public readonly Dictionary<string,EquipmentInstance> Equipment = new Dictionary<string,EquipmentInstance>();
        public readonly Dictionary<string,AnimalHerd> Herds = new Dictionary<string,AnimalHerd>();
        public readonly Dictionary<string,ConstructionProject> Projects = new Dictionary<string,ConstructionProject>();
        public readonly Dictionary<string,SupplyRoute> SupplyRoutes = new Dictionary<string,SupplyRoute>();
        public readonly Dictionary<string,SupplyConvoy> SupplyConvoys = new Dictionary<string,SupplyConvoy>();
        public readonly List<BattleReport> Battles = new List<BattleReport>();
        public readonly List<WorldEvent> Events = new List<WorldEvent>();
        public readonly Dictionary<string,BattleSession> ActiveBattles = new Dictionary<string,BattleSession>();

        public GameDataCatalog Data;
        public PopulationOwner Population;
        public EconomyLedger Economy;
        public ProposalSystem Proposals;
        public PolicyOwner Policies;
        public WarAdministrationOwner WarAdministration;
        public ConstructionOwner Construction;
        public CityPlanningOwner Planning;
        public FamilyOwner FamilySystem;
        public MerchantOwner Merchants;
        public PromotionOwner Promotions;
        public ArmyOwner Military;
        public EquipmentOwner Gear;
        public LogisticsOwner Logistics;
        public DiplomacyOwner Diplomacy;
        public WeatherOwner Weather;
        public AnimalOwner Animals;
        public CombatResolution Combat;
        public SiegeOwner Sieges;
        public MarchOwner March;
        public TickScheduler Scheduler = new TickScheduler();

        DeterministicRandom _rng;
        WorldGenerator _worldGenerator;
        readonly Dictionary<string,AiOwner> _ai = new Dictionary<string,AiOwner>();
        float _dayAccumulator; public float SecondsPerDay = 30f; public float TimeScale = 1f; public bool Paused;
        public float DayProgress01 { get { return Mathx.Clamp(_dayAccumulator / Math.Max(1f, SecondsPerDay), 0f, 1f); } }
        public float TimeIntoDaySeconds { get { return _dayAccumulator; } }
        public bool IsNightTime { get { float span=Math.Max(.01f,SecondsPerDay);float q=(_dayAccumulator/span+.25f)%1f;return q<.20f||q>.80f; } }
        public void RestoreClock(float seconds,float timeScale,bool paused){_dayAccumulator=Mathx.Clamp(seconds,0f,Math.Max(1f,SecondsPerDay)-.001f);TimeScale=Mathx.Clamp(timeScale,.1f,8f);Paused=paused;}

        public string PlayerKingdomId { get; private set; }
        public AiDifficulty ComputerDifficulty = AiDifficulty.Hard;

        public GameWorld(int seed, GameDataCatalog data, AiDifficulty computerDifficulty = AiDifficulty.Hard) {
            Seed = seed;
            Data = data;
            ComputerDifficulty = computerDifficulty;
            _rng = new DeterministicRandom(seed);
        }

        public void RecordEvent(string category,string title,string detail,string relatedId="") {
            WorldEvent e=new WorldEvent{Id=Ids.Next("EVENT"),Day=Day,Category=string.IsNullOrEmpty(category)?"大事":category,Title=string.IsNullOrEmpty(title)?"未名事件":title,Detail=detail??"",RelatedId=relatedId??""};Events.Add(e);while(Events.Count>300)Events.RemoveAt(0);
        }

        public void RebindAfterLoad(string playerKingdomId, IEnumerable<Proposal> proposals, IEnumerable<DiplomacyRelation> diplomacy, IEnumerable<MerchantInvestment> investments, IEnumerable<SiegeState> sieges, IEnumerable<SupplyRoute> supplyRoutes, IEnumerable<SupplyConvoy> supplyConvoys, IEnumerable<BattleSession> activeBattles=null, IEnumerable<WarAdministration> warAdministrations=null) {
            PlayerKingdomId = playerKingdomId;
            _rng = new DeterministicRandom(Seed ^ Day);
            _worldGenerator = new WorldGenerator(Seed);
            Population = new PopulationOwner(People, Cities, Seed);
            Economy = new EconomyLedger(People, Kingdoms);
            Proposals = new ProposalSystem(); Proposals.Restore(proposals);
            Construction = new ConstructionOwner(Projects, Buildings, People, Cities);
            Planning = new CityPlanningOwner(Map, Buildings, Seed + Day);
            foreach(City c in Cities.Values)Planning.EnsureStarterZones(c,Day);
            FamilySystem = new FamilyOwner(Families, People);
            Merchants = new MerchantOwner(People); Merchants.Restore(investments);
            Promotions = new PromotionOwner(People, Proposals);
            Military = new ArmyOwner(Armies, People, Population, Data);
            Gear = new EquipmentOwner(Equipment, People);
            Logistics = new LogisticsOwner(SupplyRoutes, SupplyConvoys, _worldGenerator, Map, Seed + Day); Logistics.Restore(supplyRoutes, supplyConvoys);
            Diplomacy = new DiplomacyOwner(); Diplomacy.Restore(diplomacy);
            Weather = new WeatherOwner(Seed); for (int i = 0; i < Day; i++) Weather.TickDay();
            Animals = new AnimalOwner(Herds, Map, Seed);
            Combat = new CombatResolution(People, Equipment, Seed + Day, Data);
            ActiveBattles.Clear();if(activeBattles!=null)foreach(BattleSession session in activeBattles)if(session!=null&&!session.Completed&&!string.IsNullOrEmpty(session.Id))ActiveBattles[session.Id]=session;
            Sieges = new SiegeOwner(); Sieges.Restore(sieges);
            March = new MarchOwner(_worldGenerator, Map);
            Policies = new PolicyOwner(Data, Proposals, Economy);
            WarAdministration = new WarAdministrationOwner(People, Kingdoms, Data); WarAdministration.Restore(warAdministrations);
            _ai.Clear(); int aiIndex = 0;
            foreach (Kingdom k in Kingdoms.Values) _ai[k.Id] = new AiOwner(k.Id == PlayerKingdomId ? AiDifficulty.Normal : ComputerDifficulty, Seed + aiIndex++ * 97);
            Scheduler = new TickScheduler();
            Scheduler.Register("施工", .25f, delegate(float dt) { TickConstruction(dt); });
            Scheduler.Register("治理", 1f, delegate(float dt) { TickGovernance(dt); });
            Scheduler.Register("军队行军", .25f, delegate(float dt) { TickArmyMovement(dt); });
            Scheduler.Register("持续战斗", .25f, 3, delegate(float dt) { TickActiveBattles(dt); });
            RecordEvent("世界","读档恢复","Seed "+Seed+"，地图 "+(Map==null?0:Map.Width)+"×"+(Map==null?0:Map.Height)+"，国家 "+Kingdoms.Count+"，电脑难度 "+ChineseText.Difficulty(ComputerDifficulty));
        }

        public void CreateNewWorld(int width, int height, int kingdomCount) { CreateNewWorld(width, height, kingdomCount, null); }

        public void CreateNewWorld(int width, int height, int kingdomCount, WorldGenerationOptions options) {
            _worldGenerator = new WorldGenerator(Seed, options);
            Map = _worldGenerator.Generate(width, height, kingdomCount);

            Population = new PopulationOwner(People, Cities, Seed);
            Economy = new EconomyLedger(People, Kingdoms);
            Proposals = new ProposalSystem();
            Construction = new ConstructionOwner(Projects, Buildings, People, Cities);
            Planning = new CityPlanningOwner(Map, Buildings, Seed);
            FamilySystem = new FamilyOwner(Families, People);
            Merchants = new MerchantOwner(People);
            Promotions = new PromotionOwner(People, Proposals);
            Military = new ArmyOwner(Armies, People, Population, Data);
            Gear = new EquipmentOwner(Equipment, People);
            SupplyRoutes.Clear(); SupplyConvoys.Clear();
            Logistics = new LogisticsOwner(SupplyRoutes, SupplyConvoys, _worldGenerator, Map, Seed);
            Diplomacy = new DiplomacyOwner();
            Weather = new WeatherOwner(Seed);
            Animals = new AnimalOwner(Herds, Map, Seed); Animals.Seed(Math.Max(12, kingdomCount * 8));
            Combat = new CombatResolution(People, Equipment, Seed, Data);
            ActiveBattles.Clear();
            Sieges = new SiegeOwner();
            March = new MarchOwner(_worldGenerator, Map);
            Policies = new PolicyOwner(Data, Proposals, Economy);
            WarAdministration = new WarAdministrationOwner(People, Kingdoms, Data);

            int i = 0;
            foreach (GridPoint pt in Map.CitySites) {
                string cname = Data.CityNames.Count > 0 ? Data.CityNames[i % Data.CityNames.Count] : "新城" + (i + 1);
                City c = new City {
                    Id = Ids.Next("CITY"), Name = cname, KingdomId = "", X = pt.X, Y = pt.Y,
                    PopulationCapacity = 120, Food = 420, Wood = 180, Stone = 140, Iron = 80, Horses = 12,
                    FarmJobs = 30, MineJobs = 8, LoggingJobs = 8, WorkshopJobs = 5, MarketJobs = 8, BuildJobs = 6,
                    CultureId = SelectCulture(pt, i)
                };
                Cities[c.Id] = c;
                Planning.EnsureStarterZones(c,Day);

                Kingdom k = new Kingdom { Id = Ids.Next("K"), Name = cname + "领", Treasury = 1800, CapitalCityId = c.Id, ColorHex = PaletteHex(i) };
                Kingdoms[k.Id] = k;
                k.CityIds.Add(c.Id);
                c.KingdomId = k.Id;
                if (i == 0) PlayerKingdomId = k.Id;
                _ai[k.Id] = new AiOwner(i == 0 ? AiDifficulty.Normal : ComputerDifficulty, Seed + i * 97);

                SeedPopulation(c, k, i);
                SeedFamilies(c, i);
                SeedInitialGovernment(c, k);
                i++;
            }

            Scheduler.Register("施工", .25f, delegate(float dt) { TickConstruction(dt); });
            Scheduler.Register("治理", 1f, delegate(float dt) { TickGovernance(dt); });
            Scheduler.Register("军队行军", .25f, delegate(float dt) { TickArmyMovement(dt); });
            Scheduler.Register("持续战斗", .25f, 3, delegate(float dt) { TickActiveBattles(dt); });
        }

        string PaletteHex(int i){string[] p={"#A63B32","#365E8D","#467A4A","#84643F","#6B4C86","#2C7772","#8B7135","#7D3F52"};return p[Math.Abs(i)%p.Length];}
        string SelectCulture(GridPoint pt,int index){if(Data.CityCultures.Count==0)return "平原王领";WorldTile t=Map.Get(pt.X,pt.Y);bool river=t!=null&&t.River;foreach(WorldTile n in Map.Neighbors4(pt.X,pt.Y))if(n.River||n.Terrain==TerrainKind.Coast)river=true;if(t!=null){if(t.Terrain==TerrainKind.Hill||t.Height>.58f)return FindCulture("山岭堡城");if(t.Terrain==TerrainKind.Forest)return FindCulture("森林猎镇");if(river)return FindCulture("河谷商都");if(t.Temperature<.3f)return FindCulture("寒原边塞");if(t.Moisture<.3f)return FindCulture("草原牧城");}return Data.CityCultures[index%Data.CityCultures.Count].Name;}
        string FindCulture(string name){foreach(CityCultureDef c in Data.CityCultures)if(c.Name==name)return c.Name;return Data.CityCultures[0].Name;}

        void SeedPopulation(City c, Kingdom k, int index) {
            int n = _rng.Range(45, 71);
            for (int i = 0; i < n; i++) {
                SocialClass cls = SocialClass.Commoner;
                if (i < 5) cls = SocialClass.Noble;
                else if (i < 8) cls = SocialClass.Merchant;

                string name;
                if (cls == SocialClass.Noble && Data.OfficialNames.Count > 0) name = Data.OfficialNames[(index * 11 + i) % Data.OfficialNames.Count];
                else if (Data.SoldierNames.Count > 0) name = Data.SoldierNames[(index * 71 + i) % Data.SoldierNames.Count];
                else name = "百姓" + i;

                Person p = Population.CreatePerson(c, name, cls, _rng.Range(17, 55));
                if (Data.PersonTraitIds.Count > 0) p.TraitIds.Add(Data.PersonTraitIds[_rng.Range(0, Data.PersonTraitIds.Count)]);
                if (cls == SocialClass.Noble) { p.Noble = true; if (Data.GeneralTraitIds.Count > 0) p.TraitIds.Add(Data.GeneralTraitIds[_rng.Range(0, Data.GeneralTraitIds.Count)]); }
                if (cls == SocialClass.Merchant) { p.Merchant = true; p.Wealth = _rng.Range(350, 1200); }

                if (i == 0) {
                    p.Class = SocialClass.Lord; p.Noble = true; p.Stats.Prestige = 45; k.LordId = p.Id;
                } else if (i == 1) {
                    // 世界生成时的初始留守官属于国家初始配置，不绕过后续任命申请链。
                    p.Class = SocialClass.Official; p.Job = JobKind.Official; p.Noble = true;
                    p.Stats.Administration = 62; p.Stats.Engineering = 58; p.Stats.Logistics = 60;
                    AssignEligibleOfficialTrait(p);
                }
            }
            Population.AssignJobs(c);
        }

        void SeedFamilies(City c, int index) {
            List<Person> nobles = new List<Person>(), merchants = new List<Person>(), commoners = new List<Person>();
            foreach (string id in c.PersonIds) { Person p; if (!People.TryGetValue(id, out p)) continue; if (p.Noble) nobles.Add(p); else if (p.Merchant) merchants.Add(p); else if (commoners.Count < 8) commoners.Add(p); }
            int count = Math.Min(3, nobles.Count);
            for (int i = 0; i < count; i++) {
                string fname = NextFamilyName(index * 7 + i); FamilyTraditionDef tr = Data.FamilyTraditions.Count > 0 ? Data.FamilyTraditions[(index * 3 + i) % Data.FamilyTraditions.Count] : null;
                string color = PaletteHex(index * 3 + i + 1), tradition = tr == null ? "守土传家" : tr.Name; string type=i==0?"王族":(i==1?"大贵族":"地方贵族");
                Family f = FamilySystem.Create(fname, type, c.Id, color, tradition); f.CrestKey=(tr==null?"山河":tr.Type)+"_"+f.Id; c.FamilyIds.Add(f.Id); FamilySystem.AddMember(f, nobles[i]);
            }
            if(merchants.Count>0){Family f=FamilySystem.Create(NextFamilyName(index*7+4),"商人豪族",c.Id,PaletteHex(index*5+4),"商贸传家");f.PrimaryIndustry="商贸";f.Wealth=merchants[0].Wealth+500;f.CrestKey="商印_"+f.Id;c.FamilyIds.Add(f.Id);foreach(Person m in merchants)FamilySystem.AddMember(f,m);}
            if(commoners.Count>0){Family f=FamilySystem.Create(NextFamilyName(index*7+5),"平民家族",c.Id,PaletteHex(index*5+5),"勤耕守业");f.PrimaryIndustry="农工";f.Wealth=80;f.Prestige=5;f.Influence=4;f.CrestKey="乡纹_"+f.Id;c.FamilyIds.Add(f.Id);for(int i=0;i<Math.Min(5,commoners.Count);i++)FamilySystem.AddMember(f,commoners[i]);}
        }
        string NextFamilyName(int index){return Data.FamilyNames.Count>0?Data.FamilyNames[Math.Abs(index)%Data.FamilyNames.Count]:"新兴氏族";}
        Family EnsureFounderFamily(Person founder,string familyType){if(founder==null)return null;Family existing;if(!string.IsNullOrEmpty(founder.FamilyId)&&Families.TryGetValue(founder.FamilyId,out existing)){if(existing.FamilyType=="平民家族"||existing.FamilyType=="商人豪族")existing.FamilyType=familyType;existing.Prestige+=10;existing.Influence+=6;return existing;}City c;if(!Cities.TryGetValue(founder.CityId,out c))return null;Family f=FamilySystem.Create(NextFamilyName((int)(Ids.Current%Math.Max(1,Data.FamilyNames.Count))),familyType,c.Id,PaletteHex((int)(Ids.Current%8)),familyType=="军功家族"?"军功传家":"新晋门第");f.CrestKey=(familyType=="军功家族"?"军旗":"新爵")+"_"+f.Id;c.FamilyIds.Add(f.Id);FamilySystem.AddMember(f,founder);return f;}

        void SeedInitialGovernment(City c, Kingdom k) {
            Person official = FindOfficial(c);
            if (official == null) return;
            SubmitConstructionProposal(c, official, BuildingKind.Market, "修建市集", 70, 180);
            SubmitConstructionProposal(c, official, BuildingKind.Granary, "修建粮仓", 60, 150);
            SubmitConstructionProposal(c, official, BuildingKind.Barracks, "修建军营", 90, 260);

            foreach (PolicyDef d in Data.Policies.Values) {
                if (d.Ability <= official.Stats.Administration && d.Category != null && d.Category.IndexOf("农业") >= 0) {
                    Policies.Propose(d.Id, official, k, Day);
                    break;
                }
            }
        }

        public bool TryRecruitSpecial(string armyId,string specialId,out string reason) {
            reason=""; Army army; SpecialUnitDef u; if(!Armies.TryGetValue(armyId,out army)){reason="军队不存在";return false;}if(!Data.SpecialUnits.TryGetValue(specialId,out u)){reason="特殊兵种不存在";return false;}
            Person general; if(!People.TryGetValue(army.GeneralId,out general)){reason="缺少主将";return false;}City city;if(!Cities.TryGetValue(general.CityId,out city)){reason="缺少所属城市";return false;}Kingdom k=Kingdoms[army.KingdomId];
            int national=0;foreach(Army a in Armies.Values)if(a.KingdomId==army.KingdomId)national+=Military.UnitCount(a,specialId);if(u.NationalCap>0&&national>=u.NationalCap){reason="已达到全国兵额上限";return false;}
            var ctx=new UnlockContext{CityStyle=city.CultureId,BarracksLevel=HighestBuildingLevel(city,BuildingKind.Barracks),Population=Population.CivilianPopulation(city),Horses=city.Horses,ArmyGold=army.Finance.Gold,IronStock=city.Iron,WoodStock=city.Wood,AvailableGearQuality=AvailableMilitaryGearQuality(city,army),HasStable=HasBuilding(city,BuildingKind.Stable),HasArmory=HasBuilding(city,BuildingKind.Armory)||HasBuilding(city,BuildingKind.Smithy),HasTrainingGround=HasBuilding(city,BuildingKind.TrainingGround),HasDefenseCamp=HasBuilding(city,BuildingKind.Barracks)&&HasBuilding(city,BuildingKind.Wall),PolicyApproved=HasMilitaryPolicy(k),ActivePolicyText=ActivePolicyText(k),MatchingBattles=general.Battles,SkillTier=HighestCommanderSkillTier(general),GeneralTraitText=GeneralTraitNames(general),GeneralSkillText=GeneralSkillNames(general),FamilyTradition=GeneralFamilyHasTradition(general),DoctrineAuthorized=HasApprovedOfficialDoctrine(city,k),GeneralUnlockSatisfied=MatchesGeneralSpecialUnlock(u,general,city),SpecialTraitAuthorized=GeneralTraitAuthorizesSpecialUnit(u,general)};
            if(!UnlockResolver.CanRecruit(u,ctx,out reason))return false;CityCultureProfile recruitCulture=CityCultureEffectEngine.Build(city);int target=Math.Max(1,(int)Math.Round((u.SquadCap<=0?12:u.SquadCap)*recruitCulture.Recruitment));if(u.NationalCap>0)target=Math.Min(target,u.NationalCap-national);bool cavalry=(u.Category??"").IndexOf("骑")>=0;if(cavalry)target=Math.Min(target,city.Horses);int cost=Math.Max(8,(int)Math.Round(8f*Math.Max(1f,u.Upkeep)));int n=Military.Recruit(army,city,u.Id,target,cost);if(n<=0){reason="人口、军马或军资不足";return false;}PrepareArmyPersonnelAndEquipment(army,city,GenericGearForSpecial(u,city),GearQualityFloor(u.Gear));return true;
        }

        public Proposal SubmitConstructionProposal(City city, Person official, BuildingKind kind, string title, int seconds, int cost) {
            if (city == null || official == null || official.Class != SocialClass.Official) return null;
            int required = kind == BuildingKind.Wall || kind == BuildingKind.Bridge ? 55 : 35;
            int ability = Math.Max(official.Stats.Engineering, official.Stats.Administration);
            Proposal p = new Proposal {
                Id = Ids.Next("CONREQ"), Title = title, Description = official.Name + "奏请：" + title,
                Kind = ProposalKind.Construction, ProposerId = official.Id, TargetId = city.Id, DataId = kind.ToString(),
                RequiredAbility = required, CostGold = cost, DurationDays = 1, SubmittedDay = Day
            };
            p.SetEffect("seconds", seconds);
            GridPoint plannedSite=Planning==null?new GridPoint(city.X,city.Y):Planning.FindBuildSite(city,kind);
            p.SetEffect("x", plannedSite.X);
            p.SetEffect("y", plannedSite.Y);
            return Proposals.Submit(p, ability) ? p : null;
        }

        public bool ApproveProposal(string id) {
            Proposal p = FindProposal(id);
            if (p == null || p.State != ProposalState.Pending) return false;
            // Construction must remain pending until the target and treasury are valid.
            // This avoids marking an invalid request approved and charging gold first.
            if (p.Kind == ProposalKind.Construction && !CanStartConstruction(p)) return false;
            if (!Proposals.Approve(id)) return false;
            return ExecuteApprovedProposal(p);
        }

        bool CanStartConstruction(Proposal p) {
            City city; Kingdom kingdom;
            if (p == null || !Cities.TryGetValue(p.TargetId, out city) ||
                !Kingdoms.TryGetValue(city.KingdomId, out kingdom) || kingdom.Treasury < p.CostGold || p.CostGold < 0)
                return false;
            if ((p.DataId ?? "").StartsWith("repair:", StringComparison.Ordinal)) {
                Building target;
                return Buildings.TryGetValue(p.DataId.Substring(7), out target) &&
                    target.CityId == city.Id && target.MaxDurability > target.Durability;
            }
            BuildingKind kind;
            if (!Enum.TryParse<BuildingKind>(p.DataId, out kind) || !Enum.IsDefined(typeof(BuildingKind), kind)) return false;
            int x = (int)p.GetEffect("x", city.X), y = (int)p.GetEffect("y", city.Y);
            WorldTile tile = Map == null ? null : Map.Get(x, y);
            return tile != null && tile.Terrain != TerrainKind.DeepWater && tile.Terrain != TerrainKind.Lake &&
                tile.Terrain != TerrainKind.Mountain;
        }

        public bool RejectProposal(string id) {
            Proposal p = FindProposal(id); if (p == null) return false;
            bool ok = Proposals.Reject(id); if (!ok) return false;
            ApplyRejectionPoliticalCost(p);
            if (p.Kind == ProposalKind.Promotion && p.DataId == "general_independent") { Person candidate; if (People.TryGetValue(p.TargetId, out candidate)) MakeSeniorDeputy(candidate); }
            return true;
        }

        Proposal FindProposal(string id) { foreach (Proposal p in Proposals.Queue) if (p.Id == id) return p; return null; }
        public bool IsPlayerProposal(Proposal p) { if (p == null) return false; if (p.Kind == ProposalKind.Diplomacy && p.TargetId == PlayerKingdomId) return true; return ProposalKingdom(p) == PlayerKingdomId; }
        public int PlayerPendingProposalCount { get { int n = 0; foreach (Proposal p in Proposals.Queue) if (p.State == ProposalState.Pending && IsPlayerProposal(p)) n++; return n; } }

        bool ExecuteApprovedProposal(Proposal p) {
            if (p.Kind == ProposalKind.Construction) {
                if (!CanStartConstruction(p)) { p.State = ProposalState.Pending; return false; }
                City c = Cities[p.TargetId]; Kingdom k = Kingdoms[c.KingdomId];
                int seconds = (int)p.GetEffect("seconds", 60); ConstructionProject cp;
                int committedWood=0, committedStone=0;
                if ((p.DataId??"").StartsWith("repair:",StringComparison.Ordinal)) {
                    Building b=Buildings[p.DataId.Substring(7)];
                    committedWood=Math.Min(c.Wood,Math.Max(1,(b.MaxDurability-b.Durability)/12));
                    committedStone=Math.Min(c.Stone,Math.Max(1,(b.MaxDurability-b.Durability)/18));
                    cp=Construction.StartRepair(c,p,b,seconds);
                } else {
                    BuildingKind kind=(BuildingKind)Enum.Parse(typeof(BuildingKind),p.DataId);
                    int x=(int)p.GetEffect("x",c.X), y=(int)p.GetEffect("y",c.Y);
                    cp=Construction.Start(c,p,kind,x,y,seconds);
                }
                if(cp==null) { p.State=ProposalState.Pending; return false; }
                if (!Economy.Pay(k,p.CostGold)) { Projects.Remove(cp.Id); p.State=ProposalState.Pending; return false; }
                c.Wood-=committedWood; c.Stone-=committedStone;
                Construction.AssignWorkers(cp,c,4);
                Proposals.MarkRunning(p, Day);
                return true;
            }
            if (p.Kind == ProposalKind.Policy) {
                Person proposer; if (!People.TryGetValue(p.ProposerId, out proposer)) { p.State = ProposalState.Failed; return false; }
                Kingdom k; if (!Kingdoms.TryGetValue(proposer.KingdomId, out k)) { p.State = ProposalState.Failed; return false; }
                return Policies.StartApproved(p, k, Day);
            }
            if (p.Kind == ProposalKind.Appointment && p.DataId == "official") {
                Person candidate;if(!People.TryGetValue(p.TargetId,out candidate)||!candidate.Alive||!candidate.Noble||candidate.Merchant){p.State=ProposalState.Failed;return false;}if(!AppointmentAccepted(candidate)){p.State=ProposalState.Suspended;RecordEvent("人事","拒绝任命",candidate.Name+"因对领主威望与政治前景缺乏信心，暂不接受官职",candidate.Id);return false;}candidate.Class=SocialClass.Official;candidate.Job=JobKind.Official;candidate.Stats.Administration=Math.Max(candidate.Stats.Administration,45);candidate.Stats.Organization=Math.Max(candidate.Stats.Organization,40);AssignEligibleOfficialTrait(candidate);p.State=ProposalState.Completed;return true;
            }
            if (p.Kind == ProposalKind.Appointment && p.DataId == "general") {
                Person candidate; City c;
                if (!People.TryGetValue(p.TargetId, out candidate) || !Cities.TryGetValue(candidate.CityId, out c)) { p.State = ProposalState.Failed; return false; }
                if(!AppointmentAccepted(candidate)){p.State=ProposalState.Suspended;RecordEvent("人事","拒绝任命",candidate.Name+"因对领主威望与政治前景缺乏信心，暂不接受将军任命",candidate.Id);return false;}
                if (!HasBuilding(c, BuildingKind.Barracks) || !Military.CanBeGeneral(candidate, true)) { p.State = ProposalState.Suspended; return false; }
                Kingdom k = Kingdoms[c.KingdomId];if(FieldArmies(k.Id).Count>=KingdomArmySoftCap(k)){p.State=ProposalState.Suspended;return false;}
                string gname = Data.GeneralNames.Count > 0 ? Data.GeneralNames[_rng.Range(0, Data.GeneralNames.Count)] : candidate.Name;
                candidate.Name = gname;
                EnsureGeneralIdentity(candidate);
                Army a = Military.CreateArmy(k, c, candidate, gname + "军");
                if (a != null) a.FormedDay = Day;
                if (a == null) { p.State = ProposalState.Failed; return false; }
                int grant = Math.Min(500, k.Treasury);
                k.Treasury -= grant; a.Finance.Gold += grant;
                Military.Recruit(a, c, FirstBasicUnitId(), 20, 8);
                PrepareArmyPersonnelAndEquipment(a, c);
                p.State = ProposalState.Completed;
                return true;
            }
            if (p.Kind == ProposalKind.Diplomacy) {
                string[] bits=(p.DataId??"").Split(':'); if(bits.Length<2){p.State=ProposalState.Failed;return false;}string action=bits[0],other=bits[1];Person proposer;string own=People.TryGetValue(p.ProposerId,out proposer)?proposer.KingdomId:PlayerKingdomId;bool ok=false;if(action=="truce")ok=Diplomacy.MakeTruce(own,other,Day,30);else if(action=="alliance")ok=Diplomacy.Alliance(own,other);else if(action=="trade")ok=Diplomacy.Trade(own,other);else if(action=="nonaggression")ok=Diplomacy.NonAggression(own,other,Day,45);p.State=ok?ProposalState.Completed:ProposalState.Failed;if(ok){Kingdom ownK,otherK;Kingdoms.TryGetValue(own,out ownK);Kingdoms.TryGetValue(other,out otherK);string act=action=="truce"?"停战":(action=="alliance"?"结盟":(action=="trade"?"通商":"互不侵犯"));if(action=="truce")EndWarAdministration(own,other,"停战后战争负责官员自动解除战务");RecordEvent("外交",act,(ownK==null?"本国":ownK.Name)+"与"+(otherK==null?"他国":otherK.Name)+"达成"+act,other);}return ok;
            }
            if (p.Kind == ProposalKind.Promotion) {
                Person candidate; if (!People.TryGetValue(p.TargetId, out candidate)) { p.State = ProposalState.Failed; return false; }
                if (p.DataId == "officer") {
                    if (!Promotions.ApproveOfficer(candidate)) { p.State = ProposalState.Failed; return false; }
                    Army home = ArmyContaining(candidate.Id); if (home != null) AssignOfficerToSquad(home, candidate); p.State = ProposalState.Completed; return true;
                }
                if (p.DataId == "noble") { bool ok = Population.PromoteToNoble(candidate, "军功授爵");if(ok)EnsureFounderFamily(candidate,"军功家族"); p.State = ok ? ProposalState.Completed : ProposalState.Failed; return ok; }
                if (p.DataId == "merchant_noble") {if(!candidate.Merchant||candidate.Wealth<1400){p.State=ProposalState.Failed;return false;}candidate.Merchant=false;candidate.Noble=true;candidate.Class=SocialClass.Noble;candidate.Job=JobKind.Trader;candidate.Promotions++;candidate.Stats.Prestige+=15;EnsureFounderFamily(candidate,"商人豪族");p.State=ProposalState.Completed;return true;}
                if (p.DataId == "civil_noble") {bool ok=Population.PromoteToNoble(candidate,string.IsNullOrEmpty(candidate.LastMeritReason)?"国家功勋授爵":candidate.LastMeritReason+"授爵");if(ok)EnsureFounderFamily(candidate,"新晋贵族");p.State=ok?ProposalState.Completed:ProposalState.Failed;return ok;}
                if (p.DataId == "general_independent") {
                    if (!candidate.Noble || candidate.Class != SocialClass.Officer) { p.State = ProposalState.Failed; return false; }
                    City c; if (!Cities.TryGetValue(candidate.CityId, out c) || !HasBuilding(c, BuildingKind.Barracks)) { p.State = ProposalState.Suspended; return false; }
                    Kingdom k = Kingdoms[c.KingdomId]; EnsureGeneralIdentity(candidate); Army existing = ArmyContaining(candidate.Id); if (existing != null) RemoveFromArmy(existing, candidate.Id);
                    Army newArmy = Military.CreateArmy(k,c,candidate,candidate.Name+"军"); if (newArmy == null) { p.State = ProposalState.Failed; return false; } newArmy.FormedDay=Day; int grant=Math.Min(350,k.Treasury);k.Treasury-=grant;newArmy.Finance.Gold+=grant;Military.Recruit(newArmy,c,FirstBasicUnitId(),12,8);PrepareArmyPersonnelAndEquipment(newArmy,c);p.State=ProposalState.Completed;return true;
                }
            }
            if (p.Kind == ProposalKind.MerchantInvestment) {
                Person merchant; Army army;
                if (!People.TryGetValue(p.ProposerId, out merchant) || !Armies.TryGetValue(p.TargetId, out army)) { p.State = ProposalState.Failed; return false; }
                Kingdom investmentKingdom;PolicyRuntimeProfile investmentPolicy=Kingdoms.TryGetValue(army.KingdomId,out investmentKingdom)?Policies.RuntimeProfile(investmentKingdom):new PolicyRuntimeProfile();int maxInvestors=Mathx.Clamp((int)Math.Round(2f*investmentPolicy.MerchantInvestmentCapacity),1,4);MerchantInvestment inv = Merchants.Activate(merchant, army, p, Day,maxInvestors);
                p.State = inv == null ? ProposalState.Failed : ProposalState.Completed;
                return inv != null;
            }
            return true;
        }

        void AssignEligibleOfficialTrait(Person p) {
            if (p == null || Data.OfficialTraitIds.Count == 0) return;
            for (int n = 0; n < Data.OfficialTraitIds.Count; n++) { string id = Data.OfficialTraitIds[(_rng.Range(0, Data.OfficialTraitIds.Count) + n) % Data.OfficialTraitIds.Count]; TraitDef d = Data.OfficialTraits[id]; if (p.Stats.Intelligence >= d.MinIntelligence && p.Stats.Administration >= d.MinAdministration) { if (!p.TraitIds.Contains(id)) p.TraitIds.Add(id); return; } }
        }

        int SkillStageRank(string stage){if(stage=="名将")return 3;if(stage=="精熟")return 2;return 1;}
        int HighestCommanderSkillTier(Person p){int best=1;if(p==null)return best;foreach(string id in p.CommanderSkillIds){SkillDef d;if(Data.Skills.TryGetValue(id,out d)&&d!=null)best=Math.Max(best,SkillStageRank(d.Stage));}return best;}
        void GrantCommanderSkill(Person p){if(p==null||Data.SkillIds.Count==0)return;var candidates=new List<string>();foreach(string id in Data.SkillIds){SkillDef d;if(!Data.Skills.TryGetValue(id,out d)||d==null||d.Stage!="初学")continue;bool same=false;foreach(string owned in p.CommanderSkillIds){SkillDef od;if(Data.Skills.TryGetValue(owned,out od)&&od!=null&&od.Name==d.Name){same=true;break;}}if(!same)candidates.Add(id);}if(candidates.Count>0)p.CommanderSkillIds.Add(candidates[_rng.Range(0,candidates.Count)]);}
        void ProgressCommanderSkills(Person p){if(p==null||p.Class!=SocialClass.General)return;int target=p.Battles>=10?3:(p.Battles>=3?2:1);for(int i=0;i<p.CommanderSkillIds.Count;i++){SkillDef cur;if(!Data.Skills.TryGetValue(p.CommanderSkillIds[i],out cur)||cur==null||SkillStageRank(cur.Stage)>=target)continue;string replacement="";foreach(string id in Data.SkillIds){SkillDef d;if(Data.Skills.TryGetValue(id,out d)&&d!=null&&d.Name==cur.Name&&SkillStageRank(d.Stage)==target){replacement=id;break;}}if(!string.IsNullOrEmpty(replacement))p.CommanderSkillIds[i]=replacement;}int familyCap=p.Battles>=16?4:(p.Battles>=7?3:2);if(p.CommanderSkillIds.Count<familyCap&&p.Experience>=30)GrantCommanderSkill(p);}
        void EnsureGeneralIdentity(Person p) {
            if (p == null) return;
            bool hasGeneralTrait=false;foreach(string id in p.TraitIds)if(Data.GeneralTraits.ContainsKey(id)){hasGeneralTrait=true;break;}
            if(!hasGeneralTrait&&Data.GeneralTraitIds.Count>0)p.TraitIds.Add(Data.GeneralTraitIds[_rng.Range(0,Data.GeneralTraitIds.Count)]);
            if(p.CommanderSkillIds.Count==0)GrantCommanderSkill(p);if(p.Stats.Command>=55&&p.CommanderSkillIds.Count<2)GrantCommanderSkill(p);
            int generalLife=120+Mathx.Clamp(p.Stats.Martial/5,6,20);p.Stats.MaxLife=Math.Max(p.Stats.MaxLife,generalLife);p.Stats.Life=Math.Max(p.Stats.Life,Math.Min(p.Stats.MaxLife,generalLife));
        }

        void PrepareArmyPersonnelAndEquipment(Army army, City city, string overrideGear = null, int overrideMinimumQuality = 0) {
            if (army == null || city == null) return;
            foreach (Squad sq in army.Squads) {
                UnitDef unit; Data.Units.TryGetValue(sq.UnitTemplateId, out unit);
                foreach (string sid in sq.SoldierIds) {
                    Person soldier; if (!People.TryGetValue(sid, out soldier)) continue;
                    bool hasSoldierTrait = false; foreach (string tid in soldier.TraitIds) if (Data.SoldierTraits.ContainsKey(tid)) { hasSoldierTrait = true; break; }
                    if (!hasSoldierTrait && Data.SoldierTraitIds.Count > 0) soldier.TraitIds.Add(Data.SoldierTraitIds[_rng.Range(0, Data.SoldierTraitIds.Count)]);
                    if (soldier.EquipmentIds.Count == 0) {
                        string rawGear = !string.IsNullOrEmpty(overrideGear) ? overrideGear : (unit == null ? "" : unit.Gear);
                        int minimumQuality=Math.Max(overrideMinimumQuality,GearQualityFloor(rawGear));
                        string gear;
                        if (!string.IsNullOrEmpty(overrideGear)) gear = overrideGear;
                        else gear = UnitLoadoutResolver.Join(UnitLoadoutResolver.ResolveBasic(unit,city));
                        ProvisionEquipment(soldier, army, city, gear, minimumQuality);
                    }
                }
            }
        }

        void ProvisionEquipment(Person soldier, Army army, City city, string gearText, int minimumQuality = 0) {
            if (soldier == null || army == null || city == null || string.IsNullOrEmpty(gearText)) return;
            string[] pieces = gearText.Replace("＋", "+").Split('+');
            foreach (string raw in pieces) {
                string token = raw.Trim(); if (token.Length == 0) continue; int slash = token.IndexOf('/'); if (slash > 0) token = token.Substring(0, slash).Trim();
                EquipmentTypeDef type = Data.FindEquipmentType(token); if (type == null) continue;
                bool metal = type.Material != null && (type.Material.IndexOf("铁") >= 0 || type.Material.IndexOf("铜") >= 0 || type.Material.IndexOf("钢") >= 0);
                bool wood = type.Material != null && (type.Material.IndexOf("木") >= 0 || type.Name.IndexOf("弓") >= 0 || type.Name.IndexOf("枪") >= 0 || type.Name.IndexOf("矛") >= 0 || type.Name.IndexOf("盾") >= 0);
                Kingdom policyKingdom;PolicyRuntimeProfile policyProfile=Kingdoms.TryGetValue(army.KingdomId,out policyKingdom)?Policies.RuntimeProfile(policyKingdom):new PolicyRuntimeProfile();int targetLevel = Math.Max(minimumQuality, soldier.Class == SocialClass.Officer ? 3 : (soldier.Battles >= 4 ? 3 : (army.Finance.Gold > 180 ? 2 : 1)));targetLevel=Math.Min(7,targetLevel+policyProfile.EquipmentQualityBonus);
                EquipmentQualityDef quality = EquipmentQuality(targetLevel); if (quality == null) continue;
                int craftCost = Math.Max(2, (int)Math.Round((6 + type.Attack + type.Defense + type.Durability * .04f) * quality.CostMultiplier));
                if ((metal && city.Iron <= 0) || (wood && city.Wood <= 0) || army.Finance.Gold < craftCost) continue; // 空槽是合法状态。
                if (metal) city.Iron--; if (wood) city.Wood--; army.Finance.Gold -= craftCost;
                EquipmentInstance e = Gear.CreateFromDefinition(type, quality, city.Id, "军械工坊", "军队配发");
                if (e != null) Gear.Equip(soldier, e, e.Slot);
            }
        }

        EquipmentQualityDef EquipmentQuality(int requestedLevel) {
            EquipmentQualityDef best = null;
            foreach (EquipmentQualityDef q in Data.EquipmentQualities) if (q.Level <= requestedLevel && (best == null || q.Level > best.Level)) best = q;
            return best;
        }
        int GearQualityFloor(string text){if(string.IsNullOrEmpty(text))return 0;if(text.IndexOf("传奇")>=0)return 7;if(text.IndexOf("家族名器")>=0)return 6;if(text.IndexOf("大师")>=0)return 5;if(text.IndexOf("精良")>=0)return 4;if(text.IndexOf("优良")>=0)return 3;if(text.IndexOf("制式")>=0)return 2;return 0;}

        int HighestBuildingLevel(City c,BuildingKind kind){int level=0;foreach(string id in c.BuildingIds){Building b;if(Buildings.TryGetValue(id,out b)&&b.Complete&&b.Kind==kind)level=Math.Max(level,b.Level);}return level;}
        string GeneralTraitNames(Person g){string s="";foreach(string id in g.TraitIds){TraitDef d;if(Data.GeneralTraits.TryGetValue(id,out d))s+=(s.Length==0?"":"、")+d.Name;}return s;}
        string GeneralSkillNames(Person g){string s="";if(g==null)return s;foreach(string id in g.CommanderSkillIds){SkillDef d;if(Data.Skills.TryGetValue(id,out d))s+=(s.Length==0?"":"、")+d.Name;}return s;}
        bool HasMilitaryPolicy(Kingdom k){if(k==null)return false;foreach(string id in k.ActivePolicyIds){PolicyDef d;if(Data.Policies.TryGetValue(id,out d)&&((d.Category??"").IndexOf("军")>=0||(d.Category??"").IndexOf("城防")>=0))return true;}return false;}
        string ActivePolicyText(Kingdom k){if(k==null)return "";string s="";foreach(string id in k.ActivePolicyIds){PolicyDef d;if(Data.Policies.TryGetValue(id,out d))s+=(s.Length==0?"":"；")+d.Name+" "+d.Category+" "+d.EffectText;}return s;}
        bool GeneralFamilyHasTradition(Person g){Family f;return g!=null&&!string.IsNullOrEmpty(g.FamilyId)&&Families.TryGetValue(g.FamilyId,out f)&&!string.IsNullOrEmpty(f.MilitaryTradition);}
        bool GeneralTraitAuthorizesSpecialUnit(SpecialUnitDef unit,Person general){
            if(unit==null||general==null)return false;bool requiresTrait=(unit.GeneralCondition??"").IndexOf("特性",StringComparison.Ordinal)>=0;if(!requiresTrait)return true;
            var categories=new HashSet<string>();string semantic=((unit.Category??"")+" "+(unit.Role??"")+" "+(unit.Behavior??"")+" "+(unit.Weakness??""));
            if(ContainsAnyText(semantic,"骑","马","侧翼","追击"))categories.Add("骑战");if(ContainsAnyText(semantic,"弓","弩","远程","射界","轮射"))categories.Add("远程");
            if(ContainsAnyText(semantic,"伏","奇袭","袭营","截断","影")){categories.Add("奇袭");categories.Add("侦察");}if(ContainsAnyText(semantic,"侦察","斥候","探路"))categories.Add("侦察");
            if(ContainsAnyText(semantic,"粮","辎重","运输","补给","工战"))categories.Add("后勤");if(ContainsAnyText(semantic,"守","盾","重甲","阵线","关键点","亲卫")){categories.Add("防守");categories.Add("治军");}
            if(ContainsAnyText(semantic,"突击","破坏","强攻","枪阵","槊","刀卫"))categories.Add("进攻");if(categories.Count==0){categories.Add("治军");categories.Add("决断");}
            bool hasDirectRule=false;foreach(TraitDef t in Data.GeneralTraits.Values)if(t!=null&&t.Unlock==unit.Name){hasDirectRule=true;break;}
            foreach(string tid in general.TraitIds){TraitDef t;if(!Data.GeneralTraits.TryGetValue(tid,out t)||t==null)continue;if(t.Unlock==unit.Name)return true;if(!hasDirectRule&&categories.Contains(t.Category))return true;}return false;
        }
        static bool ContainsAnyText(string text,params string[] words){if(string.IsNullOrEmpty(text))return false;for(int i=0;i<words.Length;i++)if(text.IndexOf(words[i],StringComparison.Ordinal)>=0)return true;return false;}
        int AvailableMilitaryGearQuality(City city,Army army){if(city==null||army==null)return 0;int q=1;if(HasBuilding(city,BuildingKind.Smithy))q++;if(HasBuilding(city,BuildingKind.Armory))q++;if(HasBuilding(city,BuildingKind.TrainingGround)&&city.Iron>=12)q++;if(army.Finance.Gold>=600&&city.Iron>=24)q++;return Math.Min(5,q);}
        bool MatchesGeneralSpecialUnlock(SpecialUnitDef unit,Person general,City city){
            if(unit==null||general==null||city==null)return false;GeneralUnlockDef requirement=null;foreach(GeneralUnlockDef x in Data.GeneralUnlocks)if(x.SpecialUnit==unit.Name){requirement=x;break;}if(requirement==null)return true;
            string traitText=GeneralTraitNames(general),skillText=GeneralSkillNames(general);if(!string.IsNullOrEmpty(requirement.GeneralTrait)&&traitText.IndexOf(requirement.GeneralTrait,StringComparison.Ordinal)<0)return false;if(!string.IsNullOrEmpty(requirement.RequiredSkill)&&skillText.IndexOf(requirement.RequiredSkill,StringComparison.Ordinal)<0)return false;
            string cond=requirement.CityCondition??"";if(cond.Length>0){string[] parts=cond.Replace("；",";").Split(';');foreach(string raw in parts){string x=raw.Trim();if(x.Length==0)continue;if(x.IndexOf("高级军营",StringComparison.Ordinal)>=0&&HighestBuildingLevel(city,BuildingKind.Barracks)<2)return false;if(x.IndexOf("军营",StringComparison.Ordinal)>=0&&x.IndexOf("高级军营",StringComparison.Ordinal)<0&&!HasBuilding(city,BuildingKind.Barracks))return false;if(x.IndexOf("军械库",StringComparison.Ordinal)>=0&&!HasBuilding(city,BuildingKind.Armory))return false;if(x.IndexOf("训练场",StringComparison.Ordinal)>=0&&!HasBuilding(city,BuildingKind.TrainingGround))return false;if(x.IndexOf("马厩",StringComparison.Ordinal)>=0&&!HasBuilding(city,BuildingKind.Stable))return false;if(x.IndexOf("工坊",StringComparison.Ordinal)>=0&&!HasBuilding(city,BuildingKind.Workshop))return false;if(x.IndexOf("城墙",StringComparison.Ordinal)>=0&&!HasBuilding(city,BuildingKind.Wall))return false;if(x.IndexOf("塔楼",StringComparison.Ordinal)>=0&&!HasBuilding(city,BuildingKind.Tower))return false;if(x.IndexOf("桥梁",StringComparison.Ordinal)>=0&&!HasBuilding(city,BuildingKind.Bridge))return false;if(x.IndexOf("道路",StringComparison.Ordinal)>=0&&!HasBuilding(city,BuildingKind.Road))return false;if(x.IndexOf("市场",StringComparison.Ordinal)>=0&&!HasBuilding(city,BuildingKind.Market))return false;if(x.IndexOf("官署",StringComparison.Ordinal)>=0&&!HasBuilding(city,BuildingKind.Government))return false;if(x.IndexOf("城",StringComparison.Ordinal)>=0&&x.IndexOf("军营",StringComparison.Ordinal)<0&&x.IndexOf("军械",StringComparison.Ordinal)<0&&x.IndexOf("训练",StringComparison.Ordinal)<0&&x.IndexOf("马厩",StringComparison.Ordinal)<0&&x.IndexOf("城墙",StringComparison.Ordinal)<0&&city.CultureId!=x)return false;}}
            return true;
        }
        bool HasApprovedOfficialDoctrine(City city,Kingdom kingdom){
            if(city==null||kingdom==null)return false;foreach(string pid in city.PersonIds){Person o;if(!People.TryGetValue(pid,out o)||!o.Alive||o.Class!=SocialClass.Official)continue;foreach(string tid in o.TraitIds){TraitDef trait;if(!Data.OfficialTraits.TryGetValue(tid,out trait))continue;foreach(OfficialDoctrineDef d in Data.OfficialDoctrines){if(d.OfficialTrait!=trait.Name)continue;foreach(string policyId in kingdom.ActivePolicyIds){PolicyDef policy;if(Data.Policies.TryGetValue(policyId,out policy)&&policy.Name==d.Policy)return true;}}}}return false;
        }
        string GenericGearForSpecial(SpecialUnitDef u,City city){return UnitLoadoutResolver.Join(UnitLoadoutResolver.ResolveSpecial(u,city));}
        string FirstBasicUnitId() { if (Data.UnitIds.Count > 0) return Data.UnitIds[0]; return "U001"; }
        int ActiveMerchantInvestmentsForKingdom(string kingdomId){int n=0;foreach(MerchantInvestment inv in Merchants.Investments){if(inv==null||!inv.Active)continue;Person g;if(People.TryGetValue(inv.GeneralId,out g)&&g.KingdomId==kingdomId)n++;}return n;}
        int KingdomArmySoftCap(Kingdom k){if(k==null)return 0;PolicyRuntimeProfile pp=Policies.RuntimeProfile(k);int cities=Math.Max(1,k.CityIds.Count),barracks=0,pop=0;foreach(string cid in k.CityIds){City c;if(!Cities.TryGetValue(cid,out c))continue;pop+=Population.LivingPopulation(c);if(HasBuilding(c,BuildingKind.Barracks))barracks+=Math.Max(1,HighestBuildingLevel(c,BuildingKind.Barracks));}int baseCap=Math.Max(1,cities+barracks+pop/180);return Mathx.Clamp((int)Math.Round(baseCap*pp.MilitaryCapacity),1,12);}

        void TickConstruction(float dt) {
            foreach (ConstructionProject cp in new List<ConstructionProject>(Projects.Values)) {
                if (cp.Stage == ConstructionStage.Complete) continue;
                City projectCity; Cities.TryGetValue(cp.CityId,out projectCity);
                OfficialRuntimeProfile projectOfficial=projectCity==null?new OfficialRuntimeProfile():OfficialProfileFor(projectCity);
                Kingdom projectKingdom;PolicyRuntimeProfile projectPolicy=projectCity!=null&&Kingdoms.TryGetValue(projectCity.KingdomId,out projectKingdom)?Policies.RuntimeProfile(projectKingdom):new PolicyRuntimeProfile();
                CityCultureProfile projectCulture=CityCultureEffectEngine.Build(projectCity);float projectPolicySpeed=projectPolicy.ConstructionSpeed*(cp.IsRepair?Math.Max(projectPolicy.RebuildSpeed,projectPolicy.WallRepair)*projectCulture.Repair:projectCulture.Construction);if (Construction.Tick(cp, dt*projectOfficial.Construction*projectPolicySpeed)) {
                    Proposal p = FindProposal(cp.ProposalId); if (p != null) p.State = ProposalState.Completed;
                    City c; if (Cities.TryGetValue(cp.CityId, out c)) {
                        int civicAward=ConstructionMerit(cp.Kind,cp.IsRepair);foreach (string workerId in cp.WorkerIds) { Person worker; if (People.TryGetValue(workerId, out worker)) { if(worker.Job == JobKind.Builder) worker.Job = JobKind.Unemployed;if(civicAward>0&&!worker.Noble&&!worker.Merchant){worker.CivicMerit+=civicAward;worker.Experience+=Math.Max(2,civicAward/3);worker.LastMeritReason=cp.IsRepair?"修复公共工程":"营造"+cp.Name;} } }
                        if (cp.Kind == BuildingKind.House) c.PopulationCapacity += 6;
                        if (cp.Kind == BuildingKind.Apartment) c.PopulationCapacity += 14;
                        if (cp.Kind == BuildingKind.Villa) c.PopulationCapacity += 10;
                        if (cp.Kind == BuildingKind.Manor) c.PopulationCapacity += 18;
                        if (cp.Kind == BuildingKind.Shop) c.MarketJobs += 4;
                        if (cp.Kind == BuildingKind.BunShop) c.MarketJobs += 4;
                        if (cp.Kind == BuildingKind.Market) c.MarketJobs += 8;
                        if (cp.Kind == BuildingKind.Farm) c.FarmJobs += 10;
                        if (cp.Kind == BuildingKind.Lumberyard) c.LoggingJobs += 10;
                        if (cp.Kind == BuildingKind.Mill) c.WorkshopJobs += 6;
                        if (cp.Kind == BuildingKind.Bakery) { c.WorkshopJobs += 2; c.MarketJobs += 3; }
                        if (cp.Kind == BuildingKind.Warehouse) c.BuildJobs += 4;
                        if (cp.Kind == BuildingKind.Barracks) RequestGeneralCandidate(c);
                        if (cp.Kind == BuildingKind.Road || cp.Kind == BuildingKind.Bridge) { WorldTile t=Map.Get(cp.X,cp.Y); if(t!=null){t.Road=true;t.RoadLevel=Math.Min(3,Math.Max(1,t.RoadLevel+1));t.RoadCapacity=t.RoadLevel==1?2:(t.RoadLevel==2?4:7);t.RoadCondition=1f;t.SurfaceMud=Math.Min(t.SurfaceMud,.18f);if(cp.Kind==BuildingKind.Bridge||t.River)t.Bridge=true;} }
                        if (cp.Kind == BuildingKind.Wall) BuildWallRing(c);
                        if(Planning!=null){Building completed;if(!string.IsNullOrEmpty(Construction.LastCompletedBuildingId)&&Buildings.TryGetValue(Construction.LastCompletedBuildingId,out completed))Planning.NotifyBuildingCompleted(c,completed);else Planning.Recount(c);}
                        RecordEvent("工程",cp.IsRepair?"工程修复完成":"工程竣工",c.Name+"："+cp.Name,cp.Id);
                    }
                }
            }
        }

        int ConstructionMerit(BuildingKind kind,bool repair){
            if(repair)return 3;
            if(kind==BuildingKind.Wall||kind==BuildingKind.Gate||kind==BuildingKind.Tower||kind==BuildingKind.Bridge||kind==BuildingKind.Barracks||kind==BuildingKind.Government)return 12;
            if(kind==BuildingKind.Road||kind==BuildingKind.Granary||kind==BuildingKind.Market||kind==BuildingKind.Warehouse||kind==BuildingKind.Farm)return 7;
            return 4;
        }
        int ConstructionCost(BuildingKind kind){
            if(kind==BuildingKind.Wall)return 420;
            if(kind==BuildingKind.Road)return 120;
            if(kind==BuildingKind.Bridge)return 180;
            if(kind==BuildingKind.Armory||kind==BuildingKind.TrainingGround||kind==BuildingKind.Government)return 220;
            if(kind==BuildingKind.Apartment)return 200;
            if(kind==BuildingKind.Villa)return 240;
            if(kind==BuildingKind.Manor)return 320;
            if(kind==BuildingKind.Warehouse||kind==BuildingKind.Mill)return 180;
            if(kind==BuildingKind.Farm||kind==BuildingKind.Lumberyard||kind==BuildingKind.Bakery||kind==BuildingKind.BunShop||kind==BuildingKind.Shop)return 150;
            return 180;
        }
        int ConstructionSeconds(BuildingKind kind){
            if(kind==BuildingKind.Wall)return 120;
            if(kind==BuildingKind.Road)return 55;
            if(kind==BuildingKind.Bridge)return 70;
            if(kind==BuildingKind.Apartment||kind==BuildingKind.Villa||kind==BuildingKind.Manor||kind==BuildingKind.Government)return 90;
            if(kind==BuildingKind.Warehouse||kind==BuildingKind.Mill||kind==BuildingKind.Armory||kind==BuildingKind.TrainingGround)return 75;
            return 70;
        }

        bool HasPendingAppointment(string dataId,string personId) {
            foreach(Proposal q in Proposals.Queue) if(q.Kind==ProposalKind.Appointment&&q.DataId==dataId&&q.TargetId==personId&&q.State==ProposalState.Pending) return true;
            return false;
        }

        float LordPrestigeFactor(Kingdom k){if(k==null)return 1f;Person lord;float personal=People.TryGetValue(k.LordId,out lord)&&lord!=null?lord.Stats.Prestige:0f;return Mathx.Clamp(.82f+(k.Prestige+personal)*.0017f,.82f,1.28f);}
        bool AppointmentAccepted(Person candidate){if(candidate==null)return false;Kingdom k;if(!Kingdoms.TryGetValue(candidate.KingdomId,out k))return false;Person lord;float lordPrestige=People.TryGetValue(k.LordId,out lord)&&lord!=null?lord.Stats.Prestige:0f;float acceptance=candidate.Stats.Loyalty+lordPrestige*.22f+k.Prestige*.18f-candidate.Stats.Ambition*.24f;return acceptance>=28f;}

        public float GeneralCandidateScore(Person candidate) {
            if(candidate==null)return float.MinValue;PersonBehaviorProfile personality=TraitEffectEngine.PersonProfile(candidate,Data);
            float score=candidate.Stats.Martial*1.70f+candidate.Stats.Command*1.30f+candidate.Stats.Military*.80f+candidate.Stats.Organization*.45f+candidate.Stats.Prestige*.25f;
            score*=Mathx.Clamp(.94f+.025f*personality.Risk+.025f*personality.Prestige+.02f*personality.Loyalty,.90f,1.08f);
            if(candidate.HasCommandPotential)score+=10f;return score;
        }

        public float OfficialCandidateScore(Person candidate) {
            if(candidate==null)return float.MinValue;PersonBehaviorProfile personality=TraitEffectEngine.PersonProfile(candidate,Data);OfficialRuntimeProfile official=TraitEffectEngine.OfficialProfile(candidate,Data);
            float domain=(official.Population+official.Agriculture+official.Treasury+official.Commerce+official.Construction+official.Military+official.Armory+official.Logistics+official.Fortification+official.Oversight+official.PublicOrder+official.Horses)/12f;
            float score=candidate.Stats.Intelligence*1.55f+candidate.Stats.Command*1.05f+candidate.Stats.Organization*.90f+candidate.Stats.Logistics*.70f+candidate.Stats.Administration*.55f+candidate.Stats.Loyalty*.18f;
            score*=Mathx.Clamp(.94f+.025f*personality.Work+.02f*personality.Loyalty+.025f*(domain-1f),.91f,1.08f);return score;
        }

        public List<Person> RankedServingOfficials(string kingdomId){var x=new List<Person>();foreach(Person p in People.Values)if(p!=null&&p.Alive&&p.KingdomId==kingdomId&&p.Class==SocialClass.Official&&p.Job==JobKind.Official)x.Add(p);x.Sort((a,b)=>OfficialCandidateScore(b).CompareTo(OfficialCandidateScore(a)));return x;}
        public List<Person> RankedServingGenerals(string kingdomId){var x=new List<Person>();foreach(Person p in People.Values)if(p!=null&&p.Alive&&p.KingdomId==kingdomId&&p.Class==SocialClass.General)x.Add(p);x.Sort((a,b)=>GeneralCandidateScore(b).CompareTo(GeneralCandidateScore(a)));return x;}
        public List<Person> RankedOfficialCandidates(City city){var x=new List<Person>();if(city==null)return x;foreach(string id in city.PersonIds){Person p;if(!People.TryGetValue(id,out p)||p==null||!p.Alive||p.Merchant||p.Class==SocialClass.General||p.Class==SocialClass.Official||!p.Noble)continue;x.Add(p);}x.Sort((a,b)=>OfficialCandidateScore(b).CompareTo(OfficialCandidateScore(a)));return x;}
        public List<Person> RankedGeneralCandidates(City city){var x=new List<Person>();if(city==null)return x;foreach(string id in city.PersonIds){Person p;if(!People.TryGetValue(id,out p)||p==null||!p.Alive||p.Merchant||p.Class==SocialClass.General||p.Class==SocialClass.Official||!p.Noble)continue;x.Add(p);}x.Sort((a,b)=>GeneralCandidateScore(b).CompareTo(GeneralCandidateScore(a)));return x;}
        public Proposal SubmitPlayerAppointment(string personId,string role,out string reason){
            reason="";Person candidate;if(!People.TryGetValue(personId,out candidate)||candidate==null||!candidate.Alive){reason="候选人无效";return null;}
            if(candidate.KingdomId!=PlayerKingdomId){reason="只能任命本国人才";return null;}
            string dataId=(role??"").ToLowerInvariant()=="general"?"general":"official";
            if(candidate.Merchant||!candidate.Noble){reason="当前制度下需先取得贵族身份才能进入官员/将军正式任命名单";return null;}
            if(candidate.Class==SocialClass.General||candidate.Class==SocialClass.Official){reason="该人物已经在正式官员或将军序列中";return null;}
            foreach(Proposal q in Proposals.Queue)if(q!=null&&q.Kind==ProposalKind.Appointment&&q.DataId==dataId&&q.TargetId==candidate.Id&&q.State==ProposalState.Pending){reason="该候选人的任命申请已在待批箱";return q;}
            string score=dataId=="general"?Math.Round(GeneralCandidateScore(candidate)).ToString():Math.Round(OfficialCandidateScore(candidate)).ToString();
            string desc=dataId=="general"?(candidate.Name+"以武力、统帅、军政和战功综合评分 "+score+" 申请出任将军。"):(candidate.Name+"以智慧、统帅、组织、后勤和行政综合评分 "+score+" 申请出任官员。");
            Proposal p=new Proposal{Id=Ids.Next(dataId=="general"?"GENREQ":"OFFREQ"),Title=dataId=="general"?"将军任命申请":"官员任命申请",Description=desc,Kind=ProposalKind.Appointment,DataId=dataId,ProposerId=candidate.Id,TargetId=candidate.Id,SubmittedDay=Day};
            if(!Proposals.Submit(p,100)){reason="无法提交任命申请";return null;}reason="已将"+candidate.Name+"的任命申请送入待批箱";return p;
        }
        public Proposal FirstPlayerPendingProposal(){foreach(Proposal p in Proposals.Queue)if(p!=null&&p.State==ProposalState.Pending&&IsPlayerProposal(p))return p;return null;}
        public bool AssignBestAvailableWarOfficial(out string reason){
            reason="当前没有需要指派战役官员的战争";if(WarAdministration==null)return false;
            foreach(Kingdom enemy in Kingdoms.Values){if(enemy==null||enemy.Id==PlayerKingdomId||enemy.Status==KingdomStatus.Eliminated)continue;if(Diplomacy.Get(PlayerKingdomId,enemy.Id).State!=DiplomacyState.War)continue;WarAdministration active=WarAdministration.Get(PlayerKingdomId,enemy.Id);if(active!=null&&WarAdministration.IsOfficialValid(PlayerKingdomId,active.OfficialId)){reason="对"+enemy.Name+"已存在有效战役官员";continue;}Person best=WarAdministration.BestOfficial(PlayerKingdomId,WarAdministrationFocus.Balanced);if(best==null){reason="当前没有可用正式官员";return false;}return AssignWarOfficial(enemy.Id,best.Id,out reason);}
            return false;
        }

        void RequestGeneralCandidate(City c) {
            Kingdom kingdom;if(c==null||!Kingdoms.TryGetValue(c.KingdomId,out kingdom)||FieldArmies(kingdom.Id).Count>=KingdomArmySoftCap(kingdom))return;
            Person best=null;float bestScore=float.MinValue;
            foreach (string id in c.PersonIds) {
                Person candidate; if (!People.TryGetValue(id, out candidate)||!candidate.Alive||!candidate.Noble||candidate.Merchant||candidate.Class==SocialClass.General||candidate.Class==SocialClass.Official) continue;
                if(HasPendingAppointment("general",candidate.Id)) continue;
                float score=GeneralCandidateScore(candidate);if(score>bestScore){bestScore=score;best=candidate;}
            }
            if(best==null)return;
            Proposal p = new Proposal { Id = Ids.Next("GENREQ"), Title = "将军任命申请", Description = best.Name + "依据武力、统帅、军政、组织与战功申请出任将军并组建军队。", Kind = ProposalKind.Appointment, DataId = "general", ProposerId = best.Id, TargetId = best.Id, SubmittedDay = Day };
            Proposals.Submit(p, 100);
        }

        void RequestOfficialCandidate(City c){
            if(FindOfficial(c)!=null)return;Person best=null;float bestScore=float.MinValue;
            foreach(string id in c.PersonIds){Person candidate;if(!People.TryGetValue(id,out candidate)||!candidate.Alive||!candidate.Noble||candidate.Merchant||candidate.Class==SocialClass.General)continue;if(HasPendingAppointment("official",candidate.Id))continue;float score=OfficialCandidateScore(candidate);if(score>bestScore){bestScore=score;best=candidate;}}
            if(best==null)return;var p=new Proposal{Id=Ids.Next("OFFREQ"),Title="官员任命申请",Description=best.Name+"依据智慧、统帅、组织、后勤、行政与治理特性申请出任本城官员。",Kind=ProposalKind.Appointment,DataId="official",ProposerId=best.Id,TargetId=best.Id,SubmittedDay=Day,RequiredAbility=0};Proposals.Submit(p,100);
        }
        void ConsiderMerchantEnnoblement(City c){foreach(string id in c.PersonIds){Person m;if(!People.TryGetValue(id,out m)||!m.Alive||!m.Merchant||m.Noble||m.Wealth<1600||m.Stats.Loyalty<55)continue;bool exists=false;foreach(Proposal q in Proposals.Queue)if(q.Kind==ProposalKind.Promotion&&q.DataId=="merchant_noble"&&q.TargetId==m.Id&&q.State==ProposalState.Pending){exists=true;break;}if(exists)continue;var p=new Proposal{Id=Ids.Next("MERIT"),Title="商贾授爵申请",Description=m.Name+"以财富、商税与国家贡献申请进入贵族身份；批准后不再以商人身份直接参与官职申请。",Kind=ProposalKind.Promotion,DataId="merchant_noble",ProposerId=m.Id,TargetId=m.Id,SubmittedDay=Day};Proposals.Submit(p,100);return;}}

        bool HasPendingPromotion(string dataId,string personId){foreach(Proposal q in Proposals.Queue)if(q.Kind==ProposalKind.Promotion&&q.DataId==dataId&&q.TargetId==personId&&q.State==ProposalState.Pending)return true;return false;}
        void ConsiderCivilianEnnoblement(City c){if(c==null)return;foreach(string id in c.PersonIds){Person p;if(!People.TryGetValue(id,out p)||!p.Alive||p.Noble||p.Merchant||p.CivicMerit<100||p.Stats.Loyalty<50||HasPendingPromotion("civil_noble",p.Id))continue;string reason=string.IsNullOrEmpty(p.LastMeritReason)?"国家功勋":p.LastMeritReason;Proposal q=new Proposal{Id=Ids.Next("CIVMERIT"),Title="平民功勋授爵申请",Description=p.Name+"因"+reason+"累积国家功勋，申请晋升贵族。",Kind=ProposalKind.Promotion,DataId="civil_noble",ProposerId=p.Id,TargetId=p.Id,SubmittedDay=Day};Proposals.Submit(q,100);return;}}
        bool HasHistoricEquipment(Person p){if(p==null)return false;foreach(string eid in p.EquipmentIds){EquipmentInstance e;if(Equipment.TryGetValue(eid,out e)&&e!=null&&(e.Unique||e.Quality>=7||e.QualityName=="传奇"))return true;}return false;}
        void TickCivilMerit(){foreach(City c in Cities.Values){foreach(string id in new List<string>(c.PersonIds)){Person p;if(!People.TryGetValue(id,out p)||!p.Alive||p.Noble||p.Merchant)continue;if(p.Job==JobKind.Miner&&p.Experience>=90&&(p.LastMeritReason??"").IndexOf("矿脉",StringComparison.Ordinal)<0&&_rng.Chance(.055f)){c.Iron+=36;c.Stone+=28;p.CivicMerit=Math.Max(110,p.CivicMerit+60);p.LastMeritReason="发现大型矿脉并献于国家";p.Stats.Prestige+=8;RecordEvent("民生","发现大型矿脉",p.Name+"在"+c.Name+"发现大型矿脉并将采掘权献给国家",p.Id);}if(p.Job==JobKind.Smith&&p.Experience>=140&&p.CivicMerit<105){p.CivicMerit=105;p.LastMeritReason="成为国家认可的铸造名匠";p.Stats.Prestige+=6;RecordEvent("工艺","名匠立功",p.Name+"因长期军民铸造贡献获得国家功勋",p.Id);}if(HasHistoricEquipment(p)&&p.CivicMerit<120){p.CivicMerit=120;p.LastMeritReason="持有并保全传奇名器";p.Stats.Prestige+=8;RecordEvent("人物","传奇名器功勋",p.Name+"因持有并保全传奇名器获得授爵资格",p.Id);}}}}
        void TickMerchantSecurityEvents(){foreach(City c in Cities.Values){Kingdom k;if(!Kingdoms.TryGetValue(c.KingdomId,out k))continue;PolicyRuntimeProfile pp=Policies.RuntimeProfile(k);OfficialRuntimeProfile op=OfficialProfileFor(c);bool war=false,enemyNear=false;foreach(Kingdom other in Kingdoms.Values)if(other.Id!=k.Id&&other.Status!=KingdomStatus.Eliminated&&Diplomacy.Get(k.Id,other.Id).State==DiplomacyState.War){war=true;break;}foreach(Army a in Armies.Values)if(a.KingdomId!=k.Id&&Diplomacy.Get(k.Id,a.KingdomId).State==DiplomacyState.War&&Mathx.Manhattan(a.X,a.Y,c.X,c.Y)<=6){enemyNear=true;break;}foreach(string id in c.PersonIds){Person m;if(!People.TryGetValue(id,out m)||!m.Alive||!m.Merchant||m.Wealth<500)continue;PersonBehaviorProfile mp=TraitEffectEngine.PersonProfile(m,Data);float risk=.012f+Math.Min(.055f,m.Wealth/40000f)+(war ? .018f:0f)+(enemyNear ? .07f:0f);risk*=Mathx.Clamp(mp.Risk,.75f,1.45f);risk/=Mathx.Clamp(pp.PublicOrder*op.PublicOrder,.65f,2.4f);if(!_rng.Chance(Mathx.Clamp(risk,.003f,.16f)))continue;int loss=Math.Max(18,(int)Math.Round(m.Wealth*_rng.Range(5,16)/100f));loss=Math.Min(loss,m.Wealth);m.Wealth-=loss;m.Stats.Loyalty=Math.Max(0,m.Stats.Loyalty-3);RecordEvent("治安","商路劫掠",m.Name+"的商队在"+c.Name+"附近遭劫，损失 "+loss+" 金币；治安政策、官员能力和战争态势会改变此风险",m.Id);}}}
        void TickLordPrestigeEffects(){foreach(Kingdom k in Kingdoms.Values){if(k.Status==KingdomStatus.Eliminated)continue;float factor=LordPrestigeFactor(k);int loyaltyDelta=factor>=1.12f?2:(factor>=1.02f?1:(factor<.90f?-2:(factor<.98f?-1:0)));if(loyaltyDelta==0)continue;foreach(Person p in People.Values)if(p.Alive&&p.KingdomId==k.Id&&p.Injury!=InjuryState.Captured)p.Stats.Loyalty=Mathx.Clamp(p.Stats.Loyalty+loyaltyDelta,0,100);foreach(Family f in Families.Values){City home;if(!string.IsNullOrEmpty(f.HomeCityId)&&Cities.TryGetValue(f.HomeCityId,out home)&&home.KingdomId==k.Id)f.Loyalty=Mathx.Clamp(f.Loyalty+loyaltyDelta,0,100);}}}
        City DefectionDestination(string oldKingdomId){City best=null;int bestScore=int.MinValue;foreach(City c in Cities.Values){if(c.KingdomId==oldKingdomId)continue;Kingdom k;if(!Kingdoms.TryGetValue(c.KingdomId,out k)||k.Status==KingdomStatus.Eliminated)continue;DiplomacyRelation r=Diplomacy.Get(oldKingdomId,k.Id);if(r.State==DiplomacyState.Vassal)continue;int score=k.Prestige+(r.State==DiplomacyState.War?12:(r.State==DiplomacyState.Trade?8:0))+Population.LivingPopulation(c)/10;if(score>bestScore){bestScore=score;best=c;}}return best;}
        void TickOfficialPolitics(){foreach(Person official in new List<Person>(People.Values)){if(!official.Alive||official.Class!=SocialClass.Official||official.Job!=JobKind.Official)continue;Kingdom k;if(!Kingdoms.TryGetValue(official.KingdomId,out k)||k.Status==KingdomStatus.Eliminated)continue;Family f=null;if(!string.IsNullOrEmpty(official.FamilyId))Families.TryGetValue(official.FamilyId,out f);float pressure=Math.Max(0,45-official.Stats.Loyalty)*.012f+Math.Max(0,official.Stats.Ambition-60)*.008f+Math.Max(0,1f-LordPrestigeFactor(k))*.16f;if(f!=null&&official.Stats.Loyalty<28&&official.Stats.Ambition>72){f.Loyalty=Math.Max(0,f.Loyalty-5);f.Ambition=Math.Min(100,f.Ambition+4);}if(pressure<.12f||!_rng.Chance(Mathx.Clamp(pressure*.14f,.005f,.11f)))continue;City oldCity;Cities.TryGetValue(official.CityId,out oldCity);City target=DefectionDestination(k.Id);if(target==null)continue;if(oldCity!=null)oldCity.PersonIds.Remove(official.Id);target.PersonIds.Add(official.Id);if(f!=null)f.MemberIds.Remove(official.Id);official.FamilyId="";official.KingdomId=target.KingdomId;official.CityId=target.Id;official.Class=SocialClass.Noble;official.Job=JobKind.Unemployed;official.Stats.Prestige=Math.Max(0,official.Stats.Prestige-6);RecordEvent("政治","官员叛逃",official.Name+"因忠诚低落、野心与领主威望失衡，弃官叛逃至"+target.Name,official.Id);}}

        void TickGovernance(float dt) {
            Proposals.Tick(Day);
            foreach (Proposal p in Proposals.Queue) {
                if (p.State == ProposalState.Completed && p.Kind == ProposalKind.Policy) {
                    Person official; if (!People.TryGetValue(p.ProposerId, out official)) continue;
                    Kingdom k; City c; if (Kingdoms.TryGetValue(official.KingdomId, out k) && Cities.TryGetValue(official.CityId, out c)) {bool wasActive=k.ActivePolicyIds.Contains(p.DataId);Policies.ApplyCompleted(p,k,c);if(!wasActive&&k.ActivePolicyIds.Contains(p.DataId)){PolicyDef pd;if(Data.Policies.TryGetValue(p.DataId,out pd))RecordEvent("政策","政策生效",k.Name+"施行《"+pd.DisplayName+"》",pd.Id);}}
                }
            }
            foreach (City c in Cities.Values) { Merchants.DiscoverMerchants(c, 650); RequestOfficialCandidate(c); if(Day%30==0){ConsiderMerchantEnnoblement(c);ConsiderCivilianEnnoblement(c);} if(Day%5==0)EnsurePolicyProposal(c); EnsureDevelopmentProposal(c); }
        }

        void EnsurePolicyProposal(City c){Person o=FindOfficial(c);Kingdom k;if(o==null||!Kingdoms.TryGetValue(c.KingdomId,out k))return;int pop=Population.LivingPopulation(c);string wanted;if(c.Food<pop*5)wanted="农业粮政";else if(c.Horses<8)wanted="骑兵马政";else if(!HasBuilding(c,BuildingKind.Wall))wanted="城防营造";else {Army a=FirstArmy(k.Id);if(a!=null&&a.FoodDays<5f)wanted="军粮后勤";else {string[] cats={"人口户籍","市场商税","财政国库","贵族家族","官员行政","基础军制","军械装备","道路工程","治安民生","外交边防","军功荣誉"};wanted=cats[(Day/5+Math.Abs(c.Id.GetHashCode()))%cats.Length];}}foreach(PolicyDef d in Data.Policies.Values){if(d.Category!=wanted)continue;if(Policies.Propose(d.Id,o,k,Day))return;}}

        void TickEconomy(float dt) {
            float days = dt / 30f;
            foreach (City c in Cities.Values) {
                Kingdom k; if (!Kingdoms.TryGetValue(c.KingdomId, out k)) continue;PolicyRuntimeProfile profile=Policies.RuntimeProfile(k);OfficialRuntimeProfile official=OfficialProfileFor(c);int before=k.Treasury;
                float blockadeMul=Sieges!=null&&Sieges.IsBlockaded(c.Id) ? .35f:1f;float tradeMul=1f+Math.Min(.2f,Diplomacy.TradePartnerCount(k.Id)*.05f);
                float weatherAgriculture=Weather.AgricultureMultiplier();if(weatherAgriculture<1f)weatherAgriculture=Math.Min(1f,weatherAgriculture*profile.DisasterResilience);CityCultureProfile culture=CityCultureEffectEngine.Build(c);CityBuildingEffectProfile built=CityBuildingEffectEngine.Build(c,Buildings);
                float productiveLand=Mathx.Clamp(profile.FarmlandCapacity,.75f,1.55f);Economy.ProductionTick(c, days, weatherAgriculture*profile.Agriculture*productiveLand*official.Agriculture*culture.Agriculture*built.Agriculture*blockadeMul,culture.Mining,culture.Logging*built.Logging,culture.Smithing*built.Smithing,culture.Commerce*built.Commerce);int baseGain=Math.Max(0,k.Treasury-before);float prestigeTax=Mathx.Clamp(LordPrestigeFactor(k),.88f,1.20f);float treasuryMul=profile.TreasuryEfficiency*official.Treasury*culture.Commerce*prestigeTax*blockadeMul;if(treasuryMul>1f&&baseGain>0)Economy.Credit(k,(int)Math.Round(baseGain*(treasuryMul-1f)));
                foreach (string id in c.PersonIds) {
                    Person p; if (!People.TryGetValue(id, out p) || !p.Alive || !p.Merchant) continue;PersonBehaviorProfile merchantProfile=TraitEffectEngine.PersonProfile(p,Data);
                    int tax = (int)Math.Round(Economy.MerchantTax(p, Math.Max(1, (int)Math.Round(3f * days)), Merchants.TaxReductionFor(p.Id))*profile.MerchantTax*official.Commerce*merchantProfile.Commerce*culture.Commerce*prestigeTax*blockadeMul*tradeMul);
                    Economy.Credit(k, tax);
                }
            }
        }

        void TickMerchantInvestmentOffers(){foreach(City c in Cities.Values){Kingdom k;if(!Kingdoms.TryGetValue(c.KingdomId,out k))continue;PolicyRuntimeProfile pp=Policies.RuntimeProfile(k);Army target=FirstArmy(c.KingdomId);if(target==null)continue;Person general;People.TryGetValue(target.GeneralId,out general);int maxForArmy=Mathx.Clamp((int)Math.Round(2f*pp.MerchantInvestmentCapacity),1,4);int maxForKingdom=Mathx.Clamp((int)Math.Round(Math.Max(1,k.CityIds.Count)*2f*pp.MerchantInvestmentCapacity),1,10);if(Merchants.ActiveInvestmentCountForGeneral(target.GeneralId)>=maxForArmy||ActiveMerchantInvestmentsForKingdom(k.Id)>=maxForKingdom)continue;foreach(string id in c.PersonIds){Person m;if(!People.TryGetValue(id,out m)||!m.Alive||!m.Merchant||m.Noble||m.Wealth<850||Merchants.HasActiveInvestment(m.Id))continue;PersonBehaviorProfile mp=TraitEffectEngine.PersonProfile(m,Data);int prestige=general==null?0:general.Stats.Prestige;bool need=target.Finance.Gold<320||target.Finance.MonthsInArrears>0||target.BattleCount>1;if(!need||prestige<5&&target.BattleCount==0)continue;float politicalRisk=MilitaryFinanceSystem.PoliticalRisk(target.Finance,pp.DebtGrace);float willingness=mp.Investment*mp.Risk*mp.Loyalty*pp.MerchantLoyalty*(.8f+Math.Min(.5f,prestige/100f))*(1f-.35f*politicalRisk);if(willingness<.85f)continue;int principal=Math.Min(320,Math.Max(120,(int)Math.Round(m.Wealth/4f*Mathx.Clamp(mp.Investment*pp.MerchantInvestmentCapacity,.75f,1.55f))));int monthly=Math.Min(100,Math.Max(30,(int)Math.Round(m.Wealth/16f*Mathx.Clamp(mp.Investment*pp.ArmyFinanceReliability,.75f,1.45f))));float taxReduction=Mathx.Clamp(.55f/Mathx.Clamp(pp.MerchantTax,.8f,1.35f),.35f,.65f);Proposal p=Merchants.CreateInvestmentProposal(m,target,principal,monthly,Day,taxReduction);if(p!=null)Proposals.Submit(p,100);break;}}}

        WarAdministration WarAdministrationForArmy(Army a){
            if(a==null||WarAdministration==null)return null;string enemyId="";BattleSession battle=ActiveBattleForArmy(a.Id);if(battle!=null){Army other;string otherId=battle.AttackerArmyId==a.Id?battle.DefenderArmyId:battle.AttackerArmyId;if(Armies.TryGetValue(otherId,out other))enemyId=other.KingdomId;}
            if(string.IsNullOrEmpty(enemyId)){SiegeState siege=SiegeForArmy(a.Id);City siegeCity;if(siege!=null&&Cities.TryGetValue(siege.CityId,out siegeCity))enemyId=siegeCity.KingdomId;}
            if(string.IsNullOrEmpty(enemyId)){City target=CityAt(a.TargetX,a.TargetY,a.KingdomId);if(target!=null)enemyId=target.KingdomId;}
            if(!string.IsNullOrEmpty(enemyId))return WarAdministration.Get(a.KingdomId,enemyId);WarAdministration only=null;foreach(WarAdministration x in WarAdministration.All()){if(x.KingdomId!=a.KingdomId||Diplomacy.Get(x.KingdomId,x.EnemyKingdomId).State!=DiplomacyState.War)continue;if(only!=null)return null;only=x;}return only;
        }
        float WarCoordinationForArmy(Army a){WarAdministration x=WarAdministrationForArmy(a);if(x!=null&&WarAdministration.IsOfficialValid(x.KingdomId,x.OfficialId))return WarAdministration.Profile(x).Coordination;foreach(Kingdom enemy in Kingdoms.Values)if(enemy.Id!=a.KingdomId&&Diplomacy.Get(a.KingdomId,enemy.Id).State==DiplomacyState.War)return .96f;return 1f;}
        float WarLogisticsForArmy(Army a){WarAdministration x=WarAdministrationForArmy(a);if(x!=null&&WarAdministration.IsOfficialValid(x.KingdomId,x.OfficialId))return WarAdministration.Profile(x).Logistics;foreach(Kingdom enemy in Kingdoms.Values)if(enemy.Id!=a.KingdomId&&Diplomacy.Get(a.KingdomId,enemy.Id).State==DiplomacyState.War)return .96f;return 1f;}

        void TickArmies(float dt) {
            foreach (Army a in Armies.Values) {
                Person general;People.TryGetValue(a.GeneralId,out general);CommanderBehaviorProfile command=CommanderSkillEffectEngine.Build(general,Data);
                City own=NearestOwnCity(a.KingdomId,a.X,a.Y);SupplyRoute route=Logistics.RouteForArmy(a.Id);bool routeIntact=route!=null&&route.Intact;
                if(own!=null&&Mathx.Manhattan(a.X,a.Y,own.X,own.Y)<=2&&a.FoodDays<8f)Logistics.LoadFood(a,own,8,Military.SoldierCount(a),FindOfficialLogistics(a.KingdomId),(command.SupplyEfficiency*WarLogisticsForArmy(a)));
                Logistics.DailySupplyTick(a, Military.SoldierCount(a), routeIntact, FindOfficialLogistics(a.KingdomId),(command.SupplyEfficiency*WarLogisticsForArmy(a)));
                Kingdom financeKingdom;PolicyRuntimeProfile financePolicy=Kingdoms.TryGetValue(a.KingdomId,out financeKingdom)?Policies.RuntimeProfile(financeKingdom):new PolicyRuntimeProfile();float debtPenalty=MilitaryFinanceSystem.LoyaltyPenalty(a.Finance,financePolicy.DebtGrace);a.Morale = Math.Max(0, a.Morale - debtPenalty * command.ArrearsSensitivity * .08f * dt);if(a.FoodDays<=0f||a.Finance.MonthsInArrears>=2)ApplyCampaignDesertion(a,command,financePolicy);if(own!=null&&a.Order==ArmyOrder.Hold&&a.FoodDays>2f){a.Morale=Math.Min(100f,a.Morale+.05f*financePolicy.SoldierMorale*dt);a.Fatigue=Math.Max(0f,a.Fatigue-.18f*financePolicy.FatigueRecovery*dt);}
                if(a.Order==ArmyOrder.Camp){float weatherRest=(Weather.Weather==WeatherKind.Storm||Weather.Weather==WeatherKind.Blizzard)? .58f:(Weather.Weather==WeatherKind.Rain||Weather.Weather==WeatherKind.Snow||Weather.Weather==WeatherKind.Cold)? .78f:1f;float foodRest=a.FoodDays>0f?1f:.45f;a.Fatigue=Math.Max(0f,a.Fatigue-18f*weatherRest*foodRest*financePolicy.FatigueRecovery);a.Morale=Math.Min(100f,a.Morale+(a.FoodDays>1f?2.2f:.3f)*financePolicy.SoldierMorale);if(a.Fatigue<=34f||EnemyArmyNear(a,3)||a.FoodDays<=.25f){a.Order=a.ResumeOrder==ArmyOrder.Camp?ArmyOrder.March:a.ResumeOrder;a.CampStartDay=-1;}}
            }
        }
        void ApplyCampaignDesertion(Army a,CommanderBehaviorProfile command,PolicyRuntimeProfile policy){if(a==null)return;int soldiers=Military.SoldierCount(a);if(soldiers<=0)return;float hunger=a.FoodDays<=0f ? .016f : 0f;float arrears=Math.Min(.035f,a.Finance.MonthsInArrears*.006f);float morale=Math.Max(0f,35f-a.Morale)*.00055f;float discipline=Mathx.Clamp(command.Discipline*command.Morale*policy.SoldierMorale,.65f,1.9f);int leave=Math.Min(Math.Max(0,(int)Math.Ceiling(soldiers*(hunger+arrears+morale)/discipline)),Math.Max(1,soldiers/10));if(leave<=0)return;int gone=0;for(int qi=a.Squads.Count-1;qi>=0&&gone<leave;qi--){Squad sq=a.Squads[qi];for(int i=sq.SoldierIds.Count-1;i>=0&&gone<leave;i--){string id=sq.SoldierIds[i];Person p;if(!People.TryGetValue(id,out p)||!p.Alive||p.Job!=JobKind.Soldier)continue;SoldierBehaviorProfile sp=TraitEffectEngine.SoldierProfile(p,Data);if(sp.Discipline>1.15f&&p.Stats.Loyalty>=55&&((p.Experience+p.Battles+Day+i)%3)!=0)continue;sq.SoldierIds.RemoveAt(i);p.Job=JobKind.Unemployed;p.Stats.Loyalty=Math.Max(0,p.Stats.Loyalty-5);gone++;}}if(gone>0){a.Deserters+=gone;a.Morale=Math.Max(0,a.Morale-gone/(float)Math.Max(1,soldiers)*18f);RecordEvent("军纪","军中出现逃兵",a.Name+" 因断粮/欠饷有 "+gone+" 人脱离部队",a.Id);}}

        void TickLogisticsDaily(){
            Dictionary<string,int> logistics=new Dictionary<string,int>();
            foreach(Kingdom k in Kingdoms.Values){PolicyRuntimeProfile p=Policies.RuntimeProfile(k);logistics[k.Id]=Mathx.Clamp((int)Math.Round(FindOfficialLogistics(k.Id)*p.Supply*p.ConvoySafety),0,100);}
            foreach(Army a in Armies.Values){
                City source=NearestOwnCity(a.KingdomId,a.X,a.Y);if(source==null)continue;int distance=Mathx.Manhattan(a.X,a.Y,source.X,source.Y);if(distance<=2)continue;
                Person general;People.TryGetValue(a.GeneralId,out general);CommanderBehaviorProfile command=CommanderSkillEffectEngine.Build(general,Data);
                SpecialUnitBehaviorProfile specialSupply=SpecialUnitBehaviorEngine.ArmyProfile(a,Data);int effectiveLogistics=Mathx.Clamp((int)Math.Round(logistics[a.KingdomId]*Mathx.Clamp(command.SupplyEfficiency*specialSupply.SupplyGuard*WarLogisticsForArmy(a),.65f,1.85f)),0,100);
                SupplyRoute route=Logistics.EnsureRoute(a,source,Day);
                if(route!=null&&route.Intact&&a.FoodDays<7f){
                    int estimatedCost=Math.Max(1,(int)Math.Ceiling(Military.SoldierCount(a)/18f*command.SupplyGoldCost));
                    if(a.Finance.Gold>=estimatedCost){SupplyConvoy convoy=Logistics.Dispatch(route,a,source,Military.SoldierCount(a),effectiveLogistics,Day);if(convoy!=null){a.Finance.Gold-=estimatedCost;a.Finance.SupplyCost+=estimatedCost;}}
                }
            }
            Logistics.TickDay(Armies,Cities,logistics,Weather.Weather);
        }
        City NearestOwnCity(string kingdomId,int x,int y){City best=null;int d0=int.MaxValue;foreach(City c in Cities.Values)if(c.KingdomId==kingdomId){int d=Mathx.Manhattan(x,y,c.X,c.Y);if(d<d0){d0=d;best=c;}}return best;}

        int FindOfficialLogistics(string kingdomId) {
            int best = 20;
            foreach (Person p in People.Values) if (p.Alive && p.KingdomId == kingdomId && p.Class == SocialClass.Official) {OfficialRuntimeProfile op=TraitEffectEngine.OfficialProfile(p,Data);best = Math.Max(best, (int)Math.Round(p.Stats.Logistics*op.Logistics));}
            return Mathx.Clamp(best,0,100);
        }
        OfficialRuntimeProfile OfficialProfileFor(City c){Person o=FindOfficial(c);return o==null?new OfficialRuntimeProfile():TraitEffectEngine.OfficialProfile(o,Data);}

        void ApplyMarchEquipmentWear(Army a,int distance){if(a==null||distance<=0)return;Kingdom k;PolicyRuntimeProfile pp=Kingdoms.TryGetValue(a.KingdomId,out k)?Policies.RuntimeProfile(k):new PolicyRuntimeProfile();int index=0;WorldTile tile=Map.Get(a.X,a.Y);TerrainKind terrain=tile==null?TerrainKind.Grass:tile.Terrain;int adjustedDistance=Math.Max(1,(int)Math.Ceiling(distance/Mathx.Clamp(pp.EquipmentDurability,.75f,1.7f)));foreach(Squad sq in a.Squads)foreach(string id in sq.SoldierIds){if((index++ + a.RouteIndex + Day)%4!=0)continue;Person p;if(!People.TryGetValue(id,out p))continue;foreach(string eid in p.EquipmentIds){EquipmentInstance e;if(Equipment.TryGetValue(eid,out e))EquipmentDurabilitySystem.MarchWear(e,terrain,adjustedDistance);}}}
        void RepairArmyEquipmentAtHome(Army a){if(a==null)return;City c=NearestOwnCity(a.KingdomId,a.X,a.Y);if(c==null||Mathx.Manhattan(a.X,a.Y,c.X,c.Y)>1||!HasBuilding(c,BuildingKind.Smithy))return;Kingdom k;PolicyRuntimeProfile pp=Kingdoms.TryGetValue(a.KingdomId,out k)?Policies.RuntimeProfile(k):new PolicyRuntimeProfile();OfficialRuntimeProfile op=OfficialProfileFor(c);float efficiency=pp.RepairEfficiency*op.Armory;int repaired=0;foreach(Squad sq in a.Squads)foreach(string pid in sq.SoldierIds){Person p;if(!People.TryGetValue(pid,out p))continue;foreach(string eid in p.EquipmentIds){EquipmentInstance e;if(!Equipment.TryGetValue(eid,out e)||e.Durability>=e.MaxDurability*.7f)continue;int cost=Gear.Repair(e,a.Finance,efficiency);if(cost<0){a.Finance.ExpectedRepairCost=Math.Max(a.Finance.ExpectedRepairCost,-cost);return;}if(cost>0&&++repaired>=Math.Max(8,(int)Math.Round(12f*pp.RepairEfficiency)))return;}}}

        bool EnemyArmyNear(Army a,int radius){if(a==null)return false;foreach(Army other in Armies.Values){if(other==null||other.Id==a.Id||other.KingdomId==a.KingdomId||Military.ReadySoldierCount(other)<=0)continue;if(Diplomacy.Get(a.KingdomId,other.KingdomId).State!=DiplomacyState.War)continue;if(Mathx.Manhattan(a.X,a.Y,other.X,other.Y)<=radius)return true;}return false;}

        void TickArmyMovement(float dt) {
            Dictionary<string,int> occupancy=new Dictionary<string,int>();
            foreach(Army x in Armies.Values){string key=x.X+":"+x.Y;int n;occupancy.TryGetValue(key,out n);occupancy[key]=n+1;}
            foreach (Army a in new List<Army>(Armies.Values)) {
                if(IsArmyInActiveBattle(a.Id))continue;
                if(a.Order==ArmyOrder.March&&a.Fatigue>=72f&&a.FoodDays>.5f&&!EnemyArmyNear(a,3)){a.ResumeOrder=ArmyOrder.March;a.Order=ArmyOrder.Camp;a.CampStartDay=Day;continue;}
                if(a.Order==ArmyOrder.Camp)continue;
                if (a.Order == ArmyOrder.Retreat) { City home=NearestOwnCity(a.KingdomId,a.X,a.Y); if(home!=null)March.SetDestination(a,home.X,home.Y); }
                if (a.Order == ArmyOrder.Muster) {
                    Person musterGeneral;People.TryGetValue(a.GeneralId,out musterGeneral);CommanderBehaviorProfile musterProfile=CommanderSkillEffectEngine.Build(musterGeneral,Data);int total=Math.Max(1,Military.SoldierCount(a)),ready=Military.ReadySoldierCount(a);float readiness=Mathx.Clamp(ready/(float)total,.25f,1f);Kingdom musterKingdom;PolicyRuntimeProfile musterPolicy=Kingdoms.TryGetValue(a.KingdomId,out musterKingdom)?Policies.RuntimeProfile(musterKingdom):new PolicyRuntimeProfile();
                    float warCoord=WarCoordinationForArmy(a);float musterPolicyMul=Mathx.Clamp(musterPolicy.RecruitmentSpeed*musterPolicy.CommandResponse*warCoord,.65f,1.9f);a.MusterProgress=Mathx.Clamp(a.MusterProgress+dt*(.12f+.16f*musterProfile.ReformSpeed+.12f*AverageCohesion(a))*readiness*musterPolicyMul,0f,1f);
                    if(a.MusterProgress>=1f){if(a.StrategicRoute==null||a.StrategicRoute.Count==0)a.Order=ArmyOrder.Hold;else{a.Order=ArmyOrder.March;a.Morale=Math.Min(100f,a.Morale+2f);}}
                    continue;
                }
                if (a.Order == ArmyOrder.March) {
                    Person general; People.TryGetValue(a.GeneralId, out general);
                    CommanderBehaviorProfile profile = CommanderSkillEffectEngine.Build(general, Data);
                    int roadOccupancy=1;occupancy.TryGetValue(a.X+":"+a.Y,out roadOccupancy);int oldX=a.X,oldY=a.Y;
                    Kingdom marchKingdom;PolicyRuntimeProfile marchPolicy=Kingdoms.TryGetValue(a.KingdomId,out marchKingdom)?Policies.RuntimeProfile(marchKingdom):new PolicyRuntimeProfile();if(March.Step(a, dt * 2.5f * profile.MarchSpeed * marchPolicy.RoadSpeed * SpecialUnitBehaviorEngine.ArmyProfile(a,Data).FlankMobility, false, Weather.MarchPenalty(), Math.Max(1,roadOccupancy),profile.TerrainAdapt,profile.FatigueEfficiency,profile.MarchFatigueCost*SpecialUnitBehaviorEngine.ArmyProfile(a,Data).FatigueCost,Weather.Weather,marchPolicy.BridgeSpeed))ApplyMarchEquipmentWear(a,Mathx.Manhattan(oldX,oldY,a.X,a.Y));
                }
                if (a.Order != ArmyOrder.Hold) continue;
                City enemy = CityAt(a.X, a.Y, a.KingdomId);
                if (enemy != null) {
                    Army defender = DefenderArmyAtCity(enemy);
                    if (defender == null) defender = EnsureRealCityDefender(enemy);
                    if (defender != null && Military.ReadySoldierCount(defender) > 0) {
                        if (a.LastBattleDay != Day && defender.LastBattleDay != Day) StartBattle(a, defender, enemy.Name);
                    } else {
                        Sieges.Begin(a, enemy, FortificationsFor(enemy,true), AverageGarrisonMorale(enemy));
                        a.Order=ArmyOrder.Siege;
                    }
                }
            }
            RefreshGarrisonRegistry();
        }

        public bool DeclareWarAndMarch() {
            if (string.IsNullOrEmpty(PlayerKingdomId)) return false;
            Kingdom player = Kingdoms[PlayerKingdomId];Army army=FirstArmy(player.Id);if(army==null)return false;
            Kingdom enemy=NearestEnemyKingdom(player,army.X,army.Y);if(enemy==null)return false;
            return DeclareWarAndMarch(enemy.Id,new string[]{army.Id})>0;
        }
        public int DeclareWarAndMarch(string enemyKingdomId,IEnumerable<string> selectedArmyIds){
            if(string.IsNullOrEmpty(PlayerKingdomId)||string.IsNullOrEmpty(enemyKingdomId)||selectedArmyIds==null)return 0;
            Kingdom player,enemy;if(!Kingdoms.TryGetValue(PlayerKingdomId,out player)||!Kingdoms.TryGetValue(enemyKingdomId,out enemy)||enemy.Id==player.Id||player.Status!=KingdomStatus.Active||enemy.Status==KingdomStatus.Eliminated)return 0;
            DiplomacyRelation rel=Diplomacy.Get(player.Id,enemy.Id);if(rel.State==DiplomacyState.Alliance||rel.State==DiplomacyState.NonAggression||rel.TruceUntilDay>Day)return 0;
            City target;if(!Cities.TryGetValue(enemy.CapitalCityId,out target))return 0;var eligible=new List<Army>();var seen=new HashSet<string>();
            foreach(string id in selectedArmyIds){if(string.IsNullOrEmpty(id)||!seen.Add(id))continue;Army a;if(!Armies.TryGetValue(id,out a)||a.KingdomId!=player.Id||Military.SoldierCount(a)<=0||a.FoodDays<=1f)continue;eligible.Add(a);}
            if(eligible.Count==0)return 0;bool newWar=rel.State!=DiplomacyState.War;if(newWar&&!Diplomacy.DeclareWar(player.Id,enemy.Id,Day))return 0;if(newWar)RecordEvent("战争","宣战",player.Name+"向"+enemy.Name+"宣战；持续战争调度需由玩家另行指定在任官员负责",enemy.Id);int moved=0;
            foreach(Army a in eligible){if(March.SetDestination(a,target.X,target.Y,false)){a.MusterStartDay=Day;moved++;}}
            return moved;
        }
        public WarAdministration WarAdministrationFor(string kingdomId,string enemyKingdomId){return WarAdministration==null?null:WarAdministration.Get(kingdomId,enemyKingdomId);}
        void ApplyWarPlanning(Army army,CommanderBehaviorProfile command){if(army==null||command==null||WarAdministration==null)return;WarAdministration x=WarAdministrationForArmy(army);if(x!=null&&WarAdministration.IsOfficialValid(x.KingdomId,x.OfficialId))WarAdministration.ApplyCampaignPlanning(x,command);}
        public bool AssignWarOfficial(string enemyKingdomId,string officialId,out string reason){
            reason="";if(WarAdministration==null||string.IsNullOrEmpty(PlayerKingdomId)){reason="战争行政系统尚未初始化";return false;}DiplomacyRelation rel=Diplomacy.Get(PlayerKingdomId,enemyKingdomId);if(rel.State!=DiplomacyState.War){reason="只有已经处于战争状态的敌国才能指派战争负责官员";return false;}
            WarAdministration x=WarAdministration.Assign(PlayerKingdomId,enemyKingdomId,officialId,Day,WarAdministrationFocus.Balanced,out reason);if(x==null)return false;Person official;Kingdom enemy;People.TryGetValue(officialId,out official);Kingdoms.TryGetValue(enemyKingdomId,out enemy);RecordEvent("战争","指派战争负责官员",(official==null?"官员":official.Name)+"受命负责对"+(enemy==null?"敌国":enemy.Name)+"的守城、野战、攻城、拦截与后勤协调",enemyKingdomId);CoordinateWarAdministration(x,true);return true;
        }
        public bool RevokeWarOfficial(string enemyKingdomId,out string reason){reason="";if(WarAdministration==null){reason="战争行政系统尚未初始化";return false;}WarAdministration removed;if(!WarAdministration.Revoke(PlayerKingdomId,enemyKingdomId,out removed)){reason="该场战争当前没有已指派官员";return false;}Person official;Kingdom enemy;People.TryGetValue(removed.OfficialId,out official);Kingdoms.TryGetValue(enemyKingdomId,out enemy);RecordEvent("战争","撤销战争负责官员",(official==null?"原战争负责官员":official.Name)+"被撤销对"+(enemy==null?"敌国":enemy.Name)+"的战务职责；已下达军令继续执行，新的自动调度停止",enemyKingdomId);return true;}
        public bool SetWarOfficialFocus(string enemyKingdomId,WarAdministrationFocus focus,out string reason){reason="";WarAdministration x=WarAdministrationFor(PlayerKingdomId,enemyKingdomId);if(x==null){reason="请先指派战争负责官员";return false;}if(!WarAdministration.SetFocus(PlayerKingdomId,enemyKingdomId,focus)){reason="无法修改战务重点";return false;}x.LastAction="领主将战务重点调整为"+ChineseText.WarFocus(focus);CoordinateWarAdministration(x,true);return true;}

        public SiegeState SiegeForArmy(string armyId){foreach(SiegeState s in Sieges.All())if(s!=null&&s.AttackerArmyId==armyId&&!s.Captured)return s;return null;}

        public BattleSession ActiveBattleForArmy(string armyId) {
            if(string.IsNullOrEmpty(armyId))return null;
            foreach(BattleSession session in ActiveBattles.Values)if(session!=null&&!session.Completed&&(session.AttackerArmyId==armyId||session.DefenderArmyId==armyId))return session;
            return null;
        }
        public bool IsArmyInActiveBattle(string armyId){return ActiveBattleForArmy(armyId)!=null;}

        public BattleSession StartBattle(Army attacker,Army defender,string location,string siegeCityId="") {
            if(attacker==null||defender==null||attacker.Id==defender.Id)return null;
            BattleSession existing=ActiveBattleForArmy(attacker.Id);if(existing!=null)return existing;existing=ActiveBattleForArmy(defender.Id);if(existing!=null)return existing;
            Person ag,dg;People.TryGetValue(attacker.GeneralId,out ag);People.TryGetValue(defender.GeneralId,out dg);
            CommanderBehaviorProfile ap=CommanderSkillEffectEngine.Build(ag,Data),dp=CommanderSkillEffectEngine.Build(dg,Data);ApplyWarPlanning(attacker,ap);ApplyWarPlanning(defender,dp);
            City battleCity=!string.IsNullOrEmpty(siegeCityId)&&Cities.ContainsKey(siegeCityId)?Cities[siegeCityId]:FindCityByName(location);
            bool defenderLastStand=battleCity!=null&&Kingdoms.ContainsKey(defender.KingdomId)&&(Kingdoms[defender.KingdomId].CapitalCityId==battleCity.Id||Kingdoms[defender.KingdomId].CityIds.Count<=1);
            WorldTile battleTile=Map==null?null:Map.Get(attacker.X,attacker.Y);TerrainKind battleTerrain=battleTile==null?TerrainKind.Grass:battleTile.Terrain;
            BattleSession session=Combat.StartSession(attacker,defender,Day,160,ap,dp,Weather.RangedAccuracyMultiplier(),false,defenderLastStand,battleTerrain,Weather.Weather,location,IsNightTime);
            session.SiegeCityId=siegeCityId??"";ActiveBattles[session.Id]=session;attacker.LastBattleDay=Day;defender.LastBattleDay=Day;
            return session;
        }

        void TickActiveBattles(float dt) {
            int battleSeconds=Math.Max(1,(int)Math.Round(dt*8f));
            foreach(BattleSession session in new List<BattleSession>(ActiveBattles.Values)) {
                if(session==null){continue;}Army attacker,defender;
                if(!Armies.TryGetValue(session.AttackerArmyId,out attacker)||!Armies.TryGetValue(session.DefenderArmyId,out defender)){ActiveBattles.Remove(session.Id);continue;}
                Person ag,dg;People.TryGetValue(attacker.GeneralId,out ag);People.TryGetValue(defender.GeneralId,out dg);
                CommanderBehaviorProfile ap=CommanderSkillEffectEngine.Build(ag,Data),dp=CommanderSkillEffectEngine.Build(dg,Data);ApplyWarPlanning(attacker,ap);ApplyWarPlanning(defender,dp);
                if(!Combat.StepSession(session,attacker,defender,battleSeconds,ap,dp))continue;
                FinalizeBattle(attacker,defender,session.Report,ap,dp);
                ActiveBattles.Remove(session.Id);
                ResolveSiegeBattleOutcome(session,attacker,defender);
            }
        }

        void ResolveSiegeBattleOutcome(BattleSession session,Army attacker,Army defender) {
            if(session==null||string.IsNullOrEmpty(session.SiegeCityId))return;City city;if(!Cities.TryGetValue(session.SiegeCityId,out city))return;
            SiegeState siege=SiegeForArmy(attacker.Id);if(siege==null||siege.CityId!=city.Id)return;
            if(attacker.Order==ArmyOrder.Retreat||attacker.Morale<10f){Sieges.End(city.Id);return;}
            if(defender.Order==ArmyOrder.Retreat||Military.ReadySoldierCount(defender)<=0||defender.Morale<10f){siege.Captured=true;CaptureCity(attacker,city);return;}
            attacker.Order=ArmyOrder.Siege;
        }

        public BattleReport ResolveBattle(Army attacker, Army defender, string location) {
            Person ag,dg;People.TryGetValue(attacker.GeneralId,out ag);People.TryGetValue(defender.GeneralId,out dg);
            CommanderBehaviorProfile ap=CommanderSkillEffectEngine.Build(ag,Data),dp=CommanderSkillEffectEngine.Build(dg,Data);ApplyWarPlanning(attacker,ap);ApplyWarPlanning(defender,dp);
            City battleCity=FindCityByName(location);bool defenderLastStand=battleCity!=null&&Kingdoms.ContainsKey(defender.KingdomId)&&(Kingdoms[defender.KingdomId].CapitalCityId==battleCity.Id||Kingdoms[defender.KingdomId].CityIds.Count<=1);
            WorldTile battleTile=Map==null?null:Map.Get(attacker.X,attacker.Y);TerrainKind battleTerrain=battleTile==null?TerrainKind.Grass:battleTile.Terrain;
            BattleReport report=Combat.ResolveSkirmish(attacker,defender,Day,160,ap,dp,Weather.RangedAccuracyMultiplier(),false,defenderLastStand,battleTerrain,Weather.Weather);report.LocationName=location;
            attacker.LastBattleDay=Day;defender.LastBattleDay=Day;FinalizeBattle(attacker,defender,report,ap,dp);return report;
        }

        void FinalizeBattle(Army attacker,Army defender,BattleReport r,CommanderBehaviorProfile ap,CommanderBehaviorProfile dp) {
            if(attacker==null||defender==null||r==null)return;if(!Battles.Exists(delegate(BattleReport x){return x!=null&&x.Id==r.Id;}))Battles.Add(r);
            AssignCapturedPersonnel(attacker,defender.KingdomId);AssignCapturedPersonnel(defender,attacker.KingdomId);
            if(r.Captured>0)r.Highlights.Add("本战共有"+r.Captured+"人被俘，俘虏归属已写入人物状态");
            foreach(Squad squad in attacker.Squads)foreach(string id in squad.SoldierIds){Person p;if(People.TryGetValue(id,out p)){p.Battles++;p.Experience+=5;Population.UpdateMilitaryPotential(p);}}
            foreach(Squad squad in defender.Squads)foreach(string id in squad.SoldierIds){Person p;if(People.TryGetValue(id,out p)){p.Battles++;p.Experience+=5;Population.UpdateMilitaryPotential(p);}}
            Person attackGeneral,defenseGeneral;if(People.TryGetValue(attacker.GeneralId,out attackGeneral)&&attackGeneral!=null){attackGeneral.Battles++;attackGeneral.Experience+=12;ProgressCommanderSkills(attackGeneral);}if(People.TryGetValue(defender.GeneralId,out defenseGeneral)&&defenseGeneral!=null){defenseGeneral.Battles++;defenseGeneral.Experience+=12;ProgressCommanderSkills(defenseGeneral);}
            Military.PromoteVeterans(attacker);Military.PromoteVeterans(defender);
            attacker.BattleCount++;defender.BattleCount++;bool attackerWon=attacker.Morale>=defender.Morale;if(attackerWon){attacker.Wins++;defender.Losses++;}else{defender.Wins++;attacker.Losses++;}
            ResolveBattleLoot(attackerWon?attacker:defender,attackerWon?defender:attacker,r,attackerWon?ap:dp);
            attacker.FamousBattles.Add("第"+Day+"日 "+r.LocationName+"："+r.ResultText);defender.FamousBattles.Add("第"+Day+"日 "+r.LocationName+"："+r.ResultText);TrimBattleHistory(attacker);TrimBattleHistory(defender);ConsiderPromotions(attacker);ConsiderPromotions(defender);
            if(attacker.Order==ArmyOrder.Engage)attacker.Order=ArmyOrder.Hold;if(defender.Order==ArmyOrder.Engage)defender.Order=ArmyOrder.Hold;
            if(r.AttackerOrderlyRetreat||attacker.Morale<10)attacker.Order=ArmyOrder.Retreat;if(r.DefenderOrderlyRetreat||defender.Morale<10)defender.Order=ArmyOrder.Retreat;
            if(r.AttackerOrderlyRetreat&&ap.RetreatSupplyLoss>0){attacker.FoodDays=Math.Max(0,attacker.FoodDays*(1f-ap.RetreatSupplyLoss));r.Highlights.Add("进攻方为保持撤退速度抛弃部分辎重");}
            if(r.DefenderOrderlyRetreat&&dp.RetreatSupplyLoss>0){defender.FoodDays=Math.Max(0,defender.FoodDays*(1f-dp.RetreatSupplyLoss));r.Highlights.Add("守方为保持撤退速度抛弃部分辎重");}
            float aRout=defender.Morale<10 ? 1f : Mathx.Clamp((35f-defender.Morale)/35f,0f,1f),dRout=attacker.Morale<10 ? 1f : Mathx.Clamp((35f-attacker.Morale)/35f,0f,1f);
            if(defender.Order==ArmyOrder.Retreat)ResolvePursuitOrder(attacker,defender,ap,dp,aRout,r,"进攻方");
            if(attacker.Order==ArmyOrder.Retreat)ResolvePursuitOrder(defender,attacker,dp,ap,dRout,r,"守方");
        }

        PursuitMode EffectivePursuitMode(Army pursuer,CommanderBehaviorProfile own,CommanderBehaviorProfile enemy,float routSeverity) {
            if(pursuer==null)return PursuitMode.Stop;
            // 玩家军队严格执行玩家预先设置的追击军令；AI只提高判断质量，不获得额外属性。
            if(pursuer.KingdomId==PlayerKingdomId)return pursuer.PursuitMode;
            float cohesion=AverageCohesion(pursuer);SpecialUnitBehaviorProfile pursuitSpecial=SpecialUnitBehaviorEngine.ArmyProfile(pursuer,Data);
            bool chase=CommanderSkillEffectEngine.ShouldPursue(own,routSeverity*pursuitSpecial.PursuitCut,cohesion,.22f*enemy.Ambush*pursuitSpecial.PursuitRisk);
            if(!chase)return PursuitMode.Stop;
            float safety=own.Scout/Math.Max(.65f,enemy.Ambush);
            return cohesion>=68f&&pursuer.Fatigue<=55f&&pursuer.FoodDays>=1.5f&&safety>=1.02f ? PursuitMode.Full : PursuitMode.Limited;
        }

        void ResolvePursuitOrder(Army pursuer,Army routed,CommanderBehaviorProfile own,CommanderBehaviorProfile enemy,float routSeverity,BattleReport report,string label) {
            if(pursuer==null||routed==null||report==null)return;
            PursuitMode mode=EffectivePursuitMode(pursuer,own,enemy,routSeverity);
            if(mode==PursuitMode.Stop){report.Highlights.Add(label+"执行停止追击军令，保持阵线并收拢伤兵");pursuer.Order=ArmyOrder.Hold;return;}
            int strikes=mode==PursuitMode.Full?28:12;
            pursuer.Order=mode==PursuitMode.Full?ArmyOrder.PursueFull:ArmyOrder.PursueLimited;
            int loss=Combat.ResolvePursuit(pursuer,routed,strikes,own);
            if(mode==PursuitMode.Limited){
                pursuer.Fatigue=Mathx.Clamp(pursuer.Fatigue+1.5f*own.PursuitFatigueCost,0f,100f);
                foreach(Squad q in pursuer.Squads)q.Cohesion=Math.Max(8f,q.Cohesion-1.5f);
                report.Highlights.Add(label+"执行有限追击，追加击溃"+loss+"人，并保持主要阵线");
            } else {
                pursuer.Fatigue=Mathx.Clamp(pursuer.Fatigue+5.5f*own.PursuitFatigueCost,0f,100f);
                foreach(Squad q in pursuer.Squads)q.Cohesion=Math.Max(5f,q.Cohesion-6f/Math.Max(.75f,own.Discipline));
                report.Highlights.Add(label+"执行全面追击，追加击溃"+loss+"人；追击部队疲劳上升、阵型明显拉长");
                float terrainRisk=0f;WorldTile t=Map==null?null:Map.Get(pursuer.X,pursuer.Y);if(t!=null&&(t.Terrain==TerrainKind.Forest||t.Terrain==TerrainKind.Hill||t.Terrain==TerrainKind.MountainPass||t.Terrain==TerrainKind.Marsh))terrainRisk=.11f;
                SpecialUnitBehaviorProfile pursuitSpecial=SpecialUnitBehaviorEngine.ArmyProfile(pursuer,Data),routedSpecial=SpecialUnitBehaviorEngine.ArmyProfile(routed,Data);float counterRisk=Mathx.Clamp((.05f+terrainRisk+.16f*(enemy.Ambush*routedSpecial.ScoutAmbush/Math.Max(.7f,own.Scout)-1f)+(65f-AverageCohesion(pursuer))*.0025f)*pursuitSpecial.PursuitRisk,0f,.48f);
                if(_rng.Chance(counterRisk)){int counter=Combat.ResolvePursuit(routed,pursuer,6,enemy);report.Highlights.Add("全面追击阵型拉长，遭败军反伏击，追兵追加损失"+counter+"人");pursuer.Morale=Math.Max(0f,pursuer.Morale-5f);}
            }
            pursuer.Order=ArmyOrder.Hold;
        }

        void ResolveBattleLoot(Army winner,Army loser,BattleReport report,CommanderBehaviorProfile command){
            if(winner==null||loser==null||report==null)return;if(command==null)command=new CommanderBehaviorProfile();
            Kingdom wk;PolicyRuntimeProfile pp=Kingdoms.TryGetValue(winner.KingdomId,out wk)?Policies.RuntimeProfile(wk):new PolicyRuntimeProfile();
            float control=Mathx.Clamp(command.LootControl,.75f,1.6f);int loot=Math.Min(Math.Max(0,(int)Math.Round(loser.Finance.Gold*(.16f+.04f*(control-1f)))),120);
            if(loot>0){
                loser.Finance.Gold-=loot;float treasuryRate=Mathx.Clamp((pp.LootToTreasury-1f)*.8f,0f,.35f);int treasuryShare=(int)Math.Round(loot*treasuryRate);int armyShare=loot-treasuryShare;winner.Finance.Gold+=armyShare;if(wk!=null)wk.Treasury+=treasuryShare;report.LootGold=loot;
                report.Highlights.Add(treasuryShare>0?"缴获军资"+loot+"金币，其中"+treasuryShare+"金币按战利品入库政策归国库":"胜军在军纪约束下缴获军资"+loot+"金币");
            }
            if(pp.EquipmentRecovery>1f){int repaired=0;foreach(Squad sq in winner.Squads)foreach(string pid in sq.SoldierIds){Person soldier;if(!People.TryGetValue(pid,out soldier))continue;foreach(string eid in soldier.EquipmentIds){EquipmentInstance item;if(!Equipment.TryGetValue(eid,out item)||item.Durability>=item.MaxDurability)continue;int gain=Math.Max(1,(int)Math.Round(item.MaxDurability*Math.Min(.18f,(pp.EquipmentRecovery-1f)*.35f)));item.Durability=Math.Min(item.MaxDurability,item.Durability+gain);if(++repaired>=6)break;}if(repaired>=6)break;}if(repaired>0)report.Highlights.Add("战场回收可用军械零件，修复"+repaired+"件己方装备");}
            Person victor;People.TryGetValue(winner.GeneralId,out victor);if(victor==null)return;
            foreach(Squad sq in loser.Squads)foreach(string pid in sq.SoldierIds){Person fallen;if(!People.TryGetValue(pid,out fallen)||(fallen.Alive&&fallen.Injury!=InjuryState.Captured))continue;foreach(string eid in new List<string>(fallen.EquipmentIds)){EquipmentInstance item;if(!Equipment.TryGetValue(eid,out item)||!item.Unique)continue;if(Gear.TransferUnique(item,fallen,victor,"第"+Day+"日于"+report.LocationName+"缴获")){report.Highlights.Add("缴获"+(string.IsNullOrEmpty(item.DisplayName)?"传奇装备":item.DisplayName));return;}}}
        }

        void TickUniqueCrafting(){foreach(City c in Cities.Values){if(!HasBuilding(c,BuildingKind.Smithy)||!HasBuilding(c,BuildingKind.Armory)||c.Iron<18||c.Wood<8)continue;int smiths=0;foreach(string id in c.PersonIds){Person p;if(People.TryGetValue(id,out p)&&p.Alive&&p.Job==JobKind.Smith)smiths++;}if(smiths<2)continue;Kingdom k=Kingdoms[c.KingdomId];float chance=.025f+Math.Min(.08f,smiths*.008f)+Math.Min(.05f,k.Prestige*.0005f);if(!_rng.Chance(chance))continue;int qLevel=k.Prestige>150&&_rng.Chance(.08f)?7:(k.Prestige>90&&_rng.Chance(.22f)?6:5);EquipmentQualityDef q=EquipmentQuality(qLevel);EquipmentTypeDef type=null;int pick=_rng.Range(0,Math.Max(1,Data.EquipmentTypes.Count)),n=0;foreach(EquipmentTypeDef e in Data.EquipmentTypes.Values)if(n++==pick){type=e;break;}if(type==null||q==null)continue;c.Iron-=18;c.Wood-=8;string maker="";foreach(string id in c.PersonIds){Person sp;if(People.TryGetValue(id,out sp)&&sp.Job==JobKind.Smith){maker=sp.Name;break;}}EquipmentInstance item=Gear.CreateHistoric(type,q,c,maker,string.IsNullOrEmpty(maker)?q.Name+"·"+type.Name:maker+"所铸"+type.Name);Person recipient=null;foreach(Army a in Armies.Values)if(a.KingdomId==c.KingdomId&&Mathx.Manhattan(a.X,a.Y,c.X,c.Y)<=1){People.TryGetValue(a.GeneralId,out recipient);if(recipient!=null)break;}if(recipient==null)People.TryGetValue(k.LordId,out recipient);if(item!=null&&recipient!=null)Gear.Equip(recipient,item,item.Slot);}}
        void ResolveUniqueInheritance(){foreach(EquipmentInstance e in Equipment.Values){if(!e.Unique||string.IsNullOrEmpty(e.OwnerId))continue;Person owner;if(!People.TryGetValue(e.OwnerId,out owner)||owner.Alive)continue;Person heir=null;if(!string.IsNullOrEmpty(owner.FamilyId))foreach(Person p in People.Values)if(p.Alive&&p.Id!=owner.Id&&p.FamilyId==owner.FamilyId){heir=p;break;}if(heir!=null)Gear.TransferUnique(e,owner,heir,"第"+Day+"日由"+owner.Name+"传承给"+heir.Name);}}

        void TickRecoveryAndRetirement(){
            foreach(Person p in new List<Person>(People.Values)){
                if(!p.Alive)continue;if(p.Injury==InjuryState.Captured)continue;Kingdom k;PolicyRuntimeProfile pp=Kingdoms.TryGetValue(p.KingdomId,out k)?Policies.RuntimeProfile(k):new PolicyRuntimeProfile();
                if(p.Injury==InjuryState.Light||p.Injury==InjuryState.Heavy||p.Injury==InjuryState.Incapacitated){
                    p.InjuryDays++;Army army=ArmyContaining(p.Id);bool home=false;if(army!=null){City c=NearestOwnCity(army.KingdomId,army.X,army.Y);home=c!=null&&Mathx.Manhattan(army.X,army.Y,c.X,c.Y)<=1;}else home=true;
                    int heal=p.Injury==InjuryState.Light?16:(p.Injury==InjuryState.Heavy?9:5);if(home)heal+=5;heal=Math.Max(1,(int)Math.Round(heal*TraitEffectEngine.RecoveryMultiplier(p,Data)*pp.MedicalRecovery));if(army!=null&&army.MedicalSupplies>0&&p.Injury!=InjuryState.Light){army.MedicalSupplies--;heal+=Math.Max(4,(int)Math.Round(6f*pp.MedicalRecovery));army.Finance.MedicalCost++;}p.Stats.Life=Math.Min(100,p.Stats.Life+heal);
                    int need=p.Injury==InjuryState.Light?2:(p.Injury==InjuryState.Heavy?6:10);need=Math.Max(1,(int)Math.Ceiling(need/Mathx.Clamp(pp.MedicalRecovery,.75f,1.7f)));if(home&&p.InjuryDays>=need&&p.Stats.Life>=55){p.Injury=InjuryState.None;p.InjuryDays=0;}
                }
                int retirementAge=Mathx.Clamp((int)Math.Round(55f+(pp.VeteranRetention-1f)*18f),52,65);if(Day%30==0&&p.Age>=retirementAge&&p.Job==JobKind.Soldier&&p.Battles>=3&&p.Injury==InjuryState.None){Army a=ArmyContaining(p.Id);City home=a==null?null:NearestOwnCity(a.KingdomId,a.X,a.Y);if(a!=null&&home!=null&&Mathx.Manhattan(a.X,a.Y,home.X,home.Y)<=1){RemoveFromArmy(a,p.Id);p.Job=JobKind.Unemployed;p.Class=p.Noble?SocialClass.Noble:SocialClass.Commoner;p.Stats.Prestige+=2;}}
                if(Day%360==0&&p.Age<90)p.Age++;
            }
        }

        bool HasProposalFor(City c,BuildingKind kind){foreach(Proposal p in Proposals.Queue)if(p.Kind==ProposalKind.Construction&&p.TargetId==c.Id&&p.DataId==kind.ToString()&&(p.State==ProposalState.Pending||p.State==ProposalState.Approved||p.State==ProposalState.Running))return true;return false;}
        bool HasRepairProposal(Building b){string key="repair:"+b.Id;foreach(Proposal p in Proposals.Queue)if(p.Kind==ProposalKind.Construction&&p.DataId==key&&(p.State==ProposalState.Pending||p.State==ProposalState.Approved||p.State==ProposalState.Running))return true;return false;}
        void EnsureRepairProposal(City c){Person o=FindOfficial(c);if(o==null)return;foreach(string id in c.BuildingIds){Building b;if(!Buildings.TryGetValue(id,out b)||b.Durability>=55||HasRepairProposal(b))continue;int missing=Math.Max(1,b.MaxDurability-b.Durability);var p=new Proposal{Id=Ids.Next("REPQ"),Title="维修"+b.Name,Description=o.Name+"奏请修复受损的"+b.Name+"。",Kind=ProposalKind.Construction,DataId="repair:"+b.Id,ProposerId=o.Id,TargetId=c.Id,CostGold=Math.Max(20,missing*2),DurationDays=0,SubmittedDay=Day};p.SetEffect("seconds",Math.Max(25,missing));Proposals.Submit(p,100);return;}}
        void EnsureDevelopmentProposal(City c){
            Person o=FindOfficial(c);if(o==null)return;
            if(HighestBuildingLevel(c,BuildingKind.Barracks)==1&&!HasProposalFor(c,BuildingKind.Barracks)){SubmitConstructionProposal(c,o,BuildingKind.Barracks,"扩建军营至二级",85,230);return;}
            int desiredWall=DesiredFortificationRadius(c);
            if(c.FortificationRadius>0&&desiredWall>c.FortificationRadius&&!HasProposalFor(c,BuildingKind.Wall)){SubmitConstructionProposal(c,o,BuildingKind.Wall,"扩建城墙外郭",115+desiredWall*4,360+desiredWall*28);return;}
            CityCultureProfile culture=CityCultureEffectEngine.Build(c);BuildingKind[] order={
                culture.PreferredA,culture.PreferredB,
                BuildingKind.Farm,BuildingKind.Warehouse,BuildingKind.Lumberyard,
                BuildingKind.Shop,BuildingKind.BunShop,BuildingKind.Market,
                BuildingKind.Apartment,BuildingKind.Villa,BuildingKind.Manor,
                BuildingKind.Stable,BuildingKind.Mill,BuildingKind.Bakery,
                BuildingKind.Smithy,BuildingKind.Armory,BuildingKind.TrainingGround,
                BuildingKind.Road,BuildingKind.Wall
            };
            for(int i=0;i<order.Length;i++){BuildingKind kind=order[i];if(kind==BuildingKind.Wall&&c.FortificationRadius>0)continue;if(HasBuilding(c,kind)||HasProposalFor(c,kind))continue;int cost=ConstructionCost(kind);int sec=ConstructionSeconds(kind);SubmitConstructionProposal(c,o,kind,"修建"+ChineseText.Building(kind),sec,cost);break;}
        }
        int DesiredFortificationRadius(City c){return Math.Max(5,Math.Min(12,Population.LivingPopulation(c)/25+4));}
        int InferFortificationRadius(City c){int r=0;foreach(string id in c.BuildingIds){Building b;if(!Buildings.TryGetValue(id,out b)||!IsFortification(b))continue;r=Math.Max(r,Math.Max(Math.Abs(b.X-c.X),Math.Abs(b.Y-c.Y)));}return r;}
        bool IsFortification(Building b){return b!=null&&(b.Kind==BuildingKind.Wall||b.Kind==BuildingKind.Gate||b.Kind==BuildingKind.Tower);}
        List<Building> FortificationsFor(City c,bool outerOnly){
            var list=new List<Building>();if(c==null)return list;int r=c.FortificationRadius>0?c.FortificationRadius:InferFortificationRadius(c);if(c.FortificationRadius<=0&&r>0)c.FortificationRadius=r;
            foreach(string id in c.BuildingIds){Building b;if(!Buildings.TryGetValue(id,out b)||!IsFortification(b))continue;int br=Math.Max(Math.Abs(b.X-c.X),Math.Abs(b.Y-c.Y));if(!outerOnly||r<=0||br==r)list.Add(b);}
            return list;
        }
        int DesiredGateCount(City c){int pop=c==null?0:Population.LivingPopulation(c);if(pop<140)return 2;if(pop<260)return 3;return 4;}
        bool GateNorthSouth(int offset,int gateCount){return offset==0;}
        bool GateEast(int offset,int gateCount){return gateCount>=3&&offset==0;}
        bool GateWest(int offset,int gateCount){return gateCount>=4&&offset==0;}
        void BuildWallRing(City c){
            int r=DesiredFortificationRadius(c);int old=c.FortificationRadius>0?c.FortificationRadius:InferFortificationRadius(c);if(old>=r&&old>0){c.FortificationRadius=old;return;}
            if(old>0){foreach(Building b in FortificationsFor(c,true))if(!b.Ruined)b.Name="旧"+ChineseText.Building(b.Kind);} 
            string looseId=Construction.LastCompletedBuildingId;Building loose;if(!string.IsNullOrEmpty(looseId)&&Buildings.TryGetValue(looseId,out loose)&&loose.CityId==c.Id&&loose.Kind==BuildingKind.Wall){int lr=Math.Max(Math.Abs(loose.X-c.X),Math.Abs(loose.Y-c.Y));if(lr!=r){c.BuildingIds.Remove(loose.Id);Buildings.Remove(loose.Id);}}
            c.FortificationRadius=r;c.FortificationPlanVersion=Math.Max(1,c.FortificationPlanVersion+1);c.LastFortificationReplanDay=Day;int gates=DesiredGateCount(c);
            for(int d=-r;d<=r;d++){
                CreateFortification(c,c.X+d,c.Y-r,GateNorthSouth(d,gates)?BuildingKind.Gate:BuildingKind.Wall);
                CreateFortification(c,c.X+d,c.Y+r,GateNorthSouth(d,gates)?BuildingKind.Gate:BuildingKind.Wall);
                if(d>-r&&d<r){
                    CreateFortification(c,c.X-r,c.Y+d,GateWest(d,gates)?BuildingKind.Gate:BuildingKind.Wall);
                    CreateFortification(c,c.X+r,c.Y+d,GateEast(d,gates)?BuildingKind.Gate:BuildingKind.Wall);
                }
            }
            CreateFortification(c,c.X-r,c.Y-r,BuildingKind.Tower);CreateFortification(c,c.X+r,c.Y-r,BuildingKind.Tower);CreateFortification(c,c.X-r,c.Y+r,BuildingKind.Tower);CreateFortification(c,c.X+r,c.Y+r,BuildingKind.Tower);
        }
        void CreateFortification(City c,int x,int y,BuildingKind kind){WorldTile t=Map.Get(x,y);if(t==null||t.Terrain==TerrainKind.DeepWater)return;foreach(string id in c.BuildingIds){Building old;if(Buildings.TryGetValue(id,out old)&&old.X==x&&old.Y==y&&IsFortification(old))return;}Kingdom fk;PolicyRuntimeProfile fp=Kingdoms.TryGetValue(c.KingdomId,out fk)?Policies.RuntimeProfile(fk):new PolicyRuntimeProfile();OfficialRuntimeProfile op=OfficialProfileFor(c);int fortDur=(int)Math.Round(160f*fp.Fortification*op.Fortification);var b=new Building{Id=Ids.Next("FORT"),CityId=c.Id,Name=ChineseText.Building(kind),Kind=kind,X=x,Y=y,Durability=fortDur,MaxDurability=fortDur,Complete=true};Buildings[b.Id]=b;c.BuildingIds.Add(b.Id);}
        void ApplyRejectionPoliticalCost(Proposal p){if(p==null||string.IsNullOrEmpty(p.ProposerId))return;Person proposer;if(!People.TryGetValue(p.ProposerId,out proposer))return;proposer.Stats.Loyalty=Math.Max(0,proposer.Stats.Loyalty-2);Family f;if(!string.IsNullOrEmpty(proposer.FamilyId)&&Families.TryGetValue(proposer.FamilyId,out f))f.Loyalty=Math.Max(0,f.Loyalty-1);}
        void TickFamilyPolitics(){var snapshot=new List<Family>(Families.Values);foreach(Family f in snapshot){Person lord=null;City city;Kingdom k;if(!Cities.TryGetValue(f.HomeCityId,out city)||!Kingdoms.TryGetValue(city.KingdomId,out k)||!People.TryGetValue(k.LordId,out lord))continue;int arrears=0;foreach(Army a in Armies.Values)if(a.KingdomId==k.Id)arrears=Math.Max(arrears,a.Finance.MonthsInArrears);PolicyRuntimeProfile pp=Policies.RuntimeProfile(k);float risk=FamilySystem.RebellionRisk(f,lord.Stats.Prestige,arrears,Math.Max(0,55-f.Loyalty))/Math.Max(1f,pp.FamilyStability);if(pp.PublicOrder>1f)f.Loyalty=Math.Min(100,f.Loyalty+(int)Math.Round((pp.PublicOrder-1f)*3f));if(risk>=92f&&f.FamilyType!="王族")TriggerFamilyRebellion(f,city,k);}}
        void TriggerFamilyRebellion(Family f,City city,Kingdom oldKingdom){Person leader=null;foreach(string id in f.MemberIds){Person p;if(People.TryGetValue(id,out p)&&p.Alive&&p.Noble){leader=p;break;}}if(leader==null)return;oldKingdom.CityIds.Remove(city.Id);Kingdom rebel=new Kingdom{Id=Ids.Next("K"),Name=f.Name+"义军",LordId=leader.Id,CapitalCityId=city.Id,Treasury=Math.Max(250,oldKingdom.Treasury/6)};oldKingdom.Treasury=Math.Max(0,oldKingdom.Treasury-rebel.Treasury);rebel.CityIds.Add(city.Id);Kingdoms[rebel.Id]=rebel;city.KingdomId=rebel.Id;foreach(string pid in city.PersonIds){Person p;if(People.TryGetValue(pid,out p))p.KingdomId=rebel.Id;}f.Loyalty=70;_ai[rebel.Id]=new AiOwner(ComputerDifficulty,Seed+Day+rebel.Id.Length*31);Diplomacy.DeclareWar(oldKingdom.Id,rebel.Id,Day);RecordEvent("反叛","家族举兵",f.Name+"在"+city.Name+"举兵反叛，建立"+rebel.Name+"并与"+oldKingdom.Name+"开战",rebel.Id);if(HasBuilding(city,BuildingKind.Barracks)){EnsureGeneralIdentity(leader);Army a=Military.CreateArmy(rebel,city,leader,f.Name+"义军");if(a!=null){a.FormedDay=Day;a.Finance.Gold=Math.Min(260,rebel.Treasury);rebel.Treasury-=a.Finance.Gold;Military.Recruit(a,city,FirstBasicUnitId(),Math.Min(18,Population.CivilianPopulation(city)/4),8);PrepareArmyPersonnelAndEquipment(a,city);}}}
        void TickDiplomaticOffers(){
            if(Day%5!=0||string.IsNullOrEmpty(PlayerKingdomId))return;
            foreach(Kingdom otherKingdom in Kingdoms.Values){
                if(otherKingdom.Id==PlayerKingdomId||otherKingdom.Status==KingdomStatus.Eliminated)continue;DiplomacyRelation r=Diplomacy.Get(PlayerKingdomId,otherKingdom.Id);if(r.State==DiplomacyState.Vassal)continue;if(Day%30==0){if(r.State==DiplomacyState.Trade)Diplomacy.ChangeOpinion(PlayerKingdomId,otherKingdom.Id,2);else if(r.State==DiplomacyState.NonAggression||r.State==DiplomacyState.Alliance)Diplomacy.ChangeOpinion(PlayerKingdomId,otherKingdom.Id,1);}Person proposer=FindDiplomaticProposer(otherKingdom);if(proposer==null)continue;string action=null,title=null,description=null;
                if(r.State==DiplomacyState.War){Army enemy=FirstArmy(otherKingdom.Id);if(enemy!=null&&enemy.Morale<=28){action="truce";title="停战奏议";description=otherKingdom.Name+"遣使请求停战三十日。";}}
                else if(r.State==DiplomacyState.Neutral&&r.Opinion>=0&&Day%15==0){action="trade";title="通商奏议";description=otherKingdom.Name+"请求开放商路与互市。";}
                else if(r.State==DiplomacyState.Trade&&r.Opinion>=12&&Day%20==0){action="nonaggression";title="互不侵犯奏议";description=otherKingdom.Name+"请求订立四十五日互不侵犯约。";}
                else if((r.State==DiplomacyState.Trade||r.State==DiplomacyState.NonAggression)&&r.Opinion>=25&&Day%30==0){action="alliance";title="结盟奏议";description=otherKingdom.Name+"请求正式结盟。";}
                if(action==null)continue;bool exists=false;foreach(Proposal q in Proposals.Queue)if(q.Kind==ProposalKind.Diplomacy&&q.State==ProposalState.Pending&&q.DataId==action+":"+PlayerKingdomId){exists=true;break;}if(exists)continue;
                var p=new Proposal{Id=Ids.Next("DIP"),Title=title,Description=description,Kind=ProposalKind.Diplomacy,DataId=action+":"+PlayerKingdomId,ProposerId=proposer.Id,TargetId=PlayerKingdomId,SubmittedDay=Day};Proposals.Submit(p,100);
            }
        }
        Person FindDiplomaticProposer(Kingdom k){foreach(Person p in People.Values)if(p.Alive&&p.KingdomId==k.Id&&p.Class==SocialClass.Official)return p;Person lord;return People.TryGetValue(k.LordId,out lord)&&lord.Alive?lord:null;}

        void AssignCapturedPersonnel(Army army,string captorKingdomId){
            if(army==null||string.IsNullOrEmpty(captorKingdomId))return;foreach(Squad sq in army.Squads){for(int i=sq.SoldierIds.Count-1;i>=0;i--){string id=sq.SoldierIds[i];Person p;if(People.TryGetValue(id,out p)&&p.Alive&&p.Injury==InjuryState.Captured){MarkCaptured(p,captorKingdomId);sq.SoldierIds.RemoveAt(i);}}Person o;if(!string.IsNullOrEmpty(sq.OfficerId)&&People.TryGetValue(sq.OfficerId,out o)&&o.Alive&&o.Injury==InjuryState.Captured){MarkCaptured(o,captorKingdomId);sq.OfficerId="";}}
        }
        void MarkCaptured(Person p,string captorKingdomId){if(p==null)return;p.CapturedByKingdomId=captorKingdomId;p.CapturedDay=Day;int status=p.Class==SocialClass.General?90:(p.Class==SocialClass.Officer||p.Noble?55:18);p.RansomValue=Math.Max(status,status+p.Stats.Prestige/2+p.Experience/20);}
        void ReleaseCaptive(Person p){if(p==null)return;p.Injury=InjuryState.Light;p.InjuryDays=0;p.Stats.Life=Math.Max(35,p.Stats.Life);p.CapturedByKingdomId="";p.CapturedDay=-1;p.RansomValue=0;Kingdom home;if(Kingdoms.TryGetValue(p.KingdomId,out home)){City c;if(Cities.TryGetValue(home.CapitalCityId,out c)){p.CityId=c.Id;if(!c.PersonIds.Contains(p.Id))c.PersonIds.Add(p.Id);}}}
        void TickCaptivity(){
            foreach(Person p in People.Values){if(!p.Alive||p.Injury!=InjuryState.Captured)continue;if(string.IsNullOrEmpty(p.CapturedByKingdomId)||p.CapturedByKingdomId==p.KingdomId){ReleaseCaptive(p);continue;}Kingdom home,captor;if(!Kingdoms.TryGetValue(p.KingdomId,out home)||!Kingdoms.TryGetValue(p.CapturedByKingdomId,out captor)){ReleaseCaptive(p);continue;}PolicyRuntimeProfile captivePolicy=Policies.RuntimeProfile(home);DiplomacyRelation rel=Diplomacy.Get(home.Id,captor.Id);int wait=Math.Max(1,(int)Math.Ceiling(2f/Mathx.Clamp(captivePolicy.CaptiveRecovery,.75f,1.7f)));if(rel.State==DiplomacyState.War||Day-Math.Max(0,p.CapturedDay)<wait)continue;int adjustedRansom=(int)Math.Round(p.RansomValue/Mathx.Clamp(captivePolicy.CaptiveRecovery,.8f,1.6f));int ransom=Math.Min(Math.Max(0,adjustedRansom),Math.Max(0,home.Treasury/8));if(ransom>0&&p.Noble){home.Treasury-=ransom;captor.Treasury+=ransom;}ReleaseCaptive(p);}
        }

        Army ArmyContaining(string personId) { foreach (Army a in Armies.Values) { if (a.GeneralId == personId || a.SubGeneralIds.Contains(personId)) return a; foreach (Squad s in a.Squads) if (s.OfficerId == personId || s.SoldierIds.Contains(personId)) return a; } return null; }
        void AssignOfficerToSquad(Army a,Person p){if(a==null||p==null)return;foreach(Squad s in a.Squads)if(s.SoldierIds.Contains(p.Id)){s.OfficerId=p.Id;s.SoldierIds.Remove(p.Id);return;}foreach(Squad s in a.Squads)if(string.IsNullOrEmpty(s.OfficerId)){s.OfficerId=p.Id;return;}}
        void MakeSeniorDeputy(Person p){Army a=ArmyContaining(p.Id);if(a==null||p==null)return;p.Class=SocialClass.Officer;p.Job=JobKind.Officer;EnsureGeneralIdentity(p);if(!a.SubGeneralIds.Contains(p.Id))a.SubGeneralIds.Add(p.Id);}
        void RemoveFromArmy(Army a,string personId){if(a==null)return;a.SubGeneralIds.Remove(personId);foreach(Squad s in a.Squads){if(s.OfficerId==personId)s.OfficerId="";s.SoldierIds.Remove(personId);}}
        void ConsiderPromotions(Army a){if(a==null)return;foreach(Squad s in a.Squads){foreach(string id in new List<string>(s.SoldierIds)){Person p;if(People.TryGetValue(id,out p))Promotions.ConsiderOfficer(p,Day);}if(!string.IsNullOrEmpty(s.OfficerId)){Person o;if(People.TryGetValue(s.OfficerId,out o)){Promotions.ConsiderNoble(o,Day);Promotions.ConsiderIndependentGeneral(o,Day);}}}}
        void TrimBattleHistory(Army a){while(a.FamousBattles.Count>40)a.FamousBattles.RemoveAt(0);}
        void SettleMilitaryMonth(){
            foreach(Army a in Armies.Values){
                Kingdom k;PolicyRuntimeProfile pp=Kingdoms.TryGetValue(a.KingdomId,out k)?Policies.RuntimeProfile(k):new PolicyRuntimeProfile();int need=MilitaryFinanceSystem.MonthlyCost(a.Finance,pp.ArmyUpkeepCost);
                if(a.Finance.Gold<need&&k!=null){int missing=need-a.Finance.Gold;int reserve=Math.Max(120,k.Treasury/5);int grantLimit=Math.Max(0,(int)Math.Round(Math.Max(0,k.Treasury-reserve)*Mathx.Clamp(.20f+.35f*(pp.ArmyFinanceReliability-1f),.12f,.55f)));int loan=Math.Min(missing,grantLimit);if(loan>0&&Economy.Pay(k,loan))MilitaryFinanceSystem.BorrowFromLord(a.Finance,loan);}
                if(a.Finance.Gold<need){int request=need-a.Finance.Gold;int merchantLoan=Merchants.EmergencyLoan(a.GeneralId,a.Finance,(int)Math.Round(request*Mathx.Clamp(pp.MerchantInvestmentCapacity,.75f,1.5f)));if(merchantLoan>0)need=MilitaryFinanceSystem.MonthlyCost(a.Finance,pp.ArmyUpkeepCost);}
                int shortfall=MilitaryFinanceSystem.PayMonth(a.Finance,pp.ArmyUpkeepCost);float risk=MilitaryFinanceSystem.PoliticalRisk(a.Finance,pp.DebtGrace);Person general;if(People.TryGetValue(a.GeneralId,out general)){int loyaltyLoss=(int)Math.Ceiling(risk*(shortfall>0?7f:3f));general.Stats.Loyalty=Math.Max(0,general.Stats.Loyalty-loyaltyLoss);Family family;if(!string.IsNullOrEmpty(general.FamilyId)&&Families.TryGetValue(general.FamilyId,out family))family.Loyalty=Math.Max(0,family.Loyalty-Math.Max(0,loyaltyLoss/2));}
                if(k!=null&&risk>.45f)k.Prestige=Math.Max(0,k.Prestige-(int)Math.Ceiling(risk*3f));
                if(k!=null&&a.Finance.Gold>need*2&&a.Finance.DebtToLord>0){int repay=Math.Min(a.Finance.DebtToLord,Math.Max(0,a.Finance.Gold-need*2));a.Finance.Gold-=repay;a.Finance.DebtToLord-=repay;k.Treasury+=repay;}
            }
        }
        bool HasActiveProject(City c,BuildingKind kind){foreach(ConstructionProject cp in Projects.Values)if(cp.CityId==c.Id&&cp.Kind==kind&&cp.Stage!=ConstructionStage.Complete)return true;return false;}
        bool StartCivilianBuilding(City c,BuildingKind kind,int wood,int stone,int seconds,int workers){if(c==null||HasActiveProject(c,kind)||c.Wood<wood||c.Stone<stone)return false;c.Wood-=wood;c.Stone-=stone;GridPoint site=Planning==null?new GridPoint(c.X,c.Y):Planning.FindBuildSite(c,kind);ConstructionProject cp=Construction.Start(c,null,kind,site.X,site.Y,seconds);Construction.AssignWorkers(cp,c,workers);return true;}
        int BuildingCount(City c,BuildingKind kind){int n=0;if(c==null)return n;foreach(string id in c.BuildingIds){Building b;if(Buildings.TryGetValue(id,out b)&&b!=null&&!b.Ruined&&b.Kind==kind)n++;}return n;}
        void TickCivilianBuilding(){
            foreach(City c in Cities.Values){if(Planning!=null)Planning.EnsureStarterZones(c,Day);int pop=Population.LivingPopulation(c);float housing=pop/(float)Math.Max(1,c.PopulationCapacity);
                if(housing>.88f){BuildingKind home=pop>240?BuildingKind.Apartment:(pop>140&&BuildingCount(c,BuildingKind.Villa)<2?BuildingKind.Villa:BuildingKind.House);if(StartCivilianBuilding(c,home,home==BuildingKind.Apartment?15:10,home==BuildingKind.Villa?8:4,home==BuildingKind.Apartment?65:50,3))continue;}
                if(c.FarmJobs<Math.Max(10,pop/4)&&StartCivilianBuilding(c,BuildingKind.Farm,5,0,35,3))continue;
                if(c.LoggingJobs<Math.Max(6,pop/8)&&StartCivilianBuilding(c,BuildingKind.Lumberyard,7,2,45,3))continue;
                if(BuildingCount(c,BuildingKind.Granary)>0&&BuildingCount(c,BuildingKind.Warehouse)==0&&StartCivilianBuilding(c,BuildingKind.Warehouse,10,5,55,3))continue;
                if(BuildingCount(c,BuildingKind.Farm)>0&&BuildingCount(c,BuildingKind.Mill)==0&&StartCivilianBuilding(c,BuildingKind.Mill,9,4,55,3))continue;
                if(BuildingCount(c,BuildingKind.Mill)>0&&BuildingCount(c,BuildingKind.Bakery)==0&&StartCivilianBuilding(c,BuildingKind.Bakery,8,3,45,2))continue;
                if(c.MarketJobs<Math.Max(6,pop/7)){BuildingKind shop=BuildingCount(c,BuildingKind.BunShop)<=BuildingCount(c,BuildingKind.Shop)?BuildingKind.BunShop:BuildingKind.Shop;if(StartCivilianBuilding(c,shop,6,2,38,2))continue;}
                if(pop>180&&BuildingCount(c,BuildingKind.Manor)==0)StartCivilianBuilding(c,BuildingKind.Manor,18,12,80,4);
            }
        }

        public CityZone DesignatePlayerZone(ZoneKind kind){Kingdom k;City c;if(string.IsNullOrEmpty(PlayerKingdomId)||!Kingdoms.TryGetValue(PlayerKingdomId,out k)||!Cities.TryGetValue(k.CapitalCityId,out c)||Planning==null)return null;return Planning.AddSuggested(c,kind,true,Day);}

        float AverageCohesion(Army a) { if (a == null || a.Squads.Count == 0) return 0f; float sum = 0f; foreach (Squad s in a.Squads) sum += s.Cohesion / 100f; return sum / a.Squads.Count; }

        string ProposalKingdom(Proposal p) {
            if (p == null) return null;
            Person proposer; if (!string.IsNullOrEmpty(p.ProposerId) && People.TryGetValue(p.ProposerId, out proposer)) return proposer.KingdomId;
            City city; if (!string.IsNullOrEmpty(p.TargetId) && Cities.TryGetValue(p.TargetId, out city)) return city.KingdomId;
            Army army; if (!string.IsNullOrEmpty(p.TargetId) && Armies.TryGetValue(p.TargetId, out army)) return army.KingdomId;
            return null;
        }

        City FindCityByName(string name){foreach(City c in Cities.Values)if(c.Name==name)return c;return null;}
        Army DefenderArmyAtCity(City c){
            if(c==null)return null;Army best=null;int strength=-1;
            foreach(Army d in Armies.Values){if(d.KingdomId!=c.KingdomId||Mathx.Manhattan(d.X,d.Y,c.X,c.Y)>2)continue;int ready=Military.ReadySoldierCount(d);if(ready>strength){strength=ready;best=d;}}
            return strength>0?best:null;
        }
        Person EmergencyCommander(City c){
            if(c==null)return null;Person best=null;int score=-1;
            foreach(string id in c.PersonIds){Person p;if(!People.TryGetValue(id,out p)||!p.Alive||!p.Noble||p.Merchant||p.Injury==InjuryState.Captured||p.Class==SocialClass.Official)continue;if(ArmyContaining(p.Id)!=null)continue;int s=p.Stats.Command*3+p.Stats.Military*2+p.Stats.Martial+p.Stats.Loyalty;if(s>score){score=s;best=p;}}
            return best;
        }
        Army EnsureRealCityDefender(City c){
            Army existing=DefenderArmyAtCity(c);if(existing!=null)return existing;if(c==null||!HasBuilding(c,BuildingKind.Barracks)||Population.CivilianPopulation(c)<8)return null;
            Kingdom k;if(!Kingdoms.TryGetValue(c.KingdomId,out k))return null;Person commander=EmergencyCommander(c);if(commander==null)return null;EnsureGeneralIdentity(commander);
            Army guard=Military.CreateArmy(k,c,commander,c.Name+"守备军");if(guard==null)return null;guard.FormedDay=Day;int grant=Math.Min(k.Treasury,Math.Max(90,Math.Min(260,Population.CivilianPopulation(c)*5)));if(grant>0){k.Treasury-=grant;guard.Finance.Gold+=grant;}
            int requested=Math.Min(30,Math.Max(8,Population.CivilianPopulation(c)/4));Military.Recruit(guard,c,FirstBasicUnitId(),requested,8);PrepareArmyPersonnelAndEquipment(guard,c);
            if(Military.SoldierCount(guard)<=0){k.ArmyIds.Remove(guard.Id);c.GarrisonArmyIds.Remove(guard.Id);Armies.Remove(guard.Id);commander.Class=SocialClass.Noble;commander.Job=JobKind.Unemployed;return null;}
            return guard;
        }
        void RefreshGarrisonRegistry(){foreach(City c in Cities.Values)c.GarrisonArmyIds.Clear();foreach(Army a in Armies.Values){foreach(City c in Cities.Values){if(c.KingdomId==a.KingdomId&&Mathx.Manhattan(a.X,a.Y,c.X,c.Y)<=1){if(!c.GarrisonArmyIds.Contains(a.Id))c.GarrisonArmyIds.Add(a.Id);break;}}}}
        float AverageGarrisonMorale(City c){float sum=0f;int n=0;foreach(Army d in Armies.Values)if(d.KingdomId==c.KingdomId&&Mathx.Manhattan(d.X,d.Y,c.X,c.Y)<=2&&Military.SoldierCount(d)>0){sum+=d.Morale;n++;}return n==0?Mathx.Clamp(40f+Population.LivingPopulation(c)*.06f,40f,65f):sum/n;}
        int DefenderSoldiersAtCity(City c){int n=0;foreach(Army d in Armies.Values)if(d.KingdomId==c.KingdomId&&Mathx.Manhattan(d.X,d.Y,c.X,c.Y)<=2)n+=Military.ReadySoldierCount(d);return n;}
        float FortificationStrength(City c){List<Building> forts=FortificationsFor(c,true);if(forts.Count==0)return 0f;float sum=0f;int n=0;foreach(Building b in forts){sum+=b.Durability/(float)Math.Max(1,b.MaxDurability);n++;}return n==0?0f:sum/n;}
        void TickSiegesDaily(){
            foreach(SiegeState ss in new List<SiegeState>(Sieges.All())){
                if(ss.Captured)continue;Army a;City c;if(!Armies.TryGetValue(ss.AttackerArmyId,out a)||!Cities.TryGetValue(ss.CityId,out c)){Sieges.End(ss.CityId);continue;}
                if(a.KingdomId==c.KingdomId){Sieges.End(c.Id);a.Order=ArmyOrder.Hold;continue;}
                Army defender=DefenderArmyAtCity(c);if(defender==null)defender=EnsureRealCityDefender(c);
                Person g;People.TryGetValue(a.GeneralId,out g);CommanderBehaviorProfile p=CommanderSkillEffectEngine.Build(g,Data);List<Building> forts=FortificationsFor(c,true);Kingdom defenderKingdom;PolicyRuntimeProfile defenderPolicy=Kingdoms.TryGetValue(c.KingdomId,out defenderKingdom)?Policies.RuntimeProfile(defenderKingdom):new PolicyRuntimeProfile();
                int attackerSoldiers=Math.Max(1,Military.ReadySoldierCount(a)),defenderSoldiers=defender==null?0:Military.ReadySoldierCount(defender);float powerRatio=attackerSoldiers/(float)Math.Max(1,defenderSoldiers);
                SpecialUnitBehaviorProfile siegeSpecial=SpecialUnitBehaviorEngine.ArmyProfile(a,Data);WarAdministration attackWar=WarAdministration==null?null:WarAdministration.Get(a.KingdomId,c.KingdomId);WarAdministration defendWar=WarAdministration==null?null:WarAdministration.Get(c.KingdomId,a.KingdomId);WarAdministrationProfile attackAdmin=attackWar!=null&&WarAdministration.IsOfficialValid(a.KingdomId,attackWar.OfficialId)?WarAdministration.Profile(attackWar):null;WarAdministrationProfile defendAdmin=defendWar!=null&&WarAdministration.IsOfficialValid(c.KingdomId,defendWar.OfficialId)?WarAdministration.Profile(defendWar):null;float attackWarFactor=attackAdmin==null ? .96f:Mathx.Clamp((attackAdmin.Siege+attackAdmin.Logistics+attackAdmin.Coordination)/3f,.92f,1.12f);float defendWarFactor=defendAdmin==null ? .96f:Mathx.Clamp((defendAdmin.Defense+defendAdmin.Coordination)/2f,.92f,1.12f);int engineering=(int)Math.Round(FindOfficialLogistics(a.KingdomId)*Mathx.Clamp((p.Siege+p.Engineering)*.5f*siegeSpecial.Engineering*attackWarFactor,.65f,2.25f)/Mathx.Clamp(defenderPolicy.SiegeDefense*defendWarFactor,.7f,2.0f));if(defenderPolicy.WallRepair*defendWarFactor>1f&&c.Stone>0&&ss.Days%2==0){int repaired=0;foreach(Building fort in forts){if(fort==null||fort.Ruined||fort.Durability>=fort.MaxDurability)continue;int gain=Math.Max(1,(int)Math.Round((defenderPolicy.WallRepair*defendWarFactor-1f)*8f));int actual=Math.Min(gain,fort.MaxDurability-fort.Durability);if(actual>0){fort.Durability+=actual;c.Stone=Math.Max(0,c.Stone-1);repaired+=actual;}if(c.Stone<=0)break;}if(repaired>0)ss.GarrisonMorale=Math.Min(100f,ss.GarrisonMorale+1f);}

                AiOwner ai;bool assault;if(_ai.TryGetValue(a.KingdomId,out ai)&&a.KingdomId!=PlayerKingdomId)assault=ai.ShouldAssaultSiege(ss.GateIntegrity,ss.WallIntegrity,a.Morale,a.FoodDays,powerRatio,(p.Siege+p.Engineering)*.5f);else assault=a.Morale>38&&a.FoodDays>1.5f&&(ss.Days>=2||ss.GateIntegrity<75f);
                Sieges.TickDay(ss,a,c,assault,engineering,forts,attackerSoldiers,defenderSoldiers);
                if(ss.Captured){CaptureCity(a,c);continue;}
                if(ss.Breached&&assault&&ss.LastAssaultBattleDay!=Day){
                    ss.LastAssaultBattleDay=Day;defender=DefenderArmyAtCity(c);
                    if(defender!=null&&Military.ReadySoldierCount(defender)>0){if(!IsArmyInActiveBattle(a.Id)&&!IsArmyInActiveBattle(defender.Id)){BattleSession breach=StartBattle(a,defender,c.Name+"破城战",c.Id);if(breach!=null)breach.Report.Highlights.Add("攻城军通过真实城墙破口进入守军近战，而非直接判定占领");}continue;}
                    else {ss.Captured=true;CaptureCity(a,c);continue;}
                }
                if(Sieges.ShouldOfferSurrender(ss))a.Order=ArmyOrder.Siege;
            }
        }
        void CaptureCity(Army victor,City city){
            string oldId=city.KingdomId;if(oldId==victor.KingdomId){victor.Order=ArmyOrder.Hold;Sieges.End(city.Id);return;}
            Kingdom oldK,newK;if(!Kingdoms.TryGetValue(oldId,out oldK)||!Kingdoms.TryGetValue(victor.KingdomId,out newK))return;
            bool lastCity=oldK.CityIds.Count<=1;
            if(lastCity&&CanBecomeVassal(oldK,newK)){
                oldK.Status=KingdomStatus.Vassal;oldK.OverlordKingdomId=newK.Id;oldK.DefeatDay=Day;Diplomacy.SetVassal(newK.Id,oldK.Id);RecordEvent("外交","成为附庸",oldK.Name+"向"+newK.Name+"臣服并成为附庸",oldK.Id);
                int surrenderTribute=Math.Min(oldK.Treasury,Math.Max(80,oldK.Treasury/4));oldK.Treasury-=surrenderTribute;newK.Treasury+=surrenderTribute;
                oldK.Prestige=Math.Max(5,oldK.Prestige-15);newK.Prestige+=8;city.Food=Math.Max(0,city.Food-25);
                foreach(Army subjectArmy in Armies.Values)if(subjectArmy.KingdomId==oldK.Id){subjectArmy.Order=ArmyOrder.Hold;subjectArmy.Morale=Math.Min(subjectArmy.Morale,55f);subjectArmy.TargetX=subjectArmy.X;subjectArmy.TargetY=subjectArmy.Y;}
                victor.Order=ArmyOrder.Hold;Sieges.End(city.Id);RefreshGarrisonRegistry();return;
            }
            oldK.CityIds.Remove(city.Id);if(oldK.CapitalCityId==city.Id&&oldK.CityIds.Count>0)oldK.CapitalCityId=oldK.CityIds[0];if(!newK.CityIds.Contains(city.Id))newK.CityIds.Add(city.Id);city.PreviousKingdomId=oldId;city.LastCapturedDay=Day;city.Occupation=OccupationPolicy.Pending;city.KingdomId=newK.Id;
            foreach(string pid in city.PersonIds){Person person;if(People.TryGetValue(pid,out person)&&person.Injury!=InjuryState.Captured){person.KingdomId=newK.Id;person.Stats.Loyalty=Math.Max(15,person.Stats.Loyalty-8);}}
            victor.Order=ArmyOrder.Hold;Sieges.End(city.Id);RecordEvent("战争","城市陷落",newK.Name+"攻占"+city.Name+"，原属"+oldK.Name+"；占领处置尚未决定",city.Id);
            if(newK.Id!=PlayerKingdomId){OccupationPolicy aiPolicy=(victor.Finance.MonthsInArrears>0||newK.Treasury<300)?OccupationPolicy.LimitedPlunder:(newK.Prestige>=80?OccupationPolicy.Conciliate:OccupationPolicy.LimitedPlunder);if(newK.Prestige<28&&victor.Morale<48f)aiPolicy=OccupationPolicy.FullPlunder;ResolveOccupation(city.Id,aiPolicy);}
            if(oldK.CityIds.Count==0)EliminateKingdom(oldK,newK,city);RefreshGarrisonRegistry();
        }

        public bool ResolveOccupation(string cityId,OccupationPolicy policy){City city;Kingdom winner;if(!Cities.TryGetValue(cityId,out city)||!Kingdoms.TryGetValue(city.KingdomId,out winner)||city.Occupation!=OccupationPolicy.Pending||policy==OccupationPolicy.Pending||policy==OccupationPolicy.None)return false;Kingdom former=null;if(!string.IsNullOrEmpty(city.PreviousKingdomId))Kingdoms.TryGetValue(city.PreviousKingdomId,out former);Army occupier=null;foreach(Army a in Armies.Values)if(a.KingdomId==winner.Id&&Mathx.Manhattan(a.X,a.Y,city.X,city.Y)<=1){occupier=a;break;}int lootGold=0,foodLoss=0,woodLoss=0,stoneLoss=0,ironLoss=0,loyaltyDelta=0,damage=0;if(policy==OccupationPolicy.Conciliate){int cost=Math.Min(100,Math.Max(0,winner.Treasury));winner.Treasury-=cost;loyaltyDelta=14;winner.Prestige+=3;foreach(Person p in People.Values)if(p.Alive&&p.CityId==city.Id)p.Stats.Loyalty=Math.Min(100,p.Stats.Loyalty+loyaltyDelta);RecordEvent("占领","安抚占领",winner.Name+"在"+city.Name+"出资安抚、保护民产并恢复秩序，支出 "+cost+" 金币",city.Id);}else {bool full=policy==OccupationPolicy.FullPlunder;foodLoss=Math.Min(city.Food,full?180:80);woodLoss=Math.Min(city.Wood,full?55:24);stoneLoss=Math.Min(city.Stone,full?45:20);ironLoss=Math.Min(city.Iron,full?35:14);city.Food-=foodLoss;city.Wood-=woodLoss;city.Stone-=stoneLoss;city.Iron-=ironLoss;int treasuryTake=former==null?0:Math.Min(former.Treasury,full?220:90);if(former!=null)former.Treasury-=treasuryTake;lootGold=treasuryTake+(int)Math.Round(foodLoss*.25f+woodLoss*.8f+stoneLoss*.6f+ironLoss*1.6f);winner.Treasury+=lootGold/2;if(occupier!=null){occupier.Finance.Gold+=lootGold-lootGold/2;occupier.FoodDays=Math.Min(30f,occupier.FoodDays+foodLoss/Math.Max(8f,Military.ReadySoldierCount(occupier)*.8f));}loyaltyDelta=full?-32:-15;damage=full?24:9;foreach(string pid in city.PersonIds){Person person;if(!People.TryGetValue(pid,out person)||!person.Alive)continue;person.Stats.Loyalty=Math.Max(0,person.Stats.Loyalty+loyaltyDelta);if(person.Merchant){int wealthLoss=Math.Min(person.Wealth,(int)Math.Round(person.Wealth*(full ? .18f:.07f)));person.Wealth-=wealthLoss;lootGold+=wealthLoss/2;}}foreach(string bid in city.BuildingIds){Building b;if(!Buildings.TryGetValue(bid,out b)||b.Ruined||b.Kind==BuildingKind.Wall||b.Kind==BuildingKind.Gate||b.Kind==BuildingKind.Tower)continue;if(full||b.Kind==BuildingKind.Shop||b.Kind==BuildingKind.Market||b.Kind==BuildingKind.Workshop){b.Durability=Math.Max(1,b.Durability-damage);if(b.Durability<=1){b.Ruined=true;b.Complete=false;}}}winner.Prestige=Math.Max(0,winner.Prestige+(full?-8:-2));RecordEvent("占领",full?"全面掠夺":"有限掠夺",winner.Name+"在"+city.Name+(full?"实施全面掠夺":"实施有限军需征发")+"：粮 "+foodLoss+"、木 "+woodLoss+"、石 "+stoneLoss+"、铁 "+ironLoss+"，折算战利金币 "+lootGold,city.Id);}city.Occupation=policy;return true;}
        public int PendingOccupationCount(){int n=0;foreach(City c in Cities.Values)if(c.KingdomId==PlayerKingdomId&&c.Occupation==OccupationPolicy.Pending)n++;return n;}

        bool CanBecomeVassal(Kingdom loser,Kingdom winner){
            if(loser==null||winner==null||loser.Status==KingdomStatus.Eliminated||winner.Status==KingdomStatus.Eliminated)return false;
            Person lord;if(People.TryGetValue(loser.LordId,out lord)&&lord.Alive&&lord.Injury!=InjuryState.Captured)return true;
            foreach(Person p in People.Values)if(p.Alive&&p.KingdomId==loser.Id&&p.Noble&&p.Injury!=InjuryState.Captured)return true;
            return false;
        }

        void EliminateKingdom(Kingdom loser,Kingdom winner,City refuge){
            if(loser==null||winner==null)return;loser.Status=KingdomStatus.Eliminated;loser.OverlordKingdomId="";loser.DefeatDay=Day;loser.CapitalCityId="";loser.Treasury=0;
            var removeArmies=new List<string>();foreach(Army a in Armies.Values)if(a.KingdomId==loser.Id){removeArmies.Add(a.Id);foreach(Squad sq in a.Squads){foreach(string pid in sq.SoldierIds)AbsorbDefeatedPerson(pid,winner,refuge);AbsorbDefeatedPerson(sq.OfficerId,winner,refuge);}AbsorbDefeatedPerson(a.GeneralId,winner,refuge);foreach(string pid in a.SubGeneralIds)AbsorbDefeatedPerson(pid,winner,refuge);}
            foreach(string aid in removeArmies){Armies.Remove(aid);loser.ArmyIds.Remove(aid);}loser.ArmyIds.Clear();
            foreach(Person p in People.Values)if(p.KingdomId==loser.Id&&p.Alive&&p.Injury!=InjuryState.Captured)AbsorbDefeatedPerson(p.Id,winner,refuge);
            foreach(DiplomacyRelation r in Diplomacy.All())if(r.A==loser.Id||r.B==loser.Id){r.State=DiplomacyState.Neutral;r.CommonWar=false;r.SuzerainId="";r.VassalId="";}RecordEvent("国家","国家灭亡",loser.Name+"失去全部城市并灭亡；胜者为"+winner.Name,loser.Id);
        }

        void AbsorbDefeatedPerson(string personId,Kingdom winner,City refuge){
            if(string.IsNullOrEmpty(personId)||winner==null)return;Person p;if(!People.TryGetValue(personId,out p)||!p.Alive||p.Injury==InjuryState.Captured)return;p.KingdomId=winner.Id;p.CityId=refuge==null?p.CityId:refuge.Id;p.Stats.Loyalty=Math.Max(10,Math.Min(45,p.Stats.Loyalty-15));if(refuge!=null&&!refuge.PersonIds.Contains(p.Id))refuge.PersonIds.Add(p.Id);
        }

        void TickVassalTribute(){
            foreach(Kingdom subject in Kingdoms.Values){if(subject.Status!=KingdomStatus.Vassal||string.IsNullOrEmpty(subject.OverlordKingdomId))continue;Kingdom suzerain;if(!Kingdoms.TryGetValue(subject.OverlordKingdomId,out suzerain)||suzerain.Status==KingdomStatus.Eliminated){subject.Status=KingdomStatus.Active;subject.OverlordKingdomId="";subject.DefeatDay=-1;continue;}int reserve=300;int tribute=Math.Min(Math.Max(0,subject.Treasury-reserve),Math.Max(0,subject.Treasury/10));if(tribute>0){subject.Treasury-=tribute;suzerain.Treasury+=tribute;}Diplomacy.SetVassal(suzerain.Id,subject.Id);}
        }

        void SyncVassalWars(){
            foreach(Kingdom subject in Kingdoms.Values){
                if(subject.Status!=KingdomStatus.Vassal||string.IsNullOrEmpty(subject.OverlordKingdomId))continue;Kingdom suzerain;if(!Kingdoms.TryGetValue(subject.OverlordKingdomId,out suzerain)||suzerain.Status==KingdomStatus.Eliminated)continue;
                Diplomacy.SetVassal(suzerain.Id,subject.Id);
                foreach(Kingdom enemy in Kingdoms.Values){if(enemy.Id==subject.Id||enemy.Id==suzerain.Id||enemy.Status==KingdomStatus.Eliminated)continue;DiplomacyRelation master=Diplomacy.Get(suzerain.Id,enemy.Id);DiplomacyRelation sub=Diplomacy.Get(subject.Id,enemy.Id);if(master.State==DiplomacyState.War)Diplomacy.JoinCommonWar(subject.Id,enemy.Id);else if(sub.State==DiplomacyState.War&&sub.CommonWar)Diplomacy.EndCommonWar(subject.Id,enemy.Id,Day);}
            }
        }

        void SyncAllianceWars(){
            var relations=new List<DiplomacyRelation>(Diplomacy.All());foreach(DiplomacyRelation alliance in relations){if(alliance==null||alliance.State!=DiplomacyState.Alliance)continue;Kingdom a,b;if(!Kingdoms.TryGetValue(alliance.A,out a)||!Kingdoms.TryGetValue(alliance.B,out b)||a.Status==KingdomStatus.Eliminated||b.Status==KingdomStatus.Eliminated)continue;PolicyRuntimeProfile ap=Policies.RuntimeProfile(a),bp=Policies.RuntimeProfile(b);int delayA=Mathx.Clamp((int)Math.Ceiling(5f/Mathx.Clamp(ap.AlliedResponse,.75f,1.8f)),2,7),delayB=Mathx.Clamp((int)Math.Ceiling(5f/Mathx.Clamp(bp.AlliedResponse,.75f,1.8f)),2,7);
                foreach(Kingdom enemy in Kingdoms.Values){if(enemy.Id==a.Id||enemy.Id==b.Id||enemy.Status==KingdomStatus.Eliminated)continue;DiplomacyRelation ar=Diplomacy.Get(a.Id,enemy.Id),br=Diplomacy.Get(b.Id,enemy.Id);if(ar.State==DiplomacyState.War&&br.State!=DiplomacyState.War&&br.State!=DiplomacyState.Vassal&&br.TruceUntilDay<=Day&&Day%delayB==0)Diplomacy.JoinCommonWar(b.Id,enemy.Id);else if(br.State==DiplomacyState.War&&ar.State!=DiplomacyState.War&&ar.State!=DiplomacyState.Vassal&&ar.TruceUntilDay<=Day&&Day%delayA==0)Diplomacy.JoinCommonWar(a.Id,enemy.Id);}
            }
        }

        void MaintenanceSweep(){
            Proposals.PruneResolved(300);Logistics.PruneInvalid(Armies,Cities);while(Battles.Count>300)Battles.RemoveAt(0);
            foreach(Kingdom k in Kingdoms.Values){for(int i=k.CityIds.Count-1;i>=0;i--){City c;if(!Cities.TryGetValue(k.CityIds[i],out c)||c.KingdomId!=k.Id)k.CityIds.RemoveAt(i);}for(int i=k.ArmyIds.Count-1;i>=0;i--){Army a;if(!Armies.TryGetValue(k.ArmyIds[i],out a)||a.KingdomId!=k.Id)k.ArmyIds.RemoveAt(i);}}
            foreach(City c in Cities.Values){for(int i=c.BuildingIds.Count-1;i>=0;i--)if(!Buildings.ContainsKey(c.BuildingIds[i]))c.BuildingIds.RemoveAt(i);for(int i=c.FamilyIds.Count-1;i>=0;i--)if(!Families.ContainsKey(c.FamilyIds[i]))c.FamilyIds.RemoveAt(i);for(int i=c.PersonIds.Count-1;i>=0;i--)if(!People.ContainsKey(c.PersonIds[i]))c.PersonIds.RemoveAt(i);}
            foreach(Army a in Armies.Values){for(int i=a.SubGeneralIds.Count-1;i>=0;i--){Person p;if(!People.TryGetValue(a.SubGeneralIds[i],out p)||!p.Alive||p.Injury==InjuryState.Captured)a.SubGeneralIds.RemoveAt(i);}foreach(Squad sq in a.Squads){for(int i=sq.SoldierIds.Count-1;i>=0;i--){Person p;if(!People.TryGetValue(sq.SoldierIds[i],out p)||!p.Alive||p.Injury==InjuryState.Captured)sq.SoldierIds.RemoveAt(i);}Person officer;if(!string.IsNullOrEmpty(sq.OfficerId)&&(!People.TryGetValue(sq.OfficerId,out officer)||!officer.Alive||officer.Injury==InjuryState.Captured))sq.OfficerId="";}}
            var completedProjects=new List<string>();foreach(ConstructionProject cp in Projects.Values)if(cp==null||cp.Stage==ConstructionStage.Complete)completedProjects.Add(cp==null?null:cp.Id);foreach(string id in completedProjects)if(!string.IsNullOrEmpty(id))Projects.Remove(id);
            var deadGear=new List<string>();foreach(EquipmentInstance e in Equipment.Values)if(e!=null&&!e.Unique&&e.Durability<=0&&string.IsNullOrEmpty(e.OwnerId))deadGear.Add(e.InstanceId);foreach(string id in deadGear)Equipment.Remove(id);
            RefreshGarrisonRegistry();
        }

        List<Army> FieldArmies(string kingdomId){var list=new List<Army>();foreach(Army a in Armies.Values)if(a.KingdomId==kingdomId&&Military.ReadySoldierCount(a)>0)list.Add(a);return list;}
        float ArmyPower(Army a){if(a==null)return 0f;return Math.Max(1,Military.ReadySoldierCount(a))*(a.Morale/100f)*Math.Max(.3f,AverageCohesion(a))*Mathx.Clamp(1f-a.Fatigue/180f,.45f,1f);}
        float CombinedDefensePower(string kingdomId,City city){float sum=0f;foreach(Army a in Armies.Values)if(a.KingdomId==kingdomId&&Mathx.Manhattan(a.X,a.Y,city.X,city.Y)<=8)sum+=ArmyPower(a);return sum>0f?sum:Math.Max(8f,Population.LivingPopulation(city)/20f);}
        bool WarArmyAvailableForOfficial(Army a){return a!=null&&Military.ReadySoldierCount(a)>0&&!IsArmyInActiveBattle(a.Id)&&(a.Order==ArmyOrder.Hold||a.Order==ArmyOrder.Camp)&&a.FoodDays>1.5f&&a.Morale>28f;}
        List<Army> AvailableWarArmies(string kingdomId){var list=new List<Army>();foreach(Army a in Armies.Values)if(a.KingdomId==kingdomId&&WarArmyAvailableForOfficial(a))list.Add(a);return list;}
        Army HighestPriorityWarThreat(WarAdministration x,WarAdministrationProfile profile,out City threatenedCity,out int distance){threatenedCity=null;distance=int.MaxValue;Army best=null;float bestScore=float.MinValue;foreach(Army enemy in Armies.Values){if(enemy.KingdomId!=x.EnemyKingdomId||Military.ReadySoldierCount(enemy)<=0)continue;foreach(string cid in Kingdoms[x.KingdomId].CityIds){City city;if(!Cities.TryGetValue(cid,out city))continue;int d=Mathx.Manhattan(enemy.X,enemy.Y,city.X,city.Y);float score=(profile.ReactionRadius+6-d)*10f+ArmyPower(enemy)*.015f+(city.Id==Kingdoms[x.KingdomId].CapitalCityId?35f:0f);if(score>bestScore){bestScore=score;best=enemy;threatenedCity=city;distance=d;}}}return best;}
        City SelectWarSiegeTarget(WarAdministration x,Army from,WarAdministrationProfile profile){City best=null;float score=float.MinValue;Kingdom enemy;if(!Kingdoms.TryGetValue(x.EnemyKingdomId,out enemy))return null;foreach(string cid in enemy.CityIds){City c;if(!Cities.TryGetValue(cid,out c))continue;float fort=FortificationStrength(c);int d=Mathx.Manhattan(from.X,from.Y,c.X,c.Y);float s=-d*1.4f-fort*18f+(c.Id==enemy.CapitalCityId?16f:0f);if(x.Focus==WarAdministrationFocus.Siege)s+=10f;if(s>score){score=s;best=c;}}return best;}
        bool IssueWarOfficialMove(WarAdministration x,Army army,int tx,int ty,string action,string detail){if(x==null||army==null||!WarArmyAvailableForOfficial(army))return false;if(!March.SetDestination(army,tx,ty,false))return false;army.MusterStartDay=Day;x.OrdersIssued++;x.LastDecisionDay=Day;x.LastAction=action+"："+detail;RecordEvent("战务",action,detail,x.Id);return true;}
        void CoordinateWarAdministration(WarAdministration x,bool force){
            if(x==null||WarAdministration==null)return;if(!WarAdministration.IsOfficialValid(x.KingdomId,x.OfficialId))return;if(Diplomacy.Get(x.KingdomId,x.EnemyKingdomId).State!=DiplomacyState.War)return;if(!force&&x.LastDecisionDay==Day)return;WarAdministrationProfile profile=WarAdministration.Profile(x);List<Army> ready=AvailableWarArmies(x.KingdomId);if(ready.Count==0){x.LastDecisionDay=Day;x.LastAction="暂无可重新调度的空闲部队，维持现有军令";return;}
            int budget=profile.MaxOrders;if(x.Focus==WarAdministrationFocus.Balanced)budget=Math.Max(1,budget);Kingdom warningKingdom;float earlyWarning=Kingdoms.TryGetValue(x.KingdomId,out warningKingdom)?Policies.RuntimeProfile(warningKingdom).EarlyWarning:1f;int reactionRadius=Math.Max(2,(int)Math.Round(profile.ReactionRadius*Mathx.Clamp(earlyWarning,.7f,1.8f)));City threatened;int threatDistance;Army threat=HighestPriorityWarThreat(x,profile,out threatened,out threatDistance);
            if(threat!=null&&threatened!=null&&threatDistance<=reactionRadius+2){ready.Sort((a,b)=>Mathx.Manhattan(a.X,a.Y,threatened.X,threatened.Y).CompareTo(Mathx.Manhattan(b.X,b.Y,threatened.X,threatened.Y)));int defenseNeed=threatDistance<=4?Math.Min(2,budget):1;for(int i=0;i<ready.Count&&budget>0&&defenseNeed>0;i++){Army a=ready[i];bool issued=false;if(threatDistance<=4&&x.DefenseEnabled&&(x.Focus==WarAdministrationFocus.Balanced||x.Focus==WarAdministrationFocus.Defense||x.Focus==WarAdministrationFocus.Interception)){issued=IssueWarOfficialMove(x,a,threatened.X,threatened.Y,"守城调度",threatened.Name+"发现敌军逼近，命"+a.Name+"回援守城");if(issued)x.DefenseOrders++;}else if(x.InterceptionEnabled&&(x.Focus==WarAdministrationFocus.Balanced||x.Focus==WarAdministrationFocus.Interception||x.Focus==WarAdministrationFocus.FieldBattle)){issued=IssueWarOfficialMove(x,a,threat.X,threat.Y,"敌军拦截",threat.Name+"进入本国预警区，命"+a.Name+"前出拦截");if(issued)x.InterceptOrders++;}if(issued){budget--;defenseNeed--;}}if(budget<=0)return;ready=AvailableWarArmies(x.KingdomId);}
            if(ready.Count==0)return;
            if(x.FieldBattleEnabled&&(x.Focus==WarAdministrationFocus.FieldBattle||x.Focus==WarAdministrationFocus.Balanced)&&threat!=null&&threatDistance<=reactionRadius+6&&budget>0){ready.Sort((a,b)=>Mathx.Manhattan(a.X,a.Y,threat.X,threat.Y).CompareTo(Mathx.Manhattan(b.X,b.Y,threat.X,threat.Y)));if(IssueWarOfficialMove(x,ready[0],threat.X,threat.Y,"野战调度",ready[0].Name+"奉命寻机与"+threat.Name+"进行野战")){x.FieldOrders++;budget--;}}
            ready=AvailableWarArmies(x.KingdomId);if(ready.Count==0||budget<=0)return;
            if(x.SiegeEnabled&&(x.Focus==WarAdministrationFocus.Siege||x.Focus==WarAdministrationFocus.Balanced)){ready.Sort((a,b)=>ArmyPower(b).CompareTo(ArmyPower(a)));int cap=x.Focus==WarAdministrationFocus.Siege?Math.Min(2,budget):1;for(int i=0;i<ready.Count&&budget>0&&cap>0;i++){Army a=ready[i];if(a.FoodDays<3.5f||a.Fatigue>72f)continue;City target=SelectWarSiegeTarget(x,a,profile);if(target==null)break;if(IssueWarOfficialMove(x,a,target.X,target.Y,"攻城调度",a.Name+"奉命向"+target.Name+"推进并组织攻城")){x.SiegeOrders++;budget--;cap--;}}}
        }
        void EndWarAdministration(string a,string b,string detail){if(WarAdministration==null)return;WarAdministration one,two;if(WarAdministration.Revoke(a,b,out one)&&one!=null)RecordEvent("战务","解除战争负责官员",detail,b);if(WarAdministration.Revoke(b,a,out two)&&two!=null)RecordEvent("战务","解除战争负责官员",detail,a);}
        void EnsureComputerWarOfficials(Kingdom k){if(k==null||k.Id==PlayerKingdomId||WarAdministration==null)return;foreach(Kingdom enemy in Kingdoms.Values){if(enemy.Id==k.Id||enemy.Status==KingdomStatus.Eliminated||Diplomacy.Get(k.Id,enemy.Id).State!=DiplomacyState.War)continue;WarAdministration x=WarAdministration.Get(k.Id,enemy.Id);if(x!=null&&WarAdministration.IsOfficialValid(k.Id,x.OfficialId))continue;Person official=WarAdministration.BestOfficial(k.Id,WarAdministrationFocus.Balanced);if(official==null)continue;string reason;WarAdministration assigned=WarAdministration.Assign(k.Id,enemy.Id,official.Id,Day,WarAdministrationFocus.Balanced,out reason);if(assigned!=null)RecordEvent("战务","战争官员就任",official.Name+"受命负责"+k.Name+"对"+enemy.Name+"的战务",enemy.Id);}}
        void TickWarAdministrationDaily(){
            if(WarAdministration==null)return;var rows=new List<WarAdministration>(WarAdministration.All());foreach(WarAdministration x in rows){if(x==null)continue;if(Diplomacy.Get(x.KingdomId,x.EnemyKingdomId).State!=DiplomacyState.War){WarAdministration removed;WarAdministration.Revoke(x.KingdomId,x.EnemyKingdomId,out removed);continue;}if(!WarAdministration.IsOfficialValid(x.KingdomId,x.OfficialId)){WarAdministration removed;WarAdministration.Revoke(x.KingdomId,x.EnemyKingdomId,out removed);Kingdom k,e;Kingdoms.TryGetValue(x.KingdomId,out k);Kingdoms.TryGetValue(x.EnemyKingdomId,out e);RecordEvent("战务","战争负责官员失效",(k==null?"本国":k.Name)+"对"+(e==null?"敌国":e.Name)+"的战争暂时无人负责，等待重新指派",x.EnemyKingdomId);continue;}CoordinateWarAdministration(x,false);}
        }

        void TickComputerKingdoms() {
            foreach (Kingdom k in Kingdoms.Values) {
                if (k.Id == PlayerKingdomId || k.Status==KingdomStatus.Eliminated) continue;
                AiOwner ai; if (!_ai.TryGetValue(k.Id, out ai)) continue;
                City capital; if (!Cities.TryGetValue(k.CapitalCityId, out capital)) continue;
                EnsureComputerWarOfficials(k);

                foreach (Proposal p in Proposals.Queue) {
                    if (p.State != ProposalState.Pending || ProposalKingdom(p) != k.Id) continue; if (p.Kind == ProposalKind.Diplomacy && p.TargetId == PlayerKingdomId) continue;
                    PolicyRuntimeProfile approvalPolicy=Policies.RuntimeProfile(k);int policyReserve=(int)Math.Ceiling(approvalPolicy.MonthlyMaintenance*3f);bool approve = p.CostGold <= Math.Max(0,k.Treasury-policyReserve);
                    if (p.Kind == ProposalKind.Construction) approve = approve && ai.ConstructionPriority(capital) >= 40;
                    if (approve) ApproveProposal(p.Id); else Proposals.Reject(p.Id);
                }

                PolicyRuntimeProfile aiPolicy=Policies.RuntimeProfile(k);var field=FieldArmies(k.Id);if(field.Count==0){if(HasBuilding(capital,BuildingKind.Barracks))RequestGeneralCandidate(capital);continue;}
                foreach(Army recruitArmy in field){if(!ai.ShouldRecruit(capital,k,Military.SoldierCount(recruitArmy))||recruitArmy.Order!=ArmyOrder.Hold)continue;int grant=Math.Min(120,k.Treasury);if(grant<=0)break;k.Treasury-=grant;recruitArmy.Finance.Gold+=grant;CityCultureProfile aiCulture=CityCultureEffectEngine.Build(capital);int recruitCount=Mathx.Clamp((int)Math.Round(8f*aiPolicy.RecruitmentSpeed*aiCulture.Recruitment),5,16);Military.Recruit(recruitArmy,capital,FirstBasicUnitId(),recruitCount,8);PrepareArmyPersonnelAndEquipment(recruitArmy,capital);break;}

                bool alreadyAtWar=false;foreach(Kingdom existingEnemy in Kingdoms.Values)if(existingEnemy.Id!=k.Id&&Diplomacy.Get(k.Id,existingEnemy.Id).State==DiplomacyState.War){alreadyAtWar=true;break;}
                if(alreadyAtWar)continue;

                Army lead=null;float bestLead=-1f;foreach(Army candidate in field){if(candidate.Order!=ArmyOrder.Hold||candidate.FoodDays<3.5f||candidate.Morale<42f||candidate.Fatigue>=78f)continue;float score=ArmyPower(candidate)+candidate.FoodDays*2f-candidate.Fatigue*.3f;if(score>bestLead){bestLead=score;lead=candidate;}}
                if(lead==null)continue;Kingdom target=BestEnemyKingdom(k,lead);if(target==null)continue;City targetCity;if(!Cities.TryGetValue(target.CapitalCityId,out targetCity))continue;
                float ownPower=0f,food=0f,morale=0f,fatigue=0f;int counted=0;foreach(Army a in field){if(a.Order!=ArmyOrder.Hold||a.FoodDays<3f)continue;ownPower+=ArmyPower(a);food+=a.FoodDays;morale+=a.Morale;fatigue+=a.Fatigue;counted++;}if(counted==0)continue;
                Person general;People.TryGetValue(lead.GeneralId,out general);CommanderBehaviorProfile cp=CommanderSkillEffectEngine.Build(general,Data);SupplyRoute route=Logistics.RouteForArmy(lead.Id);float supplyRisk=route==null ? .35f:route.Risk+(route.Intact?0f:.45f);
                float enemyPower=CombinedDefensePower(target.Id,targetCity);Army defending=DefenderArmyAtCity(targetCity);
                var assess=new AiWarAssessment{PowerRatio=ownPower/Math.Max(1f,enemyPower),OwnFoodDays=food/counted,OwnMorale=morale/counted,OwnFatigue=fatigue/counted,SupplyRisk=supplyRisk,CommanderScout=cp.Scout,CommanderSupply=cp.SupplyEfficiency,EnemyFortification=FortificationStrength(targetCity),EnemyMorale=defending==null?55f:defending.Morale,Distance=Mathx.Manhattan(lead.X,lead.Y,targetCity.X,targetCity.Y),Weather=Weather.Weather,EnemyCapital=true,OwnCapitalThreatened=false};
                if(ai.ShouldAttack(assess)&&Diplomacy.DeclareWar(k.Id,target.Id,Day)){RecordEvent("战争","宣战",k.Name+"向"+target.Name+"宣战",target.Id);EnsureComputerWarOfficials(k);WarAdministration wa=WarAdministration.Get(k.Id,target.Id);if(wa!=null)CoordinateWarAdministration(wa,true);}
            }
        }

        Kingdom BestEnemyKingdom(Kingdom own,Army army){Kingdom best=null;float bestScore=float.MinValue;foreach(Kingdom k in Kingdoms.Values){if(k.Id==own.Id||k.Status==KingdomStatus.Eliminated)continue;if(k.Status==KingdomStatus.Vassal&&k.OverlordKingdomId==own.Id)continue;DiplomacyRelation rel=Diplomacy.Get(own.Id,k.Id);if(rel.State==DiplomacyState.Alliance||rel.State==DiplomacyState.NonAggression||rel.TruceUntilDay>Day)continue;City c;if(!Cities.TryGetValue(k.CapitalCityId,out c))continue;int distance=Mathx.Manhattan(army.X,army.Y,c.X,c.Y);float score=-distance-FortificationStrength(c)*9f+k.CityIds.Count*-1.5f;if(rel.State==DiplomacyState.War)score+=18f;if(score>bestScore){bestScore=score;best=k;}}return best;}

        Kingdom NearestEnemyKingdom(Kingdom own, int x, int y) {
            Kingdom best = null; int bestDist = int.MaxValue;
            foreach (Kingdom k in Kingdoms.Values) {
                if (k.Id == own.Id || k.Status==KingdomStatus.Eliminated || (k.Status==KingdomStatus.Vassal&&k.OverlordKingdomId==own.Id)) continue;
                DiplomacyRelation rel = Diplomacy.Get(own.Id, k.Id);
                if (rel.State == DiplomacyState.Alliance || rel.State == DiplomacyState.NonAggression || rel.TruceUntilDay > Day) continue;
                City c; if (!Cities.TryGetValue(k.CapitalCityId, out c)) continue;
                int d = Mathx.Manhattan(x, y, c.X, c.Y); if (d < bestDist) { bestDist = d; best = k; }
            }
            return best;
        }

        public void AdvanceDay() {
            Day++;
            Weather.TickDay(Map);
            Animals.TickDay();
            foreach (City c in Cities.Values) {
                Kingdom ck;PolicyRuntimeProfile cp=Kingdoms.TryGetValue(c.KingdomId,out ck)?Policies.RuntimeProfile(ck):new PolicyRuntimeProfile();OfficialRuntimeProfile cityOfficial=OfficialProfileFor(c);int living=Math.Max(1,Population.LivingPopulation(c));float effectiveHousing=c.PopulationCapacity*Mathx.Clamp(cp.HousingCapacity,.75f,1.6f);Population.Grow(c, FoodSecurity(c)*Mathx.Clamp(cp.DisasterResilience,.8f,1.35f), Math.Min(1.1f, effectiveHousing/living), 4f,cp.PopulationGrowth*cityOfficial.Population);
                Population.AssignJobs(c,cp.JobMatching);
                bool granary=HasBuilding(c,BuildingKind.Granary);CityBuildingEffectProfile built=CityBuildingEffectEngine.Build(c,Buildings);float spoilageRate=(granary ? .00045f : .0014f)/Mathx.Clamp(cp.FoodStorage*built.FoodStorage,.75f,2.4f);int spoiled=(int)Math.Floor(c.Food*spoilageRate);if(spoiled>0)c.Food=Math.Max(0,c.Food-spoiled);
                if((Weather.Weather==WeatherKind.Cold||Weather.Weather==WeatherKind.Blizzard)&&c.Horses>0){float lossRate=(Weather.Weather==WeatherKind.Blizzard ? .006f : .002f)/Mathx.Clamp(cp.HorseSurvival,.75f,1.8f);int lost=(int)Math.Floor(c.Horses*lossRate);if(lost>0)c.Horses=Math.Max(0,c.Horses-lost);}
                if (Day % 5 == 0) Animals.HuntNear(c, Math.Max(1, Population.CivilianPopulation(c) / 25));
                if (Day % 10 == 0) {OfficialRuntimeProfile op=OfficialProfileFor(c);int got=Animals.DomesticateHorses(c, HasBuilding(c, BuildingKind.Stable));if(got>0){CityCultureProfile horseCulture=CityCultureEffectEngine.Build(c);float horseMul=cp.HorseGrowth*op.Horses*horseCulture.HorseGrowth;if(horseMul>1f)c.Horses+=(int)Math.Round(got*(horseMul-1f));}}
            }
            TickEconomy(30f);
            TickLogisticsDaily();
            foreach(Army trainingArmy in Armies.Values){Person trainingGeneral;People.TryGetValue(trainingArmy.GeneralId,out trainingGeneral);Kingdom tk;PolicyRuntimeProfile tp=Kingdoms.TryGetValue(trainingArmy.KingdomId,out tk)?Policies.RuntimeProfile(tk):new PolicyRuntimeProfile();Military.TrainDay(trainingArmy,CommanderSkillEffectEngine.Build(trainingGeneral,Data),tp.Training,tp.CavalryTraining);}
            TickArmies(1f);
            TickRecoveryAndRetirement();
            TickCaptivity();
            TickSiegesDaily();
            TickGovernance(1f);
            foreach (Building b in Buildings.Values) {City bc;Kingdom bk;PolicyRuntimeProfile bp=Cities.TryGetValue(b.CityId,out bc)&&Kingdoms.TryGetValue(bc.KingdomId,out bk)?Policies.RuntimeProfile(bk):new PolicyRuntimeProfile();Construction.WeatherWear(b, Weather.Weather,bp.BuildingDurability);}
            foreach(City c in Cities.Values)EnsureRepairProposal(c);
            foreach(Army a in Armies.Values)RepairArmyEquipmentAtHome(a);
            ResolveUniqueInheritance();
            if (Day % 4 == 0) TickCivilianBuilding();
            if (Day % 15 == 0) TickMerchantInvestmentOffers();
            if (Day % 30 == 0) { Merchants.MonthlySupport(Armies);foreach(Kingdom k in Kingdoms.Values){Policies.ChargeMonthlyMaintenance(k);Policies.ReactivateAffordable(k);} SettleMilitaryMonth(); TickVassalTribute(); TickCivilMerit(); TickMerchantSecurityEvents(); TickLordPrestigeEffects(); TickOfficialPolitics(); TickFamilyPolitics(); TickUniqueCrafting(); }
            TickDiplomaticOffers();
            SyncVassalWars();
            SyncAllianceWars();
            TickComputerKingdoms();
            TickWarAdministrationDaily();
            RefreshGarrisonRegistry();
            if(Day%10==0)MaintenanceSweep();
        }

        public void Tick(float realSeconds) { if(Paused||realSeconds<=0)return;float scaled=realSeconds*Mathx.Clamp(TimeScale,.1f,8f);Scheduler.Tick(scaled);_dayAccumulator+=scaled;while(_dayAccumulator>=SecondsPerDay){_dayAccumulator-=SecondsPerDay;AdvanceDay();} }

        float FoodSecurity(City c) { return Mathx.Clamp(c.Food / (float)Math.Max(1, c.PersonIds.Count * 6), .2f, 1f); }

        public bool HasBuilding(City c, BuildingKind kind) {
            foreach (string id in c.BuildingIds) { Building b; if (Buildings.TryGetValue(id, out b) && b.Complete && b.Kind == kind) return true; }
            return false;
        }

        Person FindOfficial(City c) { foreach (string id in c.PersonIds) { Person p; if (People.TryGetValue(id, out p) && p.Alive && p.Class == SocialClass.Official) return p; } return null; }
        public Person FindEligibleNoble(City c) { foreach (string id in c.PersonIds) { Person p; if (People.TryGetValue(id, out p) && p.Alive && p.Noble && p.Class == SocialClass.Noble) return p; } return null; }
        Army FirstArmy(string kingdomId) { foreach (Army a in Armies.Values) if (a.KingdomId == kingdomId) return a; return null; }
        City CityAt(int x, int y, string excludeKingdom) { foreach (City c in Cities.Values) if (c.KingdomId != excludeKingdom && Mathx.Manhattan(c.X, c.Y, x, y) <= 1) return c; return null; }
    }
}
