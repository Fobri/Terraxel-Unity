using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Terraxel;
using Unity.Mathematics;
using Unity.Collections;
using Terraxel.DataStructures;
using Unity.Jobs;
using UnityEngine.Rendering;
using System;
using System.Linq;
using UnityEditor;
using TMPro;
using Unity.Profiling;

[CreateAssetMenu(fileName = "World Settings", menuName = "Terraxel/World Settings", order = 0), System.Serializable]
public class TerraxelWorldSettings : ScriptableObject
{

    public bool placePlayerOnSurface = true;
    public int seed;
    public bool renderGrass;
    public bool frustumCulling;
    public WorldData generator;
}