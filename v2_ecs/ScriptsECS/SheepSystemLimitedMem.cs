using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;

public class SheepSystemLimitedMem : ComponentSystem   //ComponentSystem or SystemBase?
{

    Unity.Mathematics.Random random;

    protected override void OnStartRunning()
    {
        random = new Unity.Mathematics.Random(1);
        //spawnSheep();
    }

    protected override void OnUpdate()
    {
        float deltaTime = Time.DeltaTime;

        GameManagerComponent GM = GetSingleton<GameManagerComponent>();

        Entities.ForEach((Animator anim, ref Rotation rotation, ref SheepComponentLimitedMem sheepComponent, ref Translation sheepTranslation) =>
        {
            // timer for calculating idle/walk state
            sheepComponent.stateCalcTimerWalk += deltaTime;

            // save sheep position
            float3 sheepPos = sheepTranslation.Value;

            // get visible dogs
            List<float3> dogList = getVisibleAndHeardDogs(sheepComponent.sheepVisionAngle, sheepPos, rotation.Value, sheepComponent.sheepHearDist);

            // get visible sheep
            List<float3> visibleSheep = getVisibleAndHeardSheep(sheepComponent.sheepVisionAngle, sheepPos, rotation.Value, sheepComponent.sheepHearDist);

            // check if dog is near
            bool dogClose = false;
            Entities.ForEach((ref DogComponentLimitedMultipleMonitorMemCol dogComponent, ref Translation dogTranslation) =>
            {
                if (math.distance(sheepPos, dogTranslation.Value) < GM.rs)
                {
                    dogClose = true;
                }
            });

            // Decide next state: if dog close go to run state, otherwise calculate between idle and walk
            if (dogClose)
            {
                sheepComponent.state = Enums.State.Run;

                // TODO: change animation:
                if (anim != null && anim.isActiveAndEnabled)
                {
                    anim.Play("Run");
                    anim.SetBool("IsIdle", false);
                    anim.SetBool("IsRunning", true);
                }

            }
            else
            {
                // new state (walk/idle) calculation
                if (sheepComponent.stateCalcTimerWalk > sheepComponent.stateCalcTime)
                {
                    Enums.State newState = computeNewState(sheepComponent.state, visibleSheep, sheepPos);
                    sheepComponent.state = newState;
                    sheepComponent.stateCalcTimerWalk = 0;
                }

                // TODO: change animation
                if (anim != null && anim.isActiveAndEnabled)
                {
                    anim.Play("Idle 1");
                    anim.SetBool("IsIdle", true);
                    anim.SetBool("IsRunning", false);
                }

            }

            // move the sheep according to state
            if (sheepComponent.state == Enums.State.Walk)
            {
                Quaternion desiredRotation = Walk_rotation();

                // rotate sheep
                rotation.Value = Quaternion.Lerp(rotation.Value, desiredRotation, sheepComponent.sheepRotationSpeed * deltaTime);

                // move sheep
                var direction = math.mul(rotation.Value, new float3(0f, 0f, 1f));
                sheepTranslation.Value += direction * deltaTime * GM.v1 * 3;
            }
            else if (sheepComponent.state == Enums.State.Run)
            {
                Vector3 desiredRotationVec = Run_rotation(sheepTranslation.Value, sheepComponent.sheepObstacleDist, visibleSheep);

                // rotate sheep
                //rotation.Value = Quaternion.Lerp(rotation.Value, desiredRotation, sheepComponent.sheepRotationSpeed * deltaTime);
                var direction = math.mul(rotation.Value, new float3(0f, 0f, 1f));
                rotation.Value = Quaternion.LookRotation(Vector3.RotateTowards(direction, desiredRotationVec, sheepComponent.sheepRotationSpeed * deltaTime, 0.0f));

                // TODO: check if another sheep infront of -> dont move

                // move sheep
                direction = math.mul(rotation.Value, new float3(0f, 0f, 1f));
                sheepTranslation.Value += direction * deltaTime * GM.v2;

                // force sheep to the ground
                sheepTranslation.Value = new float3(sheepTranslation.Value.x, 0, sheepTranslation.Value.z);
            }
        });
    }

