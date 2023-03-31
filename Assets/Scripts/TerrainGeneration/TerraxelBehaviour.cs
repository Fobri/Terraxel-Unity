using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerraxelBehaviour : MonoBehaviour
{
    public TerraxelWorld worldSettings;
    public GameObject player;
    
    Transform poolParent;
    Transform activeParent;

    public void Start(){
        poolParent = new GameObject("Pooled Chunks").transform;
        poolParent.SetParent(transform);
        activeParent = new GameObject("Active Chunks").transform;
        activeParent.SetParent(transform);
        worldSettings.Init(poolParent, activeParent, player);
    }

    public void Update(){
        worldSettings.Run();
    }

    public void OnDisable(){
        worldSettings.Dispose();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        worldSettings.DebugDraw();
    }
#endif
}
