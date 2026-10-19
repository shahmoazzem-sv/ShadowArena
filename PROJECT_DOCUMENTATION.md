# ShadowArena — Chess Game

**Engine:** Unity  
**Architecture:** MVC-like (Model → BoardManager, View → UI panels, Controller → GameManager + PlayerControllers)  
**Two Scenes:** `MainMenu1` (scene index 0) and `GameScene1` (scene index 1)

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Scenes](#2-scenes)
3. [Core Systems](#3-core-systems)
4. [Player Controllers](#4-player-controllers)
5. [Chess Pieces](#5-chess-pieces)
6. [Board & Move Logic](#6-board--move-logic)
7. [UI Panels](#7-ui-panels)
8. [Audio System](#8-audio-system)
9. [Data Flow](#9-data-flow)
10. [Inspector Wiring Guide](#10-inspector-wiring-guide)
11. [Known / Future Work](#11-known--future-work)

---

## 1. Architecture Overview

```
MainMenu1 ──(settings)──► GameManager ──► GameScene1
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
   BoardManager         PlayerManager         AudioManager
   (Model / Rules)      (Controller)          (Audio)
         │                    │
   Chess Pieces           IPlayerController
   (Pawn, Rook…)         HumanPlayerController
                          BotPlayerController
         │
   UI Panels (HistoryBoard, GameOverPanel…)
```

- **Singleton pattern** is used for `GameManager`, `BoardManager`, `InputManager`, `AudioManager`, `HistoryBoard`, `SettingsPanel`.
- **Events / Delegates** drive communication (e.g., `GameManager.OnTurnStarted`, `GameManager.OnMoveRecorded`, `BoardManager.OnKingInCheck`).
- **New Input System** (`ChessInput`) handles drag-and-drop piece interaction.
- **DOTween** powers all panel animations (scale + fade).

---

## 2. Scenes

### 2.1 `MainMenu1` (Build Index 0)

The entry scene. Contains:

| GameObject / Script | Purpose |
|---|---|
| `MainMenuManager` | Orchestrates menu navigation, game-mode selection, difficulty, and player colour. |
| `SettingsPanel` | Volume sliders (music + SFX). Singleton — persists across scenes. |
| `QuitButton` | Quit application / editor. |
| `HomeButton` | Returns to scene 0 (self-reload). |

**Flow:**
```
Open Play Setup → Choose Game Mode → (if HvB) choose Difficulty & Colour → Launch Game
```

### 2.2 `GameScene1` (Build Index 1)

The active chess game scene. Contains:

| GameObject / Script | Purpose |
|---|---|
| `GameManager` | Game state, history (FEN, PGN), turn management, scene navigation. |
| `BoardManager` | Board model, piece spawning, input handling, legal-move filtering, check/mate detection. |
| `PlayerManager` | Instantiates the correct `IPlayerController` pair (Human/Bot) based on `GameManager` settings. |
| `InputManager` | Unity Input System wrapper for drag-and-drop. |
| `AudioManager` | Music playlist + chess SFX. |
| `HistoryBoard` | Spawns `HistoryPanel` rows from `OnMoveRecorded`. |
| `GameOverPanel` | Shows winner + Play Again / Main Menu / Quit buttons. |
| `NotificationPanel` | Floating "King in check" / "Checkmated" banner. |
| `SettingsPanel` | Volume controls (singleton, persists). |
| `OrderY` | Per-piece `SpriteRenderer.sortingOrder` for correct 2D depth. |

---

## 3. Core Systems

### 3.1 `GameManager`

| Property / Method | Description |
|---|---|
| `CurrentState` | `GameState` enum: `Initializing` → `WhiteTurn` / `BlackTurn` → `GameOver` |
| `CheckedKing` | `PieceColor?` — which king is currently in check. |
| `FullMoveNumber` / `HalfMoveClock` | Standard FEN counters. |
| `CurrentFEN` / `PgnMoves` | FEN string and list of SAN move strings. |
| `CurrentGameMode` | `HumanVsHuman`, `HumanVsBot`, `BotVsBot`, `Networked` |
| `WhitePlayer` / `BlackPlayer` | `PlayerType.Human` or `PlayerType.Bot` |
| `BotDifficulty` | `Easy` (depth 2), `Medium` (depth 3), `Hard` (depth 4) |
| `HumanPlayerColor` | Which colour the human plays. |
| `IsBoardFlipped` | True when camera is rotated 180° (human plays Black). |
| `Pending*` statics | Cross-scene setup transfer from `MainMenuManager`. |
| `OnTurnStarted` | Fires when a new turn begins. |
| `OnMoveRecorded` | Fires after each valid move (move number, SAN, isWhite). |
| `OnKingChecked` / `OnKingCleared` | Check / un-check events. |
| `OnGameOver` | Fires with the losing king's colour. |
| `OnInvalidMoveInCheck` | Fires when a piece in check tries an illegal move. |
| `RecordMoveInfo()` | Updates FEN, PGN, half-move clock, full-move number. |
| `EndTurn()` | Swaps `WhiteTurn ↔ BlackTurn`, fires `OnTurnStarted`. |
| `TryMakeMove()` | Validates and delegates to `BoardManager.TryMovePiece`. |
| `RestartGame()` / `GoToMainMenu()` / `QuitGame()` | Scene / quit helpers. |

### 3.2 `BoardManager`

| Property / Method | Description |
|---|---|
| `gridSystem` | `GridDataSystem<BoardCell>` — 8×8 grid mapping world ↔ grid coordinates. |
| `LastMove` | `MoveRecord?` — last move for en-passant tracking. |
| `OnKingInCheck` | Event fired when a king is found in check. |
| `GetLegalMoves(piece)` | Returns legal moves by simulating each pseudo-legal move and checking `IsKingInCheck`. |
| `TryMovePiece(piece, to)` | Executes a move: captures, en-passant, castling, promotion, SFX, finalization. |
| `SimulateMove()` / `UndoSimulatedMove()` | Model-only move/undo for legal-move filtering and AI. |
| `IsSquareAttacked(square, byColor)` | Checks if a square is attacked by pawns, knights, kings, rooks/queens (orthogonal), bishops/queens (diagonal). |
| `IsKingInCheck(kingColor)` | Wrapper: finds king position → `IsSquareAttacked`. |
| `UpdateCheckStatus()` | Checks both kings, updates `GameManager.CheckedKing`, fires events. |
| `SetupBoard()` | Clears board, spawns pieces (default FEN layout or custom `ChessSetupAsset`). |
| `TryGetKingPosition()` | Finds king of given colour. |
| `HasAnyLegalMoveForColor()` | Used for checkmate / stalemate detection. |

**Board flip logic:** When `HumanPlayerColor == Black`, the camera rotates 180° on Z. All piece sprites are counter-rotated so they remain upright. `OrderY` uses `IsBoardFlipped` to invert its sort-order calculation.

### 3.3 `InputManager`

| Property / Event | Description |
|---|---|
| `ChessInput` | Unity Input Map asset (`DragAndDrop` action map). |
| `IsPressed` / `IsDragging` | State flags. |
| `ClickWorldPosition` / `DragWorldPosition` / `DropWorldPosition` | World-space positions. |
| `OnClick` | Fires once on touch/click start. |
| `OnDragStart` | Fires when drag begins. |
| `OnDragging` | Fires continuously while dragging. |
| `OnDrop` | Fires on release. |

`BoardManager` subscribes to all four events in `Start()`.

---

## 4. Player Controllers

### 4.1 `IPlayerController` (Interface)

```csharp
public interface IPlayerController
{
    void Initialize(PieceColor color);
    void OnTurnStarted();
    void OnTurnEnded();
}
```

### 4.2 `HumanPlayerController`

| Method | Behavior |
|---|---|
| `OnTurnStarted()` | Enables `InputManager` GameObject. |
| `OnTurnEnded()` | Disables `InputManager` GameObject. |

### 4.3 `BotPlayerController` (AI)

| Feature | Detail |
|---|---|
| **Algorithm** | Minimax with Alpha-Beta pruning. |
| **Depth** | Easy = 2, Medium = 3, Hard = 4. |
| **Move Ordering** | MVV-LVA (Most Valuable Victim / Least Valuable Attacker) + check-giving bonus. |
| **Evaluation** | Material balance + Piece-Square Tables (PST) + check bonus + endgame heuristics. |
| **PSTs** | Separate tables for Pawn, Knight, Bishop, Rook, Queen, King (midgame + endgame). |
| **Endgame Detection** | No queens on board → activates king-activity bonus, passed-pawn bonus, mop-up score. |
| **Opening Book** | `ChessOpeningBook.GetBookMove()` — if a PGN match exists, plays from book before falling back to minimax. |
| **Think Delay** | Configurable `ThinkDelay` (default 0.6 s) via coroutine. |

**Material Values:**

| Piece | Value |
|---|---|
| Pawn | 100 |
| Knight | 320 |
| Bishop | 330 |
| Rook | 500 |
| Queen | 900 |
| King | 20000 |

### 4.4 `PlayerManager`

| Responsibility | Detail |
|---|---|
| Creates controllers | Instantiates `HumanPlayerController` or `BotPlayerController` GameObjects for White and Black based on `GameManager` settings. |
| Turn dispatch | Subscribes to `GameManager.OnTurnStarted`, calls `OnTurnStarted()` on the active controller and `OnTurnEnded()` on the inactive one. |

---

## 5. Chess Pieces

All pieces inherit from `ChessPiece` (base class) and override `GetValidMoves(BoardManager)`.

### 5.1 `Pawn`

- **Forward 1** square (always).
- **Forward 2** squares (only on first move, path must be clear).
- **Diagonal captures** (one square forward-left/right).
- **En-passant** (if opponent just double-pushed adjacent).
- **Promotion** on reaching the last rank (auto-queen for bots, UI prompt for humans).

### 5.2 `Knight`

- **8 possible L-shaped offsets**.
- Jumps over pieces (no blocking).

### 5.3 `Bishop`

- **4 diagonal directions**, ray-casts until blocked.

### 5.4 `Rook`

- **4 orthogonal directions**, ray-casts until blocked.

### 5.5 `Queen`

- **8 directions** (orthogonal + diagonal), ray-casts until blocked.

### 5.6 `King`

- **8 adjacent squares**.
- **Castling** (kingside & queenside): checks `hasMoved`, empty intervening squares, and that the king doesn't pass through or land on an attacked square (via simulation).

---

## 6. Board & Move Logic

### 6.1 Move Execution Flow (`BoardManager.TryMovePiece`)

```
1. Validate legal move via GetLegalMoves()
2. Handle capture (destroy captured piece)
3. Handle en-passant capture
4. Move piece to new grid cell
5. Handle castling (move rook)
6. Handle promotion (auto-queen or UI prompt)
7. Play SFX (move / capture)
8. FinalizeMoveProcess():
   a. Check check/mate for next player
   b. Generate SAN notation
   c. Update FEN
   d. GameManager.RecordMoveInfo()
   e. GameManager.EndTurn()
   f. If checkmate → GameManager.ChangeState(GameOver)
   g. Clear selection
```

### 6.2 Legal Move Filtering

```
piece.GetValidMoves()  →  pseudo-legal moves (ignores check)
    ↓
BoardManager.GetLegalMoves()  →  for each pseudo move:
    1. SimulateMove()
    2. IsKingInCheck(pieceColor)
    3. UndoSimulatedMove()
    4. Keep only if NOT in check
```

### 6.3 Check / Checkmate / Stalemate Detection

| Condition | Logic |
|---|---|
| **Check** | `IsKingInCheck(color)` → king position found → `IsSquareAttacked(kingPos, opponent)` |
| **Checkmate** | `inCheck && !HasAnyLegalMoveForColor(opponent)` |
| **Stalemate** | `!inCheck && !HasAnyLegalMoveForColor(opponent)` (scored as 0 in minimax) |

---

## 7. UI Panels

### 7.1 `HistoryBoard`

- Singleton. Subscribes to `GameManager.OnMoveRecorded`.
- **White move** → instantiates new `HistoryPanel` row with move number.
- **Black move** → updates the current row's black-move text.

### 7.2 `HistoryPanel`

- Single row showing: `MoveNumber. WhiteMove  BlackMove` (TextMeshPro).

### 7.3 `GameOverPanel`

- Initially inactive. Shows on `GameManager.OnGameOver`.
- Animates in with DOTween (scale + fade).
- Buttons: **Play Again** (restarts with same settings), **Main Menu**, **Quit**.
- Freezes `Time.timeScale = 0` during display.

### 7.4 `NotificationPanel`

- Floating banner: "White/Black king is in check" / "White/Black is checkmated!"
- Pop-up with `Ease.OutBack`, auto-hide after `autoHideDelay` (unless checkmate).

### 7.5 `SettingsPanel`

- Singleton, `DontDestroyOnLoad`.
- Music & SFX sliders synced with `AudioManager`.
- Values saved to `PlayerPrefs`.
- Animates in/out with DOTween.

### 7.6 `HomeButton` / `QuitButton` / `SettingButton`

- Simple button wrappers: load scene 0, quit, open `SettingsPanel`.

---

## 8. Audio System

### 8.1 `AudioManager`

| Feature | Detail |
|---|---|
| **Singleton** | `DontDestroyOnLoad`. |
| **Music** | Random playlist looping. Stops on game over, resumes on menu return. |
| **SFX** | `PlayOneShot` on a separate source. |
| **Volume** | Saved/loaded via `PlayerPrefs` (`MusicVolume`, `SFXVolume`). |
| **AudioMixerGroups** | Separate `musicGroup` and `sfxGroup`. |

**Chess SFX Clips:**

| Clip | Trigger |
|---|---|
| `ClickNormal` | General click |
| `PieceClick` | Piece selection |
| `PieceMove` | Non-capture move |
| `PieceCapture` | Human capture |
| `PieceCapturedByAI` | Bot captures human piece |
| `KingCheck` | King put in check |
| `Checkmate` | Checkmate occurs |
| `GameOver` | Looping game-over music |

### 8.2 `OrderY`

- Per-piece script. Calculates `SpriteRenderer.sortingOrder` from world Y position.
- Inverts calculation when `IsBoardFlipped` is true.
- Prevents piece overlap visual bugs.

---

## 9. Data Flow

### 9.1 Game Start

```
MainMenu1 → MainMenuManager.OpenPlaySetup()
  → Select Game Mode (HvH or HvB)
  → (if HvB) Select Difficulty + Player Colour
  → MainMenuManager.LaunchHumanVsBot()
    → Sets GameManager.Pending* statics
    → SceneManager.LoadScene(1)  // GameScene1
  → GameManager.Awake()
    → Applies Pending* → sets CurrentGameMode, player types, difficulty
    → CurrentState = Initializing
  → BoardManager.Start()
    → Creates gridSystem, subscribes InputManager events
    → FlipBoardIfNeeded() (if human plays Black)
    → SetupBoard() → spawns pieces → ChangeState(WhiteTurn)
  → PlayerManager.Start()
    → Creates Human/Bot controllers
    → Fires OnTurnStarted for first turn
```

### 9.2 Human Move

```
InputManager.OnDragStart → BoardManager.TrySelectPieceAt()
  → Validates turn / piece ownership
  → Computes legal moves (simulation)
  → Shows valid-move indicators
  → Plays PieceClick SFX

OnDrop → BoardManager.TryMovePiece()
  → Execute move (capture, castling, promotion)
  → FinalizeMoveProcess()
    → Check detection → SAN → FEN
    → GameManager.RecordMoveInfo() → OnMoveRecorded
    → GameManager.EndTurn() → OnTurnStarted(opponent)
    → Checkmate? → OnGameOver → GameOverPanel.Show()
```

### 9.3 Bot Move

```
OnTurnStarted(BotColor) → BotPlayerController.OnTurnStarted()
  → Coroutine ThinkAndPlay()
    → Wait ThinkDelay
    → Try ChessOpeningBook → if match, play it
    → Else → FindBestMove(depth)
      → GatherAllMoves → OrderMoves (MVV-LVA + check)
      → Minimax(depth, alpha, beta, …)
        → Evaluate: material + PST + check + endgame
    → Execute best move via GameManager.TryMakeMove()
```

---

## 10. Inspector Wiring Guide

### 10.1 `MainMenuManager`

| Field | Assign |
|---|---|
| `playButton` | Play button in main menu |
| `settingsButton` | Settings button |
| `quitButton` | Quit button |
| `playSetupPanel` | Root of Play Setup panel (inactive by default) |
| `closeSetupButton` | Close button on Play Setup panel |
| `humanVsHumanButton` | Human vs Human mode button |
| `humanVsBotButton` | Human vs Bot mode button |
| `botSetupPanel` | Bot configuration sub-panel |
| `diffEasyButton` / `diffEasyIndicator` | Easy difficulty selector |
| `diffMediumButton` / `diffMediumIndicator` | Medium difficulty selector |
| `diffHardButton` / `diffHardIndicator` | Hard difficulty selector |
| `botWhiteButton` / `botWhiteIndicator` | Play as White (bot = Black) |
| `botBlackButton` / `botBlackIndicator` | Play as Black (bot = White) |
| `finalPlayButton` | Apply settings + load game scene |
| `gameSceneIndex` | Build index of `GameScene1` (usually 1) |

### 10.2 `BoardManager`

| Field | Assign |
|---|---|
| `boardSize` | 8 |
| `cellSize` | e.g., (1, 1) |
| `originPosition` | Top-left or bottom-left corner of board |
| `pieceData` | `PieceData` asset (sprite mapping for all piece types + colours) |
| `showMovePrefab` | Prefab for legal-move indicator dots |
| `pawnPrefab` / `rookPrefab` / `knightPrefab` / `bishopPrefab` / `queenPrefab` / `kingPrefab` | Piece prefabs |
| `promotionUI` | Promotion selection UI component |
| `customSetupAsset` | (Optional) `ChessSetupAsset` for custom board layouts |
| `useCustomSetup` | Toggle to use custom setup |

### 10.3 `GameManager`

| Field | Assign |
|---|---|
| `gameSceneIndex` | Build index of `GameScene1` (usually 1) |
| `mainMenuSceneIndex` | Build index of `MainMenu1` (usually 0) |

### 10.4 `AudioManager`

| Field | Assign |
|---|---|
| `musicGroup` | AudioMixerGroup for music |
| `sfxGroup` | AudioMixerGroup for SFX |
| `playlist` | List of background music `AudioClip`s |
| Chess SFX fields | Assign respective `AudioClip` assets |

### 10.5 `GameOverPanel`

| Field | Assign |
|---|---|
| `panelRoot` | RectTransform of the panel |
| `canvasGroup` | CanvasGroup on same object |
| `resultText` | TextMeshPro showing winner |
| `playAgainButton` | Restart button |
| `mainMenuButton` | Main Menu button |
| `quitButton` | Quit button |

### 10.6 `HistoryBoard`

| Field | Assign |
|---|---|
| `historyPanelPrefab` | Prefab for a single move row |
| `historyPanelParent` | Transform to parent new rows under |

### 10.7 `SettingsPanel`

| Field | Assign |
|---|---|
| `musicSlider` | Music volume slider |
| `sfxSlider` | SFX volume slider |
| `closeButton` | Close button |
| `panelRoot` | RectTransform for animation |
| `canvasGroup` | CanvasGroup for fade animation |

---

## 11. Known / Future Work

| Area | Status | Notes |
|---|---|---|
| **Draw detection** | ❌ Not implemented | 50-move rule, threefold repetition, insufficient material not yet handled. |
| **Full promotion UI** | ⚠️ Partial | Human players see a UI prompt; bots auto-queen. |
| **Network play** | ❌ Placeholder | `PlayerType.Network` exists in enums but no networking code. |
| **Undo / replay** | ❌ Not implemented | `UndoSimulatedMove()` exists but only for AI simulation. |
| **Opening book** | ⚠️ Basic | `ChessOpeningBook` class referenced but implementation details not in current scripts. |
| **Custom board setups** | ✅ Supported | `ChessSetupAsset` allows arbitrary initial positions. |
| **Stalemate handling** | ⚠️ Scored as 0 in AI | Game-over panel doesn't show stalemate result. |
| **Piece animations** | ⚠️ Direct position snap | No tween/lerp for piece movement (drag follows cursor). |

---

*Last updated: 2026-06-03*
