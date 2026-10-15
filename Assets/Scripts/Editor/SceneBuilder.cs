using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Cinemachine;
using HalloweenDerby.Core;
using HalloweenDerby.Pitching;
using HalloweenDerby.Player;
using HalloweenDerby.Ball;
using HalloweenDerby.CameraSystem;
using HalloweenDerby.Audio;
using HalloweenDerby.UI;

namespace HalloweenDerby.EditorTools
{
    /// Builds the Derby scene and supporting data assets entirely from code, since this
    /// project is assembled through a CLI-only pipeline with no interactive Editor session.
    /// Primitive stand-ins now; swapped for sourced CC0 models/animators in a later pass.
    public static class SceneBuilder
    {
        private const string PitchDataDir = "Assets/Data/Pitches";
        private const string RoundDataDir = "Assets/Data/Rounds";
        private const string PrefabDir = "Assets/Prefabs";
        private const string ScenePath = "Assets/Scenes/Derby.unity";

        [MenuItem("Halloween Derby/Build Derby Scene")]
        public static void BuildDerbyScene()
        {
            Directory.CreateDirectory(PitchDataDir);
            Directory.CreateDirectory(RoundDataDir);
            Directory.CreateDirectory(PrefabDir);

            // Windup duration is the same across all three pitch types on purpose, matching real
            // baseball: the deception comes from an identical-looking windup, not a different
            // one - you read the pitch from its actual flight (speed/break), not a tell before
            // release. Flight duration is where they actually differ.
            const float sharedWindup = 0.7f;
            var fastball = CreatePitchType("Fastball", windup: sharedWindup, flight: 0.45f, perfect: 0.045f, good: 0.09f, weak: 0.15f, lateralBreak: 0f, extraDrop: 0f);
            // LateralBreak/ExtraDrop are applied as a permanent offset from the pitch's actual
            // target (BallFlightController.TickPitchFlight), not a detour that returns to it -
            // the old values (1.1 lateral, 0.3 drop) were roughly 3x and 0.6x the strike zone's
            // own half-width/half-height (StrikeZone: HalfWidth=0.375, HalfHeight=0.475), so a
            // curveball's real landing spot was essentially never within reach of the zone the
            // scoring system thought it was aimed at. Scaled down to a fraction of the zone
            // dimensions instead - still a clearly readable break, but one that can actually
            // land as a strike.
            var curveball = CreatePitchType("Curveball", windup: sharedWindup, flight: 0.7f, perfect: 0.045f, good: 0.09f, weak: 0.15f, lateralBreak: 0.26f, extraDrop: 0.12f);
            var changeup = CreatePitchType("Changeup", windup: sharedWindup, flight: 1.0f, perfect: 0.045f, good: 0.09f, weak: 0.15f, lateralBreak: 0f, extraDrop: 0.16f);
            // Wider-window variant used only in Round 1 so the very first pitches a player ever
            // sees are forgiving on TIMING precision; later rounds use plain Fastball. windup/
            // flight were originally 1.1/1.1 (slow enough to feel trivial combined with the
            // oversized 0.5-scale ball) - sped up to a meaningfully faster read while keeping
            // the wide timing windows, since a faster ball is harder to time even at the same
            // window width (less total reaction budget before you have to commit).
            var fastballEasy = CreatePitchType("Fastball_Easy", windup: 0.85f, flight: 0.8f, perfect: 0.09f, good: 0.16f, weak: 0.26f, lateralBreak: 0f, extraDrop: 0f);

            // Each round adds exactly one new pitch to the player's arsenal rather than
            // replacing the pool outright - round 2 still throws fastballs most of the time,
            // just with the occasional changeup mixed in now, and so on. Difficulty also climbs
            // via PitchLocationSpread (how far off-center a pitch can be) and the point
            // threshold required to advance. All rounds: fixed 5 outs, no early-exit-on-goal-met
            // (more outs just means more chances to add points).
            var roundWarmUp = CreateRoundConfig(
                id: "round_1_warm_up", name: "Warm-Up",
                tutorial: "Move the reticle into the strike zone and click to hit. Where and when you make connect both matter.",
                pitches: new[] { fastballEasy }, outLimit: 5,
                pointsToAdvance: 1000f, locationSpread: 0.35f);

            var roundChangeOfPace = CreateRoundConfig(
                id: "round_2_change_of_pace", name: "Change of Pace",
                tutorial: "Now you are facing a fastball and a changeup. It moves much slower.",
                pitches: new[] { fastballEasy, changeup }, outLimit: 5,
                pointsToAdvance: 1250f, locationSpread: 0.45f);

            var roundCurveballAlley = CreateRoundConfig(
                id: "round_3_curveball_alley", name: "Curveball Alley",
                tutorial: "A curveball is mixed in now too.",
                pitches: new[] { fastballEasy, changeup, curveball }, outLimit: 5,
                pointsToAdvance: 1500f, locationSpread: 0.55f);

            var roundFullCount = CreateRoundConfig(
                id: "round_4_full_count", name: "Full Count",
                tutorial: "Now the fastball is even faster.",
                pitches: new[] { fastball, changeup, curveball }, outLimit: 5,
                pointsToAdvance: 2000f, locationSpread: 0.65f);

            var roundNightGame = CreateRoundConfig(
                id: "round_5_night_game", name: "Night Game",
                tutorial: "Pitches are placed all over the zone. Taking a pitch does not count as an out.",
                pitches: new[] { fastball, changeup, curveball }, outLimit: 5,
                pointsToAdvance: 2500f, locationSpread: 0.75f);

            var roundHalloweenDerby = CreateRoundConfig(
                id: "round_6_halloween_derby", name: "Halloween Derby",
                tutorial: "Final round. Maximum difficulty.",
                pitches: new[] { fastball, changeup, curveball }, outLimit: 5,
                pointsToAdvance: 2500f, locationSpread: 0.9f, isBossRound: true);

            var ballPrefab = CreateBallPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Transform plate = CreateMarker("HomePlate", new Vector3(0f, 0.05f, 0f));
            Transform releasePoint = CreateMarker("PitcherReleasePoint", new Vector3(0f, 1.8f, 18f));

            CreateLighting();
            CreateGround();
            CreateField(plate, releasePoint);
            CreateBallparkDressing();

            StrikeZone strikeZone = CreateStrikeZone(plate);

            GameObject pitcher = CreatePitcher(releasePoint, plate, ballPrefab);
            GameObject batter = CreateBatter();

            GameObject reticleVisual = CreateReticleVisual();
            var reticleController = CreateZoneReticleController(batter.GetComponent<PlayerInputHandler>(), strikeZone, reticleVisual);
            var contactMarkers = CreateZoneContactMarkers(strikeZone);

            GameObject audioManagerGO = new GameObject("AudioManager");
            var audioManager = audioManagerGO.AddComponent<AudioManager>();
            WireAudioManager(audioManager);

            GameObject roundManagerGO = new GameObject("RoundManager");
            var roundManager = roundManagerGO.AddComponent<RoundManager>();
            var pitchController = pitcher.GetComponent<PitchController>();
            SetPrivateField(roundManager, "rounds", new[]
            {
                roundWarmUp, roundChangeOfPace, roundCurveballAlley, roundFullCount, roundNightGame, roundHalloweenDerby
            });
            SetPrivateField(roundManager, "pitchController", pitchController);

            var (batterCam, ballCam) = CreateCameras(releasePoint, strikeZone);
            GameObject cameraDirectorGO = new GameObject("CameraDirector");
            var director = cameraDirectorGO.AddComponent<CameraDirector>();
            SetPrivateField(director, "batterCamera", batterCam);
            SetPrivateField(director, "ballFollowCamera", ballCam);

            WireSwingController(batter, pitchController, reticleController, contactMarkers);
            WirePitcherComponents(pitcher, releasePoint, plate, ballPrefab, strikeZone);

            CreateHud();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("SceneBuilder: Derby.unity built successfully.");
        }

