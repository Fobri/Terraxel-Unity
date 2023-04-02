using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ODIN_INSPECTOR  
using Sirenix.OdinInspector;
#endif
using UnityEditor;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(fileName = "Texture Data", menuName = "Terraxel/TextureArray", order = 1), System.Serializable]
public class TextureData : ScriptableObject
{
    [SerializeField]
    public Texture2D[] textures;
    public TextureFormat textureFormat = TextureFormat.RGBA32;
    public GraphicsFormat graphicsFormat;
    public bool normal;
#if ODIN_INSPECTOR
    [Button]
#endif
    public void GenerateTextureArray(){
        Texture2DArray ar = new Texture2DArray(textures[0].width, textures[0].height, textures.Length, textureFormat, true);
        for(int i = 0; i < textures.Length; i++){
            var tex = normal ? ConvertDTXnmToRGBA(textures[i]) : textures[i];
            for(int s = 0; s < tex.mipmapCount; s++){
                ar.SetPixels(tex.GetPixels(s), i, s);
            }
        }
        ar.Apply();
        AssetDatabase.CreateAsset(ar, "Assets/Resources/Textures/GeneratedTextureArray.asset");
    }
    public Texture2D ConvertDTXnmToRGBA(Texture2D dtxnmTexture)
    {
        // Get the width and height of the texture
        int width = dtxnmTexture.width;
        int height = dtxnmTexture.height;

        // Create a new Texture2D to store the RGBA texture data
        Texture2D rgbaTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        // Get the raw texture data of the DTXnm texture
        for(int x = 0; x < width; x++){
            for(int y = 0; y < height; y++){
                var dtxPixel = dtxnmTexture.GetPixel(x,y);
                dtxPixel.b = 1;
                rgbaTexture.SetPixel(x,y, dtxPixel);
            }
        }

        // Apply the changes to
        rgbaTexture.Apply();
        return rgbaTexture;
    }
}
