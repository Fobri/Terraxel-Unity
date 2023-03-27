using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using Unity.Mathematics;
using Terraxel.DataStructures;
using UnityEditor;
using System.IO;
using System;
using System.Text;

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
		CreateFromTemplate(data.computeGenerator, "ComputeTemplate.txt", "TerraxelGenerated.compute");
		CreateFromTemplate(data.scriptGenerator, "ScriptTemplate.txt", "TerraxelGenerated.cs");
        AssetDatabase.Refresh();
	}
	static void CreateFromTemplate(GeneratorString generatorString, string templateFile, string outputFile){
		TextAsset templateTextFile = AssetDatabase.LoadAssetAtPath("Assets/Generated/Templates/" + templateFile, typeof(TextAsset)) as TextAsset;
		if(templateTextFile == null){
			throw new System.Exception("Template text file for code generation missing");
		}
		string fullGeneratorString = templateTextFile.text;
		fullGeneratorString = fullGeneratorString.Replace("DENSITY_FUNCTION_HERE", generatorString.body +";");
		fullGeneratorString = fullGeneratorString.Replace("PROPS_HERE", RemoveDuplicateLines(generatorString.properties));
		fullGeneratorString = fullGeneratorString.Replace("FUNCTIONS_HERE", RemoveDuplicateLines(generatorString.functions));
		using(StreamWriter sw = new StreamWriter(string.Format(Application.dataPath + "/Generated/" + outputFile))) {
            sw.Write(fullGeneratorString);
        }
	}
	static string RemoveDuplicateLines(string input){
		string[] lines = input.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

		HashSet<string> uniqueLines = new HashSet<string>();
		foreach (string line in lines)
		{
			uniqueLines.Add(line);
		}

		StringBuilder sb = new StringBuilder();
		foreach (string line in uniqueLines)
		{
			sb.Append(line);
			sb.Append(Environment.NewLine);
		}
		return sb.ToString();
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
		data.scriptGenerator = buffer.scriptGenerator;
		data.computeGenerator = buffer.computeGenerator;
	}
}