        [MenuItem("Halloween Derby/Verify Derby Scene Wiring")]
        public static void VerifyDerbyScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool ok = true;

            var roundManager = Object.FindAnyObjectByType<RoundManager>();
            ok &= Check(roundManager != null, "RoundManager exists in scene");
            if (roundManager != null)
            {
                var rounds = GetPrivateField<RoundConfig[]>(roundManager, "rounds");
                ok &= Check(rounds != null && rounds.Length == 6, "RoundManager has all 6 rounds");
                var pc = GetPrivateField<PitchController>(roundManager, "pitchController");
                ok &= Check(pc != null, "RoundManager.pitchController is wired");
            }

            var pitchController2 = Object.FindAnyObjectByType<PitchController>();
            ok &= Check(pitchController2 != null, "PitchController exists on pitcher");
            if (pitchController2 != null)
            {
                ok &= Check(GetPrivateField<BallFlightController>(pitchController2, "ballPrefab") != null, "PitchController.ballPrefab is wired");
                ok &= Check(GetPrivateField<Transform>(pitchController2, "releasePoint") != null, "PitchController.releasePoint is wired");
                ok &= Check(GetPrivateField<Transform>(pitchController2, "platePoint") != null, "PitchController.platePoint is wired");
                ok &= Check(GetPrivateField<Animator>(pitchController2, "animator") != null, "PitchController.animator (real SkeletonPitcher model) is wired");
                ok &= Check(GetPrivateField<StrikeZone>(pitchController2, "strikeZone") != null, "PitchController.strikeZone is wired");
            }

            var swing = Object.FindAnyObjectByType<SwingController>();
            ok &= Check(swing != null, "SwingController exists on batter");
            if (swing != null)
            {
                ok &= Check(GetPrivateField<PlayerInputHandler>(swing, "input") != null, "SwingController.input is wired");
                ok &= Check(GetPrivateField<PitchController>(swing, "pitchController") != null, "SwingController.pitchController is wired");
                ok &= Check(GetPrivateField<Animator>(swing, "batterAnimator") != null, "SwingController.batterAnimator (real Batter model) is wired");
                ok &= Check(GetPrivateField<ZoneReticleController>(swing, "reticle") != null, "SwingController.reticle is wired");
                ok &= Check(GetPrivateField<ZoneContactMarkers>(swing, "contactMarkers") != null, "SwingController.contactMarkers is wired");
            }

            var strikeZone = Object.FindAnyObjectByType<StrikeZone>();
            ok &= Check(strikeZone != null, "StrikeZone exists in scene");

            var reticle = Object.FindAnyObjectByType<ZoneReticleController>();
            ok &= Check(reticle != null, "ZoneReticleController exists in scene");
            if (reticle != null)
            {
                ok &= Check(GetPrivateField<PlayerInputHandler>(reticle, "input") != null, "ZoneReticleController.input is wired");
                ok &= Check(GetPrivateField<StrikeZone>(reticle, "zone") != null, "ZoneReticleController.zone is wired");
                ok &= Check(GetPrivateField<Transform>(reticle, "reticleVisual") != null, "ZoneReticleController.reticleVisual is wired");
            }

            var contactMarkersObj = Object.FindAnyObjectByType<ZoneContactMarkers>();
            ok &= Check(contactMarkersObj != null, "ZoneContactMarkers exists in scene");
            if (contactMarkersObj != null)
            {
                ok &= Check(GetPrivateField<StrikeZone>(contactMarkersObj, "zone") != null, "ZoneContactMarkers.zone is wired");
                ok &= Check(GetPrivateField<Transform>(contactMarkersObj, "swingMarker") != null, "ZoneContactMarkers.swingMarker is wired");
                ok &= Check(GetPrivateField<Transform>(contactMarkersObj, "ballMarker") != null, "ZoneContactMarkers.ballMarker is wired");
            }

            var inputHandler = Object.FindAnyObjectByType<PlayerInputHandler>();
            ok &= Check(inputHandler != null, "PlayerInputHandler exists on batter");
            if (inputHandler != null)
                ok &= Check(GetPrivateField<InputActionAsset>(inputHandler, "actionsAsset") != null, "PlayerInputHandler.actionsAsset is wired");

            var cameraDirector = Object.FindAnyObjectByType<CameraDirector>();
            ok &= Check(cameraDirector != null, "CameraDirector exists");
            if (cameraDirector != null)
            {
                ok &= Check(GetPrivateField<CinemachineCamera>(cameraDirector, "batterCamera") != null, "CameraDirector.batterCamera is wired");
                ok &= Check(GetPrivateField<CinemachineCamera>(cameraDirector, "ballFollowCamera") != null, "CameraDirector.ballFollowCamera is wired");
            }

            var hud = Object.FindAnyObjectByType<HUDController>();
            ok &= Check(hud != null, "HUDController exists");
            if (hud != null)
            {
                ok &= Check(GetPrivateField<Text>(hud, "distanceText") != null, "HUDController.distanceText is wired");
                ok &= Check(GetPrivateField<Text>(hud, "outsText") != null, "HUDController.outsText is wired");
                ok &= Check(GetPrivateField<Text>(hud, "scoreText") != null, "HUDController.scoreText is wired");
                ok &= Check(GetPrivateField<GameObject>(hud, "tutorialPanel") != null, "HUDController.tutorialPanel is wired");
            }

