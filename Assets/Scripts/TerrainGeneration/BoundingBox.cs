using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

[System.Serializable]
public struct BoundingBox
{
    public BoundingBox(Vector3 center, Vector3 bounds)
    {
        this._center = center;
        this._bounds = bounds;
        cullRadius = 0f;
        CalculateCullRadius();
    }

    public void DebugDraw()
    {
        
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(center, bounds);
        //Gizmos.DrawSphere(center, cullRadius);
    }
    public float cullRadius;
    private float3 _center;
    public float3 center{
        get{
            return _center;
        }
        set{
            _center = value;
        }
    }
    public float3 _bounds;
    public float3 bounds{
        get{
            return _bounds;
        }set{
            _bounds = value;
            CalculateCullRadius();
        }
    }
    private void CalculateCullRadius(){
        cullRadius = math.sqrt(math.pow(_bounds.x / 2, 2) + math.pow(_bounds.y / 2, 2) + math.pow(bounds.z / 2, 2));
    }

    public bool IsColliding(BoundingBox other)
    {
        bool xCollision = Mathf.Abs(center.x - other.center.x) - ((bounds.x / 2) + (other.bounds.x / 2)) < 0;
        bool yCollision = Mathf.Abs(center.y - other.center.y) - ((bounds.y / 2) + (other.bounds.y / 2)) < 0;
        bool zCollision = Mathf.Abs(center.z - other.center.z) - ((bounds.z / 2) + (other.bounds.z / 2)) < 0;

        return xCollision && yCollision && zCollision;
    }

    //Returns true if other is fully inside of this bounding box
    public bool Contains(BoundingBox other)
    {
        bool xContains = (Mathf.Abs(center.x - other.center.x) + (other.bounds.x / 2 + bounds.x / 2)) < bounds.x;
        bool yContains = (Mathf.Abs(center.y - other.center.y) + (other.bounds.y / 2 + bounds.x / 2)) < bounds.y;
        bool zContains = (Mathf.Abs(center.z - other.center.z) + (other.bounds.z / 2 + bounds.x / 2)) < bounds.z;

        return xContains && yContains && zContains;
    }
    public bool Contains(float3 other)
    {
        bool xContains = (Mathf.Abs(center.x - other.x) + (bounds.x / 2)) < bounds.x;
        bool yContains = (Mathf.Abs(center.y - other.y) + (bounds.x / 2)) < bounds.y;
        bool zContains = (Mathf.Abs(center.z - other.z) + (bounds.x / 2)) < bounds.z;

        return xContains && yContains && zContains;
    }
    /*
    public static BoundingBox EncloseObjects(List<BoundingBox> boxes)
    {
        Vector3 positionAverage = Vector3.zero;

        float maxXBound = 0;
        float maxYBound = 0;
        float maxZBound = 0;

        foreach (BoundingBox b in boxes)
        {
            positionAverage += b.center;

            if (b.bounds.x > maxXBound)
                maxXBound = b.bounds.x;

            if (b.bounds.y > maxYBound)
                maxYBound = b.bounds.y;
            
            if (b.bounds.z > maxZBound)
                maxZBound = b.bounds.z;
        }

        positionAverage /= boxes.Count;

        return new BoundingBox(positionAverage, new Vector3(maxXBound, maxYBound, maxZBound));
    }
    */
}
