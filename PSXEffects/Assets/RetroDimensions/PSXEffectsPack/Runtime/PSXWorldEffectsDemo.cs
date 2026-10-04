using System.Collections.Generic;
using UnityEngine;

namespace RetroDimensions.PSXEffects
{
    [AddComponentMenu("")]
    public sealed class PSXWorldEffectsDemo : MonoBehaviour
    {
        public PSXEffectLibrary library;
        public Transform spawnPoint;
        public Camera previewCamera;
        public bool showControls = true;
        private readonly List<string> categories = new List<string>();
        private string[] categoryLabels;
        private int category;
        private int selected = -1;
        private Vector2 scroll;
        private GameObject instance;
        private PSXEffectController controller;
        private float intensity = 1f;
        private float size = 1f;
        private int fps = 12;
        private bool lights = true;
        private int palette;
        private float zoom = 1f;
        private float angle = -32f;
        private bool authoredOverride;
        private Color authoredColour;
        private string[] paletteLabels = { "Original", "Blue", "Green" };
        private readonly Color[] paletteColours =
        {
            Color.white, new Color(0.25f, 0.6f, 1f), new Color(0.3f, 1f, 0.3f)
        };
        private float lastIntensity = -1f, lastSize = -1f;
        private int lastFPS = -1, lastPalette = -1;
        private bool lastLights;

        private void Start()
        {
            categories.Add("All");
            if (library == null || library.entries == null) return;
            foreach (PSXEffectLibrary.Entry entry in library.entries)
                if (entry != null && !categories.Contains(entry.category)) categories.Add(entry.category);
            categoryLabels = categories.ToArray();
            if (library.entries.Length > 0) Select(0);
        }

        public void Select(int index)
        {
            if (library == null || library.entries == null || index < 0 || index >= library.entries.Length) return;
            PSXEffectLibrary.Entry entry = library.entries[index];
            if (entry == null || entry.prefab == null) return;
            ConfigurePalette(entry);
            if (instance != null) { instance.SetActive(false); Destroy(instance); }
            selected = index;
            Transform anchor = spawnPoint != null ? spawnPoint : transform;
            instance = Instantiate(entry.prefab, anchor.position, Quaternion.Euler(entry.previewRotation), anchor);
            controller = instance.GetComponent<PSXEffectController>();
            if (controller != null)
            {
                controller.autoDestroy = false;
                intensity = controller.intensity;
                size = controller.size;
                fps = controller.flameFPS;
                lights = controller.useLight;
                authoredOverride = controller.overrideColour;
                authoredColour = controller.effectColour;
            }
            palette = 0;
            zoom = 1f;
            lastIntensity = -1f;
            lastSize = -1f;
            lastFPS = -1;
            lastPalette = -1;
        }

        private void ConfigurePalette(PSXEffectLibrary.Entry entry)
        {
            bool blueOriginal = entry.category == "Water" || entry.category == "Energy" ||
                entry.id == "ElectricalSparks" || entry.id == "Spawn" || entry.id == "Despawn";
            bool greenOriginal = entry.id == "PoisonCloud" || entry.id == "HealingGlow";
            Color yellow = new Color(1f, 0.82f, 0.18f);
            paletteLabels[1] = blueOriginal ? "Yellow" : "Blue";
            paletteLabels[2] = greenOriginal ? "Yellow" : "Green";
            paletteColours[1] = blueOriginal ? yellow : new Color(0.25f, 0.6f, 1f);
            paletteColours[2] = greenOriginal ? yellow : new Color(0.3f, 1f, 0.3f);
        }

        private void Update()
        {
            if (controller != null && (intensity != lastIntensity || size != lastSize || fps != lastFPS ||
                palette != lastPalette || lights != lastLights))
            {
                controller.intensity = intensity;
                controller.size = size;
                controller.flameFPS = fps;
                controller.useLight = lights;
                controller.overrideColour = palette == 0 ? authoredOverride : true;
                controller.effectColour = palette == 0 ? authoredColour : paletteColours[palette];
                controller.ApplySettings();
                lastIntensity = intensity; lastSize = size; lastFPS = fps; lastPalette = palette; lastLights = lights;
            }
            if (previewCamera == null || library == null || selected < 0) return;
            PSXEffectLibrary.Entry entry = library.entries[selected];
            if (instance != null && entry.id == "EmberTrail")
                instance.transform.localPosition = new Vector3(Mathf.Sin(Time.time * 1.4f) * 0.8f, 0f,
                    Mathf.Cos(Time.time * 1.4f) * 0.8f);
            Vector3 centre = (spawnPoint != null ? spawnPoint.position : transform.position) + Vector3.up * entry.focusHeight * size;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.35f, -1f).normalized * entry.cameraDistance * zoom;
            previewCamera.transform.position = centre + offset;
            previewCamera.transform.LookAt(centre);
        }

        private void OnGUI()
        {
            if (!showControls || library == null || library.entries == null || categories.Count == 0) return;
            Matrix4x4 oldMatrix = GUI.matrix;
            float scale = Mathf.Clamp(Screen.width / 1440f, 0.65f, 1.4f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float height = Mathf.Min(680f, Screen.height / scale - 32f);
            float width = Screen.width / scale;
            float panelWidth = Mathf.Min(310f, (width - 48f) / 2f);
            GUILayout.BeginArea(new Rect(16f, 16f, panelWidth, height), GUI.skin.box);
            GUILayout.Label("RETRO DIMENSIONS / PSX EFFECTS v0.2.6");
            int newCategory = GUILayout.SelectionGrid(category, categoryLabels, 2);
            if (newCategory != category) { category = newCategory; scroll = Vector2.zero; }
            GUILayout.Space(6f);
            scroll = GUILayout.BeginScrollView(scroll);
            for (int i = 0; i < library.entries.Length; i++)
            {
                PSXEffectLibrary.Entry entry = library.entries[i];
                if (entry == null || (category > 0 && entry.category != categories[category])) continue;
                string prefix = i == selected ? "> " : string.Empty;
                if (GUILayout.Button(prefix + entry.displayName)) Select(i);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(width - panelWidth - 16f, 16f, panelWidth, Mathf.Min(590f, height)), GUI.skin.box);
            if (selected >= 0)
            {
                PSXEffectLibrary.Entry entry = library.entries[selected];
                GUILayout.Label(entry.displayName);
                GUILayout.Label(entry.oneShot ? "Triggered effect" : "Looping effect");
                GUILayout.Label(entry.description, new GUIStyle(GUI.skin.label) { wordWrap = true });
                GUILayout.Space(8f);
            }
            GUILayout.Label("Intensity: " + intensity.ToString("0.0"));
            intensity = GUILayout.HorizontalSlider(intensity, 0f, 3f);
            GUILayout.Label("Size: " + size.ToString("0.0"));
            size = GUILayout.HorizontalSlider(size, 0.3f, 2.2f);
            GUILayout.Label("Loop animation: " + fps + " FPS");
            fps = Mathf.RoundToInt(GUILayout.HorizontalSlider(fps, 1f, 24f));
            lights = GUILayout.Toggle(lights, "Effect lights");
            palette = GUILayout.Toolbar(palette, paletteLabels);
            if (GUILayout.Button("Play / Restart")) { if (controller != null) controller.Play(); }
            GUILayout.Space(8f);
            GUILayout.Label("Camera angle"); angle = GUILayout.HorizontalSlider(angle, -180f, 180f);
            GUILayout.Label("Camera distance"); zoom = GUILayout.HorizontalSlider(zoom, 0.5f, 1.8f);
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }
    }
}
