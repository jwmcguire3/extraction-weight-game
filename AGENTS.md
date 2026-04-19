## Project overview
Extraction Weight is a Unity mobile extraction game where every item picked up degrades movement, noise, visibility, or handling, making greed the core difficulty knob. Target platforms are Android (minimum Snapdragon 7-gen class devices) and iOS, built with Unity 2023 LTS, URP, and C#. Orientation is still TBD; default assumptions should use landscape until a formal decision is made.

## Architecture conventions
- Use assembly definitions per major system: `Core`, `Weight`, `Loot`, `Zone`, `Threat`, `Extraction`, `UI`, and `Audio`.
- Disallow cross-assembly access except via public interfaces defined in `Core`.
- Use ScriptableObject-driven data for tunable content, including loot definitions, threat profiles, zone configs, and extraction profiles.
- Use Addressables for all runtime-loaded content.
- Do not place runtime content in `Resources/`.
- Use MonoBehaviour only for scene-resident behavior.
- Keep systems and services as plain C# classes.
- Avoid static state except explicit singletons declared in `Core`.

## Weight system invariants (do not violate)
- Weight penalties are modeled on exactly four axes: Noise, Silhouette, Handling, and Mobility.
- Use three breakpoints: Light (0–40%), Loaded (40–80%), and Overburdened (80–100%), with a soft ceiling to 120%.
- Penalty accumulation must be continuous between breakpoints; breakpoints steepen curves and must not apply penalties as discrete steps.
- Every sensory feedback signal must include both audio and visual representation (mobile players may be muted).

## Testing conventions
- Write unit tests (NUnit via Unity Test Framework) for all weight calculations, threat detection math, and extraction gating logic.
- Write play mode tests for end-to-end run flows.
- Place tests in `Tests/` adjacent to the system they cover.
- A task is not done until its tests pass and the changed system’s existing tests still pass.

## Code style
- C# 10 features are allowed.
- Nullable reference types are enabled project-wide.
- Use async/await for work that touches I/O or frame boundaries.
- Do not use LINQ in per-frame update paths.
- Enforce allocation discipline in gameplay code; LINQ is acceptable in editor tooling.
- Naming conventions: PascalCase for types/methods, `_camelCase` for private fields, and camelCase for locals.

## Commit and PR conventions
- Use Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `chore:`.
- Keep PRs scoped to one system when possible.
- PR descriptions must include what changed, how to verify, and test results.

## What to ask before acting
- If scope is unclear, ask before writing code.
- If a change would break a weight system invariant, surface the issue instead of proceeding.
- If tests would need to be removed or weakened, stop and ask.

## Performance budget
- Target 60 FPS on Snapdragon 7-gen 1 class hardware.
- Keep per-frame allocations near zero in gameplay code paths.
- Do not instantiate GameObjects during gameplay except through object pools declared in `Core`.
