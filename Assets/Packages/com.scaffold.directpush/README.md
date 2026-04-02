# Scaffold Direct Push

Push notification module for sending and receiving player/project messages via Unity Cloud Code.

## Overview

This package provides two services:

- **`PushSubscriptionService`** — Listens for incoming push notifications (player-targeted and project-wide) and dispatches them to registered handlers by message type.
- **`DirectPushClient`** — Sends push notifications through the LiveOps backend using typed `ModuleRequest<SendPushResponse>` DTOs.

## Public API

### Receiving Pushes

```csharp
// Register handlers before subscriptions are active
pushSubscriptionService.SubscribeToPlayerMessage("MyMessageType", OnMyMessage);
pushSubscriptionService.SubscribeToProjectMessage("Broadcast", OnBroadcast);
```

`PushSubscriptionService` implements `IAsyncLayerInitializable` — subscriptions are established automatically during the layer initialization phase.

### Sending Pushes

```csharp
// Self-push (no AccessKey needed)
await directPushClient.SendSelfPushAsync("hello", "greeting");

// Targeted player push (requires AccessKey GUID)
await directPushClient.SendPlayerPushAsync("hello", "greeting", targetPlayerId, accessKeyGuid);

// Project broadcast (requires AccessKey GUID)
await directPushClient.SendProjectPushAsync("hello", "broadcast", accessKeyGuid);
```

## DI Registration

Use `DirectPushInstaller` in your VContainer scope:

```csharp
new DirectPushInstaller().Install(builder);
```

## Backend Endpoints

| Request DTO | Cloud Code Function | AccessKey |
|---|---|---|
| `SendSelfPushRequest` | `SendSelfPushRequest` | No |
| `SendPlayerPushRequest` | `SendPlayerPushRequest` | Yes |
| `SendProjectPushRequest` | `SendProjectPushRequest` | Yes |

All endpoints return `SendPushResponse : ModuleResponse`.
