using UnityEngine;

public class GridSystem : IGridSystem
{
    protected readonly int width;
    protected readonly int height;
    protected readonly Vector2 cellSize;
    protected readonly Vector3 originPosition;

    public int Width => width;
    public int Height => height;
    public Vector2 CellSize => cellSize;

    public GridSystem(int width, int height, Vector2 cellSize, Vector3 originPosition)
    {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;
        this.originPosition = originPosition;
    }

    #region Basic Conversion
    public Vector3 GetWorldPosition(int x, int y)
    {
        return new Vector3(x * cellSize.x, y * cellSize.y, 0) + originPosition;
    }

    public Vector2Int GetGridPosition(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x - originPosition.x) / cellSize.x);
        int y = Mathf.FloorToInt((worldPosition.y - originPosition.y) / cellSize.y);
        return new Vector2Int(x, y);
    }
    #endregion

    #region Cell Anchor Points
    public Vector3 GetCellCenterWorldPosition(int x, int y)
    {
        return GetWorldPosition(x, y) + (Vector3)cellSize * 0.5f;
    }

    public Vector3 GetCellBottomCenterWorldPosition(int x, int y) => GetWorldPosition(x, y) + new Vector3(cellSize.x * 0.5f, 0, 0);
    public Vector3 GetCellTopCenterWorldPosition(int x, int y) => GetWorldPosition(x, y) + new Vector3(cellSize.x * 0.5f, cellSize.y, 0);
    public Vector3 GetCellLeftCenterWorldPosition(int x, int y) => GetWorldPosition(x, y) + new Vector3(0, cellSize.y * 0.5f, 0);
    public Vector3 GetCellRightCenterWorldPosition(int x, int y) => GetWorldPosition(x, y) + new Vector3(cellSize.x, cellSize.y * 0.5f, 0);

    public Vector3 GetCellBottomLeftWorldPosition(int x, int y) => GetWorldPosition(x, y);
    public Vector3 GetCellBottomRightWorldPosition(int x, int y) => GetWorldPosition(x, y) + new Vector3(cellSize.x, 0, 0);
    public Vector3 GetCellTopLeftWorldPosition(int x, int y) => GetWorldPosition(x, y) + new Vector3(0, cellSize.y, 0);
    public Vector3 GetCellTopRightWorldPosition(int x, int y) => GetWorldPosition(x, y) + (Vector3)cellSize;
    #endregion

    #region Validation & Bounds
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    public bool IsInBounds(Vector2Int gridPos) => IsInBounds(gridPos.x, gridPos.y);

    public bool IsWorldPositionInBounds(Vector3 worldPosition)
    {
        Vector2Int gridPos = GetGridPosition(worldPosition);
        return IsInBounds(gridPos.x, gridPos.y);
    }
    #endregion

    #region Utility
    public Vector3 SnapToGridCenter(Vector3 worldPosition)
    {
        Vector2Int gridPos = GetGridPosition(worldPosition);
        return GetCellCenterWorldPosition(gridPos.x, gridPos.y);
    }

    public void DebugDrawGridLines(Color color, float duration = 0.01f)
    {
        for (int x = 0; x <= width; x++)
            Debug.DrawLine(GetWorldPosition(x, 0), GetWorldPosition(x, height), color, duration);

        for (int y = 0; y <= height; y++)
            Debug.DrawLine(GetWorldPosition(0, y), GetWorldPosition(width, y), color, duration);
    }
    #endregion
}