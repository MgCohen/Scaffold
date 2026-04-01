# ExecPlan: Refactor & reorganize `.agents/scripts`

**Goal:** Reduce duplication, clarify “daily” vs “migration” vs “diagnostic” tooling, and make the same workflows usable on **Windows and macOS/Linux** (cross-platform project).

**Non-goals:** Rewriting quality semantics, changing CI contract strings (`TOTAL:`, `ISSUE:`, `AGENT_*` blocks), or moving scripts out of `.agents/` without updating all doc references.

---

## 1. What to kill (or archive)

| Item | Recommendation | Rationale |
|------|----------------|-----------|
| **Nothing must be deleted on day one** | Prefer **rename + archive** under `.agents/scripts/_archive/migration/` (or `migration/archive/`) | Migration scripts may still help forks, cherry-picks, or doc/csproj repair. Hard delete loses history of “how we got here.” |
| **`*.cmd` wrappers** | **Keep** `validate-changes.cmd` and `run-coverage-audit.cmd` | Zero-friction on Windows CMD and existing docs. They are tiny; cost is negligible. |
| **Duplicate replacement tables** (three files) | **Do not delete files** until replacements are merged into one source (see §2) | Killing two of three before merge loses the mapping. |

**Optional later:** After one release cycle with no one using archived migration scripts, delete the archive folder or move to git history only.

---

## 2. What to merge

### 2.1 Unity test runners → one implementation

- **Merge:** `run-editmode-tests.ps1` and `run-playmode-tests.ps1` into a single script, e.g. `run-unity-tests.ps1`, with a mandatory or default **`-TestPlatform`** (`EditMode` | `PlayMode`).
- **Keep thin shims (temporary):** `run-editmode-tests.ps1` and `run-playmode-tests.ps1` as **10-line forwarders** to preserve every README and `AGENTS.MD` link, **or** update all references once and delete shims in the same PR.

### 2.2 Unity path resolution → one module

- **Merge:** `Get-UnityVersion`, `Resolve-UnityPath`, and shared test-result helpers into **one dot-sourced file**, e.g. `.agents/scripts/lib/UnityEditorPaths.ps1` (name TBD).
- **Consumers:** `check-unity-compilation.ps1`, `run-unity-tests.ps1` (merged runner), and optionally `run-coverage-audit.ps1`.
- **Resolve behavior:** Encode explicitly:
  - **Strict:** project `ProjectVersion.txt` → exact Hub editor path only (good for **compilation** reproducibility).
  - **Optional fallback:** newest installed editor (current test-runner behavior) behind **`-AllowHubVersionFallback`** or env var `SCAFFOLD_UNITY_ALLOW_VERSION_FALLBACK=1` so Mac/CI behavior is predictable and documented.

### 2.3 Migration path mappings → one data file

- **Merge:** The repeated `Assets/Scripts/...` → `Assets/Packages/com.scaffold.*` tables from:
  - `rewrite-docs-package-paths.ps1`
  - `rewrite-unity-root-csproj-paths.ps1`
  - (and conceptually align with `migrate-scaffold-packages.ps1` package IDs)
- **Create:** `.agents/scripts/migration/package-path-mappings.json` (or `.psd1`) listing:
  - legacy path segments,
  - UPM package folder name,
  - optional notes.
- **Scripts become:** readers of that file + small platform-specific logic (markdown vs csproj slash style).

---

## 3. What to change

### 3.1 Cross-platform execution

| Area | Change |
|------|--------|
| **Shell** | Standardize on **PowerShell 7+ (`pwsh`)** for cross-platform scripts. Document that Windows PowerShell 5.1 is **best-effort** where .NET/Unity APIs behave the same (existing scripts already target 5.x quirks). |
| **Invocations** | Add **`#!/usr/bin/env pwsh`** shebang to entry scripts intended to run as `./script.ps1` on Unix (chmod +x). |
| **Paths** | Audit for hard-coded `\` or `C:\`; use `Join-Path` and repo-relative resolution from `$PSScriptRoot`. |
| **Process start** | Keep `UnityProcess.ps1` logic; verify `ProcessStartInfo` + quoting under **macOS** (paths with spaces). |
| **Line endings** | `.gitattributes` for `*.ps1` → `lf` if mixed endings cause shebang issues on Linux/macOS. |

### 3.2 Entrypoints for non-Windows users

- **Add:** `validate-changes.sh` and `run-coverage-audit.sh` (and optionally `run-unity-tests.sh`) that:
  - Prefer `command -v pwsh` and run `pwsh -NoProfile -File "$(dirname "$0")/validate-changes.ps1" "$@"`.
  - If `pwsh` missing, print install instructions (Homebrew: `brew install --cask powershell`, etc.) and link to `.agents/scripts/README.md`.

### 3.3 Documentation updates (same PR as structure)

- **`Docs/Testing.md`**, **`AGENTS.MD`**, **`Architecture.md`**: add a **“macOS / Linux”** subsection with `pwsh` + `.sh` wrapper examples.
- **Module READMEs** that reference `.\.agents\scripts\...`: add forward-slash or `pwsh -File` variants so copy-paste works on Unix.

### 3.4 `validate-changes.ps1`

- After extracting `Invoke-PowerShellScript` / child script calls, consider calling merged `run-unity-tests.ps1` with `-TestPlatform` instead of two separate files (internal only; external API unchanged if shims remain).

### 3.5 `scan-meta-health.ps1`

- No merge required; optionally move under `diagnostics/` and mention in README. Consider **non-zero exit** when duplicates/missing meta count &gt; 0 for CI opt-in (separate flag `-FailOnIssues`).

---

## 4. What to organize (folder layout)

Proposed target (adjust names to taste):

```text
.agents/
  scripts/
    README.md                 # how to install pwsh, UNITY_PATH, run gates on Win/Mac/Linux
    validate-changes.ps1      # main orchestrator (stays at root for short paths)
    validate-changes.cmd
    validate-changes.sh       # new
    run-coverage-audit.ps1
    run-coverage-audit.cmd
    run-coverage-audit.sh     # new
    run-unity-tests.ps1       # merged Edit/Play runner
    run-editmode-tests.ps1    # shim → run-unity-tests.ps1 -TestPlatform EditMode (optional)
    run-playmode-tests.ps1    # shim → run-unity-tests.ps1 -TestPlatform PlayMode (optional)
    check-analyzers.ps1
    check-unity-compilation.ps1
    check-scripts-asmdef-references.ps1
    check-pragma-warning-suppressions.ps1
    pragma-warning-disable-allowlist.txt
    lib/
      UnityProcess.ps1
      UnityEditorPaths.ps1    # new: version + resolve + shared helpers
      InvokeChildScript.ps1   # optional: extract Invoke-PowerShellScript from validate-changes
    migration/
      migrate-scaffold-packages.ps1
      rewrite-docs-package-paths.ps1
      rewrite-unity-root-csproj-paths.ps1
      package-path-mappings.json   # new
    diagnostics/
      scan-meta-health.ps1
    _archive/                 # optional: retired one-offs after a deprecation period
