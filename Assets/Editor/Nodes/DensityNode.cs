using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using Unity.Mathematics;
using WorldGeneration.DataStructures;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

[System.Serializable, NodeMenuItem("Density/Noise")]
public class DensityNode : TerraxelPreviewNode
{
	[Input(name = "X"), ShowAsDrawer]
    public float               x = 1;
	[Input(name = "Y"), ShowAsDrawer]
    public float               y = 1;

	[Input(name = "Frequency"), ShowAsDrawer]
	public float frequency = 60;
	[Input(name = "Amplitude"), ShowAsDrawer]
	public float amplitude = 1;
	[Input(name = "Octaves"), ShowAsDrawer]
	public int octaves = 2;
	NoiseGraphInput[] inputValues;

	public override string		name => "Noise";
	private NoiseProperties noiseProperties;
	//public static int2 curIndex = 0;

	protected override void Enable(){
		base.Enable();
		UpdateNoiseProperties();
		inputValues = new NoiseGraphInput[5];
		Process();
	}
	protected override void Process()
	{
		UpdateNoiseProperties();
		//var value = DensityGenerator.SurfaceNoise2D(input + new Vector2(curIndex.x, curIndex.y), noiseProperties);
		//output = value;
		//values[curIndex.y * 128 + curIndex.x] = value;
		for(int x = 0; x < 128; x++){
			for(int y = 0; y < 128; y++){
				float2 multiplier = new float2(this.x,this.y);
				if(inputValues[0] != null){
					multiplier.x = inputValues[0][y * 128 + x];
				}
				if(inputValues[1] != null){
					multiplier.y = inputValues[1][y * 128 + x];
				}
				if(inputValues[2] != null){
					noiseProperties.freq = inputValues[2][y * 128 + x];
				}
				if(inputValues[3] != null){
					noiseProperties.ampl = inputValues[3][y * 128 + x];
				}
				if(inputValues[4] != null){
					noiseProperties.oct = (int)inputValues[4][y * 128 + x];
				}
				var value = (DensityGenerator.SurfaceNoise2D(new float2(x,y) + multiplier, noiseProperties) + 1) / 2;
				values[y * 128 + x] = value;
			}
		}
		var seed = new Unity.Mathematics.Random((uint)TerraxelWorld.seed);
		values.generatorString = "DensityGenerator.FinalNoise(pos + new float3("+(inputValues[0] != null ? inputValues[0].generatorString : Utils.floatToString(x))+",0,"+(inputValues[1] != null ? inputValues[1].generatorString : Utils.floatToString(y))+
									"), "+(inputValues[3] != null ? inputValues[3].generatorString : Utils.floatToString(amplitude))+", "+(inputValues[2] != null ? inputValues[2].generatorString : Utils.floatToString(frequency * 0.0001f))+", "+seed.NextInt(-1_000_000, 1_000_000)+", "+(inputValues[4] != null ? inputValues[4].generatorString : octaves)+",11)";
	    //outputs = DensityGenerator.SurfaceNoise2D(input, noiseProperties);
	}
	void UpdateNoiseProperties(){
		if(noiseProperties.Equals(default)) noiseProperties = new NoiseProperties();
		noiseProperties.ampl = amplitude;
		noiseProperties.freq = frequency * 0.001f;
		noiseProperties.oct = octaves;
		noiseProperties.seed = 300;
		noiseProperties.surfaceLevel = 0;
	}
	/*[CustomPortBehavior(nameof(inputs))]
	IEnumerable< PortData > ListPortBehavior(List< SerializableEdge > edges)
	{
		yield return new PortData {
			displayName = "Offset",
			displayType = typeof(float),
			identifier = "Offset"
		};
		yield return new PortData {
			displayName = "Frequency",
			displayType = typeof(float),
			identifier = "Frequency"
		};
		yield return new PortData {
			displayName = "Amplitude",
			displayType = typeof(float),
			identifier = "Amplitude"
		};
		yield return new PortData {
			displayName = "Octaves",
			displayType = typeof(float),
			identifier = "Octaves"
		};
		yield return new PortData {
			displayName = "Seed",
			displayType = typeof(float),
			identifier = "Seed"
		};
	}*/
	// This function will be called once per port created from the `inputs` custom port function
	// will in parameter the list of the edges connected to this port
	[CustomPortInput(nameof(x), typeof(NoiseGraphInput))]
	void PullX(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			inputValues[0] = null;
			return;
		}
		inputValues[0] = new NoiseGraphInput();
		var buffer = ((NoiseGraphInput)inputEdges.First().passThroughBuffer);
		inputValues[0].previewValues = buffer.previewValues;
		inputValues[0].generatorString = buffer.generatorString;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortInput(nameof(y), typeof(NoiseGraphInput))]
	void PullY(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			inputValues[0] = null;
			return;
		}
		inputValues[1] = new NoiseGraphInput();
		var buffer = ((NoiseGraphInput)inputEdges.First().passThroughBuffer);
		inputValues[1].previewValues = buffer.previewValues;
		inputValues[1].generatorString = buffer.generatorString;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortInput(nameof(frequency), typeof(NoiseGraphInput))]
	void PullFrequency(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			inputValues[0] = null;
			return;
		}
		
		inputValues[2] = new NoiseGraphInput();
		var buffer = ((NoiseGraphInput)inputEdges.First().passThroughBuffer);
		inputValues[2].previewValues = buffer.previewValues;
		inputValues[2].generatorString = buffer.generatorString;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortInput(nameof(amplitude), typeof(NoiseGraphInput))]
	void PullAmplitude(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			inputValues[0] = null;
			return;
		}
		
		inputValues[3] = new NoiseGraphInput();
		var buffer = ((NoiseGraphInput)inputEdges.First().passThroughBuffer);
		inputValues[3].previewValues = buffer.previewValues;
		inputValues[3].generatorString = buffer.generatorString;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortInput(nameof(octaves), typeof(NoiseGraphInput))]
	void PullOctaves(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			inputValues[0] = null;
			return;
		}
		
		inputValues[4] = new NoiseGraphInput();
		var buffer = ((NoiseGraphInput)inputEdges.First().passThroughBuffer);
		inputValues[4].previewValues = buffer.previewValues;
		inputValues[4].generatorString = buffer.generatorString;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortOutput(nameof(output), typeof(NoiseGraphInput))]
	void PushOutputs(List< SerializableEdge > connectedEdges)
	{
		for (int i = 0; i < connectedEdges.Count; i++)
			connectedEdges[i].passThroughBuffer = values;
	}
}