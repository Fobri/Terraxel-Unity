using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using GraphProcessor;
using Unity.Mathematics;

[NodeCustomEditor(typeof(TerraxelPreviewNode))]
public class DensityView : BaseNodeView
{
	Texture2D image;

	public override void Enable()
	{
		base.Enable();
        // Create your fields using node's variables and add them to the controlsContainer
		var node = nodeTarget as TerraxelPreviewNode;
		node.onProcessed += RegenImage;
		RegenImage();
	}
	/*public override void Disable(){
		base.Disable();
		var node = nodeTarget as TerraxelPreviewNode;
		node.onProcessed -= RegenImage;
	}*/
	void RegenImage(){
		controlsContainer.Clear();
		debugContainer.Clear();
		/*var btn = new Button(RegenImage);
		btn.text = "Update";
		controlsContainer.Add(btn);*/
		var node = nodeTarget as TerraxelPreviewNode;
		if(image == null) image = new Texture2D(128, 128);
		for(int x = 0; x < 128; x++){
			for(int y = 0; y < 128; y++){
				var value = node.values[128 * y + x];
				image.SetPixel(x, y, new Color(value, value, value));
			}
		}
		image.Apply();
		var img = new Image();
		img.image = image;
		controlsContainer.Add(img);
		debugContainer.Add(new Label(node.values.generatorString));
	}
}