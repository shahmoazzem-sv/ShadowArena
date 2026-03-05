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

        SetupBoard();
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

        // Check against legal moves (we already compute legal moves when selecting but re-check to be safe)
        // List<Vector2Int> legal = GetLegalMoves(selectedPiece);
        // bool isValid = legal.Count == 0 ? IsDropToEmptyCellAllowed(dropGridPos) : legal.Contains(dropGridPos);
        // bool isValid = legal.Contains(dropGridPos);

        bool isValid = selectedPieceValidMoves.Contains(dropGridPos);

        if (!isValid)
        {
            SnapSelectedPieceBackToOriginal();
            return;
        }

        // Build move record to store LastMove after successful commit
        BoardCell originCell = selectedPieceOriginalCell;
        BoardCell destCell = gridSystem.GetGridObject(dropGridPos.x, dropGridPos.y);

        // Handle en-passant special case: if pawn moved diagonally to empty square and last move was enemy double pawn push
        ChessPiece captured = destCell.GetPiece();
        bool enPassantCapture = false;
        if (selectedPiece is Pawn)
        {
            if (captured == null && LastMove.HasValue)
            {
                MoveRecord lm = LastMove.Value;
                // en-passant capture occurs when pawn moves diagonally into square behind the just-moved pawn
                if (lm.piece is Pawn &&
                    lm.wasDoublePawnPush &&
                    lm.to.y == selectedPiece.currentGridPosition.y &&
                    Mathf.Abs(lm.to.x - selectedPiece.currentGridPosition.x) == 1 &&
                    dropGridPos.x == lm.to.x &&
                    dropGridPos.y == selectedPiece.currentGridPosition.y + ((selectedPiece.pieceColor == PieceColor.White) ? 1 : -1))
                {
                    // capture the pawn that did the double push (it's at lm.to)
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

        // Normal capture
        if (captured != null && !enPassantCapture)
        {
            destCell.SetPiece(null);
            Destroy(captured.gameObject);
        }

        // Remove from origin
        if (originCell != null && originCell.GetPiece() == selectedPiece)
            originCell.SetPiece(null);

        // Place to destination
        selectedPiece.currentGridPosition = dropGridPos;
        selectedPiece.transform.position = gridSystem.GetCellBottomCenterWorldPosition(dropGridPos.x, dropGridPos.y);
        destCell.SetPiece(selectedPiece);

        // create and store LastMove
        MoveRecord rec = new MoveRecord
        {
            piece = selectedPiece,
            from = selectedPieceOriginalGrid,
            to = dropGridPos,
            captured = captured,
            wasDoublePawnPush = (selectedPiece is Pawn) && Mathf.Abs(dropGridPos.y - selectedPieceOriginalGrid.y) == 2
        };
        LastMove = rec;

        // Promotion: auto-queen for simplicity
        if (selectedPiece is Pawn)
        {
            if ((selectedPiece.pieceColor == PieceColor.White && dropGridPos.y == boardSize - 1) ||
                (selectedPiece.pieceColor == PieceColor.Black && dropGridPos.y == 0))
            {
                PromotePawnToQueen((Pawn)selectedPiece, dropGridPos);
            }
        }

        selectedPiece.hasMoved = true;

        // cleanup visuals
        DeleteAllValidMoveShowers();

        // Recompute check and checkmate
        UpdateCheckStatus();

        // End turn and test for checkmate on next player
        GameManager.Instance.EndTurn();

        // After turn flip, check for checkmate for the new current player
        PieceColor next = (GameManager.Instance.CurrentState == GameState.WhiteTurn) ? PieceColor.White : PieceColor.Black;
        bool inCheck = IsKingInCheck(next);
        bool hasAnyLegal = HasAnyLegalMoveForColor(next);

        if (inCheck && !hasAnyLegal)
        {
            // checkmate
            Debug.Log($"{next} is checkmated!");
            GameManager.Instance.ChangeState(GameState.GameOver);
        }

        // Clear selection
        ClearSelection();
    }

    // Promote pawn to queen (simple automatic promotion)
    private void PromotePawnToQueen(Pawn pawn, Vector2Int pos)
    {
        BoardCell cell = gridSystem.GetGridObject(pos.x, pos.y);
        if (cell == null) return;

        // instantiate queen, copy color, set position and grid cell
        ChessPiece newQ = Instantiate(queenPrefab, transform);
        newQ.pieceType = PieceType.Queen;
        newQ.pieceColor = pawn.pieceColor;
        newQ.currentGridPosition = pos;
        newQ.transform.position = gridSystem.GetCellBottomCenterWorldPosition(pos.x, pos.y);

        cell.SetPiece(newQ);

        // destroy pawn
        Destroy(pawn.gameObject);
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
                selectedPieceValidMoveShower.Add(showMove);
            }
        }

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
        selectedPiece.transform.position = gridSystem.GetCellBottomCenterWorldPosition(selectedPieceOriginalGrid.x, selectedPieceOriginalGrid.y);
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
    public MoveRecord SimulateMove(ChessPiece piece, Vector2Int to)
    {
        Vector2Int from = piece.currentGridPosition;
        BoardCell fromCell = gridSystem.GetGridObject(from.x, from.y);
        BoardCell toCell = gridSystem.GetGridObject(to.x, to.y);

        ChessPiece captured = null;
        bool wasEnPassant = false;

        // check for normal capture on destination
        if (toCell != null)
        {
            captured = toCell.GetPiece();
        }

        // En-passant simulation: pawn moving diagonally into an empty cell captures pawn behind
        if (captured == null && piece is Pawn && from.x != to.x)
        {
            // captured pawn location is at (to.x, from.y)
            BoardCell epCell = gridSystem.GetGridObject(to.x, from.y);
            if (epCell != null)
            {
                ChessPiece possible = epCell.GetPiece();
                if (possible != null && possible is Pawn && possible.pieceColor != piece.pieceColor)
                {
                    // the pseudo-legal move list would only include this ep move if last move double-pushed; 
                    // but for safety, we allow simulation to treat it as a capture if the pawn exists
                    captured = possible;
                    wasEnPassant = true;
                    // remove captured from its cell in simulation
                    epCell.SetPiece(null);
                }
            }
        }

        // perform move in data only (do not touch transforms)
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

    // Undo simulation (must be called with the record returned by SimulateMove)
    public void UndoSimulatedMove(MoveRecord rec)
    {
        // rec.piece is currently at rec.to
        BoardCell fromCell = gridSystem.GetGridObject(rec.from.x, rec.from.y);
        BoardCell toCell = gridSystem.GetGridObject(rec.to.x, rec.to.y);

        // remove piece from dest cell and put back to origin
        if (toCell != null && toCell.GetPiece() == rec.piece) toCell.SetPiece(null);
        if (fromCell != null) fromCell.SetPiece(rec.piece);

        rec.piece.currentGridPosition = rec.from;

        // restore captured
        if (rec.captured != null)
        {
            if (rec.wasEnPassant)
            {
                // captured pawn was on rec.to.x, rec.from.y
                BoardCell epCell = gridSystem.GetGridObject(rec.to.x, rec.from.y);
                if (epCell != null) epCell.SetPiece(rec.captured);
            }
            else
            {
                // put captured back at rec.to
                if (toCell != null) toCell.SetPiece(rec.captured);
            }
            // NOTE: we do not destroy or recreate objects during simulation; just reattach them to cells.
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
    public bool IsKingInCheck(PieceColor kingColor)
    {
        if (!TryGetKingPosition(kingColor, out Vector2Int kingPos)) return false;

        PieceColor attackerColor = (kingColor == PieceColor.White) ? PieceColor.Black : PieceColor.White;

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                BoardCell c = gridSystem.GetGridObject(x, y);
                ChessPiece p = c.GetPiece();
                if (p == null || p.pieceColor != attackerColor) continue;

                // Use pseudo-legal moves (attacks) to see if king square is reachable
                List<Vector2Int> attacks = p.GetValidMoves(this);
                if (attacks != null && attacks.Contains(kingPos))
                {
                    return true;
                }
            }
        }
        return false;
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

        newPiece.transform.position = gridSystem.GetCellBottomCenterWorldPosition(x, y);

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
}