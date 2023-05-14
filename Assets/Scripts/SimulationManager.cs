using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;
using UnityEngine;
using static Unity.MLAgents.Sensors.RayPerceptionOutput;

public class SimulationManager : MonoBehaviour {

    [Header("Configuration")]
    
    [SerializeField] private int maxSteps = 5000;
    [SerializeField] private int validParkingDuration = 5; // how long in valid parking before episode ends
    [SerializeField] private bool useHeuristics = false;

    // =====================================================

    [Header("Simulation")]

    [SerializeField] GameObject carAgentObj;
    [SerializeField] BoxCollider carCollider;
    private Vector3 carStartPosition;
    private CarAgent carAgent;
    private CarController carController;
    private Rigidbody carRigidbody;
    private RayPerceptionSensor[] carRays;
    private RayPerceptionSensorComponent3D[] carRayComponent;
    private BehaviorParameters behaviorParameters;

    [SerializeField] GameObject roadObj;
    [SerializeField] MeshCollider spawnRegion;

    [SerializeField] GameObject parkingLotsObj;
    [SerializeField] GameObject parkingGoalObj;

    // =====================================================

    // assign rewards and penalties
    // reward based on entering goal area, alignment of car with axis

    [Header("Rewards")]

    [SerializeField] private float spottedGoalReward = 2.5f; // once for spotting the goal
    [HideInInspector] public bool hasSpottedGoal;

    [SerializeField] private float enteredGoalReward = 10f; // once for entering the goal (might not be fully in)
    [HideInInspector] public bool hasEnteredGoal;
    [HideInInspector] public bool isEnteringGoal;

    [SerializeField] private float maxWithinBoundsReward = 5f; // based on proportion of car within goal bounds (for now only awarded at the end)
    [HideInInspector] public bool hasBeenWithinBounds;

    [SerializeField] private float maxAlignmentReward = 2.5f; // any direction, as long as it is perfectly parallel to parking axis (for now only awarded at the end)

    // =====================================================

    // penalize for driving offroad, even more penalty for hitting other cars / edges
    // small penalty for each second passing

    [Header("Penalties")]
    
    [SerializeField] private float maxTimePenalty = 1f; // given per unit time regardless, encourages faster completion

    [SerializeField] private float maxOffroadPenalty = 2.5f; // given per unit time any part of the car is offroad
    [HideInInspector] public bool isOffroad;

    [SerializeField] private float collisionPenalty = 10f; // given for each hit

    // start and end episodes, manage random spawn and goal location
    // car should be rotated sometimes

    // UI and Materials =====================================================

    [Header("UI and Materials")]

    [SerializeField] private Material neutral;
    [SerializeField] private Material success;
    [SerializeField] private Material failure;
    [SerializeField] private GameObject resultObject;

    // Enums =====================================================

    private static string PARKING_GOAL = "ParkingGoal";
    private static string PARKED_CAR = "ParkedCar";
    private static string ROAD = "Road";
    private static string OFFROAD = "OffRoad";
    private static string EDGE = "Edge";

    // Subscriptions =====================================================

    private void SubscribeToAgentEvents() {
        carAgent.EpisodeBeginEvent += OnEpisodeBeginEvent;
        carAgent.AfterActionReceivedEvent += OnAfterActionReceivedEvent;

        carController.CollisionEnterEvent += OnCollisionEnterEvent;
        carController.CollisionStayEvent += OnCollisionStayEvent;
        carController.CollisionExitEvent += OnCollisionExitEvent;
    }

    private void UnsubscribeToAgentEvents() {
        // agent should not be destroyed before training is over, so no need to unsubscribe each episode
        carAgent.EpisodeBeginEvent -= OnEpisodeBeginEvent;
        carAgent.AfterActionReceivedEvent -= OnAfterActionReceivedEvent;

        carController.CollisionEnterEvent -= OnCollisionEnterEvent;
        carController.CollisionStayEvent -= OnCollisionStayEvent;
        carController.CollisionExitEvent -= OnCollisionExitEvent;
    }

