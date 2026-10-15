using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HalloweenDerby.EditorTools
{
    /// Builds Humanoid-free (Generic rig) Animator Controllers and prefabs for the CC0
    /// characters sourced from Kenney's Graveyard Kit, reusing its shared 32-clip animation
    /// library across the skeleton pitcher, keeper (batter), ghost, and vampire (horseman rider).
    /// All four FBX files share an identical clip set/skeleton, so the same state-machine
    /// topology is rebuilt per-character pointing at that character's own clip sub-assets.
    public static class CharacterSetup
    {
        private const string CharactersDir = "Assets/Models/Characters";
        private const string PropsDir = "Assets/Models/Props";
        private const string AnimDir = "Assets/Animations";
        private const string PrefabDir = "Assets/Prefabs";

        // Clips that should hold/loop while a state is active (windups, idle, locomotion).
        private static readonly string[] LoopingClips = { "idle", "crouch", "holding-right", "holding-both", "interact-right", "walk", "sprint" };

        [MenuItem("Halloween Derby/Build Character Animators And Prefabs")]
        public static void BuildAll()
        {
            Directory_CreateAll();

            ConfigureModelImport("character-skeleton.fbx");
            ConfigureModelImport("character-keeper.fbx");
            ConfigureModelImport("character-ghost.fbx");
            ConfigureModelImport("character-vampire.fbx");
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            BuildPitcherPrefabAndController();
            BuildBatterPrefabAndController();
            BuildGhostPrefabAndController();
            BuildHorsemanPrefabAndController();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CharacterSetup: character animators and prefabs built.");
        }

        private static void Directory_CreateAll()
        {
            System.IO.Directory.CreateDirectory(AnimDir);
            System.IO.Directory.CreateDirectory(PrefabDir);
        }

        private static void ConfigureModelImport(string fbxFileName)
        {
            string path = $"{CharactersDir}/{fbxFileName}";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"CharacterSetup: could not find ModelImporter at {path}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;

            var clipAnimations = importer.clipAnimations;
            if (clipAnimations == null || clipAnimations.Length == 0)
                clipAnimations = importer.defaultClipAnimations;

            for (int i = 0; i < clipAnimations.Length; i++)
            {
                if (LoopingClips.Contains(clipAnimations[i].name))
                    clipAnimations[i].loopTime = true;
            }
            importer.clipAnimations = clipAnimations;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static Dictionary<string, AnimationClip> LoadClips(string fbxFileName)
        {
            string path = $"{CharactersDir}/{fbxFileName}";
            var dict = new Dictionary<string, AnimationClip>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    dict[clip.name] = clip;
            }
            return dict;
        }

        private static GameObject LoadModelRoot(string fbxFileName) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{CharactersDir}/{fbxFileName}");

        // ---------- Pitcher (skeleton) ----------

        private static void BuildPitcherPrefabAndController()
        {
            var clips = LoadClips("character-skeleton.fbx");
            var controller = AnimatorController.CreateAnimatorControllerAtPath($"{AnimDir}/SkeletonPitcherController.controller");
            var sm = controller.layers[0].stateMachine;

            var idle = AddState(sm, "Idle", clips["idle"]);
            sm.defaultState = idle;

            AddTriggerAndOneWayState(controller, sm, "Windup_Fastball_Easy", clips["crouch"]);
            AddTriggerAndOneWayState(controller, sm, "Windup_Fastball", clips["holding-right"]);
            AddTriggerAndOneWayState(controller, sm, "Windup_Curveball", clips["interact-right"]);
            AddTriggerAndOneWayState(controller, sm, "Windup_Changeup", clips["holding-both"]);

            AddTriggerAndReturnToIdleState(controller, sm, "Release", clips["attack-melee-right"], idle);
            AddTriggerAndReturnToIdleState(controller, sm, "Celebrate", clips["emote-yes"], idle);
            AddTriggerAndReturnToIdleState(controller, sm, "Slump", clips["emote-no"], idle);

            var root = (GameObject)PrefabUtility.InstantiatePrefab(LoadModelRoot("character-skeleton.fbx"));
            root.name = "SkeletonPitcher";
            root.transform.localScale = Vector3.one * 2.7f; // source mesh imports ~0.7m tall - scale to human height
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            SavePrefab(root, "SkeletonPitcher");
        }

        // ---------- Batter (keeper) ----------

        private static void BuildBatterPrefabAndController()
        {
            var clips = LoadClips("character-keeper.fbx");
            var controller = AnimatorController.CreateAnimatorControllerAtPath($"{AnimDir}/BatterController.controller");
            var sm = controller.layers[0].stateMachine;

            // "crouch" reads as a ready/athletic stance rather than the flat-footed "idle" -
            // much closer to a batter waiting on a pitch.
            var idle = AddState(sm, "Idle", clips["crouch"]);
            sm.defaultState = idle;

            AddTriggerAndReturnToIdleState(controller, sm, "Swing", clips["attack-melee-right"], idle);

            var root = (GameObject)PrefabUtility.InstantiatePrefab(LoadModelRoot("character-keeper.fbx"));
            root.name = "Batter";
            root.transform.localScale = Vector3.one * 2.1f; // source mesh imports ~0.86m tall - scale to human height
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            AttachBat(root);

            SavePrefab(root, "Batter");
        }

        // ---------- Ghost outfielder ----------

        private static void BuildGhostPrefabAndController()
        {
            var clips = LoadClips("character-ghost.fbx");
            var controller = AnimatorController.CreateAnimatorControllerAtPath($"{AnimDir}/GhostController.controller");
            var sm = controller.layers[0].stateMachine;

            var idle = AddState(sm, "Idle", clips["idle"]);
            sm.defaultState = idle;

            var root = (GameObject)PrefabUtility.InstantiatePrefab(LoadModelRoot("character-ghost.fbx"));
            root.name = "GhostOutfielder";
            root.transform.localScale = Vector3.one * 2.2f; // source mesh imports ~0.66m tall
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            // GhostOutfielder.cs drives world position/bob entirely in code (Drift/Intercept
            // FSM); the Animator just supplies a subtle idle flutter on top of that.
            root.AddComponent<HalloweenDerby.Enemies.GhostOutfielder>();
            AddTriggerCollider(root, radius: 0.8f);

            SavePrefab(root, "GhostOutfielder");
        }

        // ---------- Headless Horseman (boss) ----------

        private static void BuildHorsemanPrefabAndController()
        {
            var clips = LoadClips("character-vampire.fbx");
            var controller = AnimatorController.CreateAnimatorControllerAtPath($"{AnimDir}/HorsemanController.controller");
            var sm = controller.layers[0].stateMachine;

            var idle = AddState(sm, "Idle", clips["idle"]);
            sm.defaultState = idle;

            AddTriggerAndOneWayState(controller, sm, "BossIntro", clips["sprint"]);
            AddTriggerAndReturnToIdleState(controller, sm, "BossDefeated", clips["die"], idle);

            // No CC0 "headless horseman" model exists in any searched source (Kenney/Quaternius/
            // Poly Pizza) - assembled instead from a primitive horse body (literally headless,
            // since there's no head mesh to hide) plus the vampire rider model with its own head
            // renderer disabled. Both pieces get code-driven gallop/idle bob via HorsemanRig.
            GameObject horseman = new GameObject("HeadlessHorseman");

            GameObject horseBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            horseBody.name = "HorseBody";
            horseBody.transform.SetParent(horseman.transform, false);
            horseBody.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            horseBody.transform.localScale = new Vector3(0.9f, 1.6f, 0.9f);
            horseBody.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            ApplyColor(horseBody, new Color(0.08f, 0.07f, 0.09f));

            GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            neck.name = "HorseNeck";
            neck.transform.SetParent(horseman.transform, false);
            neck.transform.localRotation = Quaternion.Euler(0f, 0f, 60f);
            neck.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
            neck.transform.localPosition = new Vector3(0f, 1.7f, 0.65f);
            ApplyColor(neck, new Color(0.08f, 0.07f, 0.09f));

            string[] legNames = { "FrontLeft", "FrontRight", "BackLeft", "BackRight" };
            Vector3[] legOffsets = {
                new Vector3(-0.35f, 0.55f, 0.55f), new Vector3(0.35f, 0.55f, 0.55f),
                new Vector3(-0.35f, 0.55f, -0.5f), new Vector3(0.35f, 0.55f, -0.5f),
            };
            var legs = new List<Transform>();
            for (int i = 0; i < legNames.Length; i++)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                leg.name = $"Leg_{legNames[i]}";
                leg.transform.SetParent(horseman.transform, false);
                leg.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
                leg.transform.localPosition = legOffsets[i];
                ApplyColor(leg, new Color(0.08f, 0.07f, 0.09f));
                legs.Add(leg.transform);
            }

            var rider = (GameObject)PrefabUtility.InstantiatePrefab(LoadModelRoot("character-vampire.fbx"));
            rider.name = "Rider";
            rider.transform.SetParent(horseman.transform, false);
            rider.transform.localScale = Vector3.one * 2.3f; // source mesh imports ~0.8m tall
            rider.transform.localPosition = new Vector3(0f, 1.95f, -0.05f);
            var animator = rider.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            DisableHeadRenderer(rider);

            var rig = horseman.AddComponent<HalloweenDerby.Boss.HorsemanRig>();
            SetPrivateField(rig, "bodyRoot", horseBody.transform);
            SetPrivateField(rig, "neck", neck.transform);
            SetPrivateField(rig, "legs", legs.ToArray());

            // HorsemanBossController has [RequireComponent(typeof(PitchController))], which
            // auto-adds it. Round 6 isn't built yet, so its release/plate points are left
            // unassigned here - whichever round-6 builder wires this prefab into a scene must set those.
            horseman.AddComponent<HalloweenDerby.Boss.HorsemanBossController>();
            var reactions = horseman.AddComponent<HalloweenDerby.Pitching.PitcherReactions>();
            SetPrivateField(reactions, "animator", animator);

            SavePrefab(horseman, "HeadlessHorseman");
        }

        /// No CC0 bat model was sourced, so this builds a simple tapered wooden bat from
        /// primitives and parents it to whatever hand bone it can find on the rig, so it
        /// animates along with the swing instead of sitting static in world space.
        private static void AttachBat(GameObject root)
        {
            Transform hand = FindHandBone(root.transform);
            if (hand == null)
            {
                Debug.LogWarning("CharacterSetup: no hand bone found on Batter rig - bat not attached. Run 'Halloween Derby > Debug > Dump Batter Bone Names' to find the correct bone and wire it manually if needed.");
                return;
            }

            // This rig has no wrist bone - "hand" here is really the whole arm bone. Measured via
            // Halloween Derby > Debug > Dump Batter Rig Transforms: arm-right sits at world
            // Y=0.78 on a 1.81-tall (world bounds 0.05-1.86) character with a 2.1x lossyScale,
            // so a local offset gets multiplied by 2.1 before it becomes world distance.
            // Bat points mostly upward (ready/cocked position) rather than hanging down.
            // Pushed further out from the body and sized up (stylized/cartoony, matching the
            // low-poly blocky art direction) so it clearly reads as a separate held object
            // instead of a thin line lost against the body's silhouette.
            GameObject bat = new GameObject("Bat");
            bat.transform.SetParent(hand, false);
            bat.transform.localPosition = new Vector3(0.18f, 0f, 0.16f);
            bat.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);

            var woodMat = GetOrCreateWoodMaterial();

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "BatHandle";
            handle.transform.SetParent(bat.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            handle.transform.localScale = new Vector3(0.03f, 0.1f, 0.03f);
            Object.DestroyImmediate(handle.GetComponent<CapsuleCollider>());
            handle.GetComponent<MeshRenderer>().sharedMaterial = woodMat;

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            barrel.name = "BatBarrel";
            barrel.transform.SetParent(bat.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            barrel.transform.localScale = new Vector3(0.07f, 0.22f, 0.07f);
            Object.DestroyImmediate(barrel.GetComponent<CapsuleCollider>());
            barrel.GetComponent<MeshRenderer>().sharedMaterial = woodMat;
        }

        /// A plain Shader.Find("...Lit") call inside this editor-script flow was observed to
        /// sometimes resolve to null in a pure -batchmode process (shader not yet warmed up),
        /// which silently bakes a missing-shader (magenta) material into the saved prefab. A
        /// persisted, reused Material asset avoids re-hitting that path on every rebuild.
        private static Material GetOrCreateWoodMaterial()
        {
            const string path = "Assets/Materials/BatWood.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && existing.shader != null && existing.shader.name != "Hidden/InternalErrorShader")
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("CharacterSetup: Shader.Find(\"Universal Render Pipeline/Lit\") returned null - falling back to Standard.");
                shader = Shader.Find("Standard");
            }

            // Bright warm tan + a touch of emission so the bat reads clearly against the dark
            // character/night ambient instead of blending into the shadows.
            var mat = new Material(shader) { color = new Color(0.78f, 0.55f, 0.24f) };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.35f, 0.2f, 0.05f));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Transform FindHandBone(Transform root)
        {
            // This rig (Kenney Halloween kit, "character-keeper.fbx") has no wrist/hand joint -
            // the arm bone is the lowest-level bone on the limb (root > torso > arm-right).
            // Prefer an actual hand bone if a different-sourced model ever has one, else fall
            // back to the right arm bone directly.
            Transform handFallback = null;
            Transform armFallback = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                bool isRight = n.Contains("right") || n.EndsWith("_r") || n.EndsWith(".r");
                if (n.Contains("hand"))
                {
                    if (isRight) return t;
                    handFallback ??= t;
                }
                else if (n.Contains("arm") && isRight)
                {
                    armFallback = t;
                }
            }
            return handFallback ?? armFallback;
        }

        private static void DisableHeadRenderer(GameObject rider)
        {
            foreach (var t in rider.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLowerInvariant().Contains("head"))
                {
                    var renderer = t.GetComponent<Renderer>();
                    if (renderer != null) renderer.enabled = false;
                }
            }
        }

        // ---------- Shared helpers ----------

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            var state = sm.AddState(name);
            state.motion = clip;
            return state;
        }

        /// AnyState --trigger--> state (no return transition - caller's code drives what happens next,
        /// e.g. PitchController always follows a windup trigger with a "Release" trigger).
        private static void AddTriggerAndOneWayState(AnimatorController controller, AnimatorStateMachine sm, string triggerName, AnimationClip clip)
        {
            controller.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);
            var state = AddState(sm, triggerName, clip);
            var transition = sm.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.If, 0, triggerName);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
        }

        /// AnyState --trigger--> state --(exit time)--> idle. For one-shot reactions (release swing,
        /// celebrate, slump) that should automatically return to idle once the clip finishes.
        private static void AddTriggerAndReturnToIdleState(AnimatorController controller, AnimatorStateMachine sm, string triggerName, AnimationClip clip, AnimatorState idle)
        {
            controller.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);
            var state = AddState(sm, triggerName, clip);
            var into = sm.AddAnyStateTransition(state);
            into.AddCondition(AnimatorConditionMode.If, 0, triggerName);
            into.hasExitTime = false;
            into.duration = 0.1f;

            var back = state.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.15f;
        }

        private static void AddTriggerCollider(GameObject go, float radius)
        {
            var collider = go.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = radius;
            collider.center = new Vector3(0f, 1f, 0f);
            if (go.GetComponent<Rigidbody>() == null)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        }

        private static GameObject SavePrefab(GameObject instance, string prefabName)
        {
            string path = $"{PrefabDir}/{prefabName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"CharacterSetup: field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }
            field.SetValue(target, value);
        }
    }
}
