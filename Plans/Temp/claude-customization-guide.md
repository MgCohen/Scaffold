# Claude Code Customization Guide

---

## 1. CLAUDE.md — Project Rules & Instructions

**The most important customization.** Gives Claude persistent instructions loaded every session.

**File locations (highest → lowest precedence):**

| Scope | Path | Shared? |
|-------|------|---------|
| Managed (org) | `/etc/claude-code/CLAUDE.md` (Linux) | Yes (IT-managed) |
| User | `~/.claude/CLAUDE.md` | No (all your projects) |
| Project | `./CLAUDE.md` or `./.claude/CLAUDE.md` | Yes (commit to git) |
| Local | `./CLAUDE.local.md` | No (auto-gitignored) |

**Example:**
```markdown
## Build Commands
- Build: `npm run build`
- Test: `npm run test`

## Code Standards
- Use TypeScript everywhere
- 2-space indentation
- All exports need JSDoc

## Architecture
- API handlers live in `src/api/handlers/`
```

**Pro tips:**
- Keep under 200 lines — longer files cost more context
- Use `.claude/rules/*.md` with frontmatter for path-specific rules:
  ```markdown
  ---
  paths:
    - "src/api/**/*.ts"
  ---
  All endpoints must validate input.
  ```
- Import other files: `See @README for overview`

---

## 2. Settings — `settings.json`

**File locations:**

| Scope | Path |
|-------|------|
| User | `~/.claude/settings.json` |
| Project | `.claude/settings.json` |
| Local | `.claude/settings.local.json` |

**Key options:**
```json
{
  "model": "claude-sonnet-4-6",
  "permissions": {
    "allow": ["Bash(npm run *)", "Bash(git *)", "Read"],
    "deny": ["Bash(curl *)", "Read(.env)"]
  },
  "autoMemoryEnabled": true,
  "respectGitignore": true,
  "env": {
    "MY_VAR": "value"
  }
}
```

---

## 3. Hooks — Event-Driven Automation

Run shell commands, HTTP calls, or LLM prompts at lifecycle events.

**File locations:**
- `~/.claude/hooks.json` — all projects
- `.claude/hooks.json` — project-level (committed)
- `.claude/hooks.local.json` — local only

**Available events:** `on-session-start`, `on-session-end`, `on-file-edit`, `on-bash-start`, `on-bash-complete`, `on-commit-create`, `on-pr-create`

**Example:**
```json
{
  "hooks": {
    "on-file-edit": {
      "matcher": "**/*.ts",
      "handler": "shell",
      "command": "npx prettier --write $file"
    }
  }
}
```

---

## 4. Skills — Custom Slash Commands

Reusable prompts invoked with `/skill-name`.

**File locations:**
- `~/.claude/skills/<name>/SKILL.md` — personal (all projects)
- `.claude/skills/<name>/SKILL.md` — project-level

**SKILL.md format:**
```yaml
---
name: review-pr
description: Review a pull request for quality and security
disable-model-invocation: false
allowed-tools: Read, Grep
---

Review PR $ARGUMENTS:
1. Check for security vulnerabilities
2. Verify test coverage
3. Look for performance issues
4. Provide actionable feedback
```

**Key frontmatter:**
- `disable-model-invocation: true` — prevents Claude from auto-invoking (use for destructive commands like `/deploy`)
- `user-invocable: false` — only Claude can invoke (background knowledge)
- `context: fork` — runs in isolated subagent
- `agent: Explore` — which subagent type to use

**Inject dynamic context with `!`:**
```yaml
PR diff: !`gh pr diff`
Summarize the above...
```

---

## 5. Custom Agents — Specialized Subagents

Define specialized agents Claude can spawn for specific tasks.

**File locations:**
- `~/.claude/agents/<name>/agent.md`
- `.claude/agents/<name>/agent.md`

**Format:**
```markdown
---
description: Expert code reviewer for pull requests
tools:
  - Read
  - Grep
  - Bash
model: sonnet
maxTurns: 10
---

You are a senior code reviewer. Focus on:
1. Security vulnerabilities
2. Test coverage
3. Performance
```

---

## 6. Models — Selecting Which Claude to Use

**Options (in order of priority):**
```bash
# CLI flag
claude --model claude-opus-4-6

# Environment variable
export CLAUDE_CODE_MODEL=claude-sonnet-4-6

# settings.json
{ "model": "claude-sonnet-4-6" }

# In-session: Alt+P (Win/Linux) or Option+P (macOS)
```

**Model aliases:** `sonnet`, `opus`, `haiku` → resolve to latest version

---

## 7. Keybindings — Custom Keyboard Shortcuts

**File:** `~/.claude/keybindings.json`

**Defaults to know:**

| Key | Action |
|-----|--------|
| `Ctrl+G` | Open prompt in editor |
| `Ctrl+L` | Clear screen |
| `Shift+Enter` | Multiline input |
| `Alt+P` | Switch model |
| `Alt+T` | Toggle extended thinking |
| `Esc+Esc` | Rewind/summarize |

**Custom bindings:**
```json
{
  "bindings": [
    {
      "keys": "ctrl+shift+d",
      "command": "/debug",
      "modes": ["normal"]
    }
  ]
}
```

---

## 8. MCP Servers — External Tool Integration

Connect Claude to GitHub, databases, Slack, etc.

```bash
# Add a remote HTTP server
claude mcp add --transport http github https://api.githubcopilot.com/mcp/

# Add a local stdio server
claude mcp add --transport stdio mydb -- python /path/to/server.py

# List / remove
claude mcp list
claude mcp remove github
```

**Project-shared config** in `.mcp.json`:
```json
{
  "mcpServers": {
    "github": {
      "type": "http",
      "url": "https://api.githubcopilot.com/mcp/",
      "headers": { "Authorization": "Bearer ${GITHUB_TOKEN}" }
    }
  }
}
```

---

## 9. Memory — Auto-Learning

Claude writes its own notes that persist across sessions.

**Storage:** `~/.claude/projects/<project>/memory/MEMORY.md`
- First 200 lines auto-loaded each session
- Additional topic files loaded on demand

**Control:**
```bash
# Toggle in session
/memory

# Or in settings.json
{ "autoMemoryEnabled": false }
```

Edit `MEMORY.md` directly to clean up stale notes.

---

## 10. Key Environment Variables

| Variable | Purpose |
|----------|---------|
| `ANTHROPIC_API_KEY` | API authentication |
| `CLAUDE_CODE_MODEL` | Default model |
| `CLAUDE_CODE_DISABLE_AUTO_MEMORY=1` | Disable auto memory |
| `CLAUDE_CODE_ENABLE_TELEMETRY=0` | Disable telemetry |
| `MCP_TIMEOUT` | MCP server startup timeout (ms) |

---

## Quick File Reference

```
~/.claude/
├── settings.json          # User settings
├── CLAUDE.md              # User rules (all projects)
├── keybindings.json       # Keyboard shortcuts
├── hooks.json             # Global hooks
├── skills/<name>/SKILL.md
├── agents/<name>/agent.md
└── projects/<hash>/memory/MEMORY.md

.claude/                   # Project-level (commit to git)
├── settings.json
├── settings.local.json    # (gitignored)
├── CLAUDE.md
├── hooks.json
├── skills/
├── agents/
├── rules/
└── .mcp.json
```
