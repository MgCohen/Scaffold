# Card Game Architecture Research

## Magic: The Gathering Arena (MTG Arena) Architecture

MTG Arena uses a **Client-Server architecture** that strictly separates the core game logic from the visual presentation. This ensures that the rules are authoritative and tamper-proof while allowing the client to focus purely on rendering the experience. 

### 1. The Game Rules Engine (GRE)
The authoritative brain of MTG Arena is the **Game Rules Engine (GRE)**, which runs on the server.
- **Technology:** It is primarily written in **C++** and heavily utilizes **CLIPS** (C Language Integrated Production System), which is an AI forward-chaining rules engine variant of LISP. 
- **Functionality:** The GRE understands the *fundamental, core rules* of Magic—such as turn phases, the stack, priority, combat damage, and state-based actions. It tracks the actual game state, handles priority, applies state-based actions, and determines the flow of the game. It does not handle anything visual or audio-related.
- **Rules evaluation:** The GRE does *not* know what every single card in MTG does natively. Instead, it relies on a dynamic set of "facts" and "rules" injected into the CLIPS system.

### 2. The Game Rules Parser (GRP)
Because new cards are constantly released with novel wording, it is unscalable to program cards one-by-one.
- **What it does:** The GRP (written in **Python**) takes the raw, plain English text written on MTG cards and uses natural language processing (NLP) to compile that text into functional **CLIPS logical rules**. 
- **The Result:** This automation translates standard MTG "Oracle text" into code snippets that the GRE can ingest. About **80% of new cards** work completely automatically in MTG Arena upon release without needing a human engineer. The remaining 20% involve entirely new mechanics or complicated wording requiring manual developer intervention.

### 3. The Unity Client & Synchronization
The game we actually play is built in **Unity (C#)**.
- **Role:** The Unity client is effectively a "dumb" terminal. It maintains no authority over the rules and handles zero game logic. It strictly manages UI, 3D animations, particle effects, shaders, sounds, and user inputs. 
- **Synchronization (The Action Queue):** When a player takes an action, the GRE calculates the outcome and sends a structured message (an event or signal) to the Unity client detailing what happened (e.g. "Card X deals 3 damage to Player Y"). 
- **Triggering VFX & Timing:** The client receives a stream of these events and places them in an action queue, interpreting them to trigger specific visual behaviors (like spawning 3D card animations). Because Magic can have massive stacks of simultaneous triggers, the client actively manages the *timing* of these rules resolving visually so players can understand them.

---

## Hearthstone Effect System & VFX Architecture

Hearthstone's architecture eschews hardcoded card logic in favor of a highly modular, data-driven **Entity-Component-System (ECS)** approach combined with a robust **Event-Driven Engine**.

### 1. Complete Server-Client Separation
Hearthstone strictly segregates game logic from presentation. The server is purely a headless rules engine that resolves the entire logical chain of events almost instantaneously. It then serializes this outcome as an ordered sequence of discrete state changes (often referred to as a `PowerTaskList` or Action Queue) and sends it to the Unity client.

### 2. Entities and GameTags
Everything in Hearthstone—a card, a player hero, a hero power, and even the "Game" itself—is an `Entity`. 
Instead of having distinct hardcoded variables, entities act essentially as key-value stores. The keys are a massive enum called `GameTag` (e.g., `ATK`, `HEALTH`, `ZONE`, `TAUNT`), and the values are integers. For instance, a minion moving from hand to board means updating its `GameTag.ZONE` from `HAND` to `PLAY`.

### 3. Data-Driven Card Logic
Cards are not individual C# classes. They are defined purely as data (JSON/XML/AST). When a card is loaded, the engine creates an Entity and assigns its base GameTags dictated by data. This allows designers to create thousands of cards without writing unique code.

### 4. The Event-Driven Action System
When something happens in the game, the sequence fires off an `Event` or `Action`. Entities that have triggered effects (like "Deathrattle") subscribe as listeners. When multiple effects trigger simultaneously, they are placed in a centralized queue and resolved based on their "Timestamp" or "Order of Play".

### 5. Client-Side Presentation & VFX (Queue-Based Rendering)
- **The Visual Bottleneck:** The Hearthstone Unity client receives the `PowerTaskList` from the server, but instead of applying state changes instantly, it places them into a rendering queue.
- **Client-Driven VFX:** The visual effects (VFX) are primarily driven client-side. The client reads the next event, plays the corresponding animation (e.g., a fireball flying), and only updates the local client's UI game state *after* the animation resolves.
- **Synchronization Challenge:** Since the server resolves instantly, the client relies entirely on sequentially queuing animations tight against pre-baked constraints. This design means third-party deck trackers can read game logs to know outcomes (like lethal damage) long before the client animations finish.

*(Note: If building a CCG in Unity, adopting the "GameTag/Entity" dictionary pattern early provides essential flexibility for complex future card rules!)*

---

## References & Sources

### Magic: The Gathering Arena
- **Wizards of the Coast Blogs:** "Building a Rules Engine for Magic: The Gathering Arena" (GRE C++/CLIPS structure, GRP Python parsing).
- **Hacker News (YCombinator):** Discussions on Magic's Turing Completeness, CLIPS, LISP, and rules engines.
- **Engineering Community (\/r/MagicArena):** Client/Backend Communication (Azure/Unity separation).
- **GDC / Developer Insights:** Card Rendering as 3D physical models, and integrating logic queues with Unity VFX.

### Hearthstone
- **HearthSim Documentation:** Extensively reverse-engineered Core Mechanics & GameTags.
- **The Liquid Fire Blog:** "Creating a CCG" architecture series with ECS & Event systems.
- **Hearthstone Wiki Engine:** Advanced Rulebook documenting Trigger Resolution and Timestamps.
- **Blizzard Developer Blog:** "The VFX of Hearthstone" details tightly slotting animations into baked rules engine timing.
- **GDC Talks:** 
  - *Hearthstone: How to Create an Immersive User Interface (2015)*
  - *Applying Artistic Principles to VFX Design in Hearthstone (2017)*
- **Community Tech Discussions (Reddit/StackExchange):** Trigger Resolution Order, Server/Client sync, and `PowerTaskList` architecture.
