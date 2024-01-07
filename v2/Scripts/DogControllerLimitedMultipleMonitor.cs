using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DogControllerLimitedMultipleMonitor : MonoBehaviour
{

    // Dog Animator Controller
    public Animator anim;

    private GameManager GM;
    private Rigidbody m_Rigidbody;

    private Vector3 gcm;
    private List<GameObject> visibleSheep;
    private List<GameObject> dogList;
    private float fN;
    private float Pd;
    private GameObject goal;
    private Vector3 goingDirection;
    private Vector3 destinationPosition;
    private Enums.dogState state;
    private float monitorSpeed;

    void Start()
    {
        GM = FindObjectOfType<GameManager>();
        m_Rigidbody = GetComponent<Rigidbody>();
        goal = GM.goal;
        state = Enums.dogState.collect;

        // get visible sheep
        visibleSheep = getVisibleSheep(GM.sheepList, 180);

        // get dog list
        dogList = GM.dogList;

        // monitor speed
        monitorSpeed = 1;
    }

    // Update is called once per frame
    void Update()
    {
        // get visible sheep
        visibleSheep = getVisibleSheep(GM.sheepList, 180);

        // if no sheep visible and dog has no destination -> only rotate
        if (visibleSheep.Count <= 0 && state != Enums.dogState.hasDestination)
        {
            transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(transform.forward, Vector3.Cross(transform.forward, Vector3.up), GM.dogRotationSpeed * Time.deltaTime, 0.0f));
            return;
        }

        // calculate GCM
        gcm = calculateGCM(visibleSheep);

        // calculate f(N) - how far sheep can be from gcm
        fN = GM.strombom.ra * Mathf.Pow(visibleSheep.Count, 2f / 3f);

        // check if any sheep not in f(N) = ra_N^2/3 from GCM
        // Vector3? furthestSheepPos = allInGCM(visibleSheep, gcm, fN);
        Vector3? furthestSheepPos = allInGCMAngle(visibleSheep, gcm, fN, goal.transform.position);

        // reset monitor speed
        monitorSpeed = 1;

        // do collect
        if (furthestSheepPos.HasValue)
        {
            // change state
            state = Enums.dogState.collect;

            // get desired location
            Vector3 direction = Vector3.Normalize((Vector3)furthestSheepPos - gcm);
            Vector3 desiredPosition = (Vector3)furthestSheepPos + direction * GM.strombom.Pc;
            Debug.DrawLine(transform.position, desiredPosition, Color.black);

            // check if some other dog is closer to that sheep
            float myDist = Vector3.Distance(desiredPosition, transform.position);
            bool closer = false;
            foreach(GameObject d in dogList)
            {
                if (Vector3.Distance(desiredPosition, d.transform.position) < myDist)
                {
                    closer = true;
                }
            }

            // if some other dog closer, go around the gcm and check for other out of gcm sheep
            if (closer)
            {
                // get desired location on the other side of gcm
                Pd = GM.strombom.ra * Mathf.Sqrt(visibleSheep.Count);
                direction = Vector3.Normalize(gcm - transform.position);

                // decide to which direction to run based on angle with gcm
                Vector3 perpendicularLeft = Vector3.Cross(direction, Vector3.up).normalized;
                Vector3 perpendicularRight = -perpendicularLeft;
                if (Vector3.Angle(perpendicularLeft, Vector3.Normalize(GM.goal.transform.position - gcm)) < Vector3.Angle(perpendicularRight, Vector3.Normalize(GM.goal.transform.position - gcm))) {
                    desiredPosition = gcm + (direction + perpendicularRight * 3f) * Pd;
                } else
                {
                    desiredPosition = gcm + (direction + perpendicularLeft * 3f) * Pd;
                }
                
                goingDirection = Vector3.Normalize(desiredPosition - transform.position);
                Debug.DrawLine(transform.position, desiredPosition, Color.white);
                Debug.DrawLine(transform.position, gcm, Color.red);
            }
            // else if this dog is the closes, go to that location
            else
            {
                goingDirection = Vector3.Normalize(desiredPosition - transform.position);
            }
        }
        // do herd
        else if (state != Enums.dogState.hasDestination)
        {
            // get desired location
            Pd = GM.strombom.ra * Mathf.Sqrt(visibleSheep.Count);
            Vector3 direction = Vector3.Normalize(gcm - goal.transform.position);
            Vector3 desiredPosition = gcm + direction * Pd;

            // check if some other dog is closer and get its position
            float myDist = Vector3.Distance(desiredPosition, transform.position);
            bool closer = false;
            float smallestDist = Mathf.Infinity;
            Vector3 herderPos = new Vector3();
            foreach (GameObject d in dogList)
            {
                if (Vector3.Distance(desiredPosition, d.transform.position) < myDist)
                {
                    closer = true;
                    if (Vector3.Distance(desiredPosition, d.transform.position) < smallestDist)
                    {
                        herderPos = d.transform.position;
                    }
                }
            }

            // if this dog is closest -> go to that position
            if (!closer)
            {
                goingDirection = Vector3.Normalize(desiredPosition - transform.position);
            }
            // else get destination for monitoring
            else
            {
                // decide to which direction to run based on angle with gcm
                Vector3 perpendicularLeft = Vector3.Cross(direction, Vector3.up).normalized;
                Vector3 perpendicularRight = -perpendicularLeft;
                if (Vector3.Angle(perpendicularLeft, Vector3.Normalize(herderPos - gcm)) < Vector3.Angle(perpendicularRight, Vector3.Normalize(herderPos - gcm)))
                {
                    desiredPosition += (direction + perpendicularRight) * Pd * 4;
                }
                else
                {
                    desiredPosition += (direction + perpendicularLeft) * Pd * 4;
                }

                // if destination is futher from gcm -> move towards gcm
                if (Vector3.Distance(transform.position, gcm) < Vector3.Distance(desiredPosition, gcm))
                {
                    goingDirection = gcm;
                    monitorSpeed = 0.1f;
                } else
                {
                    goingDirection = Vector3.Normalize(desiredPosition - transform.position);

                    state = Enums.dogState.hasDestination;
                    destinationPosition = desiredPosition;

                    Debug.DrawLine(transform.position, desiredPosition, Color.white);
                }
            }
        }
        // go to destination
        else
        {
            goingDirection = Vector3.Normalize(destinationPosition - transform.position);
            monitorSpeed = 0.2f;

            if (state == Enums.dogState.hasDestination && Vector3.Distance(transform.position, destinationPosition) < 2)
            {
                state = Enums.dogState.herd;
            }

            Debug.DrawLine(transform.position, destinationPosition, Color.magenta);
        }

        // distort rotation by gcm - so it goes around the sheep
        Vector3 finalDirection = Vector3.Normalize(Vector3.Normalize(transform.position - gcm) * 0.5f + goingDirection);
        Debug.DrawLine(transform.position, transform.position + finalDirection, Color.green);

        // rotate shepherd
        transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(transform.forward, finalDirection, GM.dogRotationSpeed * Time.deltaTime, 0.0f));

        // add ratio to shepherd speed according to distance to closest sheep
        float distRatio;
        if (visibleSheep.Count > 0)
        {
            Vector3 sheepPos = closestSheep(visibleSheep);
            float dist = Vector3.Distance(transform.position, sheepPos);
            distRatio = (dist - GM.minDistToSheep) / (GM.maxDistToSheep - GM.minDistToSheep);
        } else
        {
            distRatio = 1;
        }

        // move shepherd
        transform.Translate(transform.forward * Time.deltaTime * GM.dogSpeed * distRatio * monitorSpeed, Space.World);

        // choose animation depending on speed
        if (GM.dogSpeed * distRatio * monitorSpeed > GM.dogSpeed / 2)
        {
            anim.SetBool("IsRunning", true);
            anim.SetBool("IsWalking", false);
        }
        else
        {
            anim.SetBool("IsRunning", false);
            anim.SetBool("IsWalking", true);
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

    // check if all objects in lst are dist around gcm or return furthest object
    public Vector3? allInGCM(List<GameObject> lst, Vector3 gcm_, float dist)
    {
        float furthestDist = 0;
        Vector3? furthestPoint = null;
        foreach (GameObject s in lst)
        {
            float fromGCM = Vector3.Distance(s.transform.position, gcm_);
            if (fromGCM > dist && fromGCM > furthestDist)
            {
                furthestDist = fromGCM;
                furthestPoint = s.transform.position;
            }
        }

        return furthestPoint;
    }

    // check if all objects in lst are dist around gcm or if not they are not in the way of gcm, otherwise return furthest object
    public Vector3? allInGCMAngle(List<GameObject> lst, Vector3 gcm_, float dist, Vector3 goal_)
    {
        float furthestDist = 0;
        Vector3? furthestPoint = null;
        foreach (GameObject s in lst)
        {
            // get distances and angle
            float fromGCM = Vector3.Distance(s.transform.position, gcm_);
            float fromGoal = Vector3.Distance(s.transform.position, goal_);
            Vector3 goalSheepVec = Vector3.Normalize(s.transform.position - goal_);
            Vector3 gcmSheepVec = Vector3.Normalize(s.transform.position - gcm_);
            float angle = Vector3.Angle(goalSheepVec, gcmSheepVec);

            if (fromGCM > dist && fromGCM > furthestDist && (angle > 25 || fromGoal > fromGCM))
            {
                furthestDist = fromGCM;
                furthestPoint = s.transform.position;
            }
        }

        return furthestPoint;
    }

    // returns position of closest object in lst
    Vector3 closestSheep(List<GameObject> lst)
    {
        if (lst.Count == 0)
        {
            print("ERROR: no elements in closestSheep lst");
        }

        Vector3 closest = new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
        float dist = Mathf.Infinity;
        foreach (GameObject s in lst)
        {
            float currentDist = Vector3.Distance(transform.position, s.transform.position);
            if (currentDist < dist)
            {
                dist = currentDist;
                closest = s.transform.position;
            }
        }

        return closest;
    }

    // return list of sheep in sheepList that are in viewAngle and not occluded
    private List<GameObject> getVisibleSheep(List<GameObject> sheepList, int viewAngle)
    {
        // create list
        List<GameObject> visibleSheep_ = new List<GameObject>();

        // objects in view array - length equal to viewAngle
        float[] viewDeg = new float[viewAngle];

        // TODO: filter sheep
        List<GameObject> closeSheep = new List<GameObject>(sheepList);

        // sort sheep according to distance
        closeSheep.Sort(SortListByDist);

        // decide for each sheep if visible
        foreach (GameObject s in closeSheep)
        {
            bool sheepVisible = false;

            // calculate amount of angle the sheep represents in dogs view, *2 is used later
            float sheepAngle = Mathf.Atan((GM.sheepLength / 2) / Vector3.Distance(this.transform.position, s.transform.position));
            // from rad to deg
            sheepAngle = sheepAngle * 180 / Mathf.PI;

            // calculate angle between sheep and dog
            float angle = Vector3.SignedAngle(this.transform.forward, s.transform.position - this.transform.position, Vector3.up);

            // get sheep start and stop angle
            int startAngle = (int)Mathf.Round(angle - sheepAngle);
            int stopAngle = (int)Mathf.Round(angle + sheepAngle);
            //print(startAngle + ", " + stopAngle);

            // if sheep to far
            if (startAngle == stopAngle)
            {
                
            }
            // else if sheep outside of viewAngle
            else if (Mathf.Abs(startAngle) > viewAngle / 2 || Mathf.Abs(stopAngle) > viewAngle / 2)
            {
                
            } else
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
                    visibleSheep_.Add(s);
                }
            }
        }

        return visibleSheep_;
    }

    private int SortListByDist(GameObject obj1, GameObject obj2)
    {
        float d1 = Vector3.Distance(this.transform.position, obj1.transform.position);
        float d2 = Vector3.Distance(this.transform.position, obj2.transform.position);
        if (d1 < d2)
        {
            return -1;
        } else
        {
            return 1;
        }
    }
}
