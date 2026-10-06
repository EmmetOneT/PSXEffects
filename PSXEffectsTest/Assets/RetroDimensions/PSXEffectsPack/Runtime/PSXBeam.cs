using UnityEngine;

namespace RetroDimensions.PSXEffects
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Retro Dimensions/PSX Effects/Beam")]
    public sealed class PSXBeam : MonoBehaviour
    {
        public Transform target;
        public Vector3 localEnd = new Vector3(0f, 0f, 3f);
        [Range(2, 32)] public int segments = 12;
        [Range(0f, 0.5f)] public float jitter = 0.04f;
        [Range(1, 24)] public int motionFPS = 8;
        private LineRenderer[] lines;
        private int lastTick = int.MinValue;

        private void Awake() { lines = GetComponentsInChildren<LineRenderer>(true); }
        private void OnEnable() { lastTick = int.MinValue; }

        private void LateUpdate()
        {
            int tick = Mathf.FloorToInt(Time.time * Mathf.Max(1, motionFPS));
            if (tick == lastTick && target == null) return;
            lastTick = tick;
            if (lines == null) lines = GetComponentsInChildren<LineRenderer>(true);
            Vector3 end = target != null ? transform.InverseTransformPoint(target.position) : localEnd;
            int count = Mathf.Clamp(segments, 2, 32);
            foreach (LineRenderer line in lines)
            {
                if (line == null) continue;
                line.useWorldSpace = false;
                line.positionCount = count;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (count - 1f);
                    float envelope = Mathf.Sin(t * Mathf.PI);
                    float dx = (Mathf.PerlinNoise(i * 1.71f, tick * 0.23f) - 0.5f) * 2f;
                    float dy = (Mathf.PerlinNoise(i * 2.31f + 7f, tick * 0.31f) - 0.5f) * 2f;
                    Vector3 point = Vector3.Lerp(Vector3.zero, end, t) + new Vector3(dx, dy, 0f) * jitter * envelope;
                    line.SetPosition(i, line.transform.InverseTransformPoint(transform.TransformPoint(point)));
                }
            }
        }
    }
}
