#if UNITY_5_3_OR_NEWER
using System;
using System.IO;
using UnityEngine;
using LordWar.Save;
using LordWar.Simulation;
namespace LordWar.UnityRuntime {
 public static class UnitySaveService {
  public static string SavePath{get{return Path.Combine(Application.persistentDataPath,"lordwar_save.json");}}
  static string BackupPath{get{return SavePath+".bak";}}
  static string PreviousBackupPath{get{return SavePath+".bak.previous";}}
  static string TempPath{get{return SavePath+".tmp";}}
  static string LegacyPath{get{return Path.Combine(Application.persistentDataPath,"lordwar_save_v7.json");}}
  public static bool Save(GameWorld w){
   if(w==null)return false;
   try{
    GameSave data=GameSaveService.Capture(w);string error;
    if(!GameSaveService.Validate(data,out error)){Debug.LogError("存档前校验失败："+error);return false;}
    string json=JsonUtility.ToJson(data,true);
    using(var stream=new FileStream(TempPath,FileMode.Create,FileAccess.Write,FileShare.None)){
     using(var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false),4096,true)){
      writer.Write(json);writer.Flush();stream.Flush(true);
     }
    }
    GameSave verify=JsonUtility.FromJson<GameSave>(File.ReadAllText(TempPath,System.Text.Encoding.UTF8));
    if(!GameSaveService.Validate(verify,out error)){Debug.LogError("存档回读校验失败："+error);return false;}
    if(File.Exists(SavePath)){
     try{File.Replace(TempPath,SavePath,BackupPath,true);}
     catch(PlatformNotSupportedException){MoveWithBackup();}
     catch(IOException){MoveWithBackup();}
    }else File.Move(TempPath,SavePath);
    return File.Exists(SavePath);
   }catch(Exception e){Debug.LogError("保存失败："+e);return false;}
  }
  static void MoveWithBackup(){
   if(File.Exists(BackupPath))File.Copy(BackupPath,PreviousBackupPath,true);
   if(File.Exists(BackupPath))File.Delete(BackupPath);
   File.Move(SavePath,BackupPath);
   File.Move(TempPath,SavePath);
  }
  public static bool TryRead(out GameSave data){data=null;string[] candidates={SavePath,BackupPath,PreviousBackupPath,LegacyPath};for(int i=0;i<candidates.Length;i++){string path=candidates[i];if(!File.Exists(path))continue;try{string json=File.ReadAllText(path,System.Text.Encoding.UTF8);GameSave candidate=JsonUtility.FromJson<GameSave>(json);string error;if(!GameSaveService.Validate(candidate,out error)){Debug.LogWarning("存档不可用："+path+" / "+error);continue;}data=candidate;return true;}catch(Exception e){Debug.LogWarning("读取存档失败："+path+" / "+e.Message);}}return false;}
  public static bool HasSave {get{return File.Exists(SavePath)||File.Exists(BackupPath)||File.Exists(PreviousBackupPath)||File.Exists(LegacyPath);}}
 }
}
#endif
