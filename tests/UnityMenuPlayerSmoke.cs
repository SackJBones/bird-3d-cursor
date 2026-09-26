using System;
using System.IO;
using Bird3DCursor.Samples;
using Bird3DCursor.UI;
using UnityEngine;

// Validation-project helper only. Without the command argument the player is interactive.
public sealed class UnityMenuPlayerSmoke : MonoBehaviour
{
    BirdMenuPreview preview;
    string resultPath;
    int frame;
    Camera ownedCamera;
    void Start()
    {
        var args=Environment.GetCommandLineArgs();
        int index=Array.IndexOf(args,"-birdMenuResult");
        if (index<0 || index+1>=args.Length) { enabled=false; return; }
        resultPath=args[index+1];
        try
        {
            preview=GetComponent<BirdMenuPreview>(); preview.desktopInput=false; preview.Initialize();
            Physics.SyncTransforms();
        }
        catch(Exception exception) { Fail(exception); }
    }
    void Update()
    {
        if (resultPath==null) return;
        try
        {
            switch(frame++)
            {
                case 0: Feed(new Vector3(0,.35f,-.2f)); break;
                case 1: Feed(new Vector3(0,1.05f,-.2f)); break;
                case 2:
                    Require(preview.Menu.State==BirdMenuPanel.PanelState.Open,"Directional open in player");
                    Aim(preview.ColorButton,false); break;
                case 3:
                    Require(preview.ColorButton.State==BirdMenuVisualState.Highlighted,"Player point-through highlight");
                    Aim(preview.ColorButton,true); break;
                case 4:
                    Require(preview.Colors.State==BirdMenuPanel.PanelState.Open,"Player nested action");
                    Aim(preview.CyanButton,false); break;
                case 5: Aim(preview.CyanButton,true); break;
                case 6:
                    Require(preview.SelectionCount==1 && preview.ChosenColor.b>.9f,"Player color action once");
                    Capture(); Aim(preview.BackButton,false); break;
                case 7: Aim(preview.BackButton,true); break;
                case 8:
                    Require(preview.Colors.State==BirdMenuPanel.PanelState.Closed && preview.Menu.State==BirdMenuPanel.PanelState.Open,"Player back restores parent");
                    preview.Pointer.Cancel(); break;
                case 9:
                    Require(preview.Menu.State==BirdMenuPanel.PanelState.Closed,"Player tracking loss clears focus");
                    foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        Require(!assembly.GetName().Name.StartsWith("UnityEditor") && assembly.GetName().Name!="Bird3D.Editor","Editor code loaded in player");
                    ownedCamera=preview.View; Destroy(preview); break;
                case 10:
                    Require(ownedCamera==null || !ownedCamera.gameObject.activeInHierarchy,"Removed preview leaves no active camera"); break;
                case 11:
                    Require(ownedCamera==null,"Removed preview cleans up generated objects");
                    File.WriteAllText(resultPath,"PASS: standalone player used normal LateUpdate routing to open, highlight, select color, return and cancel on tracking loss; preview removal cleaned up generated objects; camera captured; GPU="+SystemInfo.graphicsDeviceType+". Synthetic input, not GUI or headset input.");
                    enabled=false; Application.Quit(0); break;
            }
        }
        catch(Exception exception) { Fail(exception); }
    }
    void Fail(Exception exception)
    {
        File.WriteAllText(resultPath,"FAIL: "+exception); Debug.LogException(exception); enabled=false; Application.Quit(1);
    }
    void Feed(Vector3 point,bool pressed=false) { preview.Pointer.Submit(preview.View.transform.position,point,true,pressed); }
    void Aim(BirdMenuElement element,bool pressed)
    {
        var delta=element.transform.position-preview.View.transform.position;
        Feed(preview.View.transform.position+delta.normalized*(delta.magnitude+1),pressed);
    }
    static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
    void Capture()
    {
        foreach(var feedback in preview.GetComponentsInChildren<BirdMenuFeedback>()) feedback.Apply(1);
        var texture=new RenderTexture(1280,800,24) { antiAliasing=4 };
        var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        var previousTarget=preview.View.targetTexture;
        try
        {
            preview.View.targetTexture=texture; preview.View.Render(); RenderTexture.active=texture;
            pixels.ReadPixels(new Rect(0,0,1280,800),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(resultPath),"menu-player.png"),pixels.EncodeToPNG());
        }
        finally
        {
            preview.View.targetTexture=previousTarget; RenderTexture.active=previous;
            texture.Release(); Destroy(texture); Destroy(pixels);
        }
    }
}
