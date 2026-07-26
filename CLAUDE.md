# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity project for a physically realistic long-range rifle shooting simulator. The goal is external-ballistics accuracy (drag from real drag models, wind, Coriolis, spin drift, gyroscopic stability) rather than an arcade hitscan shooter.

- Engine: Unity **6000.3.14f1** (must match `ProjectSettings/ProjectVersion.txt` — open with this exact editor version).
- Render pipeline: URP 17.3.0.
- Input: the **new Input System exclusively** (`activeInputHandler: 1` in `ProjectSettings/ProjectSettings.asset`). There is no `.inputactions` asset — code polls `Mouse.current` / `Keyboard.current` (`UnityEngine.InputSystem`) directly. Do not use the legacy `UnityEngine.Input` class, it is disabled.
- No `.asmdef` files exist anywhere — every script compiles into the default `Assembly-CSharp`, which auto-references all installed packages.
- Not a git repository. There is no CI, no lint config, and no test assemblies set up yet (the `com.unity.test-framework` package is installed but unused).

## Commands

There is no CLI build/lint/test pipeline — this is a plain Unity Editor project.

- **Open the project**: launch via Unity Hub, or open `S:\Git\RealShooter` directly in the Unity Editor (version 6000.3.14f1).
- **Run**: press Play in the Editor. Controls: WASD move, mouse look, left click fires, right click toggles the scope, mouse wheel zooms while scoped, Escape releases the cursor.
- **Regenerate the test scene**: Unity menu `RealShooter > Crear Escena de Prueba de Balística`. This is an editor script (`ShooterScripts/Editor/TestSceneSetup.cs`) that builds `Assets/Scenes/BallisticsTestScene.unity` from scratch every time it's run (ground, 4 targets at 100/300/600/900m, player, all managers wired via `SerializedObject`, HUD). It never touches `SampleScene`; re-run it any time scene wiring needs to be reset.
- **Create ammo/weapon presets**: Project window → right-click → `Create > RealShooter > Ballistics > Bullet Data` / `Weapon Data`.

## Architecture

Code lives in two top-level folders, each its own C# namespace:

- `Assets/ShooterScripts/` → namespace `RealShooter.Ballistics` (with a `.Visuals` sub-namespace), plus `RealShooter.UI` and `RealShooter.EditorTools`.
- `Assets/PlayerScripts/` → namespace `RealShooter.Player`.

### Ballistics simulation core (`ShooterScripts/`)

The simulation is a deliberately separated set of systems that only talk to each other through explicit references or C# events — never through Unity physics callbacks:

- **`Data/`** — `BulletData` and `WeaponData` are `ScriptableObject`s holding all physical parameters (mass, diameter, length, muzzle velocity + SD, ballistic coefficient, drag model, twist rate, twist direction). `DragTables` holds the standard G1/G7 Mach→Cd reference tables used industry-wide (JBM, Applied Ballistics, GNU Ballistics all ship the same data). A bullet's *actual* Cd is `standardCd(mach) * FormFactor`, where `FormFactor = SectionalDensity(imperial lb/in²) / BC` — this is the standard `i = SD/BC` relation, so real published G1/G7 BCs (always in imperial units) can be plugged in directly even though the rest of the sim is SI.
- **`Physics/`** — `BallisticsMath` is a static, side-effect-free library (gravity by latitude/altitude, Coriolis, drag acceleration, Miller gyroscopic stability factor, Litz spin-drift formula). `Projectile` is a **plain C# class, not a MonoBehaviour** — it's the in-flight state of one fired bullet. `PhysicsManager` is the only MonoBehaviour here: it owns the list of active `Projectile`s, integrates each one with **RK4** every `FixedUpdate` (gravity + drag + Coriolis as continuous forces, spin drift applied afterward as an empirical lateral position correction — see below), and does impact detection via a swept `Physics.Raycast` per substep against a configurable `impactLayerMask` whitelist (the projectile has no Collider/Rigidbody, so this is the only way it detects hits, and it also avoids tunneling through thin geometry). It exposes `ProjectileFired`, `ProjectileDespawned`, and `ProjectileHit` events — this is the intended integration point for any new system (audio, damage, VFX); don't poll `ActiveProjectiles` from outside if an event will do.
- **`Weather/`** — `WeatherManager` owns all atmospheric/geographic state (temperature, pressure, humidity, altitude, wind, latitude) and derives air density and speed of sound from it. `PhysicsManager` only *reads* from `WeatherManager` through its public getters; it never owns or mutates atmospheric state. This split was an explicit requirement, keep it that way when extending either system.
- **`Visuals/`** — `ProjectileVisual` syncs a spawned GameObject's transform to its `Projectile`'s simulated position each `LateUpdate` (needed because `Projectile` isn't a MonoBehaviour and can't be moved directly). `ProjectileVisualManager` subscribes to `PhysicsManager`'s fired/despawned events, spawns a visual (a user-supplied prefab, or a scaled default sphere + `TrailRenderer` if none is assigned) and lets the trail fade out (`TrailRenderer.autodestruct`) instead of cutting it off.
- **`UI/`** — `BallisticsHudController` is a diagnostics-only overlay (speed/distance/time-of-flight of the last fired bullet), also driven by the same `PhysicsManager` events. It's a dev/test tool, not meant to ship.
- **`Editor/`** — `TestSceneSetup`, editor-only tooling (see Commands above).

### Player (`PlayerScripts/PlayerShooterController.cs`)

A single `MonoBehaviour` (per explicit design choice — not split into separate look/move/shoot components) requiring a `CharacterController`. Handles mouse look (yaw on the player root, pitch on the child camera), WASD movement via `CharacterController.SimpleMove` (gravity handled automatically), firing (calls `PhysicsManager.Fire(bulletData, weaponData, origin, cameraTransform.forward)`), and the scope: right-click toggles an `isScoped` bool that drives `Camera.fieldOfView` between `minScopedFieldOfView`/`maxScopedFieldOfView`; the mouse wheel adjusts the FOV within that range while scoped; **the zoom level is intentionally not reset when un-scoping**, so it persists to the next activation. Mouse look sensitivity is scaled down proportionally while scoped (otherwise aiming at high zoom is uncontrollable) — this is treated as part of making the scope usable, not a separate feature.

### Deliberate simplifications (do not "fix" without discussion)

- **No Rigidbody/PhysX for bullets.** Trajectories are fully custom-integrated because PhysX's drag model can't represent velocity-dependent Cd curves, Coriolis, or spin drift. The projectile visual has no Collider either — impacts are detected via manual raycasting in `PhysicsManager`.
- **Magnus force vs. spin drift.** `BallisticsMath.GetMagnusAcceleration` exists but is intentionally **not** wired into the integration loop. Spin drift (the empirical Litz formula, applied as a position correction after RK4 integration) already represents the macroscopic result of the Magnus/gyroscopic coupling; using both would double-count the same physical effect. Only revisit this if moving to a full 6-DOF attitude model.
- **World axis convention**: X = East, Y = Up, Z = North is assumed by the Coriolis and wind calculations. If a scene uses a different orientation, Coriolis/wind results will be wrong relative to real-world compass directions.
