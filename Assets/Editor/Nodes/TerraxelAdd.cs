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
		values.generatorScriptBody = "("+(dataA != null ? dataA.generatorScriptBody : A)+" + "+(dataB != null ? dataB.generatorScriptBody : B)+")";
		values.generatorComputeBody = "("+(dataA != null ? dataA.generatorComputeBody : A)+" + "+(dataB != null ? dataB.generatorComputeBody : B)+")";
		values.generatorComputeProperties = dataA.generatorComputeProperties + dataB.generatorComputeProperties;
		values.generatorScriptProperties = dataA.generatorScriptProperties + dataB.generatorScriptProperties;
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
		dataA.generatorScriptBody = buffer.generatorScriptBody;
		dataA.generatorComputeBody = buffer.generatorComputeBody;
		dataA.generatorScriptProperties = buffer.generatorScriptProperties;
		dataA.generatorComputeProperties = buffer.generatorComputeProperties;
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
		dataB.generatorScriptBody = buffer.generatorScriptBody;
		dataB.generatorComputeBody = buffer.generatorComputeBody;
		dataB.generatorScriptProperties = buffer.generatorScriptProperties;
		dataB.generatorComputeProperties = buffer.generatorComputeProperties;
	}
	[CustomPortOutput(nameof(output), typeof(NoiseGraphInput))]
	void PushOutputs(List< SerializableEdge > connectedEdges)
	{
		for (int i = 0; i < connectedEdges.Count; i++)
			connectedEdges[i].passThroughBuffer = values;
	}
}
