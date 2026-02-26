# Scaffold Infra — Reference

Detailed breakdown of each module under `Assets/Scripts/Infra/`: namespaces, key types, file paths, and how modules relate.

---

## Containers

**Namespace**: `Scaffold.Containers` (adapters: `Scaffold.Containers.Adapters`)

**Purpose**: Project-level DI and composition roots. Abstracts over VContainer; exposes a subset of builder/resolver/context APIs so Scaffold code does not depend on VContainer directly.

### Abstractions

| Type | Path |
|------|------|
| `IContainerBuilder` | Containers/Runtime/Abstractions/IContainerBuilder.cs |
| `IContainerResolver` | Containers/Runtime/Abstractions/IContainerResolver.cs |
| `IRegistrationBuilder` | Containers/Runtime/Abstractions/IRegistrationBuilder.cs |
| `IContext` | Containers/Runtime/Abstractions/IContext.cs |

### Implementation

| Type | Path |
|------|------|
| `Bootstrap` | Containers/Runtime/Implementation/Boostrap.cs |
| `Context` | Containers/Runtime/Implementation/Context.cs |
| `Container` | Containers/Runtime/Implementation/Container.cs |
| `Installer` | Containers/Runtime/Implementation/Installer.cs |
| `ContainerLifetime` | Containers/Runtime/Implementation/ContainerLifetime.cs |

### Adapters (VContainer)

| Type | Path |
|------|------|
| `VContainerBuilderAdapter` | Containers/Runtime/Adapters/VContainer/VContainerBuilderAdapter.cs |
| `VContainerResolverAdapter` | Containers/Runtime/Adapters/VContainer/VContainerResolverAdapter.cs |
| `VContainerRegistrationBuilderAdapter` | Containers/Runtime/Adapters/VContainer/VContainerRegistrationBuilderAdapter.cs |
| `NoOpRegistrationBuilderAdapter` | Containers/Runtime/Adapters/VContainer/NoOpRegistrationBuilderAdapter.cs |

### Relations

- Hosts `IContext`; build callbacks resolve context and run `Build(IContext)`.
- Other Infra modules are registered via installers (e.g. EventsInstaller, NavigationInstaller) that receive the builder; Containers does not reference Events/MVVM/Navigation by type, but the composition graph wires them.

---

## Events

**Namespace**: `Scaffold.Events`

**Purpose**: In-process event bus. Listeners subscribe by event type (`ContextEvent` subclasses); events are raised with a payload instance.

### Abstractions

| Type | Path |
|------|------|
| `IEventBus` | Events/Runtime/Abstractions/IEventBus.cs |
| `ContextEvent` | Events/Runtime/Abstractions/ContextEvent.cs |

### Implementation

| Type | Path |
|------|------|
| `EventController` | Events/Runtime/Implementation/EventController.cs |

### Container

| Type | Path |
|------|------|
| `EventsInstaller` | Events/Container/Runtime/EventsInstaller.cs |

### Relations

- All event payloads inherit from `ContextEvent`.
- Registered in the container via `EventsInstaller`; consumers resolve `IEventBus` and Add/Remove/Raise.

---

## MVVM

**Namespace**: `Scaffold.MVVM`; binding: `Scaffold.MVVM.Binding`

**Purpose**: ViewModels (ObservableObject + INavigation + bindings), views (ViewElement hierarchy), and expression-based binding system. Integrates with CommunityToolkit.Mvvm and Scaffold Navigation.

### Abstractions

| Type | Path |
|------|------|
| `IViewModel` | MVVM/Runtime/Abstractions/IViewModel.cs |
| `IView` | MVVM/Runtime/Abstractions/IView.cs |
| `IBindings` | MVVM/Runtime/Binding/Abstractions/IBindings.cs |
| `IBind`, `IBindedProperty`, `IBindedCollection`, `IBindSet`, `IBindContext` | MVVM/Runtime/Binding/Abstractions/ |
| `ICollectionHandler` | MVVM/Runtime/Binding/Abstractions/ICollectionHandler.cs |
| `INestedObservableProperties` | MVVM/Runtime/Binding/Abstractions/INestedObservableProperties.cs |

### Implementation (MVVM)

