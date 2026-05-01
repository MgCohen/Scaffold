# com.scaffold.pooling

## TL;DR

- Purpose: **`Pool<T>`** for reusing instances of any type (plain C# or `MonoBehaviour`). Optional **`IPoolable`** provides **`OnTakenFromPool`**, **`OnReturnedToPool`**, and **`ReturnRequested`** so instances can reset state and request return into the pool.
- Location: `Assets/Packages/com.scaffold.pooling/Runtime/` — assembly **`Scaffold.Pooling`**. Optional VContainer wiring in **`Scaffold.Pooling.Container`** (`PoolingInstaller` is a no-op placeholder; register concrete pools at the app root).
- Depends on: none (engine reference allowed for future Unity helpers).
- **Consumers:** Reference **`Scaffold.Pooling`** from your module `.asmdef`. Construct **`new Pool<T>(factory, onDestroy, initialSize, maxSize)`** where you need pooling.

## `Pool<T>`

- Tracks **`Available`** (idle stack) and **`Active`** (instances currently checked out).
- **`Take()`** pops from the idle stack or invokes **`factory`**.
- **`Return(T item)`** validates the item is active, unsubscribes **`IPoolable.ReturnRequested`**, runs **`OnReturnedToPool`**, then pushes to the idle stack or calls **`onDestroy`** when the idle cap (**`maxSize`**) is reached.
- **`Clear()`** unsubscribes poolables, runs **`OnReturnedToPool`** on active items, calls **`onDestroy`** for every instance (active and idle).

## `IPoolable`

Implement on types that need reset/activation when pooled:

- **`OnTakenFromPool`** / **`OnReturnedToPool`** — e.g. enable/disable `GameObject`, clear fields.
- **`ReturnRequested`** — pool subscribes on **`Take`**; raise when the instance should return (e.g. animation finished).

## Assembly layout

| Assembly | Role |
|----------|------|
| `Scaffold.Pooling` | `Pool<T>`, `IPoolable` |
| `Scaffold.Pooling.Container` | `PoolingInstaller` (placeholder) |
| `Scaffold.Pooling.Tests` | Edit Mode tests |
