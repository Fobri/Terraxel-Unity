using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Terraxel.Editor;
public class WorldDataEditor : EditorWindow
{
    [MenuItem("Tools/Terraxel/Editor")]
    public static void ShowEditor(){
        EditorWindow window = GetWindow<WorldDataEditor>();
        window.titleContent = new GUIContent("Terraxel editor");
    }
    public void CreateGUI(){
    }
}
