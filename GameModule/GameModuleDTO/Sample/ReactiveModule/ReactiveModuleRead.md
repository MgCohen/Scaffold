<!-- hash: 1b63c70762a35768074590d91c167a1b -->
# ReactiveModule Documentation

This document details the purpose and relations of the components in `/Sample/ReactiveModule`.

## Sub-Modules

- [Request](Request/RequestRead.md)

## Component Overview

### `ReactiveModuleData` (class)
- **Description**: Data container holding state and properties for reactive module data.
- **Namespace**: `GameModuleDTO.Sample.ReactiveModule`
- **Inherits/Implements**: `IGameModuleData`
- **Properties**: `Key`
- **Methods**: `IncreaseValueA`, `IncreaseValue`

## Dependency & Behavior Schema

```mermaid
graph TD
    ReactiveModuleData[ReactiveModuleData]
    ReactiveModuleData -->|inherits/implements| IGameModuleData
```


[Back to Parent](../SampleRead.md)
