# LiveOps

Authoritative documentation (Cloud Code module layout and Unity client): `[Assets/Packages/com.scaffold.liveops/README.md](../../Assets/Packages/com.scaffold.liveops/README.md)`.

**Per-package backend:** the host ships `**com.scaffold.liveops/Backend~`** (Deploy + manifest + `LiveOps.Deploy.sln`); feature packages add `**Backend~/Scaffold/<Feature>/**`. New modules can start from `[Tools/BackendTemplate/com.scaffold.example/README.md](../../Tools/BackendTemplate/com.scaffold.example/README.md)`.

**Operations (author vs consumer, CLI vs Unity, `LiveOps/` vs `Backend~/`):** [Backend-Authoring-Guide.md](Backend-Authoring-Guide.md).

Backend cache/batch behavior is documented under **Cloud Code data pipeline (backend)** in the package README.