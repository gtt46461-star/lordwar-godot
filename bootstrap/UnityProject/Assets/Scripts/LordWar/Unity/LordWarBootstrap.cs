#if UNITY_5_3_OR_NEWER
using System; using UnityEngine; using LordWar.Data; using LordWar.Simulation; using LordWar.AI; using LordWar.World;
namespace LordWar.UnityRuntime {
    public sealed class LordWarBootstrap : MonoBehaviour {
        public static GameWorld World; public int seed=0; public int width=160,height=160,kingdoms=2; public AiDifficulty computerDifficulty=AiDifficulty.Hard; static GameDataCatalog _data; static LordWarBootstrap _instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Ensure(){if(Object.FindObjectOfType<LordWarBootstrap>()!=null)return;var go=new GameObject("领主战争_运行核心");DontDestroyOnLoad(go);go.AddComponent<LordWarBootstrap>();go.AddComponent<ChineseHud>();go.AddComponent<WorldCameraController>();}
        void Awake(){_instance=this;Application.targetFrameRate=60;World=null;}
        static int NewSeed(){unchecked{return (int)(DateTime.UtcNow.Ticks^(long)Environment.TickCount);}}
        public static void CreateFreshWorld(){if(_instance!=null)_instance.CreateFreshWorld(NewSeed(),_instance.width,_instance.height,_instance.kingdoms,_instance.computerDifficulty);}
        public static void CreateConfiguredWorld(int requestedSeed,int requestedWidth,int requestedHeight,int requestedKingdoms,AiDifficulty difficulty){CreateConfiguredWorld(requestedSeed,requestedWidth,requestedHeight,requestedKingdoms,difficulty,new WorldGenerationOptions());}
        public static void CreateConfiguredWorld(int requestedSeed,int requestedWidth,int requestedHeight,int requestedKingdoms,AiDifficulty difficulty,WorldGenerationOptions options){if(_instance==null)return;_instance.CreateFreshWorld(requestedSeed,requestedWidth,requestedHeight,requestedKingdoms,difficulty,options);}
        void CreateFreshWorld(int actualSeed,int requestedWidth,int requestedHeight,int requestedKingdoms,AiDifficulty difficulty){CreateFreshWorld(actualSeed,requestedWidth,requestedHeight,requestedKingdoms,difficulty,new WorldGenerationOptions());}
        static GameDataCatalog Data(){if(_data==null){_data=new GameDataCatalog();_data.LoadAll(new UnityDataProvider());}return _data;}
        void InstallWorld(GameWorld next){WorldRenderer old=GetComponent<WorldRenderer>();if(old!=null){old.enabled=false;Destroy(old);}World=next;var r=gameObject.AddComponent<WorldRenderer>();r.Bind(next);Kingdom player;City capital;if(!string.IsNullOrEmpty(next.PlayerKingdomId)&&next.Kingdoms.TryGetValue(next.PlayerKingdomId,out player)&&next.Cities.TryGetValue(player.CapitalCityId,out capital)){Camera cam=Camera.main;if(cam!=null)cam.orthographicSize=14f;WorldCameraController controller=GetComponent<WorldCameraController>();if(controller!=null)controller.FocusWorldPoint(capital.X,capital.Y);}}
        void CreateFreshWorld(int actualSeed,int requestedWidth,int requestedHeight,int requestedKingdoms,AiDifficulty difficulty,WorldGenerationOptions options){width=Math.Max(72,Math.Min(320,requestedWidth));height=Math.Max(72,Math.Min(320,requestedHeight));kingdoms=Math.Max(2,Math.Min(6,requestedKingdoms));computerDifficulty=difficulty;GameWorld next=new GameWorld(actualSeed,Data(),computerDifficulty);next.CreateNewWorld(width,height,kingdoms,options);InstallWorld(next);}
        public static bool LoadSavedWorld(){if(_instance==null)return false;LordWar.Save.GameSave save;if(!UnitySaveService.TryRead(out save))return false;try{GameWorld next=new GameWorld(save.Seed,Data());LordWar.Save.GameSaveService.Restore(next,save);_instance.InstallWorld(next);return true;}catch(Exception e){Debug.LogError("读档世界校验或绑定失败："+e);return false;}}
        void Update(){if(World!=null)World.Tick(Time.unscaledDeltaTime);}
    }
}
#endif
