<!-- hash: 694badf726141ede1f8727ed0f6d3a76 -->
# Json Documentation

This document details the purpose and relations of the components in `/Runtime/Implementation/Core/Json`.

## Component Overview

### `CrossPlatformTypeBinder` (class)
- **Description**: Client-Side Binder for Newtonsoft JSON Deserialization/Serialization. The main goal is to convert assembly types between Backend (mscorlib) and Unity (CoreLib). It is used by the JSON serializer whenever types are shared across the client/server boundary to ensure smooth parsing.
- **Namespace**: `Scaffold.CloudModules`
- **Inherits/Implements**: `ISerializationBinder`
- **Methods**: `BindToType`, `BindToName`

### `JsonExtensions` (class)
- **Description**: Provides extension methods for JSON serialization and deserialization. The main goal is to securely try to parse arbitrary strings into JSON using cross-platform capabilities. It is used heavily by the Cloud Code Service when passing messages and structured data payloads.
- **Namespace**: `Scaffold.CloudModules`
- **Methods**: `ToJson`, `ToSimpleJson`

## Dependency & Behavior Schema

```mermaid
graph TD
    CrossPlatformTypeBinder[CrossPlatformTypeBinder]
    CrossPlatformTypeBinder -->|inherits/implements| ISerializationBinder
    JsonExtensions[JsonExtensions]
```


[Back to Parent](../CoreRead.md)
