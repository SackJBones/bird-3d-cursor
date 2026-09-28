#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Actual CharacterController.Move on normal Play Mode frames against the saved
// world's colliders. This is a geometry test, not VRChat client locomotion or VR comfort.
public class UnityCoastalWalkChecks : MonoBehaviour
{
    const string Active="Bird.Coastal.Walk";
    const string Folder="../Validation/CoastalWorld";
    [Serializable] public class Route { public string name; public Vector3[] points; }
    [Serializable] public class Routes { public Route[] routes; }
    CharacterController controller;
    IEnumerator sequence;
    float deadline;
    int frames;
    string lastCollision="";
    readonly List<string> rows=new List<string>{"route,frames,end_error_m,status"};
    public static void Run()
    {
        if(!File.Exists(Folder+"/walk-routes.json"))throw new Exception("Run the coastal scene check first.");
        File.WriteAllText("coastal-walk-result.txt","PENDING");
        EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdCoastalWorld.unity");
        SessionState.SetBool(Active,true);EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin(){if(SessionState.GetBool(Active,false))new GameObject("Coastal walking probe").AddComponent<UnityCoastalWalkChecks>();}
    void Start(){deadline=Time.unscaledTime+420;}
    void Update()
    {
        if(!SessionState.GetBool(Active,false))return;
        try
        {
            if(Time.unscaledTime>deadline)throw new Exception("Walkthrough timed out");
            if(Time.timeSinceLevelLoad<3)return;
            if(sequence==null)sequence=Walk();
            frames++;if(!sequence.MoveNext())Finish(true,"Standing CharacterController traversed seven saved-collider routes out and back in "+frames+" normal frames. No jump, teleport between corners or at turnaround, or collision bypass. Single-player geometry test, not headset/client locomotion.");
        }
        catch(Exception e){Finish(false,e.ToString()+"; last collision="+lastCollision);}
    }
    IEnumerator Walk()
    {
        // ClientSim's idle avatar occupies spawn. Test the actual world geometry
        // independently of player/player collision (not a multiplayer test).
        foreach(var c in FindObjectsOfType<CharacterController>())
        {var driver=c.GetComponentInParent<VRC.SDK3.ClientSim.ClientSimPlayerController>();if(driver!=null)driver.enabled=false;c.enabled=false;}
        controller=gameObject.AddComponent<CharacterController>();controller.height=1.75f;controller.radius=.25f;
        controller.center=Vector3.up*.875f;controller.stepOffset=.24f;controller.slopeLimit=40;controller.skinWidth=.025f;controller.minMoveDistance=0;
        var routes=JsonUtility.FromJson<Routes>(File.ReadAllText(Folder+"/walk-routes.json"));
        if(routes.routes.Length!=7)throw new Exception("Expected all seven complete navigation routes");
        foreach(var route in routes.routes)
        {
            controller.enabled=false;transform.position=route.points[0]+Vector3.up*.03f;controller.enabled=true;Physics.SyncTransforms();
            // Return from the actual endpoint without resetting the controller.
            // Descending stairs and approaching doorways from inside are distinct
            // collision cases from the outward route.
            for(int direction=0;direction<2;direction++)
            {
                int before=frames;lastCollision="";
                for(int step=1;step<route.points.Length;step++)
                {
                    int corner=direction==0?step:route.points.Length-1-step;
                    int stalled=0;
                    while(true)
                    {
                        Vector3 delta=route.points[corner]-transform.position;delta.y=0;
                        if(delta.magnitude<.09f)break;
                        Vector3 prior=transform.position;
                        controller.Move(Vector3.ClampMagnitude(delta,.06f)+Vector3.down*.12f);
                        Vector3 progress=transform.position-prior;progress.y=0;
                        stalled=progress.magnitude<.001f?stalled+1:0;
                        if(stalled>90 || frames-before>6000 || transform.position.y< -5)
                            throw new Exception(route.name+" blocked near "+transform.position+" heading to corner "+corner+" "+route.points[corner]);
                        yield return null;
                    }
                }
                for(int settle=0;settle<8;settle++){controller.Move(Vector3.down*.12f);yield return null;}
                float error=Vector3.Distance(transform.position,route.points[direction==0?route.points.Length-1:0]);
                if(error>.35f)throw new Exception(route.name+" finished at wrong level, endpoint error="+error);
                rows.Add(route.name+(direction==0?" outward":" return")+","+(frames-before)+","+error.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+",complete");
                File.WriteAllLines(Folder+"/walkthrough.csv",rows);
            }
        }
    }
    void OnControllerColliderHit(ControllerColliderHit hit){if(hit.normal.y<.8f)lastCollision=hit.collider.name+" normal="+hit.normal;}
    void Finish(bool success,string result)
    {
        SessionState.SetBool(Active,false);File.WriteAllLines(Folder+"/walkthrough.csv",rows);
        File.WriteAllText("coastal-walk-result.txt",(success?"PASS: ":"FAIL: ")+result);
        EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(success?0:1);
    }
}
#endif
