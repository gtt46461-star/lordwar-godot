#if UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using LordWar.Simulation;
using LordWar.World;
using LordWar.Data;
using LordWar.Performance;
namespace LordWar.UnityRuntime {
 public sealed class WorldRenderer : MonoBehaviour {
  GameWorld _world; SpriteAssetLibrary _art; GameObject _mapGo; Texture2D _mapTexture; Sprite _mapSprite; Color32[] _terrainPixels; float _nextDynamicSync,_nextStaticCheck,_nextTerrainSync; int _lastVisualDay=-1,_lastBuildingSignature=int.MinValue,_terrainCenterX=int.MinValue,_terrainCenterY=int.MinValue; float _terrainZoom=-1f; const int MaxVisiblePeople=480;
  readonly List<GameObject> _staticMarkers=new List<GameObject>();
  readonly List<GameObject> _terrainMarkers=new List<GameObject>();
  readonly Dictionary<string,GameObject> _armyMarkers=new Dictionary<string,GameObject>();
  readonly Dictionary<string,GameObject> _herdMarkers=new Dictionary<string,GameObject>();
  readonly Dictionary<string,GameObject> _convoyMarkers=new Dictionary<string,GameObject>();
  readonly Dictionary<string,GameObject> _constructionWorkerMarkers=new Dictionary<string,GameObject>();
  readonly Dictionary<string,GameObject> _personMarkers=new Dictionary<string,GameObject>();
  readonly Dictionary<string,Sprite> _unitSpriteCache=new Dictionary<string,Sprite>();
	  readonly Dictionary<string,Sprite[]> _unitWalkFrames=new Dictionary<string,Sprite[]>();
	  readonly Dictionary<string,Sprite[]> _unitAttackFrames=new Dictionary<string,Sprite[]>();
	  const int MaxTerrainDecorations=900;
  readonly HashSet<string> _visiblePeople=new HashSet<string>();
  public void Bind(GameWorld w){_world=w;_art=new SpriteAssetLibrary();_lastVisualDay=w==null?-1:w.Day;Refresh();}
  void OnDestroy(){DestroyMapVisual();foreach(var g in _staticMarkers)if(g!=null)Object.Destroy(g);ClearTerrainMarkers();foreach(var g in _armyMarkers.Values)if(g!=null)Object.Destroy(g);foreach(var g in _herdMarkers.Values)if(g!=null)Object.Destroy(g);foreach(var g in _convoyMarkers.Values)if(g!=null)Object.Destroy(g);foreach(var g in _constructionWorkerMarkers.Values)if(g!=null)Object.Destroy(g);foreach(var g in _personMarkers.Values)if(g!=null)Object.Destroy(g);}
  Vector3 WorldPos(int x,int y,float z){return new Vector3(x-_world.Map.Width*.5f,y-_world.Map.Height*.5f,z);}
  public void Refresh(){DestroyMapVisual();foreach(var g in _staticMarkers)if(g!=null)Object.Destroy(g);_staticMarkers.Clear();ClearTerrainMarkers();foreach(var g in _armyMarkers.Values)if(g!=null)Object.Destroy(g);_armyMarkers.Clear();foreach(var g in _herdMarkers.Values)if(g!=null)Object.Destroy(g);_herdMarkers.Clear();foreach(var g in _convoyMarkers.Values)if(g!=null)Object.Destroy(g);_convoyMarkers.Clear();foreach(var g in _constructionWorkerMarkers.Values)if(g!=null)Object.Destroy(g);_constructionWorkerMarkers.Clear();foreach(var g in _personMarkers.Values)if(g!=null)Object.Destroy(g);_personMarkers.Clear();_unitSpriteCache.Clear();_unitWalkFrames.Clear();_unitAttackFrames.Clear();BuildMapTexture();BuildStaticMarkers();_lastBuildingSignature=BuildingSignature();_lastVisualDay=_world==null?-1:_world.Day;SyncDynamicMarkers();}
  void LateUpdate(){if(_world==null)return;if(_world.Day!=_lastVisualDay){_lastVisualDay=_world.Day;RepaintMapTexture();}if(Time.unscaledTime>=_nextStaticCheck){_nextStaticCheck=Time.unscaledTime+1f;int sig=BuildingSignature();if(sig!=_lastBuildingSignature){_lastBuildingSignature=sig;RebuildStaticMarkers();}}if(Time.unscaledTime>=_nextTerrainSync){_nextTerrainSync=Time.unscaledTime+.35f;UpdateTerrainDecorations();}if(Time.unscaledTime<_nextDynamicSync)return;_nextDynamicSync=Time.unscaledTime+.08f;SyncDynamicMarkers();}
  void DestroyMapVisual(){if(_mapGo!=null)Object.Destroy(_mapGo);_mapGo=null;if(_mapSprite!=null)Object.Destroy(_mapSprite);_mapSprite=null;if(_mapTexture!=null)Object.Destroy(_mapTexture);_mapTexture=null;_terrainPixels=null;}
  int BuildingSignature(){unchecked{int h=17;if(_world==null)return h;h=h*31+_world.Buildings.Count;foreach(Building b in _world.Buildings.Values){if(b==null)continue;h=h*31+SpriteAssetLibrary.StableHash(b.Id);h=h*31+(b.Complete?1:0);h=h*31+(b.Ruined?7:0);}return h;}}
  void RebuildStaticMarkers(){foreach(var g in _staticMarkers)if(g!=null)Object.Destroy(g);_staticMarkers.Clear();BuildStaticMarkers();}
  void ClearTerrainMarkers(){foreach(var g in _terrainMarkers)if(g!=null)Object.Destroy(g);_terrainMarkers.Clear();_terrainCenterX=int.MinValue;_terrainCenterY=int.MinValue;_terrainZoom=-1f;}
  void UpdateTerrainDecorations(){if(_world==null||_world.Map==null)return;Camera cam=Camera.main;if(cam==null)return;int x=Mathf.RoundToInt(cam.transform.position.x+_world.Map.Width*.5f),y=Mathf.RoundToInt(cam.transform.position.y+_world.Map.Height*.5f);float zoom=cam.orthographicSize;if(_terrainCenterX!=int.MinValue&&Mathf.Abs(x-_terrainCenterX)<6&&Mathf.Abs(y-_terrainCenterY)<6&&Mathf.Abs(zoom-_terrainZoom)<Mathf.Max(2f,zoom*.15f))return;BuildTerrainDecorations();}
  void RepaintMapTexture(){if(_mapTexture==null||_world==null||_world.Map==null)return;PixelTerrainPainter.Paint(_world.Map,_terrainPixels);_mapTexture.SetPixels32(_terrainPixels);_mapTexture.Apply(false,false);}
  void BuildMapTexture(){var m=_world.Map;int scale=PixelTerrainPainter.PixelsPerTile;_mapTexture=new Texture2D(m.Width*scale,m.Height*scale,TextureFormat.RGBA32,false);_mapTexture.filterMode=FilterMode.Point;_mapTexture.wrapMode=TextureWrapMode.Clamp;_terrainPixels=new Color32[m.Width*m.Height*scale*scale];RepaintMapTexture();_mapGo=new GameObject("随机世界像素地表");var sr=_mapGo.AddComponent<SpriteRenderer>();_mapSprite=Sprite.Create(_mapTexture,new Rect(0,0,_mapTexture.width,_mapTexture.height),new Vector2(.5f,.5f),scale);sr.sprite=_mapSprite;_mapGo.transform.position=Vector3.zero;var cam=Camera.main;if(cam==null){var cg=new GameObject("主相机");cam=cg.AddComponent<Camera>();cg.tag="MainCamera";}cam.orthographic=true;cam.transform.position=new Vector3(0,0,-10);cam.orthographicSize=m.Height*.52f;}
  void BuildStaticMarkers(){BuildTerrainDecorations();foreach(var c in _world.Cities.Values){Sprite citySprite=_art.FindExact("N01/house");var g=Marker("城市_"+c.Name,citySprite,10,1.35f);g.transform.position=WorldPos(c.X,c.Y,-1);_staticMarkers.Add(g);BuildZoneOutlines(c);}foreach(var b in _world.Buildings.Values){if(!b.Complete)continue;City city=null;_world.Cities.TryGetValue(b.CityId,out city);Sprite sprite=BuildingSprite(b,city);var g=Marker((b.Ruined?"废墟_":"建筑_")+b.Name,sprite,8,BuildingScale(b.Kind));g.transform.position=WorldPos(b.X,b.Y,-.8f);if(b.Ruined){SpriteRenderer sr=g.GetComponent<SpriteRenderer>();if(sr!=null)sr.color=new Color(.45f,.40f,.38f,.65f);g.transform.localScale*=.8f;}_staticMarkers.Add(g);}}
      void BuildTerrainDecorations(){
          if(_world==null||_world.Map==null)return;
          ClearTerrainMarkers();
          WorldMap m=_world.Map;Camera cam=Camera.main;
          float zoom=cam==null?m.Height*.52f:cam.orthographicSize;
          int cx=cam==null?m.Width/2:Mathf.RoundToInt(cam.transform.position.x+m.Width*.5f);
          int cy=cam==null?m.Height/2:Mathf.RoundToInt(cam.transform.position.y+m.Height*.5f);
          float halfW=zoom*(cam==null?1f:cam.aspect);
          int x0=Mathf.Max(0,Mathf.FloorToInt(cx-halfW)-3),x1=Mathf.Min(m.Width-1,Mathf.CeilToInt(cx+halfW)+3);
          int y0=Mathf.Max(0,Mathf.FloorToInt(cy-zoom)-3),y1=Mathf.Min(m.Height-1,Mathf.CeilToInt(cy+zoom)+3);
          int made=0;
          for(int y=y0;y<=y1&&made<MaxTerrainDecorations;y++)for(int x=x0;x<=x1&&made<MaxTerrainDecorations;x++){
              WorldTile t=m.Get(x,y);if(t==null)continue;
              int gate=unchecked((int)(((uint)((x*73856093)^(y*19349663)^_world.Seed))&0x7fffffffu));
              bool important=t.Terrain==TerrainKind.Mountain||t.Terrain==TerrainKind.MountainPass||t.Terrain==TerrainKind.River;
              int stride=zoom>70f?64:(zoom>25f?16:(important?2:4));
              if(gate%stride!=0)continue;
              Sprite sp=TerrainSprite(t,gate);if(sp==null)continue;
              var g=Marker("地形_"+x+"_"+y,sp,2,TerrainScale(t));
              g.transform.position=WorldPos(x,y,-.25f);
              SpriteRenderer sr=g.GetComponent<SpriteRenderer>();if(sr!=null)sr.color=new Color(1f,1f,1f,.83f);
              _terrainMarkers.Add(g);made++;
          }
          _terrainCenterX=cx;_terrainCenterY=cy;_terrainZoom=zoom;
      }
      Sprite TerrainSprite(WorldTile t,int seed){switch(t.Terrain){case TerrainKind.Forest:return _art.FindExact("N01/tree");case TerrainKind.Mountain:return _art.FindVariant(seed,"icontilemountains","highground");case TerrainKind.MountainPass:return _art.FindVariant(seed,"icontilehills","highground");case TerrainKind.Snow:return _art.FindVariant(seed,"iconsnow","cloudsnow");case TerrainKind.Desert:return _art.FindVariant(seed,"icontilesand","sand");case TerrainKind.Marsh:return _art.FindVariant(seed,"icontileswamp","swamp");default:return null;}}
	  float TerrainScale(WorldTile t){if(t.Road)return .42f;if(t.Terrain==TerrainKind.Forest)return 1.55f;if(t.Terrain==TerrainKind.Mountain||t.Terrain==TerrainKind.MountainPass)return .55f;return .38f;}
	  Sprite BuildingSprite(Building b,City city){int seed=SpriteAssetLibrary.StableHash((city==null?"":city.CultureId)+"|"+b.Id);switch(b.Kind){case BuildingKind.Barracks:case BuildingKind.TrainingGround:return _art.FindVariant(seed,"barracks_human","barracks_");case BuildingKind.Tower:return _art.FindVariant(seed,"watch_tower_human","watch_tower_");case BuildingKind.Gate:case BuildingKind.Wall:return _art.FindVariant(seed,"watch_tower_human","tower","building");case BuildingKind.Mine:return _art.FindVariant(seed,"mine#0","icon_tech_building_mine");case BuildingKind.House:return _art.FindExact("N01/house");case BuildingKind.Government:return _art.FindVariant(seed,"hall_human","hall_");case BuildingKind.Workshop:case BuildingKind.Smithy:case BuildingKind.Armory:return _art.FindVariant(seed,"iconCitizenJobBlacksmith","workshop","building");case BuildingKind.Market:return _art.FindVariant(seed,"iconCity","shop","building");case BuildingKind.Stable:return _art.FindVariant(seed,"马匹图标","t_cow","building");case BuildingKind.Granary:return _art.FindVariant(seed,"icon_tech_city_storage","building");default:return _art.FindVariant(seed,"building","house_human");}}
	  float BuildingScale(BuildingKind kind){return kind==BuildingKind.Government?1.15f:((kind==BuildingKind.Wall||kind==BuildingKind.Gate||kind==BuildingKind.Tower) ? .55f : .78f);}
  void BuildZoneOutlines(City city){if(city==null||city.Zones==null)return;foreach(CityZone z in city.Zones){if(z==null)continue;var g=new GameObject("规划区_"+city.Name+"_"+z.Name);var lr=g.AddComponent<LineRenderer>();lr.useWorldSpace=true;lr.loop=true;lr.positionCount=4;lr.widthMultiplier=.07f;Shader shader=Shader.Find("Sprites/Default");if(shader!=null)lr.material=new Material(shader);Color c=ZoneColor(z.Kind);c.a=z.PlayerPlanned ? .78f:.42f;lr.startColor=c;lr.endColor=c;float r=z.Radius+.45f;lr.SetPosition(0,WorldPos(z.CenterX-r,z.CenterY-r,-.55f));lr.SetPosition(1,WorldPos(z.CenterX+r,z.CenterY-r,-.55f));lr.SetPosition(2,WorldPos(z.CenterX+r,z.CenterY+r,-.55f));lr.SetPosition(3,WorldPos(z.CenterX-r,z.CenterY+r,-.55f));_staticMarkers.Add(g);}}
  Vector3 WorldPos(float x,float y,float z){return new Vector3(x-_world.Map.Width*.5f,y-_world.Map.Height*.5f,z);}
  Color ZoneColor(ZoneKind kind){switch(kind){case ZoneKind.Residential:return new Color(.65f,.85f,.60f);case ZoneKind.Commercial:return new Color(.88f,.73f,.32f);case ZoneKind.Workshop:return new Color(.64f,.49f,.34f);case ZoneKind.Agriculture:return new Color(.48f,.76f,.38f);case ZoneKind.Military:return new Color(.72f,.34f,.30f);default:return new Color(.42f,.63f,.82f);}}
  GameObject Marker(string name,Sprite sprite,int order,float scale){var g=new GameObject(name);var sr=g.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=order;g.transform.localScale=Vector3.one*scale;return g;}
  void Tint(GameObject g,string hex,float alpha){if(g==null)return;SpriteRenderer sr=g.GetComponent<SpriteRenderer>();Color c;if(sr!=null&&ColorUtility.TryParseHtmlString(hex,out c)){c.a=alpha;sr.color=c;}}
  string UnitRoleText(string unitId){UnitDef u;SpecialUnitDef sp;if(_world.Data.Units.TryGetValue(unitId,out u))return (u.Name??"")+(u.Category??"")+(u.Role??"");if(_world.Data.SpecialUnits.TryGetValue(unitId,out sp))return (sp.Name??"")+(sp.Category??"")+(sp.Role??"");return "";}
	  Sprite UnitSprite(string unitId){Sprite s;if(_unitSpriteCache.TryGetValue(unitId,out s))return s;string role=UnitRoleText(unitId);int seed=SpriteAssetLibrary.StableHash(unitId);if(role.IndexOf("弓")>=0||role.IndexOf("弩")>=0||role.IndexOf("射")>=0)s=_art.FindVariant(seed,"unit_warrior","icon_bow","warrior");else if(role.IndexOf("骑")>=0)s=_art.FindVariant(seed,"轻骑兵","重骑兵","弓骑兵","将军骑兵","unit_warrior");else s=_art.FindVariant(seed,"unit_warrior","warrior","walk_0");_unitSpriteCache[unitId]=s;return s;}
	  Sprite[] UnitFrames(string unitId,bool attack,Kingdom k){string role=UnitRoleText(unitId);if(role.IndexOf("骑")<0)return new Sprite[0];Dictionary<string,Sprite[]> cache=attack?_unitAttackFrames:_unitWalkFrames;string key=unitId+"|"+(k==null?"":k.Id);Sprite[] frames;if(cache.TryGetValue(key,out frames))return frames;string archetype=(role.IndexOf("弓")>=0||role.IndexOf("射")>=0)?"弓骑兵":(role.IndexOf("重")>=0?"重骑兵":"轻骑兵");int seed=SpriteAssetLibrary.StableHash(key);frames=_art.FindStripFrames(seed,attack?"攻击4帧":"行走4帧",archetype,"将军骑兵");cache[key]=frames;return frames;}
  string ArmyFlagColor(Army a,Kingdom k){Person g;Family f;if(a!=null&&!string.IsNullOrEmpty(a.GeneralId)&&_world.People.TryGetValue(a.GeneralId,out g)&&!string.IsNullOrEmpty(g.FamilyId)&&_world.Families.TryGetValue(g.FamilyId,out f)&&!string.IsNullOrEmpty(f.ColorHex))return f.ColorHex;return k==null?"#FFFFFF":k.ColorHex;}
  Sprite ArmyFlagSprite(Army a){int seed=SpriteAssetLibrary.StableHash(a==null?"":a.Id);return _art.FindVariant(seed,"total_war_banner_icon","iconclan","iconkingdom");}
  void SyncDynamicMarkers(){Sprite horseSprite=_art.FindContains("马匹_","t_cow","t_sheep");Sprite convoySprite=_art.FindContains("wagon","cart","box","barrel","bag");_visiblePeople.Clear();HashSet<string> activeArmies=new HashSet<string>();foreach(var a in _world.Armies.Values){activeArmies.Add(a.Id);GameObject g;if(!_armyMarkers.TryGetValue(a.Id,out g)){g=Marker("军旗_"+a.Name,ArmyFlagSprite(a),24,.58f);_armyMarkers[a.Id]=g;}g.SetActive(true);g.transform.position=WorldPos(a.X+.34f,a.Y+.42f,-2);Kingdom k=null;_world.Kingdoms.TryGetValue(a.KingdomId,out k);Tint(g,ArmyFlagColor(a,k),1f);SyncArmyFormation(a,k);}foreach(var pair in _armyMarkers)if(!activeArmies.Contains(pair.Key))pair.Value.SetActive(false);HashSet<string> activeHerds=new HashSet<string>();foreach(var h in _world.Herds.Values){GameObject g;if(h.Count<=0){if(_herdMarkers.TryGetValue(h.Id,out g))g.SetActive(false);continue;}activeHerds.Add(h.Id);if(!_herdMarkers.TryGetValue(h.Id,out g)){g=Marker("动物_"+h.Species,horseSprite,7,.8f);_herdMarkers[h.Id]=g;}g.SetActive(true);g.transform.position=WorldPos(h.X,h.Y,-.5f);}foreach(var pair in _herdMarkers)if(!activeHerds.Contains(pair.Key))pair.Value.SetActive(false);HashSet<string> activeConvoys=new HashSet<string>();foreach(var c in _world.SupplyConvoys.Values){if(c==null||!c.Active)continue;SupplyRoute route;if(!_world.SupplyRoutes.TryGetValue(c.RouteId,out route)||route.Path==null||route.Path.Count==0)continue;int idx=Mathf.Clamp(c.PathIndex,0,route.Path.Count-1);GridPoint pt=route.Path[idx];activeConvoys.Add(c.Id);GameObject g;if(!_convoyMarkers.TryGetValue(c.Id,out g)){g=Marker("运输队_"+c.Id,convoySprite,16,.72f);_convoyMarkers[c.Id]=g;}g.SetActive(true);g.transform.position=WorldPos(pt.X,pt.Y,-1.4f);Kingdom k;if(_world.Kingdoms.TryGetValue(route.KingdomId,out k))Tint(g,k.ColorHex,.88f);}foreach(var pair in _convoyMarkers)if(!activeConvoys.Contains(pair.Key))pair.Value.SetActive(false);SyncConstructionWorkers();SyncWorldWalkers();foreach(var pair in _personMarkers)if(!_visiblePeople.Contains(pair.Key))pair.Value.SetActive(false);}
  void SyncWorldWalkers(){
    Sprite[] frames=_art.FindExactFrames("N01/walker_strip",4,16);
    if(frames.Length!=4)return;
    foreach(Person p in _world.People.Values){
      if(!p.IsWorldWalker||!p.Alive||p.Injury==InjuryState.Captured)continue;
      string key="resident|"+p.Id;_visiblePeople.Add(key);
      GameObject g;if(!_personMarkers.TryGetValue(key,out g)){g=Marker("居民_"+p.Name,frames[0],22,.7f);_personMarkers[key]=g;}
      g.SetActive(true);SpriteRenderer sr=g.GetComponent<SpriteRenderer>();
      int nextIndex=p.WalkRouteIndex;
      if(p.WalkRoute!=null&&p.WalkRoute.Count>1){
        nextIndex+=p.WalkRouteIndex>=p.WalkRoute.Count-1?-1:(p.WalkRouteIndex<=0?1:(p.WalkForward?1:-1));
        nextIndex=Mathf.Clamp(nextIndex,0,p.WalkRoute.Count-1);
      }
      float progress=Mathf.Clamp01(p.WalkProgress);
      GridPoint target=p.WalkRoute!=null&&p.WalkRoute.Count>1?p.WalkRoute[nextIndex]:new GridPoint(p.X,p.Y);
      bool walking=target.X!=p.X||target.Y!=p.Y;
      sr.sprite=frames[walking?(int)((_world.Clock.TickIndex/3)%4):0];
      if(walking)sr.flipX=target.X<p.X;
      g.transform.position=WorldPos(Mathf.Lerp(p.X,target.X,progress),Mathf.Lerp(p.Y,target.Y,progress),-2f);
    }
  }
  void SyncConstructionWorkers(){Sprite workerSprite=_art.FindContains("builder","worker","unit_peasant","unit_warrior");HashSet<string> active=new HashSet<string>();foreach(ConstructionProject cp in _world.Projects.Values){if(cp==null||cp.Stage==ConstructionStage.Complete||cp.WorkerActions==null)continue;for(int i=0;i<cp.WorkerActions.Count;i++){ConstructionWorkerAction state=cp.WorkerActions[i];Person p;if(state==null||!_world.People.TryGetValue(state.WorkerId,out p)||!p.Alive)continue;string key=cp.Id+"|"+p.Id;active.Add(key);GameObject g;if(!_constructionWorkerMarkers.TryGetValue(key,out g)){g=Marker("施工_"+p.Name,workerSprite,18,.58f);_constructionWorkerMarkers[key]=g;}g.name="施工_"+p.Name+"_"+state.Action;g.SetActive(true);float phase=Time.unscaledTime*3.2f+i*.9f+state.Cycle*.18f;float dx=(i%3-1)*.28f,dy=(i/3)*.20f;if(state.Action=="搬运")dx+=Mathf.Sin(phase)*.24f;else if(state.Action=="锯木"||state.Action=="敲打")dy+=Mathf.Abs(Mathf.Sin(phase))*.16f;else if(state.Action=="砌筑")dy+=Mathf.PingPong(phase*.08f,.16f);else if(state.Action=="铺路")dx+=Mathf.PingPong(phase*.09f,.22f)-.11f;else if(state.Action=="测量")dx+=Mathf.Sin(phase*.45f)*.12f;g.transform.position=WorldPos(cp.X,cp.Y,-1.8f)+new Vector3(dx,dy,0);}}foreach(var pair in _constructionWorkerMarkers)if(!active.Contains(pair.Key))pair.Value.SetActive(false);}
  void SyncArmyFormation(Army a,Kingdom k){Vector3 anchor=WorldPos(a.X,a.Y,-2.25f);BattleSession battle=_world.ActiveBattleForArmy(a.Id);if(battle!=null){float side=battle.AttackerArmyId==a.Id?-.72f:.72f;float surge=Mathf.Sin(Time.unscaledTime*5.4f+(battle.AttackerArmyId==a.Id?0f:1.6f))*.10f;anchor+=new Vector3(side+surge,0,0);}Camera cam=Camera.main;float zoom=cam==null?_world.Map.Height*.52f:cam.orthographicSize;int cap=zoom<=16f?12:(zoom<=32f?6:(zoom<=60f?2:0));if(cap==0)return;for(int si=0;si<a.Squads.Count;si++){if(_visiblePeople.Count>=MaxVisiblePeople)break;Squad sq=a.Squads[si];int shown=Mathf.Min(cap,sq.SoldierIds.Count);Sprite sprite=UnitSprite(sq.UnitTemplateId);bool attacking=a.Order==ArmyOrder.Engage||a.Order==ArmyOrder.PursueLimited||a.Order==ArmyOrder.PursueFull;Sprite[] frames=UnitFrames(sq.UnitTemplateId,attacking,k);if(frames.Length==4)sprite=frames[Mathf.FloorToInt(Time.unscaledTime*6f)%4];for(int i=0;i<shown&&_visiblePeople.Count<MaxVisiblePeople;i++){string pid=sq.SoldierIds[i];Person p;if(!_world.People.TryGetValue(pid,out p)||!p.Alive||p.Injury==InjuryState.Dead||p.Injury==InjuryState.Captured||p.Injury==InjuryState.Incapacitated)continue;string key=a.Id+"|"+pid;_visiblePeople.Add(key);GameObject g;if(!_personMarkers.TryGetValue(key,out g)){g=Marker("士兵_"+p.Name,sprite,21,.62f);_personMarkers[key]=g;}g.SetActive(true);SpriteRenderer unitRenderer=g.GetComponent<SpriteRenderer>();if(unitRenderer!=null&&sprite!=null)unitRenderer.sprite=sprite;GridPoint f=MarchFormationSystem.FormationSlot(new GridPoint(a.X,a.Y),si,i,a.Order==ArmyOrder.March);float ox=(f.X-a.X)*.12f,oy=(f.Y-a.Y)*.12f;g.transform.position=anchor+new Vector3(ox,oy,0)+BattleVisualOffset(battle,a,pid);if(k!=null)Tint(g,k.ColorHex,.92f);}}

  Vector3 BattleVisualOffset(BattleSession battle,Army army,string personId){
    if(battle==null||army==null||battle.Report==null||string.IsNullOrEmpty(personId))return Vector3.zero;BattleReport r=battle.Report;float pulse=(Mathf.Sin(Time.unscaledTime*18f)+1f)*.5f;float dir=battle.AttackerArmyId==army.Id?1f:-1f;
    if(r.LastActorId==personId){float amount=.10f;if(r.LastAttackAction=="重击")amount=.18f;else if(r.LastAttackAction=="刺击")amount=.14f;else if(r.LastAttackAction=="远程射击")amount=.05f;else if(r.LastAttackAction=="骑兵冲锋")amount=.28f;return new Vector3(dir*amount*pulse,0,0);}
    if(r.LastTargetId==personId){if(r.LastDefenseAction=="闪避")return new Vector3(0,(pulse-.5f)*.20f,0);float recoil=r.LastDamage>0?Mathf.Min(.16f,.035f+r.LastDamage*.006f):.035f;if(r.LastDefenseAction=="盾挡"||r.LastDefenseAction=="武器格挡")recoil*=.45f;return new Vector3(-dir*recoil*pulse,0,0);}
    return Vector3.zero;
  }
  }
 }
}
#endif
