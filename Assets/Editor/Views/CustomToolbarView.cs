using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GraphProcessor;
using Status = UnityEngine.UIElements.DropdownMenuAction.Status;

public class CustomToolbarView : ToolbarView
{
	public CustomToolbarView(BaseGraphView graphView) : base(graphView) {}

    public static bool shouldCompileGraph;

	protected override void AddButtons()
	{
		// Add the hello world button on the left of the toolbar
		AddButton("Compile", CompileGraph, left: true);
		AddButton("Open generated script", OpenScript, left: false);

		// add the default buttons (center, show processor and show in project)
		base.AddButtons();
	}
    void CompileGraph(){
        shouldCompileGraph = true;
    }
	void OpenScript(){
		AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath("Assets/Generated/TerraxelGenerated.cs"));
	}
}