#if UNITY_EDITOR
using UnityEngine;

[CreateAssetMenu(menuName = "Bird/Coastal pond profile")]
public class BirdCoastalPondProfile : ScriptableObject
{
    [Tooltip("Plan coordinates in metres. End tangents meet the existing inland promenade. Mesh update does not relocate beacons or stairs.")]
    public Vector2[] centerline = {
        new Vector2(20f,48f),
        new Vector2(19.4f,53.6f),
        new Vector2(16.3f,58.3f),
        new Vector2(11.6f,61.4f),
        new Vector2(6f,62f),
        new Vector2(0.4f,61.4f),
        new Vector2(-4.3f,58.3f),
        new Vector2(-7.4f,53.6f),
        new Vector2(-8f,48f)
    };
    [Min(1)] public float waterHalfWidth = 2;
    [Min(3)] public float walkWidth = 3.3f;
    [Range(6,24)] public int samplesPerSpan = 12;
    public float floorHeight = -2;
    public float waterDepth = .6f;
}
#endif
