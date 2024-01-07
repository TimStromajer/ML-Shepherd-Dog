using System;
using Unity.Entities;
using UnityEngine;

[GenerateAuthoringComponent]
public struct SheepComponentLimitedMem : IComponentData
{
    [Header("Settings")]
    public float sheepRotationSpeed;
    public float sheepObstacleDist;
    public float obstacleVectStrength;
    public float sheepLength;
    public int sheepVisionAngle;
    public float sheepHearDist;

    public Enums.State state;

    [Header("Timers")]
    // how often calculate new state
    public float stateCalcTimerWalk;
    public float stateCalcTime;

    // how often change rotation in walk
    public float rotationCalcTimerWalk;
    public float rotationCalcTime;

    // how often change rotation in run
    public float rotationCalcTimerRun;
    public float rotationCalcRun;

    [HideInInspector]
    // last calculated rotation
    public Quaternion desiredRotation;
}
