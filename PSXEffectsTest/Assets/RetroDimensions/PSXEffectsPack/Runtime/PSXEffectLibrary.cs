using System;
using UnityEngine;

namespace RetroDimensions.PSXEffects
{
    [CreateAssetMenu(menuName = "Retro Dimensions/PSX Effects/Effect Library", fileName = "PSX_World_Effects_Library")]
    public sealed class PSXEffectLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string category;
            public string displayName;
            [TextArea] public string description;
            public GameObject prefab;
            public bool oneShot;
            public float focusHeight = 1f;
            public float cameraDistance = 6f;
            public Vector3 previewRotation;
        }
        public Entry[] entries = new Entry[0];
    }
}
