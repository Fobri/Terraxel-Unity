using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using WorldGeneration.DataStructures;

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
		values.generatorString = "("+(dataA != null ? dataA.generatorString : A)+" + "+(dataB != null ? dataB.generatorString : B)+")";
		values.generator2DString = "("+(dataA != null ? dataA.generator2DString : A)+" + "+(dataB != null ? dataB.generator2DString : B)+")";
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
		dataA.generatorString = buffer.generatorString;
		dataA.generator2DString = buffer.generator2DString;
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
		dataB.generatorString = buffer.generatorString;
		dataB.generator2DString = buffer.generator2DString;
	}
	[CustomPortOutput(nameof(output), typeof(NoiseGraphInput))]
	void PushOutputs(List< SerializableEdge > connectedEdges)
	{
		for (int i = 0; i < connectedEdges.Count; i++)
			connectedEdges[i].passThroughBuffer = values;
	}
}
