using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarParked : MonoBehaviour
{

    private Vector3 startPosition;
    private Quaternion startRotation;

    // Start is called before the first frame update
    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public void ResetPositionAndRotation() {
        transform.position = startPosition;
        transform.rotation = startRotation;
    }

    public void SetPositionAndRotation(Vector3 position, Quaternion rotation) {
        startPosition = position;
        startRotation = rotation;
    }
}
