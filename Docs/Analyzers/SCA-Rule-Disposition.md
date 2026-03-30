# SCA rule disposition lookup

Living table: what to do with each diagnostic. Update as decisions land.

**ID format:** **`SCA` + one category digit (1–8) + three-digit rule index** within that category — e.g. **SCA1001** = category 1, first rule; **SCA3001** = category 3, first rule. *(Retired numeric-only IDs like **SCA0001** are fully superseded.)*

**Legend:** *Review* = read rule + tests + `.editorconfig` interaction; *OK* = keep as-is unless new evidence; *MVVM pack* = move to dedicated MVVM analyzer project (with MVVM source generator stack).

## Categories

Eight buckets so rules can be **documented, configured, and shipped** (e.g. MVVM pack) without mixing unrelated concerns.

| Category | Meaning | Typical severity stance |
| -------- | ------- | ------------------------ |
| **1 — Surface & naming** | Line-level readability: identifiers, comments, brace style, expression bodies, “single-line” formatting. | Often **warning/suggestion** for AI iteration; strict in CI if desired. |
| **2 — Structure & decomposition** | Shape and size of logic: call order, nesting depth, method length, static-method constraints. | Mixed: some **error**-worthy (order), some tunable (SCA2003). |
| **3 — Organization & placement** | Where types live vs folders/namespaces, member order, one namespace per file. | Usually **error** for repo navigability. |
| **4 — Module layout & boundaries** | Assemblies, required folders, asmdef conventions, `*.Runtime` boundaries. | **Error** — architectural mistakes. |
| **5 — Guards & validation** | Invariants at public method/constructor entry. | **Error** for correctness-sensitive surface. |
| **6 — Unity & serialization** | Engine API choices, `[Serializable]` patterns, UI text stack. | Depends on Unity version / project. |
| **7 — MVVM** | Toolkit attributes, bind APIs, base types — **candidate for separate analyzer assembly**. | Align with MVVM package version. |
| **8 — Hygiene & policy** | Dead code, `#pragma warning disable` — quality gates, not layout. | Often **suggestion** or narrow scope (SCA8001 / SCA8002). |

## Disposition by rule

