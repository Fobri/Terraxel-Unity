using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Terraxel.DataStructures;
using System.IO;

[CreateAssetMenu(fileName = "World Data", menuName = "Terraxel/World Data", order = 1)]
public class WorldData : ScriptableObject
{
    public BiomeData[] biomes;

    public BiomeData GetBiomeData(int biomeIndex){
        return biomes[biomeIndex];
    }

    public void Generate(){
        TextAsset templateTextFile = AssetDatabase.LoadAssetAtPath("Assets/Resources/Generated/Templates/BiomeTemplate.txt", typeof(TextAsset)) as TextAsset;
		if(templateTextFile == null){
			throw new System.Exception("Template text file for code generation missing");
		}
		string fullGeneratorString = templateTextFile.text;
        string dataStrings = "";
        for(int i = 0; i < biomes.Length; i++){
            dataStrings += biomes[i].GetGeneratorString();
        }
        int numLines = 5;//dataStrings.Length - dataStrings.Replace(System.Environment.NewLine, string.Empty).Length;
        string getter = "";
        for(int i = 0; i < numLines; i++){
            getter += "case "+i+": return data"+i+";";
        }
        fullGeneratorString = fullGeneratorString.Replace("MEMBERS_HERE", dataStrings);
        fullGeneratorString = fullGeneratorString.Replace("GETTER_HERE", getter);

        using(StreamWriter sw = new StreamWriter(string.Format(Application.dataPath + "/Resources/Generated/BiomesGenerated.cs"))) {
            sw.Write(fullGeneratorString);
        }
    }
}
