using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarEngine : MonoBehaviour
{
    [Header("Path Following")]
    public Transform path;
    public float maxSteerAngle = 45f;
    public float turnSpeed = 5f;

    [Header("Wheel Physics")]
    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public WheelCollider wheelRL;
    public WheelCollider wheelRR;
    public float maxMotorTorque = 80f;
    public float maxBrakeTorque = 150f;
    public float maxSpeed = 100f;
    public Vector3 centerOfMass;
    public bool isBraking;

    [Header("Sensors")]
    public float sensorLength = 5f;
    public Vector3 frontSensorPosition = new Vector3(0f, 0.5f, 0.5f);
    public float frontSideSensorPosition = 0.2f;
    public float frontSensorAngle = 30f;

    [Header("Runtime Information")]
    public float currentSpeed;

    private readonly List<Transform> nodes = new List<Transform>();
    private Rigidbody carRigidbody;
    private int currentNode;
    private bool avoiding;
    private float targetSteerAngle;

    private void Awake()
    {
        carRigidbody = GetComponent<Rigidbody>();
        carRigidbody.centerOfMass = centerOfMass;
    }

    private void Start()
    {
        CachePathNodes();
    }

    private void FixedUpdate()
    {
        if (nodes.Count == 0)
        {
            StopDriving();
            return;
        }

        DetectObstacles();
        ApplySteering();
        Drive();
        CheckWaypointDistance();
        ApplyBrakes();
        SmoothSteering();
    }

    private void CachePathNodes()
    {
        nodes.Clear();

        if (path == null)
        {
            Debug.LogWarning("CarEngine requires a path reference.", this);
            return;
        }

        Transform[] pathTransforms = path.GetComponentsInChildren<Transform>();

        foreach (Transform pathTransform in pathTransforms)
        {
            if (pathTransform != path)
            {
                nodes.Add(pathTransform);
            }
        }
    }

    private void DetectObstacles()
    {
        Vector3 centerSensorPosition = transform.position
            + transform.forward * frontSensorPosition.z
            + transform.up * frontSensorPosition.y;

        float avoidMultiplier = 0f;
        avoiding = false;

        // Center sensor.
        if (TryDetectObstacle(centerSensorPosition, transform.forward, out RaycastHit centerHit))
        {
            avoiding = true;
            DrawSensor(centerSensorPosition, centerHit);
        }

        // Right sensors.
        Vector3 rightSensorPosition = centerSensorPosition
            + transform.right * frontSideSensorPosition;

        if (TryDetectObstacle(rightSensorPosition, transform.forward, out RaycastHit rightHit))
        {
            avoiding = true;
            avoidMultiplier -= 1f;
            DrawSensor(rightSensorPosition, rightHit);
        }
        else
        {
            Vector3 rightAngle = Quaternion.AngleAxis(frontSensorAngle, transform.up) * transform.forward;

            if (TryDetectObstacle(rightSensorPosition, rightAngle, out RaycastHit rightAngleHit))
            {
                avoiding = true;
                avoidMultiplier -= 0.5f;
                DrawSensor(rightSensorPosition, rightAngleHit);
            }
        }

        // Left sensors.
        Vector3 leftSensorPosition = centerSensorPosition
            - transform.right * frontSideSensorPosition;

        if (TryDetectObstacle(leftSensorPosition, transform.forward, out RaycastHit leftHit))
        {
            avoiding = true;
            avoidMultiplier += 1f;
            DrawSensor(leftSensorPosition, leftHit);
        }
        else
        {
            Vector3 leftAngle = Quaternion.AngleAxis(-frontSensorAngle, transform.up) * transform.forward;

            if (TryDetectObstacle(leftSensorPosition, leftAngle, out RaycastHit leftAngleHit))
            {
                avoiding = true;
                avoidMultiplier += 0.5f;
                DrawSensor(leftSensorPosition, leftAngleHit);
            }
        }

        // If multiple sensors detect an obstacle without establishing a direction,
        // use the obstacle surface normal relative to the car to choose a direction.
        if (avoiding && Mathf.Approximately(avoidMultiplier, 0f))
        {
            if (TryDetectObstacle(centerSensorPosition, transform.forward, out RaycastHit hit))
            {
                Vector3 localNormal = transform.InverseTransformDirection(hit.normal);
                avoidMultiplier = localNormal.x < 0f ? -1f : 1f;
                DrawSensor(centerSensorPosition, hit);
            }
        }

        if (avoiding)
        {
            targetSteerAngle = maxSteerAngle * Mathf.Clamp(avoidMultiplier, -1f, 1f);
        }
    }

    private bool TryDetectObstacle(Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        if (Physics.Raycast(origin, direction, out hit, sensorLength))
        {
            return !hit.collider.CompareTag("Terrain");
        }

        return false;
    }

    private void DrawSensor(Vector3 origin, RaycastHit hit)
    {
        Debug.DrawLine(origin, hit.point);
    }

    private void ApplySteering()
    {
        if (avoiding || currentNode >= nodes.Count)
        {
            return;
        }

        Vector3 relativePosition = transform.InverseTransformPoint(nodes[currentNode].position);

        if (relativePosition.sqrMagnitude < 0.001f)
        {
            targetSteerAngle = 0f;
            return;
        }

        float steeringInput = relativePosition.x / relativePosition.magnitude;
        targetSteerAngle = steeringInput * maxSteerAngle;
    }

    private void Drive()
    {
        currentSpeed = 2f * Mathf.PI * wheelFL.radius * wheelFL.rpm * 60f / 1000f;

        if (currentSpeed < maxSpeed && !isBraking)
        {
            wheelFL.motorTorque = maxMotorTorque;
            wheelFR.motorTorque = maxMotorTorque;
        }
        else
        {
            wheelFL.motorTorque = 0f;
            wheelFR.motorTorque = 0f;
        }
    }

    private void StopDriving()
    {
        if (wheelFL != null) wheelFL.motorTorque = 0f;
        if (wheelFR != null) wheelFR.motorTorque = 0f;
    }

    private void CheckWaypointDistance()
    {
        if (Vector3.Distance(transform.position, nodes[currentNode].position) >= 0.5f)
        {
            return;
        }

        currentNode = currentNode == nodes.Count - 1 ? 0 : currentNode + 1;
    }

    private void ApplyBrakes()
    {
        float brakeTorque = isBraking ? maxBrakeTorque : 0f;
        wheelRL.brakeTorque = brakeTorque;
        wheelRR.brakeTorque = brakeTorque;
    }

    private void SmoothSteering()
    {
        float blend = Time.fixedDeltaTime * turnSpeed;
        wheelFL.steerAngle = Mathf.Lerp(wheelFL.steerAngle, targetSteerAngle, blend);
        wheelFR.steerAngle = Mathf.Lerp(wheelFR.steerAngle, targetSteerAngle, blend);
    }
}
