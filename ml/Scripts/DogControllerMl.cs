using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class DogControllerMl : Agent
{
    // Dog Animator Controller
    public Animator anim;

    public LearnManagerScript GM;
    private Rigidbody m_Rigidbody;

    private Vector3 gcm;
    private List<GameObject> visibleSheep;

    private Vector3 previousGcm;
    private Vector3 previousDogPos;
    private float previousSummedDist;

    public override void Initialize()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        GM.EpisodeBegin();

        // add visible sheep
        visibleSheep = new List<GameObject>();
        foreach (GameObject s in GM.sheepList)
        {
            visibleSheep.Add(s);
        }

        previousGcm = calculateGCM(visibleSheep);
        previousDogPos = transform.position;
        previousSummedDist = Mathf.Infinity;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float move = actions.ContinuousActions[0];
        float rotate = actions.ContinuousActions[1];

        // move shepherd
        transform.Translate(transform.forward * Time.deltaTime * GM.dogSpeed * move, Space.World);

        // rotate shepherd
        transform.Rotate(transform.up * GM.dogRotationSpeed * Time.deltaTime * rotate);

        // calculate GCM
        gcm = calculateGCM(visibleSheep);

        // if gcm close to any fence -> minus reward
        foreach (GameObject f in GM.fenceList)
        {
            if (Vector3.Distance(gcm, f.transform.position) <= 5)
            {
                AddReward(-0.05f);
            }
        }

        // if summed distance of sheep to gcm is smaller -> add reward, else minus reward
        float summedDist = 0;
        foreach(GameObject s in GM.sheepList)
        {
            summedDist += Vector3.Distance(s.transform.position, gcm);
        }

        if (summedDist <= previousSummedDist)
        {
            AddReward(0.05f);
        } else
        {
            AddReward(-0.02f);
        }

        // if gcm closer to goal then before -> add positive reward else add negative reward
        if (Vector3.Distance(gcm, GM.goal.transform.position) < Vector3.Distance(previousGcm, GM.goal.transform.position))
        {
            AddReward(0.1f);
        } else if (Vector3.Distance(gcm, GM.goal.transform.position) > Vector3.Distance(previousGcm, GM.goal.transform.position))
        {
            AddReward(-0.05f);
        } else
        {
            AddReward(-0.01f);
        }

        // if dog closer to gcm add reward, if same reward is 0, if less negative reward
        if (Vector3.Distance(gcm, transform.position) < Vector3.Distance(previousGcm, previousDogPos))
        {
            AddReward(0.02f);
        } else if (Vector3.Distance(gcm, transform.position) > Vector3.Distance(previousGcm, previousDogPos))
        {
            AddReward(-0.01f);
        }

        // end episode if gcm close to goal
        if (Vector3.Distance(GM.goal.transform.position, gcm) < 5)
        {
            AddReward(10f);
            EndEpisode();
        }

        // update previous GCM
        previousGcm = gcm;

        // update previous dog position
        previousDogPos = transform.position;

        // update previous summed distance
        previousSummedDist = summedDist;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // add dog position (3)
        sensor.AddObservation(this.transform.position);

        // add dog direction (3)
        sensor.AddObservation(this.transform.forward);

        // add goal position (3)
        sensor.AddObservation(GM.goal.transform.position);

        // add fence position small x, big x, small z, big z
        sensor.AddObservation(GM.fenceList[120].transform.position.x);
        sensor.AddObservation(GM.fenceList[80].transform.position.x);
        sensor.AddObservation(GM.fenceList[0].transform.position.z);
        sensor.AddObservation(GM.fenceList[40].transform.position.z);

        // TODO: ena ovca, verjetnost izbire 1/d
        // TODO: vedno 7 najbližjih vidnih

        // add sheep positons (x * 3)
        foreach (GameObject s in visibleSheep)
        {
            sensor.AddObservation(s.transform.position);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        actionsOut.ContinuousActions.Array[0] = v;
        actionsOut.ContinuousActions.Array[1] = h;

        float space = Input.GetAxis("Jump");
        if (space != 0)
        {
            print(this.transform.position + ", " + this.transform.forward + ", " + GM.goal.transform.position + ", " + visibleSheep[0].transform.position);
        }
    }

    // randomly choose sheep from lst with probability 1/d from object pos
    public GameObject randomSheep(List<GameObject> lst, Vector3 pos)
    {

        // calculate distances and find max distance
        List<float> distances = new List<float>();
        float maxDist = 0;
        foreach (GameObject s in lst)
        {
            float dist = Vector3.Distance(pos, s.transform.position);
            distances.Add(dist);
            if (dist > maxDist)
            {
                maxDist = dist;
            }
        }

        // convert to probability
        float sumOfAll = 0;
        for (int i = 0; i < distances.Count; i++)
        {
            // avoid zero values
            if (distances[i] == maxDist)
            {
                distances[i] = 1;
            } else
            {
                distances[i] = maxDist - distances[i];
            }
            
            sumOfAll += distances[i];
        }

        // normalize
        for (int i = 0; i < distances.Count; i++)
        {
            distances[i] = distances[i] / sumOfAll;
        }

        // get random value from 0 to 1
        float rand = Random.Range(0, 1);

        // decide which value
        float summer = 0;
        GameObject chosenSheep = new GameObject();
        for (int i = 0; i < distances.Count; i++)
        {
            summer += distances[i];
            if (rand < summer)
            {
                chosenSheep = lst[i];
                break;
            }
        }

        return chosenSheep;
    }

    // calculate GCM of the objects in the list
    public Vector3 calculateGCM(List<GameObject> lst)
    {
        Vector3 gcm_ = new Vector3(0, 0, 0);
        foreach (GameObject s in lst)
        {
            gcm_ += s.transform.position;
        }

        gcm_ /= lst.Count;

        return gcm_;
    }

}
