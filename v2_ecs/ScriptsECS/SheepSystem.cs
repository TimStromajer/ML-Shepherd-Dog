using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class SheepSystem : ComponentSystem   //ComponentSystem or SystemBase?
{

    Unity.Mathematics.Random random;

    protected override void OnStartRunning()
    {
        random = new Unity.Mathematics.Random(1);
        spawnSheep();
    }

    protected override void OnUpdate()
    {
        float deltaTime = Time.DeltaTime;

        GameManagerComponent GM = GetSingleton<GameManagerComponent>();

        Entities.ForEach((Animator anim, ref Rotation rotation, ref SheepComponent sheepComponent, ref Translation sheepTranslation) =>
        {
            // timer for calculating idle/walk state
            sheepComponent.stateCalcTimerWalk += deltaTime;

            // save sheep position
            float3 sheepPos = sheepTranslation.Value;

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
                    Enums.State newState = computeNewState(sheepComponent.state);
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
                Vector3 desiredRotationVec = Run_rotation(sheepTranslation.Value, sheepComponent.sheepObstacleDist);

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

    // check if there is another sheep infron of this one
    bool behindSheep()
    {

        return false;
    }

    Vector3 Run_rotation(float3 sheepPosition, float sheepObstacleDist)
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
            if (math.distance(dogTranslation.Value, sheepPosition) < GM.rs)
            {
                dogVector += Vector3.Normalize(sheepPosition - dogTranslation.Value);
            }
        });
        dogVector = Vector3.Normalize(dogVector);

        // get sum vector from each close sheep and gcm vector of close sheep
        Vector3 sheepVector = new Vector3(0, 0, 0);
        Vector3 gcm_ = new Vector3(0, 0, 0);
        Entities.ForEach((ref SheepComponent sheepComponent, ref Translation sheepTranslation, ref Rotation rotation) =>
        {
            if (math.distance(sheepTranslation.Value, sheepPosition) < GM.ra)
            {
                sheepVector += Vector3.Normalize(sheepPosition - sheepTranslation.Value);
            }

            gcm_ += (Vector3)sheepTranslation.Value;
        });
        sheepVector = Vector3.Normalize(sheepVector);
        gcm_ /= sheepCount;

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
        Entities.ForEach((ref SheepComponent sheepComponent, ref Translation sheepTranslation, ref Rotation rotation) =>
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

    Enums.State computeNewState(Enums.State state)
    {
        GameManagerComponent gameManagerComponent = GetSingleton<GameManagerComponent>();

        if (state == Enums.State.Run)
        {
            state = Enums.State.Idle;
        }

        if (state == Enums.State.Idle)
        {
            // get state of each neighbour
            int walkState = 0;
            Entities.ForEach((ref SheepComponent sheepComponent, ref Translation sheepTranslation) => 
            { 
                if (sheepComponent.state == Enums.State.Walk)
                {
                    walkState += 1;
                }
            });

            // calculate porbability
            float prob = (1 + gameManagerComponent.alpha * walkState) / gameManagerComponent.tau0_1;

            // potentially change state
            if (random.NextFloat(0, 1) < prob)
            {
                state = Enums.State.Walk;
            }
        }
        else if (state == Enums.State.Walk)
        {
            // get state of each neighbour
            int idleState = 0;
            Entities.ForEach((ref SheepComponent sheepComponent, ref Translation sheepTranslation) =>
            {
                if (sheepComponent.state == Enums.State.Idle)
                {
                    idleState += 1;
                }
            });

            // calculate porbability
            float prob = (1 + gameManagerComponent.alpha * idleState) / gameManagerComponent.tau1_0;

            // potentially change state
            if (random.NextFloat(0, 1) < prob)
            {
                state = Enums.State.Idle;
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
