# Changelog

## 0.2.6 — Bubble velocity modes

- Set X, Y and Z bubble velocity curves to matching Random Between Two Constants modes.
- Keep the authored upward speed range, with zero horizontal velocity.
- Enable the velocity module only after all three axes are configured.
- Add mismatched velocity curve modes to the normal asset validation report.

## 0.2.5 — Fire Jet and gallery colours

- Replace Fire Jet's upright flame sprites with a dedicated eight-frame jet-wisp texture.
- Rework its emission into a narrow fast stream with a bright core and light smoke at the tip.
- Offer yellow and green alternatives for blue water, electricity and energy effects.
- Offer blue and yellow alternatives for green poison and healing effects.
- Preserve the Original button and unrestricted scene-controller colour overrides.

## 0.2.4 — Direct sprite anchors

- Remove the temporary particle/camera anchor probe that failed during demo generation.
- Set fire and smoke renderer pivots directly, with padding compensation for the fire art.
- Keep the normal builder and validation independent of particle simulation and mesh baking.
- Preserve the original helper filename and the other placement corrections.

## 0.2.3 — Import compatibility

- Keep the original particle setup helper filename so importing over an earlier folder
  replaces its obsolete source instead of leaving a script with a missing method reference.
- Keep placement setup in normal generation with no separate maintenance command.

## 0.2.2 — Clean fresh-install package

- Generate the 47 presets with their corrected placement as part of normal setup.
- Anchor upright flames at their visible base using Unity's measured billboard geometry.
- Keep box emission volumes horizontal, foam flat and bubbles rising.
- Keep ground-flame embers and charging motes above their source floor.
- Restrict low smoke noise to horizontal movement.
- Simplify installation instructions for replacing the previous pack with a fresh copy.
- Remove the separate maintenance command and existing-prefab migration code.

## 0.2 — World building

- Added 44 recipes across fire, smoke, machinery sparks, environmental explosions,
  water, energy, magical feedback and atmosphere (47 presets with the originals).
- Added 17 source textures, including water, rings, energy, insects, debris and dissolve masks.
- Added a category browser demo with one active effect, camera controls and burst replay.
- Extended the controller with burst density, pooled playback, optional cleanup and beam tint/size.
- Added beam endpoints, stepped prop motion and a sample mesh dissolve/reveal shader.
- Kept original asset GUIDs and the folder-discovery fix. Existing generated assets are preserved.
- Excluded weather, weapons, projectiles, muzzle flashes, ricochets, grenades and combat impacts.

## 0.1.1 — Installation path fix

- Resolve the pack root from the imported builder script so nested/renamed folders work.

## 0.1 — Fire and smoke prototype

- Added fire, fire with smoke and smoke presets.
- Added original fire/smoke/ember textures, URP particle shader, controller and demo builder.
