using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "World Data", menuName = "Terraxel/World Data", order = 1)]
public class WorldData : ScriptableObject
{
    public BiomeData[] biomes;
}
