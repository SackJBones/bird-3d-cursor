using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Optional laboratory presentation. Never feeds projected positions back to Bird.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(100)]
public class BirdLabPointView : UdonSharpBehaviour
{
    public BirdAvatarHandInput input;
    public Renderer core;
    public LineRenderer halo;
    [Tooltip("Optional short lab guide toward the logical point; never changes its range.")]
    public LineRenderer directionGuide;
    public Transform[] tipMarkers;
    public Color tint = Color.cyan;
    private MaterialPropertyBlock coreProperties, haloProperties;
    public override void PostLateUpdate()
    {
        if (!enabled || !gameObject.activeInHierarchy || input == null || !input.enabled || !input.gameObject.activeInHierarchy || !input.tipsReady ||
            input.sampledFrame != Time.frameCount || input.cursor == null || !input.cursor.poseValid)
        { Clear(); return; }
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) { Clear(); return; }
        var head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        if (directionGuide != null)
        {
            Vector3 fromHand = input.cursor.position-input.cursor.handRoot;
            directionGuide.enabled = fromHand.sqrMagnitude > .000001f;
            directionGuide.SetPosition(0,input.cursor.handRoot);
            directionGuide.SetPosition(1,input.cursor.handRoot+fromHand.normalized*Mathf.Min(.4f,fromHand.magnitude));
        }
        Vector3 offset = input.cursor.position-head.position;
        float distance = Mathf.Max(.001f,offset.magnitude);
        float renderDistance = distance <= 100 ? distance : 100+400*(1-1/(1+(distance-100)/400));
        Vector3 projected = head.position + offset*(renderDistance/distance);
        float t = Mathf.Clamp01(Mathf.Log(Mathf.Max(1,distance/4))/Mathf.Log(5));
        float diameter = .032f*(1+8.5f*t*t*(3-2*t))*Mathf.Sqrt(Mathf.Max(1,distance/20));
        if (coreProperties == null) coreProperties = new MaterialPropertyBlock();
        if (haloProperties == null) haloProperties = new MaterialPropertyBlock();
        float depthScale = distance/renderDistance;
        if (core != null)
        {
            core.enabled = true; core.transform.position = projected;
            core.transform.localScale = Vector3.one*(diameter/depthScale);
            coreProperties.SetColor("_Color",tint); coreProperties.SetFloat("_BirdDepthScale",depthScale); core.SetPropertyBlock(coreProperties);
        }
        if (halo != null)
        {
            float blend = 1-Mathf.Clamp01((diameter/distance-.0015f)/.003f);
            halo.enabled = distance > 4 && blend > 0;
            halo.startWidth = halo.endWidth = renderDistance*.0012f;
            halo.startColor = halo.endColor = new Color(tint.r,tint.g,tint.b,.85f*blend);
            haloProperties.SetFloat("_BirdDepthScale",depthScale); halo.SetPropertyBlock(haloProperties);
            for (int i=0;i<32;i++)
            {
                float angle=i*Mathf.PI*2/32;
                halo.SetPosition(i,projected+head.rotation*(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*renderDistance*.002625f));
            }
        }
        if (tipMarkers != null) for (int i=0;i<tipMarkers.Length && i<5;i++) if (tipMarkers[i] != null)
        { tipMarkers[i].gameObject.SetActive(true); tipMarkers[i].position=input.estimatedTips[i]; }
    }
    public void Clear()
    {
        if (core != null) core.enabled=false;
        if (halo != null) halo.enabled=false;
        if (directionGuide != null) directionGuide.enabled=false;
        if (tipMarkers != null) foreach(var marker in tipMarkers) if(marker!=null) marker.gameObject.SetActive(false);
    }
    private void OnDisable() { Clear(); }
}
