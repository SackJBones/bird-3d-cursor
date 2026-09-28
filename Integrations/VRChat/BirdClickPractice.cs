using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

// Read-only feedback for the existing click-depth law. Never writes cursor,
// pointer, interaction, permission or player state.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(200)]
public class BirdClickPractice : UdonSharpBehaviour
{
    public BirdPersonalStation station;
    public BirdAvatarHandInput[] inputs = new BirdAvatarHandInput[2];
    public BirdPointPresentation[] views = new BirdPointPresentation[2];
    public Text status;
    public Text[] readouts = new Text[2];
    public Image[] meters = new Image[2];
    public Image[] lights = new Image[2];
    public float practiceRadius = 3.5f;
    public bool automatic = true;
    [HideInInspector] public int sampledFrame = -1;
    [HideInInspector] public bool observing;
    [HideInInspector] public bool[] fresh = new bool[2];
    [HideInInspector] public bool[] armed = new bool[2];
    [HideInInspector] public bool[] pressed = new bool[2];
    [HideInInspector] public int[] presses = new int[2];
    [HideInInspector] public float[] depth = new float[2];
    private BirdAvatarHandInput[] boundInputs = new BirdAvatarHandInput[2];
    private BirdCursorState[] boundCursors = new BirdCursorState[2];
    private int[] histories = new int[2];
    private int[] acceptedFrames = new int[] { -1, -1 };
    private float lastTime = -1, nextText;

    public override void PostLateUpdate() { if (automatic) Process(); }
    public void Process()
    {
        if (!enabled || !gameObject.activeInHierarchy || sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;
        var player = Networking.LocalPlayer;
        float now = Time.realtimeSinceStartup;
        bool gap = lastTime >= 0 && (now < lastTime || now-lastTime > .25f);
        lastTime = now;
        observing = Utilities.IsValid(player) && station != null && station.acquired &&
            station.enabled && station.gameObject.activeInHierarchy && Finite(practiceRadius) && practiceRadius > 0 &&
            (player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position-transform.position).sqrMagnitude <= practiceRadius*practiceRadius;
        for (int i=0; i<2; i++)
        {
            var input = inputs.Length>i ? inputs[i] : null;
            var cursor = input == null ? null : input.cursor;
            int history = cursor == null ? 0 : cursor.historyRevision;
            bool changed = input!=boundInputs[i] || cursor!=boundCursors[i] || history!=histories[i];
            boundInputs[i]=input; boundCursors[i]=cursor; histories[i]=history;
            if (gap || changed || (acceptedFrames[i]>=0 && acceptedFrames[i]!=Time.frameCount-1)) ClearContact(i);
            bool valid = observing && input!=null && input.enabled && input.gameObject.activeInHierarchy &&
                input.dataReady && input.tipsReady && input.sampledFrame==Time.frameCount && cursor!=null &&
                cursor.enabled && cursor.gameObject.activeInHierarchy && cursor.tracking && cursor.poseValid &&
                cursor.clickAvailable && Finite(cursor.clickDepth);
            if (!valid)
            {
                ClearContact(i); depth[i]=0;
                if (!observing) presses[i]=0;
            }
            else
            {
                fresh[i]=true; acceptedFrames[i]=Time.frameCount; depth[i]=cursor.clickDepth;
                // Same 7/5 mm hysteresis as BirdCursorState. Release first on
                // entry/recovery: an already bent finger is never counted.
                if (depth[i]<.005f) { pressed[i]=false; armed[i]=true; }
                else if (armed[i] && !pressed[i] && depth[i]>.007f)
                { pressed[i]=true; if (presses[i]<int.MaxValue) presses[i]++; }
            }
            Color tint = views.Length>i && views[i]!=null ? views[i].tint : i==0 ? Color.cyan : Color.magenta;
            if (meters.Length>i && meters[i]!=null)
            {
                meters[i].fillAmount=fresh[i]?Mathf.Clamp01(depth[i]/.014f):0;
                meters[i].color=tint;
            }
            if (lights.Length>i && lights[i]!=null)
                lights[i].color=!fresh[i]?new Color(.25f,.3f,.32f):pressed[i]?Color.white:!armed[i]?new Color(1,.65f,.2f):tint;
        }
        if (now>=nextText) { nextText=now+.1f; RefreshText(); }
    }
    public void ResetPractice()
    {
        observing=false; lastTime=-1; nextText=0;
        for (int i=0;i<2;i++) { ClearContact(i); depth[i]=0; presses[i]=0; }
        RefreshText();
        for (int i=0;i<meters.Length;i++) if(meters[i]!=null) meters[i].fillAmount=0;
        for (int i=0;i<lights.Length;i++) if(lights[i]!=null) lights[i].color=new Color(.25f,.3f,.32f);
    }
    private void ClearContact(int i) { fresh[i]=armed[i]=pressed[i]=false; acceptedFrames[i]=-1; }
    private void RefreshText()
    {
        if (status!=null) status.text=observing ? "Lower and raise each index finger\nPractice only: nothing will move" : "Get a Bird, then come closer\nPractice only: nothing will move";
        for(int i=0;i<2;i++) if(readouts.Length>i && readouts[i]!=null)
        {
            string state=!fresh[i]?"SHOW HAND":!armed[i]?"RELEASE TO BEGIN":pressed[i]?"PRESS":"RELEASED";
            readouts[i].text=(i==0?"LEFT":"RIGHT")+"  "+state+"\n"+(depth[i]*1000).ToString("F1")+" mm   /   "+presses[i]+" taps";
        }
    }
    private void Start() { ResetPractice(); }
    private void OnDisable() { ResetPractice(); }
    private bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
}
