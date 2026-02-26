# Test Case Plan for Infra Modules

Proposed test cases for each module in `Assets/Scripts/Infra`. These are pure-logic unit tests — all Unity-dependent types (MonoBehaviour, Transform, GameObject, NetworkManager) should be mocked or substituted via interfaces.

> [!IMPORTANT]
> No existing test infrastructure was found in the project. Before implementation, we need to decide on a test framework (NUnit via Unity Test Framework, or similar) and where tests should live (e.g. `Assets/Tests/EditMode/Infra/`).

---

## 1. Containers

### 1.1 `Context`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `AddChild_BuildsChildScope` | Calling `AddChild(container)` invokes `scope.BuildChild` on the parent scope with the container and a new child `Context`. |
| 2 | `AddChild_Generic_CreatesContainerInstance` | `AddChild<T>()` instantiates `T` and delegates to `AddChild(Container)`. |
| 3 | `Append_UsesParentScope` | `Append(container)` builds the child through the **grandparent** scope, not the current scope. |
| 4 | `ChangeContext_DisposesCurrentScope_ThenAppends` | `ChangeContext(container)` calls `Dispose()` on the current scope before building. |

### 1.2 `NoOpRegistrationBuilder<T>`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `WithParameter_ReturnsSelf` | Fluent call returns the same instance. |
| 2 | `AsImplementedInterfaces_ReturnsSelf` | Fluent call returns the same instance. |

### 1.3 `VContainerRegistry` (via interface mock)

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Register_MapsLifetimeCorrectly` | Each `ContainerLifetime` value maps to the right VContainer `Lifetime`. |
| 2 | `Register_Factory_WrapsResolverCorrectly` | The factory overload wraps the inner resolver in a `VContainerResolver`. |
| 3 | `RegisterBuildCallback_InvokesWithWrappedResolver` | The callback receives a `VContainerResolver`. |
| 4 | `ToVContainerLifetime_UnknownValue_DefaultsToTransient` | An undefined enum value maps to `Transient`. |

### 1.4 `VContainerResolver`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Constructor_NullInner_ThrowsArgumentNullException` | Guard clause works. |
| 2 | `Resolve_DelegatesToInner` | `Resolve<T>()` forwards to the wrapped `IObjectResolver`. |
| 3 | `Inject_DelegatesToInner` | `Inject(obj)` forwards correctly. |

---

## 2. Events

### 2.1 `EventController`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `AddListener_Raise_InvokesHandler` | Register a typed listener, raise an event of that type — handler receives it. |
| 2 | `AddListener_DifferentType_DoesNotFire` | Register for type A, raise type B — handler is **not** invoked. |
| 3 | `AddListener_Duplicate_IgnoresSecondRegistration` | Adding the same delegate twice does not cause a double-fire. |
| 4 | `RemoveListener_StopsReceiving` | After removal, raising the event no longer invokes the handler. |
| 5 | `RemoveListener_UnknownDelegate_NoOp` | Removing a never-registered delegate does not throw. |
| 6 | `AddListener_UntypedOverload_ReceivesEvents` | The `AddListener(Type, Action<ContextEvent>)` overload works. |
| 7 | `RemoveListener_UntypedOverload_StopsReceiving` | The untyped overload removal works correctly. |
| 8 | `MultipleListeners_AllInvoked` | Multiple listeners on the same type all fire. |
| 9 | `Clear_RemovesAllListeners` | After `Clear()`, no handlers fire. |
| 10 | `Raise_NoListeners_NoOp` | Raising an event with no registered listeners does not throw. |

---

## 3. NetworkMessages

### 3.1 `EquatableWrapper<T>`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Equals_SameValue_ReturnsTrue` | Two wrappers with equal values are equal. |
| 2 | `Equals_DifferentValue_ReturnsFalse` | Two wrappers with different values are not equal. |
| 3 | `Constructor_StoresValue` | `Value` field holds the provided value. |

