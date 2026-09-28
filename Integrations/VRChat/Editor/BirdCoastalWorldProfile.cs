#if UNITY_EDITOR
using UnityEngine;

// Editor asset only. The exported world contains ordinary saved meshes and prefabs.
[CreateAssetMenu(menuName="Bird/Coastal world mesh profile")]
public class BirdCoastalWorldProfile : ScriptableObject
{
    [Header("Updates five shared meshes only; check stairs, rails and anchors after resizing")]
    [Range(24,128)] public int silhouetteSegments=64;
    public float arrivalHalfWidth=15, arrivalLength=22.8f, vaultSpring=4, vaultHeight=9;
    public float thresholdRadius=6, thresholdCenterHeight=6, thresholdWallHeight=14;
    [Tooltip("Column radius and horizontal offset along its height. x = offset, y = height, z = radius.")]
    public Vector3[] supportProfile=new[]{new Vector3(0,0,2.8f),new Vector3(-.2f,2,2),new Vector3(-.3f,5,1.65f),new Vector3(0,6.8f,2.5f),new Vector3(1,8.8f,8),new Vector3(1.5f,12,9)};
    public Vector2 upperDeckRadii=new Vector2(19,19), lookoutRadii=new Vector2(12,10);
    [Header("Initial materials (edit saved material assets after creation)")]
    public Color plaster=Color.white, stone=new Color(.67f,.66f,.59f), wood=new Color(.53f,.33f,.17f);
    public Color sea=new Color(.10f,.40f,.53f), foliage=new Color(.23f,.35f,.22f);
}
#endif
