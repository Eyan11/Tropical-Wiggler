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
    private InputManager input;
    private Transform bodyParent;

    [Header ("Stretch Settings")]
    [SerializeField] private float maxRotation = 60f;
    [SerializeField] private float flipRotationThreshold = 65f;
    [SerializeField] private float maxDistance = 0.80f;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float maxStretchDistanceThreshold = 0.3f;
    [SerializeField] private int constrainIterations = 5;
    private bool hasSpawnedAllBodies = false;
    private int numSpawnedBodies = 0;
    private bool isStretching = false;

    private void Awake()
    {
        bodyParent = tailAndBodyTran.parent;

        input = GetComponent<InputManager>();
        input.OnStretchInputChanged += OnStretchInputChanged;

        // Get all siblings of tail (body sphere's 1 - 14)
        foreach (Transform child in tailTran.parent.transform)
        {
            if (child != tailTran) {
                bodyTran.Add(child);
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
            tailAndBodyTran.SetParent(null, false);
        }
        else
        {
            tailAndBodyTran.SetParent(bodyParent);
            tailAndBodyTran.localPosition = Vector3.zero;
            tailAndBodyTran.localRotation = Quaternion.identity;
        
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
                referenceBody = body;
            }

            if (!hasSpawnedAllBodies) return;

            referenceBody = tailTran;

            // Move bodies towards body behind them, starting closest to tail
            for (int i = bodyTran.Count - 1; i >= 0; i--)
            {
                body = bodyTran[i];
                ConstrainBodyDistance(body, referenceBody);
                referenceBody = body;
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

            // If the distance is greater than maxDistance, move the body towards the next body
            if (distanceToNextBody > maxDistance)
            {
                body.position += (distanceToNextBody - maxDistance) * directionToNextBody.normalized;
            }
        }
        else
        {
            if (Mathf.Abs(angle) > flipRotationThreshold) angle = Mathf.Sign(angle) * flipRotationThreshold;
            float clampedAngle = Mathf.Clamp(angle, -maxRotation, maxRotation);

            // Start behind the next body.
            Vector3 direction = -nextBody.forward;

            // Rotate direction vector by clampedAngle amount around the Y axis
            direction = Quaternion.AngleAxis(-clampedAngle, Vector3.up) * direction;
            Vector3 targetPosition = nextBody.position + direction * maxDistance;
            
            body.position = targetPosition;

            /* DEBUG
            float newAngle = Vector3.SignedAngle(
                body.forward,
                nextBody.forward,
                Vector3.up
            );
            Debug.Log("Old Angle: " + angle + ", New Angle: " + newAngle);
            */
        }
    }

    // Constrains a body to be no further than max distance from the body behind it
    private void ConstrainBodyDistance(Transform body, Transform prevBody)
    {
        Vector3 offset = prevBody.position - body.position;
        float distance = offset.magnitude;

        if (distance <= maxDistance) return;

        // Don't lerp position to enforce constraint immediately
        body.position += (distance - maxDistance) * offset.normalized;
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
}
