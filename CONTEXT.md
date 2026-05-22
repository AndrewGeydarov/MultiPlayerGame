# Party Mini-Games

A local multiplayer party game for two players on a shared screen, featuring three standalone mini-games selectable from a main menu.

## Language

**Mini-Game**:
A self-contained game that two players can play from the main menu. Each mini-game has its own rules, win condition, and visual theme.
_Avoid_: level, stage, mode

**Round**:
A single play-through of a mini-game, from start to winner declaration.
_Avoid_: match, session, game (when referring to a single play)

**Player**:
A participant identified by a chosen name and an assigned color. There are always exactly two players.
_Avoid_: user, account

**Turn**:
A single opportunity for a player to act within a turn-based mini-game. In the Crocodile and Memory mini-games, turns alternate automatically between players.
_Avoid_: move, action

### Crocodile

**Tooth**:
A clickable element in the Crocodile mini-game, arranged in a semicircle inside the crocodile's mouth. There are exactly 8 teeth per round.
_Avoid_: button, tile

**Bad Tooth**:
The single randomly-selected tooth that triggers the crocodile's bite and causes the pressing player to lose the round.
_Avoid_: trap, bomb, danger tooth

### Tower Builder

**Block**:
A horizontally oscillating element that a player drops onto their tower by pressing their key. Once placed, overhanging portions are cut off.
_Avoid_: brick, piece, tile

**Tower**:
The stack of placed blocks belonging to one player. Its height determines the winner.
_Avoid_: stack, column

**Overhang**:
The portion of a placed block that extends beyond the block below it. The overhang is always cut off, making the next block's landing surface smaller.
_Avoid_: excess, spillover

### Memory

**Card**:
A face-down element in the Memory mini-game, hiding an icon on its face. Players flip cards to reveal their icons.
_Avoid_: tile, piece

**Pair**:
Two cards with the same icon. Finding both in a single turn constitutes a match.
_Avoid_: set, duo

## Relationships

- A **Round** is always played inside exactly one **Mini-Game**
- A **Round** always involves exactly two **Players**
- A **Turn** belongs to exactly one **Player** within a **Round**
- A **Tower** belongs to exactly one **Player** within a Tower Builder **Round**
- A **Pair** consists of exactly two **Cards**

## Example dialogue

> **Dev:** "When a **Player** presses a **Tooth**, does the **Turn** switch immediately?"
> **Domain expert:** "Yes — the **Turn** switches automatically after each **Tooth** press, with a brief visual indicator showing whose **Turn** it is now."
>
> **Dev:** "What ends a Tower Builder **Round**?"
> **Domain expert:** "When either **Player** completely misses — their **Block** has zero overlap — the **Round** ends instantly for both players."

## Flagged ambiguities

- "game" is overloaded — it can mean the entire application, a mini-game, or a single round. Resolved: use **Mini-Game** for the game type, **Round** for a single play-through, and avoid "game" alone.
