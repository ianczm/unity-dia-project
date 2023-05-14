using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParkingLots : MonoBehaviour
{

    [SerializeField] private ParkingGoal goal;
    private CarParked[] cars;

    private void Awake() {
        cars = GetComponentsInChildren<CarParked>();
    }

    public void ResetParkingLots() {
        goal.ResetPositionAndRotation();
        foreach (CarParked car in cars) {
            car.ResetPositionAndRotation();
        }
    }
}
