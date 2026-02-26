---
name: scaffold-infra
description: Describes Scaffold Infra modules (Containers, Events, MVVM, Navigation): namespaces, key types, and how they connect. Use when working in Assets/Scripts/Infra/, when editing or adding code in Containers, Events, MVVM, or Navigation, or when answering questions about Scaffold Infra.
---

# Scaffold Infra

## When to use this skill

Apply this skill when:
- Working under `Assets/Scripts/Infra/`
- The user mentions Infra, Containers, Events, MVVM, or Navigation in this project
- Editing or adding code that touches DI, event bus, view models, bindings, or view/screen navigation

## Module map

| Module | Namespace | Purpose |
|--------|-----------|---------|
| **Containers** | `Scaffold.Containers` | DI over VContainer; composition roots and context tree |
| **Events** | `Scaffold.Events` | In-process event bus; typed listeners and `ContextEvent` payloads |
| **MVVM** | `Scaffold.MVVM` | ViewModels, views, and expression-based bindings |
| **Navigation** | `Scaffold.Navigation` | View/screen navigation (stack, options, transitions) |

### Containers

- **Abstractions**: `IContainerBuilder`, `IContainerResolver`, `IRegistrationBuilder`, `IContext`
- **Flow**: `Bootstrap` (VContainer `LifetimeScope`) builds an `IContext` and runs `Build(IContext)`; context can `AddChild`, `Append`, or `ChangeContext` to add `Container` instances
- **Container** / **Installer**: Override `Build(IContainerBuilder, Transform)` to register services; `Container` creates child scopes
- **Adapters**: `Scaffold.Containers.Adapters` — VContainerBuilderAdapter, VContainerResolverAdapter, VContainerRegistrationBuilderAdapter, NoOpRegistrationBuilderAdapter
- **Lifetime**: `ContainerLifetime` enum for registrations

### Events

- **Abstractions**: `IEventBus`, `ContextEvent` (abstract record; all event payloads inherit)
- **API**: `AddListener<T>`, `RemoveListener<T>`, `Raise(ContextEvent)`, `Clear()`; also non-generic `Type` + `Action<ContextEvent>` overloads
- **Implementation**: `EventController`; registration via `EventsInstaller` in Container

### MVVM

- **ViewModel**: Abstract partial `ObservableObject` + `IViewModel`; holds `INavigation` and `IBindings` (e.g. `TreeBinding`); `Bind(INavigation)`, `Close()`, `BindChildViewModel<T>`
- **ViewElement** / **ViewElement&lt;T&gt;** / **ViewElement&lt;T,J&gt;** (MonoBehaviour view base); binds to `IViewController`; registers property and collection bindings; subscribes to `INotifyPropertyChanged` / `INestedObservableProperties`
- **Binding** (`Scaffold.MVVM.Binding`): `IBindings`, `IBind`, `IBindedProperty`, `IBindedCollection`, `IBindSet`, `IBindContext`; implementations include `TreeBinding`, `BindSets`, `BindingPath`, `BindGroup`/`BindGroups`, `BindRegistry`, `BindFactory`, `BindedProperty`, `BindedCollection`, converters, `ICollectionHandler`, `NestedObservableObject` / `NestedPropertyAttribute`
- **Other**: `View`, `UIView`, `ViewComponent`, `Model`; `IView`, `IViewModel`

### Navigation

- **Abstractions**: `INavigation`, `IViewController`, `IView`, `INavigationMiddleware`
- **Implementation**: `NavigationController`, `NavigationStack`, `NavigationProvider`, `NavigationPoint`, `NavigationOptions` / `NavigationOptionsSchema`, `NavigationTransitions`, `ViewConfig`, `ViewSchema`, `ViewTransitionData`, `NavigationMiddlewares`, `NavigationEvents`, `ViewChangedEvent`; `ServerNavigationController` (in MVVM namespace); `NoView` utility; enums `ViewType`, `ViewState`
- **Container**: `NavigationInstaller`, `NavigationInjection`; hero transitions (`HeroHandler`, `IHeroHandler`, `HeroMarker`) in MVVM/Extensions

## Cross-links

- **Containers** host `IContext` and run installers (e.g. `EventsInstaller`, `NavigationInstaller`).
- **Events** use `ContextEvent` as payload base; registered in container via `EventsInstaller`.
- **MVVM** ViewModels get `INavigation` via `Bind(INavigation)` and use `IBindings`; ViewElements bind to `IViewController` and use the same binding system.
- **Navigation** is registered and injected via Container; MVVM calls `INavigation` to open/close views.

## Conventions and reference

- **Folder layout and Abstractions vs Implementation**: Follow [.cursor/rules/infra-folder-guidelines.md](.cursor/rules/infra-folder-guidelines.md).
- **Detailed types and file paths**: See [reference.md](reference.md).
