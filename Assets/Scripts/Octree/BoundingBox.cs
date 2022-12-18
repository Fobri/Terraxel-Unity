using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct BoundingBox
{
    public BoundingBox(Vector3 center, Vector3 bounds)
    {
        this.center = center;
        this.bounds = bounds;
    }

    public void Draw()
    {
        Gizmos.DrawWireCube(center, bounds);
    }

    public Vector3 center;
    public Vector3 bounds;

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
