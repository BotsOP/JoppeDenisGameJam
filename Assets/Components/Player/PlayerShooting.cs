using System;
using Managers;
using Unity.Mathematics;
using UnityEngine;
using EventType = Managers.EventType;

public class PlayerShooting : MonoBehaviour
{
    [SerializeField] private Camera cam;
    private void Update()
    {
        if (Input.GetMouseButton(0))
        {
            RaycastHit hit;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        
            if (Physics.Raycast(ray, out hit)) 
            {
                EventSystem<float3>.RaiseEvent(EventType.PLAYER_SHOOT, hit.point);
            }
        }
    }
}
