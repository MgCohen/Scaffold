---
name: worktree-branch-agent
description: Uses Git worktrees so the Cursor agent runs in a separate branch. Use when the user wants to run the agent on another branch, use worktrees with Cursor, or work on multiple branches at once.
---

# Run Cursor Agent in a Separate Branch (Git Worktree)

Use a Git worktree when the user wants the Cursor agent to operate on a different branch without switching the main repo. The agent always works in the **open workspace**; a worktree is a second working directory with its own branch.

## Workflow

1. **Create a worktree** (from repo root):
   ```bash
   git worktree add <path> <branch>
   ```
   Example: `git worktree add ../Scaffold-feature feature-branch`  
   Use an existing branch name, or `-b new-branch` to create and check out a new branch.

2. **Open the worktree in Cursor**
   - File → Open Folder → select the worktree path (e.g. `../Scaffold-feature` or its absolute path).
   - Or open a new Cursor window and open that folder. The agent in that window then runs in that branch.

3. **Use the agent** in that window; all edits and commands apply to that worktree's branch.

## Useful commands

| Goal | Command |
|------|--------|
| List worktrees | `git worktree list` |
| Remove a worktree | `git worktree remove <path>` (ensure branch is merged or discarded first) |
| Create worktree with new branch | `git worktree add -b new-branch ../path main` |

## Summary

- One Cursor window = one workspace = one branch (or one worktree's branch).
- To run the agent "in" another branch: create a worktree for that branch, then open that worktree's folder in Cursor (or a second window).
