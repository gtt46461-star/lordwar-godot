#if UNITY_5_3_OR_NEWER
using System; using UnityEngine; using LordWar.Data; using LordWar.Simulation; using LordWar.AI; using LordWar.World;
namespace LordWar.UnityRuntime {
    public sealed class LordWarBootstrap : MonoBehaviour {
        public static GameWorld World; public int seed=0; public int width=160,height=160,kingdoms=2; public AiDifficulty computerDifficulty=AiDifficulty.Hard; static GameDataCatalog _data; static LordWarBootstrap _instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Ensure(){if(Object.FindObjectOfType<LordWarBootstrap>()!=null)return;var go=new GameObject("领主战争_运行核心");DontDestroyOnLoad(go);go.AddComponent<LordWarBootstrap>();go.AddComponent<ChineseHud>();go.AddComponent<WorldCameraController>();}
        void Awake(){_instance=this;Application.targetFrameRate=60;CreateFreshWorld(seed==0?NewSeed():seed,width,height,kingdoms,computerDifficulty);}
        static int NewSeed(){unchecked{return (int)(DateTime.UtcNow.Ticks^(long)Environment.TickCount);}}
        public static void CreateFreshWorld(){if(_instance!=null)_instance.CreateFreshWorld(NewSeed(),_instance.width,_instance.height,_instance.kingdoms,_instance.computerDifficulty);}
        public static void CreateConfiguredWorld(int requestedSeed,int requestedWidth,int requestedHeight,int requestedKingdoms,AiDifficulty difficulty){CreateConfiguredWorld(requestedSeed,requestedWidth,requestedHeight,requestedKingdoms,difficulty,new WorldGenerationOptions());}
        public static void CreateConfiguredWorld(int requestedSeed,int requestedWidth,int requestedHeight,int requestedKingdoms,AiDifficulty difficulty,WorldGenerationOptions options){if(_instance==null)return;_instance.CreateFreshWorld(requestedSeed,requestedWidth,requestedHeight,requestedKingdoms,difficulty,options);}
        void CreateFreshWorld(int actualSeed,int requestedWidth,int requestedHeight,int requestedKingdoms,AiDifficulty difficulty){CreateFreshWorld(actualSeed,requestedWidth,requestedHeight,requestedKingdoms,difficulty,new WorldGenerationOptions());}
        void CreateFreshWorld(int actualSeed,int requestedWidth,int requestedHeight,int requestedKingdoms,AiDifficulty difficulty,WorldGenerationOptions options){width=Math.Max(72,Math.Min(320,requestedWidth));height=Math.Max(72,Math.Min(320,requestedHeight));kingdoms=Math.Max(2,Math.Min(6,requestedKingdoms));computerDifficulty=difficulty;if(_data==null){_data=new GameDataCatalog();_data.LoadAll(new UnityDataProvider());}GameWorld next=new GameWorld(actualSeed,_data,computerDifficulty);next.CreateNewWorld(width,height,kingdoms,options);WorldRenderer old=GetComponent<WorldRenderer>();if(old!=null)Destroy(old);World=next;var r=gameObject.AddComponent<WorldRenderer>();r.Bind(World);}
        void Update(){if(World!=null)World.Tick(Time.unscaledDeltaTime);}
    }
}
#endif
