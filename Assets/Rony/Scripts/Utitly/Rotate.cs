using UnityEngine;

public class Rotate : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] float rotationSpeed = 180f; // degrees per second

    [Header("Scale")]
    [SerializeField] float scaleAmount = 0.2f; // + = scale up, - = scale down
    [SerializeField] float scaleSpeed = 2f;

    Vector3 originalScale;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        originalScale = transform.localScale;
    }

    // Update is called once per frame
    void Update()
    {
        KeepRotating();
        KeepScaling();
    }

    void KeepRotating()
    {
        // Rotate around Z axis for 2D
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    void KeepScaling()
    {
        // Ping-pong value between 0 and 1
        float t = Mathf.PingPong(Time.time * scaleSpeed, 1f);

        // Target scale based on positive or negative scaleAmount
        Vector3 targetScale = originalScale * (1f + scaleAmount);

        // Lerp between original and target scale
        transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
    }

}
