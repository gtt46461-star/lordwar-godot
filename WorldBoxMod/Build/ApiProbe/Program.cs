using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

if (args.Length < 1 || args.Length > 2 || !File.Exists(args[0]) || (args.Length == 2 && !File.Exists(args[1])))
{
    Console.Error.WriteLine("usage: ApiProbe <Android Assembly-CSharp.dll> [NeoModLoader_mobile.dll]");
    return 2;
}

var assembly = AssemblyDefinition.ReadAssembly(args[0]);
var names = new HashSet<string>(StringComparer.Ordinal)
{
    "City", "CityData", "CityStorage", "Actor", "ActorData", "Kingdom", "KingdomData",
    "Army", "ArmyData", "Building", "WorldTile", "WorldZone", "MapBox",
    "ResourceAsset", "ResourceStorage", "Storage", "WorldActor",
    "SaveManager", "SavedMap", "AutoSaveManager", "SaveSlotManager", "BuildingData",
    "LoadWorldButton", "SaveWorldButton", "MapUploader", "BaseSimObject",
    "BaseObjectData", "BaseSystemData", "CoreSystemObject`1", "NanoObject", "MetaObjectData", "MetaObject`1", "MetaObjectWithTraits`2"
};
var saveTypes = new HashSet<string>(StringComparer.Ordinal)
{
    "SaveManager", "SavedMap", "AutoSaveManager", "SaveSlotManager",
    "LoadWorldButton", "SaveWorldButton", "MapUploader", "ActorData",
    "CityData", "KingdomData", "ArmyData", "BuildingData", "BaseSimObject",
    "BaseObjectData", "BaseSystemData", "CoreSystemObject`1", "NanoObject", "MetaObjectData", "MetaObject`1", "MetaObjectWithTraits`2"
};

foreach (var type in assembly.MainModule.Types.Where(type => names.Contains(type.Name)).OrderBy(type => type.Name))
{
    Console.WriteLine("TYPE " + type.FullName);
    Console.WriteLine("  BASE " + (type.BaseType == null ? "<none>" : type.BaseType.FullName));
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

if (args.Length == 2)
{
    var loader = AssemblyDefinition.ReadAssembly(args[1]);
    foreach (var reference in loader.MainModule.AssemblyReferences)
        Console.WriteLine("LOADER_REFERENCE " + reference.Name);

    var allTypes = loader.MainModule.Types.Where(type => type.Namespace.StartsWith("NeoModLoader", StringComparison.Ordinal)).ToArray();
    var mainTypes = allTypes.Where(type => type.Name.Contains("BasicMod", StringComparison.Ordinal)
            || type.Name.Contains("ModDeclare", StringComparison.Ordinal)
            || type.Name.Contains("ModSettings", StringComparison.Ordinal)
            || type.Name.Contains("ModStorage", StringComparison.Ordinal)
            || type.Name.Contains("WrappedBehaviour", StringComparison.Ordinal)
            || type.Name.Contains("Il2CPPBehaviour", StringComparison.Ordinal)
            || type.Name.Contains("WrappedMethodHandler", StringComparison.Ordinal))
        .ToArray();
    foreach (var type in mainTypes.OrderBy(type => type.FullName))
    {
        Console.WriteLine("LOADER_TYPE " + type.FullName);
        Console.WriteLine("  BASE " + (type.BaseType == null ? "<none>" : type.BaseType.FullName));
        foreach (var field in type.Fields)
            Console.WriteLine("  FIELD " + field.FieldType.FullName + " " + field.Name);
        foreach (var property in type.Properties)
            Console.WriteLine("  PROPERTY " + property.PropertyType.FullName + " " + property.Name);
        foreach (var method in type.Methods.Where(method => !method.IsConstructor))
        {
            var parameters = string.Join(", ", method.Parameters.Select(p => p.ParameterType.FullName + " " + p.Name));
            var visibility = method.IsPublic ? "public" : method.IsFamily ? "protected" : method.IsAssembly ? "internal" : "private";
            var modifiers = (method.IsStatic ? " static" : "") + (method.IsVirtual ? " virtual" : "");
            Console.WriteLine("  METHOD " + visibility + modifiers + " " + method.ReturnType.FullName + " " + method.Name + "(" + parameters + ") body=" + method.HasBody);
        }
    }

    var lifecycleTypes = allTypes.Where(type => !mainTypes.Contains(type)
        && type.Methods.Any(method => method.Name.StartsWith("OnMod", StringComparison.Ordinal)
            || method.Name == "Update" || method.Name == "LateUpdate" || method.Name == "FixedUpdate"
            || method.Name == "OnApplicationQuit" || method.Name == "OnDestroy"
            || method.Name.StartsWith("Save", StringComparison.Ordinal)
            || method.Name.StartsWith("Load", StringComparison.Ordinal)))
        .OrderBy(type => type.FullName);
    foreach (var type in lifecycleTypes)
    {
        var methods = type.Methods.Where(method => method.Name.StartsWith("OnMod", StringComparison.Ordinal)
            || method.Name == "Update" || method.Name == "LateUpdate" || method.Name == "FixedUpdate"
            || method.Name == "OnApplicationQuit" || method.Name == "OnDestroy"
            || method.Name.StartsWith("Save", StringComparison.Ordinal)
            || method.Name.StartsWith("Load", StringComparison.Ordinal));
        foreach (var method in methods)
            Console.WriteLine("LOADER_LIFECYCLE " + type.FullName + " " + method.Name + " body=" + method.HasBody);
    }
}
return 0;
