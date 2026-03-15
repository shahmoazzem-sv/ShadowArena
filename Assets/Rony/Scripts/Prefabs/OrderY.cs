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
        if (_spriteRenderer != null)
        {
            // We measure how far down we are from the top of the level (_maxYPosition).
            // This way, the result is always positive. 
            // As Y decreases (moves down), difference grows bigger -> sorting order goes higher (topper).
            float yDifference = _maxYPosition - transform.position.y;
            
            int calculatedOrder = _minOrder + Mathf.RoundToInt(yDifference * _precision);
            
            // Force it to never be minus and strictly minimum of 5.
            _spriteRenderer.sortingOrder = Mathf.Max(_minOrder, calculatedOrder);
        }
    }
}