    // Initialisation =====================================================

    private void Awake() {
        // populating components
        carAgent = carAgentObj.GetComponent<CarAgent>();
        carController = carAgentObj.GetComponent<CarController>();
        carStartPosition = carAgentObj.transform.position;
        carRigidbody = carAgentObj.GetComponent<Rigidbody>();
        carRayComponent = carAgentObj.GetComponents<RayPerceptionSensorComponent3D>();
        carRays = carRayComponent.Select(rayComponent => rayComponent.RaySensor).ToArray();
        behaviorParameters = carAgentObj.GetComponent<BehaviorParameters>();

        // configuration
        carAgent.MaxStep = 0; // let it run indefinitely, check stepcount in this script instead
        behaviorParameters.BehaviorType = useHeuristics ? BehaviorType.HeuristicOnly : BehaviorType.Default;

        // agent events
        SubscribeToAgentEvents();

        // UI and Results
        SetResultMaterial(neutral);
    }

    // Episode Management =====================================================

    private void ResetFlags() {
        hasSpottedGoal = false;
        hasEnteredGoal = false;
        hasBeenWithinBounds = false;
        isEnteringGoal = false;
        isOffroad = false;
    }

    private void ResetCarsRotation() {
        // Todo: Fix!
    }

    private void SetupCarsPosition() {
        // Todo: Fix!
    }

    private void SetupCars() {
        // do not teardown, just reset all rotations, reposition
        int goalPositionIndex = Random.Range(0, parkingLotsObj.transform.childCount - 1);
        ResetCarsRotation();
        SetupCarsPosition();
    }

    private void ResetSimulation() {
        SetupCars();
        Quaternion rotation = Quaternion.identity;
        rotation.eulerAngles = new Vector3(0, Random.Range(0, 360), 0);
        carAgentObj.transform.SetLocalPositionAndRotation(GetCarStartPosition(), rotation);
    }

    private void OnEpisodeBeginEvent() {
        Debug.Log("Begin Episode");
        ResetFlags();
        ResetSimulation();
    }

    // executed each agent step
    private void OnAfterActionReceivedEvent() {

        // time-bound penalties

        if (isOffroad) {
            Debug.Log("Offroad Penalty");
            carAgent.AddReward(-maxOffroadPenalty / maxSteps);
        }

        carAgent.AddReward(-maxTimePenalty / maxSteps);

    }

    private void FixedUpdate() {
        // spotted goal (event driven instead?)
        if (!hasSpottedGoal && IsSpottingGoal()) {
            hasSpottedGoal = true;
            Debug.Log("Goal Spot Reward");
            carAgent.AddReward(spottedGoalReward);
        }

        if ((IsWithinBounds() && CurrentValidParkingDuration() > validParkingDuration)) {
            Debug.Log("Ending Episode on Success");
            EndEpisodeSuccess();
        } else if (carAgent.StepCount >= maxSteps) {
            Debug.Log("Ending Episode on Failure");
            EndEpisodeSuccess();
        }
    }

    private void AssignFinalRewards() {
        carAgent.AddReward(CalculateGoalDistanceReward());
        carAgent.AddReward(CalculateAlignmentReward());
    }

    private void EndEpisodeFailure() {
        carAgent.EndEpisode();
        SetResultMaterial(failure);
    }

    private void EndEpisodeSuccess() {
        Debug.Log("Assigning Final Rewards");
        AssignFinalRewards();
        carAgent.EndEpisode();
        SetResultMaterial(success);
    }

    // Reward Calculation =====================================================

    private float CalculateGoalDistanceReward() {
        float reward = maxWithinBoundsReward - GetGoalDistance();
        return reward > 0 ? reward : 0;
    }

    private float CalculateAlignmentReward() {
        float angleDifference = GetGoalAngleDifference();
        float reward = Mathf.Cos(angleDifference) * maxAlignmentReward;
        return Mathf.Abs(reward);
    }

