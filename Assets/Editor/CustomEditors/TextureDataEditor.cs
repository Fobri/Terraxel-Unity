 using System.Collections;
 using System.Collections.Generic;
 using UnityEditor;
 using UnityEngine;
 
 [CustomEditor(typeof(TextureData))]
 public class TestScriptableEditor : Editor
 {
     public override void OnInspectorGUI()
     {
         base.OnInspectorGUI();
         var script = (TextureData)target;
 
             if(GUILayout.Button("Generate TextureArray", GUILayout.Height(40)))
             {
                 script.GenerateTextureArray();
             }
         
     }
 }