using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TerraxelWorld))]
public class TerraxelWorldEditor : Editor
{
    SerializedProperty worldSettings;
    SerializedProperty playerObject;
    SerializedProperty debugMode;
    
    void OnEnable()
    {
        worldSettings = serializedObject.FindProperty("worldSettings");
        playerObject = serializedObject.FindProperty("player");
        debugMode = serializedObject.FindProperty("debugMode");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(worldSettings);
        EditorGUILayout.PropertyField(playerObject);
        EditorGUILayout.PropertyField(debugMode);
        serializedObject.ApplyModifiedProperties();
        if(debugMode.boolValue){
            base.OnInspectorGUI();
        }
    }
}
