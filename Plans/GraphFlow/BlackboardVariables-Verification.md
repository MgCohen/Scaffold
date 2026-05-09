# Blackboard Variables — Local Verification Brief

Companion to `BlackboardVariables.md`. The plan is design-of-record;
this is the operational checklist a local agent runs after pulling
PR #49 to confirm the package compiles, all tests pass, and the GT
integration works against a real graph.

Branch: `claude/graph-flow-blackboard-vars-IsUNX` (PR #49)
Scope: Phases 1–6 + post-review fixes.

---

## 0. Pre-flight

1. Pull the branch.
2. Open the project in Unity. Wait for
   `Assets/Packages/com.scaffold.graphflow/Generators/Scaffold.GraphFlow.PackageGenerator.dll`
   to be reimported (right-click → Reimport if it doesn't auto-pick
   up).
3. **Expect:** zero `error CS` in the editor console. A small number
   of warnings is fine.
4. If errors mention "missing partial declaration" on
   `GetIntVariable` / `SetIntVariable` / `ObserveIntVariable`, the
   source generator didn't run. Verify the generator DLL is present
   and Unity has reloaded.

## 1. Run the test suite

Window → General → Test Runner → EditMode. 19 tests across 7 files
under `Scaffold.GraphFlow.Tests`.

### `VariableBagTests` (7) — no Unity dep

| Test | Expected |
|---|---|
| `SeedsTypedCellsFromDefaults` | int + string cells seeded from defaults |
| `TypeMismatchReturnsFalse` | `TryGetCell<float>` on int cell → false |
| `MissingKeyReturnsFalseAtRoot` | unknown id → false |
| `LookupCascadesThroughParents` | flow → graph → global all visible from flow |
| `WriteHitsOwningLayerImplicitly` | cached cell ref means writes target the owning bag (same instance returned across all 3 layers) |
| `ChangedFiresOnDistinctValueOnly` | same-value writes don't fire; distinct values fire each |
| `NonGenericTryGetCellReturnsBaseCell` | `TryGetCell(id, out VariableCell)` returns base type with `Type` and `Id` |

### `VariableWiringTests` (4) — three-layer chain

| Test | Expected |
|---|---|
| `RunnerVariablesSeededFromAsset` | after `Build`, `runner.Variables` has the asset's defaults; parent null |
| `RunnerParentBagComesFromCreateParentBag` | override returning a bag becomes `Parent`; lookup cascades |
| `FlowVariablesParentChainsToRunner` | `flow.Variables.Parent === runner.Variables` |
| `SetThroughFlowHitsRunnerOwnedCell` | `flow.Variables.TryGetCell` returns the same cell instance as the runner's; writing through flow visible at runner |

### `VariableEdgeTests` (2) — variable-bound input ports (Phase 3)

| Test | Expected |
|---|---|
| `VariableEdgeWiresInputPortToBag` | `VariableEdge` from `speed` → `Doubler.In` reads cell at runtime; `21 * 2 = 42` |
| `SettingVariableAfterBuildFlowsThroughToOutput` | mutating cell post-Build → next read reflects new value |

### `VariableGetSetTests` (3) — Get/Set nodes (Phase 4)

| Test | Expected |
|---|---|
| `GetIntVariableReadsCellValue` | reads seed; mutating cell + `flow.InvalidateAll()` reads new value |
| `SetIntVariableWritesCellThroughFlowExecution` | flow drives Set → cell updated |
| `GetUnsetVariableReturnsTypedDefault` | unknown variable id → `default(int) = 0` |

### `VariableObserveTests` (1) — cell-change-driven flows (Phase 5)

| Test | Expected |
|---|---|
| `CellChangeFiresObserverFlowWithNewValue` | `cell.Value = 7; cell.Value = 7; cell.Value = 11;` → recorder ends with `[7, 11]` (same-value skipped) |

### `VariableEndToEndTests` (2) — full integration (Phase 6)

| Test | Expected |
|---|---|
| `FullChain_GraphAndGlobalVariables_VariableEdges_GetSetObserve` | Graph + global vars; `Start → SetHp(75) → SetScore(250)` runs; `hp = 75` graph layer, `score = 250` in **global bag** (bubble-up); Observer recorded `[75]` then `[75, 60, 30]` |
| `VariableBoundPort_FollowsCellMutations` | bound port reads default 21 → 42; mutate cell to 50 + re-Run → 100 |

### `RuntimeSmokeTests` (existing) — must remain green

Existing smoke tests should pass. Assets without variables seed an
empty bag; new wiring is a no-op for graphs that don't use variables.

---

## 2. Manual editor checks (GT integration, Phase 3)

Can't be unit-tested without a real GT graph asset. Walk through these
once the package compiles.

### A. Variable bake into a fresh graph

1. Create a new graph asset for one of the existing samples
   (CardSandbox).
2. Open it in the GT editor.
3. In the **Blackboard panel**, declare a variable: name `hp`, type
   `int`, default `100`.
4. Save / re-bake.
5. Inspect the runtime asset (the baked `.asset` next to the GT
   graph). **Expect:**
   - `variables` list has one entry with `id` populated (a Hash128
     GUID), `name = "hp"`, `typeName` containing `Int32`,
     `defaultValue` is a polymorphic `IntDefault { value = 100 }`.
   - `variableEdges` is empty.
   - `schemaVersion = 4`.

### B. Variable node feeding a port

1. Drop the `hp` variable on the canvas as an `IVariableNode`.
2. Wire its output to an input port on a downstream node.
3. Re-bake.
4. **Expect:**
   - `variableEdges` has one entry: `variableId` = the GUID,
     `toNodeId` = downstream node id, `toPortName` = the port name.
   - `connections` does **not** contain that edge.
   - The variable node itself is **not** in `nodes` (skipped from
     runtime emission; identity captured via the edge).

### C. Orphaned-variable warning

1. Drop a variable node on the canvas.
2. **Delete the underlying variable** from the blackboard panel.
3. Re-bake.
4. **Expect:** `Debug.LogWarning` —
   `GraphFlow bake: variable node ... references unknown variable
   '...'; edge skipped.` Bake should not throw.

### D. Get / Set / Observe nodes appear in the picker

1. Open the node creation menu in a graph editor.
2. **Expect:** under `Variables/Get`, `Variables/Set`,
   `Variables/Observe` — typed entries for `int` / `float` / `bool` /
   `string` / `Object`. (15 nodes total.)
3. Drop a `GetIntVariable`, paste a variable GUID into the
   `variableId` `[SerializeField]` slot. (No picker drawer yet.)

### E. Sample regression

1. Open CardSandbox.
2. Bake. Run tests under `Scaffold.GraphFlow.CardSandbox.Tests`.
3. **Expect:** all green.

---

## 3. What to flag back

- Any compile error referencing `RuntimeVariable`, `VariableCell`,
  `IVariableBag` — runtime contract drift.
- Test asserting `AreSame(...)` on cells failing — cell-cache identity
  broke.
- Bake-time `NullReferenceException` in `EditorVariableIdentity`
  (`m_Implementation` / `Guid` property) — GT internal field name
  drifted under your GT version.
- `[SerializeReference]` field empty in the inspector after a domain
  reload — managed-reference deserialization issue.
- `Variables/Get` etc. not in the node picker — generator didn't pick
  up the new `[GraphNode]` types. Right-click reimport on the
  generator DLL.

## 4. Out of scope (deferred)

See `BlackboardVariables.md` §"Post-v1 follow-up backlog" for the
prioritized list. Notable deferrals:

- No GT-authored sample asset on disk (this brief's §2 is the
  workaround until one exists).
- No editor variable-id picker drawer — paste GUIDs by hand.
- Get/Set/Observe trios are hand-authored, not generator-emitted.
- Observe subscriptions never tear down (leaks documented in source).
- No bake-time type validation on `VariableEdge` (Backlog #2 — fix
  before this lands in production graphs).

If §1 (automated tests) fails, that's a runtime-contract bug. If §2
(editor checks) fails, that's a GT-integration drift. Different fix
paths.
