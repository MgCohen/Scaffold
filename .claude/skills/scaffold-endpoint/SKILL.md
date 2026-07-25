---
name: scaffold-endpoint
description: Use when the user wants to add a new Cloud Code endpoint (GameApi handler) to a Scaffold LiveOps module — e.g. "add an endpoint to Ads", "create a new GameApi request", "scaffold a Cloud Code handler", "add a DoThing endpoint to ModuleX". Generates the request/response DTOs under `LiveOps/Scaffold/<Module>.DTO/Request/`, wires `IGameApiHandler<TReq,TResp>` onto the module's service class (or creates a separate handler class), and runs the Backend~ refresh. Skips this skill for purely client-side wiring or for new modules from scratch — use the vertical-slice walkthrough for those.
---

# scaffold-endpoint

Generate the boilerplate for one new GameApi endpoint on an existing LiveOps module. The skill writes the request/response DTOs and the handler signature; the user fills in the `HandleAsync` body.

Repo conventions live in:
- `Docs/Standards/Module-Vertical-Slice.md` (especially §3 keys and §4 backend module)
- `Docs/Core/LiveOpsKeys.md` (`[LiveOpsKey]`, `[GameApiRequest]`, `KeyOf<T>`)
- `Tools/BackendTemplate/com.scaffold.example/Backend~/Scaffold/Example.DTO/Request/` (canonical request/response shape)
- `LiveOps/Scaffold/Ads/AdsService.cs` (canonical handler with real logic)
- `LiveOps/Scaffold/DirectPush/` (canonical multi-handler module)

## Inputs to gather (ask the user before generating)

If the user's invocation already supplies these, skip the question. Otherwise prompt for each:

1. **Module name** — e.g. `Ads`, `DirectPush`, `ModuleX`. Must already exist as `LiveOps/Scaffold/<Module>/<Module>Service.cs` (or a comparable handler file). If it does not, stop and tell the user to run the new-module bootstrap from `Docs/Standards/Module-Vertical-Slice.md` §8 first.
2. **Endpoint name** — verb-noun, e.g. `WatchAd`, `DoThing`, `ClaimReward`. The wire key defaults to `<Endpoint>Request` (the type name). Override via `[GameApiRequest("Custom.Wire")]` only if the user explicitly asks for a stable name across renames.
3. **Request fields** — list of `{ name, C# type, default? }`. Default to `string Message = string.Empty` when the user has nothing specific in mind, but ask first.
4. **Response shape** — one of:
   - **`refresh`** (default): the response wraps a refreshed `<Module>Data` snapshot, matching `DoThingResponse` and `WatchAdResponse`.
   - **`custom`**: a free-form response with named fields the user provides.
5. **Handler placement** — one of:
   - **`add-to-service`** (default): append `, IGameApiHandler<<Endpoint>Request, <Endpoint>Response>` to the existing `<Module>Service` class declaration and add a new `HandleAsync` method.
   - **`new-handler-class`**: create `LiveOps/Scaffold/<Module>/<Endpoint>Handler.cs` as a sealed class implementing only this one handler. Use this when the responsibility is clearly distinct (precedent: `DirectPush/DirectPush{Player,Project,Self}PushHandler.cs`).

## Pre-generation validation gates

Run these before writing any file. Stop and report on first failure.

1. **Module exists** — `LiveOps/Scaffold/<Module>/` and `LiveOps/Scaffold/<Module>.DTO/Request/` must both exist. If `Request/` is missing, create it.
2. **Wire-key collision** — grep `LiveOps/Scaffold/**/Request/*.cs` for `class <Endpoint>Request` and for `[GameApiRequest("<Endpoint>Request")]`. If any other module already declares this name, stop: `GameApiRegistry` would throw at startup. Suggest a different name or a `[GameApiRequest("...")]` override.
3. **Response-name collision** — same grep for `class <Endpoint>Response`. Allowed to reuse if the user explicitly says so (e.g. several handlers all return `SendPushResponse` in `DirectPush`); otherwise warn.
4. **Snapshot type exists** — for `refresh` shape, confirm `LiveOps/Scaffold/<Module>.DTO/<Module>Data.cs` exists and the class is named `<Module>Data` and implements `IGameModuleData`. If it doesn't match, ask the user for the actual snapshot type name.
5. **csprojs exist** — `LiveOps/Scaffold/<Module>/Scaffold.LiveOps.<Module>.csproj` and `LiveOps/Scaffold/<Module>.DTO/Scaffold.LiveOps.<Module>.DTO.csproj`. Both must be present (the deploy build globs `Scaffold/**/*.csproj` automatically; csproj edits are not required to register a new file).

## File templates

Substitute `<Module>`, `<Endpoint>`, and the user-supplied field list. Namespaces and `[LiveOpsKey]` values follow the existing convention exactly.

### Request DTO — `LiveOps/Scaffold/<Module>.DTO/Request/<Endpoint>Request.cs`

    using LiveOps.DTO.Keys;
    using LiveOps.DTO.ModuleRequest;

    namespace LiveOps.Modules.DTO.<Module>
    {
        [LiveOpsKey("<Endpoint>Request")]
        public class <Endpoint>Request : ModuleRequest<<Endpoint>Response>
        {
            // <user-supplied fields, one per line, with defaults where given>
            public string Message { get; set; } = string.Empty;
        }
    }

