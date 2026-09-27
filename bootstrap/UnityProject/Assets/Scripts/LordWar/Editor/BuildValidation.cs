#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LordWar.EditorBuild {
    public static class BuildValidation {
        // Called by AndroidBuild after ProjectSetup has opened and saved the scene.
        public static void Run(){
            if(!File.Exists(ProjectSetup.MainScene))throw new InvalidOperationException("主场景缺失");
            foreach(string id in new[]{"house","tree","walker_strip"}){
                string path="Assets/Resources/LordWarArt/N01/"+id+".png";
                if(AssetDatabase.LoadAssetAtPath<Texture2D>(path)==null)throw new InvalidOperationException("N01真实资源缺失："+path);
            }
            string data="Assets/Resources/LordWarData/policies_v8.csv";
            if(!File.Exists(data))throw new InvalidOperationException("运行数据缺失："+data);
            if(EditorBuildSettings.scenes.Length==0||!EditorBuildSettings.scenes[0].enabled)throw new InvalidOperationException("主场景未启用");
            string pathToKey=Environment.GetEnvironmentVariable("LORDWAR_KEYSTORE_PATH");
            if(string.IsNullOrEmpty(pathToKey)||!File.Exists(pathToKey))throw new InvalidOperationException("缺少既有签名密钥：LORDWAR_KEYSTORE_PATH");
            foreach(string key in new[]{"LORDWAR_KEYSTORE_PASS","LORDWAR_KEY_ALIAS","LORDWAR_KEY_PASS","LORDWAR_CERT_SHA256"})
                if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))throw new InvalidOperationException("缺少签名配置："+key);
        }
    }
}
#endif
