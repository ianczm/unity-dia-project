using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController : MonoBehaviour {

    private float horizontalInput;
    private float verticalInput;
    private bool isBraking;

    [Header("Handling")]
    [SerializeField] private float torqueMultiplier;
    [SerializeField] private float brakeForce;
    [SerializeField] private float maxSteeringAngle;

    private float currentBrakeForce;
    private float currentSteeringAngle;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeftWheel;
    [SerializeField] private WheelCollider frontRightWheel;
    [SerializeField] private WheelCollider backLeftWheel;
    [SerializeField] private WheelCollider backRightWheel;

    [Header("Wheel Transforms")]
    [SerializeField] private Transform frontLeftWheelTransform;
    [SerializeField] private Transform frontRightWheelTransform;
    [SerializeField] private Transform backLeftWheelTransform;
    [SerializeField] private Transform backRightWheelTransform;

    private void FixedUpdate() {
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateAllWheels();
    }

    private void GetInput() {
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");
        isBraking = Input.GetKey(KeyCode.Space);
    }

    private void HandleMotor() {

        // Front Wheel Drive
        frontLeftWheel.motorTorque = verticalInput * torqueMultiplier;
        frontRightWheel.motorTorque = verticalInput * torqueMultiplier;

        currentBrakeForce = isBraking ? brakeForce : 0f;
        ApplyBrakingForce();

    }

    private void ApplyBrakingForce() {

        // Brake on all wheels
        frontLeftWheel.brakeTorque = currentBrakeForce;
        frontRightWheel.brakeTorque = currentBrakeForce;
        backLeftWheel.brakeTorque = currentBrakeForce;
        backRightWheel.brakeTorque = currentBrakeForce;

    }

    private void HandleSteering() {
        currentSteeringAngle = horizontalInput * maxSteeringAngle;

        // Steer only with front wheels
        frontLeftWheel.steerAngle = currentSteeringAngle;
        frontRightWheel.steerAngle = currentSteeringAngle;
    }

    private void UpdateAllWheels() {

        // Visuals
        UpdateWheel(frontLeftWheel, frontLeftWheelTransform);
        UpdateWheel(frontRightWheel, frontRightWheelTransform);
        UpdateWheel(backLeftWheel, backLeftWheelTransform);
        UpdateWheel(backRightWheel, backRightWheelTransform);

    }

    private void UpdateWheel(WheelCollider collider, Transform transform) {
        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        transform.position = position;
        transform.rotation = rotation;
    }
}
