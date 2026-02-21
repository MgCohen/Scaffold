# Magic: The Gathering Arena (MTG Arena) Architecture Summary

## Architecture Summary: GRE & Unity Client

MTG Arena uses a **Client-Server architecture** that strictly separates the core game logic from the visual presentation. This ensures that the rules are authoritative and tamper-proof while allowing the client to focus purely on rendering the experience. 

### 1. The Game Rules Engine (GRE)
The authoritative brain of MTG Arena is the **Game Rules Engine (GRE)**, which runs on the server.
*   **Technology:** It is written in a combination of C++ and **CLIPS** (a rules-based programming language that is a variant of LISP). 
*   **Functionality:** The GRE tracks the actual game state, handles priority, applies state-based actions, and determines the flow of the game. It does not handle anything visual or audio-related.
*   **Card Parsing:** The GRE does not have every single card hard-coded into it. Instead, MTGA uses a **Game Rules Parser (GRP)** written in Python. This parser reads the raw English text of Magic cards and translates them into logical CLIPS rules that the GRE can execute.

### 2. The Unity Client (Presentation Layer)
The game client installed on the player's device is built using the **Unity Engine**.
*   **Role:** The Unity client is essentially a "dumb" presentation layer. It does not dictate game logic or verify rules. Its sole job is to process player inputs to send to the server, and to render the visual state and animations based on server instructions.
*   **Visual Effects:** MTGA utilizes detailed 3D modeling for cards (they aren't just flat images, but have bumps and curves that catch light), shaders, and particle systems to create the VFX for spells and triggers. 

### 3. How Synchronization Works (The Event-Driven Model)
The synchronization between the logical rules and the visual effects is handled through an **event-driven messaging system**:
*   **State Changes as Events:** When a card is played or an ability resolves, the GRE calculates the outcome and sends a structured message (an event or signal) to the Unity client detailing *what* just happened (e.g., "Card X deals 3 damage to Player Y," or "Card Z moves from Hand to Battlefield").
*   **Client Interpretation (The Action Queue):** The Unity client receives a stream of these events from the server and places them in an action queue. 
*   **Triggering VFX:** The client parses these messages and maps them to specific visual behaviors. For example, if the message denotes an "Enter The Battlefield" (ETB) event for a specific Mythic card, the client visually spawns the card and triggers its associated 3D animation and sound effect.
*   **Pacing and Timings:** Because Magic can have massive stacks of simultaneous triggers, the client manages the *timing* of these rules resolving visually so the player can understand them. This is why you sometimes see a slight delay as the client "plays out" a complex chain of logical events that the GRE calculated in milliseconds.

---

## Sources & References

1. **Building a Rules Engine for Magic: The Gathering (Wizards of the Coast Blog)**  
   *Details how the Game Rules Engine operates, using C++ and CLIPS, and how the Python-based GRP parses card text.*  
   [Read Article](https://magic.wizards.com/en/news/mtg-arena/building-rules-engine-magic-gathering-arena-2018-02-14)
2. **MTG Arena Client/Backend Communication (Reddit Discussions)**  
   *Community insights from reverse engineering and developer interactions regarding how the Unity client receives game state updates from the server infrastructure (hosted on Azure).*  
   [Discussion Thread 1](https://www.reddit.com/r/MagicArena/comments/eukrnm/does_mtg_arena_run_on_a_rules_engine/) | [Discussion Thread 2](https://www.reddit.com/r/MagicArena/comments/81z33v/how_much_of_mtga_is_hardcoded/)
3. **Card Rendering and Visual Effects in Unity (GDC / Developer Insights)**  
   *Developers have shared how cards are not 2D planes but full 3D models with light-reactive shaders, and how VFX artists sync animations to the event queues.*  
   [Developer Video - Card Rendering](https://www.youtube.com/watch?v=R9Z-YjB-hEU)
4. **Hacker News (YCombinator) Discussion on MTGA Architecture**  
   *Technical discussions on the choice of using CLIPS and LISP for managing the immense complexity of MTG's ruleset.*  
   [Hacker News Thread](https://news.ycombinator.com/item?id=20815412)
