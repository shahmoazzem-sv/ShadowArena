using UnityEngine;
using UnityEngine.InputSystem;

public class BoardManager : MonoBehaviour
{

    Mouse mouse;
    private GridDataSystem<BoardCell> gridSystem;


    [Header("Grid Settings")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private Vector2 cellSize = new Vector2(1f, 1f);
    [SerializeField] Vector3 originPosition = Vector3.zero;

    [Header("Piece Setup")]
    [SerializeField] private PieceData pieceData; // Assign your ScriptableObject here
    [SerializeField] private Pawn pawnPrefab;
    [SerializeField] private Rook rookPrefab;
    [SerializeField] private Knight knightPrefab;
    [SerializeField] private Bishop bishopPrefab;
    [SerializeField] private Queen queenPrefab;
    [SerializeField] private King kingPrefab;


    void Start()
    {
        // Initialize the grid logic
        gridSystem = new GridDataSystem<BoardCell>(
            boardSize,
            boardSize,
            cellSize,
            originPosition,
            (GridDataSystem<BoardCell> g, int x, int y) => new BoardCell(g, x, y)
        );
        mouse = Mouse.current;

        // 2. Spawn the pieces
        SetupBoard();
    }

    void Update()
    {

        // We use Time.deltaTime for duration to keep it smooth.
        gridSystem.DebugDrawGridLines(Color.green, Time.deltaTime);

        if (mouse == null) return;

        // Example: Convert mouse position to grid (Optional)
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPosition = mouse.position.ReadValue();
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(screenPosition);
            mouseWorldPos.z = 0;


            Vector2Int gridPos = gridSystem.GetGridPosition(mouseWorldPos);
            if (gridSystem.IsInBounds(gridPos))
            {
                // 2. Use the "Data" part of the system (from the Generic class)
                BoardCell cell = gridSystem.GetGridObject(gridPos.x, gridPos.y);
                ChessPiece piece = cell.GetPiece();
                string pieceName = piece != null ? piece.pieceType.ToString() : "None";

                Debug.Log($"Clicked on <color=red> {cell.GetCellName()}.</color> Occupied: {cell.IsOccupied()} Piece: {pieceName}");
            }
        }
    }

    void SetupBoard()
    {
        //Spawn white pices (Row 0 and 1)
        SpawnRowOfPawns(1, PieceColor.White);
        SpawnMajorPieces(0, PieceColor.White);

        // Spawn Black Pieces (Row 6 and 7)
        SpawnRowOfPawns(6, PieceColor.Black);
        SpawnMajorPieces(7, PieceColor.Black);
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
        // 1. Instantiate the piece
        ChessPiece newPiece = Instantiate(prefab, transform);

        //2. setup its internal data
        newPiece.pieceType = type;
        newPiece.pieceColor = color;
        newPiece.currentGridPosition = new Vector2Int(x, y);

        // 3. Assign the correct Sprite using your PieceData
        SpriteRenderer sr = newPiece.GetComponent<SpriteRenderer>();
        if (sr != null && pieceData != null)
        {
            sr.sprite = pieceData.GetSprite(type, color);
        }

        // 4. Move it to the center of the grid cell
        // newPiece.transform.position = gridSystem.GetCellCenterWorldPosition(x, y);

        // 4. Move it to the bottom center of the grid cell
        newPiece.transform.position = gridSystem.GetCellBottomCenterWorldPosition(x, y);

        // 5. Tell the Grid System that this cell is now occupied
        BoardCell cell = gridSystem.GetGridObject(x, y);
        cell.SetPiece(newPiece);

    }

    // Helper method for the future when Pieces need to look at the board
    public BoardCell GetCell(Vector2Int gridPosition)
    {
        return gridSystem.GetGridObject(gridPosition.x, gridPosition.y);
    }
}