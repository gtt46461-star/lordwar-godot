#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace LordWar.EditorBuild {
 public static class AndroidBuild {
  public const string OutputDirectory="Builds/Android";
  [MenuItem("领主战争/构建Android APK")]
  public static void BuildApk(){BuildInternal(false);}
  // Unity batchmode: -executeMethod LordWar.EditorBuild.AndroidBuild.BuildFromCommandLine
  public static void BuildFromCommandLine(){BuildInternal(true);}
  static void BuildInternal(bool commandLine){
   ProjectSetup.EnsureMainScene();
   BuildValidation.Run();
   ConfigureAndroid();
   string[] scenes=EnabledScenes();if(scenes.Length==0)throw new Exception("构建前未找到已启用主场景");
   string run=Path.Combine(OutputDirectory,BuildInfo.BuildId+"-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
   Directory.CreateDirectory(run);
   string apk=Path.Combine(run,"LordWar-"+BuildInfo.Batch+"-"+BuildInfo.VersionCode+".apk");
   string started=DateTime.UtcNow.ToString("O");
   BuildReport report=null;
   try{
    if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android){if(!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android))throw new Exception("无法切换到Android构建目标");}
    BuildPlayerOptions opt=new BuildPlayerOptions{scenes=scenes,locationPathName=apk,target=BuildTarget.Android,options=BuildOptions.None};
    report=BuildPipeline.BuildPlayer(opt);WriteBuildEvidence(report,apk,started,null);
    if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Android构建失败: "+report.summary.result);
    Debug.Log("APK构建成功: "+apk+" SHA256="+Sha256(apk));
   }catch(Exception ex){WriteBuildEvidence(report,apk,started,ex);if(commandLine)Debug.LogError(ex);throw;}
  }
  public static void ConfigureAndroid(){
   PlayerSettings.productName=BuildInfo.ProductName;
   PlayerSettings.applicationIdentifier=BuildInfo.PackageName;
   PlayerSettings.bundleVersion=BuildInfo.VersionName;
   PlayerSettings.Android.bundleVersionCode=BuildInfo.VersionCode;
   PlayerSettings.Android.useCustomKeystore=true;
   PlayerSettings.Android.keystoreName=Environment.GetEnvironmentVariable("LORDWAR_KEYSTORE_PATH");
   PlayerSettings.Android.keystorePass=Environment.GetEnvironmentVariable("LORDWAR_KEYSTORE_PASS");
   PlayerSettings.Android.keyaliasName=Environment.GetEnvironmentVariable("LORDWAR_KEY_ALIAS");
   PlayerSettings.Android.keyaliasPass=Environment.GetEnvironmentVariable("LORDWAR_KEY_PASS");
   PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;
   PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
   PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
   PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
   PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);
   PlayerSettings.stripEngineCode=true;
   PlayerSettings.gcIncremental=true;
   EditorUserBuildSettings.buildAppBundle=false;
   AssetDatabase.SaveAssets();
  }
  static string[] EnabledScenes(){var list=new List<string>();foreach(var s in EditorBuildSettings.scenes)if(s.enabled&&File.Exists(s.path))list.Add(s.path);return list.ToArray();}
  static string Sha256(string path){using(SHA256 sha=SHA256.Create())using(FileStream fs=File.OpenRead(path)){byte[] h=sha.ComputeHash(fs);StringBuilder b=new StringBuilder(h.Length*2);for(int i=0;i<h.Length;i++)b.Append(h[i].ToString("x2"));return b.ToString();}}
  static string GitHead(){try{string git=Path.GetFullPath(".git/HEAD");if(!File.Exists(git))return "unknown";string h=File.ReadAllText(git).Trim();if(h.StartsWith("ref: ")){string rp=Path.GetFullPath(".git/"+h.Substring(5));if(File.Exists(rp))return File.ReadAllText(rp).Trim();}return h;}catch{return "unknown";}}
  static string ManifestSha256(params string[] directories){try{var files=new List<string>();foreach(string dir in directories)if(Directory.Exists(dir))files.AddRange(Directory.GetFiles(dir,"*",SearchOption.AllDirectories));files.Sort(StringComparer.Ordinal);using(SHA256 sha=SHA256.Create()){foreach(string f in files){byte[] p=Encoding.UTF8.GetBytes(f.Replace('\\','/')+"\n");sha.TransformBlock(p,0,p.Length,null,0);byte[] b=File.ReadAllBytes(f);sha.TransformBlock(b,0,b.Length,null,0);}sha.TransformFinalBlock(new byte[0],0,0);StringBuilder s=new StringBuilder();foreach(byte b in sha.Hash)s.Append(b.ToString("x2"));return s.ToString();}}catch{return "unknown";}}
  static void WriteBuildEvidence(BuildReport report,string apk,string started,Exception ex){
   string run=Path.GetDirectoryName(apk);Directory.CreateDirectory(run);string path=Path.Combine(run,"BUILD_INFO.txt");StringBuilder b=new StringBuilder();
   b.AppendLine("游戏="+BuildInfo.ProductName);b.AppendLine("batch="+BuildInfo.Batch);b.AppendLine("buildId="+BuildInfo.BuildId);b.AppendLine("versionName="+BuildInfo.VersionName);b.AppendLine("versionCode="+BuildInfo.VersionCode);b.AppendLine("package="+BuildInfo.PackageName);b.AppendLine("signingCertSha256="+Environment.GetEnvironmentVariable("LORDWAR_CERT_SHA256"));b.AppendLine("源码基线="+BuildInfo.SourceBaseline);b.AppendLine("源码Git="+GitHead());b.AppendLine("sourceTreeHash="+ManifestSha256("Assets","Packages","ProjectSettings","Tools"));b.AppendLine("contentHash="+ManifestSha256("Assets/Resources"));b.AppendLine("buildStartUtc="+started);b.AppendLine("buildEndUtc="+DateTime.UtcNow.ToString("O"));
   b.AppendLine("target=Android APK");b.AppendLine("architecture=ARM64");b.AppendLine("scriptingBackend=IL2CPP");b.AppendLine("minSdk=26");
   b.AppendLine("buildResult="+(report==null?"未开始":report.summary.result.ToString()));
   if(report!=null){b.AppendLine("totalErrors="+report.summary.totalErrors);b.AppendLine("totalWarnings="+report.summary.totalWarnings);b.AppendLine("totalTime="+report.summary.totalTime);}
   if(ex!=null)b.AppendLine("exception="+ex);
   if(File.Exists(apk)){FileInfo fi=new FileInfo(apk);b.AppendLine("apkPath="+apk);b.AppendLine("apkBytes="+fi.Length);b.AppendLine("apkSha256="+Sha256(apk));}
   File.WriteAllText(path,b.ToString(),Encoding.UTF8);
  }
 }
}
#endif
