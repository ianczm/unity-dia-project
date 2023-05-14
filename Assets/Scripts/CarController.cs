using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController : MonoBehaviour {

    private float steeringInput;
    private float throttleInput;
    private bool brakingInput;

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
        ApplyTorque();
        ApplySteering();
        TransformAllWheels();
    }

    public void FeedInput(float steeringInput, float throttleInput, bool brakingInput) {
        this.steeringInput = steeringInput;
        this.throttleInput = throttleInput;
        this.brakingInput = brakingInput;
    }

    private void ApplyTorque() {

        // Front Wheel Drive
        frontLeftWheel.motorTorque = throttleInput * torqueMultiplier;
        frontRightWheel.motorTorque = throttleInput * torqueMultiplier;

        currentBrakeForce = brakingInput ? brakeForce : 0f;
        ApplyBrakingForce();

    }

    private void ApplyBrakingForce() {

        // Brake on all wheels
        frontLeftWheel.brakeTorque = currentBrakeForce;
        frontRightWheel.brakeTorque = currentBrakeForce;
        backLeftWheel.brakeTorque = currentBrakeForce;
        backRightWheel.brakeTorque = currentBrakeForce;

    }

    private void ApplySteering() {
        currentSteeringAngle = steeringInput * maxSteeringAngle;

        // Steer only with front wheels
        frontLeftWheel.steerAngle = currentSteeringAngle;
        frontRightWheel.steerAngle = currentSteeringAngle;
    }

    private void TransformAllWheels() {

        // Visuals
        TransformWheel(frontLeftWheel, frontLeftWheelTransform);
        TransformWheel(frontRightWheel, frontRightWheelTransform);
        TransformWheel(backLeftWheel, backLeftWheelTransform);
        TransformWheel(backRightWheel, backRightWheelTransform);

    }

    private void TransformWheel(WheelCollider collider, Transform transform) {
        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        transform.position = position;
        transform.rotation = rotation;
    }

    public event OnCollisionEnterEvent CollisionEnterEvent;
    public delegate void OnCollisionEnterEvent(Collider collider);

    private void OnCollisionEnter(Collision collision) {
        CollisionEnterEvent?.Invoke(collision.collider);
    }

    private void OnTriggerEnter(Collider collider) {
        CollisionEnterEvent?.Invoke(collider);
    }

    public event OnCollisionStayEvent CollisionStayEvent;
    public delegate void OnCollisionStayEvent(Collider collider);

    private void OnCollisionStay(Collision collision) {
        CollisionStayEvent?.Invoke(collision.collider);
    }

    private void OnTriggerStay(Collider collider) {
        CollisionStayEvent?.Invoke(collider);
    }

    public event OnCollisionExitEvent CollisionExitEvent;
    public delegate void OnCollisionExitEvent(Collider collider);

    private void OnCollisionExit(Collision collision) {
        CollisionExitEvent?.Invoke(collision.collider);
    }

    private void OnTriggerExit(Collider collider) {
        CollisionExitEvent?.Invoke(collider);
    }
}
