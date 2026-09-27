#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LordWar.UnityRuntime;
namespace LordWar.EditorBuild {
 public static class ProjectSetup {
  public const string MainScene="Assets/Scenes/LordWarMain.unity";
  public static string EnsureMainScene(){
   Directory.CreateDirectory("Assets/Scenes");
   Scene s=File.Exists(MainScene)?EditorSceneManager.OpenScene(MainScene,OpenSceneMode.Single):EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   GameObject core=GameObject.Find("领主战争_运行核心");if(core==null)core=new GameObject("领主战争_运行核心");
   if(core.GetComponent<LordWarBootstrap>()==null)core.AddComponent<LordWarBootstrap>();
   if(core.GetComponent<ChineseHud>()==null)core.AddComponent<ChineseHud>();
   if(core.GetComponent<WorldCameraController>()==null)core.AddComponent<WorldCameraController>();
   EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s,MainScene);
   EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(MainScene,true)};
   AssetDatabase.SaveAssets();return MainScene;
  }
  [MenuItem("领主战争/初始化工程")]
  public static void Setup(){EnsureMainScene();PlayerSettings.productName=BuildInfo.ProductName;PlayerSettings.applicationIdentifier=BuildInfo.PackageName;PlayerSettings.bundleVersion=BuildInfo.VersionName;PlayerSettings.Android.bundleVersionCode=BuildInfo.VersionCode;AssetDatabase.SaveAssets();Debug.Log("领主战争工程初始化完成："+BuildInfo.VersionName);}
 }
}
#endif
