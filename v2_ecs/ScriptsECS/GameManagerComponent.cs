using Unity.Entities;
using UnityEngine;

[GenerateAuthoringComponent]
public struct GameManagerComponent : IComponentData
{
    [Header("Sheep settings")]
    public Entity sheepPrefab;
    public int sheepNumber;
    public float sheepLength;

    public float minSpawnX;
    public float maxSpawnX;
    public float minSpawnZ;
    public float maxSpawnZ;

    [Header("Dog settings")]
    public Entity dogPrefab;
    public int dogNumber;
    public float dogLength;
    public float dogSpeed;
    public float collectAngle;

    public float minDistToSheep;
    public float maxDistToSheep;

    public float minSpawnXd;
    public float maxSpawnXd;
    public float minSpawnZd;
    public float maxSpawnZd;

    [Header("Goal")]
    public Vector3 goal;

    [Header("Strombon")]
    public float rs;
    public float ra;
    public float ro_a;
    public float ro_s;
    public float c;
    public float Pc;

    [Header("Ginelli")]
    public float v1;
    public float v2;
    public float alpha;
    public float eta;
    public float delta;
    public float tau0_1;
    public float tau1_0;
    public float dR;
    public float dS;
}
