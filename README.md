# HapticNav — Haptic Navigation Bracelet companion app (C#)

Companion phone app for the **Haptic Navigation Bracelet** (Challenge 3 group project).
It guides a blind or visually impaired pedestrian along a route using **vibration only**,
so the user keeps their hearing for traffic and their hands for the white cane.

This repository contains a runnable **demo/simulation** of the app + bracelet pair:

- `src/HapticNav.Core` — all domain logic (haptic vocabulary, BLE frame protocol, pairing/security, route guidance engine). Written against small interfaces so a real BLE stack (e.g. .NET MAUI + GATT) can replace the simulation later without touching the logic.
- `src/HapticNav.ConsoleApp` — interactive console demo of the whole system, including a simulated bracelet.
- `tests/HapticNav.Core.Tests` — xUnit tests for the protocol, security and navigation logic.

## Run it

```bash
dotnet run --project src/HapticNav.ConsoleApp
```

Optional: fix the pairing PIN for scripted demos with `-- --pin=1234`.

Menu options: pair with the bracelet (BLE bonding), walk the demo route (automatic
or step-by-step), list the vibration vocabulary, simulate connection loss, take the
pattern training quiz, and review the privacy/security notes.

## Run the tests

```bash
dotnet test
```

## How it maps to the analysis document

### Functional requirements

| ID | Requirement | Where |
|----|-------------|-------|
| FR-01 | Distinct vibration for left, right, turn now, stop, arrived | `Haptics/HapticVocabulary.cs` — 8 maximally distinct patterns (rhythm × motor side) |
| FR-02 | App sends direction commands to the bracelet over BLE | `Protocol/BleFrame.cs` + `Ble/SimulatedBraceletLink.cs` |
| FR-03 | App guides the user along a fixed demo route | `Navigation/WalkingRoute.cs` (Rachelsmolen campus loop) + `Navigation/NavigationEngine.cs` |
| FR-04 | Bracelet signals when the BLE connection is lost | `Ble/BraceletDevice.cs` watchdog + `ConnectionLost` pattern |
| FR-07 | Bracelet warns when the user goes off route | `NavigationEngine.DistanceFromSegmentMeters` + `OffRoute` pattern (Should) |

FR-05/06 (destination entry, real OSM routing) and FR-08/09 (obstacle detection,
emergency button) are Should/Could goals — the `NavigationEngine` and `IBraceletLink`
abstractions are the extension points for them.

### Non-functional requirements

| ID | Requirement | How the demo addresses it |
|----|-------------|---------------------------|
| NFR-01 | Patterns recognisable after 5 min training | Menu option 6: pattern training quiz with scoring |
| NFR-02 | Command reaches the bracelet within 1 s | Link measures latency per command (typ. 40–120 ms simulated); asserted in tests |
| NFR-04 | Usable with VoiceOver / TalkBack | Plain labelled text UI, no graphics, predictable prompts |
| NFR-05 | Only the paired phone can send commands | Pairing with PIN + bonding; bracelet rejects frames from any other device (tested) |
| NFR-06 | No location history stored or sent | Only direction codes cross the BLE link (5-byte frames, no coordinates); the session keeps no position history |

### Security measures from the threat model

- **Fake directions from a nearby attacker** → bracelet accepts frames only from the bonded phone; wrong PIN cannot bond (`BraceletSecurityTests`).
- **Eavesdropping on BLE traffic** → frames contain a command code and sequence number only, never coordinates, so the link leaks nothing about the route.
- **Malformed/injected frames** → CRC-8 checksum; bad frames are rejected.
- **Connection lost mid-route** → watchdog plays the distinct "connection lost" pattern (FR-04).

## Architecture

```
NavigationSession ── uses ──> NavigationEngine ── decides ──> DirectionCommand
      │                                                        │
      │ sends                                                  ▼
      ▼                                                   BleFrame.Encode
IBraceletLink (simulated now, real BLE GATT later)             │
      ▼                                                        ▼
BraceletDevice (simulated firmware) ── plays ──> HapticVocabulary patterns
```

## Notes for the design phase

- The vocabulary uses logical motor positions (Left/Front/Right/Back), so the
  "2–4 motors vs. two bracelets" decision does not change the app code.
- `IBraceletLink` is the single seam between app logic and BLE hardware; the
  eventual MAUI app only needs to implement it against the real GATT API.
- Patterns are data, not code: they can be tuned per the lab tests without
  recompiling the bracelet firmware logic.
