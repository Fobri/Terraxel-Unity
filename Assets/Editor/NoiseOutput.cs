using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using Unity.Mathematics;
using WorldGeneration.DataStructures;
using UnityEditor;
using System.IO;

[System.Serializable, NodeMenuItem("Density/Output")]
public class NoiseOutput : BaseNode
{
	[Input(name = "In")]
    public float                input;
	private NoiseGraphInput data;

	public override string		name => "Output";

	protected override void Process()
	{
		if(data == null || !CustomToolbarView.shouldCompileGraph) return;
		CustomToolbarView.shouldCompileGraph = false;
		TextAsset templateTextFile = AssetDatabase.LoadAssetAtPath("Assets/Generated/Templates/DensityJobTemplate.txt", typeof(TextAsset)) as TextAsset;
		if(templateTextFile == null){
			throw new System.Exception("Template text file for density job code generation missing");
		}
		data.generatorString = "DensityGenerator.HeightMapToIsosurface(pos, TerraxelGenerated.GenerateDensity(pos2D))";
		data.generatorString += ";";
		data.generator2DString += ";";
		string fullGeneratorString = templateTextFile.text;
		fullGeneratorString = fullGeneratorString.Replace("DENSITY2D_FUNCTION_HERE", data.generator2DString);
		fullGeneratorString = fullGeneratorString.Replace("DENSITY_FUNCTION_HERE", data.generatorString);
		using(StreamWriter sw = new StreamWriter(string.Format(Application.dataPath + "/Generated/TerraxelGenerated.cs"))) {
            sw.Write(fullGeneratorString);
        }
        //Refresh the Asset Database
        AssetDatabase.Refresh();
	}
	[CustomPortInput(nameof(input), typeof(NoiseGraphInput))]
	void PullX(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			data = null;
			return;
		}
		data = new NoiseGraphInput();
		var buffer = ((NoiseGraphInput)inputEdges.First().passThroughBuffer);
		data.previewValues = buffer.previewValues;
		data.generatorString = buffer.generatorString;
		data.generator2DString = buffer.generator2DString;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
}
