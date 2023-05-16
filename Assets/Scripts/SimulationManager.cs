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
    [SerializeField] private float maxAllowedDistance = 20f; // episode ends on failure if exceeded
    [SerializeField] public bool useHeuristics = false;

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

    [SerializeField] private GameObject parkingLotsObj;
    [SerializeField] private GameObject parkingGoalObj;
    private ParkingLots parkingLots;

    [Header("Agent Spawn")]
    [SerializeField] private bool randomAgentSpawn = false;
    [SerializeField] private bool randomDirection = false;
    [SerializeField] private bool flipRoadDirection = false;
    [SerializeField] private bool randomXAxis = false;
    [SerializeField] private bool randomZAxis = false;

    // Public Accessors

    [HideInInspector] public float validParkingTimer;
    [HideInInspector] public float lastEpisodeReward = 0f;
    [HideInInspector] public float stayInGoalReward = 0f;

    // Sensors =====================================================

    private RayPerceptionSensor[] carRays;
    private RayPerceptionSensorComponent3D[] carRayComponent;

    // =====================================================

    // assign rewards and penalties
    // reward based on entering goal area, alignment of car with axis

    [Header("Rewards")]
    
    // Spotted Goal
    [SerializeField] private bool enableSpottedGoal = true; // once for spotting the goal
    [SerializeField] private float spottedGoalReward = 2.5f; // once for spotting the goal
    
    // Entered Goal
    [SerializeField] private float enteredGoalReward = 10f; // once for entering the goal (might not be fully in)

    // Staying in Goal
    [SerializeField] private bool enableStayingInGoal = true; // once for spotting the goal
    [SerializeField] private float maxStayingInGoalReward = 5f; // divided over time, assigned per tick

    // Parking Perfection
    [SerializeField] private float thresholdPrecision = 1f; // min distance for precision to be calculated
    [SerializeField] private float maxPrecisionReward = 5f; // based on distance of car center to parking center
    [SerializeField] private float maxAlignmentReward = 2.5f; // based on how parallel the car is

    // Proximity
    [SerializeField] private bool enableProximity = false;
    [SerializeField] private float maxProximityReward = 2.5f; // divided over time, assigned per tick
    [SerializeField] private float thresholdProximity = 10f; // proximity from goal where reward starts coming in

    // Velocity
    [SerializeField] private bool enableVelocity = false;
    [SerializeField] private float maxVelocityReward = 1f; // travelling at threshold velocity the entire game will result in this
    [SerializeField] private float thresholdVelocity = 3f; // max speed after which reward is maxed

    // Bools
    [HideInInspector] public bool hasSpottedGoal;
    [HideInInspector] public bool hasEnteredGoal;
    [HideInInspector] public bool hasBeenWithinBounds;
    [HideInInspector] public bool isEnteringGoal;

    private float previousDistance;

    // =====================================================

    // penalize for driving offroad, even more penalty for hitting other cars / edges
    // small penalty for each second passing

    [Header("Penalties")]
    
    [SerializeField] private float maxTimePenalty = 1f; // given per unit time regardless, encourages faster completion

    [SerializeField] private float maxOffroadPenalty = 2.5f; // given per unit time if the entire car is not on the road or goal
    [HideInInspector] public bool isOffroad;

    [SerializeField] private float collisionPenalty = 10f; // given for each hit

    [SerializeField] private float exceedDistancePenalty = 10f; // given if exceeded

    [SerializeField] private float leaveParkingPenalty = 2.5f;

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
        stayInGoalReward = 0;
        previousDistance = GetGoalDistance();
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

        carRigidbody.velocity = Vector3.zero;

        if (randomAgentSpawn) {
            Quaternion finalRotation = GetAgentRotation();
            Vector3 finalPosition = GetAgentRandomPosition();
            carAgentObj.transform.SetPositionAndRotation(finalPosition, finalRotation);
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

    private void RewardVelocity() {
        carAgent.AddReward(CalculateVelocityReward());
    }

    private void RewardProximity() {
        carAgent.AddReward(CalculateProximityReward());
    }

    private void RewardSpottedGoal() {
        if (!hasSpottedGoal && IsSpottingGoal()) {
            hasSpottedGoal = true;
            Debug.Log("Reward: Spotted Goal");
            carAgent.AddReward(spottedGoalReward);
        }
    }

    private void RewardStayingInGoal() {
        if (IsWithinBounds()) {
            float reward = CalculateStayInGoalReward();
            stayInGoalReward = reward;
            carAgent.AddReward(reward);
        } else {
            stayInGoalReward = 0;
        }
    }

    private void FixedUpdate() {

        // encourage movement
        if (enableVelocity) {
            RewardVelocity();
        }

        // encourage proximity to goal
        if (enableProximity) {
            RewardProximity();
        }

        if (enableSpottedGoal) {
            RewardSpottedGoal();
        }

        if (enableStayingInGoal) {
            RewardStayingInGoal();
        }

        if (IsWithinBounds()) {
            validParkingTimer += Time.fixedDeltaTime;
        } else {
            validParkingTimer = 0;
        }

        if (validParkingTimer > requiredParkingDuration) {
            EndEpisodeSuccess();
        } else if (carAgent.StepCount >= maxSteps || GetGoalDistance() > maxAllowedDistance) {
            EndEpisodeFailure();
        }

        previousDistance = GetGoalDistance();
    }

    private void AssignFinalRewards() {
        Debug.Log("Reward: Goal Distance");
        carAgent.AddReward(CalculatePrecisionReward());
        Debug.Log("Reward: Alignment");
        carAgent.AddReward(CalculateAlignmentReward());
    }

    private void AssignFinalPenalties() {
        if (GetGoalDistance() > maxAllowedDistance) {
            Debug.Log("Penalty: Exceeded Max Distance");
            carAgent.AddReward(-exceedDistancePenalty);
        }
    }

    private void EndEpisodeFailure() {
        AssignFinalPenalties();
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

    public float CalculateVelocityReward() {
        float velocity = carRigidbody.velocity.magnitude;
        float velocityMultiplier = maxVelocityReward / (thresholdVelocity * maxSteps);
        return Mathf.Min(maxVelocityReward / maxSteps, velocity * velocityMultiplier);
    }

    public float CalculateProximityReward() {
        return RProx(thresholdProximity, maxProximityReward, GetGoalDistance()) / maxSteps;
    }

    public float CalculateStayInGoalReward() {
        float proximityReward = RProx(0.5f * thresholdPrecision, 0.5f, GetGoalDistance());
        float alignmentReward = RAlign(0.5f, GetGoalAngleDifference());
        float reward = (maxStayingInGoalReward * (proximityReward + alignmentReward)) / maxSteps;
        return reward;
    }

    public float CalculatePrecisionReward() {
        return RProx(thresholdPrecision, maxPrecisionReward, GetGoalDistance());
    }

    public float CalculateAlignmentReward() {
        return RAlign(maxAlignmentReward, GetGoalAngleDifference());
    }

    private float RProx(float rthresh, float proxmax, float r) {
        // -Rproxmax * (x/rthresh - 1)^3
        float reward = -proxmax * Mathf.Pow((r / rthresh) - 1, 3);
        return Mathf.Clamp(reward, 0, proxmax);
    }

    private float RAlign(float alignmax, float angleDifference) {
        float reward = Mathf.Cos(angleDifference) * alignmax;
        return Mathf.Abs(reward);
    }

    // Game Calculations =====================================================

    private Quaternion GetAgentRotation() {
        Quaternion rotation = Quaternion.identity;
        if (randomDirection) {
            rotation.eulerAngles = new Vector3(0, Random.Range(0, 360), 0);
        } else if (flipRoadDirection) {
            int decision = Random.Range(0, 2);
            float degrees = decision == 0 ? 0 : 180;
            Debug.Log(degrees);
            rotation.eulerAngles = new Vector3(0, degrees, 0);
        } else {
            rotation = carStartRotation;
        }
        return rotation;
    }

    private Vector3 GetAgentRandomPosition() {

        Bounds bounds = spawnRegion.bounds;
        Vector3 carStart = carStartPosition;

        float carHeight = carStart.y;
        float x = randomXAxis ? Random.Range(bounds.min.x, bounds.max.x) : carStart.x;
        float z = randomZAxis ? Random.Range(bounds.min.z, bounds.max.z) : carStart.z;

        return new Vector3(x, carHeight, z);
    }

    public float GetCarVelocity() {
        return carRigidbody.velocity.magnitude;
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
            if (hasBeenWithinBounds) {
                Debug.Log("Penalty: Left Parking Area");
                carAgent.AddReward(-leaveParkingPenalty);
            }
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
