using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIVariables : MonoBehaviour
{

    [SerializeField] public SimulationManager sm;
    [SerializeField] public Text output;

    private float reward;
    private float lastEpisodeReward;
    private float stepCount;
    private float parkingTimer;

    private bool hasSpottedGoal;
    private bool hasEnteredGoal;
    private bool hasBeenWithinBounds;

    private bool isSpottingGoal;
    private bool isEnteringGoal;
    private bool isWithinBounds;
    private bool isOffroad;

    private float goalDistance;
    private float goalPrecisionReward;
    private float proximityReward;

    private float angleDifference;
    private float alignmentReward;
    private float stayInGoalReward;

    private float velocity;
    private float velocityReward;

    private void UpdateFields() {

        reward = sm.carAgent.GetCumulativeReward();
        lastEpisodeReward = sm.lastEpisodeReward;
        stepCount = sm.carAgent.StepCount;
        parkingTimer = sm.validParkingTimer;

        hasSpottedGoal = sm.hasSpottedGoal;
        hasEnteredGoal = sm.hasEnteredGoal;
        hasBeenWithinBounds = sm.hasBeenWithinBounds;

        isSpottingGoal = sm.IsSpottingGoal();
        isEnteringGoal = sm.isEnteringGoal;
        isWithinBounds = sm.IsWithinBounds();
        isOffroad = sm.isOffroad;

        goalDistance = sm.GetGoalDistance();
        goalPrecisionReward = sm.CalculatePrecisionReward();
        proximityReward = sm.CalculateProximityReward();

        float angleInDegrees = Mathf.Rad2Deg * sm.GetGoalAngleDifference();
        angleDifference = getAcuteDegrees(angleInDegrees);
        alignmentReward = sm.CalculateAlignmentReward();

        stayInGoalReward = sm.stayInGoalReward;

        velocity = sm.GetCarVelocity();
        velocityReward = sm.CalculateVelocityReward();
    }

    private float getAcuteDegrees(float deg) { 
        float absDeg = Mathf.Abs(deg);
        return absDeg > 90 ? 180 - absDeg : absDeg;
    }

    private void OutputString() {

        StringBuilder sb = new StringBuilder();

        sb.AppendLineFormat($"Reward: {reward:0.00}");
        sb.AppendLineFormat($"Last Episode Reward: {lastEpisodeReward:0.00}");
        sb.AppendLineFormat($"StepCount: {stepCount}");
        sb.AppendLineFormat($"ValidParkingTimer: {parkingTimer:0.00}\n");

        sb.AppendLineFormat($"hasSpottedGoal: {hasSpottedGoal}");
        sb.AppendLineFormat($"hasEnteredGoal: {hasEnteredGoal}");
        sb.AppendLineFormat($"hasBeenWithinBounds: {hasBeenWithinBounds}\n");

        sb.AppendLineFormat($"IsSpottingGoal(): {isSpottingGoal}");
        sb.AppendLineFormat($"isEnteringGoal: {isEnteringGoal}");
        sb.AppendLineFormat($"IsWithinBounds(): {isWithinBounds}");
        sb.AppendLineFormat($"isOffroad: {isOffroad}\n");

        sb.AppendLineFormat($"GetGoalDistance(): {goalDistance:0.00}");
        sb.AppendLineFormat($"GetGoalAngleDifference(): {angleDifference:0.00}\n");

        sb.AppendLineFormat($"AlignmentReward(): {alignmentReward:0.00}");
        sb.AppendLineFormat($"GoalDistanceReward(): {goalPrecisionReward:0.00}\n");

        sb.AppendLineFormat($"ProximityReward(): {proximityReward:0.0000}\n");
        sb.AppendLineFormat($"stayInGoalReward: {stayInGoalReward:0.0000}");

        //sb.AppendLineFormat($"Velocity: {velocity:0.00}");
        //sb.AppendLineFormat($"VelocityReward(): {velocityReward:0.0000}");

        output.text = sb.ToString();
    }

    // Update is called once per frame
    private void Update()
    {
        UpdateFields();
        OutputString();
    }
}
