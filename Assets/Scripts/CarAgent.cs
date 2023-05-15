using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System;

public class CarAgent : Agent {

    [Header("Driving")]
    [SerializeField] CarController carController;
    [SerializeField] Rigidbody carRigidbody;
    [SerializeField] Transform goal;

    public override void CollectObservations(VectorSensor sensor) {
        sensor.AddObservation(carRigidbody.velocity);
        sensor.AddObservation(goal.position - transform.position);
        sensor.AddObservation(GetGoalAngleDifference());
    }

    public event OnEpisodeBeginEvent EpisodeBeginEvent;
    public delegate void OnEpisodeBeginEvent();
    
    public override void OnEpisodeBegin() {
        EpisodeBeginEvent?.Invoke();
    }

    public override void Heuristic(in ActionBuffers actionsOut) {

        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

        (float steeringInput, float throttleInput, bool brakingInput) = GetControllerInput();
        continuousActions[0] = steeringInput;
        continuousActions[1] = throttleInput;
        discreteActions[0] = brakingInput ? 1 : 0;
    }

    public event OnAfterActionReceivedEvent AfterActionReceivedEvent;
    public delegate void OnAfterActionReceivedEvent();

    public override void OnActionReceived(ActionBuffers actions) {

        ActionSegment<float> continuousActions = actions.ContinuousActions;
        ActionSegment<int> discreteActions = actions.DiscreteActions;

        float steeringInput = continuousActions[0];
        float throttleInput = continuousActions[1];
        bool brakeInput = discreteActions[0] == 1 ? true : false;

        carController.FeedInput(steeringInput, throttleInput, brakeInput);

        AfterActionReceivedEvent?.Invoke();
    }

    private (float, float, bool) GetControllerInput() {
        float steeringInput = Input.GetAxis("Horizontal");
        float throttleInput = Input.GetAxis("Vertical");
        bool brakingInput = Input.GetKey(KeyCode.Space);
        return (steeringInput, throttleInput, brakingInput);
    }

    public float GetGoalAngleDifference() {
        Vector3 carDirection = transform.forward;
        Vector3 goalDirection = goal.transform.forward;
        return CalculateHorizontalAngleDelta(carDirection, goalDirection);
    }

    private float CalculateHorizontalAngleDelta(Vector3 a, Vector3 b) {
        float angleA = Mathf.Atan2(a.x, a.z);
        float angleB = Mathf.Atan2(b.x, b.z);
        return angleA - angleB;
    }
}
