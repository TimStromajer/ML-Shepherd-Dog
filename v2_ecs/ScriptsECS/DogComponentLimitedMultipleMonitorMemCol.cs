using Unity.Entities;
using System.Collections.Generic;
using UnityEngine;

[GenerateAuthoringComponent]
public struct DogComponentLimitedMultipleMonitorMemCol : IComponentData
{
    public float dogRotationSpeed;
    public Enums.dogState state;
    public Vector3 goingDirection;
    public Vector3 destinationPosition;

    public Entity chosenSheepCollect;
}
