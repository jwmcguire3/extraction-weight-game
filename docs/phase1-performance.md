# Phase 1 Performance Audit

Date: 2026-04-21

This pass focused on Phase 1 playtest readiness in the Drydock slice, not feature work.

## Scope

- Target device: Pixel 6a or equivalent mid-tier Android hardware.
- Target frame budget: 16.67 ms average frame time (60 fps sustained).
- Verified in this workspace:
  - EditMode suite passed on 2026-04-21.
  - PlayMode suite passed on 2026-04-21.
  - `PerformanceRegressionTests.DrydockRun_AveragesAtLeast55Fps_AndCapturesExpectedTelemetry` passed with a 61-second Drydock run.
- Not verified in this workspace:
  - Physical-device profiling on Pixel 6a.
  - Android/iOS player builds on this machine.

Why the device profile is still pending:
- The installed Unity 6000.4.3f1 editor on this machine only has `WebGLSupport` and `windowsstandalonesupport` modules under `Editor/Data/PlaybackEngines`.
- Android and iOS build support modules are not installed, so no APK/Xcode output could be generated locally for device deployment from this workspace.

## Hotspots Fixed

### 1. Threat detection cadence and query cost

Before:
- Threats evaluated detection every `Update`.
- Line of sight used `Physics.RaycastAll` plus sort work.

After:
- Threat sensing now runs on a cached 10 Hz cadence in [`Assets/_Project/Threat/ThreatBehaviourBase.cs`](/C:/Users/jwmcg/OneDrive/Documents/AI%20Projects/Live%20Envirnments/extraction-weight-game/Assets/_Project/Threat/ThreatBehaviourBase.cs).
- Line of sight now uses a single raycast in [`Assets/_Project/Threat/DetectionSystem.cs`](/C:/Users/jwmcg/OneDrive/Documents/AI%20Projects/Live%20Envirnments/extraction-weight-game/Assets/_Project/Threat/DetectionSystem.cs).

Impact:
- Roughly 90% fewer threat detection evaluations in steady state.
- Removed `RaycastAll` allocation/sort pressure from the main threat loop.

### 2. Pickup audio source allocation

Before:
- Loot pickup sounds used `AudioSource.PlayClipAtPoint`, which allocates a temporary audio object for one-shots.

After:
- Pickup audio now routes through [`Assets/_Project/Audio/PooledAudioSourcePlayer.cs`](/C:/Users/jwmcg/OneDrive/Documents/AI%20Projects/Live%20Envirnments/extraction-weight-game/Assets/_Project/Audio/PooledAudioSourcePlayer.cs).
- [`Assets/_Project/Loot/LootPickup.cs`](/C:/Users/jwmcg/OneDrive/Documents/AI%20Projects/Live%20Envirnments/extraction-weight-game/Assets/_Project/Loot/LootPickup.cs) uses the pool instead of ad-hoc allocations.

Impact:
- Removes per-pickup audio object churn.
- Lowers GC pressure during loot-heavy runs.

### 3. Tide fog overdraw path

Before:
- Fog segments used full-height volumes throughout the run.

After:
- `PHASE_1` mode flattens rendered fog height to 35% of the original visual height and prefers a simpler color shader path in [`Assets/_Project/Zone/TideController.cs`](/C:/Users/jwmcg/OneDrive/Documents/AI%20Projects/Live%20Envirnments/extraction-weight-game/Assets/_Project/Zone/TideController.cs).

Impact:
- Reduces transparent pixel coverage in the Drydock tide presentation.
- Keeps visual tide feedback while trimming fill-rate cost for Phase 1 playtests.

## Before / After Summary

Because a physical Android build could not be produced on this machine, the table below separates verified code-path changes from still-pending device capture.

| Area | Before | After |
| --- | --- | --- |
| Threat sensing | 60 Hz evaluation per threat with `RaycastAll` LOS | 10 Hz cached evaluation with single-ray LOS |
| Pickup audio | Temporary one-shot audio source allocation | Reused pooled one-shot audio sources |
| Tide fog | Full-height fog volumes | `PHASE_1` flatter fog bands plus simpler shader fallback |
| Physical-device frame timing | Not captured in this workspace | Pending after Android build support is installed and a Pixel 6a profile run is performed |
| Automated regression gate | No dedicated Drydock perf guardrail | 61-second Drydock regression test passes the `>=55 fps` floor in the current headless editor harness |

## Next Physical-Device Step

Once Android build support is installed in Unity Hub:

1. Build the Phase 1 APK with `Tools > Extraction Weight > Build Phase 1 Android APK`.
2. Deploy to a Pixel 6a or equivalent.
3. Capture a profiler trace for a representative 3-run session in Drydock.
4. Replace the pending physical-device row above with measured average frame time and 95th percentile frame time.
