using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using Terraxel.DataStructures;

[System.Serializable, NodeMenuItem("Custom/TerraxelPreviewNode")]
public class TerraxelPreviewNode : BaseNode
{
	[HideInInspector]
	public NoiseGraphInput values;

	[Output(name = "Out")]
	public float				output;

	protected override void Enable(){
		values = new NoiseGraphInput();
		values.previewValues = new float[128*128];
	}
}
