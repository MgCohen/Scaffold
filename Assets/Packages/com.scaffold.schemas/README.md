# com.scaffold.schemas

SchemaObject / Schema runtime and editor tooling for component-like data on ScriptableObjects.

In the **Scaffold holder**, sources live under **`Assets/Packages/com.scaffold.schemas/`** and compile as normal project assets (no `file:` entry in the holder’s `Packages/manifest.json`).

**Other Unity projects** can depend on it via Git subpath, for example:

`"com.scaffold.schemas": "https://github.com/<org>/Scaffold.git?path=/Assets/Packages/com.scaffold.schemas#<branchOrTag>"`

The historical standalone repository was **https://github.com/ScaffoldLibrary/Schemas**; development for Scaffold now tracks this monorepo copy.
