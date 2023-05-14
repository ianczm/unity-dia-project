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
    [SerializeField] private int requiredParkingDuration = 5; // how long in valid parking before episode ends
    [SerializeField] private bool useHeuristics = false;

    // =====================================================

    [Header("Under Development")]

    [SerializeField] private bool randomSpawnCars = false;
    [SerializeField] private float spaceBetweenCars = 10f;
    [SerializeField] private int numSlots = 12; // including goal

    // =====================================================

    [Header("Simulation")]

    [SerializeField] private GameObject carAgentObj;
    [SerializeField] private BoxCollider carCollider;
    private Vector3 carStartPosition;
    private Quaternion carStartRotation;
    [HideInInspector] public CarAgent carAgent;
    private Rigidbody carRigidbody;
    private CarController carController;
    private BehaviorParameters behaviorParameters;

    [SerializeField] private GameObject roadObj;
    [SerializeField] private MeshCollider spawnRegion;
    [SerializeField] private bool randomAgentSpawn;

    [SerializeField] private GameObject parkingLotsObj;
    [SerializeField] private GameObject parkingGoalObj;
    private ParkingLots parkingLots;

    [HideInInspector] public float validParkingTimer;
    [HideInInspector] public float lastEpisodeReward = 0f;

    // Sensors =====================================================

    private RayPerceptionSensor[] carRays;
    private RayPerceptionSensorComponent3D[] carRayComponent;

    // =====================================================

    // assign rewards and penalties
    // reward based on entering goal area, alignment of car with axis

    [Header("Rewards")]
    [SerializeField] private float spottedGoalReward = 2.5f; // once for spotting the goal
    [HideInInspector] public bool hasSpottedGoal;

    [SerializeField] private float enteredGoalReward = 10f; // once for entering the goal (might not be fully in)
    [HideInInspector] public bool hasEnteredGoal;
    [HideInInspector] public bool isEnteringGoal;

    [SerializeField] private float maxWithinBoundsReward = 5f; // based on distance of car center to parking center
    [HideInInspector] public bool hasBeenWithinBounds;

    [SerializeField] private float maxAlignmentReward = 2.5f; // based on how parallel the car is

    [HideInInspector] public float velocityReward = 0f;
    [SerializeField] private float maxVelocityReward = 2.5f;
    [SerializeField] private float velocityMultiplier = 0.0002f; // greater here means slower is encouraged velocity

    // =====================================================

    // penalize for driving offroad, even more penalty for hitting other cars / edges
    // small penalty for each second passing

    [Header("Penalties")]
    
    [SerializeField] private float maxTimePenalty = 1f; // given per unit time regardless, encourages faster completion

    [SerializeField] private float maxOffroadPenalty = 2.5f; // given per unit time if the entire car is not on the road or goal
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
        carRigidbody = carAgentObj.GetComponent<Rigidbody>();
        carStartPosition = carAgentObj.transform.position;
        carStartRotation = carAgentObj.transform.rotation;

        carRayComponent = carAgentObj.GetComponentsInChildren<RayPerceptionSensorComponent3D>();
        carRays = carRayComponent.Select(rayComponent => rayComponent.RaySensor).ToArray();

        parkingLots = parkingLotsObj.GetComponent<ParkingLots>();

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
        validParkingTimer = 0;
        velocityReward = 0;
    }

    private void ResetParkingLots() {
        parkingLots.ResetParkingLots();
    }

    // Should be able to specify spacing between cars and how many to spawn
    // handles randomly placing the lot
    private void SetupCarsPosition() {
        Debug.LogError("Not implemented!");
    }

    private void SetupCars() {
        // do not teardown, just reset all rotations, reposition
        int goalPositionIndex = Random.Range(0, parkingLotsObj.transform.childCount - 1);

        if (randomSpawnCars) {
            SetupCarsPosition();
        } else {
            ResetParkingLots();
        }
    }

    private void ResetSimulation() {
        SetupCars();

        if (randomAgentSpawn) {
            Quaternion rotation = Quaternion.identity;
            rotation.eulerAngles = new Vector3(0, Random.Range(-45, 45), 0);
            carAgentObj.transform.SetPositionAndRotation(GetCarStartPosition(), rotation);
        } else {
            carAgentObj.transform.SetPositionAndRotation(carStartPosition, carStartRotation);
        }
        
    }

    private void OnEpisodeBeginEvent() {
        ResetFlags();
        ResetSimulation();
    }

    // executed each agent step
    private void OnAfterActionReceivedEvent() {

        // time-bound penalties

        if (isOffroad) {
            carAgent.AddReward(-maxOffroadPenalty / maxSteps);
        }

        carAgent.AddReward(-maxTimePenalty / maxSteps);

    }

    private void FixedUpdate() {

        // encourage movement
        float velocity = carRigidbody.velocity.magnitude;
        velocityReward = Mathf.Min(maxVelocityReward / maxSteps, velocity * velocityMultiplier);
        carAgent.AddReward(velocityReward);

        // spotted goal (event driven instead?)
        if (!hasSpottedGoal && IsSpottingGoal()) {
            hasSpottedGoal = true;
            Debug.Log("Reward: Spotted Goal");
            carAgent.AddReward(spottedGoalReward);
        }

        if (IsWithinBounds()) {
            validParkingTimer += Time.fixedDeltaTime;
        } else {
            validParkingTimer = 0;
        }

        if (validParkingTimer > requiredParkingDuration) {
            EndEpisodeSuccess();
        } else if (carAgent.StepCount >= maxSteps) {
            EndEpisodeFailure();
        }
    }

    private void AssignFinalRewards() {
        Debug.Log("Reward: Goal Distance");
        carAgent.AddReward(CalculateGoalDistanceReward());
        Debug.Log("Reward: Alignment");
        carAgent.AddReward(CalculateAlignmentReward());
    }

    private void EndEpisodeFailure() {
        lastEpisodeReward = carAgent.GetCumulativeReward();
        carAgent.EndEpisode();
        SetResultMaterial(failure);
    }

    private void EndEpisodeSuccess() {
        AssignFinalRewards();
        lastEpisodeReward = carAgent.GetCumulativeReward();
        carAgent.EndEpisode();
        SetResultMaterial(success);
    }

    // Reward Calculation =====================================================

    public float CalculateGoalDistanceReward() {
        float reward = maxWithinBoundsReward - GetGoalDistance();
        return reward > 0 ? reward : 0;
    }

    public float CalculateAlignmentReward() {
        float angleDifference = GetGoalAngleDifference();
        float reward = Mathf.Cos(angleDifference) * maxAlignmentReward;
        return Mathf.Abs(reward);
    }

    // Game Calculations =====================================================

    public float GetCarVelocity() {
        return carRigidbody.velocity.magnitude;
    }

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
        //foreach (RayPerceptionSensor ray in carRays) {
        //    foreach (RayOutput rayOutput in ray.RayPerceptionOutput.RayOutputs) {
        //        if (rayOutput.HasHit && rayOutput.HitGameObject.tag == PARKING_GOAL) {
        //            Debug.Log(rayOutput.HitFraction);
        //            return true;
        //        }
        //    }
        //}

        // Unity Bug: If you select the car to view sensor rays in the editor, rays will not be updated

        for (int i = 0; i < carRays.Length; i++) {
            RayPerceptionSensor ray = carRays[i];
            for (int j = 0; j < ray.RayPerceptionOutput.RayOutputs.Length; j++) {
                RayOutput rayOutput = ray.RayPerceptionOutput.RayOutputs[j];
                if (rayOutput.HasHit && rayOutput.HitGameObject.tag == PARKING_GOAL) {
                    // Debug.Log($"{i}, {j}");
                    // Debug.Log(rayOutput.HitFraction);
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
                Debug.Log("Reward: Enter Goal");
                carAgent.AddReward(enteredGoalReward);
            }
        }
        
        if (tag == PARKED_CAR || tag == EDGE) {
            Debug.Log("Penalty: Collision");
            carAgent.AddReward(-collisionPenalty);
        }

        if (tag == ROAD) {
            isOffroad = false;
        }
    }

    private void OnCollisionStayEvent(Collider collider) {
        string tag = collider.tag;

        if (tag == PARKING_GOAL) {
            isEnteringGoal = true;
            isOffroad = false;
            if (!hasBeenWithinBounds && IsWithinBounds()) {
                hasBeenWithinBounds = true;
            }
        }

        if (tag == ROAD) {
            isOffroad = false;
        }
    }

    private void OnCollisionExitEvent(Collider collider) {
        string tag = collider.tag;

        if (tag == PARKING_GOAL) {
            isEnteringGoal = false;
            isOffroad = true;
        }

        if (tag == ROAD) {
            isOffroad = true;
        }
    }

    // Decommissioning =====================================================

    private void OnDisable() {
        UnsubscribeToAgentEvents();
    }

}
