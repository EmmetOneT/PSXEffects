using UnityEngine;

namespace RetroDimensions.PSXEffects
{
    [AddComponentMenu("")]
    public sealed class PSXEffectsDemo : MonoBehaviour
    {
        public PSXEffectController[] effects;
        public bool showControls = true;
        private float intensity = 1f;
        private float size = 1f;
        private float fps = 12f;
        private bool useLight = true;
        private int palette;
        private float lastIntensity = -1f;
        private float lastSize = -1f;
        private int lastFPS = -1;
        private int lastPalette = -1;
        private bool lastUseLight;

        private void OnGUI()
        {
            if (!showControls) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            float scale = Mathf.Clamp(Screen.width / 1280f, 0.6f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(16f, 16f, 300f, 360f), GUI.skin.box);
            GUILayout.Label("RETRO DIMENSIONS");
            GUILayout.Label("PSX EFFECTS PACK / v0.1 prototype");
            GUILayout.Space(8f);
            GUILayout.Label("Intensity: " + intensity.ToString("0.0"));
            intensity = GUILayout.HorizontalSlider(intensity, 0f, 3f);
            GUILayout.Label("Size: " + size.ToString("0.0"));
            size = GUILayout.HorizontalSlider(size, 0.4f, 1.8f);
            GUILayout.Label("Flame animation: " + Mathf.RoundToInt(fps) + " FPS");
            fps = GUILayout.HorizontalSlider(fps, 1f, 24f);
            useLight = GUILayout.Toggle(useLight, "Fire lights");
            palette = GUILayout.Toolbar(palette, new[] { "Original", "Blue", "Green" });
            GUILayout.Space(6f);
            if (GUILayout.Button("Restart particles"))
            {
                if (effects != null)
                    foreach (PSXEffectController effect in effects)
                        if (effect != null) effect.Restart();
            }
            GUILayout.Label("Left: fire / Middle: fire + smoke");
            GUILayout.Label("Right: smoke");
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void Update()
        {
            if (effects == null) return;
            int frameRate = Mathf.RoundToInt(fps);
            if (intensity == lastIntensity && size == lastSize && frameRate == lastFPS &&
                palette == lastPalette && useLight == lastUseLight) return;
            Color colour = palette == 1 ? new Color(0.2f, 0.6f, 1f) :
                new Color(0.3f, 1f, 0.25f);
            foreach (PSXEffectController effect in effects)
            {
                if (effect == null) continue;
                effect.intensity = intensity;
                effect.size = size;
                effect.flameFPS = frameRate;
                effect.useLight = useLight;
                effect.overrideColour = palette != 0;
                effect.effectColour = colour;
                effect.ApplySettings();
            }
            lastIntensity = intensity;
            lastSize = size;
            lastFPS = frameRate;
            lastPalette = palette;
            lastUseLight = useLight;
        }
    }
}
