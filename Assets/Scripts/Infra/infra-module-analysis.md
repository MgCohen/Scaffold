# Infrastructure Module Analysis

This document tracks the infrastructure modules in the project, their dependencies, and their circular dependency status.

## Modules

| Module Name | Path | Description | Status |
| ----------- | ---- | ----------- | ------ |
| Events | `Assets/Scripts/Infra/Events` | Event bus and messaging. | Verified |
| MVVM | `Assets/Scripts/Infra/MVVM` | MVVM framework and extensions. | Verified |
| Navigation | `Assets/Scripts/Infra/Navigation` | UI and scene navigation. | Verified |
| AwaitableQueue | `Assets/Scripts/Infra/AwaitableQueue` | Async task queuing. | Verified |
| CloudModule | `Assets/Scripts/Infra/CloudModule` | Cloud services integration. | Verified |
| Containers | `Assets/Scripts/Infra/Containers` | DI container abstractions. | Verified |
| LifeCycle | `Assets/Scripts/Infra/LifeCycle` | Application and object lifecycle. | Verified |
| NetworkMessages | `Assets/Scripts/Infra/NetworkMessages` | Networked messaging layer. | Verified |

## Dependency Graph

(To be updated with Mermaid diagram)

## Guidelines

- Refer to `module-structure-guidelines.md` for structure rules.
- Use `/create-module` workflow for adding new modules.
