using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;

[System.Serializable, NodeMenuItem("Conversions/Vector2")]
public class Vector2Node : BaseNode
{
	[Input(name = "X"), SerializeField]
    public float                x;
	[Input(name = "Y"), SerializeField]
    public float                y;

	[Output(name = "Out")]
	public Vector2				output;

	public override string		name => "Vector2";

	protected override void Process()
	{
	    output = new Vector2(x, y);
	}
}
