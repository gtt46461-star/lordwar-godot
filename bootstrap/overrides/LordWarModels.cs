using System;
using System.Collections.Generic;

namespace LordWar {
    public enum SocialClass { Commoner, Merchant, Noble, Official, Officer, General, Lord }
    public enum JobKind { Unemployed, Farmer, Miner, Logger, Smith, Builder, Porter, Trader, Soldier, Officer, Official }
    public enum ProposalState { Pending, Approved, Rejected, Running, Completed, Failed, Suspended }
    public enum ProposalKind { Appointment, Policy, Construction, MilitaryReform, MerchantInvestment, Diplomacy, Promotion }
    public enum ResourceKind { Food, Wood, Stone, Iron, Horses, Medicine }
    public enum BuildingKind { House, Apartment, Villa, Manor, Shop, BunShop, Market, Granary, Warehouse, Farm, Lumberyard, Mill, Bakery, Barracks, Stable, Workshop, Smithy, Armory, TrainingGround, Road, Bridge, Wall, Gate, Tower, Government }
    public enum InjuryState { None, Light, Heavy, Incapacitated, Dead, Captured }
    public enum MoraleState { Exalted, Steady, Shaken, Breaking, Routed }
    public enum WeatherKind { Clear, Rain, Storm, Snow, Blizzard, Fog, Heat, Cold }
    public enum SeasonKind { Spring, Summer, Autumn, Winter }
    public enum DiplomacyState { Neutral, Trade, NonAggression, Alliance, War, Vassal }
    public enum KingdomStatus { Active, Vassal, Eliminated }
    public enum OccupationPolicy { None, Pending, Conciliate, LimitedPlunder, FullPlunder }
    public enum ArmyOrder { Hold, Muster, March, Engage, Siege, Retreat, PursueLimited, PursueFull, Camp }
    public enum PursuitMode { Stop, Limited, Full }
    public enum CombatAttackMode { Light, Heavy, Thrust, Ranged, CavalryCharge }
    public enum CombatDefenseReaction { None, Dodge, Parry, ShieldBlock }
    public enum TerrainKind { DeepWater, Lake, Coast, Grass, Forest, Hill, Mountain, MountainPass, Snow, Desert, Marsh, River, Road }
    public enum ConstructionStage { Survey, Foundation, Frame, MainWork, Inspection, Complete }
    public enum ZoneKind { Residential, Commercial, Workshop, Agriculture, Military, Civic }
    public enum WarAdministrationFocus { Balanced, Defense, FieldBattle, Siege, Interception }

    [Serializable] public sealed class Stats {
        public int Life = 100, MaxLife = 100, Martial = 35, Defense = 30, Intelligence = 30, Stamina = 100;
        public int Administration = 20, Organization = 20, Logistics = 20, Engineering = 20, Military = 20;
        public int Loyalty = 60, Ambition = 30, Prestige = 0, Command = 10;
        public float Hunger, Fatigue;
    }

    [Serializable] public sealed class Person {
        public string Id, Name, KingdomId, CityId, FamilyId;
        public int Age;
        public bool IsWorldWalker, WalkForward = true;
        public int X, Y, WalkRouteIndex;
        public float WalkProgress;
        public List<GridPoint> WalkRoute = new List<GridPoint>();
        public SocialClass Class = SocialClass.Commoner;
        public JobKind Job = JobKind.Unemployed;
        public Stats Stats = new Stats();
        public bool Alive = true, Noble, Merchant, HasCommandPotential;
        public int Wealth, Experience, Kills, Battles, Promotions;
        public int CivicMerit;
        public string LastMeritReason;
        public int InjuryDays;
        public InjuryState Injury = InjuryState.None;
        public string CapturedByKingdomId;
        public int CapturedDay = -1;
        public int RansomValue;
        public List<string> TraitIds = new List<string>();
        public List<string> CommanderSkillIds = new List<string>();
        public List<string> EquipmentIds = new List<string>();
    }

    [Serializable] public sealed class Family {
        public string Id, Name, CrestKey, ColorHex, HomeCityId, PrimaryIndustry, MilitaryTradition;
        public int Wealth, Prestige, Loyalty = 60, Ambition = 30, Influence = 10;
        public string FamilyType;
        public List<string> MemberIds = new List<string>();
        public List<string> AllyFamilyIds = new List<string>();
        public List<string> RivalFamilyIds = new List<string>();
    }

    [Serializable] public sealed class NumericEffect {
        public string Key;
        public float Value;
        public NumericEffect() {}
        public NumericEffect(string key, float value) { Key = key; Value = value; }
    }

