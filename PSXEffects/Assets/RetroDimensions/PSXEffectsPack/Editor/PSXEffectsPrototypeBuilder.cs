using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RetroDimensions.PSXEffects.Editor
{
    public static class PSXEffectsPrototypeBuilder
    {
        private static string builderScriptGuid;
        internal static string Root
        {
            get
            {
                string scriptPath = string.IsNullOrEmpty(builderScriptGuid) ? string.Empty :
                    AssetDatabase.GUIDToAssetPath(builderScriptGuid);
                if (string.IsNullOrEmpty(scriptPath))
                {
                    string foundGuid = null;
                    foreach (string guid in AssetDatabase.FindAssets("PSXEffectsPrototypeBuilder t:MonoScript", new[] { "Assets" }))
                    {
                        string candidate = AssetDatabase.GUIDToAssetPath(guid);
                        if (!candidate.EndsWith("/Editor/PSXEffectsPrototypeBuilder.cs", StringComparison.Ordinal)) continue;
                        if (foundGuid != null)
                            throw new InvalidOperationException("More than one PSX Effects prototype builder was found. Keep one installed copy.");
                        foundGuid = guid;
                        scriptPath = candidate;
                    }
                    if (foundGuid == null)
                        throw new DirectoryNotFoundException("Cannot locate the installed PSX Effects prototype builder under Assets.");
                    builderScriptGuid = foundGuid;
                }
                // Derive the pack location from the imported script, including nested or renamed folders.
                return Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)).Replace('\\', '/');
            }
        }
        internal static string Generated { get { return Root + "/Generated"; } }
        private const string ShaderName = "RetroDimensions/PSX Effects/Particles URP";
        private const string Menu = "Tools/Retro Dimensions/PSX Effects Pack/";
        private static string ScenePath { get { return Root + "/Samples/Demo/Scenes/PSX_Effects_Demo.unity"; } }
        private static readonly string[] PrefabNames = { "PSX_Fire", "PSX_Fire_Smoke", "PSX_Smoke" };

        [MenuItem(Menu + "Build Prototype Assets", false, 10)]
        public static void BuildAssets()
        {
            try
            {
                BuildAssetsInternal();
                Debug.Log("PSX Effects v0.1: assets ready in " + Generated + "/Prefabs.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem(Menu + "Create Prototype Demo", false, 11)]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play Mode before creating the PSX Effects demo.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                GameObject[] prefabs = BuildAssetsInternal();
                if (File.Exists(ScenePath))
                {
                    EditorSceneManager.OpenScene(ScenePath);
                    Debug.Log("Opened the existing PSX Effects demo. Your scene edits were kept.");
                    return;
                }

                EnsureFolder(Root + "/Samples/Demo/Scenes");
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.32f, 0.36f, 0.42f);
                RenderSettings.fog = false;
                Material floorMaterial = EnsureStageMaterial();
                AddBlock("Demo floor", new Vector3(0f, -0.2f, 0f), new Vector3(12f, 0.2f, 9f), floorMaterial);

                PSXEffectController[] controllers = new PSXEffectController[prefabs.Length];
                for (int i = 0; i < prefabs.Length; i++)
                {
                    float x = (i - 1) * 2.1f;
                    AddBlock("Effect stand " + (i + 1), new Vector3(x, 0.04f, 0f),
                        new Vector3(1.55f, 0.18f, 1.55f), floorMaterial);
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene);
                    instance.transform.position = new Vector3(x, 0.13f, 0f);
                    controllers[i] = instance.GetComponent<PSXEffectController>();
                }

                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.045f, 0.075f);
                camera.fieldOfView = 45f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 80f;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                cameraObject.transform.position = new Vector3(3.8f, 3.1f, -8.5f);
                cameraObject.transform.LookAt(new Vector3(0f, 1.3f, 0f));

                GameObject lightObject = new GameObject("Demo fill light", typeof(Light));
                Light fill = lightObject.GetComponent<Light>();
                fill.type = LightType.Directional;
                fill.color = new Color(0.78f, 0.84f, 1f);
                fill.intensity = 0.45f;
                fill.shadows = LightShadows.None;
                lightObject.transform.rotation = Quaternion.Euler(48f, -30f, 0f);

                PSXEffectsDemo demo = new GameObject("Demo Controls").AddComponent<PSXEffectsDemo>();
                demo.effects = controllers;
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save the PSX Effects demo scene.");
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = controllers[1].gameObject;
                Debug.Log("PSX Effects v0.1: demo created. Press Play. " +
                    "Use the on-screen controls or the effect controller Inspector.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal static GameObject[] BuildAssetsInternal()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building PSX Effects assets.");
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null || pipeline.GetType().Name.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Use a Unity 6 Universal 3D / URP project for this prototype.");
            Shader shader = Shader.Find(ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("The PSX particle shader is missing or has errors. " +
                    "Check the Console and confirm URP is installed before building the prototype.");

            EnsureFolder(Generated + "/Materials");
            EnsureFolder(Generated + "/Prefabs");
            Texture2D fireTexture = LoadTexture("PSX_Fire_8x1.png", 256, 32);
            Texture2D smokeTexture = LoadTexture("PSX_Smoke_8x1.png", 256, 32);
            Texture2D emberTexture = LoadTexture("PSX_Ember.png", 8, 8);
            Material fire = EnsureMaterial("PSX_Fire", shader, fireTexture, false, 6f);
            Material smoke = EnsureMaterial("PSX_Smoke", shader, smokeTexture, false, 5f);
            Material ember = EnsureMaterial("PSX_Ember", shader, emberTexture, true, 6f);

            GameObject[] prefabs = new GameObject[PrefabNames.Length];
            for (int i = 0; i < PrefabNames.Length; i++)
            {
                string path = Generated + "/Prefabs/" + PrefabNames[i] + ".prefab";
                if (File.Exists(path))
                {
                    prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefabs[i] == null) throw new IOException("Cannot load existing prefab: " + path);
                    continue;
                }
                GameObject root = new GameObject(PrefabNames[i]);
                try
                {
                    if (i != 2)
                    {
                        AddFire(root.transform, fire);
                        AddEmbers(root.transform, ember);
                        AddFireLight(root.transform);
                    }
                    if (i != 0) AddSmoke(root.transform, smoke, i == 2);
                    PSXEffectsGroundPlacement.Configure(root);
                    PSXEffectController controller = root.AddComponent<PSXEffectController>();
                    controller.CaptureDefaults();
                    controller.ApplySettings();
                    prefabs[i] = PrefabUtility.SaveAsPrefabAsset(root, path);
                    if (prefabs[i] == null) throw new IOException("Could not save prefab: " + path);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            return prefabs;
        }

        internal static Texture2D LoadTexture(string name, int width, int height)
        {
            string path = Root + "/Runtime/Textures/" + name;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Missing prototype texture: " + path);
            bool needsImport = importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.wrapMode != TextureWrapMode.Clamp || importer.textureType != TextureImporterType.Default ||
                !importer.sRGBTexture || !importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None;
            if (needsImport)
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.anisoLevel = 0;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width != width || texture.height != height)
                throw new InvalidDataException("Unexpected texture dimensions: " + path);
            return texture;
        }

        private static Material EnsureMaterial(string name, Shader shader, Texture2D texture, bool additive, float alphaSteps)
        {
            string path = Generated + "/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            if (File.Exists(path)) throw new IOException("Cannot load existing material: " + path);
            Material material = new Material(shader) { name = name };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaSteps", alphaSteps);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_FogToBlack", additive ? 1f : 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static ParticleSystem NewSystem(Transform parent, string name, Material material,
            Vector3 position, bool worldSpace, float lifeMin, float lifeMax, float sizeMin, float sizeMax,
            float speedMin, float speedMax, float rate, int maxParticles)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.duration = 4f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startColor = Color.white;
            main.maxParticles = maxParticles;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            emission.rateOverDistance = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV
            });
            return system;
        }

        private static void AddFire(Transform parent, Material material)
        {
            ParticleSystem system = NewSystem(parent, "Flames", material, new Vector3(0f, 0.02f, 0f),
                false, 0.65f, 0.95f, 0.7f, 0.95f, 0.02f, 0.07f, 12f, 96);
            ParticleSystem.MainModule main = system.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            SetFade(system, 0.06f, 0.7f, 1f);
            SetSize(system, 0.8f, 1.05f, 0.7f);
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = 8;
            sheet.numTilesY = 1;
            sheet.timeMode = ParticleSystemAnimationTimeMode.FPS;
            sheet.fps = 12f;
            sheet.startFrame = 0f;
        }

        private static void AddSmoke(Transform parent, Material material, bool standalone)
        {
            ParticleSystem system = NewSystem(parent, "Smoke", material,
                new Vector3(0f, standalone ? 0.1f : 0.6f, 0f), true,
                2.2f, 3.2f, standalone ? 0.65f : 0.45f, standalone ? 0.85f : 0.65f,
                0.45f, 0.7f, standalone ? 4f : 3f, 128);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.radius = standalone ? 0.16f : 0.1f;
            shape.angle = 10f;
            ParticleSystem.MainModule main = system.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            SetFade(system, 0.15f, 0.55f, 0.7f);
            SetSize(system, 0.8f, 1.45f, 2f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.7f;
            noise.scrollSpeed = 0.25f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = 8;
            sheet.numTilesY = 1;
            sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 0.999f));
            sheet.cycleCount = 1;
            sheet.startFrame = 0f;
        }

        private static void AddEmbers(Transform parent, Material material)
        {
            ParticleSystem system = NewSystem(parent, "Embers", material, new Vector3(0f, 0.12f, 0f),
                true, 0.5f, 1.2f, 0.045f, 0.075f, 0.65f, 1.05f, 4f, 64);
            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = 0.08f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.angle = 15f;
            SetFade(system, 0.05f, 0.6f, 1f);
            SetSize(system, 1f, 0.7f, 0.2f);
        }

        internal static void SetFade(ParticleSystem system, float fadeInEnd, float fadeOutStart, float opacity)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(opacity, fadeInEnd),
                    new GradientAlphaKey(opacity, fadeOutStart), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule module = system.colorOverLifetime;
            module.enabled = true;
            module.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        internal static void SetSize(ParticleSystem system, float start, float middle, float end)
        {
            ParticleSystem.SizeOverLifetimeModule module = system.sizeOverLifetime;
            module.enabled = true;
            module.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, start), new Keyframe(0.4f, middle), new Keyframe(1f, end)));
        }

        private static void AddFireLight(Transform parent)
        {
            GameObject child = new GameObject("Optional Fire Light", typeof(Light));
            child.transform.SetParent(parent, false);
            child.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            Light light = child.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.38f, 0.12f);
            light.intensity = 1.2f;
            light.range = 3.5f;
            light.shadows = LightShadows.None;
        }

        internal static Material EnsureStageMaterial()
        {
            string path = Generated + "/Materials/PSX_Demo_Stage.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit is unavailable for the demo stage.");
            material = new Material(shader) { name = "PSX_Demo_Stage" };
            material.SetColor("_BaseColor", new Color(0.13f, 0.16f, 0.22f));
            material.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static void AddBlock(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
        }

        internal static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        [MenuItem(Menu + "Validate Prototype", false, 30)]
        public static void ValidatePrototype()
        {
            List<string> errors = new List<string>();
            Shader shader = Shader.Find(ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) errors.Add("Particle shader is missing or has errors.");
            foreach (string textureName in new[] { "PSX_Fire_8x1.png", "PSX_Smoke_8x1.png", "PSX_Ember.png" })
            {
                string path = Root + "/Runtime/Textures/" + textureName;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed)
                    errors.Add(textureName + ": requires Point filtering, no mipmaps and no compression.");
            }
            foreach (string prefabName in PrefabNames)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Prefabs/" + prefabName + ".prefab");
                if (prefab == null) { errors.Add(prefabName + ": missing. Build Prototype Assets first."); continue; }
                PSXEffectsGroundPlacement.Validate(prefab, errors);
                PSXEffectController controller = prefab.GetComponent<PSXEffectController>();
                if (controller == null) errors.Add(prefabName + ": missing effect controller.");
                else if (!new SerializedObject(controller).FindProperty("defaultsCaptured").boolValue)
                    errors.Add(prefabName + ": particle defaults have not been captured.");
                ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length == 0) errors.Add(prefabName + ": no particle systems.");
                foreach (ParticleSystem system in systems)
                {
                    ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                    if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader != shader ||
                        renderer.sharedMaterial.GetTexture("_BaseMap") == null)
                        errors.Add(prefabName + "/" + system.name + ": missing or incorrect particle material.");
                    if (renderer == null) continue;
                    List<ParticleSystemVertexStream> streams = new List<ParticleSystemVertexStream>();
                    renderer.GetActiveVertexStreams(streams);
                    if (streams.Count != 3 || streams[0] != ParticleSystemVertexStream.Position ||
                        streams[1] != ParticleSystemVertexStream.Color || streams[2] != ParticleSystemVertexStream.UV)
                        errors.Add(prefabName + "/" + system.name + ": unexpected shader vertex streams.");
                }
            }
            if (errors.Count > 0)
            {
                Debug.LogError("PSX Effects prototype validation:\n- " + string.Join("\n- ", errors));
                return;
            }
            Debug.Log("PSX Effects prototype asset checks passed. " +
                "Visual playback and your target-platform build still need to be checked in Unity.");
        }
    }
}
