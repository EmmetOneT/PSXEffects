using UnityEngine;

namespace RetroDimensions.PSXEffects
{
    [AddComponentMenu("Retro Dimensions/PSX Effects/Effect Motion")]
    public sealed class PSXEffectMotion : MonoBehaviour
    {
        public Vector3 angularVelocity;
        public float bobHeight;
        public float bobSpeed = 1f;
        [Range(1, 24)] public int motionFPS = 12;
        private Vector3 origin;
        private Quaternion rotation;
        private float started;
        private void Awake() { origin = transform.localPosition; rotation = transform.localRotation; }
        private void OnEnable() { started = Time.time; }
        private void Update()
        {
            float elapsed = Mathf.Floor((Time.time - started) * Mathf.Max(1, motionFPS)) / Mathf.Max(1, motionFPS);
            transform.localRotation = rotation * Quaternion.Euler(angularVelocity * elapsed);
            transform.localPosition = origin + Vector3.up * Mathf.Sin(elapsed * bobSpeed * Mathf.PI * 2f) * bobHeight;
        }
    }
}
