using System;
using MelonLoader.Installer.Core;
using AssetRipper.Primitives;
public sealed class ConsoleLogger : IPatchLogger { public void Log(string message) => Console.WriteLine(message); }
public static class Program {
  public static int Main(string[] argv) {
    if (argv.Length != 5) { Console.Error.WriteLine("input.apk melon_data.zip unitydeps.zip output-dir temp-dir"); return 2; }
    var args = new PatchArguments(argv[0], "", Array.Empty<string>(), argv[3], argv[4], argv[1], argv[2], UnityVersion.Parse("2022.3.60f1"), "com.mkarpenko.worldbox", false);
    return new Patcher(args, new ConsoleLogger()).Run() ? 0 : 1;
  }
}
