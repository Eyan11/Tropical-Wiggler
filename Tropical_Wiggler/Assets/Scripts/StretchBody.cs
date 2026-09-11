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
    private SphereCollider tailCollider;

    [Header ("Stretch Settings")]
    [SerializeField] private float maxRotation = 60f;
    [Tooltip ("Rotation to set body to when max rotation is exceeded. Should be less than maxRotation.")]
    [SerializeField] private float resetRotation = 55f;
    [SerializeField] private float maxDistance = 0.8f;
    [SerializeField] private float minDistance = 0.3f;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float tailRotationSpeed = 0.8f;
    [SerializeField] private float maxStretchDistanceThreshold = 0.3f;
    [Tooltip ("The number of times the body position is set and constrained per frame (where setting and constraining both have collision checks afterwards). The algorithm will always execute all iterations and never exit early.")]
    [SerializeField] private int constrainIterations = 3;
    [Tooltip ("How fast the body spheres move towards their reset position when max rotation is exceeded. A low value can cause head front body to be too far from head and trigger max stretch too early, while a high value can allow for exploits where the body moves through colliders.")]
    [SerializeField] private float resetPositionMoveSpeed = 100f;
    private bool hasSpawnedAllBodies = false;
    private int numSpawnedBodies = 0;
    private bool isStretching = false;
    private float curDistance = 0.8f; // Current distance between all body spheres

    [Header ("Collision Settings")]
    [SerializeField] private LayerMask collisionMask;
    [Tooltip ("Extra distance to keep body spheres away from colliders when resolving collisions. Keep very low at a value below 0.1")]
    [SerializeField] private float collisionOffset = 0.01f;
    [Tooltip ("The max number of times the sphere overlap collision detection and resolution will be performed, algorithm will exit early if no collisions are detected.")]
    [SerializeField] private int maxCollisionResolveIterations = 2;
    [Tooltip ("The max number of times the spherecast collision detection and slide will be performed, algorithm will exit early if no collisions are detected.")]
    [SerializeField] private int maxCollisionSlideIterations = 3;
    [Tooltip ("The max number of times the distance constraint will be performed PER BODY, algorithm will exit early for current body if it is within curDistance + tolerance of the body behind it.")]
    [SerializeField] private int maxDistanceConstraintIterations = 5; // Values too low let the player slip through very slim poles
    [SerializeField] private float distanceConstraintTolerance = 0.01f;
    private readonly Collider[] collisionResults = new Collider[16];

    private void Awake()
    {
        curDistance = maxDistance;
        bodyParent = tailAndBodyTran.parent;
        tailAndBodyStartPos = tailAndBodyTran.localPosition;
        tailCollider = tailTran.GetComponent<SphereCollider>();

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

        if (!hasSpawnedAllBodies) TrySpawnNewBody();

        MoveBodyPositions();
        RotateBodiesAndTail();
        UpdateCurrentDistance(); // Update distance between body spheres
    }
    
    
    // Spawns a new body sphere at the tail's position if the distance between the tail and next body sphere is greater than maxDistance
    private void TrySpawnNewBody()
    {
        // Get spawned body closest to tail
        Transform nextBody;
        if (numSpawnedBodies >= 1) nextBody = bodyTran[numSpawnedBodies - 1];
        else nextBody = headTran;

        float distance = Vector3.Distance(tailTran.position, nextBody.position);
        if (distance > maxDistance) // Spawn a new body sphere at the tail's position
        {
            Transform newBody = bodyTran[numSpawnedBodies];

            newBody.SetPositionAndRotation(tailTran.position, nextBody.rotation);
            SetBodyPosition(newBody, nextBody);

            newBody.gameObject.SetActive(true);
            numSpawnedBodies++;

            if (numSpawnedBodies >= bodyTran.Count) hasSpawnedAllBodies = true;
        }
    }

    // Smoothly rotates all body spheres towards the body in front of it, starting with the body behind the head and ending with the body in front of the tail.
    private void RotateBodiesAndTail()
    {
        Transform body;
        Transform nextBody = headTran;

        // Rotate all bodies towards the body in front of it
        for (int i = 0; i < numSpawnedBodies; i++)
        {
            body = bodyTran[i];
            Vector3 direction = nextBody.position - body.position;
            Quaternion targetRotation = Quaternion.LookRotation(direction); // Rotation towards body in front of it
            body.rotation = Quaternion.Slerp(body.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            nextBody = body;
        }

        // Rotate tail towards the last body
        if (hasSpawnedAllBodies)
        {
            Vector3 direction = bodyTran[^1].position - tailTran.position;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            tailTran.rotation = Quaternion.Slerp(tailTran.rotation, targetRotation, tailRotationSpeed * Time.deltaTime);
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

    // Handles movement of all bodies by setting position from the head and constraining from the tail, also check collisions after setting both steps
    private void MoveBodyPositions()
    {
        Transform body;
        Transform referenceBody;
        SphereCollider referenceColl;

        for (int iter = 0; iter < constrainIterations; iter++)
        {
            referenceBody = headTran;

            // Move bodies towards body in front of them, starting closest to head
            for (int i = 0; i < numSpawnedBodies; i++)
            {
                body = bodyTran[i];
                Vector3 oldPos = body.position;
                SetBodyPosition(body, referenceBody);
                ResolveBodyContinuousCollision(bodyColl[i], oldPos); // Handle collisions, must do after moving
                referenceBody = body;
            }

            if (hasSpawnedAllBodies)
            {
                referenceColl = tailCollider;

                // Move bodies curDistance away from the body behind them, starting closest to tail
                for (int i = bodyTran.Count - 1; i >= 0; i--)
                {
                    ConstrainBodyDistance(bodyColl[i], referenceColl); // Also handles collisions
                    referenceColl = bodyColl[i];
                }
            }
        }
    }

    // Using the body in front of it as reference, it sets body position within curDistance and within maxRotation of it's forward vector
    private void SetBodyPosition(Transform body, Transform nextBody)
    {
        float angle = Vector3.SignedAngle(body.forward, nextBody.forward, Vector3.up);
        Vector3 directionToNextBody = nextBody.position - body.position;
        float distanceToNextBody = directionToNextBody.magnitude;
        directionToNextBody.Normalize();
        if (distanceToNextBody <= 0.001f) return;

        if (Mathf.Abs(angle) < maxRotation)
        {
            // If the distance is greater than the current distance between bodies, move the body towards the next body
            if (distanceToNextBody > curDistance)
            {
                body.position += (distanceToNextBody - curDistance) * directionToNextBody;
            }
        }
        else
        {
            // Calculate two possible reset directions, which rotate -/+resetRotation degrees from the next body's backward vector
            Vector3 resetDirA = Quaternion.AngleAxis(resetRotation, Vector3.up) * -nextBody.forward;
            Vector3 resetDirB = Quaternion.AngleAxis(-resetRotation, Vector3.up) * -nextBody.forward;

            // Choose the reset direction closest to the current direction
            Vector3 curDir = -directionToNextBody;
            Vector3 targetDir = (Vector3.Dot(curDir, resetDirA) > Vector3.Dot(curDir, resetDirB)) ? resetDirA : resetDirB;

            // Calculate how long the body can move during one constraint iteration
            float maxMove = resetPositionMoveSpeed * Time.deltaTime / constrainIterations;

            // Calculate the maximum angle we can rotate by (angle in radians = arc length / radius)
            float maxRadians = maxMove / Mathf.Max(distanceToNextBody, 0.001f);
            Vector3 newDir = Vector3.RotateTowards(curDir, targetDir, maxRadians, 0f);

            // Gradually move towards newDir without exceeding curDistance
            float newDist = Mathf.MoveTowards(distanceToNextBody, curDistance, maxMove);
            body.position = nextBody.position + (newDir * newDist);
        }
    }

    // Constrains the current body sphere to the previous one behind it so that it is curDistance away from it and not colliding with the environment.
    private void ConstrainBodyDistance(SphereCollider bodyColl, SphereCollider prevColl)
    {
        Transform body = bodyColl.transform;
        Transform prevBody = prevColl.transform;

        for (int i = 0; i < maxDistanceConstraintIterations; i++)
        {
            Vector3 offset = prevBody.position - body.position;
            float distance = offset.magnitude;

            if (distance <= curDistance + distanceConstraintTolerance) return;

            Vector3 oldBodyPos = body.position;

            Vector3 direction = offset / distance;
            body.position += direction * (distance - curDistance); // Move current body toward previous body until it's curDistance away

            ResolveBodyContinuousCollision(bodyColl, oldBodyPos);
        }
    }


    // Resolves collisions for a body sphere using a spherecast from its old position to its new position
    private void ResolveBodyContinuousCollision(SphereCollider sphereColl, Vector3 oldPos)
    {
        Transform body = sphereColl.transform;
        Vector3 targetPos = body.position;
        body.position = oldPos; // Go back so we can sweep from old to new position
        
        Vector3 remainingMove = targetPos - body.position;
        float worldRadius = sphereColl.radius * body.lossyScale.x;

        for (int iteration = 0; iteration < maxCollisionSlideIterations; iteration++)
        {
            float distance = remainingMove.magnitude;

            if (distance <= 0.001f) break;

            Vector3 direction = remainingMove / distance;
            Vector3 worldCenter = body.TransformPoint(sphereColl.center);

            // If no collisions, exit early and move towards target position
            if (!Physics.SphereCast(worldCenter, worldRadius, direction, 
                out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                body.position += remainingMove;
                break;
            }

            // Move as far as we safely can toward the collision.
            float safeDistance = Mathf.Max(0f, hit.distance - collisionOffset);
            body.position += direction * safeDistance;

            // Subtract the movement we already completed.
            remainingMove -= direction * safeDistance;

            // Remove only the part of our remaining movement that tries to go INTO the collider.
            float intoSurface = Vector3.Dot(remainingMove, hit.normal); // -1 means moving directly into the surface, 1 means directly away from surface
            if (intoSurface < 0f) remainingMove -= hit.normal * intoSurface;
        }
        
        ResolveBodyDiscreteCollision(sphereColl); // Safety cleanup
    }

    // Resolves body collisions by checking for overlaps with the current position and moving the body out of any colliders
    private void ResolveBodyDiscreteCollision(SphereCollider sphereColl)
    {
        Transform body = sphereColl.transform;

        for (int iteration = 0; iteration < maxCollisionResolveIterations; iteration++)
        {
            Vector3 worldCenter = body.TransformPoint(sphereColl.center);
            float worldRadius = sphereColl.radius * body.lossyScale.x;

            // Get how many colliders are overlapping with the body sphere
            int hitCount = Physics.OverlapSphereNonAlloc(worldCenter, worldRadius + collisionOffset, collisionResults, collisionMask, QueryTriggerInteraction.Ignore);
            bool foundOverlap = false;

            // Resolve all collisions with current body's position by moving it outside of the collider it's overlapping with
            for (int i = 0; i < hitCount; i++)
            {
                Collider other = collisionResults[i];

                if (Physics.ComputePenetration(sphereColl, body.position, body.rotation, other,
                    other.transform.position, other.transform.rotation, out Vector3 direction, out float distance))
                {
                    body.position += direction * (distance + collisionOffset);
                    foundOverlap = true;
                }
            }

            if (!foundOverlap) break; // Exit if no collisions
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

    // Returns the rotation of the body sphere right behind the head
    public Quaternion GetFrontBodyRotation()
    {
        if (numSpawnedBodies < 1) return Quaternion.identity;
        return bodyTran[0].rotation;
    }
}
