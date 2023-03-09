using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using Unity.Mathematics;

[System.Serializable, NodeMenuItem("Density/Output")]
public class NoiseOutput : BaseNode
{
	[Input(name = "In")]
    public string                input;

	public override string		name => "Output";



	protected override void Process()
	{
		Debug.Log(input);
	}
}
