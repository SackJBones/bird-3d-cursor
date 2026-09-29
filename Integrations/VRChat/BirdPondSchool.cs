using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Local cosmetic boids. Shared Bird snapshots are stimuli, never fish authority.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(100)]
public class BirdPondSchool : UdonSharpBehaviour
{
    public BirdPersonalStation station;
    public Transform[] fish;
    [Tooltip("Local-space sampled water centerline; refresh explicitly after editing the pond profile.")]
    public Vector3[] centerline;
    public float waterHalfWidth = 2;
    public float waterSurface = -2.6f;
    public float waterDepth = .55f;
    [Range(.05f,.2f)] public float stepSeconds = .1f;
    public float swimSpeed = .85f;
    public float maxAcceleration = 1.6f;
    public float activationDistance = 100;
    [HideInInspector] public int stimulusCount, simulationSteps, discoveredSources;
    private BirdCursorState[] sources = new BirdCursorState[162];
    private Vector3[] stimuli = new Vector3[162];
    private BirdCursorState[] stimulusSources = new BirdCursorState[162], followed = new BirdCursorState[4];
    private Vector3[] positions, previous, velocities, nextVelocities;
    private int[] segments;
    private Vector3[] goals = new Vector3[4];
    private int[] chosen = new int[4];
    private int[] goalSegments = new int[4];
    private Vector3 boundsMin, boundsMax;
    private float nextDiscovery, lastStep, patrolTime;
    private bool ready, sleeping;
    private Vector3 closestPoint;
    private int closestSegment;

