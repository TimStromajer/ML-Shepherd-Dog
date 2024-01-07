using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;

public class DogSystemLimitedMultiple : ComponentSystem
{
    Unity.Mathematics.Random random;

    protected override void OnStartRunning()
    {
        random = new Unity.Mathematics.Random(1);
        // spawnDogs();
    }

    protected override void OnUpdate()
    {
        GameManagerComponent GM = GetSingleton<GameManagerComponent>();

        float deltaTime = Time.DeltaTime;

        // update all dogs
        Entities.ForEach((ref DogComponentLimitedMultiple dogComponent, ref Translation dogTranslation, ref Rotation rotation) => {
            // get visible sheep
            List<float3> visibleSheep = getVisibleSheep(180, dogTranslation.Value, rotation.Value);

            // dog direction
            float3 direction;
            
            // if no sheep visible -> only rotate
            if (visibleSheep.Count <= 0)
            {
                direction = math.mul(rotation.Value, new float3(0f, 0f, 1f));
                rotation.Value = Quaternion.LookRotation(Vector3.RotateTowards(direction, Vector3.Cross(direction, Vector3.up), dogComponent.dogRotationSpeed * deltaTime, 0.0f));
                return;
            }

            // calculate GCM
            Vector3 gcm = calculateGCM(visibleSheep);

            // calculate f(N) - how far sheep can be from gcm
            float fN = GM.ra * Mathf.Pow(visibleSheep.Count, 2f / 3f);

            // check if any sheep not in f(N) = ra_N^2/3 from GCM
            // Vector3? furthestSheepPos = allInGCM(visibleSheep, gcm, fN);
            Vector3? furthestSheepPos = allInGCMAngle(visibleSheep, gcm, fN, GM.goal);

            // do collect
            Vector3 goingDirection;
            if (furthestSheepPos.HasValue)
            {
                // get desired location
                Vector3 directionGCMSheep = Vector3.Normalize((Vector3)furthestSheepPos - gcm);
                Vector3 desiredPosition = (Vector3)furthestSheepPos + directionGCMSheep * GM.Pc;
                Debug.DrawLine(dogTranslation.Value, desiredPosition, Color.black);
                Debug.DrawLine(dogTranslation.Value, gcm, Color.magenta);

                // check if some other dog is closer to that sheep
                float myDist = Vector3.Distance(desiredPosition, dogTranslation.Value);
                bool closer = false;
                Entities.ForEach((ref DogComponentLimitedMultiple dogComponent, ref Translation dog2Translation, ref Rotation rotation) => {
                    if (Vector3.Distance(desiredPosition, dog2Translation.Value) < myDist)
                    {
                        closer = true;
                    }
                });

                // if some other dog closer, go around the gcm and check for other out of gcm sheep
                if (closer)
                {
                    // get desired location on the other side of gcm
                    float Pd = GM.ra * Mathf.Sqrt(visibleSheep.Count);
                    Vector3 directionDogGCM = Vector3.Normalize(gcm - (Vector3)dogTranslation.Value);

                    Vector3 perpendicularLeft = Vector3.Cross(directionDogGCM, Vector3.up).normalized;
                    Vector3 perpendicularRight = -perpendicularLeft;
                    if (Vector3.Angle(perpendicularLeft, Vector3.Normalize(GM.goal - gcm)) < Vector3.Angle(perpendicularRight, Vector3.Normalize(GM.goal - gcm)))
                    {
                        desiredPosition = gcm + (directionDogGCM + perpendicularRight * 3f) * Pd;
                    }
                    else
                    {
                        desiredPosition = gcm + (directionDogGCM + perpendicularLeft * 3f) * Pd;
                    }

                    goingDirection = Vector3.Normalize(desiredPosition - (Vector3)dogTranslation.Value);
                    Debug.DrawLine(dogTranslation.Value, desiredPosition, Color.white);
                    Debug.DrawLine(dogTranslation.Value, gcm, Color.red);
                }
                // else if this dog is the closes, go to that location
                else
                {
                    goingDirection = Vector3.Normalize(desiredPosition - (Vector3)dogTranslation.Value);
                }
            }
            // do herd
            else
            {
                // get desired location
                float Pd = GM.ra * Mathf.Sqrt(visibleSheep.Count);
                Vector3 directionGCMGoal = Vector3.Normalize(gcm - GM.goal);
                Vector3 desiredPosition = gcm + directionGCMGoal * Pd;
                goingDirection = Vector3.Normalize(desiredPosition - (Vector3)dogTranslation.Value);
            }

            // distort rotation by gcm - so it goes around the sheep
            Vector3 finalDirection = Vector3.Normalize(Vector3.Normalize((Vector3)dogTranslation.Value - gcm) * 0.5f + goingDirection);
            Debug.DrawLine(dogTranslation.Value, (Vector3)dogTranslation.Value + finalDirection, Color.green);

            // rotate shepherd
            direction = math.mul(rotation.Value, new float3(0f, 0f, 1f));
            rotation.Value = Quaternion.LookRotation(Vector3.RotateTowards(direction, finalDirection, dogComponent.dogRotationSpeed * deltaTime, 0.0f));

            // add ratio to shepherd speed according to distance to closest sheep
            Vector3 sheepPos = closestSheep(visibleSheep, dogTranslation.Value);
            float dist = Vector3.Distance(dogTranslation.Value, sheepPos);
            float distRatio = (dist - GM.minDistToSheep) / (GM.maxDistToSheep - GM.minDistToSheep);

            // move shepherd
            dogTranslation.Value += direction * deltaTime * GM.dogSpeed * distRatio;

            // reset y position to 0
            dogTranslation.Value.y = 0;

            // TODO: change animation

        });
    }

