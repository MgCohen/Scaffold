# Scaffold Analytics

Generic analytics module that acts as an abstraction wrapper over Unity Gaming Services (UGS) Analytics.

## Purpose

Enforces a strongly-typed, object-oriented approach to Analytics events. Specific events should inherit from `AnalyticsEvent`.

## Configuration

Requires `com.scaffold.ugs` or equivalent Unity Services initialization prior to use.

Register `AnalyticsService` and `WebGlCrashReporter` through `AnalyticsInstaller`, or register both services in the consuming application. Recover the previous browser session only after UGS initialization and Analytics data collection have started:

```csharp
analyticsService.Initialize();
webGlCrashReporter.SetCheckpoint("analytics_ready");
webGlCrashReporter.TryReportPreviousSession();
```

The reporter reads a host diagnostic provider when the WebGL page exposes `window.gearEngineDiagnostics.getReport()` and `markPhase()`. Otherwise, the package JavaScript plugin persists runtime checkpoints in `localStorage` after Unity starts.

WebGL browser termination cannot reliably send a final network request. The reporter therefore recognizes an unclean prior session on the next successful launch, records one Analytics event, flushes it immediately, and acknowledges the browser session only after both operations succeed.

## Unity Analytics custom event

Create this custom event schema in the Unity Dashboard before deploying the player. Unity Analytics rejects custom events that do not have a matching schema.

Event name: `webglAbruptSession`

| Parameter | Type | Maximum length |
| --- | --- | --- |
| `sessionId` | string | 100 |
| `lastPhase` | string | 100 |
| `runtimeReady` | boolean | n/a |
| `durationMs` | integer | n/a |
| `userAgent` | string | 100 |
| `browserPlatform` | string | 100 |
| `viewport` | string | 100 |
| `devicePixelRatio` | number | n/a |
| `wasmHeapMb` | number | n/a |
| `lastErrorType` | string | 100 |
| `lastErrorMessage` | string | 100 |

## Platform behavior

- WebGL: records recovered abrupt sessions through Unity Analytics.
- Editor and non-WebGL players: `TryReportPreviousSession()` and `SetCheckpoint()` are safe no-ops.
- Loader failures before the Unity runtime starts require the host template provider because a `.jslib` plugin is unavailable until the player is running.
