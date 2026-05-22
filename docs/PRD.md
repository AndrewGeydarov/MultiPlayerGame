# PRD: Party Mini-Games — Local Multiplayer Collection

## Problem Statement

Two players sitting at the same computer have no quick, fun, pick-up-and-play game to compete against each other. Existing local multiplayer games often require complex setup, controllers, or long play sessions. There is no lightweight party game that offers short, self-contained mini-games playable with just a keyboard and mouse on a single screen.

## Solution

A desktop party game built in Unity 6 (URP 2D) with a cartoon art style, featuring three standalone mini-games selectable from a main menu. Players enter their names, choose a mini-game, play a round, see results, and return to the menu. Each round lasts under two minutes, making it ideal for quick competitive fun.

The three mini-games are:
1. **Crocodile** — a tension-based luck game where players take turns pressing teeth on a crocodile's mouth, trying to avoid the one bad tooth that triggers a bite.
2. **Tower Builder** — a simultaneous dexterity game where each player drops oscillating blocks onto their own tower, with overhangs cut off, competing for the tallest tower.
3. **Memory** — a classic card-matching game where players take turns flipping pairs, competing to collect the most matches.

## User Stories

1. As a player, I want to enter my name before playing, so that the game feels personalized.
2. As a player, I want to be assigned a distinct color, so that I can always tell which player I am at a glance.
3. As a player, I want to select a mini-game from a main menu, so that I can play whichever game I'm in the mood for.
4. As a player, I want to see a clear "whose turn" indicator in turn-based games, so that I know when to act.
5. As a player, I want the turn to switch automatically with a brief visual indicator, so that play stays fast and uninterrupted.
6. As a player, I want to see a results screen after each round, so that I know who won.
7. As a player, I want to return to the main menu after seeing results, so that I can pick another game or replay.
8. As a player, I want to hear background music during each mini-game, so that the game feels lively and engaging.
9. As a player, I want to hear sound effects for key actions (clicks, matches, bites, block drops), so that I get satisfying feedback.
10. As a Crocodile player, I want to see a top-down view of a crocodile mouth with 8 teeth in a semicircle, so that the game is visually clear.
11. As a Crocodile player, I want to click on teeth with the mouse during my turn, so that interaction is intuitive.
12. As a Crocodile player, I want already-pressed teeth to look visually different (pushed down), so that I know which teeth remain.
13. As a Crocodile player, I want one random tooth to be the bad tooth each round, so that every round is unpredictable.
14. As a Crocodile player, I want to instantly lose if I press the bad tooth, so that the stakes are clear and the round ends decisively.
15. As a Crocodile player, I want to see a bite animation when the bad tooth is pressed, so that the loss moment is dramatic and fun.
16. As a Tower Builder player, I want to see the screen split left and right with my tower on my side, so that I can focus on my own gameplay.
17. As a Tower Builder player, I want to see a block oscillating horizontally above my tower, so that I can time my drop.
18. As a Tower Builder player (P1), I want to press Space to drop my block, so that controls are simple.
19. As a Tower Builder player (P2), I want to press Enter to drop my block, so that both players can act simultaneously without conflict.
20. As a Tower Builder player, I want overhanging block portions to be visually cut off, so that I can see my landing surface shrinking.
21. As a Tower Builder player, I want the round to end instantly when either player completely misses, so that rounds stay competitive.
22. As a Tower Builder player, I want tower heights compared at the end to determine the winner, so that the objective is clear.
23. As a Memory player, I want to see a 4×3 grid of face-down cards, so that the playing field is clear.
24. As a Memory player, I want to click two cards per turn to flip them, so that interaction follows classic memory rules.
25. As a Memory player, I want matched cards to stay face-up and be visually claimed by me (in my color), so that progress is visible.
26. As a Memory player, I want unmatched cards to flip back face-down after a brief reveal delay, so that memorization matters.
27. As a Memory player, I want to take another turn immediately if I find a match, so that skill is rewarded.
28. As a Memory player, I want the player with the most pairs to win, so that the objective is straightforward.
29. As a Memory player, I want to see a "Draw!" result if both players have 3 pairs, so that ties are handled gracefully.

## Implementation Decisions

### Modules

