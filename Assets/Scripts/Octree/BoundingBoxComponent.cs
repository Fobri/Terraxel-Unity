using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoundingBoxComponent : MonoBehaviour
{
    [SerializeField] public BoundingBox box;

    private void OnDrawGizmos()
    {
        Gizmos.DrawCube(transform.position, box.bounds);
    }

    private void Awake()
    {
        box.center = transform.position;
    }

    protected void Update()
    {
        box.center = transform.position;
    }
}
