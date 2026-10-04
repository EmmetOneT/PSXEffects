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
    public static class PSXWorldEffectsBuilder
    {
        private const string Menu = "Tools/Retro Dimensions/PSX Effects Pack/";
        private const string ParticleShaderName = "RetroDimensions/PSX Effects/Particles URP";
        private const string DissolveShaderName = "RetroDimensions/PSX Effects/World Dissolve URP";
        private static string Root { get { return PSXEffectsPrototypeBuilder.Root; } }
        private static string Output { get { return Root + "/Generated/WorldBuilding"; } }
        private static string LibraryPath { get { return Output + "/PSX_World_Effects_Library.asset"; } }
        private static string ScenePath { get { return Root + "/Samples/Demo/Scenes/PSX_Effects_WorldBuilding_Demo.unity"; } }
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [Serializable] private sealed class RecipeList { public Recipe[] recipes; }
        [Serializable] private sealed class Recipe
        {
            public string id, category, displayName, description;
            public bool oneShot;
            public float focusHeight, cameraDistance;
            public Vector3 previewRotation;
        }

        [MenuItem(Menu + "Build World Effects", false, 1)]
        public static void BuildAssets()
        {
            try
            {
                PSXEffectLibrary library = BuildInternal();
                Selection.activeObject = library;
                Debug.Log("PSX world effects ready: " + library.entries.Length + " presets. " + Output);
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static Recipe[] ReadRecipes()
        {
            string path = Root + "/Editor/WorldBuildingRecipes.json";
            if (!File.Exists(path)) throw new FileNotFoundException("Missing world effects recipes: " + path);
            RecipeList list = JsonUtility.FromJson<RecipeList>(File.ReadAllText(path));
            if (list == null || list.recipes == null || list.recipes.Length != 44)
                throw new InvalidDataException("Expected 44 world-building recipes.");
            HashSet<string> ids = new HashSet<string>();
            foreach (Recipe recipe in list.recipes)
            {
                if (recipe == null || string.IsNullOrEmpty(recipe.id) || !ids.Add(recipe.id) ||
                    string.IsNullOrEmpty(recipe.category) || string.IsNullOrEmpty(recipe.displayName))
                    throw new InvalidDataException("Recipe IDs, categories and names must be present and unique.");
                foreach (char c in recipe.id + recipe.category)
                    if (!char.IsLetterOrDigit(c)) throw new InvalidDataException("Recipe paths must use letters and numbers.");
            }
            return list.recipes;
        }

        private static PSXEffectLibrary BuildInternal()
        {
            // The prototype builder performs the URP, shader and installed-folder checks.
            GameObject[] originals = PSXEffectsPrototypeBuilder.BuildAssetsInternal();
            Shader dissolve = Shader.Find(DissolveShaderName);
            if (dissolve == null || ShaderUtil.ShaderHasError(dissolve))
                throw new InvalidOperationException("The World Dissolve URP shader is missing or has errors. Check the Console.");
            PSXEffectsPrototypeBuilder.EnsureFolder(Output + "/Materials");
            PSXEffectsPrototypeBuilder.EnsureFolder(Output + "/Prefabs");
            Materials.Clear();
            PrepareMaterials(dissolve);
            Recipe[] recipes = ReadRecipes();
            List<PSXEffectLibrary.Entry> entries = new List<PSXEffectLibrary.Entry>();
            string[] originalNames = { "Original fire", "Original fire and smoke", "Original smoke" };
            string[] originalIds = { "Fire", "Fire_Smoke", "Smoke" };
            for (int i = 0; i < originals.Length; i++)
                entries.Add(new PSXEffectLibrary.Entry
                {
                    id = originalIds[i], category = "Originals", displayName = originalNames[i],
                    description = "The original v0.1 looping prototype.", prefab = originals[i],
                    focusHeight = 1f, cameraDistance = 5f
                });
            foreach (Recipe recipe in recipes)
            {
                string folder = Output + "/Prefabs/" + recipe.category;
                PSXEffectsPrototypeBuilder.EnsureFolder(folder);
                string path = folder + "/PSX_" + recipe.id + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    if (File.Exists(path)) throw new IOException("Cannot load existing prefab: " + path);
                    GameObject root = new GameObject("PSX_" + recipe.id);
                    try
                    {
                        CreateLayers(recipe.id, root.transform);
                        PSXEffectsGroundPlacement.Configure(root);
                        foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            ParticleSystem.MainModule main = system.main;
                            main.loop = !recipe.oneShot;
                            main.prewarm = !recipe.oneShot && main.prewarm;
                            main.playOnAwake = false; // Root controller handles playback and pooling.
                            if (recipe.oneShot)
                            {
                                ParticleSystem.EmissionModule emission = system.emission;
                                emission.rateOverTime = 0f;
                                emission.rateOverDistance = 0f;
                            }
                        }
                        PSXEffectController controller = root.AddComponent<PSXEffectController>();
                        controller.oneShot = recipe.oneShot;
                        controller.autoDestroy = false;
                        controller.flashDuration = recipe.id == "LargeFireball" ? 0.7f : 0.35f;
                        controller.CaptureDefaults();
                        controller.ApplySettings();
                        prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                        if (prefab == null) throw new IOException("Could not save prefab: " + path);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                entries.Add(new PSXEffectLibrary.Entry
                {
                    id = recipe.id, category = recipe.category, displayName = recipe.displayName,
                    description = recipe.description, prefab = prefab, oneShot = recipe.oneShot,
                    focusHeight = recipe.focusHeight, cameraDistance = recipe.cameraDistance,
                    previewRotation = recipe.previewRotation
                });
            }
            PSXEffectLibrary library = AssetDatabase.LoadAssetAtPath<PSXEffectLibrary>(LibraryPath);
            if (library == null)
            {
                if (File.Exists(LibraryPath)) throw new IOException("Cannot load existing library: " + LibraryPath);
                library = ScriptableObject.CreateInstance<PSXEffectLibrary>();
                library.entries = entries.ToArray();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            else
            {
                // Append missing entries while preserving edited metadata and ordering.
                List<PSXEffectLibrary.Entry> combined = new List<PSXEffectLibrary.Entry>(
                    library.entries ?? new PSXEffectLibrary.Entry[0]);
                foreach (PSXEffectLibrary.Entry entry in entries)
                {
                    PSXEffectLibrary.Entry existing = combined.Find(e => e != null && e.id == entry.id);
                    if (existing == null) combined.Add(entry);
                    else if (existing.prefab == null) existing.prefab = entry.prefab;
                }
                library.entries = combined.ToArray();
                EditorUtility.SetDirty(library);
            }
            AssetDatabase.SaveAssets();
            return library;
        }

        private static void PrepareMaterials(Shader dissolve)
        {
            MaterialFor("Fire", "Fire_8x1", 256, 32, Color.white, false);
            MaterialFor("FireJet", "FireJet_8x1", 256, 32, Color.white, false);
            MaterialFor("Smoke", "Smoke_8x1", 256, 32, new Color(0.8f, 0.8f, 0.8f), false);
            MaterialFor("Steam", "Smoke_8x1", 256, 32, new Color(1.7f, 1.8f, 1.9f), false);
            MaterialFor("Poison", "Smoke_8x1", 256, 32, new Color(0.55f, 1.4f, 0.35f), false);
            MaterialFor("Dust", "Smoke_8x1", 256, 32, new Color(1.1f, 0.9f, 0.65f), false);
            MaterialFor("Ember", "Ember", 8, 8, Color.white, true);
            MaterialFor("Spark", "SparkStreak", 8, 16, new Color(1f, 0.65f, 0.23f), true);
            MaterialFor("Electric", "SparkStreak", 8, 16, new Color(0.4f, 0.7f, 1f), true);
            MaterialFor("Blast", "Blast_8x1", 256, 32, Color.white, false);
            MaterialFor("Shockwave", "Shockwave_8x1", 256, 32, new Color(1f, 0.7f, 0.4f), true);
            MaterialFor("Debris", "Debris", 8, 8, Color.white, false);
            MaterialFor("Water", "Droplet", 16, 16, new Color(0.35f, 0.7f, 1f), false);
            MaterialFor("Splash", "Splash_8x1", 256, 32, new Color(0.55f, 0.8f, 1f), false);
            MaterialFor("Ripple", "Ripple_8x1", 256, 32, new Color(0.5f, 0.8f, 1f), false);
            MaterialFor("Foam", "Foam", 16, 16, new Color(0.8f, 0.9f, 1f), false);
            MaterialFor("Bubble", "Bubble", 16, 16, new Color(0.55f, 0.8f, 1f), false);
            MaterialFor("Orb", "EnergyOrb_8x1", 256, 32, new Color(0.4f, 0.7f, 1f), true);
            MaterialFor("Beam", "Beam", 32, 8, new Color(0.35f, 0.65f, 1f), true);
            MaterialFor("Star", "MagicStar", 16, 16, new Color(0.45f, 0.7f, 1f), true);
            MaterialFor("WarmStar", "MagicStar", 16, 16, new Color(1f, 0.8f, 0.35f), true);
            MaterialFor("Healing", "MagicStar", 16, 16, new Color(0.35f, 1f, 0.5f), true);
            MaterialFor("Rune", "RuneRing_8x1", 256, 32, new Color(0.5f, 0.65f, 1f), true);
            MaterialFor("DustSpeck", "DustSpeck", 8, 8, new Color(0.85f, 0.8f, 0.65f), false);
            MaterialFor("Firefly", "EnergyOrb_8x1", 256, 32, new Color(1f, 0.85f, 0.3f), true);
            MaterialFor("Insect", "Insect_8x1", 128, 16, new Color(0.25f, 0.25f, 0.2f), false);
            string path = Output + "/Materials/PSX_World_Dissolve.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (File.Exists(path)) throw new IOException("Cannot load existing material: " + path);
                material = new Material(dissolve) { name = "PSX_World_Dissolve" };
                material.SetTexture("_BaseMap", PSXEffectsPrototypeBuilder.LoadTexture("PSX_Surface.png", 32, 32));
                material.SetTexture("_NoiseMap", PSXEffectsPrototypeBuilder.LoadTexture("PSX_DissolveNoise.png", 16, 16));
                AssetDatabase.CreateAsset(material, path);
            }
            Materials.Add("Dissolve", material);
        }

        private static void MaterialFor(string key, string textureName, int width, int height, Color tint, bool additive)
        {
            Texture2D texture = PSXEffectsPrototypeBuilder.LoadTexture("PSX_" + textureName + ".png", width, height);
            string path = Output + "/Materials/PSX_World_" + key + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (File.Exists(path)) throw new IOException("Cannot load existing material: " + path);
                material = new Material(Shader.Find(ParticleShaderName)) { name = "PSX_World_" + key };
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_AlphaSteps", 6f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                material.SetFloat("_FogToBlack", additive ? 1f : 0f);
                AssetDatabase.CreateAsset(material, path);
            }
            Materials.Add(key, material);
        }

        private static ParticleSystem Emit(Transform parent, string name, string material, Vector3 position,
            float rate, float life, float size, float speed, int capacity = 256, bool world = true)
        {
            ParticleSystem system = PSXEffectsPrototypeBuilder.NewSystem(parent, name, Materials[material], position,
                world, life * 0.8f, life * 1.2f, size * 0.8f, size * 1.2f,
                Mathf.Min(speed * 0.8f, speed * 1.2f), Mathf.Max(speed * 0.8f, speed * 1.2f), rate, capacity);
            PSXEffectsPrototypeBuilder.SetFade(system, 0.08f, 0.65f, 1f);
            PSXEffectsPrototypeBuilder.SetSize(system, 0.7f, 1f, 0.25f);
            return system;
        }

        private static void Sheet(ParticleSystem system, bool looping)
        {
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = 8; sheet.numTilesY = 1;
            sheet.timeMode = looping ? ParticleSystemAnimationTimeMode.FPS : ParticleSystemAnimationTimeMode.Lifetime;
            sheet.fps = 12f;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 0.999f));
            sheet.startFrame = 0f; sheet.cycleCount = 1;
        }

        private static void Cone(ParticleSystem system, float radius, float angle, Vector3 rotation)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = radius; shape.angle = angle; shape.rotation = rotation;
        }

        private static void Box(ParticleSystem system, Vector3 scale)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = scale;
            shape.rotation = Vector3.zero;
        }

        private static void Sphere(ParticleSystem system, float radius, bool surface = false)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius; shape.radiusThickness = surface ? 0f : 1f;
        }

        private static void Burst(ParticleSystem system, int count, float delay = 0f)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = false; main.prewarm = false;
            main.duration = Mathf.Max(1f, delay + 0.1f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(delay, (short)count) });
        }

        private static void Noise(ParticleSystem system, float strength, float frequency = 0.7f)
        {
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true; noise.strength = strength; noise.frequency = frequency;
            noise.scrollSpeed = 0.3f; noise.quality = ParticleSystemNoiseQuality.Low;
        }

        private static void Gravity(ParticleSystem system, float gravity)
        { ParticleSystem.MainModule main = system.main; main.gravityModifier = gravity; }

        private static void WaterFlight(ParticleSystem system, float life, float speed)
        {
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = life; main.startSpeed = speed;
            PSXEffectsPrototypeBuilder.SetSize(system, 1f, 1f, 1f);
            PSXEffectsPrototypeBuilder.SetFade(system, 0.015f, 0.92f, 1f);
        }

        private static void Spin(ParticleSystem system, float speed)
        {
            ParticleSystem.MainModule main = system.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-speed, speed);
        }

        private static void Light(Transform parent, Color colour, float intensity, float range, float height)
        {
            UnityEngine.Light light = new GameObject("Optional effect light").AddComponent<UnityEngine.Light>();
            light.transform.SetParent(parent, false); light.transform.localPosition = Vector3.up * height;
            light.type = LightType.Point; light.color = colour; light.intensity = intensity;
            light.range = range; light.shadows = LightShadows.None;
        }

        private static ParticleSystem Fire(Transform root, Vector3 position, float size, float rate, float radius)
        {
            ParticleSystem system = Emit(root, "Flames", "Fire", position, rate, 0.75f, size, 0.06f, 256, false);
            Cone(system, radius, 6f, new Vector3(-90f, 0f, 0f));
            ParticleSystem.MainModule main = system.main; main.startRotation = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            PSXEffectsPrototypeBuilder.SetFade(system, 0.05f, 0.75f, 1f);
            PSXEffectsPrototypeBuilder.SetSize(system, 0.8f, 1.05f, 0.7f);
            Sheet(system, true);
            return system;
        }

        private static ParticleSystem Smoke(Transform root, string material, Vector3 position,
            float rate, float size, float speed, float life, float radius, float opacity = 0.65f)
        {
            ParticleSystem system = Emit(root, material + " puffs", material, position, rate, life, size, speed, 384);
            Cone(system, radius, 12f, new Vector3(-90f, 0f, 0f));
            PSXEffectsPrototypeBuilder.SetSize(system, 0.6f, 1.2f, 1.8f);
            PSXEffectsPrototypeBuilder.SetFade(system, 0.12f, 0.5f, opacity);
            Noise(system, 0.15f); Sheet(system, false);
            return system;
        }

        private static void FireJet(Transform root)
        {
            // Narrow moving wisps form a continuous stream along the source's +X axis.
            // The dedicated texture has no upright flame base to appear inverted.
            ParticleSystem system = Emit(root, "Jet plume", "FireJet", Vector3.up * 0.7f,
                42f, 0.42f, 0.24f, 3.2f, 192, false);
            Cone(system, 0.035f, 4f, new Vector3(0f, 90f, 0f));
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 3.6f);
            main.startRotation = 0f;
            PSXEffectsPrototypeBuilder.SetFade(system, 0.035f, 0.55f, 1f);
            PSXEffectsPrototypeBuilder.SetSize(system, 0.7f, 1.2f, 0.25f);
            Sheet(system, true);
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.2f;
            renderer.velocityScale = 0.1f;
            renderer.cameraVelocityScale = 0f;
            renderer.pivot = Vector3.zero;
            Smoke(root, "Smoke", new Vector3(1.5f, 0.75f, 0f), 2f, 0.25f, 0.2f, 1.2f, 0.06f, 0.3f);
            Light(root, new Color(1f, 0.4f, 0.1f), 1f, 4f, 0.7f);
        }

        private static ParticleSystem Sparks(Transform root, Vector3 position, string material,
            float rate, float speed, float gravity = 0.7f)
        {
            ParticleSystem system = Emit(root, "Sparks", material, position, rate, 0.7f, 0.075f, speed, 256);
            Cone(system, 0.06f, 40f, new Vector3(-90f, 0f, 0f)); Gravity(system, gravity);
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 0.6f; renderer.velocityScale = 0.12f;
            return system;
        }

        private static ParticleSystem Ring(Transform root, string material, Vector3 position,
            float size, float life, float rate, bool horizontal, bool loopingSheet = false)
        {
            ParticleSystem system = Emit(root, "Ring", material, position, rate, life, size, 0f, 64, false);
            ParticleSystem.ShapeModule shape = system.shape; shape.enabled = false;
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = life; main.startSize = size;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.alignment = ParticleSystemRenderSpace.Local;
            if (horizontal) system.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Sheet(system, loopingSheet);
            PSXEffectsPrototypeBuilder.SetSize(system, 1f, 1f, 1f);
            PSXEffectsPrototypeBuilder.SetFade(system, 0.04f, 0.7f, 1f);
            return system;
        }

        private static void FireScene(Transform root, float size, float radius, float smokeRate, float emberRate)
        {
            Fire(root, Vector3.up * 0.03f, size, 12f, radius);
            if (smokeRate > 0f) Smoke(root, "Smoke", Vector3.up * size * 0.7f,
                smokeRate, size * 0.6f, size * 0.45f, 3f, radius);
            if (emberRate > 0f)
            {
                ParticleSystem embers = Emit(root, "Embers", "Ember", Vector3.up * 0.1f,
                    emberRate, 1.2f, size * 0.07f, size * 1.1f);
                Cone(embers, radius, 22f, new Vector3(-90f, 0f, 0f)); Gravity(embers, 0.08f);
            }
            Light(root, new Color(1f, 0.4f, 0.12f), Mathf.Clamp(size, 0.2f, 2f), 2f + size * 2f, size * 0.5f);
        }

        private static void Blast(Transform root, float scale)
        {
            ParticleSystem fire = Emit(root, "Expanding fireball", "Blast", Vector3.up * scale * 0.6f,
                0f, 0.85f, 2f * scale, 0.08f, 64, false);
            Sphere(fire, scale * 0.18f); Sheet(fire, false); Burst(fire, scale > 1f ? 5 : 2);
            PSXEffectsPrototypeBuilder.SetSize(fire, 0.65f, 1f, 1.25f);
            ParticleSystem smoke = Smoke(root, "Smoke", Vector3.up * scale * 0.4f,
                0f, scale, scale * 0.8f, 2.6f, scale * 0.25f, 0.8f);
            Burst(smoke, scale > 1f ? 14 : 6, 0.12f);
            ParticleSystem sparks = Sparks(root, Vector3.up * scale * 0.35f, "Ember", 0f, scale * 3f, 0.35f);
            Burst(sparks, scale > 1f ? 32 : 14);
            ParticleSystem ring = Ring(root, "Shockwave", Vector3.up * 0.035f, scale * 4f, 0.8f, 0f, true);
            Burst(ring, 1);
            Light(root, new Color(1f, 0.55f, 0.2f), scale * 2f, scale * 4f, scale * 0.6f);
        }

        private static ParticleSystem Stars(Transform root, string material, Vector3 position,
            float rate, float life, float radius, float speed)
        {
            ParticleSystem system = Emit(root, "Motes", material, position, rate, life, 0.1f, speed);
            Sphere(system, radius); Noise(system, 0.06f); Spin(system, 0.4f);
            return system;
        }

        private static void Beam(Transform root, Vector3 position, Vector3 end, float jitter, float width)
        {
            Transform holder = new GameObject("Beam connection").transform;
            holder.SetParent(root, false); holder.localPosition = position;
            PSXBeam beam = holder.gameObject.AddComponent<PSXBeam>();
            beam.localEnd = end; beam.jitter = jitter;
            for (int i = 0; i < 2; i++)
            {
                LineRenderer line = new GameObject(i == 0 ? "Beam halo" : "Beam core").AddComponent<LineRenderer>();
                line.transform.SetParent(holder, false);
                line.sharedMaterial = Materials["Beam"]; line.useWorldSpace = false;
                line.positionCount = 2; line.SetPosition(0, Vector3.zero); line.SetPosition(1, end);
                line.widthMultiplier = i == 0 ? width : width * 0.35f;
                line.startColor = line.endColor = i == 0 ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                line.textureMode = LineTextureMode.Stretch; line.alignment = LineAlignment.View;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                line.numCapVertices = 0; line.numCornerVertices = 0;
            }
        }

        private static void SampleDissolve(Transform root, bool reveal, float duration)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Sample prop - replace with your mesh";
            block.transform.SetParent(root, false); block.transform.localPosition = Vector3.up * 0.6f;
            block.transform.localScale = Vector3.one * 0.85f;
            UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
            Renderer renderer = block.GetComponent<Renderer>(); renderer.sharedMaterial = Materials["Dissolve"];
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            PSXObjectDissolve dissolve = root.gameObject.AddComponent<PSXObjectDissolve>();
            dissolve.targets = new[] { renderer }; dissolve.reveal = reveal; dissolve.duration = duration;
        }

        private static void CreateLayers(string id, Transform root)
        {
            ParticleSystem system;
            switch (id)
            {
                case "Candle": FireScene(root, 0.22f, 0.012f, 0f, 0f); break;
                case "Torch": FireScene(root, 0.7f, 0.04f, 1.5f, 3f); break;
                case "Campfire": FireScene(root, 1.1f, 0.22f, 3f, 5f); break;
                case "Bonfire":
                    FireScene(root, 2f, 0.45f, 5f, 10f);
                    Fire(root, new Vector3(0.35f, 0f, 0.1f), 1.3f, 6f, 0.15f);
                    Fire(root, new Vector3(-0.3f, 0f, -0.1f), 1.5f, 6f, 0.15f); break;
                case "FireJet":
                    FireJet(root); break;
                case "GroundFlames":
                    system = Fire(root, Vector3.zero, 0.55f, 25f, 0.8f);
                    Cone(system, 0.8f, 2f, new Vector3(-90f, 0f, 0f));
                    Stars(root, "Ember", Vector3.up * 0.1f, 5f, 0.8f, 0.7f, 0.4f);
                    Light(root, new Color(1f, 0.4f, 0.1f), 0.8f, 4f, 0.4f); break;
                case "BurningDebris":
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = new Vector3(Mathf.Cos(i * 1.7f) * 0.65f, 0.03f, Mathf.Sin(i * 1.7f) * 0.65f);
                        Fire(root, p, 0.45f + i * 0.1f, 5f, 0.05f);
                        Smoke(root, "Smoke", p + Vector3.up * 0.4f, 1f, 0.45f, 0.45f, 2.5f, 0.06f);
                    }
                    Light(root, new Color(1f, 0.4f, 0.1f), 1f, 4f, 0.5f); break;
                case "ThinSmoke": Smoke(root, "Smoke", Vector3.up * 0.1f, 2f, 0.4f, 0.4f, 3f, 0.06f, 0.35f); break;
                case "ThickSmoke": Smoke(root, "Smoke", Vector3.up * 0.15f, 8f, 0.85f, 0.5f, 3.5f, 0.25f, 0.85f); break;
                case "ChimneyPlume": Smoke(root, "Smoke", Vector3.up * 0.1f, 5f, 0.7f, 1f, 4.5f, 0.1f, 0.65f); break;
                case "Steam": Smoke(root, "Steam", Vector3.up * 0.1f, 5f, 0.65f, 0.45f, 2f, 0.16f, 0.45f); break;
                case "VentSteam":
                    system = Smoke(root, "Steam", Vector3.up * 0.7f, 9f, 0.4f, 1.8f, 1.2f, 0.04f, 0.55f);
                    Cone(system, 0.04f, 8f, new Vector3(0f, 90f, 0f)); break;
                case "PoisonCloud":
                    system = Smoke(root, "Poison", Vector3.up * 0.3f, 6f, 0.8f, 0.04f, 3.5f, 0.9f, 0.5f);
                    Box(system, new Vector3(1.6f, 0.25f, 1.6f)); Noise(system, 0.08f); break;
                case "SparkBurst": system = Sparks(root, Vector3.up * 0.6f, "Spark", 0f, 2.8f); Burst(system, 28); break;
                case "WeldingSparks":
                    system = Sparks(root, Vector3.up * 0.8f, "Spark", 24f, 2.2f);
                    Cone(system, 0.02f, 24f, new Vector3(0f, 90f, 0f));
                    Light(root, new Color(1f, 0.75f, 0.4f), 0.7f, 2.5f, 0.8f); break;
                case "ElectricalSparks":
                    Beam(root, Vector3.up * 0.7f, new Vector3(0.9f, 0.1f, 0f), 0.16f, 0.065f);
                    system = Sparks(root, new Vector3(0.4f, 0.7f, 0f), "Electric", 10f, 0.7f, 0.05f);
                    Sphere(system, 0.3f); Light(root, new Color(0.4f, 0.65f, 1f), 0.65f, 2.5f, 0.7f); break;
                case "EmberTrail":
                    system = Emit(root, "Distance-based embers", "Ember", Vector3.up * 0.3f, 2f, 1.6f, 0.09f, 0.2f);
                    { ParticleSystem.EmissionModule emission = system.emission; emission.rateOverDistance = 12f; }
                    Sphere(system, 0.12f); Noise(system, 0.1f); break;
                case "SmallBlast": Blast(root, 0.75f); break;
                case "LargeFireball": Blast(root, 1.8f); break;
                case "DebrisBurst":
                    system = Emit(root, "Stone chips", "Debris", Vector3.up * 0.2f, 0f, 1.2f, 0.13f, 2.5f);
                    Cone(system, 0.3f, 65f, new Vector3(-90f, 0f, 0f)); Gravity(system, 0.8f); Spin(system, 4f); Burst(system, 28);
                    system = Smoke(root, "Dust", Vector3.up * 0.1f, 0f, 0.6f, 0.3f, 1.4f, 0.6f, 0.5f);
                    Burst(system, 8); break;
                case "Shockwave": system = Ring(root, "Shockwave", Vector3.up * 0.04f, 4f, 1f, 0f, true); Burst(system, 1); break;
                case "Splash":
                    system = Emit(root, "Splash flipbook", "Splash", Vector3.up * 0.3f, 0f, 0.65f, 1.3f, 0f, 64, false);
                    { ParticleSystem.ShapeModule shape = system.shape; shape.enabled = false; }
                    Sheet(system, false); Burst(system, 1);
                    system = Emit(root, "Splash droplets", "Water", Vector3.up * 0.15f, 0f, 0.85f, 0.1f, 1.8f);
                    Cone(system, 0.15f, 50f, new Vector3(-90f, 0f, 0f)); Gravity(system, 0.55f); Burst(system, 14);
                    system = Ring(root, "Ripple", Vector3.up * 0.025f, 2f, 1.1f, 0f, true); Burst(system, 1); break;
                case "Ripples": Ring(root, "Ripple", Vector3.up * 0.025f, 2.2f, 1.8f, 0.8f, true); break;
                case "Drips":
                    system = Emit(root, "Falling drips", "Water", Vector3.up * 1.7f, 1.2f, 0.68f, 0.1f, 0.05f);
                    Cone(system, 0.015f, 1f, new Vector3(90f, 0f, 0f)); Gravity(system, 0.75f);
                    WaterFlight(system, 0.68f, 0.05f);
                    Ring(root, "Ripple", Vector3.up * 0.025f, 1f, 0.8f, 1.2f, true); break;
                case "FountainSpray":
                    system = Emit(root, "Fountain water", "Water", Vector3.up * 0.1f, 32f, 1.2f, 0.12f, 3.8f);
                    Cone(system, 0.12f, 20f, new Vector3(-90f, 0f, 0f)); Gravity(system, 0.65f);
                    WaterFlight(system, 1.2f, 3.8f);
                    system = Emit(root, "Surface foam", "Foam", Vector3.up * 0.035f, 7f, 0.7f, 0.3f, 0.05f);
                    Box(system, new Vector3(1.3f, 0.03f, 1.3f));
                    Ring(root, "Ripple", Vector3.up * 0.025f, 2.3f, 1.1f, 1.5f, true); break;
                case "Bubbles":
                    system = Emit(root, "Rising bubbles", "Bubble", Vector3.up * 0.1f, 9f, 3f, 0.15f, 0.5f);
                    Box(system, new Vector3(1.2f, 0.1f, 1.2f)); Noise(system, 0.1f);
                    PSXEffectsPrototypeBuilder.SetSize(system, 0.5f, 0.9f, 1.1f); break;
                case "SmallWaterfall":
                    system = Emit(root, "Falling curtain", "Water", Vector3.up * 2.4f, 45f, 0.85f, 0.15f, 1.2f);
                    Box(system, new Vector3(0.9f, 0.03f, 0.12f));
                    { ParticleSystem.ShapeModule shape = system.shape; shape.rotation = new Vector3(90f, 0f, 0f); }
                    Gravity(system, 0.65f);
                    WaterFlight(system, 0.72f, 1.2f);
                    system = Emit(root, "Foam at base", "Foam", Vector3.up * 0.08f, 12f, 0.65f, 0.4f, 0.08f);
                    Box(system, new Vector3(1.1f, 0.05f, 0.45f));
                    Smoke(root, "Steam", Vector3.up * 0.15f, 3f, 0.55f, 0.2f, 1f, 0.35f, 0.25f); break;
                case "GlowingOrb":
                    system = Emit(root, "Orb", "Orb", Vector3.up, 2f, 1f, 0.9f, 0f, 64, false);
                    { ParticleSystem.ShapeModule shape = system.shape; shape.enabled = false; }
                    Sheet(system, true);
                    PSXEffectMotion motion = system.gameObject.AddComponent<PSXEffectMotion>();
                    motion.bobHeight = 0.12f; motion.bobSpeed = 0.7f;
                    Stars(root, "Star", Vector3.up, 9f, 1.8f, 0.6f, 0.06f);
                    Light(root, new Color(0.4f, 0.65f, 1f), 0.65f, 3f, 1f); break;
                case "EnergyBeam":
                    Beam(root, Vector3.up * 0.8f, new Vector3(0f, 0f, 3f), 0.04f, 0.25f);
                    Stars(root, "Star", Vector3.up * 0.8f, 8f, 0.8f, 0.15f, 0.2f);
                    Light(root, new Color(0.35f, 0.65f, 1f), 0.5f, 3f, 0.8f); break;
                case "MagicBurst":
                    system = Stars(root, "Star", Vector3.up * 0.8f, 0f, 1f, 0.15f, 1.3f); Burst(system, 32);
                    system = Ring(root, "Rune", Vector3.up * 0.04f, 2.2f, 0.8f, 0f, true); Burst(system, 1);
                    PSXEffectsPrototypeBuilder.SetSize(system, 0.25f, 0.9f, 1.3f); break;
                case "ChargingEffect":
                    system = Stars(root, "Star", Vector3.up, 22f, 1.2f, 1.1f, -0.9f);
                    Sphere(system, 1.1f, true);
                    system = Emit(root, "Charging core", "Orb", Vector3.up, 2f, 1f, 0.5f, 0f, 64, false);
                    { ParticleSystem.ShapeModule shape = system.shape; shape.enabled = false; }
                    Sheet(system, true); Light(root, new Color(0.4f, 0.65f, 1f), 0.6f, 3f, 1f); break;
                case "Portal":
                    system = Ring(root, "Rune", Vector3.up * 1.1f, 2.4f, 1.2f, 1.5f, false, true);
                    system.gameObject.AddComponent<PSXEffectMotion>().angularVelocity = new Vector3(0f, 0f, 24f);
                    system = Stars(root, "Star", Vector3.up * 1.1f, 18f, 1.4f, 0.85f, 0.04f);
                    { ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                      velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.orbitalZ = 0.8f; }
                    Light(root, new Color(0.45f, 0.6f, 1f), 0.7f, 3f, 1.1f); break;
                case "Teleport":
                    system = Ring(root, "Rune", Vector3.up * 0.04f, 2.4f, 0.85f, 0f, true); Burst(system, 1);
                    system = Emit(root, "Vertical teleport motes", "Star", Vector3.up * 0.1f, 0f, 1.1f, 0.14f, 1.7f);
                    Cone(system, 0.6f, 4f, new Vector3(-90f, 0f, 0f)); Burst(system, 40);
                    Light(root, new Color(0.4f, 0.65f, 1f), 1f, 3f, 0.8f); break;
                case "PickupBurst":
                    system = Stars(root, "WarmStar", Vector3.up * 0.7f, 0f, 0.85f, 0.08f, 0.9f); Burst(system, 24); break;
                case "HealingGlow":
                    system = Emit(root, "Healing motes", "Healing", Vector3.up * 0.1f, 12f, 1.8f, 0.1f, 0.55f);
                    Cone(system, 0.65f, 6f, new Vector3(-90f, 0f, 0f)); Noise(system, 0.04f);
                    Light(root, new Color(0.35f, 1f, 0.5f), 0.3f, 2.5f, 0.7f); break;
                case "ObjectDissolve": SampleDissolve(root, false, 1.6f); break;
                case "Spawn":
                    SampleDissolve(root, true, 1f);
                    system = Stars(root, "Star", Vector3.up * 0.6f, 0f, 1f, 0.4f, 0.35f); Burst(system, 24); break;
                case "Despawn":
                    SampleDissolve(root, false, 0.8f);
                    system = Stars(root, "Star", Vector3.up * 0.6f, 0f, 1f, 0.3f, 0.8f); Burst(system, 24); break;
                case "FloatingDust":
                    system = Emit(root, "Dust volume", "DustSpeck", Vector3.up, 9f, 4f, 0.07f, 0.015f);
                    Box(system, new Vector3(2.4f, 1.6f, 2f)); Noise(system, 0.045f);
                    PSXEffectsPrototypeBuilder.SetSize(system, 0.7f, 1f, 0.7f); break;
                case "FootstepDust":
                    system = Smoke(root, "Dust", Vector3.up * 0.07f, 0f, 0.38f, 0.16f, 0.7f, 0.22f, 0.45f);
                    Burst(system, 5); break;
                case "ColdBreath":
                    system = Smoke(root, "Steam", Vector3.up, 1.1f, 0.24f, 0.55f, 0.8f, 0.025f, 0.35f);
                    Cone(system, 0.025f, 14f, Vector3.zero); break;
                case "Fireflies":
                    system = Emit(root, "Glowing insects", "Firefly", Vector3.up, 4f, 4.5f, 0.1f, 0.06f);
                    Box(system, new Vector3(2.2f, 1.4f, 2.2f)); Noise(system, 0.15f); Sheet(system, true); break;
                case "InsectSwarm":
                    system = Emit(root, "Winged insects", "Insect", Vector3.up, 7f, 3f, 0.12f, 0.05f);
                    Sphere(system, 0.65f); Noise(system, 0.12f); Sheet(system, true);
                    { ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                      velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.orbitalY = 1.2f; }
                    break;
                case "FallingDebris":
                    system = Emit(root, "Falling chips", "Debris", Vector3.up * 2f, 3f, 0.8f, 0.08f, 0.05f);
                    Box(system, new Vector3(1.2f, 0.1f, 1.2f)); Gravity(system, 0.65f); Spin(system, 3f);
                    system = Smoke(root, "Dust", Vector3.up * 1.7f, 1.5f, 0.25f, -0.2f, 1.2f, 0.3f, 0.3f); break;
                default: throw new InvalidDataException("No effect implementation for recipe: " + id);
            }
        }

        [MenuItem(Menu + "Create World Effects Demo", false, 2)]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("Exit Play Mode before creating the PSX world effects demo."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                PSXEffectLibrary library = BuildInternal();
                if (File.Exists(ScenePath))
                { EditorSceneManager.OpenScene(ScenePath); Debug.Log("Opened the existing world effects demo. Scene edits were kept."); return; }
                PSXEffectsPrototypeBuilder.EnsureFolder(Root + "/Samples/Demo/Scenes");
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.35f, 0.4f, 0.5f); RenderSettings.fog = false;
                Material stage = PSXEffectsPrototypeBuilder.EnsureStageMaterial();
                PSXEffectsPrototypeBuilder.AddBlock("Gallery floor", new Vector3(0f, -0.18f, 0f), new Vector3(16f, 0.3f, 16f), stage);
                PSXEffectsPrototypeBuilder.AddBlock("Effect plinth", new Vector3(0f, -0.055f, 0f), new Vector3(3.5f, 0.1f, 3.5f), stage);
                Transform anchor = new GameObject("Preview spawn point").transform;
                Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f); camera.fieldOfView = 42f;
                camera.nearClipPlane = 0.03f; camera.farClipPlane = 80f; camera.allowHDR = false; camera.allowMSAA = false;
                camera.transform.position = new Vector3(2f, 2.5f, -6f); camera.transform.LookAt(Vector3.up);
                UnityEngine.Light fill = new GameObject("Gallery fill light").AddComponent<UnityEngine.Light>();
                fill.type = LightType.Directional; fill.color = new Color(0.75f, 0.85f, 1f); fill.intensity = 0.7f;
                fill.shadows = LightShadows.None; fill.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
                PSXWorldEffectsDemo demo = new GameObject("World Effects Browser").AddComponent<PSXWorldEffectsDemo>();
                demo.library = library; demo.spawnPoint = anchor; demo.previewCamera = camera;
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save the world effects demo.");
                AssetDatabase.SaveAssets(); Selection.activeGameObject = demo.gameObject;
                Debug.Log("PSX Effects v0.2.6: world-building demo created. Press Play and select an effect from the browser.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem(Menu + "Validate World Effects", false, 20)]
        public static void ValidateAssets()
        {
            try
            {
                List<string> errors = new List<string>();
                foreach (string shaderName in new[] { ParticleShaderName, DissolveShaderName })
                {
                    Shader shader = Shader.Find(shaderName);
                    if (shader == null || ShaderUtil.ShaderHasError(shader)) errors.Add(shaderName + ": shader missing or has errors.");
                }
                PSXEffectLibrary library = AssetDatabase.LoadAssetAtPath<PSXEffectLibrary>(LibraryPath);
                if (library == null || library.entries == null) { errors.Add("Build World Effects first."); }
                else
                {
                    HashSet<string> ids = new HashSet<string>();
                    foreach (PSXEffectLibrary.Entry entry in library.entries)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.id)) { errors.Add("Empty library entry."); continue; }
                        if (!ids.Add(entry.id)) errors.Add(entry.id + ": duplicate entry.");
                        if (entry.prefab == null) { errors.Add(entry.id + ": missing prefab."); continue; }
                        PSXEffectsGroundPlacement.Validate(entry.prefab, errors);
                        PSXEffectController controller = entry.prefab.GetComponent<PSXEffectController>();
                        if (controller == null || !new SerializedObject(controller).FindProperty("defaultsCaptured").boolValue)
                            errors.Add(entry.id + ": missing controller or captured defaults.");
                        else if (controller.oneShot != entry.oneShot) errors.Add(entry.id + ": playback metadata does not match controller.");
                        foreach (Renderer renderer in entry.prefab.GetComponentsInChildren<Renderer>(true))
                            if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null ||
                                renderer.sharedMaterial.GetTexture("_BaseMap") == null)
                                errors.Add(entry.id + "/" + renderer.name + ": missing material or texture.");
                        foreach (ParticleSystem system in entry.prefab.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            if (entry.oneShot && (system.main.loop || system.emission.burstCount == 0))
                                errors.Add(entry.id + "/" + system.name + ": triggered layers must have a burst and must not loop.");
                            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                            List<ParticleSystemVertexStream> streams = new List<ParticleSystemVertexStream>();
                            if (renderer != null) renderer.GetActiveVertexStreams(streams);
                            if (streams.Count != 3 || streams[0] != ParticleSystemVertexStream.Position ||
                                streams[1] != ParticleSystemVertexStream.Color || streams[2] != ParticleSystemVertexStream.UV)
                                errors.Add(entry.id + "/" + system.name + ": incorrect vertex streams.");
                        }
                    }
                    foreach (Recipe recipe in ReadRecipes()) if (!ids.Contains(recipe.id)) errors.Add(recipe.id + ": not in library.");
                    foreach (string original in new[] { "Fire", "Fire_Smoke", "Smoke" })
                        if (!ids.Contains(original)) errors.Add(original + ": missing original entry.");
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Runtime/Textures" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                        importer.textureCompression != TextureImporterCompression.Uncompressed)
                        errors.Add(path + ": use Point filtering, no mipmaps and no compression.");
                }
                if (errors.Count > 0) Debug.LogError("PSX world effects validation:\n- " + string.Join("\n- ", errors));
                else Debug.Log("PSX world effects asset checks passed. Check appearance and a target-platform build in Unity next.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
