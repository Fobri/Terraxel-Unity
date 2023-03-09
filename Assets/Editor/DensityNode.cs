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
using System;

[System.Serializable, NodeMenuItem("Density/Noise")]
public class DensityNode : BaseNode
{
	[Input(name = "X"), ShowAsDrawer]
    public string               x = "1";
	[Input(name = "Y"), ShowAsDrawer]
    public string               y = "1";

	[Input(name = "Frequency"), ShowAsDrawer]
	public string frequency = "60";
	[Input(name = "Amplitude"), ShowAsDrawer]
	public string amplitude = "1";
	[Input(name = "Octaves"), ShowAsDrawer]
	public string octaves = "2";
	float[][] inputValues;

	[Output(name = "Out")]
	public string				output;

	public override string		name => "Noise";
	private NoiseProperties noiseProperties;

	[HideInInspector]
	public float[] values;
	//public static int2 curIndex = 0;

	private float _x;
	private float _y;
	private float _frequency;
	private float _amplitude;
	private int _octaves;

	protected override void Enable(){
		UpdateNoiseProperties();
		inputValues = new float[5][];
		values = new float[128*128];
	}
	protected override void Process()
	{
		_x = float.Parse(x);
		_y = float.Parse(y);
		_frequency = float.Parse(frequency);
		_amplitude = float.Parse(amplitude);
		_octaves = int.Parse(octaves);
		UpdateNoiseProperties();
		//var value = DensityGenerator.SurfaceNoise2D(input + new Vector2(curIndex.x, curIndex.y), noiseProperties);
		//output = value;
		//values[curIndex.y * 128 + curIndex.x] = value;
		for(int x = 0; x < 128; x++){
			for(int y = 0; y < 128; y++){
				float2 multiplier = new float2(_x,_y);
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
				var value = (DensityGenerator.SurfaceNoise2D(new float2(x,y) * multiplier, noiseProperties) + 1) / 2;
				values[y * 128 + x] = value;
			}
		}
		output = $"DensityGenerator.SurfaceNoise2D(new float2(x, y) * new float2({x},{y}), {amplitude}, {frequency}, 300, {octaves});";
	    //outputs = DensityGenerator.SurfaceNoise2D(input, noiseProperties);
	}
	void UpdateNoiseProperties(){
		if(noiseProperties.Equals(default)) noiseProperties = new NoiseProperties();
		noiseProperties.ampl = _amplitude;
		noiseProperties.freq = _frequency * 0.001f;
		noiseProperties.oct = _octaves;
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
	[CustomPortInput(nameof(x), typeof(string))]
	void PullX(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			inputValues[0] = null;
			return;
		}
		inputValues[0] = (float[])inputEdges[0].passThroughBuffer;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortInput(nameof(y), typeof(string))]
	void PullY(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			inputValues[1] = null;
			return;
		}
		inputValues[1] = (float[])inputEdges[0].passThroughBuffer;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortInput(nameof(frequency), typeof(string))]
	void PullFrequency(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			inputValues[2] = null;
			return;
		}
		inputValues[2] = (float[])inputEdges[0].passThroughBuffer;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortInput(nameof(amplitude), typeof(string))]
	void PullAmplitude(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			inputValues[3] = null;
			return;
		}
		inputValues[3] = (float[])inputEdges[0].passThroughBuffer;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortInput(nameof(octaves), typeof(string))]
	void PullOctaves(List< SerializableEdge > inputEdges)
	{
		if(inputEdges.Count == 0){
			inputValues[4] = null;
			return;
		}
		inputValues[4] = (float[])inputEdges[0].passThroughBuffer;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortOutput(nameof(output), typeof(string))]
	void PushOutputs(List< SerializableEdge > connectedEdges)
	{
		// Values length is supposed to match connected edges length
		for (int i = 0; i < connectedEdges.Count; i++)
			connectedEdges[i].passThroughBuffer = values;
			
		// once the outputs are pushed, we don't need the inputs data anymore
		//values.Clear();
	}
}
