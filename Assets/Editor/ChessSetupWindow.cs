#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ChessSetupAsset))]
public class ChessSetupAssetEditor : Editor
{
    const int BoardSize = 8;
    const float CellSize = 32f;

    public override void OnInspectorGUI()
    {
        ChessSetupAsset asset = (ChessSetupAsset)target;
        asset.EnsureValid();

        EditorGUILayout.Space();
        asset.startingSide = (PieceColor)EditorGUILayout.EnumPopup("Starting Side", asset.startingSide);

        EditorGUILayout.Space();
        GUILayout.Label("Board Setup", EditorStyles.boldLabel);

        Rect boardRect = GUILayoutUtility.GetRect(BoardSize * CellSize, BoardSize * CellSize);

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                int displayY = BoardSize - 1 - y; // rank 8 → 1
                Rect cell = new Rect(
                    boardRect.x + x * CellSize,
                    boardRect.y + y * CellSize,
                    CellSize - 1,
                    CellSize - 1
                );

                bool dark = (x + displayY) % 2 == 1;
                EditorGUI.DrawRect(cell, dark ? new Color(0.25f, 0.5f, 0.25f) : new Color(0.9f, 0.9f, 0.9f));

                var sq = asset.Get(x, displayY);
                if (sq.hasPiece)
                {
                    string label =
                        sq.pieceType.ToString()[0].ToString() +
                        (sq.pieceColor == PieceColor.White ? "W" : "B");

                    GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                    EditorGUI.LabelField(cell, label, style);
                }

                if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                {
                    ShowSquareEditor(asset, x, displayY);
                }
            }
        }

        EditorGUILayout.Space();
        if (GUI.changed)
        {
            EditorUtility.SetDirty(asset);
        }
    }

    void ShowSquareEditor(ChessSetupAsset asset, int x, int y)
    {
        var sq = asset.Get(x, y);

        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("Clear"), !sq.hasPiece, () =>
        {
            asset.Set(x, y, false, PieceType.Pawn, PieceColor.White);
            EditorUtility.SetDirty(asset);
        });

        foreach (PieceColor color in System.Enum.GetValues(typeof(PieceColor)))
        {
            foreach (PieceType type in System.Enum.GetValues(typeof(PieceType)))
            {
                string name = $"{color}/{type}";
                menu.AddItem(new GUIContent(name), false, () =>
                {
                    asset.Set(x, y, true, type, color);
                    EditorUtility.SetDirty(asset);
                });
            }
        }

        menu.ShowAsContext();
    }
}
#endif