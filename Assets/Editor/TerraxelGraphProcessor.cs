using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GraphProcessor;
using UnityEditor.Callbacks;
using System.IO;

public class TerraxelGraphProcessor
{
    [MenuItem("Assets/Create/Terraxel/Graph", false, 10)]
	public static void CreateGraphPorcessor()
	{
		var graph = ScriptableObject.CreateInstance< BaseGraph >();
		ProjectWindowUtil.CreateAsset(graph, "TerraxelGraph.asset");
	}

	[OnOpenAsset(0)]
	public static bool OnBaseGraphOpened(int instanceID, int line)
	{
		var asset = EditorUtility.InstanceIDToObject(instanceID) as BaseGraph;

		if (asset != null)
		{
			EditorWindow.GetWindow<DensityGraphWindow>().InitializeGraph(asset as BaseGraph);
			return true;
		}
		return false;
	}
}
