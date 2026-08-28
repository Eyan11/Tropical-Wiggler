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
    [SerializeField] private float maxRotation = 30f;
    [Tooltip("Maximum rotation of body sphere right behind the head")]
    [SerializeField] private float maxFrontBodyRotation = 90f;
    [SerializeField] private float flipRotationThreshold = 30f;
    [SerializeField] private float maxDistance = 1.0f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float bodyMoveSpeed = 5f;
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
            MoveBodyPositionsWhileGrowing();
            TrySpawnNewBody();
        }
        RotateBodyTowardsHead();
    }

    private void RotateBodyTowardsHead()
    {
        Transform body;
        Transform nextBody = headTran;

        for (int i = 0; i < numSpawnedBodies; i++)
        {
            body = bodyTran[i];
            if (i >= 1) nextBody = bodyTran[i - 1];

            // Calculate the direction and distance to the body in front of it
            Vector3 directionToNextBody = nextBody.position - body.position;

            // Calculate the target rotation based on the direction to the tail
            Quaternion targetRotation = Quaternion.LookRotation(directionToNextBody);

            // Smoothly rotate towards the target rotation
            body.rotation = Quaternion.Slerp(body.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void MoveBodyPositionsWhileGrowing()
    {
        Transform body;
        Transform nextBody = headTran;

        // Stretch body
        for (int i = 0; i < numSpawnedBodies; i++)
        {
            body = bodyTran[i];
            if (i >= 1) nextBody = bodyTran[i - 1];

            SetBodyPosition(body, nextBody);
        }
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

    private void SetBodyPosition(Transform body, Transform nextBody)
    {
        float angle = Vector3.SignedAngle(body.forward, nextBody.forward, Vector3.up);
        float maxRot = (nextBody == headTran) ? maxFrontBodyRotation : maxRotation;

        if (Mathf.Abs(angle) < maxRot)
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
            float clampedAngle = Mathf.Clamp(angle, -maxRot, maxRot);

            // Start behind the next body.
            Vector3 direction = -nextBody.forward;

            // Rotate direction vector by clampedAngle amount around the Y axis
            direction = Quaternion.AngleAxis(-clampedAngle, Vector3.up) * direction;
            Vector3 targetPosition = nextBody.position + direction * maxDistance;
            
            body.position = Vector3.Lerp(body.position, targetPosition, bodyMoveSpeed * Time.deltaTime);

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
}