            ok &= Check(Object.FindAnyObjectByType<AudioManager>() != null, "AudioManager exists in scene");
            ok &= Check(Object.FindAnyObjectByType<CinemachineBrain>() != null, "CinemachineBrain exists on main camera");

            Debug.Log(ok ? "VERIFY: ALL CHECKS PASSED" : "VERIFY: ONE OR MORE CHECKS FAILED");
        }

        private static bool Check(bool condition, string label)
        {
            Debug.Log((condition ? "[PASS] " : "[FAIL] ") + label);
            return condition;
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(target) as T;
        }

        private static PitchTypeData CreatePitchType(string name, float windup, float flight, float perfect, float good, float weak, float lateralBreak, float extraDrop)
        {
            string path = $"{PitchDataDir}/PitchType_{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PitchTypeData>(path);
            var data = existing != null ? existing : ScriptableObject.CreateInstance<PitchTypeData>();
            data.DisplayName = name;
            data.WindupDuration = windup;
            data.FlightDuration = flight;
            data.PerfectWindow = perfect;
            data.GoodWindow = good;
            data.WeakWindow = weak;
            data.LateralBreak = lateralBreak;
            data.ExtraDrop = extraDrop;
            data.WindupAnimTrigger = $"Windup_{name}";
            // Release motion looks the same across pitch types in real baseball too (grip
            // differs, not the visible throw) - one shared clip/trigger keeps the Animator
            // Controller simple while the windup stays distinct per type for pitch-reading.
            data.ReleaseAnimTrigger = "Release";

            if (existing == null) AssetDatabase.CreateAsset(data, path);
            else EditorUtility.SetDirty(data);
            return data;
        }

        private static RoundConfig CreateRoundConfig(string id, string name, string tutorial, PitchTypeData[] pitches, int outLimit,
            float pointsToAdvance, float locationSpread = 0.35f, bool isBossRound = false)
        {
            string path = $"{RoundDataDir}/Round_{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RoundConfig>(path);
            var data = existing != null ? existing : ScriptableObject.CreateInstance<RoundConfig>();
            data.RoundId = id;
            data.DisplayName = name;
            data.TutorialText = tutorial;
            data.PitchPool = pitches;
            data.OutLimit = outLimit;
            data.SuccessCriteria = RoundSuccessCriteria.ScoreThreshold;
            data.PointsToAdvance = pointsToAdvance;
            data.PitchLocationSpread = locationSpread;
            data.IsBossRound = isBossRound;
            data.EndEarlyOnGoalMet = false;
            data.PitchInterval = 2.2f;

            if (existing == null) AssetDatabase.CreateAsset(data, path);
            else EditorUtility.SetDirty(data);
            return data;
        }

