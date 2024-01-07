using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SheepController : MonoBehaviour
{
    private GameManager GM;
    private Rigidbody m_Rigidbody;

    public Enums.State state;
    private Quaternion desiredRotation;
    private Vector3 desiredRotationVec;

    private float headingNoise;

    private List<GameObject> dogList;
    private List<GameObject> closeSheepList;    // list of close sheep, TODO: still all sheep

    // Sheeps Animator Controller
    public Animator anim;

    // how often calculate new state
    private float stateCalcTimerWalk = 0;
    private float stateCalcTime = 3;

    // how often change rotation in walk
    private float rotationCalcTimerWalk = 0;
    private float rotationCalcTime = 1;

    // how often change rotation in run
    private float rotationCalcTimerRun = 0;
    private float rotationCalcRun = 0.1f;

    void Start()
    {
        GM = FindObjectOfType<GameManager>();
        m_Rigidbody = GetComponent<Rigidbody>();

        dogList = GM.dogList;
        closeSheepList = GM.sheepList;

        state = Enums.State.Idle;
        anim.SetBool("IsIdle", true);

        // to perform calculation at the beginning
        stateCalcTimerWalk = stateCalcTime;
        rotationCalcTimerWalk = rotationCalcTime;
    }

    void Update()
    {
        // timer for calculating idle/walk state
        stateCalcTimerWalk += Time.deltaTime;

        // check if a dog is near
        bool dogClose = false;
        foreach (GameObject s in dogList)
        {
            if (Vector3.Distance(s.transform.position, transform.position) < GM.strombom.rs)
            {
                dogClose = true;
            }
        }

        // Decide next state: if dog close go to run state, otherwise calculate between idle and walk
        if (dogClose)
        {
            state = Enums.State.Run;
            anim.SetBool("IsIdle", false);
            anim.SetBool("IsRunning", true);
        }
        else
        {
            // new state (walk/idle) calculation
            if (stateCalcTimerWalk > stateCalcTime)
            {
                computeNewState();
                stateCalcTimerWalk = 0;
            }
        }

        // move the sheep according to state
        if (state == Enums.State.Walk)
        {
            Walk();
        }
        else if(state == Enums.State.Run)
        {
            Run();
        }
    }

    void Walk()
    {
		// TODO: move to update?
        rotationCalcTimerWalk += Time.deltaTime;
		
        if (rotationCalcTimerWalk > rotationCalcTime)
        {
            // calculate average rotation of neighbouring sheep
            Quaternion neighbourHeading = Quaternion.identity;
            float averageWeight = 1f / closeSheepList.Count;
            foreach (GameObject s in closeSheepList)
            {
                Quaternion heading = s.transform.rotation;
                neighbourHeading *= Quaternion.Slerp(Quaternion.identity, heading, averageWeight);
            }

            // rotate sheep
            float psi = Random.Range(-GM.ginelli.eta * 180, GM.ginelli.eta * 180); //[-23.4, 23.4]
            neighbourHeading = Quaternion.Euler(0, neighbourHeading.eulerAngles.y + psi, 0);
            desiredRotation = neighbourHeading;

            rotationCalcTimerWalk = 0;
        }
        
		// rotate sheep
        transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, GM.sheepRotationSpeed * Time.deltaTime);

        // move sheep
        transform.Translate(transform.forward * Time.deltaTime * GM.ginelli.v1, Space.World);
    }

    void Run()
    {
		// TODO: move in update?
        rotationCalcTimerRun += Time.deltaTime;

        // if any dog is really close -> make sheep faster
        float dogVeryClose = 1;

        if (rotationCalcTimerRun > rotationCalcRun)
        {
            // get sum vector from each close dog
            Vector3 dogVector = new Vector3(0, 0, 0);
            foreach (GameObject s in dogList)
            {
                if (Vector3.Distance(s.transform.position, transform.position) < GM.strombom.rs)
                {
                    dogVector += Vector3.Normalize(transform.position - s.transform.position);

                    // check if dog very close
                    if (Vector3.Distance(s.transform.position, transform.position) <= 5)
                    {
                        dogVeryClose = 1.2f;
                    }
                }
            }
            dogVector = Vector3.Normalize(dogVector);

            // get sum vector from each close sheep and gcm vector of close sheep
            Vector3 sheepVector = new Vector3(0, 0, 0);
            Vector3 gcm_ = new Vector3(0, 0, 0);
            foreach (GameObject s in closeSheepList)
            {
                if (Vector3.Distance(s.transform.position, transform.position) < GM.strombom.ra)
                {
                    sheepVector += Vector3.Normalize(transform.position - s.transform.position);
                }

                gcm_ += s.transform.position;
            }
            sheepVector = Vector3.Normalize(sheepVector);
            gcm_ /= closeSheepList.Count;

            Vector3 gcmVector = Vector3.Normalize(gcm_ - transform.position);

            // get sum vector from each close obstacle (trees and fences)
            Vector3 obstacleVect = new Vector3(0, 0, 0);
            foreach (GameObject s in GM.treeList)
            {
                if (Vector3.Distance(s.transform.position, transform.position) < GM.sheepObstacleDist)
                {
                    obstacleVect += Vector3.Normalize(transform.position - s.transform.position);
                }
            }
            foreach (GameObject s in GM.fenceList)
            {
                if (Vector3.Distance(s.transform.position, transform.position) < GM.sheepObstacleDist)
                {
                    obstacleVect += Vector3.Normalize(transform.position - s.transform.position);
                }
            }
            obstacleVect = new Vector3(obstacleVect.x, 0, obstacleVect.z);
            obstacleVect = Vector3.Normalize(obstacleVect);

            // add ratio to sheep repulsion from shepherd according to distance
            // float distRatio = (dist - GM.minDistToSheep) / (GM.maxDistToSheep - GM.minDistToSheep);

            // calculate final vector, TODO: add inertia vector and error term
            Vector3 final = dogVector * GM.strombom.ro_s + sheepVector * GM.strombom.ro_a + gcmVector * GM.strombom.c + obstacleVect * GM.obstacleVectStrength;
            final = Vector3.Normalize(final);

            desiredRotationVec = final;
            desiredRotation = Quaternion.LookRotation(final, Vector3.up);
        }

        // rotate sheep
        // transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, GM.sheepRotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(transform.forward, desiredRotationVec, GM.sheepRotationSpeed * Time.deltaTime, 0.0f));

        // move sheep
        transform.Translate(transform.forward * Time.deltaTime * GM.ginelli.v2 * dogVeryClose, Space.World);

        // force sheep to the ground
        transform.position = new Vector3(transform.position.x, 0, transform.position.z);
    }

    void computeNewState()
    {
        if (state == Enums.State.Run)
        {
            state = Enums.State.Idle;
            anim.SetBool("IsRunning", false);
            anim.SetBool("IsIdle", true);
        }

        if (state == Enums.State.Idle)
        {
            // get state of each neighbour
            int walkState = 0;
            foreach (GameObject s in closeSheepList)
            {
                if (s.GetComponent<SheepController>().state == Enums.State.Walk)
                {
                    walkState += 1;
                }
            }
            // calculate porbability
            float prob = (1 + GM.ginelli.alpha * walkState) / GM.ginelli.tau0_1;

            // potentially change state
            if (Random.Range(0, 1) < prob) {
                state = Enums.State.Walk;
                anim.SetBool("IsIdle", false);
            }
        }
        else if (state == Enums.State.Walk)
        {
            // get state of each neighbour
            int idleState = 0;
            foreach (GameObject s in closeSheepList)
            {
                if (s.GetComponent<SheepController>().state == Enums.State.Idle)
                {
                    idleState += 1;
                }
            }
            // calculate porbability
            float prob = (1 + GM.ginelli.alpha * idleState) / GM.ginelli.tau1_0;

            // potentially change state
            if (Random.Range(0, 1) < prob)
            {
                state = Enums.State.Idle;
                anim.SetBool("IsIdle", true);
            }
        }
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
