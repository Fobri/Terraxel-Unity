using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Terraxel.DataStructures;

[CreateAssetMenu(fileName = "Biome Data", menuName = "Terraxel/Biome", order = 1), System.Serializable]
public class BiomeData : ScriptableObject
{
    public string biomeName;
    public InstancingData[] instances;

    public JobInstancingData jobInstances;

    public void Init(){
        jobInstances = JobInstancingData.CreateFromInstancingData(instances);
    }
}