        private static BallFlightController CreateBallPrefab()
        {
            string path = $"{PrefabDir}/PumpkinBall.prefab";

            // Physics root stays an invisible primitive sphere (trigger collider + rigidbody) -
            // the actual visible "sprite" is the real Kenney pumpkin-carved mesh parented
            // underneath, since a plain colored sphere never read as an actual jack-o'-lantern.
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "PumpkinBall";
            ball.transform.localScale = Vector3.one * 0.32f;
            Object.DestroyImmediate(ball.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(ball.GetComponent<MeshFilter>());

            var collider = ball.GetComponent<SphereCollider>();
            collider.isTrigger = true;

            var rb = ball.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;

            var pumpkinModel = LoadProp("pumpkin-carved");
            if (pumpkinModel != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(pumpkinModel, ball.transform);
                visual.name = "PumpkinVisual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                // Kenney prop imports at its own real-world scale (~0.4-0.5 units across already,
                // per the other Props/* instances used elsewhere) - normalize it to fill the
                // 1-unit collider sphere above, since localScale=0.32 is applied to the parent.
                var meshRenderers = visual.GetComponentsInChildren<MeshRenderer>(true);
                if (meshRenderers.Length > 0)
                {
                    Bounds b = meshRenderers[0].bounds;
                    foreach (var r in meshRenderers) b.Encapsulate(r.bounds);
                    float largestDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    if (largestDim > 0.001f)
                    {
                        float fitScale = 1f / (largestDim / ball.transform.localScale.x);
                        visual.transform.localScale = Vector3.one * fitScale;
                    }
                }

                var glowMat = GetOrCreatePersistedMaterial("PumpkinGlow", new Color(1f, 0.55f, 0.1f), new Color(1f, 0.35f, 0.03f) * 1.8f);
                foreach (var r in meshRenderers) r.sharedMaterial = glowMat;
            }
            else
            {
                Debug.LogWarning("SceneBuilder: pumpkin-carved prop not found - ball will have no visible mesh.");
            }

            // A point light reads far better at night/in fog than relying on the emissive
            // material alone - this is gameplay clarity, not just flavor.
            var glow = ball.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.5f, 0.1f);
            glow.intensity = 3f;
            glow.range = 6f;

            var trail = ball.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.widthMultiplier = 1f;
            trail.startWidth = 0.25f;
            trail.endWidth = 0.02f;
            trail.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.6f, 0.15f) };
            trail.colorGradient = BuildFadeGradient(new Color(1f, 0.6f, 0.15f));

            ball.AddComponent<BallFlightController>();

            // Use the prefab object SaveAsPrefabAsset hands back directly rather than
            // re-loading from disk - in a long-lived Editor session (as opposed to a fresh
            // CLI process) AssetDatabase.LoadAssetAtPath right after a save can return a
            // stale/incomplete reference, leaving PitchController.ballPrefab silently null.
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(ball, path);
            Object.DestroyImmediate(ball);

            var ballController = savedPrefab != null ? savedPrefab.GetComponent<BallFlightController>() : null;
            if (ballController == null)
                Debug.LogError("SceneBuilder: failed to save/load PumpkinBall prefab - ballPrefab will be null.");
            return ballController;
        }

        private static Gradient BuildFadeGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static void CreateLighting()
        {
            // No post-processing Volume/tonemapping is configured, so light intensities map
            // close to linearly to output color - the first attempt at "brighter" (1.3 + a 0.35
            // fill + 0.22 ambient) blew the whole scene out to washed-out white/lavender instead
            // of a moody night. These are deliberately modest.
            GameObject lightGO = new GameObject("Moonlight");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.65f, 0.65f, 0.9f);
            light.intensity = 0.7f;
            lightGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            // A low warm fill from roughly where the lanterns/pumpkin glow would be, so surfaces
            // facing away from the moon aren't pure black - a flat-black field reads as "nothing
            // here" even when geometry is present.
            GameObject fillGO = new GameObject("WarmFill");
            var fill = fillGO.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(1f, 0.6f, 0.3f);
            fill.intensity = 0.12f;
            fillGO.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.08f, 0.05f, 0.14f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;

            // Explicit flat ambient + a solid-color camera background - without these, an empty
            // scene with no skybox material falls back to Unity's default flat mid-gray clear,
            // which is almost certainly what was originally reading as "an empty gray area".
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.08f, 0.14f);
            RenderSettings.skybox = null;

            CreateMoonAndStars();
        }

        /// A solid-color camera background with nothing else reads as an empty void once
        /// distant ballpark dressing is too thin/dark to register against it (see
        /// CreateBallparkDressing) - a visible moon disc and a scatter of star points give the
        /// night sky some actual presence instead of a flat black wall.
        private static void CreateMoonAndStars()
        {
            GameObject moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "Moon";
            Object.DestroyImmediate(moon.GetComponent<SphereCollider>());
            moon.transform.position = new Vector3(-45f, 70f, 140f);
            moon.transform.localScale = Vector3.one * 14f;
            var moonMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.85f, 0.85f, 0.75f) };
            moonMat.EnableKeyword("_EMISSION");
            moonMat.SetColor("_EmissionColor", new Color(0.8f, 0.8f, 0.7f) * 1.2f);
            moon.GetComponent<MeshRenderer>().sharedMaterial = moonMat;

            GameObject starsGO = new GameObject("Stars");
            var starMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
            starMat.EnableKeyword("_EMISSION");
            starMat.SetColor("_EmissionColor", Color.white * 1.5f);
            var mesh = BuildStarQuadMesh(160);
            var mf = starsGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = starsGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = starMat;
        }

        /// GL.POINTS topology isn't reliably rasterized with a visible size across platforms
        /// under URP's default Unlit shader (point size needs shader support that isn't
        /// guaranteed) - small camera-facing quads render reliably everywhere instead. The
        /// PitchZoneCamera never turns away from +z, so quads are scattered within its forward
        /// view cone and all face -z rather than doing true per-star billboarding.
        private static Mesh BuildStarQuadMesh(int count)
        {
            var vertices = new System.Collections.Generic.List<Vector3>(count * 4);
            var triangles = new System.Collections.Generic.List<int>(count * 6);
            for (int i = 0; i < count; i++)
            {
                Vector3 center = new Vector3(Random.Range(-150f, 150f), Random.Range(35f, 160f), Random.Range(60f, 260f));
                float s = Random.Range(0.4f, 1.1f);
                int baseIndex = vertices.Count;
                vertices.Add(center + new Vector3(-s, -s, 0f));
                vertices.Add(center + new Vector3(s, -s, 0f));
                vertices.Add(center + new Vector3(s, s, 0f));
                vertices.Add(center + new Vector3(-s, s, 0f));
                triangles.Add(baseIndex); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex); triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 2);
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void CreateGround()
        {
            // A ball-flight-distance check: BaseLaunchSpeed (41 m/s) at full charge (1.6x) and
            // the resolver's own max launch angle (38 deg) gives a projectile range of roughly
            // (65.6^2 * sin(76 deg)) / 9.81 =~ 425 units - a legitimately-charged high home run
            // could travel more than 4x further than the old ground plane's 100-unit radius
            // (scale 20 on Unity's 10x10 default Plane), flying off the edge into visible
            // nothing well before landing. Scaled up with a wide safety margin.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            var renderer = ground.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.18f, 0.22f, 0.14f) };
            renderer.sharedMaterial = mat;
        }

        /// Dirt infield, pitcher's mound, foul lines, batter's box, a backstop behind the
        /// plate, and mowed-stripe outfield grass - so this reads as an actual ballpark
        /// instead of a flat plane with props scattered on it.
        private static void CreateField(Transform plate, Transform releasePoint)
        {
            GameObject field = new GameObject("Field");
            var dirtMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.45f, 0.3f, 0.18f) };
            var lineMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.92f, 0.92f, 0.85f) };

            // Infield dirt - flattened cylinder centered between the plate and the mound.
            GameObject infield = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            infield.name = "InfieldDirt";
            infield.transform.SetParent(field.transform, false);
            float infieldCenterZ = (plate.position.z + releasePoint.position.z) * 0.5f - 2f;
            infield.transform.position = new Vector3(0f, 0.01f, infieldCenterZ);
            infield.transform.localScale = new Vector3(15f, 0.01f, 15f);
            infield.GetComponent<MeshRenderer>().sharedMaterial = dirtMat;
            Object.DestroyImmediate(infield.GetComponent<CapsuleCollider>());

            // Pitcher's mound.
            GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mound.name = "PitchersMound";
            mound.transform.SetParent(field.transform, false);
            mound.transform.position = new Vector3(releasePoint.position.x, 0.12f, releasePoint.position.z);
            mound.transform.localScale = new Vector3(2.2f, 0.12f, 2.2f);
            mound.GetComponent<MeshRenderer>().sharedMaterial = dirtMat;
            Object.DestroyImmediate(mound.GetComponent<CapsuleCollider>());

            // Home plate (small white marker).
            GameObject homeMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            homeMarker.name = "HomePlateMarker";
            homeMarker.transform.SetParent(field.transform, false);
            homeMarker.transform.position = plate.position + Vector3.up * 0.02f;
            homeMarker.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
            homeMarker.GetComponent<MeshRenderer>().sharedMaterial = lineMat;
            Object.DestroyImmediate(homeMarker.GetComponent<BoxCollider>());

            // Batter's box.
            GameObject battersBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            battersBox.name = "BattersBox";
            battersBox.transform.SetParent(field.transform, false);
            battersBox.transform.position = new Vector3(-1.4f, 0.015f, -0.5f);
            battersBox.transform.localScale = new Vector3(1.4f, 0.015f, 2.2f);
            battersBox.GetComponent<MeshRenderer>().sharedMaterial = dirtMat;
            Object.DestroyImmediate(battersBox.GetComponent<BoxCollider>());

            // Foul lines running from the plate out to the fence corners at +/-45 degrees.
            CreateFoulLine(field.transform, plate.position, 45f, lineMat);
            CreateFoulLine(field.transform, plate.position, -45f, lineMat);

            // Backstop: an arc of iron-fence segments behind home plate.
            var fenceProp = LoadProp("iron-fence");
            if (fenceProp != null)
            {
                for (int i = -3; i <= 3; i++)
                {
                    float angleDeg = i * 15f;
                    Vector3 pos = plate.position + Quaternion.Euler(0f, angleDeg, 0f) * new Vector3(0f, 0f, -5f);
                    SpawnProp(fenceProp, field.transform, pos, Quaternion.Euler(0f, 180f + angleDeg, 0f));
                }
            }

        }

        private static void CreateFoulLine(Transform parent, Vector3 origin, float angleDeg, Material lineMat)
        {
            const float length = 90f;
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = $"FoulLine_{angleDeg}";
            line.transform.SetParent(parent, false);
            Quaternion rot = Quaternion.Euler(0f, angleDeg, 0f);
            Vector3 direction = rot * Vector3.forward;
            line.transform.position = origin + direction * (length * 0.5f) + Vector3.up * 0.015f;
            line.transform.rotation = rot;
            line.transform.localScale = new Vector3(0.2f, 0.015f, length);
            line.GetComponent<MeshRenderer>().sharedMaterial = lineMat;
            Object.DestroyImmediate(line.GetComponent<BoxCollider>());
        }

        private const string PropsDir = "Assets/Models/Props";

        /// Dresses the ballpark with Kenney Halloween-kit props: a tree-line standing in for
        /// the cornfield beyond the outfield wall, a graveyard cluster past that, a crypt
        /// doubling as the distant haunted house in left field, and lanterns for the
        /// orange-glow night lighting the brief calls for. Purely visual - no gameplay logic.
        private static void CreateBallparkDressing()
        {
            var pine = LoadProp("pine-fall-crooked");
            var grave1 = LoadProp("gravestone-cross");
            var grave2 = LoadProp("gravestone-round");
            var grave3 = LoadProp("gravestone-broken");
            var cryptBody = LoadProp("crypt-large");
            var cryptRoof = LoadProp("crypt-large-roof");
            var lantern = LoadProp("lantern-candle");

            GameObject dressing = new GameObject("BallparkDressing");

            // Curved outfield wall at the actual home-run distance (DerbyConstants, shared with
            // BallFlightController's default home-run threshold so the visual wall and the
            // "is it a home run" math can't drift apart). Built from solid primitive segments -
            // Kenney's "iron-fence" prop is a short decorative picket garden fence with gaps
            // between pickets, which doesn't read as a real outfield wall at all.
            float wallZ = DerbyConstants.HomeRunDistanceUnits;
            CreateOutfieldWall(wallZ, dressing.transform);

            // Tree-line (cornfield stand-in) right behind the wall. The original placement
            // (scale 1, wallZ+6+/-2) was geometrically present but functionally invisible: at
            // ~100+ units from the elevated batter-cam, a ~2-unit-tall tree's top edge sits
            // almost exactly at the camera's own eye-level height, so it occupies only a sliver
            // of vertical screen space right at the horizon line - which, against a flat-black
            // sky and matching fog color, reads as nothing at all (confirmed via Debug > Dump
            // Pine Tree Size + a play-mode screenshot showing an empty void past the fence).
            // Scaling up and pulling in closer gives them real angular height above the horizon.
            if (pine != null)
            {
                for (float x = -60f; x <= 60f; x += 4f)
                {
                    var tree = SpawnProp(pine, dressing.transform, new Vector3(x + Random.Range(-1.2f, 1.2f), 0f, wallZ + 2.5f + Random.Range(-1f, 1.5f)), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    tree.transform.localScale = Vector3.one * Random.Range(2.6f, 3.4f);
                }
            }

            // Graveyard cluster beyond the trees, off to the right field side - same fix:
            // scaled up and pulled much closer to the wall so it actually clears the horizon.
            var graves = new[] { grave1, grave2, grave3 };
            for (int i = 0; i < 10; i++)
            {
                var g = graves[i % graves.Length];
                if (g == null) continue;
                Vector3 pos = new Vector3(14f + Random.Range(0f, 26f), 0f, wallZ + 4f + Random.Range(0f, 7f));
                var stone = SpawnProp(g, dressing.transform, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                stone.transform.localScale = Vector3.one * Random.Range(1.5f, 1.9f);
            }

            // Haunted house stand-in (no CC0 house model found - a Kenney crypt reads as a
            // small gothic building) in left field, pulled in close enough to actually be seen.
            if (cryptBody != null)
            {
                Vector3 housePos = new Vector3(-28f, 0f, wallZ + 7f);
                var body = SpawnProp(cryptBody, dressing.transform, housePos, Quaternion.Euler(0f, 20f, 0f));
                body.transform.localScale = Vector3.one * 1.7f;
                if (cryptRoof != null)
                {
                    var roof = SpawnProp(cryptRoof, dressing.transform, housePos, Quaternion.Euler(0f, 20f, 0f));
                    roof.transform.localScale = Vector3.one * 1.7f;
                }
            }

            // Lanterns along the foul lines near the plate for warm foreground light.
            if (lantern != null)
            {
                Vector3[] lanternSpots = { new Vector3(-6f, 0f, 2f), new Vector3(6f, 0f, 2f), new Vector3(-9f, 0f, 10f), new Vector3(9f, 0f, 10f) };
                foreach (var spot in lanternSpots)
                {
                    var instance = SpawnProp(lantern, dressing.transform, spot, Quaternion.identity);
                    var glow = instance.AddComponent<Light>();
                    glow.type = LightType.Point;
                    glow.color = new Color(1f, 0.55f, 0.15f);
                    glow.intensity = 2.5f;
                    glow.range = 7f;
                }
            }

        }

        /// A continuous curved outfield wall (dark green body, yellow home-run-line cap) built
        /// from overlapping primitive segments, since no CC0 sports-fence asset was available.
        private static void CreateOutfieldWall(float radius, Transform parent)
        {
            var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.07f, 0.26f, 0.1f) };
            var capMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.85f, 0.75f, 0.15f) };

            const float spanDeg = 55f; // a bit past the 45-degree foul lines, like a real park
            const float stepDeg = 4f;
            const float wallHeight = 2.4f;
            const float wallThickness = 0.3f;

            float stepRad = stepDeg * Mathf.Deg2Rad;
            float segmentWidth = radius * stepRad * 1.35f; // overlap so the curve has no gaps

            for (float a = -spanDeg; a <= spanDeg; a += stepDeg)
            {
                Quaternion rot = Quaternion.Euler(0f, a, 0f);
                Vector3 basePos = rot * Vector3.forward * radius;

                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = $"OutfieldWall_{a}";
                segment.transform.SetParent(parent, false);
                segment.transform.position = basePos + Vector3.up * (wallHeight * 0.5f);
                segment.transform.rotation = rot;
                segment.transform.localScale = new Vector3(segmentWidth, wallHeight, wallThickness);
                segment.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
                Object.DestroyImmediate(segment.GetComponent<BoxCollider>());

                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cap.name = $"OutfieldWallCap_{a}";
                cap.transform.SetParent(parent, false);
                cap.transform.position = basePos + Vector3.up * (wallHeight + 0.05f);
                cap.transform.rotation = rot;
                cap.transform.localScale = new Vector3(segmentWidth, 0.1f, wallThickness + 0.05f);
                cap.GetComponent<MeshRenderer>().sharedMaterial = capMat;
                Object.DestroyImmediate(cap.GetComponent<BoxCollider>());
            }
        }

        private static GameObject LoadProp(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsDir}/{name}.fbx");

        /// Shader.Find("Universal Render Pipeline/Lit") has been observed to return null on the
        /// very first script-compile pass of a fresh -batchmode process (shaders not yet
        /// "warmed up"), which silently bakes a missing-shader (magenta) material into whatever
        /// prefab is being built that run (see CharacterSetup.GetOrCreateWoodMaterial for the
        /// first time this bit us, with the bat). A persisted, reused Material asset sidesteps
        /// re-hitting that failure path on every rebuild instead of creating one in memory.
        private static Material GetOrCreatePersistedMaterial(string assetName, Color color, Color? emission = null)
        {
            string path = $"Assets/Materials/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && existing.shader != null && existing.shader.name != "Hidden/InternalErrorShader")
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError($"SceneBuilder: Shader.Find(\"Universal Render Pipeline/Lit\") returned null while building {assetName} - falling back to Standard.");
                shader = Shader.Find("Standard");
            }

            var mat = new Material(shader) { color = color };
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject SpawnProp(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            return instance;
        }

        private static Transform CreateMarker(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            return go.transform;
        }

        private static GameObject CreatePitcher(Transform releasePoint, Transform plate, BallFlightController ballPrefab)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SkeletonPitcher.prefab");
            GameObject pitcher;
            if (prefab != null)
            {
                pitcher = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                pitcher.transform.position = new Vector3(0f, 0f, 17f);
                pitcher.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the batter (-Z)
            }
            else
            {
                // Fallback if CharacterSetup hasn't been run yet - keeps the scene buildable.
                Debug.LogWarning("SceneBuilder: SkeletonPitcher.prefab not found - run Halloween Derby > Build Character Animators And Prefabs first. Using a primitive stand-in.");
                pitcher = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                pitcher.transform.position = new Vector3(0f, 1f, 17f);
                pitcher.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.white };
            }
            pitcher.name = "SkeletonPitcher";

            var pitchController = pitcher.AddComponent<PitchController>();
            var reactions = pitcher.AddComponent<PitcherReactions>();
            var animator = pitcher.GetComponent<Animator>();
            SetPrivateField(pitchController, "animator", animator);
            SetPrivateField(reactions, "animator", animator);

            if (animator == null)
            {
                // No real Animator (primitive fallback) - keep the procedural scale/color tell.
                var telegraph = pitcher.AddComponent<PrimitiveWindupTelegraph>();
                var renderer = pitcher.GetComponent<MeshRenderer>();
                SetPrivateField(telegraph, "pitchController", pitchController);
                SetPrivateField(telegraph, "visualRoot", pitcher.transform);
                SetPrivateField(telegraph, "bodyRenderer", renderer);
            }

            return pitcher;
        }

        private static GameObject CreateBatter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Batter.prefab");
            GameObject batter;
            if (prefab != null)
            {
                batter = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                // Right-handed batter's box: standing on the plate's left side as seen from the
                // pitcher/batter-cam view (-X here), lined up alongside the plate (same Z, not
                // offset forward/back), body turned 90 degrees to the pitcher - a standard
                // square baseball stance, chest facing across toward the plate (+X), not facing
                // the pitcher head-on. Verified via Halloween Derby > Debug > Screenshot Field
                // Top-Down with a forward-direction arrow - the first sign choice (-90) had the
                // batter facing away from the plate into foul territory.
                // Pulled further out/back than the first pass (-0.9, 0) - close enough to the
                // zone that the held bat visually crossed into it from the PitchZoneCamera view.
                batter.transform.position = new Vector3(-1.4f, 0f, -0.5f);
                batter.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else
            {
                Debug.LogWarning("SceneBuilder: Batter.prefab not found - run Halloween Derby > Build Character Animators And Prefabs first. Using a primitive stand-in.");
                batter = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                batter.transform.position = new Vector3(0f, 1f, -1f);
                batter.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.8f, 0.5f, 0.1f) };
            }
            batter.name = "Batter";

            batter.AddComponent<PlayerInputHandler>();
            batter.AddComponent<SwingController>();
            return batter;
        }

        /// "Zone hitting" camera (MLB The Show / Super Mega Baseball style): the batter's own
        /// point of view, positioned just behind the strike zone looking out at the pitcher, so
        /// the zone box sits large and clear right in front of the camera and the pitch flies
        /// toward the viewer as it approaches. (An earlier version put this behind the pitcher
        /// looking the other way - reverted per feedback that it should be the batter's POV.)
        private static (CinemachineCamera batterCam, CinemachineCamera ballCam) CreateCameras(Transform releasePoint, StrikeZone zone)
        {
            GameObject mainCamGO = new GameObject("Main Camera");
            mainCamGO.tag = "MainCamera";
            var mainCam = mainCamGO.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.05f, 0.035f, 0.09f); // dark purple night, not Unity's default gray
            mainCamGO.AddComponent<AudioListener>();
            mainCamGO.AddComponent<CinemachineBrain>();

            // History: 0.7 up / 3 back framed the batter from eye level at close range and
            // cropped their legs out of frame. 1.8 up / 6.2 back fixed the legs but ended up
            // looking down from too high. Pulled back further instead of raised further, so the
            // full body still fits without the steep downward angle.
            GameObject batterCamGO = new GameObject("PitchZoneCamera");
            var batterCam = batterCamGO.AddComponent<CinemachineCamera>();
            batterCamGO.transform.position = zone.WorldCenter + new Vector3(0f, 1.1f, -8f);
            batterCamGO.transform.LookAt(releasePoint.position + Vector3.down * 0.3f);
            batterCam.Lens.FieldOfView = 55f;
            batterCam.Priority = 20;

            GameObject ballCamGO = new GameObject("BallFollowCamera");
            var ballCam = ballCamGO.AddComponent<CinemachineCamera>();
            ballCamGO.AddComponent<CinemachineFollow>();
            ballCam.Priority = 5;

            return (batterCam, ballCam);
        }

        private static StrikeZone CreateStrikeZone(Transform plate)
        {
            GameObject zoneGO = new GameObject("StrikeZone");
            zoneGO.transform.position = new Vector3(plate.position.x, 0f, plate.position.z);
            return zoneGO.AddComponent<StrikeZone>();
        }

        private static GameObject CreateReticleVisual()
        {
            GameObject reticle = new GameObject("ZoneReticleVisual");
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.95f, 0.2f) };

            GameObject h = GameObject.CreatePrimitive(PrimitiveType.Cube);
            h.name = "ReticleBarH";
            h.transform.SetParent(reticle.transform, false);
            h.transform.localScale = new Vector3(0.16f, 0.02f, 0.02f);
            Object.DestroyImmediate(h.GetComponent<BoxCollider>());
            h.GetComponent<MeshRenderer>().sharedMaterial = mat;

            GameObject v = GameObject.CreatePrimitive(PrimitiveType.Cube);
            v.name = "ReticleBarV";
            v.transform.SetParent(reticle.transform, false);
            v.transform.localScale = new Vector3(0.02f, 0.16f, 0.02f);
            Object.DestroyImmediate(v.GetComponent<BoxCollider>());
            v.GetComponent<MeshRenderer>().sharedMaterial = mat;

            return reticle;
        }

        private static ZoneReticleController CreateZoneReticleController(PlayerInputHandler inputHandler, StrikeZone zone, GameObject reticleVisual)
        {
            GameObject go = new GameObject("ZoneReticleController");
            var controller = go.AddComponent<ZoneReticleController>();
            SetPrivateField(controller, "input", inputHandler);
            SetPrivateField(controller, "zone", zone);
            SetPrivateField(controller, "reticleVisual", reticleVisual.transform);
            return controller;
        }

        /// Red X (where the reticle actually was at the swing) and an orange dot (where the
        /// ball actually crossed) so the player can see exactly how their swing compared, not
        /// just infer it from the hit result.
        private static ZoneContactMarkers CreateZoneContactMarkers(StrikeZone zone)
        {
            var swingMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.15f, 0.1f) };
            GameObject swingMarker = new GameObject("SwingMarker");
            GameObject sh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sh.name = "Bar1";
            sh.transform.SetParent(swingMarker.transform, false);
            sh.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            sh.transform.localScale = new Vector3(0.18f, 0.025f, 0.025f);
            Object.DestroyImmediate(sh.GetComponent<BoxCollider>());
            sh.GetComponent<MeshRenderer>().sharedMaterial = swingMat;
            GameObject sv = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sv.name = "Bar2";
            sv.transform.SetParent(swingMarker.transform, false);
            sv.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            sv.transform.localScale = new Vector3(0.18f, 0.025f, 0.025f);
            Object.DestroyImmediate(sv.GetComponent<BoxCollider>());
            sv.GetComponent<MeshRenderer>().sharedMaterial = swingMat;

            GameObject ballMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballMarker.name = "BallCrossMarker";
            ballMarker.transform.localScale = Vector3.one * 0.14f;
            Object.DestroyImmediate(ballMarker.GetComponent<SphereCollider>());
            var ballMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.6f, 0.1f) };
            ballMarker.GetComponent<MeshRenderer>().sharedMaterial = ballMat;

            GameObject controllerGO = new GameObject("ZoneContactMarkers");
            var markers = controllerGO.AddComponent<ZoneContactMarkers>();
            SetPrivateField(markers, "zone", zone);
            SetPrivateField(markers, "swingMarker", swingMarker.transform);
            SetPrivateField(markers, "ballMarker", ballMarker.transform);
            return markers;
        }

        private static void WireSwingController(GameObject batter, PitchController pitchController, ZoneReticleController reticle, ZoneContactMarkers contactMarkers)
        {
            var swingController = batter.GetComponent<SwingController>();
            var inputHandler = batter.GetComponent<PlayerInputHandler>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/PlayerControls.inputactions");

            SetPrivateField(inputHandler, "actionsAsset", actions);
            SetPrivateField(swingController, "input", inputHandler);
            SetPrivateField(swingController, "pitchController", pitchController);
            SetPrivateField(swingController, "reticle", reticle);
            SetPrivateField(swingController, "contactMarkers", contactMarkers);
            SetPrivateField(swingController, "batterAnimator", batter.GetComponent<Animator>());
        }

        private static void WirePitcherComponents(GameObject pitcher, Transform releasePoint, Transform plate, BallFlightController ballPrefab, StrikeZone zone)
        {
            var pitchController = pitcher.GetComponent<PitchController>();
            SetPrivateField(pitchController, "ballPrefab", ballPrefab);
            SetPrivateField(pitchController, "releasePoint", releasePoint);
            SetPrivateField(pitchController, "platePoint", plate);
            SetPrivateField(pitchController, "strikeZone", zone);
        }

        private const string SfxDir = "Assets/Audio/sfx";

        /// Loads the clips AudioGenerator produces (Halloween Derby > Generate Placeholder Audio)
        /// and wires them into AudioManager's serialized entry arrays. A missing clip just logs
        /// a warning and leaves that entry empty - AudioManager itself is already null-safe at
        /// playback time, so this never blocks the scene from building.
        private static void WireAudioManager(AudioManager audioManager)
        {
            var sfxFiles = new (SfxId id, string file, float volume)[]
            {
                (SfxId.BatCrackPerfect, "BatCrack_Perfect", 0.9f),
                (SfxId.BatCrackGood, "BatCrack_Good", 0.85f),
                (SfxId.BatCrackWeak, "BatCrack_Weak", 0.8f),
                (SfxId.SwingWhoosh, "SwingWhoosh", 0.6f),
                (SfxId.SwingMiss, "SwingMiss", 0.65f),
                (SfxId.PitcherWindup, "PitcherWindup", 0.5f),
                (SfxId.PumpkinBurst, "PumpkinBurst", 0.85f),
                (SfxId.TombstoneBreak, "TombstoneBreak", 0.8f),
                (SfxId.GlassBreak, "GlassBreak", 0.8f),
                (SfxId.TargetThud, "TargetThud", 0.6f),
                (SfxId.GhostOooo, "GhostOooo", 0.55f),
                (SfxId.RoundStart, "RoundStart", 0.6f),
                (SfxId.RoundClear, "RoundClear", 0.7f),
                (SfxId.RoundFail, "RoundFail", 0.6f),
                (SfxId.StarAward, "StarAward", 0.55f),
                (SfxId.OutCalled, "OutCalled", 0.6f),
            };

            var sfxEntries = new SfxEntry[sfxFiles.Length];
            for (int i = 0; i < sfxFiles.Length; i++)
            {
                var (id, file, volume) = sfxFiles[i];
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxDir}/{file}.wav");
                if (clip == null)
                    Debug.LogWarning($"SceneBuilder: no clip found for {id} at {SfxDir}/{file}.wav - run Halloween Derby > Generate Placeholder Audio.");
                sfxEntries[i] = new SfxEntry { Id = id, Clips = clip != null ? new[] { clip } : null, Volume = volume };
            }

            // No music, per feedback - leave musicEntries empty. AudioManager.PlayMusic is
            // simply never called anywhere in the game.
            SetPrivateField(audioManager, "sfxEntries", sfxEntries);
            SetPrivateField(audioManager, "musicEntries", new MusicEntry[0]);
        }

        private static void CreateHud()
        {
            GameObject canvasGO = new GameObject("HUDCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            Text distanceText = CreateUiText(canvasGO.transform, "DistanceText", new Vector2(0f, -40f), new Vector2(600f, 60f), 36, TextAnchor.UpperCenter);
            Text outsText = CreateUiText(canvasGO.transform, "OutsText", new Vector2(-760f, -40f), new Vector2(300f, 50f), 28, TextAnchor.UpperLeft);
            Text stageText = CreateUiText(canvasGO.transform, "StageText", new Vector2(760f, -40f), new Vector2(350f, 50f), 28, TextAnchor.UpperRight);
            // Canvas reference width is 1920 (half = 960). At x=-760 the previous 420-wide box's
            // left edge (-760-210=-970) ran past the left edge of the screen (-960) - this
            // narrower box (left edge -760-150=-910) stays safely on-screen at a smaller font.
            Text scoreText = CreateUiText(canvasGO.transform, "ScoreText", new Vector2(-760f, -90f), new Vector2(300f, 50f), 24, TextAnchor.UpperLeft);

            GameObject tutorialPanel = new GameObject("TutorialPanel", typeof(RectTransform));
            tutorialPanel.transform.SetParent(canvasGO.transform, false);
            var panelRect = tutorialPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 130f);
            panelRect.sizeDelta = new Vector2(920f, 110f);
            var panelImage = tutorialPanel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.65f);
            // Anchored to the panel's own center (0.5, 0.5), not CreateUiText's default
            // top-of-parent anchor - the default put the text's pivot at the panel's top edge,
            // so only its bottom half was actually inside the box.
            Text tutorialText = CreateUiText(tutorialPanel.transform, "TutorialText", Vector2.zero, new Vector2(860f, 95f), 22, TextAnchor.MiddleCenter, anchor: new Vector2(0.5f, 0.5f));

            // Full-screen modal shown between stages - distinguishing each stage with an actual
            // "Stage Clear" break (not just an instant auto-continue) is what was asked for here.
            GameObject stageClearPanel = CreateFullScreenPanel(canvasGO.transform, "StageClearPanel", new Color(0.03f, 0.02f, 0.06f, 0.85f));
            Text stageClearText = CreateUiText(stageClearPanel.transform, "StageClearHeadline", new Vector2(0f, 60f), new Vector2(900f, 160f), 48, TextAnchor.MiddleCenter, anchor: new Vector2(0.5f, 0.5f));
            Button continueButton = CreateUiButton(stageClearPanel.transform, "ContinueButton", new Vector2(0f, -100f), new Vector2(360f, 70f), "Continue", 30);

            GameObject gameWonPanel = CreateFullScreenPanel(canvasGO.transform, "GameWonPanel", new Color(0.03f, 0.02f, 0.06f, 0.9f));
            Text gameWonText = CreateUiText(gameWonPanel.transform, "GameWonHeadline", new Vector2(0f, 60f), new Vector2(1000f, 220f), 56, TextAnchor.MiddleCenter, anchor: new Vector2(0.5f, 0.5f));
            Button playAgainButton = CreateUiButton(gameWonPanel.transform, "PlayAgainButton", new Vector2(0f, -120f), new Vector2(360f, 70f), "Play Again", 30);

            // Rules screen shown before the derby starts - explains outs/home-run scoring up
            // front instead of making the player piece it together from in-round toasts alone.
            GameObject startScreenPanel = CreateFullScreenPanel(canvasGO.transform, "StartScreenPanel", new Color(0.03f, 0.02f, 0.06f, 0.92f));
            CreateUiText(startScreenPanel.transform, "StartScreenTitle", new Vector2(0f, 180f), new Vector2(1000f, 90f), 56, TextAnchor.MiddleCenter, anchor: new Vector2(0.5f, 0.5f)).text = "HALLOWEEN DERBY";
            CreateUiText(startScreenPanel.transform, "StartScreenRules", new Vector2(0f, 30f), new Vector2(1000f, 220f), 28, TextAnchor.MiddleCenter, anchor: new Vector2(0.5f, 0.5f)).text =
                "You have 5 outs per stage.\n\nAny swing that isn't a home run counts as an out - taking a pitch is free.\n\nHome runs are worth double points.";
            Button startButton = CreateUiButton(startScreenPanel.transform, "StartButton", new Vector2(0f, -140f), new Vector2(360f, 70f), "Start", 30);

            GameObject hudGO = new GameObject("HUDController");
            var hud = hudGO.AddComponent<HUDController>();
            SetPrivateField(hud, "distanceText", distanceText);
            SetPrivateField(hud, "outsText", outsText);
            SetPrivateField(hud, "stageText", stageText);
            SetPrivateField(hud, "scoreText", scoreText);
            SetPrivateField(hud, "tutorialPanel", tutorialPanel);
            SetPrivateField(hud, "tutorialText", tutorialText);
            SetPrivateField(hud, "stageClearPanel", stageClearPanel);
            SetPrivateField(hud, "stageClearText", stageClearText);
            SetPrivateField(hud, "continueButton", continueButton);
            SetPrivateField(hud, "gameWonPanel", gameWonPanel);
            SetPrivateField(hud, "gameWonText", gameWonText);
            SetPrivateField(hud, "playAgainButton", playAgainButton);
            SetPrivateField(hud, "startScreenPanel", startScreenPanel);
            SetPrivateField(hud, "startButton", startButton);
        }

        private static GameObject CreateFullScreenPanel(Transform parent, string name, Color backdropColor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = backdropColor;
            return go;
        }

        private static Button CreateUiButton(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string label, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.65f, 0.25f, 0.05f, 0.95f); // warm pumpkin-orange, matches theme
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.4f, 0.1f, 1f);
            colors.pressedColor = new Color(0.45f, 0.15f, 0.02f, 1f);
            button.colors = colors;

            CreateUiText(go.transform, name + "_Label", Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, anchor: new Vector2(0.5f, 0.5f)).text = label;
            return button;
        }

        private static Text CreateUiText(Transform parent, string name, Vector2 anchoredPos, Vector2 size, int fontSize, TextAnchor alignment, Vector2? anchor = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Vector2 anchorPoint = anchor ?? new Vector2(0.5f, 1f);
            rect.anchorMin = anchorPoint;
            rect.anchorMax = anchorPoint;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = "";
            return text;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"SceneBuilder: field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }
            field.SetValue(target, value);
        }
    }
}
