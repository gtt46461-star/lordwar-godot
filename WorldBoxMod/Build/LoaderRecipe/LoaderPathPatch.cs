using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static class Program {
 public static int Main(string[] argv) {
  if(argv.Length!=3) return 2;
  var resolver=new DefaultAssemblyResolver();
  resolver.AddSearchDirectory(Environment.GetEnvironmentVariable("PATCH_ASSEMBLIES")!);
  resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(argv[0])!);
  var reader=new ReaderParameters { AssemblyResolver=resolver };
  var ml=AssemblyDefinition.ReadAssembly(argv[0],reader);
  var env=ml.MainModule.Types.Single(t=>t.FullName=="MelonLoader.Utils.MelonEnvironment");
  var mods=env.Methods.Single(m=>m.Name=="get_ModsDirectory");
  var root=env.Methods.Single(m=>m.Name=="get_MelonLoaderDirectory");
  var ins=mods.Body.Instructions.First(i=>i.OpCode==OpCodes.Call);
  if(((MethodReference)ins.Operand).Name!="get_MelonBaseDirectory") throw new Exception("Unexpected MelonLoader mods path body");
  ins.Operand=root;
  ml.Write(argv[1]);
  var nml=AssemblyDefinition.ReadAssembly(argv[2],reader);
  var paths=nml.MainModule.Types.Single(t=>t.FullName=="NeoModLoader.constants.Paths");
  var cctor=paths.Methods.Single(m=>m.IsConstructor&&m.IsStatic);
  var found=cctor.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr&&(string)i.Operand=="NMLMods").ToArray(); Console.WriteLine("found="+found.Length);
  if(found.Length!=1) throw new Exception("Unexpected NMLMods path count: "+found.Length);
  found[0].Operand="MelonLoader/NMLMods";
  nml.Write(argv[2]+".patched");
  Console.WriteLine("Patched loader paths: "+ins.Operand+"; NMLMods=MelonLoader/NMLMods");
  return 0;
 }
}
