using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    [Header("Simulation")]
    [SerializeField] private GameObject simulations;
    [SerializeField] private GameObject tests;

    // Start is called before the first frame update
    void Awake()
    {
        setUseHeuristics(useHeuristics);
        simulations.SetActive(!useHeuristics);
        tests.SetActive(useHeuristics);

        if (useHeuristics) {
            carCam.car = heuristicCar;
            statusDisplay.sm = heuristicSm;
        } else {
            // inference
            carCam.car = inferenceCar;
            statusDisplay.sm = inferenceSm;
        }
    }

    private void setUseHeuristics(bool flag) {
        SimulationManager[] simulationManagers = tests.GetComponentsInChildren<SimulationManager>();
        foreach (SimulationManager manager in simulationManagers) {
            manager.useHeuristics = flag;
        }
    }
}