### 3.2 `NetworkMessageDispatcher` (requires mocking `NetworkManager`)

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Constructor_NullNetworkManager_Throws` | Guard clause. |
| 2 | `RegisterHandler_StoresHandler` | After registering, the handler dictionary contains the type. |
| 3 | `RegisterHandler_Duplicate_OverwritesWithWarning` | Re-registering for the same type replaces the handler. |
| 4 | `UnregisterHandler_RemovesHandler` | After unregistering, the type is no longer in the handlers. |
| 5 | `SendToServer_AfterDispose_NoOp` | Operations after `Dispose()` are silently ignored. |
| 6 | `SendToClient_AfterDispose_NoOp` | Same for client sends. |
| 7 | `RegisterHandler_AfterDispose_NoOp` | Registering after dispose is silently ignored. |
| 8 | `Dispose_UnregistersAllHandlers` | `Dispose()` cleans up all named message handlers. |
| 9 | `Dispose_Idempotent` | Calling `Dispose()` twice does not throw. |

---

## 4. Navigation

### 4.1 `NavigationStack`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `AddToStack_SetsCurrentPoint` | After adding, `CurrentPoint` equals the added point. |
| 2 | `AddToStack_Null_NoOp` | Adding `null` does not change the stack. |
| 3 | `RemoveFromStack_UpdatesCurrentPoint` | Removing the current point updates `CurrentPoint` to the last remaining. |
| 4 | `RemoveFromStack_NonCurrent_KeepsCurrentPoint` | Removing a non-current point leaves `CurrentPoint` unchanged. |
| 5 | `RemoveFromStack_Null_NoOp` | Removing `null` does nothing. |
| 6 | `PreviousPoint_OnlyOneItem_ReturnsNull` | With a single item, `PreviousPoint` is `null`. |
| 7 | `PreviousPoint_MultipleItems_ReturnsSecondToLast` | Returns the penultimate item. |
| 8 | `Count_ReflectsStackSize` | Count increases/decreases correctly. |
| 9 | `Get_ByController_FindsMatchingPoint` | `Get(IViewController)` returns the point whose ViewModel matches. |
| 10 | `Get_ByController_NotFound_ReturnsNull` | Returns null for an unregistered controller. |
| 11 | `GetPointDepth_FirstItem_ReturnsZero` | Depth of the first item is `0`. |
| 12 | `GetPointDepth_SubsequentItems_IncrementsBy10` | Each successive item has depth incremented by at least 10. |
| 13 | `ClearStack_EmptiesStack` | After `ClearStack()`, `Count` is 0, `CurrentPoint` is still the old value (list cleared). |
| 14 | `GetAllStackedScreens_WithFilter_ReturnsFiltered` | Only points matching the predicate are returned. |

### 4.2 `NavigationMiddleware`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `OnOpen_InvokesAllOpenHandlers` | All registered `INavigationOpenHandler`s receive the `OnOpen` call. |
| 2 | `OnOpen_FiltersNonOpenHandlers` | Middleware instances that don't implement `INavigationOpenHandler` are skipped. |
| 3 | `OnOpen_EmptyMiddlewares_NoOp` | No handlers — no exception. |

### 4.3 `NavigationPoint`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Constructor_SetsAllProperties` | View, ViewModel, Config, IsSceneView, Options are all set. |
| 2 | `Dispose_NullsReferencesAndSetsFlag` | After dispose, View/ViewModel/Config are null, Disposed is true. |

### 4.4 `ViewTransitionData`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Constructor_SetsFromToAndCloseCurrent` | All constructor parameters are stored correctly. |

---

## 5. MVVM

### 5.1 `ViewEvent`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Consume_SetsIsConsumedTrue` | After `Consume()`, `IsConsumed` is true. |
| 2 | `Consume_SetsConsumerToCurrent` | `Consumer` equals `Current` at the time of consumption. |
| 3 | `Consume_NullsCurrent` | `Current` is null after consumption. |
| 4 | `Restore_ResetsAllState` | `IsConsumed` false, `Consumer`/`Current` null, `History` cleared. |
| 5 | `LogNext_SetsCurrent_AddsOldToHistory` | First `LogNext` sets `Current`; second `LogNext` pushes old `Current` to `History`. |
| 6 | `LogNext_FirstCall_NullCurrent_NoHistoryEntry` | First call with null `Current` does not add null to history. |