1. **GameManager** — A singleton state machine managing the top-level application flow: MainMenu → NameEntry → MiniGamePlay → Results → MainMenu. Owns persistent player data (names, assigned colors) for the duration of the application session. Each mini-game scene reports its result back to this manager.

2. **CrocodileGame** — Manages round state for the Crocodile mini-game. Responsibilities: generating 8 tooth objects in a semicircle layout, randomly selecting the bad tooth at round start, tracking which teeth have been pressed, detecting the bite condition, handling turn switching between players.

3. **TowerGame** — Manages round state for the Tower Builder mini-game. Responsibilities: running two independent tower instances side-by-side, each with its own oscillating block, handling block drop on player input (Space / Enter), calculating overlap and cutting overhangs, detecting the "complete miss" end condition for either player, comparing tower heights for winner determination.

4. **MemoryGame** — Manages round state for the Memory mini-game. Responsibilities: generating a shuffled 4×3 grid of 6 icon pairs, handling card flip on click, match detection (two flipped cards with the same icon), turn management (match = go again, no match = switch), tracking pairs collected per player, determining winner by pair count.

5. **UIManager** — Handles all UI screens and HUD overlays. Includes: main menu with mini-game selection buttons, name entry form for two players, in-game HUD (current turn indicator for turn-based games, score/height display), and the results screen with winner announcement.

6. **AudioManager** — A singleton that persists across scenes. Manages background music playback (one track per mini-game, one for the menu) with crossfading between scenes. Exposes a simple API for playing one-shot sound effects (tooth click, bite, block drop, block cut, card flip, card match, win fanfare, lose sound).

### Scene Structure

- **MainMenu** scene — menu UI + name entry
- **Crocodile** scene — crocodile mini-game
- **TowerBuilder** scene — tower builder mini-game
- **Memory** scene — card matching mini-game

GameManager and AudioManager persist across scenes using `DontDestroyOnLoad`.

### Input

- Turn-based games (Crocodile, Memory): mouse click, shared by both players
- Simultaneous game (Tower Builder): Player 1 uses Space, Player 2 uses Enter
- Uses Unity's Input System package (already included in the project)

### Visual Style

- Cartoon / playful aesthetic: bright colors, rounded shapes, simple but fun 2D art
- URP 2D pipeline (already configured in the project)

### Tower Builder Block Mechanics

- Block oscillates left-right at constant speed above the current tower top
- On drop, overlap with the block below is calculated
- The non-overlapping portion (overhang) is visually sliced off
- The remaining portion becomes the new top block, and the next block's width matches this smaller surface
- Zero overlap = complete miss = round ends for both players

### Card Matching Details

- 6 pairs of icons, 12 cards total, arranged in a 4×3 grid
- Cards are shuffled randomly each round
- Flip animation: basic card flip (front/back swap)
- Unmatched cards remain visible for ~1 second before flipping back
- Matched cards stay face-up, tinted with the matching player's color

## Testing Decisions

No automated tests for the initial version. Focus is on getting all three mini-games playable and polished.

## Out of Scope

- **Networking / online multiplayer** — this is strictly local, shared-screen
- **Mobile or WebGL builds** — desktop only for now
- **Difficulty settings** — all games have a single fixed difficulty
- **Session mode / cross-game scoring** — games are independent, no cumulative score
- **Controller / gamepad support** — keyboard and mouse only
- **Player profiles or persistence** — names are entered per session, nothing is saved
- **Leaderboards or statistics** — no tracking across sessions
- **Additional mini-games** — only the three described above

## Further Notes

- The project already has the URP 2D pipeline, Input System, and Unity MCP package configured. No additional Unity packages should be needed for the core gameplay.
- Audio assets (music and SFX) will need to be sourced or created. Placeholder sounds can be used during development.
- Art assets for the crocodile, teeth, blocks, cards, and icons will need to be created in the cartoon style. Simple geometric sprites can serve as initial placeholders.
- The crocodile's 8 teeth should be arranged in a semicircular arc, viewed from above (top-down). Pressed teeth should appear visually "pushed down" or darkened.
- Tower Builder's split-screen can be achieved with two cameras or two viewport regions side by side within a single camera using UI layout.
