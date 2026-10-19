using UnityEngine;
using System;

public class GridDataSystem<TGridObject> : GridSystem
{
    // The underlying data storage
    private TGridObject[,] gridArray;

    public GridDataSystem(int width, int height, Vector2 cellSize, Vector3 originPosition,
        Func<GridDataSystem<TGridObject>, int, int, TGridObject> createGridObject)
        : base(width, height, cellSize, originPosition)
    {
        gridArray = new TGridObject[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                gridArray[x, y] = createGridObject(this, x, y);
            }
        }
    }

    #region Data Management
    public TGridObject GetGridObject(int x, int y)
    {
        if (IsInBounds(x, y)) return gridArray[x, y];
        return default;
    }

    public TGridObject GetGridObject(Vector3 worldPosition)
    {
        Vector2Int pos = GetGridPosition(worldPosition);
        return GetGridObject(pos.x, pos.y);
    }

    public void SetGridObject(int x, int y, TGridObject value)
    {
        if (IsInBounds(x, y)) gridArray[x, y] = value;
    }
    #endregion
}