    // Game Calculations =====================================================

    private Vector3 GetCarStartPosition() {
        Bounds bounds = spawnRegion.bounds;
        float carHeight = carStartPosition.y;
        float x = Random.Range(bounds.min.x, bounds.max.x);
        float z = Random.Range(bounds.min.z, bounds.max.z);
        return new Vector3(x, carHeight, z);
    }

    public float GetGoalDistance() {
        Vector3 carCenter = carCollider.bounds.center;
        Vector3 goalCenter = parkingGoalObj.GetComponent<MeshCollider>().bounds.center;
        return CalculateHorizontalDistance(carCenter, goalCenter);
    }

    public float GetGoalAngleDifference() {
        Vector3 carDirection = carAgentObj.transform.forward;
        Vector3 goalDirection = parkingGoalObj.transform.forward;
        return CalculateHorizontalAngleDelta(carDirection, goalDirection);
    }

    public bool IsWithinBounds() {
        Bounds carBounds = carCollider.bounds;
        Bounds goalBounds = parkingGoalObj.GetComponent<MeshCollider>().bounds;
        if (goalBounds.min.x < carBounds.min.x
            && goalBounds.max.x > carBounds.max.x
            && goalBounds.min.z < carBounds.min.z
            && goalBounds.max.z > carBounds.max.z
            ) {
            return true;
        } else {
            return false;
        }
    }

    // Todo: Fix!
    private float CurrentValidParkingDuration() {
        return validParkingDuration + 5f;
    }

    private void SetResultMaterial(Material mat) {
        resultObject.GetComponent<Renderer>().material = mat;
    }

    // Utility Calculations =====================================================

    private float CalculateHorizontalAngleDelta(Vector3 a, Vector3 b) {
        float angleA = Mathf.Atan2(a.x, a.z);
        float angleB = Mathf.Atan2(b.x, b.z);
        return angleA - angleB;
    }

    private float CalculateHorizontalDistance(Vector3 a, Vector3 b) {
        return Vector2.Distance(
            new Vector2(a.x, a.z),
            new Vector2(b.x, b.z)
        );
    }

    // Collision Detection =====================================================

    public bool IsSpottingGoal() {
        foreach (RayPerceptionSensor ray in carRays) {
            foreach (RayOutput rayOutput in ray.RayPerceptionOutput.RayOutputs) {
                if (rayOutput.HitGameObject.tag == PARKING_GOAL) {
                    return true;
                }
            }
        }
        return false;
    }

    private void OnCollisionEnterEvent(Collider collider) {
        string tag = collider.tag;

        if (tag == PARKING_GOAL) {
            if (!hasEnteredGoal) {
                hasEnteredGoal = true;
                isEnteringGoal = true;
                isOffroad = false;
                Debug.Log("Enter Goal Reward");
                carAgent.AddReward(enteredGoalReward);
            }
        }
        
        if (tag == PARKED_CAR || tag == EDGE) {
            Debug.Log("Collision Penalty");
            carAgent.AddReward(-collisionPenalty);
        }

        if (tag == ROAD) {
            Debug.Log("Entered Road");
            isOffroad = false;
        }
    }

    private void OnCollisionStayEvent(Collider collider) {
        string tag = collider.tag;

        if (tag == PARKING_GOAL) {
            isEnteringGoal = true;
            Debug.Log("Currently Entering Goal");
            if (!hasBeenWithinBounds && IsWithinBounds()) {
                hasBeenWithinBounds = true;
                Debug.Log("Entered Bounds");
            }
        }
    }

    private void OnCollisionExitEvent(Collider collider) {
        string tag = collider.tag;

        if (tag == PARKING_GOAL) {
            isEnteringGoal = false;
            isOffroad = true;
            Debug.Log("Left Goal");
        }

        if (tag == ROAD) {
            Debug.Log("Exited Road");
            isOffroad = true;
        }
    }

    // Decommissioning =====================================================

    private void OnDisable() {
        UnsubscribeToAgentEvents();
    }

}
