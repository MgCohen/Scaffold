# Hearthstone Effect System Architecture Research

Based on research into how Hearthstone handles its effect system and game rules, here is a quick summary of its architecture and the core patterns it uses.

## Quick Architecture Summary

Hearthstone's architecture eschews hardcoded card logic in favor of a highly modular, data-driven **Entity-Component-System (ECS)** approach combined with a robust **Event-Driven Engine**.

**1. Entities and GameTags**
Everything in Hearthstone—a card, a player hero, a hero power, and even the "Game" itself—is an `Entity`. 
Instead of these entities having distinct hardcoded variables, they act essentially as key-value stores. The keys are a massive enum called `GameTag` (e.g., `ATK`, `HEALTH`, `ZONE`, `TAUNT`, `FROZEN`, `COST`), and the values are integers. 
For instance, a minion moving from a player's hand to the board simply means updating its `GameTag.ZONE` from `HAND` to `PLAY`.

**2. Data-Driven Card Logic**
Cards are not individual C# classes containing logic. Instead, they are defined purely as data (such as JSON, XML, or an Abstract Syntax Tree). When a card is loaded, the engine creates an Entity and assigns it the base GameTags dictated by its data definition. This means designers can create thousands of cards just by arranging data, without needing engineering to write unique code for every card.

**3. The Event-Driven Action System**
Abilities and effects are driven by an Event/Observer pattern. When something happens in the game (e.g., an attack is declared, a card is drawn, a minion takes damage), the engine fires off an `Event` or `Action`.
Entities that have triggered effects (like "Deathrattle" or "Whenever a minion takes damage") subscribe as listeners to these specific events. The engine then gathers all valid triggers, sorts them, and executes their effects.

**4. Trigger Resolution & Ordering**
When multiple effects trigger simultaneously (e.g., a board wipe kills several Deathrattle minions at once), Hearthstone resolves them based on their "Timestamp" or "Order of Play." The system uses a centralized queue where pending actions (and their nested sub-actions) are placed and resolved sequentially. 

**5. Complete Server-Client Separation**
Hearthstone enforces strict server validation. The server is purely a headless rules engine that processes the GameTags, resolves events, and determines the numeric outcomes. It then serializes this outcome as a log of "Tasks" or "PowerHistory" blocks and sends it to the Unity client. The client is basically just a visualizer that interprets the log, triggering animations and visual effects (VFX) in the correct sequence. The client never calculates damage or rules.

---

## References & Community Deep Dives

*   **HearthSim Architecture Documentation**: A community hub that built a functional Python simulator of Hearthstone based on reverse-engineering the game's network logs and tags.
    *   *Reference:* [Hearthsim Core Mechanics & GameTags](https://hearthsim.info/docs/mechanics/)
*   **The Liquid Fire - Creating a CCG**: A well-known game dev blog series that deeply analyzes how to architect a Hearthstone-like CCG in Unity, covering the ECS approach and the Action/Event system.
    *   *Reference:* [The Liquid Fire CCG Tutorial Series](http://theliquidfire.com/2017/08/21/make-a-ccg-actions/)
*   **Hearthstone Wiki Engine Details**: Explains the underlying timing rules, phases, and how simultaneous events and timestamps dictate action resolution based on community testing and developer clarifications.
    *   *Reference:* [Hearthstone Advanced Rulebook / Engine](https://hearthstone.wiki.gg/wiki/Advanced_rulebook)

If you're building a card game in Unity yourself, the most heavily recommended takeaway is to build the "GameTag/Entity" dictionary pattern early. It gives you incredible flexibility down the line when cards inevitably need to break the standard rules!
