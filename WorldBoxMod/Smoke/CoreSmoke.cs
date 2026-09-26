using System;
using System.IO;
using LordWar.AI;
using LordWar.Data;
using LordWar.Save;
using LordWar.Simulation;

var folder = args.Length > 0 ? args[0] : Path.GetFullPath("../LordWarMod");
var data = new GameDataCatalog();
data.LoadAll(new FileData(folder));
var world = new GameWorld(20260926, data, AiDifficulty.Hard);
world.CreateNewWorld(160, 120, 4);
if (world.Map.Width != 160 || world.Map.Height != 120 || world.Kingdoms.Count != 4)
    throw new Exception("world initialization invariant failed");
if (world.People.Count == 0 || world.Cities.Count != 4)
    throw new Exception("population/city initialization failed");
world.AdvanceDay();
if (world.Day != 1) throw new Exception("day advancement failed");
var save = GameSaveService.Capture(world);
if (!GameSaveService.Validate(save, out var reason)) throw new Exception("save validation: " + reason);
var restored = new GameWorld(save.Seed, data, AiDifficulty.Hard);
GameSaveService.Restore(restored, save);
if (restored.Day != world.Day || restored.Cities.Count != world.Cities.Count)
    throw new Exception("restore mismatch");
Console.WriteLine($"CORE_SMOKE_PASS skills={data.Skills.Count} units={data.Units.Count} " +
                  $"map={world.Map.Width}x{world.Map.Height} kingdoms={world.Kingdoms.Count} " +
                  $"cities={world.Cities.Count} people={world.People.Count} day={restored.Day}");

sealed class FileData : ITextDataProvider
{
    private readonly string _dir;
    public FileData(string dir) { _dir = Path.Combine(dir, "Data"); }
    public string Load(string key) => File.ReadAllText(Path.Combine(_dir, key + ".csv"));
}
