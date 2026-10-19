using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class OrderY : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    
    [SerializeField] private int _minOrder = 5;
    [SerializeField] private float _precision = 100f;
    
    // An assumed maximum highest Y point in your game. 
    // This allows us to make all sorting orders positive!
    [SerializeField] private float _maxYPosition = 50f;

    void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (_spriteRenderer == null) return;

        bool isFlipped = GameManager.Instance != null && GameManager.Instance.IsBoardFlipped;

        int calculatedOrder;

        if (!isFlipped)
        {
            // Normal perspective (White at bottom):
            // Lower world-Y → further down screen → higher sort order (draws on top of pieces above it).
            float yDifference = _maxYPosition - transform.position.y;
            calculatedOrder = _minOrder + Mathf.RoundToInt(yDifference * _precision);
        }
        else
        {
            // Flipped perspective (Black at bottom, camera rotated 180°):
            // Lower world-Y is now visually HIGHER on screen, so we invert:
            // Higher world-Y → further down screen → higher sort order.
            float yDifference = transform.position.y + _maxYPosition;
            calculatedOrder = _minOrder + Mathf.RoundToInt(yDifference * _precision);
        }

        // Never go below the minimum order.
        _spriteRenderer.sortingOrder = Mathf.Max(_minOrder, calculatedOrder);
    }
}
