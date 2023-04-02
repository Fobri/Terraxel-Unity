using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;

public class TerraxelWindow : EditorWindow
{
    VisualElement m_rightPanel;
    [MenuItem("Terraxel/World Settings")]
    public static void ShowMyEditor()
    {
    // This method is called when the user selects the menu item in the Editor
    EditorWindow wnd = GetWindow<TerraxelWindow>();
    wnd.titleContent = new GUIContent("Terraxel");
    }
    public void CreateGUI(){
        // Create a two-pane view with the left pane being fixed with
        var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);

        // Add the view to the visual tree by adding it as a child to the root element
        rootVisualElement.Add(splitView);

        // A TwoPaneSplitView always needs exactly two child elements
        var leftPane = new ListView();
        splitView.Add(leftPane);
        m_rightPanel = new VisualElement();
        splitView.Add(m_rightPanel);
        string[] allObjects = new string[] {"General", "Biomes", "Objects"};
        leftPane.makeItem = () => {
            var label = new Label();
            return new Label();
        };
        leftPane.bindItem = (item, index) => { (item as Label).text = allObjects[index]; };
        leftPane.itemsSource = allObjects;
        leftPane.selectionChanged += ChangeRightWindow;
    }

    void ChangeRightWindow(IEnumerable<object> selected){
        m_rightPanel.Clear();
        var type = selected.First() as string;
        switch(type){
            case "General":
            DrawTerraxelSettingsWindow();
            break;
            case "Biomes":
            DrawTerraxelBiomesWindow();
            break;
            case "Objects":
            DrawTerraxelObjectsWindow();
            break;
            default:
            throw new System.Exception("Invalid window type");
        }
    }

    void DrawTerraxelSettingsWindow(){
        TerraxelWorldSettings worldSettings = Object.FindObjectOfType<TerraxelWorld>()?.worldSettings;
        if(worldSettings == null){
            m_rightPanel.Add(new Label("No Terraxel instance found in scene. Make sure you have a GameObject with the TerraxelBehaviour component and have assigned worldsettings."));
        }else{
            Label header = new Label("Settings");
            var style = header.style;
            style.fontSize = 35;
            style.alignSelf = Align.Center;
            m_rightPanel.Add(header);

            HelpBox seedHelp = new HelpBox("If seed is 0 a random seed is generated on startup", HelpBoxMessageType.Info);
            m_rightPanel.Add(seedHelp);
            IntegerField seedField = new IntegerField("Seed");
            seedField.value = worldSettings.seed;
            seedField.RegisterValueChangedCallback((change) => worldSettings.seed = change.newValue);
            m_rightPanel.Add(seedField);

            Toggle placePlayerOnSurface = new Toggle("Place player on surface");
            placePlayerOnSurface.value = worldSettings.placePlayerOnSurface;
            placePlayerOnSurface.RegisterValueChangedCallback((change) => worldSettings.placePlayerOnSurface = change.newValue);
            m_rightPanel.Add(placePlayerOnSurface);

            Toggle frustumCulling = new Toggle("Frustum culling");
            frustumCulling.value = worldSettings.frustumCulling;
            frustumCulling.RegisterValueChangedCallback((change) => worldSettings.frustumCulling = change.newValue);
            m_rightPanel.Add(frustumCulling);

            Toggle renderGrass = new Toggle("Enable Instanced renderer");
            renderGrass.value = worldSettings.renderGrass;
            renderGrass.RegisterValueChangedCallback((change) => worldSettings.renderGrass = change.newValue);
            m_rightPanel.Add(renderGrass);

            /*Toggle debugMode = new Toggle("Enable debug mode");
            debugMode.value = worldSettings.debugMode;
            debugMode.RegisterValueChangedCallback((change) => {worldSettings.debugMode = change.newValue; m_rightPanel.Clear(); DrawTerraxelSettingsWindow();});
            m_rightPanel.Add(debugMode);

            if(debugMode.value){
                var debugPrefab = new ObjectField("Debug Canvas Prefab");
                debugPrefab.allowSceneObjects = false;
                debugPrefab.objectType = typeof(GameObject);
                debugPrefab.value = worldSettings.debugCanvas;
                debugPrefab.RegisterValueChangedCallback((change) => worldSettings.debugCanvas = change.newValue as GameObject);
                m_rightPanel.Add(debugPrefab);
                Toggle drawPlayerBounds = new Toggle("Player bounds");
                drawPlayerBounds.value = worldSettings.drawPlayerBounds;
                drawPlayerBounds.RegisterValueChangedCallback((change) => worldSettings.drawPlayerBounds = change.newValue);
                m_rightPanel.Add(drawPlayerBounds);
                Toggle drawChunkBounds = new Toggle("Chunk bounds");
                drawChunkBounds.value = worldSettings.drawChunkBounds;
                drawChunkBounds.RegisterValueChangedCallback((change) => worldSettings.drawChunkBounds = change.newValue);
                m_rightPanel.Add(drawChunkBounds);
                Toggle drawDensityMaps = new Toggle("Density maps");
                drawDensityMaps.value = worldSettings.drawDensityMaps;
                drawDensityMaps.RegisterValueChangedCallback((change) => worldSettings.drawDensityMaps = change.newValue);
                m_rightPanel.Add(drawDensityMaps);
                Toggle drawPos = new Toggle("Position");
                drawPos.value = worldSettings.drawChunkVariables.position;
                drawPos.RegisterValueChangedCallback((change) => worldSettings.drawChunkVariables.position = change.newValue);
                m_rightPanel.Add(drawPos);
                Toggle drawchunkState = new Toggle("State");
                drawchunkState.value = worldSettings.drawChunkVariables.chunkState;
                drawchunkState.RegisterValueChangedCallback((change) => worldSettings.drawChunkVariables.chunkState = change.newValue);
                m_rightPanel.Add(drawchunkState);
                Toggle drawDepth = new Toggle("Depth");
                drawDepth.value = worldSettings.drawChunkVariables.depth;
                drawDepth.RegisterValueChangedCallback((change) => worldSettings.drawChunkVariables.depth = change.newValue);
                m_rightPanel.Add(drawDepth);
                Toggle drawGenTime = new Toggle("Generation time");
                drawGenTime.value = worldSettings.drawChunkVariables.genTime;
                drawGenTime.RegisterValueChangedCallback((change) => worldSettings.drawChunkVariables.genTime = change.newValue);
                m_rightPanel.Add(drawGenTime);
                Toggle drawVertexCount = new Toggle("Vertex count");
                drawVertexCount.value = worldSettings.drawChunkVariables.vertexCount;
                drawVertexCount.RegisterValueChangedCallback((change) => worldSettings.drawChunkVariables.vertexCount = change.newValue);
                m_rightPanel.Add(drawVertexCount);
                Toggle drawIndexCount = new Toggle("Index count");
                drawIndexCount.value = worldSettings.drawChunkVariables.indexCount;
                drawIndexCount.RegisterValueChangedCallback((change) => worldSettings.drawChunkVariables.indexCount = change.newValue);
                m_rightPanel.Add(drawIndexCount);
                Toggle drawDirMask = new Toggle("Direction mask");
                drawDirMask.value = worldSettings.drawChunkVariables.dirMask;
                drawDirMask.RegisterValueChangedCallback((change) => worldSettings.drawChunkVariables.dirMask = change.newValue);
                m_rightPanel.Add(drawDirMask);
                Toggle drawType = new Toggle("Type");
                drawType.value = worldSettings.drawChunkVariables.type;
                drawType.RegisterValueChangedCallback((change) => worldSettings.drawChunkVariables.type = change.newValue);
                m_rightPanel.Add(drawType);
            }*/
        }
    }
    void DrawTerraxelBiomesWindow(){

    }
    void DrawTerraxelObjectsWindow(){

    }
}
