# Performance run summary

Generated from Unity Performance Testing JSON. **Alloc B/op** = median bytes/op (best available counter — see Bench.ByteSource). **Alloc/op** = median count of GC.Alloc Profiler-marker events per op (works in EditMode regardless of byte-counter availability).

| Test (method) | Time (ns/op) | Alloc B/op | Alloc/op | Gen0 |
|---------------|--------------|------------|----------|------|
| TryGet_KnownKey_50TypesRegistered | 16.07 | 0 | 0 | 0 |
| TryGet_UnknownKey_50TypesRegistered | 13.48 | 0 | 0 | 0 |
| Notify_50Subs_HalfUnsubscribeInline | 901.95 | 106 | 3.001 | 0 |
| Notify_50Subs_NoMutation | 1247.15 | 102 | 3.001 | 0 |
| Equals_VirtualDispatch_VsReferenceNull | 9.4 | 0 | 0 | 0 |
| ReferenceEquals_VsReferenceNull | 1.05 | 0 | 0 | 0 |
| EnumerateAll_OneTypeBucket_1k | 26491 | 0 | 2.01 | 0 |
| GetAll_OneTypeBucket_1k | 29826.5 | 122 | 3.01 | 0 |
| Execute_SingleMutator_OneSlice | 2415.27 | 32 | 5.0001 | 4 |
| Execute_TypedMutator_OneSlice_NoRegistry | 2340.26 | 32 | 5.0001 | 4 |
| Execute_TypedMutator_ValuePayload_OneSlice_NoRegistry | 2339.62 | 33 | 5.0001 | 4 |
| Execute_ValuePayload_OneSlice | 2481.9 | 64 | 6.0001 | 4 |
| Subscribe_PerCall_CachedDelegate | 182.51 | 48 | 1.0001 | 0 |

## Map vs Dictionary (same scenario)

| Scenario | Map Time | Map B/op | Map Allocs | Dict Time | Dict B/op | Dict Allocs |
|----------|----------|----------|------------|-----------|-----------|-------------|