    [Serializable] public sealed class Proposal {
        public string Id, Title, ProposerId, Description, TargetId, DataId;
        public ProposalKind Kind;
        public int RequiredAbility, CostGold, DurationDays;
        public ProposalState State = ProposalState.Pending;
        public int SubmittedDay, StartedDay;
        // JsonUtility cannot persist Dictionary<TKey,TValue>. Keep effects as an explicit list.
        public List<NumericEffect> Effects = new List<NumericEffect>();
        public void SetEffect(string key, float value) {
            for (int i = 0; i < Effects.Count; i++) if (Effects[i].Key == key) { Effects[i].Value = value; return; }
            Effects.Add(new NumericEffect(key, value));
        }
        public bool HasEffect(string key) { for (int i = 0; i < Effects.Count; i++) if (Effects[i].Key == key) return true; return false; }
        public float GetEffect(string key, float fallback = 0f) { for (int i = 0; i < Effects.Count; i++) if (Effects[i].Key == key) return Effects[i].Value; return fallback; }
    }

    [Serializable] public sealed class MerchantInvestment {
        public string Id, MerchantId, GeneralId;
        public int Principal, MonthlySupport, StartDay;
        public float TaxReductionPct;
        public bool Active;
    }

    [Serializable] public sealed class MilitaryAccount {
        public string GeneralId;
        public int Gold, DebtToLord, DebtToMerchants;
        public int MonthlyWages, MonthlyFoodCost, ExpectedRepairCost, HorseCost, MedicalCost, SupplyCost;
        public int MonthsInArrears;
    }

    [Serializable] public sealed class EquipmentInstance {
        public string InstanceId, TemplateId, DisplayName, OwnerId, OriginalOwnerId, MakerId, OriginCityId, Slot, QualityName, Material, Source;
        public int Quality, Durability, MaxDurability, Attack, Defense, Weight, BaseValue;
        public float WeightKg;
        public bool Unique;
        public List<string> History = new List<string>();
    }

    [Serializable] public sealed class Building {
        public string Id, CityId, Name;
        public BuildingKind Kind;
        public int X, Y, Level = 1, Durability = 100, MaxDurability = 100, WorkerSlots;
        public bool Complete = true, Ruined;
    }

    [Serializable] public sealed class ConstructionWorkerAction {
        public string WorkerId, Action;
        public int Cycle;
    }

    [Serializable] public sealed class ConstructionProject {
        public string Id, CityId, ProposalId, Name;
        public BuildingKind Kind;
        public int X, Y, RequiredWork, WorkDone, GoldCommitted;
        public string TargetBuildingId; public bool IsRepair;
        public ConstructionStage Stage = ConstructionStage.Survey;
        public List<string> WorkerIds = new List<string>();
        public List<ConstructionWorkerAction> WorkerActions = new List<ConstructionWorkerAction>();
        public string CurrentAction = "测量";
    }

    [Serializable] public sealed class CityZone {
        public string Id, CityId, Name;
        public ZoneKind Kind;
        public int CenterX, CenterY, Radius = 3, PlannedDay, BuildingCount;
        public bool PlayerPlanned;
    }

    [Serializable] public sealed class City {
        public string Id, Name, KingdomId, CultureId;
        public int X, Y, PopulationCapacity = 100;
        public int Food, Wood, Stone, Iron, Horses;
        public int MarketJobs, FarmJobs, MineJobs, LoggingJobs, WorkshopJobs, BuildJobs;
        public int FortificationRadius, FortificationPlanVersion, LastFortificationReplanDay;
        public string PreviousKingdomId;
        public int LastCapturedDay = -1;
        public OccupationPolicy Occupation = OccupationPolicy.None;
        public List<string> PersonIds = new List<string>();
        public List<string> BuildingIds = new List<string>();
        public List<string> FamilyIds = new List<string>();
        public List<string> GarrisonArmyIds = new List<string>();
        public List<CityZone> Zones = new List<CityZone>();
    }

    [Serializable] public sealed class Kingdom {
        public string Id, Name, LordId, CapitalCityId, ColorHex;
        public KingdomStatus Status = KingdomStatus.Active;
        public string OverlordKingdomId;
        public int DefeatDay = -1;
        public int Treasury = 1800, Prestige = 50;
        public List<string> CityIds = new List<string>();
        public List<string> ArmyIds = new List<string>();
        public List<string> ActivePolicyIds = new List<string>();
        public List<string> SuspendedPolicyIds = new List<string>();
    }

    [Serializable] public sealed class Squad {
        public string Id, ArmyId, OfficerId, UnitTemplateId, Name, FormationName;
        public List<string> SoldierIds = new List<string>();
        public float Morale = 100f, Fatigue, Cohesion = 100f;
        public MoraleState LocalMoraleState = MoraleState.Steady;
        public int RoutWave;
        public int SlotX, SlotY, TrainingRemainingDays;
        public float TrainingProgress;
        public bool Routed;
    }

