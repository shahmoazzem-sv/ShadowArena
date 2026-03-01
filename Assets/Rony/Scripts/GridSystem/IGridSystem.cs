using UnityEngine;

public interface IGridSystem
{
    int Width { get; }
    int Height { get; }
    Vector2 CellSize { get; }

    Vector3 GetWorldPosition(int x, int y);
    Vector2Int GetGridPosition(Vector3 worldPosition);
    Vector3 GetCellCenterWorldPosition(int x, int y);
    bool IsInBounds(int x, int y);
    bool IsWorldPositionInBounds(Vector3 worldPosition);
    Vector3 SnapToGridCenter(Vector3 worldPosition);
}
