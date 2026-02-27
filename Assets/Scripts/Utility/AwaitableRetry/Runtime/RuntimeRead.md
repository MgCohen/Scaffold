<!-- hash: 23c2f74ce6cf48231752cb32a019880e -->
# Runtime Documentation

This document details the purpose and relations of the components in `/Runtime`.

## Component Overview

### `TaskRetryExtensions` (class)
- **Description**: Provides extension methods for adding retry logic to Task and Task<T> delegates. The main goal is to convert standard functions into configurable RetryTaskBuilders easily. It is used across various asynchronous game systems, notably Cloud Code, to wrap volatile routines with resilient retries.
- **Namespace**: `Scaffold.AwaitableRetry`
- **Methods**: `WithCondition`, `Retry`, `OnRetry`, `WithDelay`

## Dependency & Behavior Schema

```mermaid
graph TD
    TaskRetryExtensions[TaskRetryExtensions]
```


[Back to Parent](../AwaitableRetryRead.md)
