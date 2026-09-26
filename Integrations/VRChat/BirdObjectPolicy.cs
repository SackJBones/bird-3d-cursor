using UdonSharp;
using UnityEngine;
using VRC.Udon;

public enum BirdObjectOperation { CanGrab, Begin, CanPlace, Commit, Cancel }

// Synchronous local event bridge instead of unsupported custom MonoBehaviour inheritance.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdObjectPolicy : UdonSharpBehaviour
{
    public UdonBehaviour evaluator;
    public string evaluationEvent="EvaluatePlacement";
    [HideInInspector] public BirdObjectOperation operation;
    [HideInInspector] public BirdObjectTarget item;
    [HideInInspector] public BirdObjectSnapTarget destination;
    [HideInInspector] public bool allowed;
    private bool busy;
    public bool Query(BirdObjectOperation request,BirdObjectTarget target,BirdObjectSnapTarget slot)
    {
        if(busy || evaluator==null || string.IsNullOrEmpty(evaluationEvent)) return false;
        if(request!=BirdObjectOperation.Cancel && (!enabled || !gameObject.activeInHierarchy || !evaluator.enabled || !evaluator.gameObject.activeInHierarchy)) return false;
        busy=true; operation=request; item=target; destination=slot; allowed=false;
        evaluator.SendCustomEvent(evaluationEvent);
        busy=false; return allowed;
    }
}
