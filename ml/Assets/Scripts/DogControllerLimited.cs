using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DogControllerLimited : MonoBehaviour
{

    // Dog Animator Controller
    public Animator anim;

    private GameManager GM;
    private Rigidbody m_Rigidbody;

    private Vector3 gcm;
    private List<GameObject> visibleSheep;
    private float fN;
    private float Pd;
    private GameObject goal;
    private Vector3 goingDirection;

    void Start()
    {
        GM = FindObjectOfType<GameManager>();
        m_Rigidbody = GetComponent<Rigidbody>();
        goal = GM.goal;

        // get visible sheep
        visibleSheep = getVisibleSheep(GM.sheepList, 180);
    }

    // Update is called once per frame
    void Update()
    {
        // get visible sheep
        visibleSheep = getVisibleSheep(GM.sheepList, 180);

        // calculate GCM
        gcm = calculateGCM(visibleSheep);

        // calculate f(N) - how far sheep can be from gcm
        fN = GM.strombom.ra * Mathf.Pow(visibleSheep.Count, 2f / 3f);

        // check if any sheep not in f(N) = ra_N^2/3 from GCM
        Vector3? furthestSheepPos = allInGCM(visibleSheep, gcm, fN);

        // do collect
        if (furthestSheepPos.HasValue)
        {
            // get desired location
            Vector3 direction = Vector3.Normalize((Vector3)furthestSheepPos - gcm);
            Vector3 desiredPosition = (Vector3)furthestSheepPos + direction * GM.strombom.Pc;
            goingDirection = Vector3.Normalize(desiredPosition - transform.position);
        }
        // do herd
        else
        {
            // get desired location
            Pd = GM.strombom.ra * Mathf.Sqrt(visibleSheep.Count);
            Vector3 direction = Vector3.Normalize(gcm - goal.transform.position);
            Vector3 desiredPosition = gcm + direction * Pd;
            goingDirection = Vector3.Normalize(desiredPosition - transform.position);
        }

        // distort rotation by gcm - so it goes around the sheep
        Vector3 finalDirection = Vector3.Normalize(Vector3.Normalize(transform.position - gcm) * 0.5f + goingDirection);

        // rotate shepherd
        transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(transform.forward, finalDirection, GM.dogRotationSpeed * Time.deltaTime, 0.0f));

        // add ratio to shepherd speed according to distance to closest sheep
        Vector3 sheepPos = closestSheep(visibleSheep);
        float dist = Vector3.Distance(transform.position, sheepPos);
        float distRatio = (dist - GM.minDistToSheep) / (GM.maxDistToSheep - GM.minDistToSheep);

        // move shepherd
        transform.Translate(transform.forward * Time.deltaTime * GM.dogSpeed * distRatio, Space.World);

        // choose animation depending on speed
        if (GM.dogSpeed * distRatio > GM.dogSpeed / 2)
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

            // calculate amount of angle the sheep represents in dogs view
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
