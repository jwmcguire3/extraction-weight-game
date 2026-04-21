# Phase 1 Playtest Instructions

## Install

Android:
- Install the provided APK on your device.
- If Android blocks the install, allow installs from the shared source for this session.

iPhone:
- Install the supplied TestFlight build or the provided Xcode-generated build from the organizer.

## What To Do

- Play 3 full Drydock runs.
- Try one cautious run, one greedy run, and one run where you push extraction timing late.
- Use headphones if possible, but play normally if you prefer muted audio.

## What To Send Back

- The telemetry log file from the device.
- A 5-minute post-session interview or voice note covering:
  - When the weight penalties started feeling meaningful.
  - Whether threat detection felt fair or confusing.
  - Whether extraction timing felt readable under pressure.
  - Any performance problems, hitching, or heat/battery issues you noticed.

## Where The Telemetry File Lives

- The build writes JSON-lines telemetry to the app's local persistent-data telemetry folder.
- Each line is one event with a timestamp, run id, event name, and payload.
- Share the `.jsonl` file exactly as written; no editing needed.

## Important Notes

- If the build crashes or locks up, note the run number and what was happening right before it.
- If you notice severe hitching, overheating, or battery drain, stop after the current run and report it.
- Do not worry about winning every run. The goal is to stress the slice and capture honest reactions.