    public Vector3 calculateGCM(List<float3> lst)
    {
        Vector3 gcm_ = new Vector3(0, 0, 0);
        foreach (float3 s in lst)
        {
            gcm_ += (Vector3)s;
        }

        gcm_ /= lst.Count;

        return gcm_;
    }

    // check if all objects in lst are dist around gcm or if not they are not in the way of gcm, otherwise return furthest object
    public Vector3? allInGCMAngle(List<float3> lst, Vector3 gcm_, float dist, Vector3 goal_)
    {
        float furthestDist = 0;
        Vector3? furthestPoint = null;
        foreach (float3 s in lst)
        {
            // get distances and angle
            float distSheepGCM = Vector3.Distance(s, gcm_);
            float distGoalGCM = Vector3.Distance(goal_, gcm_);
            float distGoalSheep = Vector3.Distance(goal_, s);
            Vector3 goalSheepVec = Vector3.Normalize((Vector3)s - goal_);
            Vector3 gcmSheepVec = Vector3.Normalize((Vector3)s - gcm_);
            float angle = Vector3.Angle(goalSheepVec, gcmSheepVec);

            if (distSheepGCM > dist && distSheepGCM > furthestDist && (angle > 35 || distGoalSheep > distGoalGCM))
            {
                furthestDist = distSheepGCM;
                furthestPoint = s;
            }
        }

        return furthestPoint;
    }

    // check if all objects in lst are dist around gcm or return furthest object
    public Vector3? allInGCM(List<float3> lst, Vector3 gcm_, float dist)
    {
        float furthestDist = 0;
        Vector3? furthestPoint = null;
        foreach (float3 s in lst)
        {
            float fromGCM = Vector3.Distance(s, gcm_);
            if (fromGCM > dist && fromGCM > furthestDist)
            {
                furthestDist = fromGCM;
                furthestPoint = s;
            }
        }

        return furthestPoint;
    }

    // returns position of closest object in lst
    Vector3 closestSheep(List<float3> lst, float3 dogPosition)
    {
        if (lst.Count == 0)
        {
            Debug.LogError("ERROR: no elements in closestSheep lst");
        }

        Vector3 closest = new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
        float dist = Mathf.Infinity;
        foreach (float3 s in lst)
        {
            float currentDist = Vector3.Distance(dogPosition, s);
            if (currentDist < dist)
            {
                dist = currentDist;
                closest = s;
            }
        }

        return closest;
    }

