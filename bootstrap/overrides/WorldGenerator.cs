using System;
using System.Collections.Generic;
namespace LordWar.World {
    public sealed class WorldGenerator {
        readonly int _seed; readonly DeterministicRandom _rng;
        public WorldGenerator(int seed){_seed=seed;_rng=new DeterministicRandom(seed);}
        public WorldMap Generate(int w,int h,int cityCount){var m=new WorldMap(w,h,_seed);GenerateFields(m);CarveMountainChains(m);SeedLakes(m);TraceRivers(m,Mathx.Clamp(cityCount+1,2,6));Classify(m);MarkNaturalFords(m);AssignResourceZones(m);PlaceCities(m,cityCount,Math.Max(12,Math.Min(w,h)/5));BuildRoadNetwork(m);return m;}
        float HashNoise(int x,int y,int salt){unchecked{uint n=(uint)(x*374761393+y*668265263+_seed*69069+salt*362437);n=(n^(n>>13))*1274126177u;n^=n>>16;return (n&0xffff)/65535f;}}
        float SmoothNoise(float x,float y,int salt){int x0=(int)Math.Floor(x),y0=(int)Math.Floor(y);float tx=x-x0,ty=y-y0;float a=HashNoise(x0,y0,salt),b=HashNoise(x0+1,y0,salt),c=HashNoise(x0,y0+1,salt),d=HashNoise(x0+1,y0+1,salt);float ab=a+(b-a)*tx,cd=c+(d-c)*tx;return ab+(cd-ab)*ty;}
        float Fbm(int x,int y,int salt){float sum=0,amp=.55f,scale=48f,norm=0;for(int o=0;o<5;o++){sum+=SmoothNoise(x/scale,y/scale,salt+o*31)*amp;norm+=amp;amp*=.5f;scale*=.5f;}return sum/norm;}
        void GenerateFields(WorldMap m){float cx=(m.Width-1)*.5f,cy=(m.Height-1)*.5f,max=(float)Math.Sqrt(cx*cx+cy*cy);foreach(var t in m.Tiles){float radial=1f-(float)Math.Sqrt((t.X-cx)*(t.X-cx)+(t.Y-cy)*(t.Y-cy))/max;t.Continental=Fbm(t.X,t.Y,11)*.72f+radial*.28f;t.Height=Mathx.Clamp((Fbm(t.X,t.Y,73)-.46f)*1.35f+t.Continental*.7f,0,1);t.Moisture=Fbm(t.X,t.Y,151);t.Temperature=Mathx.Clamp(1f-Math.Abs((t.Y/(float)m.Height)*2f-1f)*.65f+(Fbm(t.X,t.Y,233)-.5f)*.25f,0,1);t.Fertility=Mathx.Clamp(t.Moisture*.62f+(1f-Math.Abs(t.Temperature-.55f))*.25f+(Fbm(t.X,t.Y,307)-.5f)*.2f,0,1);t.Forest=Mathx.Clamp(t.Moisture*.75f+(Fbm(t.X,t.Y,401)-.5f)*.45f,0,1);t.Ore=Mathx.Clamp(t.Height*.5f+Fbm(t.X,t.Y,509)*.6f-.25f,0,1);}}
        void CarveMountainChains(WorldMap m){int chains=_rng.Range(3,8);for(int c=0;c<chains;c++){float x=_rng.Range(m.Width/6,m.Width*5/6),y=_rng.Range(m.Height/6,m.Height*5/6),angle=_rng.Next01()*6.28318f;int len=_rng.Range(18,Math.Max(19,Math.Min(m.Width,m.Height)/2));for(int i=0;i<len;i++){int ix=(int)x,iy=(int)y;for(int oy=-3;oy<=3;oy++)for(int ox=-3;ox<=3;ox++){var t=m.Get(ix+ox,iy+oy);if(t==null)continue;float dist=(float)Math.Sqrt(ox*ox+oy*oy);float add=Math.Max(0,1f-dist/4f)*.38f;t.Height=Mathx.Clamp(t.Height+add,0,1);}angle+=(_rng.Next01()-.5f)*.28f;x+=(float)Math.Cos(angle)*1.45f;y+=(float)Math.Sin(angle)*1.45f;if(x<3||y<3||x>m.Width-4||y>m.Height-4)break;}}}
        void SeedLakes(WorldMap m){int target=Mathx.Clamp(Math.Min(m.Width,m.Height)/28,2,6),made=0,attempts=0;while(made<target&&attempts++<target*24){int cx=_rng.Range(6,m.Width-6),cy=_rng.Range(6,m.Height-6);WorldTile center=m.Get(cx,cy);if(center==null||center.Height<.24f||center.Height>.58f)continue;int rx=_rng.Range(2,5),ry=_rng.Range(2,5),tiles=0;for(int y=-ry;y<=ry;y++)for(int x=-rx;x<=rx;x++){WorldTile t=m.Get(cx+x,cy+y);if(t==null)continue;float q=(x*x)/(float)(rx*rx)+(y*y)/(float)(ry*ry);if(q<=1f&&t.Height<.62f){t.Lake=true;t.Height=Math.Min(t.Height,.19f);t.Moisture=Math.Max(t.Moisture,.82f);tiles++;}}if(tiles>=5)made++;}}
        void MarkNaturalFords(WorldMap m){foreach(WorldTile t in m.Tiles){if(!t.River||t.Lake||t.Terrain==TerrainKind.DeepWater)continue;int banks=0;float localRelief=0f;foreach(WorldTile n in m.Neighbors4(t.X,t.Y)){if(!n.River&&!n.Lake&&n.Terrain!=TerrainKind.DeepWater)banks++;localRelief=Math.Max(localRelief,Math.Abs(n.Height-t.Height));}if(banks>=2&&localRelief<.16f&&HashNoise(t.X,t.Y,1337)>.78f)t.Ford=true;}}
        void AssignResourceZones(WorldMap m){foreach(WorldTile t in m.Tiles){t.ResourceDeposit="";t.ResourceRichness=0;if(t.Terrain==TerrainKind.DeepWater||t.Terrain==TerrainKind.Lake)continue;if(t.Ore>.72f){t.ResourceDeposit=t.Ore>.88f?"富铁矿":"铁矿";t.ResourceRichness=(int)Math.Round(t.Ore*100);}else if(t.Forest>.72f){t.ResourceDeposit="林木";t.ResourceRichness=(int)Math.Round(t.Forest*100);}else if(t.Fertility>.72f){t.ResourceDeposit="沃土";t.ResourceRichness=(int)Math.Round(t.Fertility*100);}else if(t.Terrain==TerrainKind.Grass&&t.Moisture<.42f){t.ResourceDeposit="牧草地";t.ResourceRichness=(int)Math.Round((1f-t.Moisture)*70f);}}}
        void TraceRivers(WorldMap m,int desired){var sources=new List<WorldTile>();foreach(var t in m.Tiles)if(t.Height>.72f&&t.Moisture>.45f)sources.Add(t);sources.Sort((a,b)=>b.Height.CompareTo(a.Height));for(int r=0;r<desired&&sources.Count>0;r++){var cur=sources[Math.Min(sources.Count-1,r%sources.Count)];var seen=new HashSet<int>();for(int step=0;step<(m.Width+m.Height)*2;step++){int key=cur.Y*m.Width+cur.X;if(seen.Contains(key))break;seen.Add(key);if(cur.Lake)break;cur.River=true;if(cur.Height<.235f||cur.X<=1||cur.Y<=1||cur.X>=m.Width-2||cur.Y>=m.Height-2){cur.Height=Math.Min(cur.Height,.17f);break;}WorldTile best=null;float score=999f;foreach(var n in m.Neighbors4(cur.X,cur.Y)){int nk=n.Y*m.Width+n.X;if(seen.Contains(nk))continue;int edge=Math.Min(Math.Min(n.X,m.Width-1-n.X),Math.Min(n.Y,m.Height-1-n.Y));float s=n.Height+edge*.0018f+HashNoise(n.X,n.Y,777)*.018f;if(n.River)s-=.12f;if(n.Lake)s-=.2f;if(s<score){score=s;best=n;}}if(best==null)break;if(best.Height>=cur.Height&&!best.Lake&&!best.River)best.Height=Math.Max(.17f,cur.Height-.006f);best.Moisture=Math.Max(best.Moisture,.58f);cur=best;}}}
        void Classify(WorldMap m){foreach(var t in m.Tiles){if(t.Lake)t.Terrain=TerrainKind.Lake;else if(t.Height<.18f)t.Terrain=TerrainKind.DeepWater;else if(t.Height<.24f)t.Terrain=TerrainKind.Coast;else if(t.Height>.82f)t.Terrain=TerrainKind.Mountain;else if(t.Temperature<.22f&&t.Height>.55f)t.Terrain=TerrainKind.Snow;else if(t.River)t.Terrain=TerrainKind.River;else if(t.Moisture>.78f&&t.Height<.38f)t.Terrain=TerrainKind.Marsh;else if(t.Moisture<.26f&&t.Temperature>.58f&&t.Height<.56f)t.Terrain=TerrainKind.Desert;else if(t.Forest>.62f)t.Terrain=TerrainKind.Forest;else if(t.Height>.58f)t.Terrain=TerrainKind.Hill;else t.Terrain=TerrainKind.Grass;}for(int y=1;y<m.Height-1;y++)for(int x=1;x<m.Width-1;x++){var t=m.Get(x,y);if(t.Terrain!=TerrainKind.Mountain)continue;int low=0;foreach(var n in m.Neighbors4(x,y))if(n.Height<.65f)low++;if(low>=3&&HashNoise(x,y,991)>.72f)t.Terrain=TerrainKind.MountainPass;}}
        void PlaceCities(WorldMap m,int count,int minDist){var candidates=new List<WorldTile>();foreach(var t in m.Tiles)if(t.Height>.25f&&t.Height<.63f&&t.Fertility>.48f&&t.Terrain!=TerrainKind.Marsh&&t.Terrain!=TerrainKind.Lake){bool water=false;foreach(var n in m.Neighbors4(t.X,t.Y))if(n.River||n.Lake||n.Terrain==TerrainKind.Coast)water=true;if(water||t.Fertility>.7f)candidates.Add(t);}candidates.Sort((a,b)=>(b.Fertility+b.Ore*.25f).CompareTo(a.Fertility+a.Ore*.25f));for(int i=0;i<candidates.Count&&m.CitySites.Count<count;i++){var t=candidates[i];if(CityFarEnough(m,t,minDist))m.CitySites.Add(new GridPoint(t.X,t.Y));}if(m.CitySites.Count<count){var fallback=new List<WorldTile>();foreach(var t in m.Tiles)if(t.Terrain!=TerrainKind.DeepWater&&t.Terrain!=TerrainKind.Lake&&t.Terrain!=TerrainKind.Mountain&&t.Height>.23f)fallback.Add(t);fallback.Sort((a,b)=>(b.Fertility+b.Ore*.15f).CompareTo(a.Fertility+a.Ore*.15f));for(int pass=0;pass<3&&m.CitySites.Count<count;pass++){int d=Math.Max(5,minDist-pass*Math.Max(2,minDist/4));for(int i=0;i<fallback.Count&&m.CitySites.Count<count;i++)if(CityFarEnough(m,fallback[i],d))m.CitySites.Add(new GridPoint(fallback[i].X,fallback[i].Y));}}}
        bool CityFarEnough(WorldMap m,WorldTile t,int minDist){foreach(var c in m.CitySites)if(Mathx.Manhattan(c.X,c.Y,t.X,t.Y)<minDist)return false;return true;}
        void BuildRoadNetwork(WorldMap m){for(int i=1;i<m.CitySites.Count;i++){var a=m.CitySites[i-1];var b=m.CitySites[i];var path=FindPath(m,a,b,true);foreach(var p in path){var t=m.Get(p.X,p.Y);if(t!=null&&t.Terrain!=TerrainKind.DeepWater&&t.Terrain!=TerrainKind.Lake){if(t.River)t.Bridge=true;t.Road=true;t.RoadLevel=Math.Max(t.RoadLevel,1);t.RoadCapacity=Math.Max(t.RoadCapacity,2);t.RoadCondition=1f;}}}}
        public List<GridPoint> FindPath(WorldMap m, GridPoint start, GridPoint goal, bool roadBuild) {
            var open = new PriorityQueue<GridPoint, float>();
            var came = new Dictionary<int, int>();
            var g = new Dictionary<int, float>();
            var closed = new HashSet<int>();
            int sk = start.Y * m.Width + start.X, gk = goal.Y * m.Width + goal.X;
            g[sk] = 0f;
            open.Enqueue(start, Mathx.Manhattan(start.X, start.Y, goal.X, goal.Y));
            int loops = 0, loopLimit = m.Width * m.Height * 8;
            while (open.Count > 0 && loops++ < loopLimit) {
                GridPoint cur = open.Dequeue();
                int ck = cur.Y * m.Width + cur.X;
                if (closed.Contains(ck)) continue;
                if (ck == gk) break;
                closed.Add(ck);
                float currentG;
                if (!g.TryGetValue(ck, out currentG)) continue;
                foreach (WorldTile n in m.Neighbors4(cur.X, cur.Y)) {
                    if (n.Terrain == TerrainKind.DeepWater || n.Terrain == TerrainKind.Lake) continue;
                    int nk = n.Y * m.Width + n.X;
                    if (closed.Contains(nk)) continue;
                    float cost = 1f
                        + (n.Height > .65f ? 2.4f : 0f)
                        + (n.Terrain == TerrainKind.Marsh ? 2f : 0f)
                        + (n.River && !n.Bridge ? (n.Ford ? .62f : 1.8f) : 0f)
                        + (n.Road ? -(.28f + .16f * Math.Max(1, n.RoadLevel)) : 0f);
                    if (!roadBuild && n.Terrain == TerrainKind.Mountain) cost += 4f;
                    float ng = currentG + Math.Max(.05f, cost);
                    float old;
                    if (!g.TryGetValue(nk, out old) || ng < old) {
                        g[nk] = ng;
                        came[nk] = ck;
                        float priority = ng + Mathx.Manhattan(n.X, n.Y, goal.X, goal.Y);
                        open.Enqueue(new GridPoint(n.X, n.Y), priority);
                    }
                }
            }
            var path = new List<GridPoint>();
            int k2 = gk;
            if (!came.ContainsKey(k2) && k2 != sk) return path;
            path.Add(goal);
            while (k2 != sk) {
                int prev;
                if (!came.TryGetValue(k2, out prev)) return new List<GridPoint>();
                path.Add(new GridPoint(prev % m.Width, prev / m.Width));
                k2 = prev;
            }
            path.Reverse();
            return path;
        }
    }
}