using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParkingGoal : MonoBehaviour
{
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startScale;

    // Start is called before the first frame update
    void Start() {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startScale = transform.localScale;
    }

    public void ResetPositionAndRotation() {
        transform.position = startPosition;
        transform.rotation = startRotation;
    }

    public void SetPositionAndRotation(Vector3 position, Quaternion rotation) {
        startPosition = position;
        startRotation = rotation;
    }

    public void SetScale(float width, float height) {
        transform.localScale = new Vector3(width, startScale.y, height);
    }

    public void ResetScale() {
        transform.localScale = startScale;
    }
}
