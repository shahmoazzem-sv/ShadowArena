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
                Debug.Log($"Clicked on <color=red> {cell.GetCellName()}.</color> Occupied: {cell.IsOccupied()}");
            }
        }
    }
}