using System.Collections.Generic;
using UnityEngine;

namespace RetroDimensions.PSXEffects.Editor
{
    internal static class PSXEffectsGroundPlacement
    {
        // Renderer pivot offsets are scaled by each particle's current size.
        // A positive half-size Y offset keeps an upright sprite above its source.
        // The supplied 32px fire frames have two transparent rows at their base.
        private const float BottomPivot = 0.5f;
        private const float FirePivot = BottomPivot - 2f / 32f;

        internal static void Configure(GameObject root)
        {
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer == null || renderer.sharedMaterial == null ||
                    !renderer.sharedMaterial.HasProperty("_BaseMap")) continue;
                Texture texture = renderer.sharedMaterial.GetTexture("_BaseMap");
                if (texture == null) continue;
                string name = texture.name;
                if (name == "PSX_Fire_8x1")
                {
                    bool jet = renderer.renderMode == ParticleSystemRenderMode.Stretch;
                    renderer.pivot = jet ? Vector3.zero : new Vector3(0f, FirePivot, 0f);
                    if (!jet) renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    Upright(system);
                }
                else if (name == "PSX_Smoke_8x1")
                {
                    renderer.pivot = new Vector3(0f, BottomPivot, 0f);
                    Upright(system);
                    HorizontalNoise(system);
                }
                else if (name == "PSX_Foam")
                {
                    renderer.pivot = Vector3.zero;
                    renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                }
                else if (name == "PSX_Bubble")
                {
                    ParticleSystem.MainModule main = system.main;
                    ParticleSystem.MinMaxCurve speed = main.startSpeed;
                    if (Mathf.Abs(speed.constantMin) + Mathf.Abs(speed.constantMax) > 0.0001f)
                    {
                        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                        velocity.enabled = false;
                        velocity.space = ParticleSystemSimulationSpace.Local;
                        // Unity requires matching curve modes for all linear axes.
                        // Keep the upward speed range and zero horizontal movement.
                        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                        velocity.y = new ParticleSystem.MinMaxCurve(speed.constantMin, speed.constantMax);
                        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                        main.startSpeed = 0f;
                        velocity.enabled = true;
                    }
                }
                if (root.name == "PSX_GroundFlames" && name == "PSX_Ember" && system.name == "Motes")
                {
                    ParticleSystem.ShapeModule shape = system.shape;
                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    HorizontalNoise(system);
                }
                if (root.name == "PSX_ChargingEffect" && name == "PSX_MagicStar" && system.name == "Motes")
                {
                    // Its centre is at y=1; keep the spawn shell above the floor.
                    ParticleSystem.ShapeModule shape = system.shape;
                    shape.radius = 0.85f;
                }
            }
        }

        private static void Upright(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.startRotation3D = false;
            main.startRotation = 0f;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = false;
        }

        private static void HorizontalNoise(ParticleSystem system)
        {
            ParticleSystem.NoiseModule noise = system.noise;
            if (!noise.enabled) return;
            if (!noise.separateAxes)
            {
                ParticleSystem.MinMaxCurve strength = noise.strength;
                noise.separateAxes = true;
                noise.strengthX = strength; noise.strengthZ = strength;
            }
            noise.strengthY = 0f;
        }

        internal static void Validate(GameObject root, List<string> errors)
        {
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                if (velocity.enabled && (velocity.x.mode != velocity.y.mode || velocity.z.mode != velocity.y.mode))
                    errors.Add(root.name + "/" + system.name + ": velocity axes have different curve modes.");
                ParticleSystem.ShapeModule shape = system.shape;
                if (shape.enabled && shape.shapeType == ParticleSystemShapeType.Box &&
                    Mathf.Abs(Mathf.DeltaAngle(shape.rotation.x, -90f)) < 0.01f)
                    errors.Add(root.name + "/" + system.name + ": box emission volume should be horizontal.");
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer == null || renderer.sharedMaterial == null ||
                    !renderer.sharedMaterial.HasProperty("_BaseMap")) continue;
                Texture texture = renderer.sharedMaterial.GetTexture("_BaseMap");
                if (texture == null) continue;
                if (texture.name == "PSX_Fire_8x1" && renderer.renderMode != ParticleSystemRenderMode.Stretch &&
                    (renderer.renderMode != ParticleSystemRenderMode.VerticalBillboard ||
                     Mathf.Abs(renderer.pivot.y - FirePivot) > 0.001f))
                    errors.Add(root.name + "/" + system.name + ": flame base is not anchored.");
                if (texture.name == "PSX_Foam" && renderer.renderMode != ParticleSystemRenderMode.HorizontalBillboard)
                    errors.Add(root.name + "/" + system.name + ": surface foam should lie flat.");
            }
        }

    }
}