    [Serializable] public sealed class Army {
        public string Id, Name, KingdomId, GeneralId, ParentArmyId;
        public ArmyOrder Order = ArmyOrder.Hold;
        public PursuitMode PursuitMode = PursuitMode.Limited;
        public int X, Y, TargetX, TargetY;
        public float FoodDays = 8f, Morale = 100f, Fatigue;
        public int MedicalSupplies;
        public MilitaryAccount Finance = new MilitaryAccount();
        public List<string> SubGeneralIds = new List<string>();
        public List<Squad> Squads = new List<Squad>();
        public List<GridPoint> StrategicRoute = new List<GridPoint>();
        public int RouteIndex;
        public float MusterProgress;
        public int MusterStartDay;
        public ArmyOrder ResumeOrder = ArmyOrder.Hold;
        public int CampStartDay = -1;
        public int FormedDay, BattleCount, Wins, Losses, Deserters;
        public int LastBattleDay = -1;
        public List<string> FormerGeneralIds = new List<string>();
        public List<string> FamousBattles = new List<string>();
    }

    [Serializable] public sealed class SupplyRoute {
        public string Id, KingdomId, SourceCityId, ArmyId;
        public bool Active = true, Intact = true;
        public int LastRepathDay, Capacity = 2;
        public float Risk;
        public List<GridPoint> Path = new List<GridPoint>();
    }

    [Serializable] public sealed class SupplyConvoy {
        public string Id, RouteId, SourceCityId, ArmyId;
        public int PathIndex, FoodCargo, MedicineCargo, Escorts, DepartDay, DelayedDays;
        public bool Active = true;
    }

    [Serializable] public struct GridPoint {
        public int X, Y;
        public GridPoint(int x, int y) { X = x; Y = y; }
        public override string ToString() { return X + "," + Y; }
    }

    [Serializable] public sealed class BattleReport {
        public string Id, AttackerArmyId, DefenderArmyId, LocationName, ResultText, LastActionText;
        public string LastActorId, LastTargetId, LastAttackAction, LastDefenseAction;
        public int LastDamage, ActionSequence;
        public int StartDay, DurationSeconds, AttackerDead, DefenderDead, AttackerWounded, DefenderWounded, AttackerCaptured, DefenderCaptured, Captured, LootGold;
        public int LightAttacks, HeavyAttacks, ThrustAttacks, RangedAttacks, CavalryCharges;
        public int Dodges, Parries, ShieldBlocks;
        public int AttackerRoutedSquads, DefenderRoutedSquads, MoraleCollapseWaves;
        public float AttackerFinalMorale, DefenderFinalMorale;
        public bool AttackerOrderlyRetreat, DefenderOrderlyRetreat;
        public List<string> Highlights = new List<string>();
    }

    [Serializable] public sealed class BattleSession {
        public string Id, AttackerArmyId, DefenderArmyId, LocationName, SiegeCityId;
        public int StartDay, ElapsedSeconds, MaxSeconds = 240, AttackerIndex, DefenderIndex, AttackerFlankDelay, DefenderFlankDelay;
        public uint RandomState;
        public bool AttackerFlank, DefenderFlank, AttackerWithdraw, DefenderWithdraw, AttackerLastStand, DefenderLastStand, Night, Completed;
        public float RangedAccuracy = 1f;
        public TerrainKind Terrain = TerrainKind.Grass;
        public WeatherKind Weather = WeatherKind.Clear;
        public BattleReport Report = new BattleReport();
    }

    [Serializable] public sealed class AnimalHerd {
        public string Id, Species, HomeCityId;
        public int X, Y, Count;
        public bool Domestic;
    }


    [Serializable] public sealed class WarAdministration {
        public string Id, KingdomId, EnemyKingdomId, OfficialId;
        public WarAdministrationFocus Focus = WarAdministrationFocus.Balanced;
        public int AssignedDay, LastDecisionDay = -1, OrdersIssued, DefenseOrders, FieldOrders, SiegeOrders, InterceptOrders;
        public bool DefenseEnabled = true, FieldBattleEnabled = true, SiegeEnabled = true, InterceptionEnabled = true;
        public string LastAction = "尚未下达战务";
    }

    [Serializable] public sealed class WorldEvent {
        public string Id, Category, Title, Detail, RelatedId;
        public int Day;
    }

    [Serializable] public sealed class DiplomacyRelation {
        public string A, B;
        public DiplomacyState State = DiplomacyState.Neutral;
        public string SuzerainId, VassalId;
        public int Opinion, TruceUntilDay;
        public bool CommonWar;
    }
}
