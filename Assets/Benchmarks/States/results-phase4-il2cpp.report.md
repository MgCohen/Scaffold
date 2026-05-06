# Performance run summary

Generated from Unity Performance Testing JSON. **Alloc B/op** = median bytes/op (best available counter — see Bench.ByteSource). **Alloc/op** = median count of GC.Alloc Profiler-marker events per op (works in EditMode regardless of byte-counter availability).

| Test (method) | Time (ns/op) | Alloc B/op | Alloc/op | Gen0 |
|---------------|--------------|------------|----------|------|
| TryGet_KnownKey_50TypesRegistered | 16.37 | 0 | 0 | 0 |
| TryGet_UnknownKey_50TypesRegistered | 13.54 | 0 | 0 | 0 |
| Notify_50Subs_HalfUnsubscribeInline | 507.1 | 8 | 1.001 | 0 |
| Notify_50Subs_NoMutation | 731.5 | 8 | 1.001 | 0 |
| Equals_VirtualDispatch_VsReferenceNull | 9.37 | 0 | 0 | 0 |
| ReferenceEquals_VsReferenceNull | 1.01 | 0 | 0 | 0 |
| EnumerateAll_OneTypeBucket_1k | 17641.5 | 0 | 1.01 | 0 |
| GetAll_OneTypeBucket_1k | 29322.5 | 81 | 3.01 | 0 |
| Execute_SingleMutator_OneSlice | 2262.39 | 80 | 4.0001 | 3 |
| Execute_TypedMutator_OneSlice_NoRegistry | 2046.13 | 79 | 4.0001 | 3 |
| Execute_TypedMutator_ValuePayload_OneSlice_NoRegistry | 2088.15 | 80 | 4.0001 | 3 |
| Execute_ValuePayload_OneSlice | 2279.64 | 19 | 5.0001 | 4 |
| Subscribe_PerCall_CachedDelegate | 186.58 | 48 | 1.0001 | 0 |

## Map vs Dictionary (same scenario)

| Scenario | Map Time | Map B/op | Map Allocs | Dict Time | Dict B/op | Dict Allocs |
|----------|----------|----------|------------|-----------|-----------|-------------|
