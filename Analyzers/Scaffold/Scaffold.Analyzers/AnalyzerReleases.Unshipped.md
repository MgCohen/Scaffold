### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-----------
SCA7101 | Hygiene | Warning | LiveOpsKeyAnalyzers — concrete `IGameModuleData` and types inheriting `ModuleRequest` in LiveOps DTO assemblies must have `[LiveOpsKey]`
SCA7102 | Hygiene | Info | LiveOpsKeyAnalyzers — string literal for `key` on `IReadableDataCache` / `IWriteableDataCache` methods
SCA7103 | Hygiene | Info | LiveOpsKeyAnalyzers — string literal for `GameApiEnvelopeRequest.RequestKey` (tests and unknown-key probe excluded)

### Changed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
SCA2001 | Style | Warning | MethodOrderAnalyzer — reports only when a direct callee is not declared strictly after a caller; no longer requires contiguous dependency blocks between caller and callee
SCA3005 | Design | Warning | NamespaceRootAnalyzer — first namespace segment must appear in `scaffold.SCA3005.root` and/or `scaffold.SCA3005.allowed_roots` (logic in NamespaceLayoutAnalysis)
SCA3006 | Design | Warning | NamespacePathAnalyzer — full namespace must match folder-derived path under `scaffold.SCA3006.content_roots` (shared NamespaceLayoutAnalysis with SCA3005)
SCA3004 | Design | Warning | SingleTopLevelNamespaceAnalyzer — one top-level namespace per file; types/delegates at file scope outside a block `namespace { }` (same ID, distinct messages)
SCA8001 | Quality | Info | DeadCodeInRuntimeAnalyzer — default severity suggestion
SCA8002 | Architecture | Info | PragmaWarningDisableAnalyzer — default severity suggestion
