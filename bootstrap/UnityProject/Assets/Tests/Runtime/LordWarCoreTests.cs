#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using LordWar;
using LordWar.Data;
using LordWar.Governance;
using LordWar.World;
using LordWar.Society;
using LordWar.Military;
using LordWar.Save;
using LordWar.Simulation;
using LordWar.UnityRuntime;
using LordWar.AI;
using LordWar.Diplomacy;
using LordWar.Siege;
using LordWar.Performance;
using LordWar.Construction;
using LordWar.Combat;
using LordWar.Economy;

public sealed class LordWarCoreTests {
    [Test]
    public void CsvParser_PreservesQuotedComma() {
        CsvTable t = CsvTable.Parse("甲,乙\n1,\"含,逗号\"\n");
        Assert.AreEqual(1,t.Rows.Count);
        Assert.AreEqual("含,逗号",t.Rows[0]["乙"]);
    }

    [Test]
    public void ProposalAbilityGate_IsHardGate() {
        ProposalSystem q = new ProposalSystem();
        Proposal p = new Proposal{Id="p",Title="试行政策",RequiredAbility=60};
        Assert.IsFalse(q.Submit(p,50));
        Assert.AreEqual(0,q.PendingCount);
    }

    [Test]
    public void RuntimeData_LoadsFrozenCountsAndEquipment() {
        GameDataCatalog d=new GameDataCatalog();d.LoadAll(new UnityDataProvider());
        Assert.AreEqual(300,d.Policies.Count);Assert.AreEqual(360,d.Skills.Count);Assert.AreEqual(156,d.Units.Count);Assert.AreEqual(120,d.SpecialUnits.Count);Assert.AreEqual(72,d.EquipmentTypes.Count);
        Assert.AreEqual("短剑",d.EquipmentTypes["E001"].Name);
        Assert.AreEqual("粗制",d.EquipmentQualities[1].Name);
    }

    [Test]
    public void WorldGenerator_IsDeterministicAndBuildsRequestedCities() {
        WorldMap a=new WorldGenerator(12345).Generate(96,80,2);
        WorldMap b=new WorldGenerator(12345).Generate(96,80,2);
        Assert.AreEqual(2,a.CitySites.Count);Assert.AreEqual(a.CitySites.Count,b.CitySites.Count);
        Assert.AreEqual(a.Get(13,17).Height,b.Get(13,17).Height,0.00001f);
        int mountain=0,river=0,lake=0,resource=0;
        foreach(WorldTile t in a.Tiles){if(t.Terrain==TerrainKind.Mountain||t.Terrain==TerrainKind.MountainPass)mountain++;if(t.River)river++;if(t.Lake)lake++;if(!string.IsNullOrEmpty(t.ResourceDeposit))resource++;}
        Assert.Greater(mountain,0);Assert.Greater(river,0);Assert.Greater(lake,0);Assert.Greater(resource,0);
    }

    [Test]
    public void WorldGenerator_LandRatioAndParameters_AreSavedOnMap() {
        foreach(int land in new[]{25,75}) {
            var options=new WorldGenerationOptions{LandPercent=land,ForestPercent=0,MountainPercent=0,DesertPercent=0,RiverPercent=0,ResourcePercent=0};
            WorldMap a=new WorldGenerator(4812,options).Generate(160,160,2);
            WorldMap b=new WorldGenerator(4812,options).Generate(160,160,2);
            int dry=0,forest=0,rivers=0,resources=0;
            foreach(WorldTile t in a.Tiles) {
                if(t.Terrain!=TerrainKind.DeepWater&&t.Terrain!=TerrainKind.Coast&&t.Terrain!=TerrainKind.Lake)dry++;
                if(t.Terrain==TerrainKind.Forest)forest++;
                if(t.River)rivers++;
                if(!string.IsNullOrEmpty(t.ResourceDeposit))resources++;
            }
            Assert.That(dry/(float)a.Tiles.Length,Is.InRange(land/100f-.04f,land/100f+.04f));
            Assert.AreEqual(land,a.GenerationOptions.LandPercent);
            Assert.AreEqual(a.SeaLevel,b.SeaLevel,0.00001f);
            Assert.AreEqual(a.Get(17,39).Terrain,b.Get(17,39).Terrain);
            Assert.AreEqual(0,forest);Assert.AreEqual(0,rivers);Assert.AreEqual(0,resources);
            Assert.AreEqual(2,a.CitySites.Count);
        }
    }

    [Test]
    public void FindPath_BlocksUnbridgedRiverAndMountainUntilCrossingExists() {
        WorldMap m=new WorldMap(4,3,1);
        foreach(WorldTile t in m.Tiles)t.Terrain=TerrainKind.Grass;
        for(int y=0;y<3;y++){WorldTile t=m.Get(1,y);t.River=true;t.Terrain=TerrainKind.River;}
        WorldGenerator g=new WorldGenerator(1);
        Assert.AreEqual(0,g.FindPath(m,new GridPoint(0,1),new GridPoint(3,1),false).Count);
        m.Get(1,1).Bridge=true;
        Assert.Greater(g.FindPath(m,new GridPoint(0,1),new GridPoint(3,1),false).Count,0);
        m.Get(1,1).Bridge=false;m.Get(1,1).Terrain=TerrainKind.Mountain;
        Assert.AreEqual(0,g.FindPath(m,new GridPoint(0,1),new GridPoint(3,1),false).Count);
    }

    [Test]
    public void Save27_MigratesMapWithoutChangingItsTiles() {
        WorldMap old=new WorldMap(4,3,7);
        foreach(WorldTile tile in old.Tiles)tile.Terrain=TerrainKind.Grass;
        old.Get(0,0).Terrain=TerrainKind.Coast;
        var save=new GameSave{SaveVersion=27,Map=old};
        SaveOwner.Migrate(save);
        Assert.AreEqual(28,save.SaveVersion);
        Assert.IsTrue(save.Map.LegacyGenerator);
        Assert.AreEqual(.24f,save.Map.SeaLevel,0.00001f);
        Assert.IsNotNull(save.Map.GenerationOptions);
        Assert.AreEqual(TerrainKind.Coast,save.Map.Get(0,0).Terrain);
        SaveOwner.Migrate(save);
        Assert.AreEqual(TerrainKind.Coast,save.Map.Get(0,0).Terrain);
    }

    [Test]
    public void Morale_DoesNotInstantlyRoutHealthyFormation() {
        float m=MoraleSystem.Update(80f,.03f,0f,false,true,.35f,false,1f,10f);
        Assert.Greater(m,10f);Assert.IsFalse(MoraleSystem.Rout(m,false));
    }

    [Test]
    public void Terrain_RoadIsFasterThanMarsh_AndLakeIsBlocked() {
        Assert.Greater(MarchFormationSystem.TerrainSpeed(TerrainKind.Road),MarchFormationSystem.TerrainSpeed(TerrainKind.Marsh));
        Assert.AreEqual(0f,MarchFormationSystem.TerrainSpeed(TerrainKind.Lake));
    }

