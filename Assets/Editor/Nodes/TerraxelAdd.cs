using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using Terraxel.DataStructures;

[System.Serializable, NodeMenuItem("Math/TerraxelAddNode")]
public class TerraxelAdd : TerraxelPreviewNode
{
	[Input(name = "A"), ShowAsDrawer]
    public float                A;
	[Input(name = "B"), ShowAsDrawer]
    public float                B;
	private NoiseGraphInput dataA;
	private NoiseGraphInput dataB;

	public override string		name => "TerraxelAddNode";

	protected override void Process()
	{
		if(dataA != null){
			if(dataB != null){
				for(int i = 0; i< values.previewValues.Length; i++){
					values[i] = dataA[i] + dataB[i];
				}
			}else{
				for(int i = 0; i< values.previewValues.Length; i++){
					values[i] = dataA[i] + B;
				}
			}
		}else if(dataB != null){
			for(int i = 0; i< values.previewValues.Length; i++){
				values[i] = dataB[i] + A;
			}
		}else return;
		values.scriptGenerator.functions = "float op"+base.computeOrder.ToString()+" = ("+(dataA != null ? dataA.scriptGenerator.body : A)+" + "+(dataB != null ? dataB.scriptGenerator.body : B)+");" + System.Environment.NewLine;
		if(dataA != null){
			values.scriptGenerator.functions = dataA.scriptGenerator.functions + values.scriptGenerator.functions;
		}
		if(dataB != null){
			values.scriptGenerator.functions = dataB.scriptGenerator.functions + values.scriptGenerator.functions;
		}
		values.scriptGenerator.body = "op"+base.computeOrder.ToString();
		values.scriptGenerator.properties = dataA != null ? dataA.scriptGenerator.properties : "" + dataB != null ? dataB.scriptGenerator.properties : "";
		
		values.computeGenerator.functions = "float op"+base.computeOrder.ToString()+" = ("+(dataA != null ? dataA.computeGenerator.body : A)+" + "+(dataB != null ? dataB.computeGenerator.body : B)+");" + System.Environment.NewLine;
		if(dataA != null){
			values.computeGenerator.functions = dataA.computeGenerator.functions + values.computeGenerator.functions;
		}
		if(dataB != null){
			values.computeGenerator.functions = dataB.computeGenerator.functions + values.computeGenerator.functions;
		}
		values.computeGenerator.body = "op"+base.computeOrder.ToString();
		values.computeGenerator.properties = dataA != null ? dataA.computeGenerator.properties : "" + dataB != null ? dataB.computeGenerator.properties : "";
	}
	[CustomPortInput(nameof(A), typeof(NoiseGraphInput))]
	void PullA(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			dataA = null;
			return;
		}
		dataA = new NoiseGraphInput();
		var buffer = ((NoiseGraphInput)inputEdges.First().passThroughBuffer);
		dataA.previewValues = buffer.previewValues;
		dataA.scriptGenerator = buffer.scriptGenerator;
		dataA.computeGenerator = buffer.computeGenerator;
	}
	[CustomPortInput(nameof(B), typeof(NoiseGraphInput))]
	void PullB(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			dataB = null;
			return;
		}
		dataB = new NoiseGraphInput();
		var buffer = ((NoiseGraphInput)inputEdges.First().passThroughBuffer);
		dataB.previewValues = buffer.previewValues;
		dataB.scriptGenerator = buffer.scriptGenerator;
		dataB.computeGenerator = buffer.computeGenerator;
	}
	[CustomPortOutput(nameof(output), typeof(NoiseGraphInput))]
	void PushOutputs(List< SerializableEdge > connectedEdges)
	{
		for (int i = 0; i < connectedEdges.Count; i++)
			connectedEdges[i].passThroughBuffer = values;
	}
}
