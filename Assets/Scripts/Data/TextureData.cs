using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEditor;
using Unity.Mathematics;

[CreateAssetMenu(fileName = "Texture Data", menuName = "Terraxel/TextureArray", order = 1), System.Serializable]
public class TextureData : ScriptableObject
{
    [SerializeField]
    public Texture2D[] textures;
    public TextureFormat textureFormat = TextureFormat.RGBA32;
    public bool normal;
    [Button]
    public void GenerateTextureArray(){
        Texture2DArray ar = new Texture2DArray(textures[0].width, textures[0].height, textures.Length, textureFormat, true);
        for(int i = 0; i < textures.Length; i++){
            for(int s = 0; s < textures[0].mipmapCount; s++){
                ar.SetPixels(textures[i].GetPixels(s), i, s);
            }
        }
        ar.Apply();
        AssetDatabase.CreateAsset(ar, "Assets/Resources/Textures/GeneratedTextureArray.asset");
    }
}