### Response DTO — refresh shape — `LiveOps/Scaffold/<Module>.DTO/Request/<Endpoint>Response.cs`

    using LiveOps.DTO.ModuleRequest;

    namespace LiveOps.Modules.DTO.<Module>
    {
        public class <Endpoint>Response : ModuleResponse
        {
            public <Endpoint>Response(<Module>Data data)
            {
                Data = data;
            }

            public <Module>Data Data { get; protected set; }
        }
    }

### Response DTO — custom shape

Same file location and namespace; replace the `Data` property with the user-supplied fields. Constructor takes those fields. Always extend `ModuleResponse`.

### Handler method — appended to `<Module>Service.cs` (mode `add-to-service`)

Two edits to the existing file:

1. Add `, IGameApiHandler<<Endpoint>Request, <Endpoint>Response>` to the class declaration.
2. Append this method inside the class (refresh shape shown — for custom, return the user-supplied response):

       public async Task<<Endpoint>Response> HandleAsync(GameApiSession session, <Endpoint>Request request)
       {
           IExecutionContext context = session.Context;
           IPlayerData player = session.Player;
           IRemoteConfig remoteConfig = session.RemoteConfig;

           // TODO: business logic.
           //   - Read persistence/config via player.GetOrSet / remoteConfig.Get.
           //   - Mutate persistence; call player.Set (cache only — flushed on batch dispose).
           //   - Compose the refreshed snapshot.

           <Module>Persistence persistence = await player.GetOrSet(context, new <Module>Persistence());
           <Module>Config config = await remoteConfig.Get(context, new <Module>Config());
           <Module>Data data = new <Module>Data(persistence, config);
           return new <Endpoint>Response(data);
       }

### New handler class (mode `new-handler-class`) — `LiveOps/Scaffold/<Module>/<Endpoint>Handler.cs`

    using System.Threading.Tasks;
    using LiveOps.GameApi;
    using LiveOps.Modules.DTO.<Module>;
    using Microsoft.Extensions.Logging;

    namespace LiveOps.Modules.<Module>
    {
        public sealed class <Endpoint>Handler : IGameApiHandler<<Endpoint>Request, <Endpoint>Response>
        {
            private readonly ILogger<<Endpoint>Handler> _logger;

            public <Endpoint>Handler(ILogger<<Endpoint>Handler> logger)
            {
                _logger = logger;
            }

            public async Task<<Endpoint>Response> HandleAsync(GameApiSession session, <Endpoint>Request request)
            {
                // TODO: business logic.
                throw new System.NotImplementedException();
            }
        }
    }

## Post-generation steps (execute, then report)

1. Run `pwsh -NoProfile -File .agents/scripts/refresh-liveops-template.ps1 -SkipGeneratorBuild` to mirror the new files into the package's `Backend~/`. (If the generator DLL is stale, drop `-SkipGeneratorBuild`.)
2. Print a summary block to the user:

       Created:
         LiveOps/Scaffold/<Module>.DTO/Request/<Endpoint>Request.cs
         LiveOps/Scaffold/<Module>.DTO/Request/<Endpoint>Response.cs
       Modified:
         LiveOps/Scaffold/<Module>/<Module>Service.cs
       Refreshed:
         Assets/Packages/com.scaffold.<module>/Backend~/Scaffold/<Module>.DTO/Request/
         Assets/Packages/com.scaffold.<module>/Backend~/Scaffold/<Module>/

       Next steps for you:
         1. Fill in HandleAsync in <Module>Service.cs (TODO block).
         2. Build:  dotnet build LiveOps/LiveOps.sln -c Release
         3. Call from the client:
              await liveOps.CallAsync<<Endpoint>Response>(new <Endpoint>Request { ... });

3. Do **not** modify the client-side `<Module>ClientService.cs` automatically. The client surface is a deliberate API decision — surface a recommendation in the report ("you may want to add a `<Endpoint>` method to `IModuleXClientService`") but leave it to the user.
4. Do **not** edit `LiveOps.Deploy.sln`. The deploy build globs `Scaffold/**/*.csproj` and the .DTO project includes all `*.cs` under it via the SDK default. New files compile without solution edits.

## What this skill does NOT do

- It does **not** create new modules. For that, point the user at `Docs/Standards/Module-Vertical-Slice.md` §8 (bootstrap checklist) and `Tools/BackendTemplate/com.scaffold.example/`.
- It does **not** write business logic — the `HandleAsync` body is a `TODO`.
- It does **not** generate optimistic-response handlers (`IRequestHandler<TReq,TResp>` from `com.scaffold.cloudcode`). If the user asks for an optimistic path, point at `Plans/CloudCode-Optimistic-Returns.md` and offer to add it as a follow-up step (separate flow).
- It does **not** generate tests. The repo's automated-testing posture (`Docs/Testing/`) does not yet require per-endpoint tests; revisit when it does.
- It does **not** edit `package.json` `scaffold.backend.slices`. (Once `Plans/Backend-Manifest-ExecPlan.md` lands, this skill should update the slice path if the endpoint creates a new csproj — but no new csproj is created today, so the manifest doesn't change.)

## Open decisions / TODOs for the skill itself

- Should the skill offer to add a stub `Validate` test under `LiveOps/Tests/LiveOps.Tests/` once a per-endpoint test pattern exists?
- Once the manifest plan lands (phase 2), this skill should re-read `package.json` `scaffold.backend` to confirm the module is declared before generating, and should fail fast if not.
- Consider a `--dry-run` mode that prints the planned diff without writing.
- Consider extracting the embedded templates into `.claude/skills/scaffold-endpoint/templates/*.cs.template` if they grow beyond ~30 lines each.
