using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Bird3DCursor;
using UnityEditor;
using UnityEngine;

// Generated-project-only integration checks. Never shipped with the sample or package.
public sealed class UnityPreviewPlayChecks : MonoBehaviour
{
    private int checks;
    private string runtimeError;
    private BirdDesktopPreview preview;

    private void OnEnable() { Application.logMessageReceived += CaptureError; }
    private void OnDisable() { Application.logMessageReceived -= CaptureError; }
    private void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            runtimeError = message + "\n" + stack;
    }

    private IEnumerator Start()
    {
        var run = RunChecks();
        while (true)
        {
            bool next;
            try
            {
                if (runtimeError != null) throw new Exception(runtimeError);
                next = run.MoveNext();
            }
            catch (Exception e)
            {
                File.WriteAllText("core-checks-result.txt", "FAIL: preview Play Mode: " + e);
                EditorApplication.Exit(1);
                yield break;
            }
            if (!next) break;
            yield return run.Current;
        }
        File.WriteAllText("core-checks-result.txt", "PASS: " + checks +
            " desktop preview Play Mode checks; Unity " + Application.unityVersion + "; no rendering or hardware validation");
        EditorApplication.Exit(0);
    }

    private T Read<T>(string field)
    {
        return (T)typeof(BirdDesktopPreview).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
    }
    private void Set(string field, object value)
    {
        typeof(BirdDesktopPreview).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(preview, value);
    }
    private void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private IEnumerator RunChecks()
    {
        preview = FindObjectOfType<BirdDesktopPreview>();
        Check(preview != null, "Sample must enter Play Mode");
        Set("animate", false);
        for (int i = 0; i < 30; i++) yield return null;
        var birds = Read<Bird[]>("birds");
        var cursors = Read<Transform[]>("cursors");
        var joints = Read<Transform[,]>("joints");
        var rays = Read<LineRenderer[]>("rays");
        var tips = Read<Transform[]>("tips");
        var camera = preview.GetComponentInChildren<Camera>();
        Check(camera != null, "Sample must create its camera");
        Check(preview.GetComponentsInChildren<Collider>().Length == 0, "Visual markers must not retain physics colliders");
        float nearRange = birds[0].GetRange();
        for (int h = 0; h < 2; h++)
        {
            Check(Mathf.Abs(birds[h].GetSphereFitRadius() - 0.035f) < 0.0001f, "Known synthetic sphere must be fitted");
            Check(!birds[h].GetClick(), "Preview must start released");
            Check(Vector3.Distance(cursors[h].position, birds[h].GetPosition()) < 0.00001f, "Visual cursor must follow solver");
            Check(Vector3.Distance(rays[h].GetPosition(1), cursors[h].position) < 0.00001f, "Ray must end at visual cursor");
        }
        Check(Vector3.Distance(cursors[0].position, cursors[1].position) > 0.1f, "Two hands must produce distinct positions");
        Set("radius", 0.045f);
        for (int i = 0; i < 120; i++) yield return null;
        Check(birds[0].GetRange() > nearRange + 0.08f, "Opening synthetic hand must extend cursor range");
        Set("pressed", true);
        for (int i = 0; i < 3; i++) yield return null;
        Check(birds[0].GetClick() && birds[1].GetClick(), "Index penetration must select both cursors");
        Check(Read<int>("presses") == 2, "Held selection must produce only one press per hand");
        Check(cursors[0].localScale.x > 0.02f, "Selected cursor must enlarge");
        Vector3 leftPosition = cursors[0].position, rightPosition = cursors[1].position;
        Set("tracking", false);
        for (int i = 0; i < 4; i++) yield return null;
        Check(!birds[0].GetClick() && !birds[1].GetClick(), "Pose loss must release both hands");
        Check(Read<int>("releases") == 2, "Continued pose loss must release exactly once per hand");
        Check(cursors[0].position == leftPosition && cursors[1].position == rightPosition, "Pose loss must hold both visual cursors");
        for (int h = 0; h < 2; h++)
        {
            Check(!rays[h].enabled && !tips[h].gameObject.activeSelf, "Pose loss must hide ray and index marker");
            for (int j = 0; j < 16; j++) Check(!joints[h, j].gameObject.activeSelf, "Pose loss must hide every fitting point");
        }
        Set("pressed", false);
        Set("tracking", true);
        for (int i = 0; i < 4; i++) yield return null;
        Check(Read<int>("presses") == 2 && Read<int>("releases") == 2, "Unpressed recovery must not add click edges");
        Check(rays[0].enabled && tips[0].gameObject.activeSelf && joints[1, 15].gameObject.activeSelf, "Recovery must restore input visuals");
        var materials = Read<Material[]>("materials");
        var jointMaterial = Read<Material>("jointMaterial");
        var owner = preview.gameObject;
        var authoredChild = new GameObject("Unrelated authored child");
        authoredChild.transform.SetParent(owner.transform);
        Destroy(preview);
        for (int i = 0; i < 3; i++) yield return null;
        Check(camera == null && owner.GetComponentsInChildren<Renderer>().Length == 0,
            "Removing the preview component must remove its generated camera and visuals");
        Check(materials[0] == null && materials[1] == null && jointMaterial == null, "Removing preview must release generated materials");
        Check(authoredChild != null && owner.transform.childCount == 1, "Cleanup must preserve unrelated authored children");
    }
}
