using UnityEngine;

public class AimController : MonoBehaviour
{
    public Transform weaponHolder;
    public Transform hand;

    public float minWeaponDistance;
    public float maxWeaponDistance;

    [Range(0,100)]
    public float positionSmoothing = 10f;
    [Range(0, 100)]
    public float rotationSmoothing = 10f;

    private Vector3 mousePos;
    private Vector3 direction;

    private void Update()
    {
        mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = hand.transform.position.z;

        direction = mousePos - hand.transform.position;

        float firstDistance = Mathf.Clamp(direction.magnitude, 0, maxWeaponDistance);
        direction = direction.normalized * firstDistance;

        Vector3 targetPosition = hand.position + direction;

        float currentDistance = Vector3.Distance(mousePos, targetPosition);
        if (currentDistance < minWeaponDistance)
        {
            float moveDistance = minWeaponDistance - currentDistance;
            targetPosition -= direction.normalized * moveDistance;
        }

        weaponHolder.position = Vector3.Lerp(weaponHolder.position, targetPosition, positionSmoothing * Time.deltaTime);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0, 0, angle);

        weaponHolder.rotation = Quaternion.Lerp(weaponHolder.rotation, targetRotation, rotationSmoothing * Time.deltaTime);

        Vector3 localScale = weaponHolder.localScale;
        localScale.y = (mousePos.x < hand.position.x) ? -Mathf.Abs(localScale.y) : Mathf.Abs(localScale.y);
        weaponHolder.localScale = localScale;
    }
}
