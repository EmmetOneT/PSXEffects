using UnityEngine;

namespace RetroDimensions.PSXEffects
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Retro Dimensions/PSX Effects/Object Dissolve")]
    public sealed class PSXObjectDissolve : MonoBehaviour
    {
        [Tooltip("Use materials with the included World Dissolve URP shader.")]
        public Renderer[] targets;
        public bool reveal;
        [Min(0.05f)] public float duration = 1.2f;
        [Range(1, 24)] public int animationFPS = 12;
        public bool IsPlaying { get; private set; }
        private float began;
        private int lastTick;
        private MaterialPropertyBlock block;

        public void Play()
        {
            if (targets == null || targets.Length == 0) targets = GetComponentsInChildren<Renderer>(true);
            began = Time.time;
            lastTick = int.MinValue;
            IsPlaying = true;
            Apply(reveal ? 1f : 0f);
        }

        private void Update()
        {
            if (!IsPlaying) return;
            float t = Mathf.Clamp01((Time.time - began) / Mathf.Max(0.05f, duration));
            int tick = Mathf.FloorToInt((Time.time - began) * Mathf.Max(1, animationFPS));
            if (tick != lastTick || t >= 1f)
            {
                lastTick = tick;
                Apply(reveal ? 1f - t : t);
            }
            if (t >= 1f) IsPlaying = false;
        }

        private void Apply(float amount)
        {
            if (block == null) block = new MaterialPropertyBlock();
            foreach (Renderer renderer in targets)
            {
                if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty("_Dissolve")) continue;
                renderer.GetPropertyBlock(block);
                block.SetFloat("_Dissolve", amount);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