### 5.2 `EventLedger<T>` (requires mock Transforms via `new GameObject().transform`)

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Register_Raise_InvokesCallback` | Registered callback on the same transform fires. |
| 2 | `Raise_BubblesUpParentChain` | Event raised on a child transform also fires callbacks registered on parent. |
| 3 | `Raise_Consumed_StopsBubbling` | If a handler calls `Consume()`, parent handlers do not fire. |
| 4 | `Unregister_StopsReceiving` | After unregister, callback no longer fires. |
| 5 | `Raise_WrongType_ThrowsException` | Calling `IEventLedger.Raise` with a mismatched event type throws. |
| 6 | `Register_GenericCallback_AlsoFires` | The `Action<ViewEvent>` overload fires alongside typed callbacks. |

### 5.3 `BindContext<T>`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Bind_ImmediatelyUpdatesWithCurrentValue` | Adding a binding triggers an immediate `Update` with the source's current value. |
| 2 | `Update_PushesNewValueToAllBinds` | All registered `IBind<T>` receive the refreshed value. |
| 3 | `Unbind_DisposesDisposableBinds` | Binds implementing `IDisposable` are disposed. |

### 5.4 `BindSet<TSource, TTarget>`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `TryConvert_WithRegisteredConverter_ReturnsTrue` | A matching converter successfully converts. |
| 2 | `TryConvert_NoConverter_ReturnsFalse` | Without converters, returns false. |
| 3 | `TryAdapt_WithRegisteredAdapter_ReturnsTrue` | A matching adapter successfully adapts. |
| 4 | `TryAdapt_NoAdapter_ReturnsFalse` | Without adapters, returns false. |

### 5.5 `BindedProperty<TSource, TTarget>`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Update_SameType_PassesThroughDirectly` | When `TSource == TTarget`, value goes straight to the setter. |
| 2 | `Update_WithConverter_AppliesConversion` | Custom converter transforms the value before calling the setter. |
| 3 | `Update_WithAdapter_AppliesAdaptation` | Adapter modifies the value post-conversion. |
| 4 | `Update_NoConversionPath_Throws` | If no conversion exists, throws. |
| 5 | `Update_ToString_Fallback` | When target is `string`, falls back to `ToString()`. |

### 5.6 `BindedCollection<TSource, TTarget>`

| # | Test Case | What it verifies |
|---|-----------|-----------------|
| 1 | `Update_NewCollection_PopulatesViaHandler` | Each source item calls `handler.Add`. |
| 2 | `Update_SameCollection_NoOp` | Assigning the same reference again skips re-initialization. |
| 3 | `Update_DifferentCollection_DisposesOld` | Replacing the collection disposes/unsubscribes the old one. |
| 4 | `ObservableCollection_AddItem_CallsHandlerAdd` | Adding to an `INotifyCollectionChanged` source triggers `handler.Add`. |
| 5 | `ObservableCollection_RemoveItem_CallsHandlerRemove` | Removing triggers `handler.Remove`. |
| 6 | `Dispose_UnsubscribesFromCollectionChanged` | After dispose, changes to the source collection no longer fire callbacks. |

---

## Verification Plan

### Test Framework Setup
Since there are no existing tests, we'll use **Unity Test Framework** (NUnit-based) in EditMode. Tests go in `Assets/Tests/EditMode/Infra/` with one folder per module.

> [!NOTE]
> Many of these classes depend on Unity types (Transform, GameObject, NetworkManager). For unit-testable cases (EventController, NavigationStack, EquatableWrapper, ViewEvent, BindSet, etc.), we can create plain NUnit EditMode tests. For classes tightly coupled to Unity runtime (NavigationProvider, VContainerAdapter, EventLedger), mock Transforms can be created via `new GameObject().transform` in EditMode tests.

### How to Run
```
# Open Unity Test Runner via Window > General > Test Runner
# Select EditMode tab, run all tests under Assets/Tests/EditMode/Infra/
```

### Manual Verification
I'd like your input on the following:
1. Are there any specific integration scenarios you'd like covered beyond unit tests?
2. Should tests for VContainer-coupled classes (VContainerRegistry, VContainerScope) be skipped or deferred given the tight coupling to the VContainer runtime?
3. For the MVVM binding tests, should we test with real expression trees or mock the binding path resolution?
