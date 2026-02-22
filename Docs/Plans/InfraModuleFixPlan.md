# Infra Module Fix Plan

## Goal Description
Resolve module structure violations and coding standard violations across the `Assets/Scripts/Infra` modules, which were identified in the recent analysis report.

## Proposed Changes

### MVVM Module
- **Module Structure Violations**: 
  - Move `Data`, `Generators`, and `Prefabs` folders into a new `Assets` folder (`Assets/Scripts/Infra/MVVM/Assets/`).
  - Move the `Extensions` folder into the `Runtime` folder, or evaluate if it should be an independent module. For this plan, we will move it into `Runtime` as a nested namespace/folder.
  - Create a `Tests` folder (`Assets/Scripts/Infra/MVVM/Tests/`) with an assembly definition (`Scaffold.MVVM.Tests.asmdef`).

### Navigation Module
- **Coding Standard Violations ("One class per file")**:
  - `NavigationTransitions.cs`: Extract `IViewAnimationHandler` and `IViewTransitionHandler` interfaces into their own files (`IViewAnimationHandler.cs` and `IViewTransitionHandler.cs`) inside the `Contracts` folder.
  - `ViewSchema.cs`: Extract `TransitionViewSchema` and `AnimationViewSchema` into their own files.
- **Module Structure Violations**:
  - Create a `Tests` folder (`Assets/Scripts/Infra/Navigation/Tests/`) with an assembly definition (`Scaffold.Navigation.Tests.asmdef`).
  - Move the contents of `Navigation/Container/Runtime` directly into `Navigation/Container` and update `.meta` files accordingly. Delete the now-empty `Runtime` folder.

### NetworkMessages Module
- **Module Structure Violations**:
  - Add the missing assembly definition to the `Samples` folder (`Scaffold.NetworkMessages.Samples.asmdef`).
  - Create a `Tests` folder (`Assets/Scripts/Infra/NetworkMessages/Tests/`) with an assembly definition (`Scaffold.NetworkMessages.Tests.asmdef`).

### Events Module
- **Module Structure Violations**:
  - Create a `Tests` folder (`Assets/Scripts/Infra/Events/Tests/`) with an assembly definition (`Scaffold.Events.Tests.asmdef`).
  - Move the contents of `Events/Container/Runtime` directly into `Events/Container` and update `.meta` files accordingly. Delete the now-empty `Runtime` folder.

### Containers Module
- **Module Structure Violations**:
  - Create a `Tests` folder (`Assets/Scripts/Infra/Containers/Tests/`) with an assembly definition (`Scaffold.Containers.Tests.asmdef`).

## Verification Plan
### Automated Tests
- Build Unity Project and verify that the Test Runner window displays all newly created `Tests` folders.
### Manual Verification
- Check Unity Editor for proper assembly compilation after file moves. 
- Ensure that the modules still function properly (or no compilation errors) after moving `Generators`, `Data`, `Prefabs` and `.meta` files into `Assets` folder and correctly establishing `Tests` assembly definitions.
