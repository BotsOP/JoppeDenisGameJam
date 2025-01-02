using UnityEngine;

public class CursorManager : MonoBehaviour
{
    private GameObject cursorVisual;

    private Vector3 mousePos;

    private void Start()
    {
        HideMouse();
    }

    private void Update()
    {
        mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        transform.position = new Vector3(mousePos.x, mousePos.y, transform.position.z);
    }

    public void HideMouse()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }
}
