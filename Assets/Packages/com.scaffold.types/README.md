# com.scaffold.types

In the **Scaffold holder**, sources live under **`Assets/Packages/com.scaffold.types/`** and are developed like normal project assets (no `file:` entry required in the holder’s `Packages/manifest.json`).

**Other Unity projects** can depend on it via:

- **Local path:** add to the consumer’s `Packages/manifest.json` a `file:` URL to this folder (see `C:\Unity\AITest\AITest` for an example).

- **Git subpath (published repo):** use the path where this package lives in the repository, for example:

  `"com.scaffold.types": "https://github.com/<org>/Scaffold.git?path=/Assets/Packages/com.scaffold.types#<branchOrTag>"`

When installed by UPM, Unity resolves the package under its normal package layout (e.g. package cache), not under the consumer’s `Assets/`.
