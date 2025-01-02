using UnityEngine;

public class EyeController : MonoBehaviour
{
    [Header("Eye Settings")]
    public Transform eyePivot;
    public Transform eye;
    public Transform otherEye;
    public float avoidOtherEyeDistance = 1f;
    public float minEyeDistance = 0.2f;
    public float maxEyeDistance = 1f;
    [Range(0,50)]
    public float eyeSmoothingSpeed = 5f;


    [Header("Pupil Settings")]
    public Transform pupilPivot;
    public Transform pupil;
    public float pupilMaxDistance = 0.2f;
    [Range(0, 100)]
    public float pupilSmoothingSpeed = 5f;

    private Vector3 mousePos;

    private void Update()
    {
        mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = eye.transform.position.z;

        HandlePupilMovement();
        HandleEyeMovement();
    }

    private void HandlePupilMovement()
    {
        Vector3 pupilCenter = pupilPivot.position;
        Vector3 directionToMouse = mousePos - pupilCenter;

        float distance = Mathf.Clamp(directionToMouse.magnitude, 0, pupilMaxDistance);
        Vector3 clampedPosition = pupilCenter + directionToMouse.normalized * distance;

        pupil.position = Vector3.Lerp(pupil.position, clampedPosition, pupilSmoothingSpeed * Time.deltaTime);
    }

    private void HandleEyeMovement()
    {
        Vector3 eyeCenter = eyePivot.position;
        Vector3 direction = mousePos - eyeCenter;

        float firstDistance = Mathf.Clamp(direction.magnitude, 0, maxEyeDistance);
        direction = direction.normalized * firstDistance;

        Vector3 targetEyePosition = eyeCenter + direction;

        float currentDistance = Vector3.Distance(mousePos, targetEyePosition);
        if (currentDistance < minEyeDistance)
        {
            float moveDistance = minEyeDistance - currentDistance;
            targetEyePosition -= direction.normalized * moveDistance;
        }

        float otherEyeDistance = Vector3.Distance(otherEye.position, targetEyePosition);
        if(otherEyeDistance < avoidOtherEyeDistance)
        {
            Vector3 otherEyeDirection = otherEye.position - targetEyePosition;
            float moveDistance = avoidOtherEyeDistance - otherEyeDistance;
            targetEyePosition -= otherEyeDirection.normalized * moveDistance;
        }

        eye.position = Vector3.Lerp(eye.position, targetEyePosition, eyeSmoothingSpeed * Time.deltaTime);
    }
}
