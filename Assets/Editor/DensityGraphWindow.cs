using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GraphProcessor;

public class DensityGraphWindow : BaseGraphWindow
{
	BaseGraph	tmpGraph;
	CustomToolbarView	toolbarView;

	[MenuItem("Terraxel/GraphEditor")]
	public static BaseGraphWindow OpenWithTmpGraph()
	{
		var graphWindow = CreateWindow< DensityGraphWindow >();

		// When the graph is opened from the window, we don't save the graph to disk
		graphWindow.tmpGraph = ScriptableObject.CreateInstance<BaseGraph>();
		graphWindow.tmpGraph.hideFlags = HideFlags.HideAndDontSave;
		graphWindow.InitializeGraph(graphWindow.tmpGraph);

		graphWindow.Show();

		return graphWindow;
	}

	protected override void OnDestroy()
	{
		graphView?.Dispose();
		DestroyImmediate(tmpGraph);
	}

	protected override void InitializeWindow(BaseGraph graph)
	{
		titleContent = new GUIContent("Terraxel Editor");

		if (graphView == null){
			graphView = new BaseGraphView(this);
			toolbarView = new CustomToolbarView(graphView);
			graphView.Add(toolbarView);
		}

		rootView.Add(graphView);
	}
}
