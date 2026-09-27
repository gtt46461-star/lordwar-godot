#if UNITY_5_3_OR_NEWER
using UnityEngine;
using LordWar.Data;
namespace LordWar.UnityRuntime {
    public sealed class UnityDataProvider : ITextDataProvider {
        public string Load(string key){TextAsset a=Resources.Load<TextAsset>("LordWarData/"+key);if(a==null)throw new System.InvalidOperationException("缺少领主战争数据: "+key);return a.text;}
    }
}
#endif
