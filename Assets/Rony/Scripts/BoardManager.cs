using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

/// <summary>
/// BoardManager: holds board state, handles selection, legal move filtering (via simulation),
/// en-passant, promotion (auto-queen), castling basics, check & checkmate detection.
/// Note: piece.GetValidMoves() should be pseudo-legal (it does not consider leaving own king in check).
/// BoardManager.GetLegalMoves(...) filters those using simulation.
/// </summary>
public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance;
    public event Action<PieceColor> OnKingInCheck;

    // When the human plays Black the camera is flipped 180° so Black pieces
    // appear at the bottom of the screen. All sprites are counter-rotated so
    // they remain visually upright.
    private bool isBoardFlipped = false;

    private GridDataSystem<BoardCell> gridSystem;

    [Header("Custom Setup (Optional)")]
    [SerializeField] private ChessSetupAsset customSetupAsset = null;
    [SerializeField] private bool useCustomSetup = false; // toggle in inspector

    [Header("Grid Settings")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private Vector2 cellSize = new Vector2(1f, 1f);
    [SerializeField] Vector3 originPosition = Vector3.zero;

    [Header("Piece Setup")]
    [SerializeField] private PieceData pieceData;
    [SerializeField] private GameObject showMovePrefab;
    [SerializeField] private Pawn pawnPrefab;
    [SerializeField] private Rook rookPrefab;
    [SerializeField] private Knight knightPrefab;
    [SerializeField] private Bishop bishopPrefab;
    [SerializeField] private Queen queenPrefab;
    [SerializeField] private King kingPrefab;

    [Header("UI")]
    [SerializeField] private PromotionUI promotionUI;

    // Selection state
    private ChessPiece selectedPiece;
    private Vector2Int selectedPieceOriginalGrid;
    private BoardCell selectedPieceOriginalCell;
    private Vector3 selectedPieceOriginalWorldPos;
    private List<Vector2Int> selectedPieceValidMoves = new List<Vector2Int>();
    private List<GameObject> selectedPieceValidMoveShower = new List<GameObject>();

    // Last move record (used for en-passant)
    public struct MoveRecord
    {
        public ChessPiece piece;
        public Vector2Int from;
        public Vector2Int to;
        public ChessPiece captured; // could be null
        public bool wasDoublePawnPush;
        public bool wasEnPassant;
    }
    public MoveRecord? LastMove = null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        gridSystem = new GridDataSystem<BoardCell>(
            boardSize,
            boardSize,
            cellSize,
            originPosition,
            (GridDataSystem<BoardCell> g, int x, int y) => new BoardCell(g, x, y)
        );

        if (InputManager.Instance == null)
        {
            Debug.LogError("InputManager instance not found. Make sure InputManager exists in the scene.");
            return;
        }

        InputManager.Instance.OnClick.AddListener(OnClick);
        InputManager.Instance.OnDragStart.AddListener(OnDragStart);
        InputManager.Instance.OnDragging.AddListener(OnDragging);
        InputManager.Instance.OnDrop.AddListener(OnDrop);

        FlipBoardIfNeeded(); // flip perspective if human plays Black BEFORE spawning pieces
        SetupBoard();        // pieces now spawn using correct world pos (center vs bottom-center)
    }

    void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnClick.RemoveListener(OnClick);
            InputManager.Instance.OnDragStart.RemoveListener(OnDragStart);
            InputManager.Instance.OnDragging.RemoveListener(OnDragging);
            InputManager.Instance.OnDrop.RemoveListener(OnDrop);
        }
    }

    void Update()
    {
        gridSystem.DebugDrawGridLines(Color.green, Time.deltaTime);
    }

    // -------------------------
    // Board Perspective Flip
    // -------------------------
    /// <summary>
    /// If the human player chose Black, rotate the camera 180° on the Z-axis.
    /// Unity's ScreenToWorldPoint accounts for camera rotation automatically,
    /// so click/drag input still maps to the correct grid squares.
    /// Each sprite is counter-rotated so it remains upright from the camera's view.
    /// </summary>
    private void FlipBoardIfNeeded()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.HumanPlayerColor != PieceColor.Black) return;

        isBoardFlipped = true;
        GameManager.Instance.SetBoardFlipped(true); // inform other systems (e.g. OrderY)

        // Rotate the camera 180° on Z so the board appears from Black's side.
        if (Camera.main != null)
            Camera.main.transform.Rotate(0f, 0f, 180f);

        // Pieces will be spawned AFTER this call with isBoardFlipped=true,
        // so SpawnSinglePiece already applies ApplyFlipRotation to each piece.
    }

    /// <summary>Rotates a transform 180° on Z to offset the camera flip.</summary>
    private void ApplyFlipRotation(Transform t)
    {
        t.rotation = Quaternion.Euler(0f, 0f, 180f);
    }

    /// <summary>
    /// Returns the world position at which a piece should be placed.
    /// When the board is NOT flipped: bottom-center of the cell (pieces stand on the grid line).
    /// When the board IS flipped: cell center, because the camera already mirrors everything;
    /// using bottom-center + 180° sprite rotation shifts the sprite one cell up due to pivot flip.
    /// </summary>
    private Vector3 GetPieceWorldPos(int x, int y)
    {
        return isBoardFlipped
            ? gridSystem.GetCellTopCenterWorldPosition(x, y)
            : gridSystem.GetCellBottomCenterWorldPosition(x, y);
    }

    // -------------------------
    // Input Event Handlers
    // -------------------------
    private void OnClick(Vector3 worldPos) => TrySelectPieceAt(worldPos);
    private void OnDragStart(Vector3 worldPos) => TrySelectPieceAt(worldPos);

    private void OnDragging(Vector3 worldPos)
    {
        if (selectedPiece == null) return;
        selectedPiece.transform.position = worldPos;
    }

    private void OnDrop(Vector3 worldPos)
    {
        if (selectedPiece == null) return;

        Vector2Int dropGridPos = gridSystem.GetGridPosition(worldPos);
        if (!gridSystem.IsInBounds(dropGridPos))
        {
            SnapSelectedPieceBackToOriginal();
            return;
        }

        bool isValid = selectedPieceValidMoves.Contains(dropGridPos);

        if (!isValid)
        {
            SnapSelectedPieceBackToOriginal();
            if (GameManager.Instance.CheckedKing == selectedPiece.pieceColor)
            {
                GameManager.Instance.TriggerInvalidMoveInCheck(selectedPiece.pieceColor);
            }
            return;
        }

        TryMovePiece(selectedPiece, dropGridPos);
    }

    private void FinalizeMoveProcess(MoveRecord rec, PieceType? promotedTo)
    {
        PieceColor nextPlayer = (GameManager.Instance.CurrentState == GameState.WhiteTurn) ? PieceColor.Black : PieceColor.White;
        bool inCheck = IsKingInCheck(nextPlayer);
        bool hasAnyLegal = HasAnyLegalMoveForColor(nextPlayer);
        bool isCheckmate = inCheck && !hasAnyLegal;

        string moveSAN = ChessNotation.GetSAN(this, rec, inCheck, isCheckmate, promotedTo);

        UpdateCheckStatus();

        // ──────────────────────────────────────────────
        // Chess SFX: Checkmate or Move/Capture
        // ──────────────────────────────────────────────
        if (isCheckmate)
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.Checkmate);
        }
        else if (inCheck)
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.KingCheck);
        }

        bool resetHalfMove = (rec.piece is Pawn) || (rec.captured != null) || rec.wasEnPassant;
        string newFEN = ChessNotation.GetFEN(this, nextPlayer, GameManager.Instance.HalfMoveClock, GameManager.Instance.FullMoveNumber);

        GameManager.Instance.RecordMoveInfo(moveSAN, newFEN, resetHalfMove);
        GameManager.Instance.EndTurn();

        if (isCheckmate)
        {
            Debug.Log($"{nextPlayer} is checkmated!");
            GameManager.Instance.ChangeState(GameState.GameOver);
        }

        ClearSelection();
    }



    // Promote pawn to queen (simple automatic promotion)
    // private void PromotePawnToQueen(Pawn pawn, Vector2Int pos)
    // {
    //     BoardCell cell = gridSystem.GetGridObject(pos.x, pos.y);
    //     if (cell == null) return;

    //     // instantiate queen, copy color, set position and grid cell
    //     ChessPiece newQ = Instantiate(queenPrefab, transform);
    //     newQ.pieceType = PieceType.Queen;
    //     newQ.pieceColor = pawn.pieceColor;
    //     newQ.currentGridPosition = pos;
    //     newQ.transform.position = gridSystem.GetCellBottomCenterWorldPosition(pos.x, pos.y);

    //     cell.SetPiece(newQ);

    //     // destroy pawn
    //     Destroy(pawn.gameObject);
    // }

    // generic promotion helper
    private void PromotePawn(Pawn pawn, PieceType promoteTo)
    {
        Vector2Int pos = pawn.currentGridPosition;
        BoardCell cell = gridSystem.GetGridObject(pos.x, pos.y);
        if (cell == null) return;

        ChessPiece prefab = null;
        switch (promoteTo)
        {
            case PieceType.Queen: prefab = queenPrefab; break;
            case PieceType.Rook: prefab = rookPrefab; break;
            case PieceType.Bishop: prefab = bishopPrefab; break;
            case PieceType.Knight: prefab = knightPrefab; break;
            default: prefab = queenPrefab; break; // fallback
        }

        if (prefab == null) return;

        ChessPiece newPiece = Instantiate(prefab, transform);
        newPiece.pieceType = promoteTo;
        newPiece.pieceColor = pawn.pieceColor;
        newPiece.currentGridPosition = pos;
        newPiece.transform.position = GetPieceWorldPos(pos.x, pos.y);
        if (isBoardFlipped) ApplyFlipRotation(newPiece.transform);

        // copy sprite if using pieceData
        SpriteRenderer sr = newPiece.GetComponent<SpriteRenderer>();
        if (sr != null && pieceData != null) sr.sprite = pieceData.GetSprite(promoteTo, newPiece.pieceColor);

        // place in cell
        cell.SetPiece(newPiece);

        // destroy pawn
        Destroy(pawn.gameObject);
    }

    // existing auto-queen (you can keep it as a wrapper)
    private void PromotePawnToQueen(Pawn pawn, Vector2Int pos)
    {
        PromotePawn(pawn, PieceType.Queen);
    }

    public void RaiseKingInCheck(PieceColor kingColor)
    {
        Debug.Log($"{kingColor} King is in CHECK!");
        OnKingInCheck?.Invoke(kingColor);
    }

    // -------------------------
    // Selection helpers
    // -------------------------
    private void TrySelectPieceAt(Vector3 worldPos)
    {
        Vector2Int gridPos = gridSystem.GetGridPosition(worldPos);
        if (!gridSystem.IsInBounds(gridPos)) return;

        BoardCell cell = gridSystem.GetGridObject(gridPos.x, gridPos.y);
        ChessPiece piece = cell.GetPiece();
        if (piece == null) return;

        // Turn enforcement
        if (GameManager.Instance.CurrentState == GameState.GameOver)
        {
            return;
        }

        // Prevent selecting ANY pieces if it's currently the Bot's turn
        if (GameManager.Instance.IsBotTurn())
        {
            return;
        }

        bool isWhiteTurn = GameManager.Instance.CurrentState == GameState.WhiteTurn;
        bool isWhitePiece = piece.pieceColor == PieceColor.White;
        if ((isWhiteTurn && !isWhitePiece) || (!isWhiteTurn && isWhitePiece))
        {
            Debug.Log("Not your turn!");
            return;
        }

        // If a king is currently marked as checked, allow moves that resolve check (we still allow selecting other pieces;
        // but we will only show legal moves that actually free the king — the GetLegalMoves filtering covers that).
        // If you want to strictly disallow selecting other pieces while in check, you can uncomment the block below:
        /*
        PieceColor? checkedKing = GameManager.Instance.CheckedKing;
        if (checkedKing.HasValue && checkedKing.Value == piece.pieceColor && piece.pieceType != PieceType.King)
        {
            Debug.Log("King in check: only move that resolves the check is legal. Select a piece that can resolve it.");
            // let selection proceed; legal move list may be empty.
        }
        */

        // If already selected, do nothing
        if (selectedPiece == piece) return;

        // Select
        selectedPiece = piece;
        selectedPieceOriginalGrid = piece.currentGridPosition;
        selectedPieceOriginalCell = cell;
        selectedPieceOriginalWorldPos = piece.transform.position;

        // Compute & show legal moves (get legal moves via simulation)
        selectedPieceValidMoves.Clear();
        DeleteAllValidMoveShowers();

        List<Vector2Int> legal = GetLegalMoves(piece);
        if (legal != null)
        {
            selectedPieceValidMoves.AddRange(legal);
            foreach (Vector2Int mv in selectedPieceValidMoves)
            {
                GameObject showMove = Instantiate(showMovePrefab, transform);
                showMove.transform.position = gridSystem.GetCellCenterWorldPosition(mv.x, mv.y);
                if (isBoardFlipped) ApplyFlipRotation(showMove.transform);
                selectedPieceValidMoveShower.Add(showMove);
            }
        }
        
        if (GameManager.Instance.CheckedKing == piece.pieceColor && selectedPieceValidMoves.Count == 0)
        {
            GameManager.Instance.TriggerInvalidMoveInCheck(piece.pieceColor);
        }

        // ──────────────────────────────────────────────
        // Chess SFX: Piece Click
        // ──────────────────────────────────────────────
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.PieceClick);

        Debug.Log($"Selected <color=red>{piece.pieceColor}</color> <color=green>{piece.pieceType}</color> at <color=yellow>{cell.GetCellName()}</color>");
    }

    private void DeleteAllValidMoveShowers()
    {
        foreach (GameObject shower in selectedPieceValidMoveShower) Destroy(shower);
        selectedPieceValidMoveShower.Clear();
    }

    private bool IsDropToEmptyCellAllowed(Vector2Int dropGridPos)
    {
        BoardCell destCell = gridSystem.GetGridObject(dropGridPos.x, dropGridPos.y);
        return destCell.GetPiece() == null;
    }

    private void SnapSelectedPieceBackToOriginal()
    {
        if (selectedPiece == null) return;
        selectedPiece.transform.position = GetPieceWorldPos(selectedPieceOriginalGrid.x, selectedPieceOriginalGrid.y);
        // keep selectedPiece
    }

    private void ClearSelection()
    {
        selectedPiece = null;
        selectedPieceValidMoves.Clear();
        selectedPieceOriginalCell = null;
    }

    // -------------------------
    // Simulation & legal-move filtering
    // -------------------------
    // Simulate a move and return a MoveRecord that can be used to undo.
    // public MoveRecord SimulateMove(ChessPiece piece, Vector2Int to)
    // {
    //     Vector2Int from = piece.currentGridPosition;
    //     BoardCell fromCell = gridSystem.GetGridObject(from.x, from.y);
    //     BoardCell toCell = gridSystem.GetGridObject(to.x, to.y);

    //     ChessPiece captured = null;
    //     bool wasEnPassant = false;

    //     // check for normal capture on destination
    //     if (toCell != null)
    //     {
    //         captured = toCell.GetPiece();
    //     }

    //     // En-passant simulation: pawn moving diagonally into an empty cell captures pawn behind
    //     if (captured == null && piece is Pawn && from.x != to.x)
    //     {
    //         // captured pawn location is at (to.x, from.y)
    //         BoardCell epCell = gridSystem.GetGridObject(to.x, from.y);
    //         if (epCell != null)
    //         {
    //             ChessPiece possible = epCell.GetPiece();
    //             if (possible != null && possible is Pawn && possible.pieceColor != piece.pieceColor)
    //             {
    //                 // the pseudo-legal move list would only include this ep move if last move double-pushed; 
    //                 // but for safety, we allow simulation to treat it as a capture if the pawn exists
    //                 captured = possible;
    //                 wasEnPassant = true;
    //                 // remove captured from its cell in simulation
    //                 epCell.SetPiece(null);
    //             }
    //         }
    //     }

    //     // perform move in data only (do not touch transforms)
    //     if (fromCell != null && fromCell.GetPiece() == piece) fromCell.SetPiece(null);
    //     if (toCell != null) toCell.SetPiece(piece);

    //     piece.currentGridPosition = to;

    //     MoveRecord rec = new MoveRecord
    //     {
    //         piece = piece,
    //         from = from,
    //         to = to,
    //         captured = captured,
    //         wasDoublePawnPush = (piece is Pawn) && Mathf.Abs(to.y - from.y) == 2,
    //         wasEnPassant = wasEnPassant
    //     };
    //     return rec;
    // }

    // // Undo simulation (must be called with the record returned by SimulateMove)
    // public void UndoSimulatedMove(MoveRecord rec)
    // {
    //     // rec.piece is currently at rec.to
    //     BoardCell fromCell = gridSystem.GetGridObject(rec.from.x, rec.from.y);
    //     BoardCell toCell = gridSystem.GetGridObject(rec.to.x, rec.to.y);

    //     // remove piece from dest cell and put back to origin
    //     if (toCell != null && toCell.GetPiece() == rec.piece) toCell.SetPiece(null);
    //     if (fromCell != null) fromCell.SetPiece(rec.piece);

    //     rec.piece.currentGridPosition = rec.from;

    //     // restore captured
    //     if (rec.captured != null)
    //     {
    //         if (rec.wasEnPassant)
    //         {
    //             // captured pawn was on rec.to.x, rec.from.y
    //             BoardCell epCell = gridSystem.GetGridObject(rec.to.x, rec.from.y);
    //             if (epCell != null) epCell.SetPiece(rec.captured);
    //         }
    //         else
    //         {
    //             // put captured back at rec.to
    //             if (toCell != null) toCell.SetPiece(rec.captured);
    //         }
    //         // NOTE: we do not destroy or recreate objects during simulation; just reattach them to cells.
    //     }
    // }

    public MoveRecord SimulateMove(ChessPiece piece, Vector2Int to)
    {
        Vector2Int from = piece.currentGridPosition;
        BoardCell fromCell = gridSystem.GetGridObject(from.x, from.y);
        BoardCell toCell = gridSystem.GetGridObject(to.x, to.y);

        ChessPiece captured = null;
        bool wasEnPassant = false;

        // Normal capture on destination
        if (toCell != null)
        {
            captured = toCell.GetPiece();
            if (captured != null)
            {
                // detach captured from its cell for simulation
                toCell.SetPiece(null);
            }
        }

        // En-passant simulation: pawn moving diagonally into an empty cell captures pawn behind
        if (captured == null && piece is Pawn && from.x != to.x)
        {
            BoardCell epCell = gridSystem.GetGridObject(to.x, from.y);
            if (epCell != null)
            {
                ChessPiece possible = epCell.GetPiece();
                // Only treat as en-passant capture if there's a pawn there of opposite color
                if (possible != null && possible is Pawn && possible.pieceColor != piece.pieceColor)
                {
                    captured = possible;
                    wasEnPassant = true;
                    epCell.SetPiece(null); // remove for simulation
                }
            }
        }

        // move the piece in the board model (no transforms)
        if (fromCell != null && fromCell.GetPiece() == piece) fromCell.SetPiece(null);
        if (toCell != null) toCell.SetPiece(piece);

        piece.currentGridPosition = to;

        MoveRecord rec = new MoveRecord
        {
            piece = piece,
            from = from,
            to = to,
            captured = captured,
            wasDoublePawnPush = (piece is Pawn) && Mathf.Abs(to.y - from.y) == 2,
            wasEnPassant = wasEnPassant
        };
        return rec;
    }

    public void UndoSimulatedMove(MoveRecord rec)
    {
        BoardCell fromCell = gridSystem.GetGridObject(rec.from.x, rec.from.y);
        BoardCell toCell = gridSystem.GetGridObject(rec.to.x, rec.to.y);

        // remove piece from dest cell if it's our moved piece
        if (toCell != null && toCell.GetPiece() == rec.piece) toCell.SetPiece(null);

        // put piece back to original cell
        if (fromCell != null) fromCell.SetPiece(rec.piece);
        rec.piece.currentGridPosition = rec.from;

        // restore captured piece if any
        if (rec.captured != null)
        {
            if (rec.wasEnPassant)
            {
                // captured pawn belongs on rec.to.x, rec.from.y
                BoardCell epCell = gridSystem.GetGridObject(rec.to.x, rec.from.y);
                if (epCell != null) epCell.SetPiece(rec.captured);
                rec.captured.currentGridPosition = new Vector2Int(rec.to.x, rec.from.y);
            }
            else
            {
                // put captured back at rec.to
                if (toCell != null) toCell.SetPiece(rec.captured);
                rec.captured.currentGridPosition = rec.to;
            }
        }
    }

    /// <summary>
    /// Returns legal moves for a piece (filters pseudo-legal GetValidMoves using simulation to disallow leaving own king in check).
    /// </summary>
    public List<Vector2Int> GetLegalMoves(ChessPiece piece)
    {
        List<Vector2Int> legal = new List<Vector2Int>();
        List<Vector2Int> pseudo = piece.GetValidMoves(this);
        if (pseudo == null) return legal;

        foreach (Vector2Int target in pseudo)
        {
            // simulate
            MoveRecord rec = SimulateMove(piece, target);

            // Check whether own king is in check after the move
            bool stillInCheck = IsKingInCheck(piece.pieceColor);

            // undo simulation
            UndoSimulatedMove(rec);

            if (!stillInCheck)
            {
                legal.Add(target);
            }
        }
        return legal;
    }

    // Check if any piece of the color has at least one legal move
    private bool HasAnyLegalMoveForColor(PieceColor color)
    {
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                BoardCell c = gridSystem.GetGridObject(x, y);
                ChessPiece p = c.GetPiece();
                if (p == null) continue;
                if (p.pieceColor != color) continue;
                List<Vector2Int> legal = GetLegalMoves(p);
                if (legal != null && legal.Count > 0) return true;
            }
        }
        return false;
    }

    // -------------------------
    // Check detection helpers
    // -------------------------
    public bool TryGetKingPosition(PieceColor color, out Vector2Int kingPos)
    {
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                BoardCell c = gridSystem.GetGridObject(x, y);
                ChessPiece p = c.GetPiece();
                if (p != null && p.pieceType == PieceType.King && p.pieceColor == color)
                {
                    kingPos = new Vector2Int(x, y);
                    return true;
                }
            }
        }
        kingPos = default;
        return false;
    }

    // Determine if a king of given color is currently in check (attacked by any opposing pseudo-legal moves)
    // public bool IsKingInCheck(PieceColor kingColor)
    // {
    //     if (!TryGetKingPosition(kingColor, out Vector2Int kingPos)) return false;

    //     PieceColor attackerColor = (kingColor == PieceColor.White) ? PieceColor.Black : PieceColor.White;

    //     for (int x = 0; x < boardSize; x++)
    //     {
    //         for (int y = 0; y < boardSize; y++)
    //         {
    //             BoardCell c = gridSystem.GetGridObject(x, y);
    //             ChessPiece p = c.GetPiece();
    //             if (p == null || p.pieceColor != attackerColor) continue;

    //             // Use pseudo-legal moves (attacks) to see if king square is reachable
    //             List<Vector2Int> attacks = p.GetValidMoves(this);
    //             if (attacks != null && attacks.Contains(kingPos))
    //             {
    //                 return true;
    //             }
    //         }
    //     }
    //     return false;
    // }

    // Robust attacker test: returns true if square is attacked by 'byColor'
    public bool IsSquareAttacked(Vector2Int square, PieceColor byColor)
    {
        // To find a pawn of 'byColor' that attacks 'square', we must look in the
        // OPPOSITE direction to that pawn's movement:
        //   White pawns move UP   (+y) → they attack squares above them
        //                              → look BELOW the target (pawnDir = -1)
        //   Black pawns move DOWN (-y) → they attack squares below them
        //                              → look ABOVE the target (pawnDir = +1)
        int pawnDir = (byColor == PieceColor.White) ? -1 : 1;
        Vector2Int[] pawnAttacks = new Vector2Int[] { new Vector2Int(1, pawnDir), new Vector2Int(-1, pawnDir) };
        foreach (var d in pawnAttacks)
        {
            Vector2Int p = square + d;
            if (!IsInBounds(p)) continue;
            ChessPiece cp = GetPieceAt(p);
            if (cp != null && cp.pieceColor == byColor && cp.pieceType == PieceType.Pawn)
                return true;
        }

        // Knights
        Vector2Int[] knightOffsets = new Vector2Int[]
        {
        new Vector2Int(1,2), new Vector2Int(2,1), new Vector2Int(-1,2), new Vector2Int(-2,1),
        new Vector2Int(1,-2), new Vector2Int(2,-1), new Vector2Int(-1,-2), new Vector2Int(-2,-1)
        };
        foreach (var o in knightOffsets)
        {
            Vector2Int p = square + o;
            if (!IsInBounds(p)) continue;
            ChessPiece cp = GetPieceAt(p);
            if (cp != null && cp.pieceColor == byColor && cp.pieceType == PieceType.Knight)
                return true;
        }

        // King adjacency (opponent king can't be next to your king)
        Vector2Int[] kingOffsets = new Vector2Int[]
        {
        new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1),
        new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1)
        };
        foreach (var o in kingOffsets)
        {
            Vector2Int p = square + o;
            if (!IsInBounds(p)) continue;
            ChessPiece cp = GetPieceAt(p);
            if (cp != null && cp.pieceColor == byColor && cp.pieceType == PieceType.King)
                return true;
        }

        // Sliding pieces: rook/queen (orthogonal)
        Vector2Int[] orthDirs = new Vector2Int[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        foreach (var dir in orthDirs)
        {
            Vector2Int p = square + dir;
            while (IsInBounds(p))
            {
                ChessPiece cp = GetPieceAt(p);
                if (cp != null)
                {
                    if (cp.pieceColor == byColor && (cp.pieceType == PieceType.Rook || cp.pieceType == PieceType.Queen))
                        return true;
                    // blocked by any piece
                    break;
                }
                p += dir;
            }
        }

        // Sliding pieces: bishop/queen (diagonals)
        Vector2Int[] diagDirs = new Vector2Int[] { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) };
        foreach (var dir in diagDirs)
        {
            Vector2Int p = square + dir;
            while (IsInBounds(p))
            {
                ChessPiece cp = GetPieceAt(p);
                if (cp != null)
                {
                    if (cp.pieceColor == byColor && (cp.pieceType == PieceType.Bishop || cp.pieceType == PieceType.Queen))
                        return true;
                    // blocked
                    break;
                }
                p += dir;
            }
        }

        return false;
    }

    // Replace existing IsKingInCheck with this simple wrapper:
    public bool IsKingInCheck(PieceColor kingColor)
    {
        if (!TryGetKingPosition(kingColor, out Vector2Int kingPos)) return false;
        PieceColor attackerColor = (kingColor == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        return IsSquareAttacked(kingPos, attackerColor);
    }



    private void UpdateCheckStatus()
    {
        bool whiteInCheck = IsKingInCheck(PieceColor.White);
        bool blackInCheck = IsKingInCheck(PieceColor.Black);

        if (whiteInCheck)
        {
            RaiseKingInCheck(PieceColor.White);
            GameManager.Instance.SetCheckedKing(PieceColor.White);
        }
        else if (blackInCheck)
        {
            RaiseKingInCheck(PieceColor.Black);
            GameManager.Instance.SetCheckedKing(PieceColor.Black);
        }
        else
        {
            GameManager.Instance.SetCheckedKing(null);
        }
    }

    // -------------------------
    // Setup board & spawning
    // -------------------------
    void SetupBoard()
    {

        SetupBoard(useCustomSetup ? customSetupAsset : null);
        string startFEN = ChessNotation.GetFEN(this, PieceColor.White, 0, 1);
        GameManager.Instance.InitializeHistory(startFEN);
        // SpawnRowOfPawns(1, PieceColor.White);
        // SpawnMajorPieces(0, PieceColor.White);

        // SpawnRowOfPawns(6, PieceColor.Black);
        // SpawnMajorPieces(7, PieceColor.Black);

        // GameManager.Instance.ChangeState(GameState.WhiteTurn);
    }

    // Apply a setup asset. If asset is null -> spawn default layout.
    public void SetupBoard(ChessSetupAsset asset)
    {
        // clear existing pieces in grid (if any)
        ClearBoard();
        if (asset == null)
        {
            // default spawn (classic chess)
            SpawnRowOfPawns(1, PieceColor.White);
            SpawnMajorPieces(0, PieceColor.White);

            SpawnRowOfPawns(6, PieceColor.Black);
            SpawnMajorPieces(7, PieceColor.Black);

            GameManager.Instance.ChangeState(GameState.WhiteTurn);
        }
        else
        {
            // spawn according to asset
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    var sq = asset.Get(x, y);
                    if (sq != null && sq.hasPiece)
                    {
                        // choose prefab by type
                        ChessPiece prefab = GetPrefabForType(sq.pieceType);
                        if (prefab != null)
                        {
                            SpawnSinglePiece(prefab, sq.pieceType, sq.pieceColor, x, y);
                        }
                        else
                        {
                            Debug.LogWarning($"No prefab for piece type {sq.pieceType}");
                        }
                    }
                }
            }

            // set starting side
            if (asset.startingSide == PieceColor.White) GameManager.Instance.ChangeState(GameState.WhiteTurn);
            else GameManager.Instance.ChangeState(GameState.BlackTurn);

            // after spawn check kings for any immediate checks
            UpdateCheckStatus();
        }
    }

    // destroys all pieces and clears cells
    private void ClearBoard()
    {
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                BoardCell c = gridSystem.GetGridObject(x, y);
                if (c == null) continue;
                ChessPiece p = c.GetPiece();
                if (p != null)
                {
                    Destroy(p.gameObject);
                }
                c.SetPiece(null);
            }
        }
    }

    // helper to return prefab reference by PieceType
    private ChessPiece GetPrefabForType(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return pawnPrefab;
            case PieceType.Rook: return rookPrefab;
            case PieceType.Knight: return knightPrefab;
            case PieceType.Bishop: return bishopPrefab;
            case PieceType.Queen: return queenPrefab;
            case PieceType.King: return kingPrefab;
            default: return null;
        }
    }

    void SpawnRowOfPawns(int y, PieceColor color)
    {
        for (int x = 0; x < boardSize; x++)
        {
            SpawnSinglePiece(pawnPrefab, PieceType.Pawn, color, x, y);
        }
    }

    private void SpawnMajorPieces(int y, PieceColor color)
    {
        SpawnSinglePiece(rookPrefab, PieceType.Rook, color, 0, y);
        SpawnSinglePiece(knightPrefab, PieceType.Knight, color, 1, y);
        SpawnSinglePiece(bishopPrefab, PieceType.Bishop, color, 2, y);
        SpawnSinglePiece(queenPrefab, PieceType.Queen, color, 3, y);
        SpawnSinglePiece(kingPrefab, PieceType.King, color, 4, y);
        SpawnSinglePiece(bishopPrefab, PieceType.Bishop, color, 5, y);
        SpawnSinglePiece(knightPrefab, PieceType.Knight, color, 6, y);
        SpawnSinglePiece(rookPrefab, PieceType.Rook, color, 7, y);
    }

    void SpawnSinglePiece(ChessPiece prefab, PieceType type, PieceColor color, int x, int y)
    {
        ChessPiece newPiece = Instantiate(prefab, transform);
        newPiece.pieceType = type;
        newPiece.pieceColor = color;
        newPiece.currentGridPosition = new Vector2Int(x, y);

        SpriteRenderer sr = newPiece.GetComponent<SpriteRenderer>();
        if (sr != null && pieceData != null) sr.sprite = pieceData.GetSprite(type, color);

        newPiece.transform.position = GetPieceWorldPos(x, y);
        if (isBoardFlipped) ApplyFlipRotation(newPiece.transform);

        BoardCell cell = gridSystem.GetGridObject(x, y);
        cell.SetPiece(newPiece);
    }

    // -------------------------
    // Helper Methods
    // -------------------------
    public BoardCell GetCell(Vector2Int gridPosition) => gridSystem.GetGridObject(gridPosition.x, gridPosition.y);

    public bool IsInBounds(Vector2Int gridPos) => gridSystem.IsInBounds(gridPos);

    public ChessPiece GetPieceAt(Vector2Int gridPos)
    {
        if (!IsInBounds(gridPos)) return null;
        BoardCell cell = gridSystem.GetGridObject(gridPos.x, gridPos.y);
        return cell != null ? cell.GetPiece() : null;
    }


    public bool TryMovePiece(ChessPiece piece, Vector2Int to)
    {
        if (piece == null) return false;

        List<Vector2Int> legal = GetLegalMoves(piece);
        if (legal == null || !legal.Contains(to)) return false;

        Vector2Int from = piece.currentGridPosition;
        BoardCell originCell = gridSystem.GetGridObject(from.x, from.y);
        BoardCell destCell = gridSystem.GetGridObject(to.x, to.y);

        ChessPiece captured = destCell.GetPiece();
        bool enPassantCapture = false;

        if (piece is Pawn)
        {
            if (captured == null && LastMove.HasValue)
            {
                MoveRecord lm = LastMove.Value;
                if (lm.piece is Pawn && lm.wasDoublePawnPush && lm.to.y == piece.currentGridPosition.y && Mathf.Abs(lm.to.x - piece.currentGridPosition.x) == 1 && to.x == lm.to.x && to.y == piece.currentGridPosition.y + ((piece.pieceColor == PieceColor.White) ? 1 : -1))
                {
                    BoardCell epCell = gridSystem.GetGridObject(lm.to.x, lm.to.y);
                    captured = epCell.GetPiece();
                    if (captured != null)
                    {
                        epCell.SetPiece(null);
                        Destroy(captured.gameObject);
                        enPassantCapture = true;
                    }
                }
            }
        }

        if (captured != null && !enPassantCapture)
        {
            destCell.SetPiece(null);
            Destroy(captured.gameObject);
        }

        if (originCell != null && originCell.GetPiece() == piece) originCell.SetPiece(null);

        piece.currentGridPosition = to;
        piece.transform.position = GetPieceWorldPos(to.x, to.y);
        destCell.SetPiece(piece);

        MoveRecord rec = new MoveRecord
        {
            piece = piece,
            from = from,
            to = to,
            captured = captured,
            wasDoublePawnPush = (piece is Pawn) && Mathf.Abs(to.y - from.y) == 2,
            wasEnPassant = enPassantCapture
        };
        LastMove = rec;

        // CASTLING
        if (piece is King && Mathf.Abs(to.x - from.x) == 2)
        {
            int rookX = (to.x > from.x) ? (boardSize - 1) : 0;
            BoardCell rookOrigCell = gridSystem.GetGridObject(rookX, to.y);
            ChessPiece rook = rookOrigCell?.GetPiece();
            if (rook != null && rook is Rook && rook.pieceColor == piece.pieceColor)
            {
                int rookDestX = (to.x > from.x) ? (to.x - 1) : (to.x + 1);
                BoardCell rookDestCell = gridSystem.GetGridObject(rookDestX, to.y);

                if (rookOrigCell != null && rookOrigCell.GetPiece() == rook) rookOrigCell.SetPiece(null);
                if (rookDestCell != null) rookDestCell.SetPiece(rook);

                rook.currentGridPosition = new Vector2Int(rookDestX, to.y);
                rook.transform.position = GetPieceWorldPos(rookDestX, to.y);
                rook.hasMoved = true;
            }
        }

        // Promotion
        if (piece is Pawn)
        {
            bool reachedLastRank = (piece.pieceColor == PieceColor.White && to.y == boardSize - 1) || (piece.pieceColor == PieceColor.Black && to.y == 0);
            if (reachedLastRank)
            {
                Pawn pawnToPromote = (Pawn)piece;
                DeleteAllValidMoveShowers();
                
                if (promotionUI != null && GameManager.Instance != null && !GameManager.Instance.IsBotTurn())
                {
                    promotionUI.Show(pawnToPromote.pieceColor, (chosenType) =>
                    {
                        PromotePawn(pawnToPromote, chosenType);
                        piece.hasMoved = true;
                        FinalizeMoveProcess(rec, chosenType);
                    });
                    return true;
                }
                else
                {
                    PromotePawnToQueen((Pawn)piece, to);
                    piece.hasMoved = true;
                    FinalizeMoveProcess(rec, PieceType.Queen);
                    return true;
                }
            }
        }

        piece.hasMoved = true;
        DeleteAllValidMoveShowers();

        // ──────────────────────────────────────────────
        // Chess SFX: Move vs Capture
        // ──────────────────────────────────────────────
        if (rec.captured != null)
        {
             // Check if captured by AI
             if (GameManager.Instance != null && GameManager.Instance.IsBotTurn())
                AudioManager.Instance?.PlaySFX(AudioManager.Instance.PieceCapturedByAI);
             else
                AudioManager.Instance?.PlaySFX(AudioManager.Instance.PieceCapture);
        }
        else
        {
            // Only play normal move sound if it wasn't a check (Check sound triggered in FinalizeMoveProcess)
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.PieceMove);
        }

        FinalizeMoveProcess(rec, null);
        return true;
    }
    public List<ChessPiece> GetPieces(PieceColor color)
    {
        List<ChessPiece> pieces = new List<ChessPiece>();

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                BoardCell c = gridSystem.GetGridObject(x, y);
                ChessPiece p = c.GetPiece();

                if (p != null && p.pieceColor == color)
                    pieces.Add(p);
            }
        }

        return pieces;
    }
}