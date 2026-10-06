using System;
using UnityEngine;

namespace RetroDimensions.PSXEffects
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Retro Dimensions/PSX Effects/Effect Controller")]
    public sealed class PSXEffectController : MonoBehaviour
    {
        [Header("Effect")]
        [Range(0f, 4f)] public float intensity = 1f;
        [Range(0.2f, 3f)] public float size = 1f;
        [InspectorName("Animation FPS"), Range(1, 24)] public int flameFPS = 12;
        public bool overrideColour;
        public Color effectColour = new Color(1f, 0.45f, 0.1f, 1f);

        [Header("Playback")]
        public bool oneShot;
        public bool playOnEnable = true;
        [Tooltip("Destroy a triggered effect after its particles and dissolve finish. Leave off for pooling.")]
        public bool autoDestroy;

        [Header("Optional effect light")]
        public bool useLight = true;
        [Range(0f, 0.5f)] public float lightFlicker = 0.18f;
        [Min(0.05f)] public float flashDuration = 0.4f;

        [Serializable]
        private struct EmitterDefaults
        {
            public ParticleSystem system;
            public float rateOverTime;
            public float rateOverDistance;
            public ParticleSystem.MinMaxCurve[] burstCounts;
        }

        [Serializable]
        private struct LineDefaults
        {
            public LineRenderer renderer;
            public float width;
        }

        [SerializeField, HideInInspector] private EmitterDefaults[] emitters;
        [SerializeField, HideInInspector] private LineDefaults[] lineDefaults;
        [SerializeField, HideInInspector] private Vector3 baseScale = Vector3.one;
        [SerializeField, HideInInspector] private Light fireLight;
        [SerializeField, HideInInspector] private float baseLightIntensity;
        [SerializeField, HideInInspector] private float baseLightRange;
        [SerializeField, HideInInspector] private Color baseLightColour;
        [SerializeField, HideInInspector] private bool defaultsCaptured;

        private Renderer[] effectRenderers;
        private PSXObjectDissolve[] dissolves;
        private MaterialPropertyBlock propertyBlock;
        private float previousIntensity = -1f, previousSize = -1f;
        private int previousFPS = -1;
        private bool previousOverride, previousUseLight;
        private Color previousColour;
        private int previousLightTick = int.MinValue;
        private float playTime;
        private bool hasPlayed;

        /// <summary>Capture authored rates and burst counts at intensity=1 and size=1.</summary>
        public void CaptureDefaults()
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            emitters = new EmitterDefaults[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.EmissionModule emission = systems[i].emission;
                ParticleSystem.MinMaxCurve[] counts = new ParticleSystem.MinMaxCurve[emission.burstCount];
                for (int b = 0; b < counts.Length; b++) counts[b] = emission.GetBurst(b).count;
                emitters[i] = new EmitterDefaults
                {
                    system = systems[i], rateOverTime = emission.rateOverTimeMultiplier,
                    rateOverDistance = emission.rateOverDistanceMultiplier, burstCounts = counts
                };
            }
            baseScale = transform.localScale;
            LineRenderer[] lines = GetComponentsInChildren<LineRenderer>(true);
            lineDefaults = new LineDefaults[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                lineDefaults[i] = new LineDefaults { renderer = lines[i], width = lines[i].widthMultiplier };
            fireLight = GetComponentInChildren<Light>(true);
            if (fireLight != null)
            {
                baseLightIntensity = fireLight.intensity;
                baseLightRange = fireLight.range;
                baseLightColour = fireLight.color;
            }
            CacheComponents();
            defaultsCaptured = true;
        }

        private void CacheComponents()
        {
            effectRenderers = GetComponentsInChildren<Renderer>(true);
            dissolves = GetComponentsInChildren<PSXObjectDissolve>(true);
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        }

        private void Awake()
        {
            if (!defaultsCaptured) CaptureDefaults();
            else CacheComponents();
            ApplySettings();
        }

        private void OnEnable()
        {
            previousLightTick = int.MinValue;
            if (playOnEnable) Play();
            else if (defaultsCaptured) ApplySettings();
        }

        private void OnValidate()
        {
            intensity = Mathf.Clamp(intensity, 0f, 4f);
            size = Mathf.Clamp(size, 0.2f, 3f);
            flameFPS = Mathf.Clamp(flameFPS, 1, 24);
            lightFlicker = Mathf.Clamp(lightFlicker, 0f, 0.5f);
            flashDuration = Mathf.Max(0.05f, flashDuration);
        }

        private void Update()
        {
            if (previousIntensity != intensity || previousSize != size || previousFPS != flameFPS ||
                previousOverride != overrideColour || previousColour != effectColour || previousUseLight != useLight)
                ApplySettings();
            UpdateLight();
            if (!autoDestroy || !oneShot || !hasPlayed || Time.time - playTime < 0.1f) return;
            foreach (EmitterDefaults emitter in emitters)
                if (emitter.system != null && emitter.system.IsAlive(false)) return;
            foreach (PSXObjectDissolve dissolve in dissolves)
                if (dissolve != null && dissolve.IsPlaying) return;
            Destroy(gameObject);
        }

        /// <summary>Apply size, density, tint and flipbook FPS without modifying shared materials.</summary>
        public void ApplySettings()
        {
            if (!defaultsCaptured) return;
            if (effectRenderers == null) CacheComponents();
            intensity = Mathf.Clamp(intensity, 0f, 4f);
            size = Mathf.Clamp(size, 0.2f, 3f);
            flameFPS = Mathf.Clamp(flameFPS, 1, 24);
            transform.localScale = baseScale * size;
            if (lineDefaults != null)
                foreach (LineDefaults line in lineDefaults)
                    if (line.renderer != null) line.renderer.widthMultiplier = line.width * size;
            for (int i = 0; i < emitters.Length; i++)
            {
                ParticleSystem system = emitters[i].system;
                if (system == null) continue;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTimeMultiplier = emitters[i].rateOverTime * intensity;
                emission.rateOverDistanceMultiplier = emitters[i].rateOverDistance * intensity;
                ParticleSystem.MinMaxCurve[] counts = emitters[i].burstCounts;
                if (counts != null)
                    for (int b = 0; b < counts.Length && b < emission.burstCount; b++)
                    {
                        ParticleSystem.Burst burst = emission.GetBurst(b);
                        ParticleSystem.MinMaxCurve count = counts[b];
                        if (count.mode == ParticleSystemCurveMode.Constant) count.constant *= intensity;
                        else if (count.mode == ParticleSystemCurveMode.TwoConstants)
                        { count.constantMin *= intensity; count.constantMax *= intensity; }
                        else count.curveMultiplier *= intensity;
                        burst.count = count;
                        emission.SetBurst(b, burst);
                    }
                ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
                if (sheet.enabled && sheet.timeMode == ParticleSystemAnimationTimeMode.FPS) sheet.fps = flameFPS;
            }
            foreach (Renderer renderer in effectRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat("_TintAmount", overrideColour ? 1f : 0f);
                propertyBlock.SetColor("_TintColor", effectColour);
                propertyBlock.SetFloat("_EffectOpacity", renderer is ParticleSystemRenderer ? 1f : Mathf.Clamp01(intensity));
                renderer.SetPropertyBlock(propertyBlock);
            }
            if (fireLight != null)
            {
                fireLight.enabled = useLight && intensity > 0f && (!oneShot || hasPlayed);
                fireLight.range = baseLightRange * size;
                fireLight.color = overrideColour ? effectColour : baseLightColour;
                fireLight.intensity = baseLightIntensity * intensity;
            }
            previousIntensity = intensity; previousSize = size; previousFPS = flameFPS;
            previousOverride = overrideColour; previousColour = effectColour; previousUseLight = useLight;
            previousLightTick = int.MinValue;
        }

        private void UpdateLight()
        {
            if (fireLight == null || !fireLight.enabled) return;
            int tick = Mathf.FloorToInt(Time.time * flameFPS);
            if (tick == previousLightTick) return;
            previousLightTick = tick;
            float sample = Mathf.PerlinNoise(Mathf.Abs(GetInstanceID() % 1000) * 0.031f, tick * 0.23f);
            sample = Mathf.Round(sample * 4f) / 4f;
            float envelope = oneShot ? Mathf.Clamp01(1f - (Time.time - playTime) / Mathf.Max(0.05f, flashDuration)) : 1f;
            fireLight.intensity = baseLightIntensity * intensity * envelope *
                Mathf.Lerp(1f - lightFlicker, 1f + lightFlicker, sample);
        }

        /// <summary>Clear and play all layers. Also restarts the sample prop dissolve.</summary>
        public void Play()
        {
            if (!defaultsCaptured) CaptureDefaults();
            if (dissolves == null) CacheComponents();
            hasPlayed = true;
            playTime = Time.time;
            ApplySettings();
            foreach (EmitterDefaults emitter in emitters)
            {
                if (emitter.system == null) continue;
                emitter.system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                emitter.system.Play(false);
            }
            foreach (PSXObjectDissolve dissolve in dissolves)
                if (dissolve != null) dissolve.Play();
        }

        public void Restart() { Play(); }
    }
}
