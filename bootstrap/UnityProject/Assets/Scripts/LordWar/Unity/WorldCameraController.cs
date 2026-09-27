#if UNITY_5_3_OR_NEWER
using UnityEngine;
using LordWar.Simulation;

namespace LordWar.UnityRuntime {
    /// <summary>Android/Editor world navigation. One finger/mouse drag pans; two fingers/mouse wheel zoom.</summary>
    public sealed class WorldCameraController : MonoBehaviour {
        public float minOrthographicSize=7f;
        public float zoomSpeed=.035f;
        public float mouseZoomSpeed=5f;
        Camera _camera;
        Vector2 _lastPointer;
        float _lastPinchDistance;
        bool _dragging;

        void LateUpdate(){
            GameWorld world=LordWarBootstrap.World;
            if(world==null||world.Map==null)return;
            EnsureCamera(world);
            if(_camera==null)return;
            ChineseHud hud=GetComponent<ChineseHud>();if(hud!=null&&hud.BlocksWorldInput){_dragging=false;_lastPinchDistance=0f;return;}
            HandleTouch(world);
            HandleMouse(world);
            ClampCamera(world);
        }

        void EnsureCamera(GameWorld world){
            if(_camera==null)_camera=Camera.main;
            if(_camera==null)return;
            _camera.orthographic=true;
            float maxSize=MaxOrthographicSize(world);
            _camera.orthographicSize=Mathf.Clamp(_camera.orthographicSize,minOrthographicSize,maxSize);
        }

        void HandleTouch(GameWorld world){
            if(Input.touchCount>=2){
                Touch a=Input.GetTouch(0),b=Input.GetTouch(1);
                float distance=Vector2.Distance(a.position,b.position);
                if(_lastPinchDistance>0f){
                    float delta=distance-_lastPinchDistance;
                    _camera.orthographicSize=Mathf.Clamp(_camera.orthographicSize-delta*zoomSpeed,minOrthographicSize,MaxOrthographicSize(world));
                }
                _lastPinchDistance=distance;
                _dragging=false;
                return;
            }
            _lastPinchDistance=0f;
            if(Input.touchCount!=1)return;
            Touch t=Input.GetTouch(0);
            if(IsHudTouch(t.position))return;
            if(t.phase==TouchPhase.Began){_lastPointer=t.position;_dragging=true;return;}
            if(!_dragging)return;
            if(t.phase==TouchPhase.Moved){PanPixels(t.position-_lastPointer);_lastPointer=t.position;}
            if(t.phase==TouchPhase.Ended||t.phase==TouchPhase.Canceled)_dragging=false;
        }

        void HandleMouse(GameWorld world){
            float wheel=Input.mouseScrollDelta.y;
            if(Mathf.Abs(wheel)>.001f&&!IsHudTouch(Input.mousePosition))_camera.orthographicSize=Mathf.Clamp(_camera.orthographicSize-wheel*mouseZoomSpeed,minOrthographicSize,MaxOrthographicSize(world));
            if(Input.touchCount>0)return;
            if(Input.GetMouseButtonDown(1)&&!IsHudTouch(Input.mousePosition)){_lastPointer=Input.mousePosition;_dragging=true;}
            if(Input.GetMouseButton(1)&&_dragging){Vector2 now=Input.mousePosition;PanPixels(now-_lastPointer);_lastPointer=now;}
            if(Input.GetMouseButtonUp(1))_dragging=false;
        }

        void PanPixels(Vector2 delta){
            if(_camera==null)return;
            float unitsPerPixel=(_camera.orthographicSize*2f)/Mathf.Max(1f,Screen.height);
            Vector3 p=_camera.transform.position;
            p.x-=delta.x*unitsPerPixel;
            p.y-=delta.y*unitsPerPixel;
            _camera.transform.position=p;
        }

        bool IsHudTouch(Vector2 screenPoint){
            // Input's origin is bottom-left while IMGUI's is top-left. Reserve the visible HUD sidebars.
            float guiY=Screen.height-screenPoint.y;
            if(screenPoint.x<=340f&&guiY<=346f)return true;
            if(screenPoint.x>=348f&&screenPoint.x<=880f&&guiY<=720f)return true;
            if(screenPoint.x>=Screen.width-730f&&guiY<=560f)return true;
            return false;
        }

        float MaxOrthographicSize(GameWorld world){
            if(world==null||world.Map==null)return 80f;
            float byHeight=world.Map.Height*.52f;
            float aspect=Mathf.Max(.4f,_camera==null?1f:_camera.aspect);
            float byWidth=world.Map.Width*.52f/aspect;
            return Mathf.Max(minOrthographicSize,Mathf.Min(byHeight,byWidth));
        }

        void ClampCamera(GameWorld world){
            float halfH=_camera.orthographicSize;
            float halfW=halfH*Mathf.Max(.4f,_camera.aspect);
            float worldHalfW=world.Map.Width*.5f;
            float worldHalfH=world.Map.Height*.5f;
            Vector3 p=_camera.transform.position;
            float maxX=Mathf.Max(0f,worldHalfW-halfW);
            float maxY=Mathf.Max(0f,worldHalfH-halfH);
            p.x=Mathf.Clamp(p.x,-maxX,maxX);
            p.y=Mathf.Clamp(p.y,-maxY,maxY);
            p.z=-10f;
            _camera.transform.position=p;
        }

        public void FocusWorldPoint(float worldX,float worldY){
            GameWorld world=LordWarBootstrap.World;if(world==null||world.Map==null)return;EnsureCamera(world);if(_camera==null)return;
            _camera.transform.position=new Vector3(worldX-world.Map.Width*.5f,worldY-world.Map.Height*.5f,-10f);ClampCamera(world);
        }
    }
}
#endif
