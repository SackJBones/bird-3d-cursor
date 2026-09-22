using System;
using System.IO;
using Bird3DCursor;
using UnityEngine;

// Copied only into generated player-validation projects, never into the package.
public sealed class UnityPlayerSmoke : MonoBehaviour
{
    private const BirdHandAPI SmokeBackend = (BirdHandAPI)123456;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSyntheticBackend()
    {
        HandFactory.RegisterBackend(SmokeBackend, side => new UntrackedHand(side));
    }

    private void Start()
    {
        string resultPath = null;
        int exitCode = 1;
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-birdSmokeResult");
            if (index < 0 || index + 1 >= args.Length) throw new Exception("Missing result path");
            resultPath = args[index + 1];
            var bird = new Bird(HandFactory.CreateHand(Hand.Chirality.Right, SmokeBackend));
            bird.Update();
            if (bird.GetClick() || bird.GetClickDown() || bird.GetClickUp()) throw new Exception("Untracked Bird must be idle");
            if (bird.GetPosition() != Vector3.zero) throw new Exception("Unexpected initial cursor position");
            if (typeof(Bird).Assembly.GetName().Name != "Bird3D.Runtime") throw new Exception("Missing package runtime assembly");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name.StartsWith("UnityEditor") || assembly.GetName().Name == "Bird3D.Editor")
                    throw new Exception("Editor assembly loaded in player");
            File.WriteAllText(resultPath, "PASS: packaged Bird backend registration and startup run without editor assemblies; Unity " + Application.unityVersion);
            exitCode = 0;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (resultPath != null) File.WriteAllText(resultPath, "FAIL: " + exception);
        }
        Application.Quit(exitCode);
    }

    private sealed class UntrackedHand : Hand
    {
        public UntrackedHand(Chirality side) : base(side) { }
        public override bool IsTracking() { return false; }
        public override Vector3 GetBasePosition(Finger finger) { throw new Exception("Untracked joint read"); }
        public override Vector3 GetIntermediatePosition(Finger finger) { throw new Exception("Untracked joint read"); }
        public override Vector3 GetDistalPosition(Finger finger) { throw new Exception("Untracked joint read"); }
        public override Vector3 GetTipPosition(Finger finger) { throw new Exception("Untracked joint read"); }
    }
}
