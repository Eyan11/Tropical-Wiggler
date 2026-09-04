using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(InputManager))]
public class StretchBody : MonoBehaviour
{
    [Header ("References")]
    [SerializeField] private Transform tailAndBodyTran;
    [SerializeField] private Transform tailTran;
    [SerializeField] private Transform headTran;
    private List<Transform> bodyTran = new List<Transform>();
    private List<SphereCollider> bodyColl = new List<SphereCollider>();
    private InputManager input;
    private Transform bodyParent;
    private Vector3 tailAndBodyStartPos;

    [Header ("Stretch Settings")]
    [SerializeField] private float maxRotation = 60f;
    [Tooltip ("Rotation to set body to when max rotation is exceeded. Should be less than maxRotation.")]
    [SerializeField] private float resetRotation = 55f;
    [SerializeField] private float maxDistance = 0.80f;
    [SerializeField] private float minDistance = 0.3f;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float maxStretchDistanceThreshold = 0.3f;
    [SerializeField] private int constrainIterations = 5;
    private bool hasSpawnedAllBodies = false;
    private int numSpawnedBodies = 0;
    private bool isStretching = false;
    private float curDistance = 0.80f; // Current distance between all body spheres

    [Header ("Collision Settings")]
    [SerializeField] private LayerMask collisionMask;
    [Tooltip ("Extra distance to keep body spheres away from colliders when resolving collisions. Keep very low at a value below 0.1")]
    [SerializeField] private float collisionOffset = 0.01f;
    [SerializeField] private int collisionResolveIterations = 2;
    private readonly Collider[] collisionResults = new Collider[16];

    private void Awake()
    {
        curDistance = maxDistance;
        bodyParent = tailAndBodyTran.parent;
        tailAndBodyStartPos = tailAndBodyTran.localPosition;

        input = GetComponent<InputManager>();
        input.OnStretchInputChanged += OnStretchInputChanged;

        // Get all siblings of tail (body sphere's 1 - 14)
        foreach (Transform child in tailTran.parent.transform)
        {
            if (child != tailTran) {
                bodyTran.Add(child);
                bodyColl.Add(child.GetComponent<SphereCollider>());
                child.gameObject.SetActive(false);
            }
        }
    }

    private void OnStretchInputChanged(bool _isStretching)
    {
        isStretching = _isStretching;
        if (isStretching)
        {
            hasSpawnedAllBodies = false;
            numSpawnedBodies = 0;
            tailAndBodyTran.SetParent(null);
        }
        else
        {
            tailAndBodyTran.SetParent(bodyParent);
            tailAndBodyTran.SetLocalPositionAndRotation(tailAndBodyStartPos, Quaternion.identity);
        
            foreach (Transform body in bodyTran) // Hide all body spheres
            {
                body.gameObject.SetActive(false);
            }
        }
    }

    
    private void Update()
    {
        if (!isStretching) return;

        if (!hasSpawnedAllBodies)
        {
            TrySpawnNewBody();
        }
        MoveBodyPositions();
        RotateBodyTowardsHead();
        UpdateCurrentDistance(); // Update distance between body spheres
    }
    
    

    private void TrySpawnNewBody()
    {
        // Get spawned body closest to tail
        Transform nextBody;
        if (numSpawnedBodies >= 1) nextBody = bodyTran[numSpawnedBodies - 1];
        else nextBody = headTran;

        float distance = Vector3.Distance(tailTran.position, nextBody.position);
        if (distance > maxDistance)
        {
            Transform newBody = bodyTran[numSpawnedBodies];

            newBody.position = tailTran.position;
            newBody.rotation = nextBody.rotation;
            SetBodyPosition(newBody, nextBody);

            newBody.gameObject.SetActive(true);
            numSpawnedBodies++;

            if (numSpawnedBodies >= bodyTran.Count)
            {
                hasSpawnedAllBodies = true;
            }
        }
    }

