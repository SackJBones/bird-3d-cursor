#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public partial class UnityCoastalWorldChecks
{
    public static void AddGarden()
    {
        try { BirdCoastalGardenAuthoring.Add(); Finish("coastal-garden-author", true, "Authored editable pond conversation corner with two seats and three unequal planting groups. No runtime or existing geometry changes. Rebake required."); }
        catch (Exception e) { Finish("coastal-garden-author", false, e.ToString()); }
    }

    public static void RefineGarden()
    {
        try { BirdCoastalGardenAuthoring.TightenFirstSeating(); Finish("coastal-garden-refine", true, "Moved the shorter seat and its planter one metre inward; rebake and route checks required."); }
        catch (Exception e) { Finish("coastal-garden-refine", false, e.ToString()); }
    }

    public static void CheckGarden()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath); Physics.SyncTransforms();
            string folder = Folder + "/Garden01/" + EditorUserBuildSettings.activeBuildTarget; Directory.CreateDirectory(folder);
            var garden = GameObject.Find("04 Lower water and hidden lounge/Pond garden corner");
            Require(garden != null && PrefabUtility.IsAnyPrefabInstanceRoot(garden), "Saved nested garden prefab");
            Require(garden.GetComponentsInChildren<MonoBehaviour>().Length == 0, "No garden runtime scripts");
            Require(garden.GetComponentsInChildren<Light>().Length == 0, "No garden lights");
            const int solid = (1 << 0) | (1 << 2) | (1 << 11);
            int supports = 0, clear = 0;
            var pondFloor = GameObject.Find("04 Lower water and hidden lounge/Curved pond promenade/Continuous walk and terrace").GetComponent<MeshCollider>();
            foreach (var collider in garden.GetComponentsInChildren<MeshCollider>())
            {
                Require(!collider.isTrigger && collider.gameObject.layer == 0, "Simple solid furniture collision");
                foreach (var vertex in collider.sharedMesh.vertices)
                {
                    var p = collider.transform.TransformPoint(vertex); p.y = -1.9f;
                    Require(pondFloor.Raycast(new Ray(p, Vector3.down), out var hit, .2f) && hit.normal.y > .98f, "Furniture footprint has real terrace below: " + collider.name + " " + p); supports++;
                }
            }
            Action<Vector3> supported = p =>
            {
                Require(Physics.Raycast(p + Vector3.up * .1f, Vector3.down, out var hit, .25f, solid, QueryTriggerInteraction.Ignore) && hit.normal.y > .98f, "Garden approach floor");
                foreach (float height in new[] { 1.2f, 1.75f, 2.1f })
                    Require(!Physics.CheckCapsule(p + Vector3.up * .3f, p + Vector3.up * (height - .25f), .25f, solid, QueryTriggerInteraction.Ignore), "Garden approach head/body clearance");
                clear++;
            };
            var routes = new[] {
                new UnityCoastalWalkChecks.Route { name = "Garden from eastern stair", points = new[] { new Vector3(14,-2,44), new Vector3(14,-2,52), new Vector3(5.1f,-2,54), new Vector3(5.1f,-2,57.8f), new Vector3(2.9f,-2,58) } },
                new UnityCoastalWalkChecks.Route { name = "Garden from western promenade", points = new[] { new Vector3(0,-2,44), new Vector3(0,-2,52), new Vector3(5.1f,-2,54), new Vector3(5.1f,-2,57.8f), new Vector3(7.1f,-2,58) } }
            };
            foreach (var route in routes) for (int i = 1; i < route.points.Length; i++)
            {
                int count = Mathf.CeilToInt(Vector3.Distance(route.points[i - 1], route.points[i]) / .2f);
                for (int j = 0; j <= count; j++) supported(Vector3.Lerp(route.points[i - 1], route.points[i], (float)j / count) + Vector3.up * .035f);
            }
            foreach (var seat in garden.transform.Cast<Transform>().Where(t => t.name.Contains("seat")))
            {
                Vector3 top = seat.TransformPoint(new Vector3(0, .5f, .1f));
                Require(Physics.Raycast(top, Vector3.down, out var hit, .1f, solid) && hit.normal.y > .98f && Mathf.Abs(hit.point.y + 1.55f) < .01f, "Flat seat at 45 cm");
                Require(!Physics.CheckCapsule(hit.point + Vector3.up * .28f, hit.point + Vector3.up * 1.05f, .2f, solid), "Seated torso/head clearance");
            }
            var eye = new Vector3(2.9f, -.85f, 57.18f); int visible = 0;
            for (float x = 0; x <= 10; x += 1) for (float z = 61; z <= 63; z += .5f)
            {
                var target = new Vector3(x, -2.75f, z);
                // The invisible player fences are not visual occluders. Retain
                // walls, slabs and exact visible rail geometry for this query.
                if (!Physics.Linecast(eye, target, (1 << 0) | (1 << 11) | (1 << 17), QueryTriggerInteraction.Ignore)) visible++;
            }
            Require(visible >= 8, "Seated view reaches water through/over the real bank and rails: " + visible);
            long triangles = garden.GetComponentsInChildren<MeshFilter>().Sum(f => (long)f.sharedMesh.triangles.Length / 3);
            Require(triangles < 2500, "Bounded garden geometry");
            foreach (var renderer in garden.GetComponentsInChildren<MeshRenderer>())
            {
                Require(renderer.sharedMaterial != null && renderer.sharedMaterial.renderQueue < 2500, "Opaque garden materials");
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Require(mesh.vertices.All(v => !float.IsNaN(v.sqrMagnitude) && !float.IsInfinity(v.sqrMagnitude)), "Finite saved garden geometry");
                if (GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.ContributeGI)) Require(mesh.uv2.Length == mesh.vertexCount, "New baked receiver has authored UV2");
            }
            File.WriteAllText(Folder + "/garden-walk-routes.json", JsonUtility.ToJson(new UnityCoastalWalkChecks.Routes { routes = routes }, true));
            PondCapture(folder, "01-east-approach", new Vector3(14, -.35f, 48), new Vector3(4.5f, -1.3f, 57), 75);
            PondCapture(folder, "02-west-approach", new Vector3(-7, -.35f, 48), new Vector3(5.5f, -1.1f, 57), 75);
            PondCapture(folder, "03-conversation-corner", new Vector3(6, -.35f, 59), new Vector3(5.5f, -1.25f, 54.5f), 85);
            PondCapture(folder, "04-seated-water", eye, new Vector3(5, -2.7f, 62), 78);
            PondCapture(folder, "05-seated-companion", new Vector3(7.1f, -.85f, 56.9f), new Vector3(2.9f, -.7f, 57), 80);
            // Free overview camera, not a supported visitor stance. The earlier
            // high inland view was occluded by an upper floor.
            PondCapture(folder, "06-seaward-overview", new Vector3(11, 9, 69), new Vector3(4.5f, -2, 55.5f), 70);
            PondCapture(folder, "07-garden-plan", new Vector3(4.5f, 18, 55), new Vector3(4.5f, -2, 55.01f), 70);
            File.WriteAllText(folder + "/inventory.txt", supports + " supported furniture vertices; " + clear + " route samples at three heights; " + visible + " seated-water sightlines; " + triangles + " instance triangles; no new runtime/lights/transparency. Seating geometry only, no VRCStation interaction. Physical comfort and client locomotion remain separate.");
            Finish("coastal-garden-check", true, supports + " supported furniture vertices, " + clear + " approach samples at three heights, " + visible + " seated-water sightlines, " + triangles + " triangles and seven renders. Normal-frame traversal remains separate.");
        }
        catch (Exception e) { Finish("coastal-garden-check", false, e.ToString()); }
    }
}
#endif
