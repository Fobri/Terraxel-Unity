using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class TerrainDeformer : MonoBehaviour
{
    public float deformRadius;
    Camera _camera;

    private void Start()
    {
        _camera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            var ray = new Ray(_camera.transform.position, _camera.transform.forward);
            if(Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("Terrain")))
            {
                TerraxelWorld.QueueModification((int3)(float3)hit.point, 100);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            var ray = new Ray(_camera.transform.position, _camera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("Terrain")))
            {
                TerraxelWorld.QueueModification((int3)(float3)hit.point, -100);
            }
        }
    }
}
