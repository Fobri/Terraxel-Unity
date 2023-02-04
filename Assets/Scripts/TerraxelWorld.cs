using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using WorldGeneration.DataStructures;

public class TerraxelWorld : MonoBehaviour
{
    void Update(){
        JobRunner.Update();
    }
}