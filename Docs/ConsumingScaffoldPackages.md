# Consuming Scaffold UPM packages

This repository is the **holder** project: first-party modules live under `Assets/Packages/com.scaffold.*/` with a `package.json` at each package root. In the holder, those folders compile as **normal project assets**; you do **not** need `file:` entries in `Packages/manifest.json` for them.

## Git subpath (other Unity projects)

Add a dependency to the consumer’s `Packages/manifest.json` using your Git remote, a **path** to the package folder in this repo, and a **revision** (`#branch`, `#tag`, or commit):

```json
{
  "dependencies": {
    "com.scaffold.events": "https://github.com/<org>/Scaffold.git?path=/Assets/Packages/com.scaffold.events#main"
  }
}
```

Repeat for each `com.scaffold.*` package you need. Internal dependencies are declared in each package’s `package.json` `dependencies` map (versions are aligned to `0.1.0` in this monorepo until you publish a different policy).

## Local `file:` (development)

Point at a clone of this repository on disk:

```json
"com.scaffold.types": "file:../Scaffold/Assets/Packages/com.scaffold.types"
```

Adjust the relative or absolute path to match your machine.

## Versioning and updates

- **When:** Follow SemVer for each package’s `package.json` `version` (see `Plans/ModulesAsUpmPackages/ModulesAsUpmPackages-ExecPlan.md` → **Versioning**).
- **Where:** The `version` field in `Assets/Packages/<packageId>/package.json`, plus optional `CHANGELOG.md` next to it.
- **How:** Bump version and changelog, commit, push, and pin consumers to the Git revision you want (`#v1.2.0` or branch name).

## External dependencies

Some packages depend on **`com.scaffold.schemas`** (Git URL in the holder `Packages/manifest.json`), **VContainer**, **Unity Addressables**, **UGUI**, and other Unity/registry packages. Consumer projects must still have compatible versions in their own `manifest.json` or rely on transitive resolution from your Scaffold packages where UPM supports it.
