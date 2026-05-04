# Claude Code Telemetry → Honeycomb

This folder sets up per-tool, per-API-call cost and token tracking for Claude
Code, using its built-in OpenTelemetry support and Honeycomb as the backend.

You'll be able to answer questions like:

- "Which tool is burning the most tokens this week?"
- "How much did that one session cost, broken down by web search vs file edit
  vs bash?"
- "What's my cache hit rate, and how much money is it saving me?"
- "Which model (Opus / Sonnet / Haiku) is responsible for most of my spend?"

The built-in `/cost` command and the Anthropic dashboard only give you
session-level totals. This setup gives you per-operation granularity.

---

## One-time setup

### 1. Create a Honeycomb account (free)

1. Go to <https://www.honeycomb.io/> and click **Start for free**.
2. Sign up with email or GitHub. The free tier gives you 20M events per month,
   which is way more than a single developer will ever generate from Claude
   Code.
3. When asked to create an "Environment", call it `claude-code` (or whatever
   you like - it's just a namespace).

### 2. Get your ingest API key

1. In Honeycomb, click your account icon (top right) → **Environments and API
   Keys**.
2. Pick the environment you just created.
3. Copy the **Ingest Key**. It starts with `hcaik_` (or is a 32-character hex
   string for legacy keys).
4. Keep this tab open - you'll need the key in the next step.

### 3. Configure this folder

From the repo root:

```bash
cp Tools/Telemetry/.env.example Tools/Telemetry/.env
```

Open `Tools/Telemetry/.env` in your editor and paste your Honeycomb key into
`HONEYCOMB_API_KEY=`.

The `.env` file is gitignored - it won't be committed.

### 4. Run Claude Code with telemetry on

**macOS / Linux / WSL:**

```bash
./Tools/Telemetry/enable-telemetry.sh
```

**Windows (PowerShell):**

```powershell
pwsh -NoProfile -File .\Tools\Telemetry\enable-telemetry.ps1
```

You should see `telemetry: enabled (...)` followed by Claude Code starting up
normally. Use it for a few minutes - run some tool calls, ask it to read
files, do a web search.

### 5. Verify it's working

1. Go to <https://ui.honeycomb.io/> and pick your environment.
2. Click **Datasets** in the sidebar. You should see one called `claude-code`
   appear within ~10 seconds of your first tool call.
3. Click into it - you should see events flowing in with names like
   `claude_code.tool_result` and `claude_code.api_request`.

If nothing shows up after a couple of minutes, see **Troubleshooting** below.

---

## What gets sent to Honeycomb

### Metrics (numeric counters, aggregated over time)

- `claude_code.token.usage` - tokens used, broken down by:
  - `type`: input / output / cache_read / cache_creation
  - `model`: which Claude model
  - `query_source`: main / subagent / auxiliary
- `claude_code.cost.usage` - estimated USD cost, same breakdowns
- `claude_code.session.count`, `claude_code.lines_of_code.count`,
  `claude_code.commit.count`, `claude_code.pull_request.count`

### Events (one row per occurrence - this is where the per-operation analysis happens)

- `claude_code.tool_result` - one event per tool call, with attributes:
  - `tool_name` (Read, Edit, Bash, WebSearch, ...)
  - `tool_use_id`
  - `duration_ms`
  - `tool_input_size_bytes`, `tool_result_size_bytes`
  - success / error status
- `claude_code.api_request` - one event per API call to Anthropic, with:
  - `input_tokens`, `output_tokens`, `cache_read_tokens`, `cache_creation_tokens`
  - `cost_usd`
  - `model`
  - `duration_ms`
- `claude_code.user_prompt` - one event per user message (text only sent if
  `LOG_USER_PROMPTS=1`)

### What does NOT get sent

By default: tool inputs (bash command lines, file paths) and your prompt text
stay on your machine. To opt in, set `LOG_TOOL_DETAILS=1` or
`LOG_USER_PROMPTS=1` in `.env`. Only do this if you're comfortable with that
content being visible in your Honeycomb account.

---

## Doing the actual analysis

Open `queries.md` in this folder for copy-paste recipes. Most useful starters:

- **Cost by tool** - which tools are sucking up your budget
- **Top 10 most expensive API requests** - find the worst single offenders
- **Cache hit rate** - if low, you're paying full price on repeated context
- **Cost by model** - check whether a session is accidentally on Opus when it
  could have been on Haiku

---

## Troubleshooting

**Nothing in Honeycomb after running for a few minutes**

- Check your API key. Run the launcher again and confirm
  `telemetry: enabled` is printed.
- Try doing more in the session - metrics flush every 10s, but only after
  there's something to flush.
- Check the endpoint. EU accounts need `HONEYCOMB_ENDPOINT=https://api.eu1.honeycomb.io`.

**`claude: command not found` from the launcher**

The launcher runs `claude`. Make sure Claude Code is installed and on your
PATH (`which claude` / `Get-Command claude`).

**`.env` was committed by accident**

Run `git rm --cached Tools/Telemetry/.env`, then rotate the key in Honeycomb
(API keys → Disable old key → create new).

---

## Disabling telemetry

Just run `claude` directly instead of the launcher. The launcher only sets
env vars for the process it spawns, so nothing leaks into other sessions.
