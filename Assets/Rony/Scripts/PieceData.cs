using UnityEngine;

[CreateAssetMenu(fileName = "NewPieceData", menuName = "Chess/Piece Data")]
public class PieceData : ScriptableObject
{
    [Header("White Pieces")]
    public Sprite whitePawn;
    public Sprite whiteRook;
    public Sprite whiteKnight;
    public Sprite whiteBishop;
    public Sprite whiteQueen;
    public Sprite whiteKing;

    [Header("Black Pieces")]
    public Sprite blackPawn;
    public Sprite blackRook;
    public Sprite blackKnight;
    public Sprite blackBishop;
    public Sprite blackQueen;
    public Sprite blackKing;
    // Helper method to get the right sprite based on type and color
    public Sprite GetSprite(PieceType type, PieceColor color)
    {
        if (color == PieceColor.White)
        {
            return type switch
            {
                PieceType.Pawn => whitePawn,
                PieceType.Rook => whiteRook,
                PieceType.Knight => whiteKnight,
                PieceType.Bishop => whiteBishop,
                PieceType.Queen => whiteQueen,
                PieceType.King => whiteKing,
                _ => null
            };
        }
        else
        {
            return type switch
            {
                PieceType.Pawn => blackPawn,
                PieceType.Rook => blackRook,
                PieceType.Knight => blackKnight,
                PieceType.Bishop => blackBishop,
                PieceType.Queen => blackQueen,
                PieceType.King => blackKing,
                _ => null
            };
        }
    }
}