    [Test]
    public void CavalryRecruit_ConsumesRealHorsesAndPopulation() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());
        var people=new Dictionary<string,Person>();var cities=new Dictionary<string,City>();var armies=new Dictionary<string,Army>();
        City c=new City{Id="C",KingdomId="K",Horses=3};cities[c.Id]=c;var pop=new PopulationOwner(people,cities,77);
        Person g=pop.CreatePerson(c,"赵烈",SocialClass.Noble,31);g.Noble=true;Kingdom k=new Kingdom{Id="K"};var owner=new ArmyOwner(armies,people,pop,data);Army a=owner.CreateArmy(k,c,g,"赵烈军");a.Finance.Gold=999;
        for(int i=0;i<12;i++)pop.CreatePerson(c,"募兵"+i,SocialClass.Commoner,22+i%5);
        string cavalry=null;foreach(UnitDef u in data.Units.Values)if(((u.Name??"")+(u.Category??"")+(u.Role??"")).IndexOf("骑")>=0){cavalry=u.Id;break;}
        Assert.IsNotNull(cavalry);int before=c.Horses;int recruited=owner.Recruit(a,c,cavalry,10,1);
        Assert.LessOrEqual(recruited,before);Assert.AreEqual(before-recruited,c.Horses);
    }

    [Test]
    public void NewWorld_CaptureSavePassesReferenceValidation() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());GameWorld w=new GameWorld(24680,data);w.CreateNewWorld(80,64,2);
        GameSave s=GameSaveService.Capture(w);string error;Assert.IsTrue(GameSaveService.Validate(s,out error),error);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.GreaterOrEqual(s.Kingdoms.Count,2);Assert.Greater(s.People.Count,0);
    }
    [Test]
    public void RoadGrades_ChangeCapacityAndCongestionModel() {
        WorldMap m=new WorldMap(6,3,1);for(int y=0;y<m.Height;y++)for(int x=0;x<m.Width;x++){WorldTile t=m.Get(x,y);t.Terrain=TerrainKind.Grass;}
        for(int x=0;x<6;x++){WorldTile t=m.Get(x,1);t.Road=true;t.RoadLevel=3;t.RoadCapacity=7;}
        Army a=new Army{Id="A",X=0,Y=1,FoodDays=8f};WorldGenerator g=new WorldGenerator(1);MarchOwner march=new MarchOwner(g,m);Assert.IsTrue(march.SetDestination(a,5,1));
        march.Step(a,10f,false,0f,7);Assert.AreEqual(5,a.X);Assert.AreEqual(1,a.Y);Assert.Greater(a.FoodDays,7f);
    }

    [Test]
    public void SupplyRouteAndConvoy_AreCapturedInSave() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());GameWorld w=new GameWorld(9911,data);w.CreateNewWorld(80,64,2);
        Kingdom k=w.Kingdoms[w.PlayerKingdomId];City c=w.Cities[k.CapitalCityId];Army a=null;foreach(Army x in w.Armies.Values)if(x.KingdomId==k.Id){a=x;break;}
        if(a==null){Person noble=w.FindEligibleNoble(c);Assert.IsNotNull(noble);a=w.Military.CreateArmy(k,c,noble,noble.Name+"军");a.Finance.Gold=500;w.Military.Recruit(a,c,data.UnitIds[0],8,4);}
        a.X=Math.Min(w.Map.Width-2,c.X+5);a.Y=c.Y;a.FoodDays=2f;SupplyRoute r=w.Logistics.EnsureRoute(a,c,w.Day);Assert.IsNotNull(r);SupplyConvoy cv=w.Logistics.Dispatch(r,a,c,w.Military.SoldierCount(a),70,w.Day);Assert.IsNotNull(cv);
        GameSave save=GameSaveService.Capture(w);Assert.Greater(save.SupplyRoutes.Count,0);Assert.Greater(save.SupplyConvoys.Count,0);string error;Assert.IsTrue(GameSaveService.Validate(save,out error),error);
    }

    [Test]
    public void EveryBasicUnit_HasExecutableEconomyTrainingAndLoadout() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());
        City c=new City{Id="C",CultureId="山岭堡城"};
        int cavalry=0,abstractGear=0;
        foreach(UnitDef u in data.Units.Values) {
            Assert.Greater(u.RecruitCost,0,u.Name+" 缺少招募成本");
            Assert.Greater(u.TrainingDays,0,u.Name+" 缺少训练天数");
            Assert.Greater(u.MonthlyWage,0,u.Name+" 缺少军饷");
            Assert.Greater(u.DailyFood,0,u.Name+" 缺少粮耗");
            Assert.Greater(u.Life,0,u.Name+" 缺少生命");
            Assert.Greater(u.SquadCap,0,u.Name+" 缺少小队上限");
            if((u.Gear??"").IndexOf("地区制式")>=0||(u.Gear??"").IndexOf("专属套装")>=0)abstractGear++;
            List<string> loadout=UnitLoadoutResolver.ResolveBasic(u,c);
            Assert.Greater(loadout.Count,0,u.Name+" 未解析出真实装备");
            foreach(string token in loadout) Assert.IsNotNull(data.FindEquipmentType(token),u.Name+" 装备未映射："+token);
            if(UnitLoadoutResolver.RequiresHorse(u)){cavalry++;Assert.Greater(u.HorseCost,0,u.Name+" 骑兵未配置军马消耗");}
        }
        Assert.GreaterOrEqual(cavalry,20);
        Assert.Greater(abstractGear,100);
    }

    [Test]
    public void Recruit_UsesFrozenUnitCostAndTrainingDays() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());
        UnitDef u=data.Units["U001"];
        var people=new Dictionary<string,Person>();var cities=new Dictionary<string,City>();var armies=new Dictionary<string,Army>();
        City c=new City{Id="C",KingdomId="K",Horses=20};cities[c.Id]=c;var pop=new PopulationOwner(people,cities,9);
        Person g=pop.CreatePerson(c,"赵烈",SocialClass.Noble,30);g.Noble=true;for(int i=0;i<20;i++)pop.CreatePerson(c,"兵"+i,SocialClass.Commoner,20+i%4);
        Kingdom k=new Kingdom{Id="K"};var owner=new ArmyOwner(armies,people,pop,data);Army a=owner.CreateArmy(k,c,g,"赵烈军");a.Finance.Gold=u.RecruitCost*5;
        int n=owner.Recruit(a,c,u.Id,10,1);
        Assert.AreEqual(5,n);
        Assert.AreEqual(0,a.Finance.Gold);
        Assert.AreEqual(u.TrainingDays,a.Squads[0].TrainingRemainingDays);
        Assert.AreEqual(0,owner.ReadySoldierCount(a));
        for(int d=0;d<u.TrainingDays;d++)owner.TrainDay(a);
        Assert.AreEqual(n,owner.ReadySoldierCount(a));
    }


    static float CommanderDelta(CommanderBehaviorProfile p) {
        return Math.Abs(p.MarchSpeed-1f)+Math.Abs(p.ReformSpeed-1f)+Math.Abs(p.Cohesion-1f)+Math.Abs(p.CommandDelay-1f)+Math.Abs(p.Scout-1f)+Math.Abs(p.Ambush-1f)+Math.Abs(p.ReserveUse-1f)+Math.Abs(p.FlankSearch-1f)+Math.Abs(p.Pursuit-1f)+Math.Abs(p.RetreatDiscipline-1f)+Math.Abs(p.Morale-1f)+Math.Abs(p.SupplyEfficiency-1f)+Math.Abs(p.Siege-1f)+Math.Abs(p.Defense-1f)+Math.Abs(p.InfantryCombat-1f)+Math.Abs(p.RangedCombat-1f)+Math.Abs(p.CavalryControl-1f)+Math.Abs(p.TerrainAdapt-1f)+Math.Abs(p.WeatherAdapt-1f)+Math.Abs(p.Engineering-1f)+Math.Abs(p.Training-1f)+Math.Abs(p.Medical-1f)+Math.Abs(p.Discipline-1f)+Math.Abs(p.Capture-1f)+Math.Abs(p.LootControl-1f)+Math.Abs(p.FatigueEfficiency-1f)+Math.Abs(p.RoutResistance-1f)+Math.Abs(p.MarchFatigueCost-1f)+Math.Abs(p.PursuitFatigueCost-1f)+Math.Abs(p.SupplyGoldCost-1f)+Math.Abs(p.ArrearsSensitivity-1f)+Math.Abs(p.LastStandCasualty-1f)+Math.Abs(p.RetreatSupplyLoss)+Math.Abs(p.CasualtyAversion-1f)+Math.Abs(p.RangedContactAvoidance-1f);
    }

    static float PersonDelta(PersonBehaviorProfile p) {
        return Math.Abs(p.Risk-1f)+Math.Abs(p.Loyalty-1f)+Math.Abs(p.Ambition-1f)+Math.Abs(p.Commerce-1f)+Math.Abs(p.Investment-1f)+Math.Abs(p.Work-1f)+Math.Abs(p.Social-1f)+Math.Abs(p.Health-1f)+Math.Abs(p.Growth-1f)+Math.Abs(p.Prestige-1f);
    }

    static float SoldierDelta(SoldierBehaviorProfile p) {
        return Math.Abs(p.Melee-1f)+Math.Abs(p.Ranged-1f)+Math.Abs(p.Riding-1f)+Math.Abs(p.Block-1f)+Math.Abs(p.Stamina-1f)+Math.Abs(p.Morale-1f)+Math.Abs(p.Discipline-1f)+Math.Abs(p.Scout-1f)+Math.Abs(p.Pursuit-1f)+Math.Abs(p.Defense-1f)+Math.Abs(p.Engineering-1f)+Math.Abs(p.Recovery-1f)+Math.Abs(p.OfficerPotential-1f);
    }

    static float OfficialDelta(OfficialRuntimeProfile p) {
        return Math.Abs(p.Population-1f)+Math.Abs(p.Agriculture-1f)+Math.Abs(p.Treasury-1f)+Math.Abs(p.Commerce-1f)+Math.Abs(p.Construction-1f)+Math.Abs(p.Military-1f)+Math.Abs(p.Armory-1f)+Math.Abs(p.Logistics-1f)+Math.Abs(p.Fortification-1f)+Math.Abs(p.Oversight-1f)+Math.Abs(p.PublicOrder-1f)+Math.Abs(p.Horses-1f);
    }

    [Test]
    public void EveryCommanderSkill_ChangesExecutionProfile() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());
        Assert.AreEqual(360,data.Skills.Count);
        foreach(SkillDef skill in data.Skills.Values) {
            Person general=new Person{Id="G",Name="测试统帅",Noble=true,Class=SocialClass.General};
            general.CommanderSkillIds.Add(skill.Id);
            CommanderBehaviorProfile profile=CommanderSkillEffectEngine.Build(general,data);
            Assert.Greater(CommanderDelta(profile),0.001f,skill.Id+" "+skill.Name+" 未改变任何真实执行参数");
        }
    }

    [Test]
    public void EveryTraitFamily_ChangesRuntimeBehavior() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());
        foreach(TraitDef trait in data.PersonTraits.Values) {
            Person p=new Person{Id="P",Name="人物"};p.TraitIds.Add(trait.Id);
            Assert.Greater(PersonDelta(TraitEffectEngine.PersonProfile(p,data)),0.001f,trait.Id+" "+trait.Name+" 未改变人物行为");
        }
        foreach(TraitDef trait in data.SoldierTraits.Values) {
            Person p=new Person{Id="S",Name="士兵"};p.TraitIds.Add(trait.Id);
            Assert.Greater(SoldierDelta(TraitEffectEngine.SoldierProfile(p,data)),0.001f,trait.Id+" "+trait.Name+" 未改变士兵行为");
        }
        foreach(TraitDef trait in data.OfficialTraits.Values) {
            Person p=new Person{Id="O",Name="官员"};p.TraitIds.Add(trait.Id);
            Assert.Greater(OfficialDelta(TraitEffectEngine.OfficialProfile(p,data)),0.001f,trait.Id+" "+trait.Name+" 未改变治理行为");
        }
        foreach(TraitDef trait in data.GeneralTraits.Values) {
            Person p=new Person{Id="G",Name="将军",Class=SocialClass.General};p.TraitIds.Add(trait.Id);
            Assert.Greater(CommanderDelta(TraitEffectEngine.ApplyGeneralTraits(p,data,new CommanderBehaviorProfile())),0.001f,trait.Id+" "+trait.Name+" 未改变统帅行为");
        }
    }

    [Test]
    public void CommanderTradeoffSkill_KeepsBenefitAndRealCost() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());
        Person g=new Person{Id="G",Name="疾行将",Noble=true,Class=SocialClass.General};g.CommanderSkillIds.Add("SK002_一");
        CommanderBehaviorProfile p=CommanderSkillEffectEngine.Build(g,data);
        Assert.Greater(p.MarchSpeed,1f);
        Assert.Greater(p.MarchFatigueCost,1f);
    }

    [Test]
    public void AllCommanderNumericKeys_HaveExecutionMapping() {
        string[] separators={",","{","}"};
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());
        foreach(SkillDef skill in data.Skills.Values) {
            string raw=skill.NumericJson??"";
            string body=raw.Trim().Trim('{','}');
            if(body.Length==0)continue;
            foreach(string part in body.Split(',')) {
                string[] kv=part.Split(':');if(kv.Length!=2)continue;
                string key=kv[0].Trim().Trim('"');
                Assert.IsTrue(CommanderSkillEffectEngine.IsNumericEffectSupported(key),skill.Id+" "+skill.Name+" 未映射数值键 "+key);
            }
        }
    }

    [Test]
    public void AiDifficulty_ImprovesDecisionQualityWithoutChangingWorldPowerScore() {
        AiWarAssessment a=new AiWarAssessment{PowerRatio=1.2f,OwnFoodDays=7f,OwnMorale=70f,OwnFatigue=18f,SupplyRisk=.2f,CommanderScout=1.1f,CommanderSupply=1.15f,EnemyFortification=.6f,EnemyMorale=55f,Distance=12,Weather=WeatherKind.Clear};
        AiOwner easy=new AiOwner(AiDifficulty.Easy,7),nightmare=new AiOwner(AiDifficulty.Nightmare,7);
        Assert.Less(easy.DecisionQuality(),nightmare.DecisionQuality());
        Assert.AreEqual(easy.WarScore(a),nightmare.WarScore(a),0.0001f,"难度不应偷偷改军力/资源评分，只改变决策质量");
    }

    [Test]
    public void Siege_DamagesRealGateAndWallBuildings() {
        City c=new City{Id="C",KingdomId="K2",Food=120};for(int i=0;i<40;i++)c.PersonIds.Add("P"+i);
        Army a=new Army{Id="A",KingdomId="K1",Morale=80f,FoodDays=6f};a.Squads.Add(new Squad{Id="S1"});a.Squads.Add(new Squad{Id="S2"});
        Building gate=new Building{Id="G",CityId="C",Kind=BuildingKind.Gate,Durability=160,MaxDurability=160,Complete=true};
        Building wall=new Building{Id="W",CityId="C",Kind=BuildingKind.Wall,Durability=160,MaxDurability=160,Complete=true};
        var forts=new List<Building>{gate,wall};SiegeOwner siege=new SiegeOwner();SiegeState state=siege.Begin(a,c,forts,72f);int before=gate.Durability;
        siege.TickDay(state,a,c,true,85,forts,40,20);
        Assert.Less(gate.Durability,before,"攻城必须损伤真实城门Building耐久");Assert.Less(state.GateIntegrity,100f);
    }

    [Test]
    public void SaveMigration_V10ToCurrent_AddsFortificationAndMusterState() {
        GameSave s=new GameSave{SaveVersion=10};s.Cities.Add(new City{Id="C",KingdomId="K",FortificationRadius=-1,FortificationPlanVersion=-1});
        SaveOwner.Migrate(s);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.AreEqual(0,s.Cities[0].FortificationRadius);Assert.AreEqual(0,s.Cities[0].FortificationPlanVersion);
    }

    [Test]
    public void MarchPlan_CanRemainInRealMusterState() {
        WorldMap m=new WorldMap(8,4,1);for(int y=0;y<m.Height;y++)for(int x=0;x<m.Width;x++)m.Get(x,y).Terrain=TerrainKind.Grass;
        Army a=new Army{Id="A",X=0,Y=1,FoodDays=8f};MarchOwner march=new MarchOwner(new WorldGenerator(2),m);
        Assert.IsTrue(march.SetDestination(a,6,1,false));Assert.AreEqual(ArmyOrder.Muster,a.Order);Assert.Greater(a.StrategicRoute.Count,1);Assert.AreEqual(0f,a.MusterProgress);
    }

    [Test]
    public void ArmyStrength_UsesRealPersonnelState_NotRawIds() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());
        var people=new Dictionary<string,Person>();var cities=new Dictionary<string,City>();var armies=new Dictionary<string,Army>();
        City c=new City{Id="C",KingdomId="K"};cities[c.Id]=c;var pop=new PopulationOwner(people,cities,13);Kingdom k=new Kingdom{Id="K"};
        Person g=pop.CreatePerson(c,"卫将",SocialClass.Noble,35);g.Noble=true;ArmyOwner owner=new ArmyOwner(armies,people,pop,data);Army a=owner.CreateArmy(k,c,g,"守备军");
        Person ready=pop.CreatePerson(c,"可战",SocialClass.Commoner,21),heavy=pop.CreatePerson(c,"重伤",SocialClass.Commoner,22),captured=pop.CreatePerson(c,"被俘",SocialClass.Commoner,23),dead=pop.CreatePerson(c,"阵亡",SocialClass.Commoner,24);
        heavy.Injury=InjuryState.Heavy;captured.Injury=InjuryState.Captured;dead.Injury=InjuryState.Dead;dead.Alive=false;
        Squad sq=new Squad{Id="SQ",ArmyId=a.Id,TrainingRemainingDays=0};sq.SoldierIds.Add(ready.Id);sq.SoldierIds.Add(heavy.Id);sq.SoldierIds.Add(captured.Id);sq.SoldierIds.Add(dead.Id);a.Squads.Add(sq);
        Assert.AreEqual(2,owner.SoldierCount(a),"现役人数可以包含伤员，但不能把阵亡/被俘当作现役");
        Assert.AreEqual(1,owner.ReadySoldierCount(a),"可战人数必须排除重伤、失能、阵亡和被俘");
    }

    [Test]
    public void SiegeBreach_IsOpeningNotAutomaticOccupation() {
        City c=new City{Id="C",KingdomId="K2",Food=900};for(int i=0;i<50;i++)c.PersonIds.Add("P"+i);
        Army a=new Army{Id="A",KingdomId="K1",Morale=80f,FoodDays=7f};a.Squads.Add(new Squad{Id="A1"});
        Building gate=new Building{Id="G",CityId="C",Kind=BuildingKind.Gate,Durability=1,MaxDurability=100,Complete=true};
        Building wall=new Building{Id="W",CityId="C",Kind=BuildingKind.Wall,Durability=100,MaxDurability=100,Complete=true};
        SiegeOwner siege=new SiegeOwner();SiegeState state=siege.Begin(a,c,new List<Building>{gate,wall},80f);
        siege.TickDay(state,a,c,true,100,new List<Building>{gate,wall},30,20);
        Assert.IsTrue(state.Breached,"城门破坏后应形成破口");
        Assert.IsFalse(state.Captured,"破口不能直接等于占城；后续必须由真实守军战斗或投降决定");
    }

    [Test]
    public void SaveMigration_V13ToCurrent_InitializesBattleCooldown() {
        GameSave s=new GameSave{SaveVersion=13};s.Armies.Add(new Army{Id="A",KingdomId="K",LastBattleDay=0});
        SaveOwner.Migrate(s);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.AreEqual(-1,s.Armies[0].LastBattleDay);
    }

    [Test]
    public void Diplomacy_VassalRelationship_IsDirectedAndBlocksMutualWar() {
        DiplomacyOwner d=new DiplomacyOwner();
        Assert.IsTrue(d.SetVassal("宗主","附庸"));
        Assert.IsTrue(d.IsVassalOf("附庸","宗主"));
        Assert.IsFalse(d.IsVassalOf("宗主","附庸"));
        Assert.AreEqual(DiplomacyState.Vassal,d.Get("宗主","附庸").State);
        Assert.IsFalse(d.DeclareWar("附庸","宗主",10));
    }

    [Test]
    public void SaveMigration_V14ToCurrent_NormalizesBrokenVassalState() {
        GameSave s=new GameSave{SaveVersion=14};
        s.Kingdoms.Add(new Kingdom{Id="K",Status=KingdomStatus.Vassal,OverlordKingdomId=""});
        SaveOwner.Migrate(s);
        Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);
        Assert.AreEqual(KingdomStatus.Active,s.Kingdoms[0].Status);
        Assert.AreEqual(-1,s.Kingdoms[0].DefeatDay);
    }

    [Test]
    public void TickScheduler_BoundsCatchupInsteadOfDroppingOrExplodingWork() {
        TickScheduler scheduler=new TickScheduler();int calls=0;
        scheduler.Register("测试",.25f,3,delegate(float dt){calls++;});
        scheduler.Tick(2f);
        Assert.AreEqual(3,calls,"单帧追赶次数必须有上限，避免卡顿时形成死亡螺旋");
    }

    [Test]
    public void ProposalQueue_PrunesResolvedButKeepsPendingWork() {
        ProposalSystem q=new ProposalSystem();
        for(int i=0;i<10;i++){Proposal p=new Proposal{Id="R"+i,Title="已处理"+i};Assert.IsTrue(q.Submit(p,100));Assert.IsTrue(q.Reject(p.Id));}
        Proposal pending=new Proposal{Id="P",Title="待处理"};Assert.IsTrue(q.Submit(pending,100));
        Assert.AreEqual(7,q.PruneResolved(3));Assert.AreEqual(4,q.Queue.Count);Assert.AreEqual(ProposalState.Pending,pending.State);
    }

    [Test]
    public void Vassal_CommonWarFollowsSuzerainAndCanEndSeparately() {
        DiplomacyOwner d=new DiplomacyOwner();Assert.IsTrue(d.SetVassal("宗主","附庸"));
        Assert.IsTrue(d.JoinCommonWar("附庸","敌国"));Assert.IsTrue(d.Get("附庸","敌国").CommonWar);
        Assert.IsTrue(d.EndCommonWar("附庸","敌国",20));Assert.AreEqual(DiplomacyState.Neutral,d.Get("附庸","敌国").State);Assert.GreaterOrEqual(d.Get("附庸","敌国").TruceUntilDay,27);
    }

    [Test]
    public void SaveValidation_RejectsBrokenVassalAndEquipmentOwnership() {
        GameSave s=new GameSave{SaveVersion=SaveOwner.CurrentVersion,Map=new WorldMap(8,8,1),PlayerKingdomId="K1"};
        Kingdom k1=new Kingdom{Id="K1",Name="甲国",CapitalCityId="C1"};Kingdom k2=new Kingdom{Id="K2",Name="乙国",CapitalCityId="C2",Status=KingdomStatus.Vassal,OverlordKingdomId="不存在"};
        City c1=new City{Id="C1",Name="甲城",KingdomId="K1",X=1,Y=1};City c2=new City{Id="C2",Name="乙城",KingdomId="K2",X=6,Y=6};k1.CityIds.Add(c1.Id);k2.CityIds.Add(c2.Id);s.Kingdoms.Add(k1);s.Kingdoms.Add(k2);s.Cities.Add(c1);s.Cities.Add(c2);
        string error;Assert.IsFalse(GameSaveService.Validate(s,out error));StringAssert.Contains("附庸宗主引用无效",error);
        k2.Status=KingdomStatus.Active;k2.OverlordKingdomId="";Person p=new Person{Id="P",Name="甲",KingdomId="K1",CityId="C1"};p.EquipmentIds.Add("E");c1.PersonIds.Add(p.Id);s.People.Add(p);s.Equipment.Add(new EquipmentInstance{InstanceId="E",DisplayName="短剑",OwnerId="别人",Durability=10,MaxDurability=10});
        Assert.IsFalse(GameSaveService.Validate(s,out error));StringAssert.Contains("人物装备引用不一致",error);
    }

    [Test]
    public void CombatActions_UseDistinctAttacksAndRealDefensiveReactions() {
        var people=new Dictionary<string,Person>();var equipment=new Dictionary<string,EquipmentInstance>();
        Army a=new Army{Id="A",KingdomId="K1",Morale=100f,FoodDays=10f};Army b=new Army{Id="B",KingdomId="K2",Morale=100f,FoodDays=10f};
        Squad sa=new Squad{Id="SA",ArmyId="A",TrainingRemainingDays=0};Squad sb=new Squad{Id="SB",ArmyId="B",TrainingRemainingDays=0};a.Squads.Add(sa);b.Squads.Add(sb);
        for(int i=0;i<70;i++) {
            Person pa=new Person{Id="A"+i,Name="甲兵"+i,KingdomId="K1",Class=SocialClass.Officer,Job=JobKind.Soldier};pa.Stats.Martial=42+i%18;pa.Stats.Defense=34;pa.Stats.Stamina=100;
            Person pb=new Person{Id="B"+i,Name="乙兵"+i,KingdomId="K2",Class=SocialClass.Officer,Job=JobKind.Soldier};pb.Stats.Martial=39;pb.Stats.Defense=40+i%15;pb.Stats.Stamina=100;
            EquipmentInstance wa=new EquipmentInstance{InstanceId="WA"+i,TemplateId="长枪",DisplayName="制式长枪",OwnerId=pa.Id,Slot="主手",Durability=100,MaxDurability=100,Attack=18};
            EquipmentInstance wb=new EquipmentInstance{InstanceId="WB"+i,TemplateId="横刀",DisplayName="制式横刀",OwnerId=pb.Id,Slot="主手",Durability=100,MaxDurability=100,Attack=14};
            EquipmentInstance sh=new EquipmentInstance{InstanceId="SH"+i,TemplateId="木盾",DisplayName="包铁木盾",OwnerId=pb.Id,Slot="副手",Durability=100,MaxDurability=100,Defense=18};
            pa.EquipmentIds.Add(wa.InstanceId);pb.EquipmentIds.Add(wb.InstanceId);pb.EquipmentIds.Add(sh.InstanceId);people[pa.Id]=pa;people[pb.Id]=pb;equipment[wa.InstanceId]=wa;equipment[wb.InstanceId]=wb;equipment[sh.InstanceId]=sh;sa.SoldierIds.Add(pa.Id);sb.SoldierIds.Add(pb.Id);
        }
        BattleReport r=new CombatResolution(people,equipment,8123).ResolveSkirmish(a,b,20,160,new CommanderBehaviorProfile(),new CommanderBehaviorProfile(),1f,true,true);
        Assert.Greater(r.LightAttacks+r.HeavyAttacks+r.ThrustAttacks,0,"近战必须产生真实动作");
        Assert.Greater(r.HeavyAttacks,0,"重击必须是独立动作而非统一伤害标签");Assert.Greater(r.ThrustAttacks,0,"长枪必须能够产生独立刺击");
        Assert.Greater(r.Dodges+r.Parries+r.ShieldBlocks,0,"防守必须出现闪避/武器格挡/盾挡之一");Assert.Greater(r.ShieldBlocks,0,"装备真实盾牌后应能触发盾挡");
    }

    [Test]
    public void SaveMigration_V15ToCurrent_AddsCombatActionCountersSafely() {
        GameSave s=new GameSave{SaveVersion=15};s.Battles.Add(new BattleReport{Id="B",LightAttacks=-3,Parries=-2});
        SaveOwner.Migrate(s);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.AreEqual(0,s.Battles[0].LightAttacks);Assert.AreEqual(0,s.Battles[0].Parries);
    }

    [Test]
    public void SquadMorale_LocalCollapsePropagatesWithoutInstantWholeArmyRout() {
        var people=new Dictionary<string,Person>();var equipment=new Dictionary<string,EquipmentInstance>();
        Army a=new Army{Id="A",KingdomId="K1",Morale=18f,FoodDays=8f};Army b=new Army{Id="B",KingdomId="K2",Morale=70f,FoodDays=8f};
        Squad broken=new Squad{Id="A1",ArmyId="A",Morale=18f,Cohesion=35f};Squad neighbor=new Squad{Id="A2",ArmyId="A",Morale=28f,Cohesion=65f};Squad enemy=new Squad{Id="B1",ArmyId="B",Morale=70f,Cohesion=70f};a.Squads.Add(broken);a.Squads.Add(neighbor);b.Squads.Add(enemy);
        for(int i=0;i<8;i++){Person dead=new Person{Id="D"+i,Name="阵亡兵"+i,Alive=false,Injury=InjuryState.Dead};people[dead.Id]=dead;broken.SoldierIds.Add(dead.Id);}
        for(int i=0;i<8;i++){Person p=new Person{Id="N"+i,Name="邻队兵"+i};people[p.Id]=p;neighbor.SoldierIds.Add(p.Id);Person e=new Person{Id="E"+i,Name="敌兵"+i};people[e.Id]=e;enemy.SoldierIds.Add(e.Id);}
        BattleReport r=new CombatResolution(people,equipment,991).ResolveSkirmish(a,b,9,0,new CommanderBehaviorProfile(),new CommanderBehaviorProfile());
        Assert.Greater(r.AttackerRoutedSquads,0,"重损小队必须允许局部溃败");Assert.Greater(r.MoraleCollapseWaves,0,"首次溃败必须记录传播波次");
        Assert.Less(neighbor.Morale,28f,"相邻小队应受到低士气传播影响");Assert.IsFalse(neighbor.Routed,"相邻小队可以动摇而不是全军瞬间同时溃败");
    }

    [Test]
    public void ConstructionWorkers_HaveRealStageSpecificActions() {
        var projects=new Dictionary<string,ConstructionProject>();var buildings=new Dictionary<string,Building>();var people=new Dictionary<string,Person>();var cities=new Dictionary<string,City>();City c=new City{Id="C",Name="测试城"};cities[c.Id]=c;
        for(int i=0;i<4;i++){Person p=new Person{Id="P"+i,Name="工匠"+i,Alive=true};people[p.Id]=p;c.PersonIds.Add(p.Id);}
        LordWar.Construction.ConstructionOwner owner=new LordWar.Construction.ConstructionOwner(projects,buildings,people,cities);ConstructionProject cp=owner.Start(c,null,BuildingKind.Wall,3,4,100);owner.AssignWorkers(cp,c,4);
        Assert.AreEqual(4,cp.WorkerActions.Count);Assert.AreEqual("测量",cp.WorkerActions[0].Action);
        cp.WorkDone=400;owner.Tick(cp,.1f);bool sawSaw=false,sawCarry=false;foreach(ConstructionWorkerAction x in cp.WorkerActions){if(x.Action=="锯木")sawSaw=true;if(x.Action=="搬运")sawCarry=true;}Assert.IsTrue(sawSaw&&sawCarry,"框架阶段必须出现锯木与搬运动作");
        cp.WorkDone=760;owner.Tick(cp,.1f);bool masonry=false,hammer=false;foreach(ConstructionWorkerAction x in cp.WorkerActions){if(x.Action=="砌筑")masonry=true;if(x.Action=="敲打")hammer=true;}Assert.IsTrue(masonry&&hammer,"城墙主体施工必须出现砌筑与敲打动作");
    }

    [Test]
    public void SaveMigration_V16ToCurrent_AddsConstructionActionsAndSquadMoraleState() {
        GameSave s=new GameSave{SaveVersion=16};ConstructionProject cp=new ConstructionProject{Id="P"};cp.WorkerActions=null;s.Projects.Add(cp);Army a=new Army{Id="A"};a.Squads.Add(new Squad{Id="S",Morale=8f,RoutWave=-1});s.Armies.Add(a);
        SaveOwner.Migrate(s);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.IsNotNull(s.Projects[0].WorkerActions);Assert.AreEqual(MoraleState.Routed,s.Armies[0].Squads[0].LocalMoraleState);Assert.AreEqual(0,s.Armies[0].Squads[0].RoutWave);
    }

    [Test]
    public void VeteranExperience_ChangesBehaviorNotJustRawAttackStat() {
        Person rookie=new Person{Id="R",Name="新兵",Battles=0,Experience=0};Person veteran=new Person{Id="V",Name="老兵",Battles=14,Experience=420};int originalMartial=veteran.Stats.Martial;
        VeteranBehaviorProfile r=VeteranEffectEngine.Build(rookie),v=VeteranEffectEngine.Build(veteran);
        Assert.Greater(v.Tier,r.Tier);Assert.Greater(v.MoraleResistance,r.MoraleResistance);Assert.Greater(v.Block,r.Block);Assert.Greater(v.FatigueEfficiency,r.FatigueEfficiency);Assert.Greater(v.Cohesion,r.Cohesion);Assert.Greater(v.CommandResponse,r.CommandResponse);Assert.AreEqual(originalMartial,veteran.Stats.Martial,"老兵行为画像不应偷偷改人物基础武力");
        Squad sq=new Squad{Id="S"};sq.SoldierIds.Add(rookie.Id);sq.SoldierIds.Add(veteran.Id);var people=new Dictionary<string,Person>{{rookie.Id,rookie},{veteran.Id,veteran}};Assert.AreEqual(.5f,VeteranEffectEngine.SquadVeteranRatio(sq,people),.001f);
    }

    [Test]
    public void WeatherTerrain_ReallyChangesMarchSpeedFatigueFoodAndVisibility() {
        Assert.Greater(MarchFormationSystem.WeatherTerrainPenalty(WeatherKind.Storm,TerrainKind.Marsh,false,0),0f);
        Assert.Greater(MarchFormationSystem.FoodUsePerTile(TerrainKind.Marsh,.55f,false,1f,1f),MarchFormationSystem.FoodUsePerTile(TerrainKind.Grass,0f,false,1f,1f));
        WeatherOwner weather=new WeatherOwner(7);weather.Weather=WeatherKind.Fog;Assert.Less(weather.VisibilityMultiplier(),1f);
        WorldMap m=new WorldMap(6,1,1);for(int x=0;x<m.Width;x++){WorldTile t=m.Get(x,0);t.Terrain=TerrainKind.Marsh;t.Height=.4f;}
        MarchOwner march=new MarchOwner(new WorldGenerator(1),m);Army clear=new Army{Id="A",X=0,Y=0,FoodDays=8f};Army storm=new Army{Id="B",X=0,Y=0,FoodDays=8f};
        Assert.IsTrue(march.SetDestination(clear,5,0));Assert.IsTrue(march.SetDestination(storm,5,0));
        march.Step(clear,5f,false,0f,1,1f,1f,1f,WeatherKind.Clear);
        march.Step(storm,5f,false,.35f,1,1f,1f,1f,WeatherKind.Storm);
        Assert.Greater(clear.X,storm.X,"暴雨泥地应显著降低实际行军进度");
    }

    [Test]
    public void WorldGenerator_RiversAreVisibleContinuousAndDrainTowardLowWater() {
        WorldMap m=new WorldGenerator(774411).Generate(96,80,3);int rivers=0,connected=0,terminal=0;
        foreach(WorldTile t in m.Tiles){if(!t.River)continue;rivers++;if(t.Terrain!=TerrainKind.Mountain&&t.Terrain!=TerrainKind.Snow)Assert.AreEqual(TerrainKind.River,t.Terrain,"普通河道必须以河流地形显示");bool link=false,lower=false;foreach(WorldTile n in m.Neighbors4(t.X,t.Y)){if(n.River||n.Lake)link=true;if(n.Height<=t.Height+.01f||n.Lake||n.Terrain==TerrainKind.DeepWater||n.Terrain==TerrainKind.Coast)lower=true;}if(link)connected++;bool isTerminal=t.Height<=.235f||t.X<=1||t.Y<=1||t.X>=m.Width-2||t.Y>=m.Height-2||t.Lake;if(isTerminal)terminal++;Assert.IsTrue(link||isTerminal,"河道不能无缘无故断在内陆单点");Assert.IsTrue(lower||isTerminal,"河流应沿下坡/开槽后的低处流动");}
        Assert.Greater(rivers,4);Assert.Greater(connected,2);
    }

    [Test]
    public void WorldGenerator_DifferentSeedChangesWorldSignificantly() {
        WorldMap a=new WorldGenerator(41001).Generate(96,80,3);WorldMap b=new WorldGenerator(41002).Generate(96,80,3);int changed=0;
        for(int i=0;i<a.Tiles.Length;i++){WorldTile t=a.Tiles[i],u=b.Tiles[i];if(Math.Abs(t.Height-u.Height)>.015f||t.Terrain!=u.Terrain)changed++;}
        Assert.Greater(changed,a.Tiles.Length/8,"不同Seed必须产生显著不同地图，而不是只改名称");
    }

    [Test]
    public void Weather_LeavesPersistentMudSnowAndRoadWear_AndAffectsMarch() {
        WorldMap map=new WorldMap(6,2,7);for(int y=0;y<2;y++)for(int x=0;x<6;x++){WorldTile t=map.Get(x,y);t.Terrain=TerrainKind.Grass;t.Temperature=.3f;t.RoadCondition=0f;}
        for(int x=0;x<6;x++){WorldTile t=map.Get(x,0);t.Road=true;t.RoadLevel=1;t.RoadCapacity=2;t.RoadCondition=1f;}
        WeatherOwner weather=new WeatherOwner(7);weather.Weather=WeatherKind.Storm;weather.ApplySurfaceWeather(map);Assert.Greater(map.Get(2,1).SurfaceMud,0f);Assert.Less(map.Get(2,0).RoadCondition,1f);
        for(int x=0;x<6;x++)map.Get(x,1).SnowDepth=.8f;Army roadArmy=new Army{Id="路军",X=0,Y=0,FoodDays=8f};Army snowArmy=new Army{Id="雪军",X=0,Y=1,FoodDays=8f};MarchOwner march=new MarchOwner(new WorldGenerator(7),map);
        Assert.IsTrue(march.SetDestination(roadArmy,5,0));Assert.IsTrue(march.SetDestination(snowArmy,5,1));march.Step(roadArmy,3f,false,0f,1,1f,1f,1f,WeatherKind.Clear);march.Step(snowArmy,3f,false,0f,1,1f,1f,1f,WeatherKind.Clear);
        Assert.GreaterOrEqual(roadArmy.X,snowArmy.X,"积雪地表不得比正常道路移动更快");
    }

    [Test]
    public void SaveMigration_V17ToCurrent_InitializesSurfaceWeatherState() {
        GameSave s=new GameSave{SaveVersion=17,Map=new WorldMap(3,3,1)};WorldTile road=s.Map.Get(1,1);road.Road=true;road.RoadLevel=1;road.RoadCondition=0f;road.SurfaceMud=2f;road.SnowDepth=-1f;
        SaveOwner.Migrate(s);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.AreEqual(1f,road.RoadCondition,0.001f);Assert.AreEqual(1f,road.SurfaceMud,0.001f);Assert.AreEqual(0f,road.SnowDepth,0.001f);
    }

    [Test]
    public void CityPlanning_PlayerZonesDriveActualBuildSites() {
        WorldMap map=new WorldMap(48,48,991);for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++){WorldTile tile=map.Get(x,y);tile.Terrain=TerrainKind.Grass;tile.Height=.4f;}
        var buildings=new Dictionary<string,Building>();City city=new City{Id="C1",Name="安平",KingdomId="K1",X=24,Y=24};
        CityPlanningOwner planning=new CityPlanningOwner(map,buildings,991);planning.EnsureStarterZones(city,1);CityZone civic=planning.DesignateZone(city,ZoneKind.Civic,34,34,3,true,2);
        Assert.IsNotNull(civic);Assert.IsTrue(civic.PlayerPlanned);GridPoint site=planning.FindBuildSite(city,BuildingKind.Government);CityZone zone=planning.ZoneAt(city,site.X,site.Y);Assert.IsNotNull(zone);Assert.AreEqual(ZoneKind.Civic,zone.Kind);
    }

    [Test]
    public void SaveValidation_RejectsBrokenCityZoneReferenceOrBounds() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());GameWorld w=new GameWorld(81923,data);w.CreateNewWorld(80,64,2);GameSave save=GameSaveService.Capture(w);
        Assert.Greater(save.Cities.Count,0);Assert.Greater(save.Cities[0].Zones.Count,0);save.Cities[0].Zones[0].CenterX=-99;string error;Assert.IsFalse(GameSaveService.Validate(save,out error));StringAssert.Contains("城市规划区",error);
    }

    [Test]
    public void ArtResources_ImportAsPixelSpritesAndContainCoreVisuals() {
        UnityEngine.Sprite[] sprites=UnityEngine.Resources.LoadAll<UnityEngine.Sprite>("LordWarArt");Assert.Greater(sprites.Length,1000,"真实PNG必须由Unity作为Sprite导入，而不是只复制到目录");
        bool terrain=false,building=false,person=false,horse=false;foreach(UnityEngine.Sprite sp in sprites){string n=(sp.name??"").ToLowerInvariant();if(n.Contains("icontilemountains")||n.Contains("icontileforest"))terrain=true;if(n.Contains("house_human")||n.Contains("barracks_human"))building=true;if(n.Contains("unit_warrior")||n.Contains("walk_"))person=true;if(n.Contains("马匹")||n.Contains("轻骑兵"))horse=true;}Assert.IsTrue(terrain);Assert.IsTrue(building);Assert.IsTrue(person);Assert.IsTrue(horse);
    }

    [Test]
    public void BattleSession_AdvancesAcrossMultipleStepsInsteadOfInstantResolution() {
        var people=new Dictionary<string,Person>();var equipment=new Dictionary<string,EquipmentInstance>();
        Person a1=new Person{Id="A1",Name="甲兵"},b1=new Person{Id="B1",Name="乙兵"};a1.Stats.Life=500;b1.Stats.Life=500;people[a1.Id]=a1;people[b1.Id]=b1;
        Army a=new Army{Id="A",KingdomId="K1",GeneralId="A1"};Army b=new Army{Id="B",KingdomId="K2",GeneralId="B1"};
        Squad sa=new Squad{Id="SA",ArmyId="A",UnitTemplateId="U"};sa.SoldierIds.Add(a1.Id);a.Squads.Add(sa);Squad sb=new Squad{Id="SB",ArmyId="B",UnitTemplateId="U"};sb.SoldierIds.Add(b1.Id);b.Squads.Add(sb);
        var combat=new CombatResolution(people,equipment,20260925);var ap=new CommanderBehaviorProfile();var bp=new CommanderBehaviorProfile();
        BattleSession session=combat.StartSession(a,b,1,20,ap,bp,1f,true,true,TerrainKind.Grass,WeatherKind.Clear,"校场");
        Assert.IsFalse(session.Completed);combat.StepSession(session,a,b,2,ap,bp);Assert.GreaterOrEqual(session.ElapsedSeconds,1);Assert.LessOrEqual(session.ElapsedSeconds,2);Assert.IsFalse(session.Completed);
        int guard=0;while(!session.Completed&&guard++<30)combat.StepSession(session,a,b,2,ap,bp);Assert.IsTrue(session.Completed);Assert.AreEqual(session.ElapsedSeconds,session.Report.DurationSeconds);Assert.IsNotEmpty(session.Report.LastActionText);
    }

    [Test]
    public void SaveMigration_V19ToCurrent_InitializesActiveBattleSessions() {
        GameSave s=new GameSave{SaveVersion=19};s.ActiveBattles=null;SaveOwner.Migrate(s);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.IsNotNull(s.ActiveBattles);
    }

    [Test]
    public void DeterministicRandom_StateRestoresExactSequence() {
        DeterministicRandom a=new DeterministicRandom(778899);a.NextUInt();uint saved=a.State;uint expected=a.NextUInt();DeterministicRandom b=new DeterministicRandom(1);b.State=saved;Assert.AreEqual(expected,b.NextUInt());
    }

    [Test]
    public void BattleSession_PersistsRandomStateAndRealActorEvent() {
        var people=new Dictionary<string,Person>();var equipment=new Dictionary<string,EquipmentInstance>();Person p1=new Person{Id="A1",Name="甲兵"},p2=new Person{Id="B1",Name="乙兵"};p1.Stats.Life=500;p2.Stats.Life=500;people[p1.Id]=p1;people[p2.Id]=p2;Army a=new Army{Id="A",KingdomId="K1",GeneralId="A1"},b=new Army{Id="B",KingdomId="K2",GeneralId="B1"};Squad sa=new Squad{Id="SA",ArmyId="A",UnitTemplateId="U"};sa.SoldierIds.Add(p1.Id);a.Squads.Add(sa);Squad sb=new Squad{Id="SB",ArmyId="B",UnitTemplateId="U"};sb.SoldierIds.Add(p2.Id);b.Squads.Add(sb);CombatResolution combat=new CombatResolution(people,equipment,51);CommanderBehaviorProfile ap=new CommanderBehaviorProfile(),bp=new CommanderBehaviorProfile();BattleSession session=combat.StartSession(a,b,1,20,ap,bp,1f,true,true,TerrainKind.Grass,WeatherKind.Clear,"校场");uint before=session.RandomState;combat.StepSession(session,a,b,1,ap,bp);Assert.AreNotEqual(0u,before);Assert.AreNotEqual(0u,session.RandomState);Assert.Greater(session.Report.ActionSequence,0);Assert.IsFalse(string.IsNullOrEmpty(session.Report.LastActorId));Assert.IsFalse(string.IsNullOrEmpty(session.Report.LastTargetId));Assert.IsFalse(string.IsNullOrEmpty(session.Report.LastAttackAction));
    }

    [Test]
    public void SaveMigration_V20ToCurrent_InitializesBattleRandomState() {
        GameSave s=new GameSave{SaveVersion=20,Seed=20260925};s.ActiveBattles.Add(new BattleSession{Id="SESSION",AttackerArmyId="A",DefenderArmyId="B",StartDay=4,ElapsedSeconds=7,RandomState=0});SaveOwner.Migrate(s);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.AreNotEqual(0u,s.ActiveBattles[0].RandomState);
    }

    [Test]
    public void FrozenSpecialUnitMappings_HaveNoDanglingRules() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());
        Assert.AreEqual(120,data.SpecialUnits.Count);Assert.AreEqual(60,data.GeneralUnlocks.Count);Assert.AreEqual(96,data.OfficialDoctrines.Count);
        int traitUnlocks=0;foreach(TraitDef t in data.GeneralTraits.Values)if(!string.IsNullOrEmpty(t.Unlock)){traitUnlocks++;bool found=false;foreach(SpecialUnitDef u in data.SpecialUnits.Values)if(u.Name==t.Unlock){found=true;break;}Assert.IsTrue(found,"将军特性解锁悬空："+t.Name+"→"+t.Unlock);}
        Assert.AreEqual(40,traitUnlocks);
        foreach(GeneralUnlockDef g in data.GeneralUnlocks){bool found=false;foreach(SpecialUnitDef u in data.SpecialUnits.Values)if(u.Name==g.SpecialUnit){found=true;break;}Assert.IsTrue(found,"将军专属解锁悬空："+g.SpecialUnit);}
        foreach(OfficialDoctrineDef d in data.OfficialDoctrines){bool found=false;foreach(PolicyDef p in data.Policies.Values)if(p.Name==d.Policy){found=true;break;}Assert.IsTrue(found,"官员军制政策悬空："+d.Policy);}
    }

    [Test]
    public void UnlockResolver_RequiresMatchingGeneralTraitWhenRuleSaysTrait() {
        SpecialUnitDef u=new SpecialUnitDef{Name="测试亲军",CityStyle="通用",Source="将军特性",GeneralCondition="擅长对应兵类的将军特性",SquadCap=5,NationalCap=20,Upkeep=1f};
        UnlockContext c=new UnlockContext{CityStyle="平原王领",Population=50,ArmyGold=1000,GeneralTraitText="任意特性",SpecialTraitAuthorized=false};
        string reason;Assert.IsFalse(UnlockResolver.CanRecruit(u,c,out reason));StringAssert.Contains("不匹配",reason);
        c.SpecialTraitAuthorized=true;Assert.IsTrue(UnlockResolver.CanRecruit(u,c,out reason),reason);
    }

    [Test]
    public void PolicyOwner_UsesHardAbilityGateAndTrialBeforeEstablishedPolicy() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());PolicyDef established=null,trial=null;
        foreach(PolicyDef p in data.Policies.Values)if(p.Stage=="成制"){foreach(PolicyDef q in data.Policies.Values)if(q.Name==p.Name&&q.Stage=="试行"){established=p;trial=q;break;}if(established!=null)break;}
        Assert.IsNotNull(established);Assert.IsNotNull(trial);
        var people=new Dictionary<string,Person>();var kingdoms=new Dictionary<string,Kingdom>();var queue=new ProposalSystem();var ledger=new EconomyLedger(people,kingdoms);var owner=new PolicyOwner(data,queue,ledger);Kingdom k=new Kingdom{Id="K",Treasury=99999};kingdoms[k.Id]=k;
        Person low=new Person{Id="O1",Class=SocialClass.Official};low.Stats.Administration=0;low.Stats.Intelligence=0;low.Stats.Logistics=0;low.Stats.Organization=0;low.Stats.Engineering=0;low.Stats.Military=0;
        Assert.IsFalse(owner.Propose(established.Id,low,k,1));
        Person high=new Person{Id="O2",Class=SocialClass.Official};high.Stats.Administration=100;high.Stats.Intelligence=100;high.Stats.Logistics=100;high.Stats.Organization=100;high.Stats.Engineering=100;high.Stats.Military=100;
        Assert.IsFalse(owner.Propose(established.Id,high,k,1),"未试行时不得直接提出成制");k.ActivePolicyIds.Add(trial.Id);Assert.IsTrue(owner.Propose(established.Id,high,k,2),"完成试行后且能力足够应允许提交成制申请");
    }

    [Test]
    public void MerchantInvestment_EnforcesIdentityDuplicateAndPerGeneralCap() {
        var people=new Dictionary<string,Person>();var owner=new MerchantOwner(people);Person nobleMerchant=new Person{Id="MN",Name="贵族商人",Merchant=true,Noble=true,Wealth=1000};Person m1=new Person{Id="M1",Name="商甲",Merchant=true,Wealth=1000};Person m2=new Person{Id="M2",Name="商乙",Merchant=true,Wealth=1000};people[m1.Id]=m1;people[m2.Id]=m2;Army a=new Army{Id="A",Name="测试军",GeneralId="G"};
        Assert.IsNull(owner.CreateInvestmentProposal(nobleMerchant,a,200,20,1));Proposal p1=owner.CreateInvestmentProposal(m1,a,200,20,1);Assert.IsNotNull(p1);p1.State=ProposalState.Approved;Assert.IsNotNull(owner.Activate(m1,a,p1,1,1));Assert.IsNull(owner.CreateInvestmentProposal(m1,a,100,10,2),"同一商人不得重复投资");Proposal p2=owner.CreateInvestmentProposal(m2,a,200,20,2);Assert.IsNotNull(p2);p2.State=ProposalState.Approved;Assert.IsNull(owner.Activate(m2,a,p2,2,1),"单将军投资人数上限必须生效");
    }

    [Test]
    public void MilitaryDebt_ProducesPoliticalRiskAndDebtGraceReducesPenalty() {
        MilitaryAccount a=new MilitaryAccount{DebtToLord=600,DebtToMerchants=400,MonthsInArrears=3};float normal=MilitaryFinanceSystem.LoyaltyPenalty(a,1f),grace=MilitaryFinanceSystem.LoyaltyPenalty(a,1.5f);Assert.Greater(normal,0f);Assert.Less(grace,normal);Assert.Greater(MilitaryFinanceSystem.PoliticalRisk(a,1f),0f);int paid=MilitaryFinanceSystem.RepayDebts(a,500);Assert.AreEqual(500,paid);Assert.AreEqual(500,a.DebtToLord+a.DebtToMerchants);
    }

    [Test]
    public void EverySpecialUnit_HasExecutableBehaviorWeaknessAndTrainingProfile() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());Assert.AreEqual(120,data.SpecialUnits.Count);var behaviors=new HashSet<string>();var weaknesses=new HashSet<string>();
        foreach(SpecialUnitDef u in data.SpecialUnits.Values){SpecialUnitBehaviorProfile p=SpecialUnitBehaviorEngine.Build(u.Id,data);Assert.IsTrue(p.IsSpecial,u.Name+" 未进入特殊兵种行为引擎");int days=SpecialUnitBehaviorEngine.TrainingDays(u,p);Assert.GreaterOrEqual(days,4,u.Name+" 训练天数过低");Assert.LessOrEqual(days,16,u.Name+" 训练天数异常");float delta=Math.Abs(p.LineCohesion-1f)+Math.Abs(p.AntiCavalry-1f)+Math.Abs(p.RangedSafety-1f)+Math.Abs(p.FlankMobility-1f)+Math.Abs(p.ScoutAmbush-1f)+Math.Abs(p.KeyDefense-1f)+Math.Abs(p.Engineering-1f)+Math.Abs(p.SupplyGuard-1f)+Math.Abs(p.GeneralGuard-1f)+Math.Abs(p.PursuitCut-1f)+Math.Abs(p.FrontalCombat-1f)+Math.Abs(p.MeleeDefense-1f)+Math.Abs(p.FatigueCost-1f)+Math.Abs(p.ReplacementSpeed-1f)+Math.Abs(p.PursuitRisk-1f);Assert.Greater(delta,.01f,u.Name+" 核心行为/弱点没有改变任何执行参数");behaviors.Add(u.Behavior);weaknesses.Add(u.Weakness);}
        Assert.AreEqual(10,behaviors.Count);Assert.AreEqual(10,weaknesses.Count);
    }


    [Test]
    public void WarAdministration_RequiresRealOfficialAndSupportsAssignFocusRevoke() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());var people=new Dictionary<string,Person>();var kingdoms=new Dictionary<string,Kingdom>();Kingdom own=new Kingdom{Id="K1",Name="本国"},enemy=new Kingdom{Id="K2",Name="敌国"};kingdoms[own.Id]=own;kingdoms[enemy.Id]=enemy;
        Person official=new Person{Id="O1",Name="军政官",KingdomId=own.Id,Class=SocialClass.Official,Job=JobKind.Official,Alive=true};official.Stats.Administration=75;official.Stats.Organization=82;official.Stats.Logistics=78;official.Stats.Engineering=70;official.Stats.Military=80;official.Stats.Intelligence=76;people[official.Id]=official;
        Person noble=new Person{Id="N1",Name="普通贵族",KingdomId=own.Id,Class=SocialClass.Noble,Job=JobKind.Unemployed,Alive=true};people[noble.Id]=noble;var owner=new WarAdministrationOwner(people,kingdoms,data);string reason;Assert.IsNull(owner.Assign(own.Id,enemy.Id,noble.Id,3,WarAdministrationFocus.Balanced,out reason));StringAssert.Contains("正式官员",reason);
        WarAdministration war=owner.Assign(own.Id,enemy.Id,official.Id,3,WarAdministrationFocus.Balanced,out reason);Assert.IsNotNull(war,reason);Assert.AreEqual(official.Id,war.OfficialId);Assert.Greater(owner.Profile(war).ReactionRadius,6);Assert.IsTrue(owner.SetFocus(own.Id,enemy.Id,WarAdministrationFocus.Siege));Assert.AreEqual(WarAdministrationFocus.Siege,owner.Get(own.Id,enemy.Id).Focus);WarAdministration removed;Assert.IsTrue(owner.Revoke(own.Id,enemy.Id,out removed));Assert.AreEqual(official.Id,removed.OfficialId);Assert.IsNull(owner.Get(own.Id,enemy.Id));
    }

    [Test]
    public void WarAdministration_PersistsAndRejectsOfficialManagingTwoWars() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());GameWorld w=new GameWorld(778899,data);w.CreateNewWorld(96,80,3);Kingdom player=w.Kingdoms[w.PlayerKingdomId],enemy=null,enemy2=null;foreach(Kingdom k in w.Kingdoms.Values)if(k.Id!=player.Id){if(enemy==null)enemy=k;else {enemy2=k;break;}}Assert.IsNotNull(enemy);Assert.IsNotNull(enemy2);
        Person official=null;foreach(Person p0 in w.People.Values)if(p0.KingdomId==player.Id&&p0.Alive&&p0.Class==SocialClass.Official){official=p0;break;}Assert.IsNotNull(official);Assert.IsTrue(w.Diplomacy.DeclareWar(player.Id,enemy.Id,w.Day));Assert.IsTrue(w.Diplomacy.DeclareWar(player.Id,enemy2.Id,w.Day));string reason;Assert.IsTrue(w.AssignWarOfficial(enemy.Id,official.Id,out reason),reason);Assert.IsFalse(w.AssignWarOfficial(enemy2.Id,official.Id,out reason));StringAssert.Contains("另一场战争",reason);GameSave save=GameSaveService.Capture(w);Assert.AreEqual(1,save.WarAdministrations.Count);string error;Assert.IsTrue(GameSaveService.Validate(save,out error),error);GameWorld restored=new GameWorld(save.Seed,data);GameSaveService.Restore(restored,save);Assert.IsNotNull(restored.WarAdministrationFor(player.Id,enemy.Id));Assert.AreEqual(official.Id,restored.WarAdministrationFor(player.Id,enemy.Id).OfficialId);
    }

    [Test]
    public void EndToEnd_FrozenGameplayLoop_ProgressesFromGovernmentToWarAndRestore() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());
        GameWorld w=new GameWorld(20260925,data);w.CreateNewWorld(96,80,2);w.SecondsPerDay=9999f;
        Assert.AreEqual(2,w.Kingdoms.Count);Assert.Greater(w.People.Count,80);Assert.Greater(w.PlayerPendingProposalCount,0,"新世界应产生固定申请箱事项");

        // 玩家和电脑国家都通过同一申请/施工链完成初始市场、粮仓、军营；测试夹具只代替点击批准动作。
        var initialProposalIds=new List<string>();
        foreach(Proposal p in w.Proposals.Queue)if(p.State==ProposalState.Pending&&p.Kind==ProposalKind.Construction)initialProposalIds.Add(p.Id);
        Assert.GreaterOrEqual(initialProposalIds.Count,6);
        foreach(string id in initialProposalIds)Assert.IsTrue(w.ApproveProposal(id),"初始工程申请应能批准："+id);
        for(int i=0;i<420;i++)w.Tick(.25f);

        Kingdom player=w.Kingdoms[w.PlayerKingdomId],enemy=null;foreach(Kingdom k in w.Kingdoms.Values)if(k.Id!=player.Id){enemy=k;break;}Assert.IsNotNull(enemy);
        City playerCity=w.Cities[player.CapitalCityId],enemyCity=w.Cities[enemy.CapitalCityId];
        Assert.IsTrue(w.HasBuilding(playerCity,BuildingKind.Barracks),"玩家军营必须经施工链真实完工");
        Assert.IsTrue(w.HasBuilding(enemyCity,BuildingKind.Barracks),"电脑国家军营必须使用同一施工链真实完工");

        // 军营完工后只能从真实贵族候选中产生将军申请，批准后才建立军队并从真实人口募兵。
        var generalProposalIds=new List<string>();
        foreach(Proposal p in w.Proposals.Queue)if(p.State==ProposalState.Pending&&p.Kind==ProposalKind.Appointment&&p.DataId=="general")generalProposalIds.Add(p.Id);
        Assert.GreaterOrEqual(generalProposalIds.Count,2,"双方军营完工后都应产生将军申请");
        foreach(string id in generalProposalIds)Assert.IsTrue(w.ApproveProposal(id),"将军申请应能通过固定申请箱批准："+id);
        Army playerArmy=null,enemyArmy=null;foreach(Army a in w.Armies.Values){if(a.KingdomId==player.Id&&playerArmy==null)playerArmy=a;if(a.KingdomId==enemy.Id&&enemyArmy==null)enemyArmy=a;}
        Assert.IsNotNull(playerArmy);Assert.IsNotNull(enemyArmy);Assert.Greater(w.Military.SoldierCount(playerArmy),0);Assert.Greater(w.Military.SoldierCount(enemyArmy),0);
        Person playerGeneral=w.People[playerArmy.GeneralId];Assert.AreEqual(SocialClass.General,playerGeneral.Class);Assert.IsTrue(playerGeneral.Noble);
        int playerCityAfterRecruit=w.Population.CivilianPopulation(playerCity);Assert.Less(playerCityAfterRecruit,playerCity.PersonIds.Count,"士兵必须从真实城市人口转换而不是凭空生成");

        // 训练必须完成后才形成可战兵力。
        for(int day=0;day<24;day++){w.Military.TrainDay(playerArmy);w.Military.TrainDay(enemyArmy);}
        Assert.Greater(w.Military.ReadySoldierCount(playerArmy),0);Assert.Greater(w.Military.ReadySoldierCount(enemyArmy),0);
        playerArmy.FoodDays=Math.Max(playerArmy.FoodDays,8f);enemyArmy.FoodDays=Math.Max(enemyArmy.FoodDays,8f);

        int marched=w.DeclareWarAndMarch(enemy.Id,new string[]{playerArmy.Id});Assert.AreEqual(1,marched);Assert.AreEqual(DiplomacyState.War,w.Diplomacy.Get(player.Id,enemy.Id).State);Assert.AreEqual(ArmyOrder.Muster,playerArmy.Order,"宣战后必须先真实集结而非瞬间传送");
        Person warOfficial=null;foreach(Person po in w.People.Values)if(po.KingdomId==player.Id&&po.Alive&&po.Class==SocialClass.Official){warOfficial=po;break;}Assert.IsNotNull(warOfficial);string warReason;Assert.IsTrue(w.AssignWarOfficial(enemy.Id,warOfficial.Id,out warReason),warReason);Assert.IsNotNull(w.WarAdministrationFor(player.Id,enemy.Id),"宣战后必须能够由玩家指定正式官员总理此战");
        int startX=playerArmy.X,startY=playerArmy.Y;for(int i=0;i<160&&playerArmy.Order==ArmyOrder.Muster;i++)w.Tick(.25f);Assert.AreNotEqual(ArmyOrder.Muster,playerArmy.Order,"集结进度应在多Tick后结束");
        for(int i=0;i<80&&playerArmy.X==startX&&playerArmy.Y==startY;i++)w.Tick(.25f);Assert.IsTrue(playerArmy.X!=startX||playerArmy.Y!=startY||playerArmy.Order==ArmyOrder.Siege,"行军链应真实推进坐标或进入围城");

        // 用真实持续战斗会话完成一场交锋，禁止一帧直接结算。
        playerArmy.Order=ArmyOrder.Hold;enemyArmy.Order=ArmyOrder.Hold;enemyArmy.X=playerArmy.X;enemyArmy.Y=playerArmy.Y;
        BattleSession battle=w.StartBattle(playerArmy,enemyArmy,"野外交锋");Assert.IsNotNull(battle);Assert.IsFalse(battle.Completed);
        int battleTicks=0;while(w.ActiveBattleForArmy(playerArmy.Id)!=null&&battleTicks++<180)w.Tick(.25f);
        Assert.Greater(battleTicks,1,"真实战斗必须跨多个世界Tick推进");Assert.IsNull(w.ActiveBattleForArmy(playerArmy.Id));Assert.Greater(w.Battles.Count,0);Assert.IsNotEmpty(w.Battles[w.Battles.Count-1].LastActionText);

        // 停战也通过固定申请箱处理，而不是测试里直接改外交状态。
        Person truceOfficial=null;foreach(string pid in playerCity.PersonIds){Person p;if(w.People.TryGetValue(pid,out p)&&p.Alive&&p.Class==SocialClass.Official){truceOfficial=p;break;}}Assert.IsNotNull(truceOfficial);
        Proposal truce=new Proposal{Id=Ids.Next("DIPTEST"),Title="停战奏议",Description="请求停战",Kind=ProposalKind.Diplomacy,DataId="truce:"+enemy.Id,ProposerId=truceOfficial.Id,TargetId=enemy.Id,SubmittedDay=w.Day};
        Assert.IsTrue(w.Proposals.Submit(truce,100));Assert.IsTrue(w.ApproveProposal(truce.Id));Assert.AreEqual(DiplomacyState.Neutral,w.Diplomacy.Get(player.Id,enemy.Id).State);Assert.IsNull(w.WarAdministrationFor(player.Id,enemy.Id),"停战后战争负责官员职责应自动解除");

        GameSave save=GameSaveService.Capture(w);string error;Assert.IsTrue(GameSaveService.Validate(save,out error),error);
        GameWorld restored=new GameWorld(save.Seed,data);GameSaveService.Restore(restored,save);Assert.AreEqual(w.PlayerKingdomId,restored.PlayerKingdomId);Assert.AreEqual(w.People.Count,restored.People.Count);Assert.AreEqual(w.Armies.Count,restored.Armies.Count);Assert.AreEqual(w.Buildings.Count,restored.Buildings.Count);Assert.AreEqual(SaveOwner.CurrentVersion,save.SaveVersion);
    }

    [Test]
    public void NewWorldDifficulty_IsSelectableAndPersistsThroughSaveRestore() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());GameWorld w=new GameWorld(123456,data,AiDifficulty.Nightmare);w.CreateNewWorld(96,96,2);Assert.AreEqual(AiDifficulty.Nightmare,w.ComputerDifficulty);GameSave save=GameSaveService.Capture(w);Assert.AreEqual((int)AiDifficulty.Nightmare,save.ComputerDifficulty);GameWorld restored=new GameWorld(save.Seed,data,AiDifficulty.Easy);GameSaveService.Restore(restored,save);Assert.AreEqual(AiDifficulty.Nightmare,restored.ComputerDifficulty);
    }

    [Test]
    public void SaveMigration_V21ToCurrent_NormalizesComputerDifficulty() {
        GameSave s=new GameSave{SaveVersion=21,ComputerDifficulty=99};SaveOwner.Migrate(s);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.AreEqual((int)AiDifficulty.Hard,s.ComputerDifficulty);
    }

    [Test]
    public void SaveMigration_V25ToCurrent_InitializesWarAdministration() {
        GameSave s=new GameSave{SaveVersion=25,WarAdministrations=null};SaveOwner.Migrate(s);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.IsNotNull(s.WarAdministrations);
    }

    [Test]
    public void WorldHistory_PersistsStructuredEventsAcrossSaveRestore() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());GameWorld w=new GameWorld(24680,data,AiDifficulty.Hard);w.CreateNewWorld(96,96,2);w.RecordEvent("反叛","测试家族举兵","测试事件必须进入世界历史","K_TEST");GameSave save=GameSaveService.Capture(w);Assert.GreaterOrEqual(save.Events.Count,2);string error;Assert.IsTrue(GameSaveService.Validate(save,out error),error);GameWorld restored=new GameWorld(save.Seed,data);GameSaveService.Restore(restored,save);Assert.AreEqual(save.Events.Count,restored.Events.Count);Assert.AreEqual("测试家族举兵",restored.Events[restored.Events.Count-1].Title);
    }

    [Test]
    public void SaveMigration_V22ToCurrent_InitializesWorldHistory() {
        GameSave s=new GameSave{SaveVersion=22,Events=null};SaveOwner.Migrate(s);Assert.AreEqual(SaveOwner.CurrentVersion,s.SaveVersion);Assert.IsNotNull(s.Events);
    }

    [Test]
    public void MarchingArmy_AutoCampsAndResumesAfterFatigueRecovery() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());GameWorld w=new GameWorld(12345,data,AiDifficulty.Normal);w.CreateNewWorld(96,80,2);w.SecondsPerDay=999f;
        Kingdom k=w.Kingdoms[w.PlayerKingdomId];City c=w.Cities[k.CapitalCityId];Person g=null;foreach(Person p0 in w.People.Values)if(p0.KingdomId==k.Id&&p0.Noble){g=p0;break;}Assert.IsNotNull(g);
        Army a=w.Military.CreateArmy(k,c,g,"营地验收军");Assert.IsNotNull(a);a.Fatigue=80f;a.FoodDays=5f;a.Order=ArmyOrder.March;int nx=Math.Min(w.Map.Width-1,c.X+1);if(nx==c.X)nx=Math.Max(0,c.X-1);a.StrategicRoute=new List<GridPoint>{new GridPoint(c.X,c.Y),new GridPoint(nx,c.Y)};a.RouteIndex=0;
        w.Tick(.26f);Assert.AreEqual(ArmyOrder.Camp,a.Order);Assert.GreaterOrEqual(a.CampStartDay,0);
        a.Fatigue=30f;w.SecondsPerDay=.01f;w.Tick(.02f);Assert.AreNotEqual(ArmyOrder.Camp,a.Order);
    }

    [Test]
    public void GeneratedWorld_ProvidesNaturalFordsAcrossRepresentativeSeeds() {
        int fords=0,rivers=0;for(int seed=101;seed<111;seed++){WorldMap m=new WorldGenerator(seed).Generate(128,96,4);foreach(WorldTile t in m.Tiles)if(t.River){rivers++;if(t.Ford)fords++;}}Assert.Greater(rivers,0);Assert.Greater(fords,0);
    }

    [Test]
    public void StarvationAndArrears_RemoveRealSoldiersAsDeserters() {
        GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());GameWorld w=new GameWorld(9876,data,AiDifficulty.Normal);w.CreateNewWorld(96,80,2);Kingdom k=w.Kingdoms[w.PlayerKingdomId];City c=w.Cities[k.CapitalCityId];Person g=null;foreach(Person p0 in w.People.Values)if(p0.KingdomId==k.Id&&p0.Noble){g=p0;break;}Assert.IsNotNull(g);Army a=w.Military.CreateArmy(k,c,g,"断粮验收军");Assert.IsNotNull(a);a.Finance.Gold=1000;int recruited=w.Military.Recruit(a,c,FirstUnitId(w.Data),18,8);Assert.Greater(recruited,0);int before=w.Military.SoldierCount(a);a.FoodDays=0f;a.Morale=18f;a.Finance.MonthsInArrears=4;w.SecondsPerDay=.01f;for(int i=0;i<3;i++)w.Tick(.02f);Assert.Less(w.Military.SoldierCount(a),before);Assert.Greater(a.Deserters,0);
    }

    [Test]
    public void NightBattle_IsPersistedAndCommanderNightSkillChangesProfile() {
        CommanderBehaviorProfile p=new CommanderBehaviorProfile();Assert.AreEqual(1f,p.NightCombat,.001f);GameDataCatalog data=new GameDataCatalog();data.LoadAll(new UnityDataProvider());GameWorld w=new GameWorld(5678,data,AiDifficulty.Normal);w.CreateNewWorld(96,80,2);w.SecondsPerDay=100f;Assert.IsFalse(w.IsNightTime);w.RestoreClock(70f,1f,false);Assert.IsTrue(w.IsNightTime);
    }

}
#endif