    // return list of sheep in sheepList that are in viewAngle and not occluded or around heardDist
    private List<float3> getVisibleAndHeardDogs(int viewAngle, float3 sheepPosition, quaternion sheepRotation, float heardDist)
    {
        GameManagerComponent GM = GetSingleton<GameManagerComponent>();

        // sheep direction
        float3 sheepDirection = math.mul(sheepRotation, new float3(0f, 0f, 1f));

        // get the number of all dog entities
        EntityQuery query = GetEntityQuery(
            ComponentType.ReadOnly<SheepComponent>()
        );
        int dogCount = query.CalculateEntityCount();

        // create list
        List<float3> visibleDogs = new List<float3>(dogCount);

        // create list
        List<float3> closeDogs = new List<float3>(dogCount);

        // objects in view array - length equal to viewAngle
        float[] viewDeg = new float[viewAngle];

        // add dogs position to the list
        Entities.ForEach((ref Rotation rotation, ref DogComponentLimitedMultipleMonitorMemCol dogComponent, ref Translation dogTranslation) =>
        {
            closeDogs.Add(dogTranslation.Value);
        });

        // sort list
        closeDogs.Sort((p1, p2) => {
            float d1 = Vector3.Distance(sheepPosition, p1);
            float d2 = Vector3.Distance(sheepPosition, p2);
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
        foreach (float3 s in closeDogs)
        {
            bool dogVisible = false;

            // calculate amount of angle the dog represents in dogs view, *2 is used later
            float dogAngle = Mathf.Atan((GM.sheepLength / 2) / Vector3.Distance(sheepPosition, s));
            // from rad to deg
            dogAngle = dogAngle * 180 / Mathf.PI;

            // calculate angle between sheep and dog
            float angle = Vector3.SignedAngle(sheepDirection, s - sheepPosition, Vector3.up);

            // get sheep start and stop angle
            int startAngle = (int)Mathf.Round(angle - dogAngle);
            int stopAngle = (int)Mathf.Round(angle + dogAngle);

            // if sheep to far
            if (startAngle == stopAngle)
            {

            }
            // else if sheep outside of viewAngle
            else if (Mathf.Abs(startAngle) > viewAngle / 2 || Mathf.Abs(stopAngle) > viewAngle / 2)
            {
                // add dog if not visible but around heardDist
                if (Vector3.Distance(sheepPosition, s) <= heardDist)
                {
                    visibleDogs.Add(s);
                    // Debug.DrawLine(sheepPosition, s, Color.red);
                }
            }
            else
            {
                // check every degree if nothing is occluding it
                for (int i = startAngle; i < stopAngle; i++)
                {
                    // if atleast one degree is still empty -> sheep visible
                    if (viewDeg[90 + i] == 0)
                    {
                        dogVisible = true;
                        viewDeg[90 + i] = 1;
                    }
                }

                // add visible sheep to the list
                if (dogVisible)
                {
                    visibleDogs.Add(s);
                }
            }
        }

        return visibleDogs;
    }

    // return list of sheep in sheepList that are in viewAngle and not occluded or around heardDist
    private List<float3> getVisibleAndHeardSheep(int viewAngle, float3 sheepPosition, quaternion sheepRotation, float heardDist)
    {
        GameManagerComponent GM = GetSingleton<GameManagerComponent>();

        // dog direction
        float3 sheepDirection = math.mul(sheepRotation, new float3(0f, 0f, 1f));

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
        Entities.ForEach((ref Rotation rotation, ref SheepComponentLimitedMem sheepComponent, ref Translation sheepTranslation) =>
        {
            if (Vector3.Distance(sheepTranslation.Value, sheepPosition) > 0.1)
            {
                closeSheep.Add(sheepTranslation.Value);
            }
        });

        // sort list
        closeSheep.Sort((p1, p2) => {
            float d1 = Vector3.Distance(sheepPosition, p1);
            float d2 = Vector3.Distance(sheepPosition, p2);
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
            float sheepAngle = Mathf.Atan((GM.sheepLength / 2) / Vector3.Distance(sheepPosition, s));
            // from rad to deg
            sheepAngle = sheepAngle * 180 / Mathf.PI;

            // calculate angle between sheep and dog
            float angle = Vector3.SignedAngle(sheepDirection, s - sheepPosition, Vector3.up);

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
                // add sheep if not visible but around heardDist
                if (Vector3.Distance(sheepPosition, s) <= heardDist)
                {
                    visibleSheep.Add(s);
                    // Debug.DrawLine(sheepPosition, s, Color.red);
                }
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

    Vector3 Run_rotation(float3 sheepPosition, float sheepObstacleDist, List<float3> visibleSheep)
    {
        GameManagerComponent GM = GetSingleton<GameManagerComponent>();

        // get the number of all sheep entities
        EntityQuery query = GetEntityQuery(
            ComponentType.ReadOnly<SheepComponent>()
        );
        int sheepCount = query.CalculateEntityCount();

        // get sum vector from each close dog
        Vector3 dogVector = new Vector3(0, 0, 0);
        Entities.ForEach((ref DogComponentLimitedMultipleMonitorMemCol dogComponent, ref Translation dogTranslation) =>
        {
            if (math.distance(dogTranslation.Value, sheepPosition) < GM.rs / 4)
            {
                dogVector += Vector3.Normalize(sheepPosition - dogTranslation.Value);
            }
            if (math.distance(dogTranslation.Value, sheepPosition) < GM.rs)
            {
                dogVector += Vector3.Normalize(sheepPosition - dogTranslation.Value) * 0.2f;
            }
        });
        dogVector = Vector3.Normalize(dogVector);

        // get sum vector from each close sheep and gcm vector of close sheep
        Vector3 sheepVector = new Vector3(0, 0, 0);
        Vector3 gcm_ = new Vector3(0, 0, 0);
        Entities.ForEach((ref SheepComponentLimitedMem sheepComponent, ref Translation sheepTranslation, ref Rotation rotation) =>
        {
            foreach (float3 s in visibleSheep)
            {
                // sheep in visibleSheep is found as an entity
                if (Vector3.Distance(sheepTranslation.Value, s) < 0.1)
                {
                    if (math.distance(sheepTranslation.Value, sheepPosition) < GM.ra)
                    {
                        sheepVector += Vector3.Normalize(sheepPosition - sheepTranslation.Value);
                    }

                    gcm_ += (Vector3)sheepTranslation.Value;
                    break;
                }
            }

        });
        sheepVector = Vector3.Normalize(sheepVector);
        gcm_ /= visibleSheep.Count;

        Vector3 gcmVector = Vector3.Normalize(gcm_ - (Vector3)sheepPosition);
        
        // TODO: get sum vector from each close obstacle (trees and fences)
        Vector3 obstacleVect = new Vector3(0, 0, 0);
        Entities.ForEach((ref TreeComponent treeComponent, ref Translation treeTranslation) =>
        {
            if (Vector3.Distance(treeTranslation.Value, sheepPosition) < sheepObstacleDist)
            {
                obstacleVect += Vector3.Normalize(sheepPosition - treeTranslation.Value);
            }
        });
        Entities.ForEach((ref FenceComponent fenceComponent, ref Translation fenceTranslation) =>
        {
            if (Vector3.Distance(fenceTranslation.Value, sheepPosition) < sheepObstacleDist)
            {
                obstacleVect += Vector3.Normalize(sheepPosition - fenceTranslation.Value);
            }
        });
        obstacleVect = new Vector3(obstacleVect.x, 0, obstacleVect.z);
        obstacleVect = Vector3.Normalize(obstacleVect);

        // calculate final vector
        // TODO: add inertia vector and error term
        Vector3 final = dogVector * GM.ro_s + sheepVector * GM.ro_a + gcmVector * GM.c; //+ obstacleVect * GM.obstacleVectStrength;
        final = Vector3.Normalize(final);

        Vector3 desiredRotationVec = final;
        Quaternion desiredRotation = Quaternion.LookRotation(final, Vector3.up);

        return desiredRotationVec;
    }

    Quaternion Walk_rotation()
    {
        GameManagerComponent GM = GetSingleton<GameManagerComponent>();

        // get the number of all sheep entities
        EntityQuery query = GetEntityQuery(
            ComponentType.ReadOnly<SheepComponent>()
        );
        int sheepCount = query.CalculateEntityCount();

        // calculate average rotation of neighbouring sheep, can be filtered for close sheep only
        Quaternion neighbourHeading = Quaternion.identity;
        float averageWeight = 1f / sheepCount;
        Entities.ForEach((ref SheepComponentLimitedMem sheepComponent, ref Translation sheepTranslation, ref Rotation rotation) =>
        {
            Quaternion heading = rotation.Value;
            neighbourHeading *= Quaternion.Slerp(Quaternion.identity, heading, averageWeight);
        });

        // calculate desired rotation
        float psi = random.NextFloat(-GM.eta * 180, GM.eta * 180); //[-23.4, 23.4]
        neighbourHeading = Quaternion.Euler(0, neighbourHeading.eulerAngles.y + psi, 0);
        Quaternion desiredRotation = neighbourHeading;

        return desiredRotation;
    }

    Enums.State computeNewState(Enums.State state, List<float3> visibleSheep, float3 sheepPos)
    {
        GameManagerComponent GM = GetSingleton<GameManagerComponent>();

        bool stateChanged = false;

        if (state == Enums.State.Run)
        {
            int idleState = 0;
            float meanDist = 0;
            float topologicalCount = 0;

            // go through all topological sheep
            Entities.ForEach((ref SheepComponentLimitedMem sheepComponent, ref Translation sheepTranslation) =>
            {
                foreach (float3 s in visibleSheep)
                {
                    if (Vector3.Distance(s, sheepTranslation.Value) <= 0.1 && Vector3.Distance(s, sheepPos) < sheepComponent.sheepHearDist)
                    {
                        topologicalCount += 1;

                        // add mean dist
                        meanDist += Vector3.Distance(sheepPos, s);

                        if (sheepComponent.state == Enums.State.Idle)
                        {
                            idleState += 1;
                        }
                    }
                }
            });
            meanDist = meanDist / topologicalCount;

            // calculate porbability
            float prob = (1 / topologicalCount) * ((GM.dS / meanDist) * Mathf.Pow(1 + GM.alpha * idleState, GM.delta));

            state = Enums.State.Idle;
        }

        else if (state == Enums.State.Idle)
        {
            // get state of each neighbour
            int walkState = 0;
            Entities.ForEach((ref SheepComponentLimitedMem sheepComponent, ref Translation sheepTranslation) => 
            { 
                foreach (float3 s in visibleSheep)
                {
                    if (sheepComponent.state == Enums.State.Walk && Vector3.Distance(s, sheepPos) <= 0.1)
                    {
                        walkState += 1;
                        break;
                    }
                }
            });

            // calculate porbability
            float prob = (1 + GM.alpha * walkState) / GM.tau0_1;

            // potentially change state
            if (random.NextFloat(0, 1) < prob)
            {
                state = Enums.State.Walk;
                stateChanged = true;
            }
        }
        else if (state == Enums.State.Walk)
        {
            // get state of each neighbour
            int idleState = 0;
            Entities.ForEach((ref SheepComponentLimitedMem sheepComponent, ref Translation sheepTranslation) =>
            {
                foreach (float3 s in visibleSheep)
                {
                    if (sheepComponent.state == Enums.State.Idle && Vector3.Distance(s, sheepPos) <= 0.1)
                    {
                        idleState += 1;
                        break;
                    }
                }
            });

            // calculate porbability
            float prob = (1 + GM.alpha * idleState) / GM.tau1_0;

            // potentially change state
            if (random.NextFloat(0, 1) < prob)
            {
                state = Enums.State.Idle;
                stateChanged = true;
            }
        }

        if (!stateChanged && (state == Enums.State.Idle || state == Enums.State.Walk))
        {
            int runState = 0;
            float meanDist = 0;
            float topologicalCount = 0;

            // go through all topological sheep
            Entities.ForEach((ref SheepComponentLimitedMem sheepComponent, ref Translation sheepTranslation) =>
            {
                foreach (float3 s in visibleSheep)
                {
                    if (Vector3.Distance(s, sheepTranslation.Value) <= 0.1 && Vector3.Distance(s, sheepPos) < sheepComponent.sheepHearDist)
                    {
                        topologicalCount += 1;

                        // add mean dist
                        meanDist += Vector3.Distance(sheepPos, s);

                        if (sheepComponent.state == Enums.State.Run)
                        {
                            runState += 1;
                        }
                    }
                }
            });
            meanDist = meanDist / topologicalCount;

            // calculate porbability
            float prob = (1 / topologicalCount) * ((meanDist / GM.dR) * Mathf.Pow(1 + GM.alpha * runState, GM.delta));

            // potentially change state
            if (random.NextFloat(0, 1) < prob)
            {
                state = Enums.State.Run;
            }
        }

        return state;
    }

    void spawnSheep()
    {
        GameManagerComponent gameManagerComponent = GetSingleton<GameManagerComponent>();
        
        int amount = gameManagerComponent.sheepNumber;

        float3[] spawned = new float3[amount];

        int antiInfinityLoop = 0;

        int i = 0;
        while (i < amount)
        {
            float3 position = new float3(random.NextFloat(gameManagerComponent.minSpawnX, gameManagerComponent.maxSpawnX), .0f, random.NextFloat(gameManagerComponent.minSpawnZ, gameManagerComponent.maxSpawnZ));

            // check if some object already on this position
            bool coliding = false;
            for (int j = 0; j < i; j++)
            {
                if (math.abs(position[0] - spawned[j][0]) + math.abs(position[2] - spawned[j][2]) <= gameManagerComponent.sheepLength)
                {
                    coliding = true;
                }
            }

            // if no colliding -> add new sheep
            if (!coliding)
            {
                Entity sheep = EntityManager.Instantiate(gameManagerComponent.sheepPrefab);

                // set position
                EntityManager.SetComponentData(sheep,
                    new Translation { Value = position });

                // set random rotation
                EntityManager.SetComponentData(sheep,
                    new Rotation { Value = Quaternion.AngleAxis(random.NextFloat(0, 360), Vector3.up) });

                antiInfinityLoop += 1;
                i += 1;
            } else
            {
                antiInfinityLoop += 1;
            }

            if (antiInfinityLoop >= 1000)
            {
                Debug.Log("Can't find space for the sheep. Current sheep " + i);
                break;
            }
        }
    }
}
