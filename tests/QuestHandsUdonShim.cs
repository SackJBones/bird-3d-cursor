// Validation project ONLY. The runner copies unchanged production Udon math
// alongside this shim so Quest can compare it with Bird.cs on identical joints.
// This executes ordinary C#, not the VRChat Udon VM or avatar adapter.
namespace UdonSharp
{
    public class UdonSharpBehaviour : UnityEngine.MonoBehaviour { }
    public enum BehaviourSyncMode { None }
    public sealed class UdonBehaviourSyncModeAttribute : System.Attribute
    {
        public UdonBehaviourSyncModeAttribute(BehaviourSyncMode mode) { }
    }
}
