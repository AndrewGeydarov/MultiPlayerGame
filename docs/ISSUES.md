# Issues — Party Mini-Games

Vertical-slice breakdown of [PRD](./PRD.md). Each issue is a thin end-to-end tracer bullet.

Slices 2–5 are independent of each other and can be worked in parallel once #1 is done.

---

## Issue 1: Core Loop Shell

**Type:** AFK

### What to build

GameManager singleton with a top-level state machine driving the application flow: MainMenu → NameEntry → MiniGamePlay → Results → MainMenu. UIManager handling all UI screens: main menu with three Mini-Game selection buttons, name entry form for two Players (captures names, assigns distinct colors), in-game HUD placeholder, and a results screen with winner announcement and "back to menu" button.

Create four scenes (MainMenu, Crocodile, TowerBuilder, Memory). GameManager persists across scenes via `DontDestroyOnLoad` and owns Player data (names, colors) for the session. Wire scene loading so that selecting a Mini-Game loads its scene, and the results screen returns to MainMenu.

Include a stub Mini-Game scene that immediately reports a winner to GameManager, proving the full loop works end-to-end before any real game logic exists.

### Acceptance criteria

- [ ] Two Players can enter their names and are each assigned a distinct color
- [ ] Main menu displays three Mini-Game buttons (Crocodile, Memory, Tower Builder)
- [ ] Selecting a Mini-Game loads the corresponding scene
- [ ] GameManager persists across scene transitions and retains Player data
- [ ] A stub Mini-Game scene reports a result and transitions to the results screen
- [ ] Results screen shows winner name/color and a "Back to Menu" button
- [ ] "Back to Menu" returns to the main menu with Player data intact for replay
- [ ] All four scenes exist in the build settings

### Blocked by

None — can start immediately.

---

## Issue 2: Crocodile Mini-Game

**Type:** AFK

### What to build

Complete Crocodile Round playable through the core loop. CrocodileGame module managing round state: 8 Teeth arranged in a semicircular arc (top-down view, placeholder sprites), one random Bad Tooth selected at round start, mouse-click interaction to press a Tooth during the active Player's Turn.

Pressed Teeth appear visually distinct (pushed down / darkened). Turns alternate automatically between Players with a brief "whose Turn" indicator. Pressing the Bad Tooth triggers a bite animation, the pressing Player loses, and the Round ends. The result is reported to GameManager, which transitions to the results screen.

If all safe Teeth are pressed without hitting the Bad Tooth, the last Player to act wins.

### Acceptance criteria

- [ ] Crocodile scene loads from main menu and displays 8 Teeth in a semicircle
- [ ] One random Tooth is designated as the Bad Tooth each Round (not visually revealed)
- [ ] Clicking a Tooth during your Turn presses it; pressed Teeth look visually distinct
- [ ] Turn switches automatically after each Tooth press with a visible indicator
- [ ] Pressing the Bad Tooth triggers a bite animation and ends the Round
- [ ] Player who pressed the Bad Tooth loses; result reported to GameManager
- [ ] Results screen displays correctly and returns to menu

### Blocked by

- Issue 1 (Core Loop Shell)

---

## Issue 3: Memory Mini-Game

**Type:** AFK

### What to build

Complete Memory Round playable through the core loop. MemoryGame module managing round state: a shuffled 4×3 grid of 6 Pairs (12 Cards total, placeholder icon sprites). Cards start face-down. On a Player's Turn, they click two Cards to flip them, revealing icons.

If the two flipped Cards form a Pair (same icon), they stay face-up tinted with the matching Player's color, and that Player takes another Turn immediately. If they don't match, both Cards flip back face-down after ~1 second, and the Turn passes to the other Player.

The Round ends when all 6 Pairs are found. The Player with the most Pairs wins. If both Players have 3 Pairs, the result is "Draw!" The result is reported to GameManager.

### Acceptance criteria

- [ ] Memory scene loads from main menu and displays a 4×3 grid of face-down Cards
- [ ] Cards are shuffled randomly each Round
- [ ] Clicking a Card flips it to reveal its icon (flip animation)
- [ ] After two Cards are flipped: matching Pair stays face-up tinted with Player color
- [ ] After two Cards are flipped: non-matching Cards flip back after ~1s delay
- [ ] Finding a Pair grants the Player another Turn; no match switches Turn
- [ ] Turn indicator shows whose Turn it is
- [ ] Round ends when all Pairs found; winner is Player with more Pairs
- [ ] 3-3 tie displays "Draw!" on results screen
- [ ] Result reported to GameManager; results screen and menu return work

### Blocked by

- Issue 1 (Core Loop Shell)

---

## Issue 4: Tower Builder Mini-Game

**Type:** AFK

### What to build

Complete Tower Builder Round playable through the core loop. TowerGame module managing round state for two simultaneous Players. The screen splits left/right, each side showing one Player's Tower.

Each Player has a Block oscillating horizontally above their Tower top at constant speed. Player 1 drops with Space, Player 2 drops with Enter — both act simultaneously, no turn-taking. On drop, overlap with the Block below is calculated; the Overhang is visually sliced off. The remaining portion becomes the new top Block, and the next Block's width matches this smaller landing surface.

Zero overlap (complete miss) by either Player ends the Round instantly for both. When the Round ends (miss or fixed number of drops), Tower heights are compared to determine the winner. Result is reported to GameManager.

### Acceptance criteria

- [ ] Tower Builder scene loads from main menu with a left/right split view
- [ ] Each Player sees their own Tower and oscillating Block
- [ ] Player 1 drops with Space; Player 2 drops with Enter (simultaneous play)
- [ ] Dropped Block's Overhang is calculated and visually cut off
- [ ] Next Block width matches the remaining landing surface
- [ ] Complete miss (zero overlap) ends the Round instantly for both Players
- [ ] Tower heights compared at Round end; taller Tower wins
- [ ] Result reported to GameManager; results screen and menu return work

### Blocked by

- Issue 1 (Core Loop Shell)

---

## Issue 5: Audio System

**Type:** AFK

### What to build

AudioManager singleton persisting across scenes via `DontDestroyOnLoad`. Manages background music playback with one track per scene (menu, Crocodile, Tower Builder, Memory) and crossfades between them on scene transitions.

Exposes a public API for playing one-shot sound effects: Tooth click, bite, Block drop, Block cut, Card flip, Card match, win fanfare, lose sound. Placeholder audio assets (simple generated tones/beeps) used initially. SFX calls are wired into Mini-Game scripts where those scripts already exist; where they don't yet, the API is ready for future integration.

### Acceptance criteria

- [ ] AudioManager singleton persists across all scene transitions
- [ ] Background music plays on each scene with a distinct track
- [ ] Music crossfades smoothly when transitioning between scenes
- [ ] Public `PlaySFX` API exists and can play one-shot sound effects by name/enum
- [ ] Placeholder audio assets included for all music tracks and SFX
- [ ] SFX calls wired into any already-implemented Mini-Game scripts
- [ ] No duplicate AudioManager instances when returning to MainMenu

### Blocked by

- Issue 1 (Core Loop Shell)
