# Performance run summary

Generated from Unity Performance Testing JSON. **Allocated** = median bytes/op reported by Bench.Measure (GC.GetAllocatedBytesForCurrentThread).

| Test (method) | Time median (ns/op) | Alloc median (B/op) | Gen0 median |
|---------------|---------------------|----------------------|-------------|
| BulkAdd_AfterFiveIndexers_Count_10 | 14491 | 0 | 0 |
| BulkAdd_AfterFiveIndexers_Count_100 | 157221 | 0 | 0 |
| BulkAdd_AfterFiveIndexers_Count_1000 | 6976685 | 0 | 0 |
| Indexer_Values_PerRead_Count10 | 437.62 | 0 | 0 |
| Indexer_Values_PerRead_Count100 | 3313.44 | 0 | 0 |
| Indexer_Values_PerRead_Count1000 | 31852.19 | 0 | 0 |
| GetPrimaryKeys_1000Entries_10000Calls | 111052.68 | 0 | 3 |
| GetSecondaryKeys_1000Entries_10000Calls | 84368 | 0 | 3 |
| Dict_Add_FromEmpty_Count10 | 3758.2 | 0 | 0 |
| Dict_Add_FromEmpty_Count100 | 30870 | 0 | 0 |
| Dict_Add_FromEmpty_Count1000 | 279890 | 0 | 0 |
| Dict_Foreach_Count1000 | 37615.4 | 0 | 0 |
| Dict_TryGetValue_Hit_Count1000 | 78.34 | 0 | 0 |
| Map_Add_FromEmpty_Count10 | 5740.3 | 0 | 0 |
| Map_Add_FromEmpty_Count100 | 50103.5 | 0 | 0 |
| Map_Add_FromEmpty_Count1000 | 468212.5 | 0 | 0 |
| Map_Foreach_Count1000 | 86079.4 | 0 | 0 |
| Map_TryGetValue_Hit_Count1000 | 159.2 | 0 | 0 |

## Map vs Dictionary (same scenario)

| Scenario | Map Time (ns/op) | Map Alloc (B/op) | Dict Time (ns/op) | Dict Alloc (B/op) |
|----------|------------------|-------------------|------------------|-------------------|
| Add_FromEmpty_Count10 | 5740.3 | 0 | 3758.2 | 0 |
| Add_FromEmpty_Count100 | 50103.5 | 0 | 30870 | 0 |
| Add_FromEmpty_Count1000 | 468212.5 | 0 | 279890 | 0 |
| Foreach_Count1000 | 86079.4 | 0 | 37615.4 | 0 |
| TryGetValue_Hit_Count1000 | 159.2 | 0 | 78.34 | 0 |
