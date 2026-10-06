using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroDimensions.PSXEffects.Editor
{
    internal static class PSXEffectsParticleSetup
    {
        private static float? bottomPivot;

        // The 32px fire frames have two transparent rows below the visible flame.
        // Compensate for that padding so the visible base rests on the source.
        private static float FirePivot { get { return BottomPivot * (1f - 4f / 32f); } }

        // Measure Unity's actual billboard geometry so the anchor does not depend
        // on an assumed sign for the Renderer Pivot property.
        private static float BottomPivot
        {
            get
            {
                if (bottomPivot.HasValue) return bottomPivot.Value;
                GameObject sample = new GameObject("PSX temporary anchor probe");
                GameObject cameraObject = new GameObject("PSX temporary anchor camera");
                Mesh mesh = new Mesh();
                sample.hideFlags = cameraObject.hideFlags = HideFlags.HideAndDontSave;
                mesh.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    Camera camera = cameraObject.AddComponent<Camera>();
                    camera.enabled = false;
                    camera.transform.position = new Vector3(0f, 0f, -3f);
                    camera.transform.rotation = Quaternion.identity;
                    ParticleSystem system = sample.AddComponent<ParticleSystem>();
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ParticleSystem.MainModule main = system.main;
                    main.loop = false; main.playOnAwake = false;
                    main.startSize = 1f; main.startLifetime = 10f; main.startSpeed = 0f;
                    main.startRotation = 0f; main.maxParticles = 1;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    ParticleSystem.EmissionModule emission = system.emission; emission.enabled = false;
                    ParticleSystem.ShapeModule shape = system.shape; shape.enabled = false;
                    ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                    renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    renderer.maxParticleSize = 1f;
                    system.Play(false);
                    system.Emit(new ParticleSystem.EmitParams
                    {
                        position = Vector3.zero, velocity = Vector3.zero,
                        startSize = 1f, startLifetime = 10f, rotation = 0f, startColor = Color.white
                    }, 1);
                    system.Pause(false);
                    foreach (float candidate in new[] { 0.5f, -0.5f })
                    {
                        renderer.pivot = new Vector3(0f, candidate, 0f);
                        mesh.Clear();
                        // This overload is available in earlier Unity 6 releases too.
#pragma warning disable CS0618
                        renderer.BakeMesh(mesh, camera, false);
#pragma warning restore CS0618
                        if (mesh.vertexCount == 0) continue;
                        mesh.RecalculateBounds();
                        if (Mathf.Abs(mesh.bounds.min.y) < 0.001f && mesh.bounds.size.y > 0.9f)
                        { bottomPivot = candidate; return candidate; }
                    }
                    throw new InvalidOperationException("Could not measure the fire sprite's bottom anchor. " +
                        "Check the Console and run the demo builder again outside Play Mode.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    UnityEngine.Object.DestroyImmediate(sample);
                }
            }
        }

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
                        velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
                        velocity.y = speed;
                        main.startSpeed = 0f;
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