    private void RotateBodyTowardsHead()
    {
        Transform body;
        Transform nextBody = headTran;

        for (int i = 0; i < numSpawnedBodies; i++)
        {
            body = bodyTran[i];

            // Calculate the direction and distance to the body in front of it
            Vector3 directionToNextBody = nextBody.position - body.position;

            // Calculate the target rotation based on the direction to the tail
            Quaternion targetRotation = Quaternion.LookRotation(directionToNextBody);

            // Smoothly rotate towards the target rotation
            body.rotation = Quaternion.Slerp(body.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            nextBody = body;
        }
    }

    // Updates curDistance by setting it to the average distance between all body spheres, clamped to min and max distance
    private void UpdateCurrentDistance()
    {
        if (!hasSpawnedAllBodies) 
        {
            curDistance = maxDistance;
            return;
        }

        float totalDistance = 0f;
        totalDistance += Vector3.Distance(headTran.position, bodyTran[0].position); // Head to front body
        totalDistance += Vector3.Distance(bodyTran[^1].position, tailTran.position); // Back body to tail
        for (int i = 0; i < numSpawnedBodies - 1; i++)
        {
            totalDistance += Vector3.Distance(bodyTran[i].position, bodyTran[i + 1].position); // Body to body
        }
        float avgDistance = totalDistance / (numSpawnedBodies + 1); // Divide by number of gaps instead of number of bodies

        curDistance = Mathf.Clamp(avgDistance, minDistance, maxDistance);
    }

    // Handles movement of all bodies by setting position from the head and constraining from the tail
    private void MoveBodyPositions()
    {
        Transform body;
        Transform referenceBody;

        for (int iter = 0; iter < constrainIterations; iter++)
        {
            referenceBody = headTran;

            // Move bodies towards body in front of them, starting closest to head
            for (int i = 0; i < numSpawnedBodies; i++)
            {
                body = bodyTran[i];
                SetBodyPosition(body, referenceBody);
                ResolveBodyCollision(bodyColl[i]); // Handle collisions, must do after moving
                referenceBody = body;
            }

            if (hasSpawnedAllBodies)
            {
                referenceBody = tailTran;

                // Move bodies towards body behind them, starting closest to tail
                for (int i = bodyTran.Count - 1; i >= 0; i--)
                {
                    body = bodyTran[i];
                    ConstrainBodyDistance(body, referenceBody);
                    ResolveBodyCollision(bodyColl[i]); // Handle collisions, must do after constraining
                    referenceBody = body;
                }
            }
        }
    }

    // Using the body in front of it as reference, it sets body position within max distance and withing max rotation of it's forward vector
    private void SetBodyPosition(Transform body, Transform nextBody)
    {
        float angle = Vector3.SignedAngle(body.forward, nextBody.forward, Vector3.up);

        if (Mathf.Abs(angle) < maxRotation)
        {
            // Calculate the direction and distance to the body in front of it
            Vector3 directionToNextBody = nextBody.position - body.position;
            float distanceToNextBody = directionToNextBody.magnitude;

            // If the distance is greater than the current distance between bodies, move the body towards the next body
            if (distanceToNextBody > curDistance)
            {
                body.position += (distanceToNextBody - curDistance) * directionToNextBody.normalized;
            }
        }
        else
        {
            angle = Mathf.Sign(angle) * resetRotation;
            float clampedAngle = Mathf.Clamp(angle, -maxRotation, maxRotation);

            // Start behind the next body.
            Vector3 direction = -nextBody.forward;

            // Rotate direction vector by clampedAngle amount around the Y axis
            direction = Quaternion.AngleAxis(-clampedAngle, Vector3.up) * direction;
            Vector3 targetPosition = nextBody.position + direction * curDistance;
            
            body.position = targetPosition;
        }
    }

    // Constrains a body to be no further than max distance from the body behind it
    private void ConstrainBodyDistance(Transform body, Transform prevBody)
    {
        Vector3 offset = prevBody.position - body.position;
        float distance = offset.magnitude;

        if (distance <= curDistance) return;

        // Don't lerp position to enforce constraint immediately
        body.position += (distance - curDistance) * offset.normalized;
    }

    // Resolves collisions between body spheres and the environment
    private void ResolveBodyCollision(SphereCollider sphereColl)
    {
        Transform bodyTran = sphereColl.transform;

        for (int iteration = 0; iteration < collisionResolveIterations; iteration++)
        {
            Vector3 worldCenter = bodyTran.TransformPoint(sphereColl.center);
            float worldRadius = sphereColl.radius * bodyTran.lossyScale.x; // Assuming uniform scale

            int hitCount = Physics.OverlapSphereNonAlloc(worldCenter, worldRadius + collisionOffset,
                collisionResults, collisionMask, QueryTriggerInteraction.Ignore);
            bool foundOverlap = false;

            for (int i = 0; i < hitCount; i++) // For all colliders overlapping this body sphere
            {
                Collider other = collisionResults[i];

                // If body collider is overlapping with another collider, move it outside of collider
                if (Physics.ComputePenetration(sphereColl, bodyTran.position, bodyTran.rotation, 
                    other, other.transform.position, other.transform.rotation, 
                    out Vector3 direction, out float distance))
                {
                    bodyTran.position += direction * (distance + collisionOffset);
                    foundOverlap = true;
                }
            }

            if (!foundOverlap) break;
        }
    }


    // Returns true if player has stretched as far as possible (front body sphere is too far from head)
    public bool IsMaxStretchReached()
    {
        if (numSpawnedBodies < bodyTran.Count) return false;

        float distance = Vector3.Distance(bodyTran[0].position, headTran.position);
        return distance > maxDistance + maxStretchDistanceThreshold;
    }

    // Returns the forward vector of the body sphere right behind the head
    public Vector3 GetFrontBodyForward()
    {
        if (numSpawnedBodies < 1) return Vector3.zero;
        return bodyTran[0].forward;
    }

    // Returns the right vector of the body sphere right behind the head
    public Vector3 GetFrontBodyRight()
    {
        if (numSpawnedBodies < 1) return Vector3.zero;
        return bodyTran[0].right;
    }

    public Quaternion GetFrontBodyRotation()
    {
        if (numSpawnedBodies < 1) return Quaternion.identity;
        return bodyTran[0].rotation;
    }
}
