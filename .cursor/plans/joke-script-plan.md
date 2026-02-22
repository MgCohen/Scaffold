# "Tell Me a Joke" Script — Implementation Plan

## Overview

A simple script that displays a random joke when executed. Lightweight, standalone utility.

---

## Approach Options

### Option A: Local Jokes Collection (Recommended for Simplicity)
- Store jokes in a local data structure (array/list)
- Random selection at runtime
- No external dependencies

### Option B: External API
- Fetch jokes from a public jokes API (e.g., JokeAPI, icanhazdadjoke)
- Requires HTTP client and network handling
- More variety, but adds complexity and external dependency

---

## Implementation Steps (Option A — Local Collection)

### 1. Create the Script File
- Location: `Assets/Scripts/Utility/JokeScript.cs` (or standalone if preferred)
- Single class: `JokeScript`

### 2. Define the Jokes Data
- Create a static array of joke strings
- Include 10–20 jokes for variety
- Consider two formats:
  - One-liners: `"Why don't scientists trust atoms? Because they make up everything!"`
  - Setup/Punchline pairs: Store as a struct with `Setup` and `Punchline` fields

### 3. Implement Random Selection
- Use `UnityEngine.Random.Range()` or `System.Random` to pick an index
- Return the selected joke

### 4. Output the Joke
- For Unity console: `Debug.Log(joke)`
- For UI display: Expose a public method returning the joke string
- For CLI/Editor tool: Use `EditorUtility.DisplayDialog()` or custom window

### 5. Trigger Mechanism
- **MonoBehaviour**: Call in `Start()` or via button click
- **Editor Menu**: `[MenuItem("Tools/Tell Me a Joke")]` attribute
- **Standalone Method**: Static `GetRandomJoke()` callable from anywhere

---

## Implementation Steps (Option B — External API)

### 1. Choose an API
- [JokeAPI](https://jokeapi.dev/) — free, supports categories
- [icanhazdadjoke](https://icanhazdadjoke.com/api) — dad jokes, simple JSON

### 2. Create HTTP Request Handler
- Use `UnityWebRequest` for Unity compatibility
- Handle async/await or coroutine pattern

### 3. Parse JSON Response
- Use `JsonUtility` or Newtonsoft.Json
- Map response to a joke model class

### 4. Display Logic
- Same as Option A step 4

### 5. Error Handling
- Network timeout fallback (show a default joke)
- Parse error handling

---

## Suggested File Structure

```
Assets/Scripts/Utility/
├── JokeScript.cs        # Main script with GetRandomJoke()
└── JokeData.cs          # (Optional) Joke struct/model if using setup/punchline format
```

---

## Example Jokes to Include

1. "Why do programmers prefer dark mode? Because light attracts bugs."
2. "A SQL query walks into a bar, walks up to two tables and asks, 'Can I join you?'"
3. "Why do Java developers wear glasses? Because they can't C#."
4. "There are only 10 types of people in the world: those who understand binary and those who don't."
5. "A programmer's wife tells him: 'Go to the store and buy a loaf of bread. If they have eggs, buy a dozen.' He comes home with 12 loaves of bread."

---

## Acceptance Criteria

- [ ] Script compiles without errors
- [ ] Running the script outputs a joke
- [ ] Multiple runs produce different jokes (randomness works)
- [ ] Follows project coding standards (no method comments, curly-bracket bodies, etc.)

---

## Estimated Complexity

- **Option A**: ~30 minutes, beginner-friendly
- **Option B**: ~1–2 hours, intermediate (networking, async)

---

## Recommendation

Start with **Option A** for a quick win. Can upgrade to Option B later if more variety is desired.
