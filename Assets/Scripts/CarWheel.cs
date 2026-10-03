using UnityEngine;

public class CarWheel : MonoBehaviour
{
    public WheelCollider targetWheel;

    private void Update()
    {
        if (targetWheel == null)
        {
            return;
        }

        targetWheel.GetWorldPose(out Vector3 wheelPosition, out Quaternion wheelRotation);
        transform.SetPositionAndRotation(wheelPosition, wheelRotation);
    }
}
