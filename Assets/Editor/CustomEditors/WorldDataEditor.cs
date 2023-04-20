 using System.Collections;
 using System.Collections.Generic;
 using UnityEditor;
 using UnityEngine;
 
 [CustomEditor(typeof(WorldData))]
 public class WorldDataEditor : Editor
 {
     public override void OnInspectorGUI()
     {
         base.OnInspectorGUI();
         var script = (WorldData)target;
 
             if(GUILayout.Button("Compile", GUILayout.Height(40)))
             {
                 script.Generate();
             }
         
     }
 }