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

    // Configuration =====================================================

    [Header("Configuration")]
    
    [SerializeField] private int maxSteps = 5000; // length of each episode in steps
    [SerializeField] private int requiredParkingDuration = 5; // how long in valid parking before episode ends
    [SerializeField] private float maxAllowedDistance = 20f; // episode ends on failure if exceeded
    [SerializeField] public bool useHeuristics = false; // if true, forces the game to receive input from the player, use for testing

    // Development =====================================================

    // these are not used in the project yet
    [Header("Under Development")]

    [SerializeField] private bool randomSpawnCars = false;
    [SerializeField] private float spaceBetweenCars = 10f;
    [SerializeField] private int numSlots = 12; // including goal

    // Simulation =====================================================

    // these are objects to be connected through the Unity Editor to allow
    // them to be managed from this central script
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

    // Agent Spawn =====================================================

    // determines how the agent should be spawned
    [Header("Agent Spawn")]
    [SerializeField] private bool randomAgentSpawn = false;
    [SerializeField] private bool randomDirection = false;
    [SerializeField] private bool flipRoadDirection = false;
    [SerializeField] private bool randomXAxis = false;
    [SerializeField] private bool randomZAxis = false;

    // Public Accessors =====================================================

    // for displaying values in the UI for debugging purposes
    [HideInInspector] public float validParkingTimer;
    [HideInInspector] public float lastEpisodeReward = 0f;
    [HideInInspector] public float stayInGoalReward = 0f;

    // Sensors =====================================================

    // for the game to check which object is currently beign detected by the agent
    // to assign goal spotting rewards
    private RayPerceptionSensor[] carRays;
    private RayPerceptionSensorComponent3D[] carRayComponent;

    // Game State =====================================================

    // Game State Bools
    [HideInInspector] public bool hasSpottedGoal;
    [HideInInspector] public bool hasEnteredGoal;
    [HideInInspector] public bool hasBeenWithinBounds;
    [HideInInspector] public bool isEnteringGoal;

    // Rewards =====================================================

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
    [SerializeField] private bool enableVelocity = false; // experimental, do not enable, used to encourage the car to move with steady speed
    [SerializeField] private float maxVelocityReward = 1f; // travelling at threshold velocity the entire game will result in this
    [SerializeField] private float thresholdVelocity = 3f; // max speed after which reward is maxed

    private float previousDistance; // TODO: Remove redundant field

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

    // for colouring the ground of the environment depending on whether the agent parked successfully or not
    [Header("UI and Materials")]

    [SerializeField] private Material neutral;
    [SerializeField] private Material success;
    [SerializeField] private Material failure;
    [SerializeField] private GameObject resultObject;

    // Enums =====================================================

    // tags
    private static string PARKING_GOAL = "ParkingGoal";
    private static string PARKED_CAR = "ParkedCar";
    private static string ROAD = "Road";
    private static string OFFROAD = "OffRoad";
    private static string EDGE = "Edge";

    // Subscriptions =====================================================
    // to allow the game to listen to collision events between the car and any object

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

    // link all required components to allow real-time communication
    private void Awake() {
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
        carAgent.MaxStep = 0; // let it run indefinitely, manage stepcount in this script instead
        behaviorParameters.BehaviorType = useHeuristics ? BehaviorType.HeuristicOnly : BehaviorType.Default;

        // agent events
        SubscribeToAgentEvents();

        // UI and Results
        SetResultMaterial(neutral);
    }

    // Episode Management =====================================================

    // reset game state, gets called every time a new episode begins
    private void OnEpisodeBeginEvent() {
        ResetFlags();
        ResetSimulation();
    }
    
    // reset reward state
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

    // reset object positions
    private void ResetSimulation() {

        // handles placement of parked cars
        SetupCars();

        // handles placment of agent's car
        carRigidbody.velocity = Vector3.zero;
        if (randomAgentSpawn) {
            Quaternion finalRotation = GetAgentRotation();
            Vector3 finalPosition = GetAgentRandomPosition();
            carAgentObj.transform.SetPositionAndRotation(finalPosition, finalRotation);
        } else {
            carAgentObj.transform.SetPositionAndRotation(carStartPosition, carStartRotation);
        }
        
    }

    private void SetupCars() {
        // do not teardown, just reset all rotations, reposition
        int goalPositionIndex = Random.Range(0, parkingLotsObj.transform.childCount - 1);

        if (randomSpawnCars) {
            // not implemented
            SetupCarsPosition(); // spawn cars according to predefined rules
        } else {
            // default
            ResetParkingLots(); // reset the cars to how they were placed by hand in the editor
        }
    }

    // TODO: Not implemented
    // Should be able to specify spacing between cars and how many to spawn
    // handles randomly placing the lot and the distance between cars
    private void SetupCarsPosition() {
        Debug.LogError("Not implemented!");
    }

    // Resets the position of all cars in the parking lot
    private void ResetParkingLots() {
        parkingLots.ResetParkingLots();
    }

    // Time-Bound Management =====================================================
    // Functions here are executed every step of the episode

    // TODO: Move into FixedUpdate method
    // executed each agent step
    private void OnAfterActionReceivedEvent() {
        // time-bound penalties
        if (isOffroad) {
            carAgent.AddReward(-maxOffroadPenalty / maxSteps);
        }
        carAgent.AddReward(-maxTimePenalty / maxSteps);
    }

    // executed each agent step
    private void FixedUpdate() {

        // encourage movement
        // experimental, do not enable yet
        if (enableVelocity) {
            RewardVelocity();
        }

        // encourage proximity to goal
        // assign reward each step for how close the agent is to the goal
        if (enableProximity) {
            RewardProximity();
        }

        // assign a reward once when the goal is spotted
        if (enableSpottedGoal) {
            RewardSpottedGoal();
        }

        // assign trickle rewards for each step spent inside the bounds of the goal
        if (enableStayingInGoal) {
            RewardStayingInGoal();
        }

        // begin the timer for how long the car needs to stay in the goal bounds before it
        // successfully completes the episode
        if (IsWithinBounds()) {
            validParkingTimer += Time.fixedDeltaTime;
        } else {
            validParkingTimer = 0;
        }

        // if the timer is done, the agent wins
        if (validParkingTimer > requiredParkingDuration) {
            EndEpisodeSuccess(); // positional and angular position rewards are assigned
        } else if (carAgent.StepCount >= maxSteps || GetGoalDistance() > maxAllowedDistance) {
            // no penalties or rewards are assigned, unless the car hasexceeded the max allowable distance
            EndEpisodeFailure();
        }

        // TODO: Remove redundant field
        previousDistance = GetGoalDistance();
    }

    // Reward and Penalty Logic =====================================================
    // these functions check game state every step to assign rewards

    // experimental
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

    // End-of-Episode Logic =====================================================
    // these functions run based on final game state to assign rewards and penalties

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

    // Precision Reward Functions =====================================================

    // Positional Precision Function
    private float RProx(float rthresh, float proxmax, float r) {
        // -Rproxmax * (x/rthresh - 1)^3
        float reward = -proxmax * Mathf.Pow((r / rthresh) - 1, 3);
        return Mathf.Clamp(reward, 0, proxmax);
    }

    // Angular Precision Function
    private float RAlign(float alignmax, float angleDifference) {
        float reward = Mathf.Cos(angleDifference) * alignmax;
        return Mathf.Abs(reward);
    }

    // Reward Calculations =====================================================
    // Customised calls to the precision reward functions with predefined parameters for scaling
    // used to abstract heavy math from main gaim logic

    // experimental
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

    // Game State Calculations =====================================================
    // handles velocity, rotation, position calculations to check and update game-state

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
    // functions here are called whenever a collision happens in game
    // checks are made to determine which game-state to transition to

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
