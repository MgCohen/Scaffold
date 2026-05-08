# Project notes for Claude

## GraphFlow source generator — always redeploy the DLL after edits

After any edit to files under `Generators/Scaffold.GraphFlow.*/`, rebuild the
source generator AND copy the resulting DLL into the Unity package in the same
turn — Unity reads the DLL from `Assets/Packages/com.scaffold.graphflow/Generators/`,
not from the build output, so edits without a redeploy have no effect.

```bash
dotnet build Generators/Scaffold.GraphFlow.PackageGenerator/Scaffold.GraphFlow.PackageGenerator.csproj -c Release
cp Generators/Scaffold.GraphFlow.PackageGenerator/bin/Release/netstandard2.0/Scaffold.GraphFlow.PackageGenerator.dll \
   Assets/Packages/com.scaffold.graphflow/Generators/Scaffold.GraphFlow.PackageGenerator.dll
```

PowerShell equivalent (also syncs `AttributesLib.dll`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Generators/sync-unity-dlls.ps1
```

After the DLL is copied, Unity still needs to reimport it. Tell the user to
right-click the DLL in the Project window → Reimport (or wait for Unity's
auto-refresh on focus).
