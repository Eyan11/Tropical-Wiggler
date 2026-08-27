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
    [SerializeField] private float maxDistance = 1.0f;
    [SerializeField] private float rotationSpeed = 5f;
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

            // Limit the rotation based on maxRotation
            float angleDifference = Quaternion.Angle(body.rotation, targetRotation);
            angleDifference = Mathf.Clamp(angleDifference, -maxRotation, maxRotation);
            
            if (angleDifference > maxRotation) 
            {
                targetRotation = Quaternion.RotateTowards(body.rotation, targetRotation, maxRotation);
            }

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

            // Calculate the direction and distance to the body in front of it
            Vector3 directionToNextBody = nextBody.position - body.position;
            float distanceToNextBody = directionToNextBody.magnitude;

            // If the distance is greater than maxDistance, move the body towards the next body
            if (distanceToNextBody > maxDistance)
            {
                body.position += (distanceToNextBody - maxDistance) * directionToNextBody.normalized;
            }
        }
    }



    private void TrySpawnNewBody()
    {
        // Get body in front of new body
        Transform nextBody;
        if (numSpawnedBodies >= 1) nextBody = bodyTran[numSpawnedBodies - 1];
        else nextBody = headTran;

        Vector3 direction = tailTran.position - nextBody.position;
        if (direction.magnitude > maxDistance)
        {
            Transform newBody = bodyTran[numSpawnedBodies];

            // Place new body on line between tail and next body, at max distance from next body
            newBody.position = direction.normalized * maxDistance + nextBody.position;

            newBody.gameObject.SetActive(true);
            numSpawnedBodies++;

            if (numSpawnedBodies >= bodyTran.Count)
            {
                hasSpawnedAllBodies = true;
            }
        }
    }
}
