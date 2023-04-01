using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using Unity.Mathematics;
using Terraxel.DataStructures;
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
	[Input(name = "Lacunarity"), ShowAsDrawer]
	public float lacunarity = 2;
	
	[Input(name = "Gain"), ShowAsDrawer]
	public float gain = 2;
	[Input(name = "Analytical derivative"), ShowAsDrawer]
	public bool ad;
	NoiseGraphInput[] inputValues;

	public override string		name => "Noise";
	private NoiseProperties noiseProperties;
	//public static int2 curIndex = 0;

	protected override void Enable(){
		base.Enable();
		UpdateNoiseProperties();
		inputValues = new NoiseGraphInput[5];
		base.onAfterEdgeDisconnected += EdgeUpdate;
		//Process();
	}
	void EdgeUpdate(SerializableEdge edg){
		
		inputValues = new NoiseGraphInput[5];
	}
	public override float PreviewAmplitude{
		get{
			return noiseProperties.ampl;
		}
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
					multiplier.x = inputValues[0][y * 128 + x] * 24;
				}
				if(inputValues[1] != null){
					multiplier.y = inputValues[1][y * 128 + x] * 24;
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
				noiseProperties.gain = gain;
				noiseProperties.lacunarity = lacunarity;
				var value = (DensityGenerator.SurfaceNoise2D(new float2(x,y) + multiplier, noiseProperties));
				values[y * 128 + x] = value;
			}
		}
		var _x = inputValues[0] != null ? inputValues[0].scriptGenerator.body : Utils.floatToString(x);
		var _y = inputValues[1] != null ? inputValues[1].scriptGenerator.body : Utils.floatToString(y);
		var _x2d = inputValues[0] != null ? inputValues[0].computeGenerator.body : Utils.floatToString(x);
		var _y2d = inputValues[1] != null ? inputValues[1].computeGenerator.body : Utils.floatToString(y);
		string scriptProperties = "static readonly FastNoiseLite props"+base.computeOrder.ToString()+" = new FastNoiseLite(1337, "+(inputValues[2] != null ? inputValues[2].scriptGenerator.body : Utils.floatToString(frequency * 0.0002f))+", "+(inputValues[4] != null ? inputValues[4].scriptGenerator.body : octaves)+", "+Utils.floatToString(lacunarity)+", "+Utils.floatToString(gain)+");" + System.Environment.NewLine;
		for(int i = 0; i < 5; i++){
			if(inputValues[i] != null && inputValues[i].scriptGenerator.properties != "") scriptProperties = inputValues[i].scriptGenerator.properties + scriptProperties;
		}
		values.scriptGenerator.properties = scriptProperties;
		string scriptFunctions = "float op"+base.computeOrder.ToString()+" = DensityGenerator.SurfaceNoise2D(pos2D"+ ((_x != "0.0000f" || _y != "0.0000f") ? " + new float2("+_x+","+_y+")" : "") +
									", "+(inputValues[3] != null ? inputValues[3].scriptGenerator.body : Utils.floatToString(amplitude * 24))+", props"+base.computeOrder.ToString()+", seed);" + System.Environment.NewLine;
		for(int i = 0; i < 5; i++){
			if(inputValues[i] != null && inputValues[i].scriptGenerator.functions != "") scriptFunctions = inputValues[i].scriptGenerator.functions + scriptFunctions;
		}
		values.scriptGenerator.functions = scriptFunctions;
		values.scriptGenerator.body = "op"+base.computeOrder.ToString();
	    
		string computeProperties = "static const fnl_state props"+base.computeOrder.ToString()+" = fnlCreateState(seed, "+(inputValues[2] != null ? inputValues[2].computeGenerator.body : Utils.floatToString(frequency * 0.0002f))+", "+(inputValues[4] != null ? inputValues[4].computeGenerator.body : octaves)+", "+Utils.floatToString(lacunarity)+", "+Utils.floatToString(gain)+");" + System.Environment.NewLine;
		for(int i = 0; i < 5; i++){
			if(inputValues[i] != null && inputValues[i].computeGenerator.properties != "") computeProperties = inputValues[i].computeGenerator.properties + computeProperties;
		}
		values.computeGenerator.properties = computeProperties;
		string computeFunctions = "float op"+base.computeOrder.ToString()+" = noise(pos.xz"+ ((_x2d != "0.0000f" || _y2d != "0.0000f") ? " + float2("+_x2d+","+_y2d+")" : "") +
									", "+(inputValues[3] != null ? inputValues[3].computeGenerator.body : Utils.floatToString(amplitude * 24))+", props"+base.computeOrder.ToString()+");" + System.Environment.NewLine;
		for(int i = 0; i < 5; i++){
			if(inputValues[i] != null && inputValues[i].computeGenerator.functions != "") computeFunctions = inputValues[i].computeGenerator.functions + computeFunctions;
		}
		values.computeGenerator.functions = computeFunctions;
		values.computeGenerator.body = "op"+base.computeOrder.ToString();
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
		inputValues[0].scriptGenerator = buffer.scriptGenerator;
		inputValues[0].computeGenerator = buffer.computeGenerator;
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
		inputValues[1].scriptGenerator = buffer.scriptGenerator;
		inputValues[1].computeGenerator = buffer.computeGenerator;
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
		inputValues[2].scriptGenerator = buffer.scriptGenerator;
		inputValues[2].computeGenerator = buffer.computeGenerator;
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
		inputValues[3].scriptGenerator = buffer.scriptGenerator;
		inputValues[3].computeGenerator = buffer.computeGenerator;
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
		inputValues[4].scriptGenerator = buffer.scriptGenerator;
		inputValues[4].computeGenerator = buffer.computeGenerator;
		//values.AddRange(inputEdges.Select(e => e.passThroughBuffer).ToList());
	}
	[CustomPortOutput(nameof(output), typeof(NoiseGraphInput))]
	void PushOutputs(List< SerializableEdge > connectedEdges)
	{
		for (int i = 0; i < connectedEdges.Count; i++)
			connectedEdges[i].passThroughBuffer = values;
	}
}
