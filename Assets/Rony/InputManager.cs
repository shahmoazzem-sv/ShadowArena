using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
public class InputManager : MonoBehaviour, ChessInput.IDragAndDropActions
{

    public static InputManager Instance { get; private set; }

    private ChessInput input;

    [Header("State")]
    public bool IsPressed { get; private set; }
    public bool IsDragging { get; private set; }



    [Header("World Positions")]
    public Vector3 ClickWorldPosition { get; private set; }
    public Vector3 DragStartWorldPosition { get; private set; }
    public Vector3 DragWorldPosition { get; private set; }
    public Vector3 DropWorldPosition { get; private set; }

    [Header("Events")]
    public UnityEvent<Vector3> OnClick;
    public UnityEvent<Vector3> OnDragStart;
    public UnityEvent<Vector3> OnDragging;
    public UnityEvent<Vector3> OnDrop;

    Camera mainCamera;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        mainCamera = Camera.main;

        input = new ChessInput();
        input.DragAndDrop.AddCallbacks(this);
    }

    void OnEnable()
    {
        input.Enable();
    }

    void OnDisable()
    {
        input.Disable();
    }

    void OnDestroy()
    {
        if (input != null)
        {
            input.DragAndDrop.RemoveCallbacks(this);
            input.Dispose();
            input = null;
        }
    }

    // =======================
    // INPUT CALLBACKS
    // =======================
    public void OnTouchPoint(InputAction.CallbackContext context)
    {
        //just start means touched and start dragging
        if (context.started)
        {
            IsPressed = true;
            IsDragging = false;

            ClickWorldPosition = ScreenToWorld(GetScreenPosition());
            DragStartWorldPosition = ClickWorldPosition;

            OnClick?.Invoke(ClickWorldPosition); // fire once
            OnDragStart?.Invoke(DragStartWorldPosition);
        }

        // touch canceled is also means drop if was dragging
        if (context.canceled)
        {
            // Finger/mouse released
            IsPressed = false;
            IsDragging = false;

            DropWorldPosition = ScreenToWorld(GetScreenPosition());
            OnDrop?.Invoke(DropWorldPosition);
        }
    }

    // Called continuously as pointer/mouse moves
    public void OnTouchPosition(InputAction.CallbackContext context)
    {
        if (!IsPressed) return;

        DragWorldPosition = ScreenToWorld(context.ReadValue<Vector2>());

        // Small movement threshold to start dragging
        if (!IsDragging &&
            Vector3.Distance(DragWorldPosition, DragStartWorldPosition) > 0.05f)
        {
            IsDragging = true;
        }

        if (IsDragging)
        {
            OnDragging?.Invoke(DragWorldPosition);
        }
    }

    // =======================
    // HELPERS
    // =======================

    Vector2 GetScreenPosition()
    {
        return input.DragAndDrop.TouchPosition.ReadValue<Vector2>();
    }

    Vector3 ScreenToWorld(Vector2 screenPos)
    {
        Vector3 world = mainCamera.ScreenToWorldPoint(screenPos);
        world.z = 0f;
        return world;
    }
}