| Type | Path |
|------|------|
| `ViewModel` | MVVM/Runtime/Implementation/ViewModel.cs |
| `ViewElement`, `ViewElement<T>`, `ViewElement<T,J>` | MVVM/Runtime/Implementation/ViewElement.cs |
| `View`, `UIView` | MVVM/Runtime/Implementation/View.cs, UIView.cs |
| `ViewComponent` | MVVM/Runtime/Implementation/ViewComponent.cs |
| `Model` | MVVM/Runtime/Implementation/Model.cs |
| `NavigateViewEvent` | MVVM/Runtime/Implementation/Base Events/NavigateViewEvent.cs |

### Implementation (Binding)

| Type | Path |
|------|------|
| `TreeBinding` | MVVM/Runtime/Binding/Implementation/TreeBinding.cs |
| `BindSets`, `BindSet`, `BindGroups`, `BindGroup` | MVVM/Runtime/Binding/Implementation/ |
| `BindingPath`, `BindRegistry`, `BindFactory`, `BindContext` | MVVM/Runtime/Binding/Implementation/ |
| `BindedProperty`, `BindedCollection` | MVVM/Runtime/Binding/Implementation/ |
| `Adapter`, `Converter`, `GenericConverter` | MVVM/Runtime/Binding/Implementation/ |
| `ExpressionsUtility`, `BindedPropertyUtility` | MVVM/Runtime/Binding/Implementation/ |
| `NestedObservableObjectAttribute`, `NestedPropertyAttribute` | MVVM/Runtime/Binding/Implementation/ |

### Relations

- ViewModels get `INavigation` via `Bind(INavigation)` and call `Close()` / navigation APIs.
- ViewElements bind to `IViewController` (Navigation); they register property/collection bindings and subscribe to ViewModel property changes.
- Binding system is shared between ViewModel (source updates) and ViewElement (target updates); supports nested observables and converters.

---

## Navigation

**Namespace**: `Scaffold.Navigation`; container: `Scaffold.Navigation.Container`

**Purpose**: View/screen navigation: stack, options, transitions, view config and schema. Defines how views are opened, closed, and transitioned.

### Abstractions

| Type | Path |
|------|------|
| `INavigation` | Navigation/Runtime/Abstractions/INavigation.cs |
| `IViewController` | Navigation/Runtime/Abstractions/IViewController.cs |
| `IView` | Navigation/Runtime/Abstractions/IView.cs |
| `INavigationMiddleware` | Navigation/Runtime/Abstractions/INavigationMiddleware.cs |

### Implementation

| Type | Path |
|------|------|
| `NavigationController` | Navigation/Runtime/Implementation/NavigationController.cs |
| `NavigationStack` | Navigation/Runtime/Implementation/NavigationStack.cs |
| `NavigationProvider` | Navigation/Runtime/Implementation/NavigationProvider.cs |
| `NavigationPoint` | Navigation/Runtime/Implementation/NavigationPoint.cs |
| `NavigationOptions`, `NavigationOptionsSchema` | Navigation/Runtime/Implementation/ |
| `NavigationTransitions`, `ViewTransitionData` | Navigation/Runtime/Implementation/ |
| `ViewConfig`, `ViewSchema` | Navigation/Runtime/Implementation/ |
| `NavigationMiddlewares` | Navigation/Runtime/Implementation/NavigationMiddlewares.cs |
| `NavigationEvents`, `ViewChangedEvent` | Navigation/Runtime/Implementation/ |
| `NavigationSettings` | Navigation/Runtime/Implementation/NavigationSettings.cs |
| `ServerNavigationController` | Navigation/Runtime/Implementation/ServerNavigationController.cs (namespace: Scaffold.MVVM) |

### Enums / Utility

| Type | Path |
|------|------|
| `ViewType`, `ViewState` | Navigation/Runtime/Enums/ |
| `NoView` | Navigation/Runtime/Utility/NoView.cs |
| `NavigationExtensions` | Navigation/Runtime/Utility/NavigationExtensions.cs |

### Container

| Type | Path |
|------|------|
| `NavigationInstaller` | Navigation/Container/Runtime/NavigationInstaller.cs |
| `NavigationInjection` | Navigation/Container/Runtime/NavigationInjection.cs |

### Extensions (hero-style transitions)

| Type | Path |
|------|------|
| `HeroHandler`, `IHeroHandler`, `HeroMarker` | MVVM/Extensions/Components/ (namespace: Scaffold.Navigation) |

### Relations

- Registered and injected via Container (`NavigationInstaller`, `NavigationInjection`).
- MVVM ViewModels receive `INavigation` and call `Open`/`Close`/`Return`; ViewElements receive `IViewController` and bind to the ViewModel.
- Events (e.g. `ViewChangedEvent`) can be used by Navigation or other systems to react to navigation changes.
