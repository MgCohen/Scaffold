<!-- hash: 0ec33dceb47c836c71f6f0b974a8670f -->
# Runtime Documentation

This document details the purpose and relations of the components in `/Runtime`.

## Sub-Modules

- [Enum](Enum/EnumRead.md)

## Component Overview

### `ILogger` (interface)
- **Description**: Declares the contract for emitting diagnostic strings across varying environments. The main goal is to decouple the GameDebug facade from explicit endpoints like Unity Console. It is used strictly internally by the framework's logging subsystem to route traffic flexibly.
- **Namespace**: `Scaffold.Logging`

### `GameDebug` (class)
- **Description**: Central logging facade used across Client, Server and Shared code. Handles environment tagging, log levels and key/value-style context.
- **Namespace**: `Scaffold.Logging`
- **Properties**: `IsServer`
- **Methods**: `LogClientWarning`, `AssertFail`, `LogWithImplicitKey`, `LogException`, `Initialize`, `FormatValue`, `LogInitialized`, `LogClientError`, `LogError`, `FormatKeys`, `AssertThat`, `LogServerError`, `LogServer`, `LogClient`, `LogServerException`, `LogServerWarning`, `LogClientStarting`, `LogStarting`, `FormatMessage`, `Log`, `LogWarning`, `LogClientInitialized`

### `UnityLogger` (class)
- **Description**: Implements standard Unity Console integration for the agnostic debug interface. The main goal is to map custom internal levels appropriately into UnityEngine.Debug methodologies. It is used as the default sink out-of-the-box by the shared logging facade.
- **Namespace**: `Scaffold.Logging`
- **Inherits/Implements**: `ILogger`
- **Methods**: `Log`

## Dependency & Behavior Schema

```mermaid
graph TD
    ILogger[ILogger]
    GameDebug[GameDebug]
    UnityLogger[UnityLogger]
    UnityLogger -->|inherits/implements| ILogger
```


[Back to Parent](../LoggingRead.md)
