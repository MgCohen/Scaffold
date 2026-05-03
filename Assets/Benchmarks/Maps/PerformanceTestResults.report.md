# Performance run summary

Generated from Unity Performance Testing JSON. **Alloc B/op** = median bytes/op (best available counter — see Bench.ByteSource). **Alloc/op** = median count of GC.Alloc Profiler-marker events per op (works in EditMode regardless of byte-counter availability).

| Test (method) | Time (ns/op) | Alloc B/op | Alloc/op | Gen0 |
|---------------|--------------|------------|----------|------|
| BulkAdd_AfterFiveIndexers_Count_10 | 16145 | 0 | 69.01 | 0 |
| BulkAdd_AfterFiveIndexers_Count_100 | 93507 | 0 | 280.02 | 0 |
| BulkAdd_AfterFiveIndexers_Count_1000 | 796420 | 0 | 2111.1 | 0 |
| Indexer_Values_PerRead_Count10 | 20.37 | 0 | 0.0001 | 0 |
| Indexer_Values_PerRead_Count100 | 20.34 | 0 | 0.0001 | 0 |
| Indexer_Values_PerRead_Count1000 | 20.67 | 0 | 0.0001 | 0 |
| GetPrimaryKeys_1000Entries_10000Calls | 76734.67 | 0 | 20.0001 | 3 |
| GetSecondaryKeys_1000Entries_10000Calls | 74768.34 | 0 | 20.0001 | 3 |
| Dict_Add_FromEmpty_Count10 | 4603.7 | 0 | 29.002 | 0 |
| Dict_Add_FromEmpty_Count100 | 38676.5 | 0 | 218.01 | 0 |
| Dict_Add_FromEmpty_Count1000 | 360287.5 | 0 | 2027.05 | 0 |
| Dict_Foreach_Count1000 | 34787.6 | 0 | 0.002 | 0 |
| Dict_TryGetValue_Hit_Count1000 | 67.47 | 0 | 0.0001 | 0 |
| Map_Add_FromEmpty_Count10 | 5458.4 | 0 | 32.002 | 0 |
| Map_Add_FromEmpty_Count100 | 43862 | 0 | 221.01 | 0 |
| Map_Add_FromEmpty_Count1000 | 404915 | 0 | 2030.05 | 0 |
| Map_Foreach_Count1000 | 62881.8 | 0 | 1.002 | 0 |
| Map_TryGetValue_Hit_Count1000 | 66.56 | 0 | 0.0001 | 0 |

## Map vs Dictionary (same scenario)

| Scenario | Map Time | Map B/op | Map Allocs | Dict Time | Dict B/op | Dict Allocs |
|----------|----------|----------|------------|-----------|-----------|-------------|
| Add_FromEmpty_Count10 | 5458.4 | 0 | 32.002 | 4603.7 | 0 | 29.002 |
| Add_FromEmpty_Count100 | 43862 | 0 | 221.01 | 38676.5 | 0 | 218.01 |
| Add_FromEmpty_Count1000 | 404915 | 0 | 2030.05 | 360287.5 | 0 | 2027.05 |
| Foreach_Count1000 | 62881.8 | 0 | 1.002 | 34787.6 | 0 | 0.002 |
| TryGetValue_Hit_Count1000 | 66.56 | 0 | 0.0001 | 67.47 | 0 | 0.0001 |
