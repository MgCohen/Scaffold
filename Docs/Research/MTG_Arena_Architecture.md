# MTG Arena Rules Engine Architecture
Magic The Gathering has notoriously complex rules and an immense catalog of unique card effects. To avoid hard-coding each individual card inside the game client, Wizards of the Coast separated the core game rules from the parsing of individual card texts using two main systems:

## 1. The Game Rules Engine (GRE)
The GRE is the authoritative server-side program that actually runs the game. It is primarily written in **C++** and heavily utilizes **CLIPS** (C Language Integrated Production System), which is an AI forward-chaining rules engine variant of LISP. 
- **What it does:** The GRE understands the *fundamental, core rules* of Magic—such as turn phases, the stack, priority, combat damage, and state-based actions (e.g., creatures dying from lethality). 
- **What it doesn't do:** The GRE does *not* know what every single card in MTG does natively. Instead, it relies on a dynamic set of "facts" and "rules" injected into the CLIPS system.

## 2. The Game Rules Parser (GRP)
Because new cards are constantly released with novel wording, it is completely unscalable to have developers program cards one-by-one. The studio built the Game Rules Parser, which is written in **Python**.
- **What it does:** The GRP takes the raw, plain English text written on MTG cards and uses natural language processing (NLP) to compile that text into functional **CLIPS logical rules**. 
- **The Result:** This automation translates standard MTG "Oracle text" into code snippets that the GRE can ingest. Thanks to this, about **80% of new cards** work completely automatically in MTG Arena upon release without needing a human engineer to code their specific effects. The remaining 20% involve entirely new mechanics or highly complex, unique wording that require manual developer intervention.

## 3. The Unity Client
The game we actually play (the visual interface) is built in **Unity (C#)**. The Unity client is effectively a "dumb" terminal. 
- It maintains no authority over the rules and handles zero game logic. 
- It strictly manages UI, 3D animations, particle effects, sounds, and user inputs. 
- The client sends player actions to the server (the GRE), which then validates the action using its CLIPS rules, updates the game state, and broadcasts the new state back to the Unity client to render.

## Summary
The magic of MTG Arena's scalability lies in using a **forward-chaining AI rules engine (CLIPS)** that maintains a list of global "facts" mapped to the game state. Rather than sequentially checking if lines of code execute, CLIPS evaluates facts. When a card is played, the English text is translated by **Python (GRP)** into logical CLIPS rules. The **C++ Server (GRE)** then applies these rules to the game state, allowing continuous card effects and triggers to happen correctly.

## References & Links
- **Wizards of the Coast Developer Articles:**
  - *MTG Arena Developer Updates* generally touch on the GRE infrastructure ("A Look Under the Hood" and release articles).
  - [Magic.wizards.com](https://magic.wizards.com/)
- **Hacker News (YCombinator) Discussions on Rules Engines:**
  - Tech discussions around MTG Arena's Python Parser and LISP/CLIPS backend explicitly cover how the natural language parsing hits an 80% success rate.
  - [HN Thread on Magic's Turing Completeness and Rules Engines](https://news.ycombinator.com/) (Search discussions around MTG Arena architecture and CLIPS).
- **Engineering Community Breakdowns:**
  - Discussions and compilations on Reddit's /r/MagicArena and game architecture design forums detail the C++/Python/Unity client-server split.
