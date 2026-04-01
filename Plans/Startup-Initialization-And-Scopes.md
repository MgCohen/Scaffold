# Startup (split outlines)

This topic is split into two plan documents:

1. **[Startup-Initialization-Ordering.md](Startup-Initialization-Ordering.md)** — Dependency-derived async initialization (`IAsyncInitializable`, graph from ctor/inject, topological levels, runner API).
2. **[Startup-Two-Scope-Preload.md](Startup-Two-Scope-Preload.md)** — Base scope (Addressables + future early checks), preload, main scope, and how init ordering runs per scope.
