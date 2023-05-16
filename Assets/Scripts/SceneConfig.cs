using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Barracuda;
using Unity.MLAgents.Demonstrations;
using Unity.MLAgents.Policies;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SceneConfig : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CarFollowCam carCam;
    [SerializeField] private UIVariables statusDisplay;

    [Header("Views")]
    [SerializeField] private Transform heuristicCar;
    [SerializeField] private SimulationManager heuristicSm;
    [SerializeField] private Transform inferenceCar;
    [SerializeField] private SimulationManager inferenceSm;

    [Header("Toggles")]
    [SerializeField] private bool useHeuristics;
    [SerializeField] private bool enableTraining;

    [Header("Record Demos")]
    [SerializeField] private bool recordDemo;
    [SerializeField] private DemonstrationRecorder demoRecorder;
    [SerializeField] private string demoDirectory;
    [SerializeField] private string demoName;

    [Header("Simulation")]
    [SerializeField] private GameObject simulations;
    [SerializeField] private GameObject tests;
    [SerializeField] private NNModel model;

    // Start is called before the first frame update
    void Awake()
    {
        setUseHeuristics(useHeuristics);
        simulations.SetActive(!useHeuristics);
        tests.SetActive(useHeuristics);

        demoRecorder.DemonstrationDirectory = demoDirectory;
        demoRecorder.DemonstrationName = demoName;
        demoRecorder.Record = useHeuristics ? recordDemo : false;

        if (useHeuristics) {
            carCam.car = heuristicCar;
            statusDisplay.sm = heuristicSm;

        } else {
            // inference
            carCam.car = inferenceCar;
            statusDisplay.sm = inferenceSm;

            if (!enableTraining) {
                BehaviorParameters[] behaviours = simulations.GetComponentsInChildren<BehaviorParameters>();
                foreach (BehaviorParameters behaviour in behaviours) {
                    behaviour.Model = model;
                }
            }
        }
    }

    private void setUseHeuristics(bool flag) {
        SimulationManager[] simulationManagers = tests.GetComponentsInChildren<SimulationManager>();
        foreach (SimulationManager manager in simulationManagers) {
            manager.useHeuristics = flag;
        }
    }
}
