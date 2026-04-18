# Scaffold Infra Navigation

Authoritative module documentation: [`Assets/Packages/com.scaffold.navigation/README.md`](../../Assets/Packages/com.scaffold.navigation/README.md).

Each `ViewConfig` asset can use **View asset source** in the inspector: **Addressables** (assign the addressable `GameObject` as today) or **Direct prefab** (assign a project prefab; runtime uses `Object.Instantiate` under the navigation view holder, not Addressables, for that screen). Existing assets default to Addressables so current projects keep prior behavior. Schemas and type resolution still apply after `OnValidate` for the active source.
