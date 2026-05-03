# Honeycomb Query Recipes

Copy-paste these into the Honeycomb query builder
(<https://ui.honeycomb.io/> → your environment → **New Query**).

Honeycomb's query builder has these slots:

- **VISUALIZE** - what number you want (count, sum, average, ...)
- **WHERE** - filter the rows that go in
- **GROUP BY** - split the result into categories
- **ORDER BY** - how to sort the resulting rows

Each recipe below maps to those slots.

---

## 1. Total spend over time

The first thing to look at - just confirms data is flowing and gives you a
sense of your daily cost.

- **Dataset:** `claude-code`
- **VISUALIZE:** `SUM(cost_usd)`
- **WHERE:** `name = claude_code.api_request`
- **Time range:** Last 7 days
- **Granularity:** 1 hour

---

## 2. Cost by tool (the big one)

Answers "which tool is burning the most money?" - but note: tools themselves
don't directly have a cost; the *API request that produced the tool call*
does. We approximate by joining on `tool_name` from tool events. The cleanest
proxy is **token usage attributed to tool result size**:

- **VISUALIZE:** `SUM(tool_result_size_bytes)`
- **WHERE:** `name = claude_code.tool_result`
- **GROUP BY:** `tool_name`
- **ORDER BY:** `SUM(tool_result_size_bytes) DESC`

A high-bytes tool (typically `Read`, `Bash`, `WebFetch`) means it's stuffing
a lot of content into the context window, which then has to be re-sent on
every subsequent API request until compaction.

For a true cost view, see #4.

---

## 3. Top 10 most expensive single API requests

Find the worst individual offenders.

- **VISUALIZE:** `MAX(cost_usd)`
- **WHERE:** `name = claude_code.api_request`
- **GROUP BY:** `trace.trace_id`, `model`
- **ORDER BY:** `MAX(cost_usd) DESC`
- **LIMIT:** 10

Click into one of the rows → **Trace** to see what was happening at the time
(which session, which preceding tool calls).

---

## 4. Cost broken down by model

Catches accidentally-on-Opus sessions.

- **VISUALIZE:** `SUM(cost_usd)`
- **WHERE:** `name = claude_code.api_request`
- **GROUP BY:** `model`
- **Time range:** Last 7 days

If you see a big Opus number when most of your work is routine, that's a
signal to be more deliberate about which model you start sessions on, or to
delegate to subagents (which can run on cheaper models).

---

## 5. Cache hit rate

Cache reads are ~10% the price of fresh input tokens. A low cache hit ratio
means you're paying full price on context you've already paid to ingest.

- **VISUALIZE:**
  - `SUM(cache_read_tokens)` (call this A)
  - `SUM(input_tokens) + SUM(cache_read_tokens) + SUM(cache_creation_tokens)` (call this B)
- Then in a calculated column: `A / B`
- **WHERE:** `name = claude_code.api_request`
- **Time range:** Last 7 days

Healthy projects sit around 70-90%. If yours is below 50%, your sessions are
probably too short, or you're rotating the front of the context too often
(common cause: aggressive rereads of large files).

---

## 6. Tokens by query source (main vs subagent)

Tells you how much of your budget is being spent in the main loop vs in
delegated subagents.

- **VISUALIZE:** `SUM(claude_code.token.usage)`
- **GROUP BY:** `query_source`
- **Time range:** Last 7 days

If subagent usage is near zero but you have lots of expensive Read/Grep
operations in the main loop, that's a hint to delegate exploration to the
Explore subagent.

---

## 7. Slow tool calls

Tools that take a long time often mean you're shelling out to something
expensive, or reading a giant file.

- **VISUALIZE:** `P95(duration_ms)`, `COUNT`
- **WHERE:** `name = claude_code.tool_result`
- **GROUP BY:** `tool_name`
- **ORDER BY:** `P95(duration_ms) DESC`

---

## 8. Sessions ranked by cost

Find your most expensive sessions, then click in to see what happened.

- **VISUALIZE:** `SUM(cost_usd)`
- **WHERE:** `name = claude_code.api_request`
- **GROUP BY:** `session.id`
- **ORDER BY:** `SUM(cost_usd) DESC`
- **LIMIT:** 20

---

## Building a "Token Usage" board

Once a query is useful, click **Save** at the top. To see them all together:

1. **Boards** in the sidebar → **New Board**, name it "Claude Code Spend".
2. From any saved query: **Add to Board** → pick this board.
3. Add queries 1, 2, 4, 5, 8 - that's a one-glance overview of where money
   goes.

Pin the board URL somewhere - that's now your dashboard.
