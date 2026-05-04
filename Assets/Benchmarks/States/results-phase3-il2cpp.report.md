# Performance run summary

Generated from Unity Performance Testing JSON. **Alloc B/op** = median bytes/op (best available counter — see Bench.ByteSource). **Alloc/op** = median count of GC.Alloc Profiler-marker events per op (works in EditMode regardless of byte-counter availability).

| Test (method) | Time (ns/op) | Alloc B/op | Alloc/op | Gen0 |
|---------------|--------------|------------|----------|------|
| TryGet_KnownKey_50TypesRegistered | 20.26 | 0 | 0 | 0 |
| TryGet_UnknownKey_50TypesRegistered | 21 | 0 | 0 | 0 |
| Notify_50Subs_HalfUnsubscribeInline | 1003.55 | 12 | 1.001 | 0 |
| Notify_50Subs_NoMutation | 928.5 | 12 | 1.001 | 0 |
| Equals_VirtualDispatch_VsReferenceNull | 11.53 | 0 | 0 | 0 |
| ReferenceEquals_VsReferenceNull | 1.19 | 0 | 0 | 0 |
| EnumerateAll_OneTypeBucket_1k | 28987 | 0 | 2.01 | 0 |
| GetAll_OneTypeBucket_1k | 34336 | 122 | 3.01 | 0 |
| Execute_SingleMutator_OneSlice | 2695.98 | 81 | 4.0001 | 3 |
| Execute_TypedMutator_OneSlice_NoRegistry | 3409.06 | 80 | 4.0001 | 3 |
| Execute_TypedMutator_ValuePayload_OneSlice_NoRegistry | 2719.88 | 81 | 4.0001 | 3 |
| Execute_ValuePayload_OneSlice | 2791.74 | 19 | 5.0001 | 4 |
| Subscribe_PerCall_CachedDelegate | 215.43 | 48 | 1.0001 | 0 |

## Map vs Dictionary (same scenario)

| Scenario | Map Time | Map B/op | Map Allocs | Dict Time | Dict B/op | Dict Allocs |
|----------|----------|----------|------------|-----------|-----------|-------------|