    private List<float3> getVisibleSheep(int viewAngle, float3 dogPosition, quaternion dogRotation)
    {
        GameManagerComponent GM = GetSingleton<GameManagerComponent>();

        // dog direction
        float3 dogDirection = math.mul(dogRotation, new float3(0f, 0f, 1f));

        // get the number of all sheep entities
        EntityQuery query = GetEntityQuery(
            ComponentType.ReadOnly<SheepComponent>()
        );
        int sheepCount = query.CalculateEntityCount();

        // create list
        List<float3> visibleSheep = new List<float3>(sheepCount);

        // create list
        List<float3> closeSheep = new List<float3>(sheepCount);

        // objects in view array - length equal to viewAngle
        float[] viewDeg = new float[viewAngle];

        // add sheep position to the list
        Entities.ForEach((ref Rotation rotation, ref SheepComponent sheepComponent, ref Translation sheepTranslation) =>
        {
            closeSheep.Add(sheepTranslation.Value);
        });

        // sort list
        closeSheep.Sort((p1, p2) => {
            float d1 = Vector3.Distance(dogPosition, p1);
            float d2 = Vector3.Distance(dogPosition, p2);
            if (d1 < d2)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        });

        // decide for each sheep if visible
        foreach (float3 s in closeSheep)
        {
            bool sheepVisible = false;

            // calculate amount of angle the sheep represents in dogs view, *2 is used later
            float sheepAngle = Mathf.Atan((GM.sheepLength / 2) / Vector3.Distance(dogPosition, s));
            // from rad to deg
            sheepAngle = sheepAngle * 180 / Mathf.PI;

            // calculate angle between sheep and dog
            float angle = Vector3.SignedAngle(dogDirection, s - dogPosition, Vector3.up);

            // get sheep start and stop angle
            int startAngle = (int)Mathf.Round(angle - sheepAngle);
            int stopAngle = (int)Mathf.Round(angle + sheepAngle);

            // if sheep to far
            if (startAngle == stopAngle)
            {

            }
            // else if sheep outside of viewAngle
            else if (Mathf.Abs(startAngle) > viewAngle / 2 || Mathf.Abs(stopAngle) > viewAngle / 2)
            {
                
            }
            else
            {
                // check every degree if nothing is occluding it
                for (int i = startAngle; i < stopAngle; i++)
                {
                    // if atleast one degree is still empty -> sheep visible
                    if (viewDeg[90 + i] == 0)
                    {
                        sheepVisible = true;
                        viewDeg[90 + i] = 1;
                    }
                }

                // add visible sheep to the list
                if (sheepVisible)
                {
                    visibleSheep.Add(s);
                }
            }
        }

        return visibleSheep;
    }

    void spawnDogs()
    {
        GameManagerComponent gameManagerComponent = GetSingleton<GameManagerComponent>();

        int amount = gameManagerComponent.dogNumber;

        float3[] spawned = new float3[amount];

        int i = 0;
        while (i < amount)
        {
            float3 position = new float3(random.NextFloat(gameManagerComponent.minSpawnXd, gameManagerComponent.maxSpawnXd), .0f, random.NextFloat(gameManagerComponent.minSpawnZd, gameManagerComponent.maxSpawnZd));

            // check if some object already on this position
            bool coliding = false;
            for (int j = 0; j < i; j++)
            {
                if (math.abs(position[0] - spawned[j][0]) + math.abs(position[2] - spawned[j][2]) <= gameManagerComponent.dogLength)
                {
                    coliding = true;
                }
            }

            // if no colliding -> add new dog
            if (!coliding)
            {
                Entity dog = EntityManager.Instantiate(gameManagerComponent.dogPrefab);

                // set position
                EntityManager.SetComponentData(dog,
                    new Translation { Value = position });

                // set random rotation
                EntityManager.SetComponentData(dog,
                    new Rotation { Value = Quaternion.AngleAxis(random.NextFloat(0, 360), Vector3.up) });

                i += 1;
            }
        }
    }
}
