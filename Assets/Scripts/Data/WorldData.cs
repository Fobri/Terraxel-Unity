using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Terraxel.DataStructures;

[CreateAssetMenu(fileName = "World Data", menuName = "Terraxel/World Data", order = 1)]
public class WorldData : ScriptableObject
{
    public BiomeData[] biomes;

    public void Init(){
        for(int i = 0; i < biomes.Length; i++){
            biomes[i].Init();
        }
    }

    public BiomeData GetBiomeData(int biomeIndex){
        return biomes[biomeIndex];
    }
}
