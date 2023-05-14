using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIVariables : MonoBehaviour
{

    [SerializeField] SimulationManager sm;
    [SerializeField] Text output;

    private float reward;
    private float stepCount;

    private bool hasSpottedGoal;
    private bool hasEnteredGoal;
    private bool hasBeenWithinBounds;

    private bool isSpottingGoal;
    private bool isEnteringGoal;
    private bool isWithinBounds;
    private bool isOffroad;

    private float goalDistance;
    private float angleDifference;

    private void UpdateFields() {

        reward = sm.carAgent.GetCumulativeReward();
        stepCount = sm.carAgent.StepCount;

        hasSpottedGoal = sm.hasSpottedGoal;
        hasEnteredGoal = sm.hasEnteredGoal;
        hasBeenWithinBounds = sm.hasBeenWithinBounds;

        isSpottingGoal = sm.IsSpottingGoal();
        isEnteringGoal = sm.isEnteringGoal;
        isWithinBounds = sm.IsWithinBounds();
        isOffroad = sm.isOffroad;

        goalDistance = sm.GetGoalDistance();
        angleDifference = sm.GetGoalAngleDifference();
    }

    private void OutputString() {

        StringBuilder sb = new StringBuilder();

        sb.AppendLineFormat($"Reward: {reward:0.00}");
        sb.AppendLineFormat($"StepCount: {stepCount}\n");

        sb.AppendLineFormat($"hasSpottedGoal: {hasSpottedGoal}");
        sb.AppendLineFormat($"hasEnteredGoal: {hasEnteredGoal}");
        sb.AppendLineFormat($"hasBeenWithinBounds: {hasBeenWithinBounds}\n");

        sb.AppendLineFormat($"IsSpottingGoal(): {isSpottingGoal}");
        sb.AppendLineFormat($"isEnteringGoal: {isEnteringGoal}");
        sb.AppendLineFormat($"IsWithinBounds(): {isWithinBounds}");
        sb.AppendLineFormat($"isOffroad: {isOffroad}\n");

        sb.AppendLineFormat($"GetGoalDistance(): {goalDistance:0.00}");
        sb.AppendLineFormat($"GetGoalAngleDifference(): {angleDifference:0.00}");

        output.text = sb.ToString();
    }

    // Update is called once per frame
    private void Update()
    {
        UpdateFields();
        OutputString();
    }
}
