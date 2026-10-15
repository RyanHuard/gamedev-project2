using UnityEditor;
using UnityEngine;

namespace HalloweenDerby.EditorTools
{
    public static class CharacterAssetInspector
    {
        [MenuItem("Halloween Derby/Debug/Dump Batter Bone Names")]
        public static void DumpBatterBones()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Batter.prefab");
            if (go == null) { Debug.Log("Batter.prefab -> MISSING"); return; }
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                Debug.Log($"BONE: {t.name}");
        }

        [MenuItem("Halloween Derby/Debug/Screenshot Field Top-Down")]
        public static void ScreenshotFieldTopDown()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Derby.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

            var batter = GameObject.Find("Batter");
            if (batter != null)
            {
                // Bright arrow along the batter's local forward (+Z) axis so facing direction
                // is unambiguous from directly overhead, instead of inferring it from a
                // perspective shot of a blocky low-poly character.
                GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arrow.name = "DEBUG_FacingArrow";
                arrow.transform.SetParent(batter.transform, false);
                arrow.transform.localPosition = new Vector3(0f, 1f, 1f);
                arrow.transform.localScale = new Vector3(0.15f, 0.15f, 1.8f);
                arrow.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.red };
            }

            var camGO = new GameObject("TempTopCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = new Vector3(0f, 30f, 8f);
            camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.3f, 0.3f, 0.3f);

            var sun = new GameObject("TempTopLight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            const int size = 1000;
            var rt = new RenderTexture(size, size, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            string path = "/tmp/field_topdown.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(sun.gameObject);

            Debug.Log($"Screenshot saved to {path}");
        }

        [MenuItem("Halloween Derby/Debug/Screenshot Batter Cam View")]
        public static void ScreenshotBatterCamView()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Derby.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

            var batterCamGO = GameObject.Find("PitchZoneCamera");
            if (batterCamGO == null) { Debug.LogError("PitchZoneCamera not found in Derby.unity"); return; }
            var cmCam = batterCamGO.GetComponent<Unity.Cinemachine.CinemachineCamera>();

            var camGO = new GameObject("TempRenderCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = batterCamGO.transform.position;
            camGO.transform.rotation = batterCamGO.transform.rotation;
            cam.fieldOfView = cmCam != null ? cmCam.Lens.FieldOfView : 55f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.035f, 0.09f);

            const int w = 1200, h = 800;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            string path = "/tmp/batter_cam_view.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);

            Debug.Log($"Screenshot saved to {path}");
        }

        [MenuItem("Halloween Derby/Debug/Screenshot Batter")]
        public static void ScreenshotBatter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Batter.prefab");
            if (prefab == null) { Debug.LogError("Batter.prefab missing"); return; }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var lightGO = new GameObject("TempLight");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            lightGO.transform.rotation = Quaternion.Euler(40f, -40f, 0f);

            var bgGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            bgGO.transform.position = Vector3.zero;
            bgGO.transform.localScale = Vector3.one * 2f;
            bgGO.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.3f, 0.3f, 0.3f) };

            var camGO = new GameObject("TempCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = new Vector3(1.6f, 1.1f, 1.6f);
            camGO.transform.LookAt(new Vector3(0f, 0.9f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
            cam.fieldOfView = 45f;

            const int size = 900;
            var rt = new RenderTexture(size, size, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            string path = "/tmp/batter_screenshot.png";
            System.IO.File.WriteAllBytes(path, png);

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(lightGO);
            Object.DestroyImmediate(bgGO);
            Object.DestroyImmediate(instance);

            Debug.Log($"Screenshot saved to {path}");
        }

        [MenuItem("Halloween Derby/Debug/Find Objects Near Camera")]
        public static void FindObjectsNearCamera()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Derby.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var cam = GameObject.Find("PitchZoneCamera");
            if (cam == null) { Debug.LogError("PitchZoneCamera missing"); return; }
            Vector3 camPos = cam.transform.position;

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var withDist = new System.Collections.Generic.List<(float dist, Renderer r)>();
            foreach (var r in renderers)
                withDist.Add((Vector3.Distance(r.bounds.center, camPos), r));
            withDist.Sort((a, b) => a.dist.CompareTo(b.dist));

            Debug.Log($"Camera at {camPos}. Closest renderers:");
            for (int i = 0; i < Mathf.Min(15, withDist.Count); i++)
            {
                var (dist, r) = withDist[i];
                Debug.Log($"  dist={dist:0.00} name={r.gameObject.name} path={GetPath(r.transform)} boundsCenter={r.bounds.center} boundsSize={r.bounds.size}");
            }
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        [MenuItem("Halloween Derby/Debug/Screenshot Outfield Wall")]
        public static void ScreenshotOutfieldWall()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Derby.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

            float radius = HalloweenDerby.Core.DerbyConstants.HomeRunDistanceUnits;
            var camGO = new GameObject("TempWallCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = new Vector3(0f, 1.0f, radius - 14f);
            camGO.transform.LookAt(new Vector3(0f, 1.0f, radius));
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.035f, 0.09f);

            var sun = new GameObject("TempWallLight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40f, -40f, 0f);

            const int w = 1200, h = 800;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            string path = "/tmp/outfield_wall.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(sun.gameObject);

            Debug.Log($"Screenshot saved to {path}");
        }

        [MenuItem("Halloween Derby/Debug/Dump Ballpark Dressing")]
        public static void DumpBallparkDressing()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Derby.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var dressing = GameObject.Find("BallparkDressing");
            if (dressing == null) { Debug.Log("BallparkDressing -> MISSING"); return; }

            var renderers = dressing.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"BallparkDressing has {renderers.Length} renderers, {dressing.transform.childCount} direct children.");
            var byPrefix = new System.Collections.Generic.Dictionary<string, int>();
            Bounds? overall = null;
            foreach (var r in renderers)
            {
                string prefix = r.gameObject.name.Split('(')[0].Trim();
                byPrefix[prefix] = (byPrefix.ContainsKey(prefix) ? byPrefix[prefix] : 0) + 1;
                if (overall == null) overall = r.bounds; else overall.Value.Encapsulate(r.bounds);
            }
            foreach (var kv in byPrefix)
                Debug.Log($"  {kv.Key} x{kv.Value}");
            if (overall.HasValue)
                Debug.Log($"Overall bounds: center={overall.Value.center} size={overall.Value.size} min={overall.Value.min} max={overall.Value.max}");

            var zoneCam = GameObject.Find("PitchZoneCamera");
            if (zoneCam != null)
            {
                var cam = zoneCam.GetComponent<Unity.Cinemachine.CinemachineCamera>();
                Debug.Log($"PitchZoneCamera pos={zoneCam.transform.position} farClip={Camera.main?.farClipPlane}");
            }
            Debug.Log($"RenderSettings.fogDensity={RenderSettings.fogDensity} fogMode={RenderSettings.fogMode}");
        }

        [MenuItem("Halloween Derby/Debug/Dump Pine Tree Size")]
        public static void DumpPineTreeSize()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Derby.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var dressing = GameObject.Find("BallparkDressing");
            foreach (Transform child in dressing.transform)
            {
                if (!child.name.StartsWith("pine")) continue;
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) { Debug.Log($"{child.name} at {child.position} -> NO RENDERERS"); continue; }
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                Debug.Log($"{child.name} pos={child.position} localScale={child.localScale} boundsSize={b.size}");
                break; // just need one sample
            }
        }

        [MenuItem("Halloween Derby/Debug/Screenshot Ball Prefab")]
        public static void ScreenshotBallPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PumpkinBall.prefab");
            if (prefab == null) { Debug.LogError("PumpkinBall.prefab missing"); return; }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var lightGO = new GameObject("TempLight");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGO.transform.rotation = Quaternion.Euler(40f, -40f, 0f);

            var camGO = new GameObject("TempBallCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = new Vector3(0.8f, 0.5f, -0.8f);
            camGO.transform.LookAt(Vector3.zero);
            cam.fieldOfView = 35f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.05f, 0.14f);

            const int size = 700;
            var rt = new RenderTexture(size, size, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            string path = "/tmp/ball_prefab.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(lightGO);
            Object.DestroyImmediate(instance);

            Debug.Log($"Screenshot saved to {path}");
        }

        [MenuItem("Halloween Derby/Debug/Dump Scene Key Transforms")]
        public static void DumpSceneKeyTransforms()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Derby.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            string[] names = { "PitchZoneCamera", "PitcherReleasePoint", "HomePlate", "StrikeZone", "Batter", "SkeletonPitcher", "PitchersMound", "InfieldDirt" };
            foreach (var n in names)
            {
                var go = GameObject.Find(n);
                if (go == null) { Debug.Log($"{n} -> MISSING"); continue; }
                Debug.Log($"{n} -> pos={go.transform.position} rot={go.transform.rotation.eulerAngles} scale={go.transform.lossyScale}");
            }

            var zone = Object.FindAnyObjectByType<HalloweenDerby.Player.StrikeZone>();
            if (zone != null)
                Debug.Log($"StrikeZone.WorldCenter={zone.WorldCenter} HalfWidth={zone.HalfWidth} HalfHeight={zone.HalfHeight}");

            var cam = GameObject.Find("PitchZoneCamera");
            if (cam != null)
            {
                var cmCam = cam.GetComponent<Unity.Cinemachine.CinemachineCamera>();
                Debug.Log($"PitchZoneCamera FOV={cmCam?.Lens.FieldOfView}");
            }
        }

        [MenuItem("Halloween Derby/Debug/Dump Batter Rig Transforms")]
        public static void DumpBatterRig()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Batter.prefab");
            if (go == null) { Debug.Log("Batter.prefab -> MISSING"); return; }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(go);
            try
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    Debug.Log($"Batter world bounds -> size={b.size} center={b.center} min={b.min} max={b.max}");
                }

                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name is "arm-right" or "arm-left" or "torso" or "Bat" or "BatBarrel" or "Batter")
                        Debug.Log($"RIG: {t.name} worldPos={t.position} localPos={t.localPosition} localRot={t.localRotation.eulerAngles} lossyScale={t.lossyScale}");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [MenuItem("Halloween Derby/Debug/Inspect Prefab Bounds")]
        public static void InspectBounds()
        {
            string[] paths = {
                "Assets/Prefabs/SkeletonPitcher.prefab",
                "Assets/Prefabs/Batter.prefab",
                "Assets/Prefabs/GhostOutfielder.prefab",
                "Assets/Prefabs/HeadlessHorseman.prefab",
            };
            foreach (var p in paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) { Debug.Log($"{p} -> MISSING"); continue; }
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) { Debug.Log($"{p} -> no renderers"); continue; }
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                Debug.Log($"{p} -> size={b.size} center={b.center}");
            }
        }
    }
}