| ID | Category | Disposition | Notes |
| -- | -------- | ----------- | ----- |
| **SCA1001** | 1 — Surface & naming | Review details and implementation | |
| **SCA2001** | 2 — Structure & decomposition | Review details and implementation | Direct callee-after-caller only; see [Analyzers.md](Analyzers.md#sca2001---method-order). |
| **SCA2002** | 2 — Structure & decomposition | Review details and implementation | |
| **SCA1002** | 1 — Surface & naming | Review details and implementation | |
| **SCA1003** | 1 — Surface & naming | **OK** | Keep rule and implementation as-is (no removal). |
| **SCA2003** | 2 — Structure & decomposition | **OK** | Default **15** lines; `scaffold.SCA2003.max_lines`; empty lines and fluent/builder continuation lines (leading `.`) excluded — see [SCA2003.md](../../Plans/SCA-Analyzer-Revamp/SCA2003.md). |
| **SCA3001** | 3 — Organization & placement | Review details and implementation | Config: `scaffold.SCA3001.content_roots`, `root_namespace_aliases`, `first_segment_ignore` — see [SCA3001.md](../../Plans/SCA-Analyzer-Revamp/SCA3001.md). |
| **SCA1004** | 1 — Surface & naming | OK | |
| **SCA0009** | *—* | *Merged into SCA1004* | **ID not emitted** — see [SCA0009.md](../../Plans/SCA-Analyzer-Revamp/SCA0009.md). |
| **SCA1005** | 1 — Surface & naming | OK | |
| **SCA0011** | *—* | *Removed* | **ID not emitted** — see [SCA0011.md](../../Plans/SCA-Analyzer-Revamp/SCA0011.md). |
| **SCA5001** | *—* | *Disabled* | **ID not emitted** — replan; see [SCA5001.md](../../Plans/SCA-Analyzer-Revamp/SCA5001.md); archive [SCA5001-Disabled](../../Analyzers/Scaffold/_Archive/SCA5001-Disabled/README.md). |
| **SCA6001** | 6 — Unity & serialization | Review details and implementation | Ensure correct cases are flagged. |
| **SCA2004** | 2 — Structure & decomposition | Review details and implementation | Ensure correct cases are flagged. |
| **SCA3002** | 3 — Organization & placement | **OK** | One top-level type per Unity script file; nested types allowed — [SCA3002.md](../../Plans/SCA-Analyzer-Revamp/SCA3002.md). |
| **SCA6002** | 6 — Unity & serialization | **OK** | `scaffold.SCA6002.forbidden_types` (`Metadata=>Replacement`; default Unity `Text` → TMPro). |
| **SCA5002** | *—* | *Disabled* | **ID not emitted** — replan with **SCA5001**; see [SCA5002.md](../../Plans/SCA-Analyzer-Revamp/SCA5002.md); archive [SCA5002-Disabled](../../Analyzers/Scaffold/_Archive/SCA5002-Disabled/README.md). |
| **SCM001** | 7 — MVVM | **OK** *(MVVM analyzer)* | Implemented in [`Scaffold.Mvvm.Analyzers`](../../Generators/Scaffold.Mvvm.Analyzers/) — MVVM base types (replaces **SCA0018**). |
| **SCM002** | 7 — MVVM | **OK** *(MVVM analyzer)* | Same assembly — attributes resolve (replaces **SCA0019**). |
| **SCA3003** | 3 — Organization & placement | OK | |
| **SCM003** | 7 — MVVM | **OK** *(MVVM analyzer)* | Same assembly — bind APIs (replaces **SCA0021**). |
| **SCA4001** | *—* | *Disabled* | **ID not emitted** — boundary/contract replan; see [SCA4001.md](../../Plans/SCA-Analyzer-Revamp/SCA4001.md); archive [SCA4001-Disabled](../../Analyzers/Scaffold/_Archive/SCA4001-Disabled/README.md). |
| **SCA4002** | *—* | *Disabled* | **ID not emitted** — folder layout replan; see [SCA4002.md](../../Plans/SCA-Analyzer-Revamp/SCA4002.md); archive [SCA4002-Disabled](../../Analyzers/Scaffold/_Archive/SCA4002-Disabled/README.md). |
| **SCA4003** | *—* | *Disabled* | **ID not emitted** — asmdef layout replan; see [SCA4003.md](../../Plans/SCA-Analyzer-Revamp/SCA4003.md); archive [SCA4003-Disabled](../../Analyzers/Scaffold/_Archive/SCA4003-Disabled/README.md). |
| **SCA0025** | *—* | *N/A* | **No rule** in `Scaffold.Analyzers` today (ID unused / skipped in sequence). |
| **SCA2006** | *—* | *Removed* | **ID not emitted** — same-layer init rule removed; see [SCA2006.md](../../Plans/SCA-Analyzer-Revamp/SCA2006.md); archive [SCA2006-Removed](../../Analyzers/Scaffold/_Archive/SCA2006-Removed/README.md). |
| **SCA3004** | 3 — Organization & placement | OK | |
| **SCA1006** | 1 — Surface & naming | OK | |
| **SCA1007** | 1 — Surface & naming | OK | Review cases only. |
| **SCA8001** | 8 — Hygiene & policy | **OK** | Default **Info**; Unity callback exemptions + `scaffold.SCA8001.exempt_method_names`. |
| **SCA8002** | 8 — Hygiene & policy | **OK** | Default **Info**; consecutive-line disable/restore pairs (same codes) exempt. |
| **SCA2005** | 2 — Structure & decomposition | **OK** | Nested static types inside non-static types — [SCA2005.md](../../Plans/SCA-Analyzer-Revamp/SCA2005.md). |


## Related

- [Analyzers.md](Analyzers.md) — setup and maintenance.

