using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarFollowCam : MonoBehaviour {

    [Header("Object")]
    [SerializeField] private Transform car;

    [Header("Camera")]
    [SerializeField] private Camera followCam;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private Quaternion rotationOffset;

    [Header("Follow Behaviour")]
    [SerializeField] private float maxTranslateSpeed;
    [SerializeField] private float translateTime;
    [SerializeField] private float rotationSpeed;

    void FixedUpdate()
    {
        HandleTranslation();
        HandleRotation();
    }

    private void HandleRotation() {
        Vector3 direction = car.position - transform.position;
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, rotation, rotationSpeed * Time.deltaTime);
    }

    private void HandleTranslation() {
        Vector3 targetPosition = car.TransformPoint(positionOffset);
        Vector3 currentVelocity = Vector3.zero;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, translateTime, maxTranslateSpeed);
    }
}
