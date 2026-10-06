using UnityEditor;
using UnityEngine;

namespace RetroDimensions.PSXEffects.Editor
{
    [CustomEditor(typeof(PSXEffectController)), CanEditMultipleObjects]
    public sealed class PSXEffectControllerEditor : UnityEditor.Editor
    {
        private void OnEnable() { Undo.undoRedoPerformed += RefreshEffects; }
        private void OnDisable() { Undo.undoRedoPerformed -= RefreshEffects; }

        private void RefreshEffects()
        {
            foreach (Object item in targets)
                if (item != null) ((PSXEffectController)item).ApplySettings();
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Size and intensity use captured prefab defaults. " +
                "Animation FPS changes looping flipbooks, while gameplay stays at its normal frame rate.",
                MessageType.Info);
            if (DrawDefaultInspector())
            {
                foreach (Object item in targets)
                {
                    PSXEffectController controller = (PSXEffectController)item;
                    Undo.RecordObject(controller.transform, "Change PSX Effect");
                    foreach (ParticleSystem system in controller.GetComponentsInChildren<ParticleSystem>(true))
                        Undo.RecordObject(system, "Change PSX Effect");
                    foreach (Light light in controller.GetComponentsInChildren<Light>(true))
                        Undo.RecordObject(light, "Change PSX Effect");
                    controller.ApplySettings();
                }
            }

            if (Application.isPlaying && GUILayout.Button("Play / Restart effect"))
                foreach (Object item in targets) ((PSXEffectController)item).Restart();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Advanced particle editing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Set Size and Intensity to 1 before editing child rates and burst counts. " +
                "Capture the defaults afterwards. Existing generated prefabs are kept when the builder runs again.",
                MessageType.None);
            bool canCapture = !Application.isPlaying;
            foreach (Object item in targets)
            {
                PSXEffectController controller = (PSXEffectController)item;
                canCapture &= Mathf.Approximately(controller.intensity, 1f) &&
                    Mathf.Approximately(controller.size, 1f);
            }
            using (new EditorGUI.DisabledScope(!canCapture))
            {
                if (GUILayout.Button("Capture current particle defaults"))
                {
                    foreach (Object item in targets)
                    {
                        Undo.RecordObject(item, "Capture PSX Particle Defaults");
                        ((PSXEffectController)item).CaptureDefaults();
                        EditorUtility.SetDirty(item);
                    }
                }
            }
        }
    }
}
