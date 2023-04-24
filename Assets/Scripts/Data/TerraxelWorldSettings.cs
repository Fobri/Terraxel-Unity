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
using System.IO;

[CreateAssetMenu(fileName = "World Settings", menuName = "Terraxel/World Settings", order = 0), System.Serializable]
public class TerraxelWorldSettings : ScriptableObject
{

    public bool placePlayerOnSurface = true;
    public int seed;
    [SerializeField]
    public bool renderGrass;
    [SerializeField]
    public bool frustumCulling;
    public WorldData generator;
    public ComputeShader noise2D, noise3D;

    //CONSTANTS
    [SerializeField]
    public int lodLevels;
    [SerializeField]
    public int maxGpuOperations;
    [SerializeField]
    public int densityCount;
    [SerializeField]
    public int uniformDensityRes;

    public void CompileConstants(){
        #if UNITY_EDITOR
        TextAsset templateTextFile = AssetDatabase.LoadAssetAtPath("Assets/Resources/Generated/Templates/ConstantTemplate.txt", typeof(TextAsset)) as TextAsset;
		if(templateTextFile == null){
			throw new System.Exception("Template text file for code generation missing");
		}
		string fullGeneratorString = templateTextFile.text;
        fullGeneratorString = fullGeneratorString.Replace("LOD_LEVELS", lodLevels.ToString());
        fullGeneratorString = fullGeneratorString.Replace("MAX_GPU", maxGpuOperations.ToString());
        fullGeneratorString = fullGeneratorString.Replace("DENSITY_COUNT", densityCount.ToString());
        fullGeneratorString = fullGeneratorString.Replace("UNIFORM_RES", uniformDensityRes.ToString());

        using(StreamWriter sw = new StreamWriter(string.Format(Application.dataPath + "/Resources/Generated/TerraxelConstants.cs"))) {
            sw.Write(fullGeneratorString);
        }
        AssetDatabase.Refresh();
        generator.Generate();
        AssetDatabase.Refresh();
        #endif
    }
    
    public void OnValidate(){
        #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        //AssetDatabase.SaveAssets();
        #endif
    }
}