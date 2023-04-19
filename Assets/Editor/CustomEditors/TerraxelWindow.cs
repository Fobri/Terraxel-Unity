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
        TerraxelWorldSettings worldSettings = Object.FindObjectOfType<TerraxelWorld>()?.worldSettings;
        if(worldSettings == null){
            m_rightPanel.Add(new Label("No Terraxel instance found in scene. Make sure you have a GameObject with the TerraxelBehaviour component and have assigned worldsettings."));
            return;
        }
        switch(type){
            case "General":
            DrawTerraxelSettingsWindow(worldSettings);
            break;
            case "Biomes":
            DrawTerraxelBiomesWindow(worldSettings);
            break;
            case "Objects":
            DrawTerraxelObjectsWindow(worldSettings);
            break;
            default:
            throw new System.Exception("Invalid window type");
        }
    }

    void DrawTerraxelSettingsWindow(TerraxelWorldSettings worldSettings){
        DrawHeader("Settings");

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

        DrawHeader("Constants", 25);
        HelpBox constantsHelp = new HelpBox("These settings are compiled into constants", HelpBoxMessageType.Info);
        m_rightPanel.Add(constantsHelp);
        IntegerField lodLevelsField = new IntegerField("World Size");
        lodLevelsField.value = worldSettings.lodLevels;
        lodLevelsField.RegisterValueChangedCallback((change) => worldSettings.lodLevels = change.newValue);
        m_rightPanel.Add(lodLevelsField);

        IntegerField densityCount = new IntegerField("Max active density map count");
        densityCount.value = worldSettings.densityCount;
        densityCount.RegisterValueChangedCallback((change) => worldSettings.densityCount = change.newValue);
        m_rightPanel.Add(densityCount);
        HelpBox densityHelp = new HelpBox("Memory consumption: " + worldSettings.densityCount * MemoryManager.densityMapLength * 8 / 1_000_000 + "Mb", HelpBoxMessageType.Info);
        m_rightPanel.Add(densityHelp);

        IntegerField maxGpuOperations = new IntegerField("Max Concurrent GPU operations (density map generation)");
        maxGpuOperations.value = worldSettings.maxGpuOperations;
        maxGpuOperations.RegisterValueChangedCallback((change) => worldSettings.maxGpuOperations = change.newValue);
        m_rightPanel.Add(maxGpuOperations);

        Button compileButton = new Button();
        compileButton.text = "Compile";
        compileButton.clicked += worldSettings.CompileConstants;
        m_rightPanel.Add(compileButton);
    }
    void DrawTerraxelBiomesWindow(TerraxelWorldSettings worldSettings){
        DrawHeader("Biomes");

        ObjectField activeBiomes = new ObjectField("Currently active biomes");
        activeBiomes.objectType = typeof(WorldData);
        activeBiomes.value = worldSettings.generator;
        activeBiomes.RegisterValueChangedCallback((change) => worldSettings.generator = change.newValue as WorldData);
        m_rightPanel.Add(activeBiomes);
    }
    void DrawTerraxelObjectsWindow(TerraxelWorldSettings worldSettings){

    }
    void DrawHeader(string text, int size = 35){
        Label header = new Label(text);
        var style = header.style;
        style.fontSize = size;
        style.alignSelf = Align.Center;
        m_rightPanel.Add(header);
    }
}
