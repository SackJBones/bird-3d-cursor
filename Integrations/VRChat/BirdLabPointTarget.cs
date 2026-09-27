using UdonSharp;
using UnityEngine;

// A point-through exercise. No click synthesis and no projected visual coordinates.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(200)]
public class BirdLabPointTarget : UdonSharpBehaviour
{
    public BirdAvatarHandInput left, right;
    public Renderer artwork;
    public float radius=.12f;
    [HideInInspector] public bool highlighted;
    private MaterialPropertyBlock properties;
    public override void PostLateUpdate()
    {
        highlighted = Hit(left) || Hit(right);
        if (artwork == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        properties.SetColor("_Color",highlighted ? Color.green : new Color(.4f,.4f,.4f));
        artwork.SetPropertyBlock(properties);
    }
    private bool Hit(BirdAvatarHandInput input)
    {
        if(input==null || !input.enabled || !input.gameObject.activeInHierarchy || !input.calibrated ||
            input.sampledFrame!=Time.frameCount || input.cursor==null || !input.cursor.poseValid || radius<=0) return false;
        Vector3 origin=input.cursor.handRoot, delta=input.cursor.position-origin;
        float length=delta.magnitude;
        if(length<.000001f) return false;
        Vector3 direction=delta/length;
        float along=Vector3.Dot(transform.position-origin,direction);
        return along>=0 && along<=length && (origin+direction*along-transform.position).sqrMagnitude<=radius*radius;
    }
    private void OnDisable() { highlighted=false; if(artwork!=null) artwork.SetPropertyBlock(null); }
}
