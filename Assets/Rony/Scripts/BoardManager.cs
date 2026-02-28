using UnityEngine;
using UnityEngine.InputSystem;

public class BoardManager : MonoBehaviour
{

    Mouse mouse;
    private GridSystem<BoardCell> gridSystem;


    [Header("Grid Settings")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private Vector2 cellSize = new Vector2(1f, 1f);
    [SerializeField] Vector3 originPosition = Vector3.zero;


    void Start()
    {
        // Initialize the grid logic
        gridSystem = new GridSystem<BoardCell>(
            boardSize,
            boardSize,
            cellSize,
            originPosition,
            (GridSystem<BoardCell> g, int x, int y) => new BoardCell(g, x, y)
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
            Debug.Log($"Clicked on Cell: {gridPos}");
        }
    }
}