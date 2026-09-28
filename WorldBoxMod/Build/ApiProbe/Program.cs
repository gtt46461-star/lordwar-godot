using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("usage: ApiProbe <Android Assembly-CSharp.dll>");
    return 2;
}

var assembly = AssemblyDefinition.ReadAssembly(args[0]);
var names = new HashSet<string>(StringComparer.Ordinal)
{
    "City", "CityData", "CityStorage", "Actor", "ActorData", "Kingdom", "KingdomData",
    "Army", "ArmyData", "Building", "WorldTile", "WorldZone", "MapBox",
    "ResourceAsset", "ResourceStorage", "Storage", "WorldActor",
    "SaveManager", "SavedMap", "AutoSaveManager", "SaveSlotManager",
    "LoadWorldButton", "SaveWorldButton", "MapUploader"
};
var saveTypes = new HashSet<string>(StringComparer.Ordinal)
{
    "SaveManager", "SavedMap", "AutoSaveManager", "SaveSlotManager",
    "LoadWorldButton", "SaveWorldButton", "MapUploader"
};

foreach (var type in assembly.MainModule.Types.Where(type => names.Contains(type.Name)).OrderBy(type => type.Name))
{
    Console.WriteLine("TYPE " + type.FullName);
    foreach (var field in type.Fields)
        Console.WriteLine("  FIELD " + field.FieldType.FullName + " " + field.Name);
    foreach (var property in type.Properties)
        Console.WriteLine("  PROPERTY " + property.PropertyType.FullName + " " + property.Name);
    foreach (var method in type.Methods.Where(method => !method.IsConstructor && (method.IsPublic || saveTypes.Contains(type.Name))))
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => p.ParameterType.FullName + " " + p.Name));
        var visibility = method.IsPublic ? "public" : method.IsFamily ? "protected" : method.IsAssembly ? "internal" : "private";
        var modifiers = (method.IsStatic ? " static" : "") + (method.IsVirtual ? " virtual" : "");
        Console.WriteLine("  METHOD " + visibility + modifiers + " " + method.ReturnType.FullName + " " + method.Name + "(" + parameters + ")");
    }
}
return 0;