```

**Principle:** Anything **imported** (dot-sourced) lives under `lib/`; anything humans run by name stays discoverable at `scripts/` root **or** is listed in `README.md`.

---

## 5. What to create (gaps)

| Deliverable | Purpose |
|-------------|---------|
| **`.agents/scripts/README.md`** | Single source: prerequisites (`pwsh`), `UNITY_PATH`, how to run full gate vs skip tests, macOS/Linux notes, link to `Docs/Testing.md`. |
| **Setup / env (use-once or rare)** | |
| **`.agents/scripts/setup-environment.ps1`** | Idempotent: check `pwsh` version, print `export UNITY_PATH='...'` examples for zsh/bash, optionally write **`.agents/local.env.example`** → user copies to `.agents/local.env` (gitignored) and sources it. |
| **`.agents/scripts/setup-environment.sh`** | Same for bash: detect `pwsh`, remind to install, print `UNITY_PATH` export line for `~/.zshrc` / `~/.bashrc`. |
| **`.gitignore`** entry | `/.agents/local.env` (or similar) if secrets/paths are stored. |
| **Unix wrappers** | `validate-changes.sh`, `run-coverage-audit.sh` (see §3.2). |
| **`package-path-mappings.json`** | Single mapping source for migration scripts (§2.3). |
| **`lib/UnityEditorPaths.ps1`** | Shared Unity discovery (§2.2). |

**Environment variables to document:**

- `UNITY_PATH` — full path to `Unity` (macOS: `Unity.app/Contents/MacOS/Unity`) or `Unity.exe` on Windows.
- Optional: `SCAFFOLD_UNITY_ALLOW_VERSION_FALLBACK` — only if you implement fallback behind a flag.

---

## 6. Suggested execution order (phases)

1. **Document & env (low risk)**  
   Add `README.md`, `setup-environment.ps1` / `.sh`, `.gitignore` for local env, verify `pwsh` on a Mac once.

2. **Extract `lib/UnityEditorPaths.ps1` + `UnityProcess.ps1` move**  
   Wire `check-unity-compilation.ps1` and one test runner; run `validate-changes.ps1 -SkipTests` locally.

3. **Merge test runners**  
   Introduce `run-unity-tests.ps1`, switch `validate-changes.ps1` to it, keep shims or bulk-update docs.

4. **Migration consolidation**  
   Add `package-path-mappings.json`, refactor three migration scripts to read it; move to `migration/`.

5. **Cross-platform wrappers**  
   Add `*.sh`, update `Docs/Testing.md` / `AGENTS.MD`.

6. **Archive**  
   Move obsolete scripts to `_archive/` only after a full milestone gate passes and team sign-off.

---

## 7. Verification checklist

- [ ] From **repo root** on Windows: `pwsh -File .agents/scripts/validate-changes.ps1 -SkipTests`
- [ ] Same on **macOS** with Unity Hub install + `UNITY_PATH` set
- [ ] `./.agents/scripts/validate-changes.sh` (or `bash ...`) works when `pwsh` is on `PATH`
- [ ] All `AGENT_*` / `TOTAL:` / `ISSUE:` consumers (if any automation) still parse output
- [ ] Grep for old paths after moves: update CI, `Docs/`, module READMEs

---

## 8. Out of scope (separate ExecPlan)

- Replacing PowerShell with Python/Node for gates (large rewrite; PowerShell 7 is already cross-platform).
- Docker-wrapped Unity (different ExecPlan for CI images).
