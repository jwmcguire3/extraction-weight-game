# Phase 1 Feel Notes

Manual feel verification was not performed from this Codex session because the environment is headless and does not provide an interactive Unity editor/game view.

Automated verification from the new play mode coverage indicates:

- Light load reaches the 10m mark fastest and preserves sprint pacing.
- Loaded still sprints, but its mobility penalty creates a noticeable slowdown relative to Light.
- Overburdened loses sprint access and lands in a clearly slower travel band.
- Upward breakpoint crossings trigger the expected feedback hooks, and footstep feedback fires while moving.

Recommended manual pass in `Assets/_Project/Scenes/Zones/Drydock.unity`:

- Enter Play Mode.
- Select `Phase1Player`.
- Use the custom inspector buttons on `PlayerController` to apply `Light`, `Loaded`, and `Overburdened` test loot states.
- Confirm that movement cadence, camera heaviness, carry gauge color shifts, stamina pressure, and contextual extract affordances read clearly on device-sized aspect ratios.
