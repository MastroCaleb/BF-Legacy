using UnityEngine;

public class PerpetualRotation : MonoBehaviour
{
    public enum RotationDirection
    {
        Clockwise = -1,
        CounterClockwise = 1
    }

    public float spinSpeed = 90f;

    public RotationDirection direction = RotationDirection.Clockwise;

    public Vector3 rotationAxis = Vector3.forward;

    public bool isSpinning = true;

    void Update()
    {
        if (!isSpinning || spinSpeed == 0f) return;

        float rotationAmount = spinSpeed * (int)direction * Time.deltaTime;

        transform.Rotate(rotationAxis, rotationAmount, Space.Self);
    }

    public void SetSpeed(float newSpeed)
    {
        spinSpeed = Mathf.Abs(newSpeed);
    }

    public void SetDirection(int dirIndex)
    {
        if (dirIndex == 1) direction = RotationDirection.CounterClockwise;
        else if (dirIndex == -1) direction = RotationDirection.Clockwise;
    }

    public void ToggleSpin(bool status)
    {
        isSpinning = status;
    }
}