    private void Start() { Initialize(); }
    private void Initialize()
    {
        ready = false;
        if (fish == null || fish.Length != 24 || centerline == null || centerline.Length < 2 || centerline.Length > 128 ||
            waterHalfWidth < .8f || waterDepth < .35f || !Finite(new Vector3(waterHalfWidth,waterSurface,waterDepth)) ||
            !Finite(new Vector3(swimSpeed,maxAcceleration,stepSeconds)) || swimSpeed<=0 || swimSpeed>3 || maxAcceleration<=0 || maxAcceleration>10) return;
        positions = new Vector3[24]; previous = new Vector3[24]; velocities = new Vector3[24]; nextVelocities = new Vector3[24]; segments = new int[24];
        boundsMin = boundsMax = centerline[0];
        for (int i=0;i<centerline.Length;i++)
        {
            if (!Finite(centerline[i])) return;
            boundsMin=Vector3.Min(boundsMin,centerline[i]); boundsMax=Vector3.Max(boundsMax,centerline[i]);
        }
        boundsMin-=new Vector3(waterHalfWidth,0,waterHalfWidth); boundsMax+=new Vector3(waterHalfWidth,0,waterHalfWidth);
        for (int i=0;i<24;i++)
        {
            if (fish[i]==null) return;
            int group=i/6, k=Mathf.Clamp((int)((group+.5f)*centerline.Length/4f)+(i%6)-3,0,centerline.Length-2);
            Vector3 tangent=(centerline[k+1]-centerline[k]).normalized;
            Vector3 side=new Vector3(tangent.z,0,-tangent.x);
            positions[i]=centerline[k]+side*((i%3-1)*.32f); positions[i].y=Depth(i);
            previous[i]=positions[i]; velocities[i]=tangent*swimSpeed; segments[i]=k;
            fish[i].localPosition=positions[i]; fish[i].localRotation=Quaternion.LookRotation(tangent,Vector3.up);
        }
        for(int i=0;i<4;i++){chosen[i]=-1;followed[i]=null;}
        lastStep=Time.realtimeSinceStartup; nextDiscovery=0; ready=true;
    }
    public override void PostLateUpdate()
    {
        if(!ready)return;
        float now=Time.realtimeSinceStartup;
        var player=Networking.LocalPlayer;
        if(Utilities.IsValid(player) && Vector3.Distance(player.GetPosition(),transform.TransformPoint(centerline[centerline.Length/2]))>activationDistance)
        { sleeping=true; lastStep=now; stimulusCount=0; return; }
        if(sleeping){for(int i=0;i<24;i++)previous[i]=positions[i];sleeping=false;lastStep=now;}
        if(now>=nextDiscovery){DiscoverSources();nextDiscovery=now+2;}
        float interval=Mathf.Clamp(stepSeconds,.05f,.2f);
        if(now-lastStep>=interval)
        {
            // Never catch up a long suspension with an unbounded burst or leap.
            Step(); lastStep=now;
        }
        float t=Mathf.Clamp01((now-lastStep)/interval);
        for(int i=0;i<24;i++)
        {
            fish[i].localPosition=Vector3.Lerp(previous[i],positions[i],t);
            if(velocities[i].sqrMagnitude>.0001f)
                fish[i].localRotation=Quaternion.Slerp(fish[i].localRotation,Quaternion.LookRotation(velocities[i],Vector3.up),1-Mathf.Exp(-8*Time.deltaTime));
        }
    }
    public void DiscoverSources()
    {
        for(int i=0;i<sources.Length;i++)sources[i]=null;
        if(station!=null){sources[0]=station.left;sources[1]=station.right;}
        int count=2;
        var players=new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()]; VRCPlayerApi.GetPlayers(players);
        for(int p=0;p<players.Length && count+1<sources.Length;p++)
        {
            if(!Utilities.IsValid(players[p]) || players[p].isLocal)continue;
            var objects=Networking.GetPlayerObjects(players[p]);
            for(int j=0;j<objects.Length && count+1<sources.Length;j++)
            {
                if(!Utilities.IsValid(objects[j]))continue;
                var social=objects[j].GetComponent<BirdSocialPresentation>();
                if(social==null)continue;
                sources[count++]=social.remoteLeft; sources[count++]=social.remoteRight; break;
            }
        }
        discoveredSources=count;
    }
    public override void OnPlayerJoined(VRCPlayerApi player){nextDiscovery=0;}
    public override void OnPlayerLeft(VRCPlayerApi player){nextDiscovery=0;}
    public void Step()
    {
        if(!ready)return;
        GatherStimuli(); Simulate(Mathf.Clamp(stepSeconds,.05f,.2f));
    }
    public void GatherStimuli()
    {
        stimulusCount=0;
        for(int i=0;i<discoveredSources;i++)
        {
            var cursor=sources[i];
            if(cursor==null || !cursor.enabled || !cursor.gameObject.activeInHierarchy || !cursor.poseValid)continue;
            if(i<2 && (station==null || !station.acquired || station.personalRig==null || !station.personalRig.activeInHierarchy))continue;
            // Observer interpolation can sweep between two out-of-water samples.
            // Use its latest accepted logical endpoint, never its view/proxy.
            Vector3 point=i<2?cursor.position:cursor.rawPosition;
            if(!Finite(point))continue;
            point=transform.InverseTransformPoint(point);
            if(!Contains(point))continue;
            point.y=waterSurface-.16f; stimulusSources[stimulusCount]=cursor; stimuli[stimulusCount++]=point;
        }
    }
    public bool Contains(Vector3 point)
    {
        if(!ready || !Finite(point) || point.y>waterSurface || point.y<waterSurface-waterDepth ||
            point.x<boundsMin.x || point.x>boundsMax.x || point.z<boundsMin.z || point.z>boundsMax.z)return false;
        Nearest(point,0,centerline.Length-2);
        Vector3 delta=point-closestPoint;delta.y=0;
        return delta.sqrMagnitude<=(waterHalfWidth-.015f)*(waterHalfWidth-.015f);
    }
    private void Nearest(Vector3 point,int first,int last)
    {
        float best=float.MaxValue;
        for(int k=first;k<=last;k++)
        {
            Vector3 a=centerline[k],d=centerline[k+1]-a; d.y=0;
            Vector3 offset=point-a;offset.y=0;
            float length=d.sqrMagnitude;
            Vector3 q=a+d*(length>.000001f?Mathf.Clamp01(Vector3.Dot(offset,d)/length):0);
            Vector3 delta=point-q;delta.y=0;
            float distance=delta.sqrMagnitude;
            if(distance<best){best=distance;closestPoint=q;closestSegment=k;}
        }
    }
    private void Simulate(float dt)
    {
        patrolTime+=dt; simulationSteps++;
        for(int g=0;g<4;g++)
        {
            Vector3 mean=Vector3.zero;for(int j=0;j<6;j++)mean+=positions[g*6+j];mean/=6;
            float best=float.MaxValue;int target=-1;
            for(int b=0;b<stimulusCount;b++)
            {
                float distance=(stimuli[b]-mean).sqrMagnitude;
                if(stimulusSources[b]==followed[g])distance*=.64f;
                if(distance<best){best=distance;target=b;}
            }
            // Retain a nearby target until another is meaningfully closer.
            chosen[g]=target;
            followed[g]=target>=0?stimulusSources[target]:null;
            if(target>=0)goals[g]=stimuli[target];
            else
            {
                float u=Mathf.PingPong(g*.25f+patrolTime*.012f,1)*(centerline.Length-1);
                int k=Mathf.Min((int)u,centerline.Length-2);
                goals[g]=Vector3.Lerp(centerline[k],centerline[k+1],u-k);
            }
            goals[g].y=waterSurface-.16f;
            Nearest(goals[g],0,centerline.Length-2);goalSegments[g]=closestSegment;
        }
        for(int i=0;i<24;i++)
        {
            Vector3 p=positions[i],cohesion=Vector3.zero,alignment=Vector3.zero,separation=Vector3.zero; int neighbors=0;
            for(int j=0;j<24;j++)
            {
                if(j==i)continue; Vector3 d=p-positions[j];d.y=0;float sq=d.sqrMagnitude;
                if(j/6==i/6 && sq<16){cohesion+=positions[j];alignment+=velocities[j];neighbors++;}
                if(sq<.64f && sq>.00001f)separation+=d/Mathf.Max(sq,.025f);
            }
            Vector3 goal=goals[i/6];
            // Unequal low-frequency wander avoids a synchronized ring around a
            // stationary Bird. Other shoals still repel, without sharing cohesion.
            if(chosen[i/6]>=0)goal+=new Vector3(Mathf.Sin(patrolTime*.31f+i*1.71f),0,Mathf.Sin(patrolTime*.23f+i*.89f))*1.05f;
            // Follow the channel around its bend rather than cutting across dry land.
            int goalSegment=goalSegments[i/6];
            if(Mathf.Abs(goalSegment-segments[i])>7)goal=centerline[Mathf.Clamp(segments[i]+(goalSegment>segments[i]?6:-6),0,centerline.Length-1)];
            goal.y=p.y;
            Vector3 force=(goal-p).normalized*swimSpeed-velocities[i];
            if(neighbors>0)force+=(cohesion/neighbors-p)*.35f+(alignment/neighbors-velocities[i])*.35f;
            force+=separation*.4f;force.y=0;
            nextVelocities[i]=Vector3.ClampMagnitude(velocities[i]+Vector3.ClampMagnitude(force,maxAcceleration)*dt,swimSpeed*1.35f);
        }
        for(int i=0;i<24;i++)
        {
            previous[i]=positions[i];Vector3 next=positions[i]+nextVelocities[i]*dt;
            Nearest(next,Mathf.Max(0,segments[i]-3),Mathf.Min(centerline.Length-2,segments[i]+3));segments[i]=closestSegment;
            Vector3 offset=next-closestPoint;offset.y=0;float margin=waterHalfWidth-.55f;
            if(offset.sqrMagnitude>margin*margin)
            {
                Vector3 normal=offset.normalized;next=closestPoint+normal*margin;
                nextVelocities[i]-=normal*Mathf.Max(0,Vector3.Dot(nextVelocities[i],normal));
            }
            next.y=Depth(i);positions[i]=next;velocities[i]=nextVelocities[i];
        }
    }
    private float Depth(int i){return waterSurface-.12f-(i%4)*.035f+Mathf.Sin(patrolTime*.55f+i)*.012f;}
    private bool Finite(Vector3 p){return Mathf.Abs(p.x)<1e12f && Mathf.Abs(p.y)<1e12f && Mathf.Abs(p.z)<1e12f;}
    private void OnDisable(){stimulusCount=0;ready=false;}
    private void OnEnable(){if(positions!=null)Initialize();}
}
