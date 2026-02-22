# Infra Module Analysis — Post-Refactor

> [!NOTE]
> Fresh analysis after coding-standards fixes. All files re-scanned against [coding-standards.md](file:///c:/Users/user/Documents/Unity/Scaffold/.agents/rules/coding-standards.md) and [module-structure-guidelines.md](file:///c:/Users/user/Documents/Unity/Scaffold/.agents/rules/module-structure-guidelines.md).

---

## ✅ Containers Module — Clean

All structure and coding violations fixed. `Contracts/` and `Implementation/` folders correct. `Bootstrap` typo fixed. Field naming, expression-body, nested calls all resolved.

## ✅ Events Module — Clean

`Contracts/` folder correct. `ContextEvent` uses inline record. `EventController` fields use camelCase.

## ✅ NetworkMessages Module — Clean

`Contracts/` folder correct. All `m_` prefixes removed. Method comments removed. Namespaces correct (`Scaffold.NetworkMessages`).

---

## ⚠️ Navigation Module — Remaining Violations

### Module Structure

| Rule | Status | Details |
|------|--------|---------|
| Tests | ❌ | **Missing** — guidelines say all modules **must** have tests |
| Container | ✅ | Has `Container/` folder |

### One-Class-Per-File

| File | Issue |
|------|-------|
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs) | Contains `IViewAnimationHandler` and `IViewTransitionHandler` interfaces at the bottom (lines 305–313). Should be separate files in `Contracts/` |

### Expression-Body Properties (should use curly-bracket get)

| File | Lines | Property |
|------|-------|----------|
| [NavigationController.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationController.cs#L18) | 18 | `CurrentPoint => this.stack.CurrentPoint` |
| [NavigationStack.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationStack.cs#L11-L14) | 11–14 | `Count =>`, `CurrentView =>`, `PreviousPoint =>` |
| [ViewChangedEvent.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/ViewChangedEvent.cs#L24) | 24 | `TargetType => targetType` |

### Record Style (should use inline constructor)

| File | Issue |
|------|-------|
| [ViewChangedEvent.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/ViewChangedEvent.cs) | Uses body-style record with explicit constructors and `{ get; }` properties instead of inline constructor |

### Long Methods / Mid-Body Returns (>8 lines or early return)

| File | Method | Lines | Issue |
|------|--------|-------|-------|
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs#L32-L56) | `RunTransitions` | 32–56 | 24 lines, complex async loop |
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs#L214-L229) | `DoOpenSequence` | 214–229 | 15 lines |
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs#L260-L295) | `HandleAnimator` | 260–295 | 35 lines, multiple while loops with inline comments |
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs#L195-L212) | `Hide` | 195–212 | Mid-body returns |
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs#L231-L255) | `Open` | 231–255 | Mid-body return inside try/catch |
| [NavigationController.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationController.cs#L97-L118) | `GoTo` | 97–118 | 21 lines |
| [NavigationController.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationController.cs#L48-L67) | `Close` | 48–67 | Mid-body return |
| [NavigationStack.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationStack.cs#L69-L78) | `GetPointDepth` | 69–78 | Inconsistent bracing (if without braces, else with) |
| [NavigationProvider.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationProvider.cs#L39-L51) | `GetNavigationPoint` | 39–51 | Mid-body returns |
| [NavigationSettings.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationSettings.cs#L16-L31) | `GetViewConfig` | 16–31 | 15 lines, mid-body return |

### Comments on Methods / Dead Code

| File | Lines | Issue |
|------|-------|-------|
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs) | Multiple | `#region` blocks are section comments (lines 31, 59, 101, 103, 132, 259) |
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs#L262) | 262 | Comment on method body: `//small delay to skip a frame` |
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs#L274-L289) | 274–289 | Inline comments: `//wait for the correct animation...`, `//wait until its not...`, `//wait until all...` |
| [NavigationSettings.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationSettings.cs#L33-L37) | 33–37 | Commented-out dead code (`TryGetOverlayConfig`) |
| [NavigationController.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationController.cs#L110) | 110 | Inline comment: `//in case we are returning...` |
| [NavigationController.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationController.cs#L31-L95) | 31–95 | `#region Open`, `#region Close` blocks |

### Nested Calls

| File | Line | Issue |
|------|------|-------|
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs#L108) | 108 | `await point.View.gameObject.GetComponent<>().DoTransition(...)` — chained call |
| [NavigationTransitions.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs#L300) | 300 | `await point.View.gameObject.GetComponent<>().AnimateView(...)` — chained call |
| [NavigationProvider.cs](file:///c:/Users/user/Documents/Unity/Scaffold/Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationProvider.cs#L82) | 82 | `Instantiate(config.ViewAsset, viewHolder).GetComponent<>()` — chained call |

---

## ⚠️ MVVM Module — Remaining Violations

### Module Structure

| Rule | Status | Details |
|------|--------|---------|
| Tests | ❌ | **Missing** — guidelines say all modules **must** have tests |
| Container | ❌ | **Missing** — no DI installer for the module |
| `Binding/Abstractions/` | ❌ | Should be `Binding/Contracts/` |
| Non-standard top-level folders | ⚠️ | `Binding/` is a sub-module with its own `Abstractions/Implementation` — doesn't match the single-module Runtime structure. Consider extracting to separate module or merging |

### Expression-Body (ViewChangedEvent already listed in Navigation)

No remaining expression-body violations in MVVM Runtime after our fixes.

### Long Methods / Mid-Body Returns

The files we already fixed (`EventLedger.cs`, `ViewEvent.cs`, `ViewEvents.cs`) are now clean. Remaining files were not flagged in the original review.

---

## Summary of Remaining Work

| Category | Count | Modules |
|----------|-------|---------|
| Missing Tests | 2 | Navigation, MVVM |
| Missing Container | 1 | MVVM |
| `Abstractions/` → `Contracts/` | 1 | MVVM (Binding sub-folder) |
| One-class-per-file | 1 | NavigationTransitions (2 interfaces) |
| Expression-body properties | 5 | NavigationController(1), NavigationStack(3), ViewChangedEvent(1) |
| Record style (body → inline) | 1 | ViewChangedEvent |
| Long methods (>8 lines) | 10 | Navigation (various files) |
| Comments on methods / dead code | 6+ | Navigation (various files) |
| Nested/chained calls | 3 | NavigationTransitions(2), NavigationProvider(1) |
| Commented-out code | 1 | NavigationSettings |
