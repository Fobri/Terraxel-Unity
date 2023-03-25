using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using Unity.Mathematics;
using Terraxel.DataStructures;
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
		CreateFromTemplate(data.computeString, "ComputeTemplate.txt", "TerraxelGenerated.compute");
		CreateFromTemplate(data.scriptString, "ScriptTemplate.txt", "TerraxelGenerated.cs");
        AssetDatabase.Refresh();
	}
	static void CreateFromTemplate(string contents, string templateFile, string outputFile){
		TextAsset templateTextFile = AssetDatabase.LoadAssetAtPath("Assets/Generated/Templates/" + templateFile, typeof(TextAsset)) as TextAsset;
		if(templateTextFile == null){
			throw new System.Exception("Template text file for code generation missing");
		}
		contents += ";";
		string fullGeneratorString = templateTextFile.text;
		fullGeneratorString = fullGeneratorString.Replace("DENSITY_FUNCTION_HERE", contents);
		using(StreamWriter sw = new StreamWriter(string.Format(Application.dataPath + "/Generated/" + outputFile))) {
            sw.Write(fullGeneratorString);
        }
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
		data.scriptString = buffer.scriptString;
		data.computeString = buffer.computeString;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
}
