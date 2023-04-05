using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using Terraxel.DataStructures;
using Unity.Mathematics;
using UnityEngine.Rendering;
public class InstancedRenderer : IDisposable{
        public NativeList<InstanceData> data;
        public ComputeBuffer gpuBuffer;
        public RenderParams[] renderParams;
        public MaterialPropertyBlock propertyBlock;
        public Mesh[] meshes;

        public InstancedRenderer(MeshMaterialPair[] renderData, ShadowCastingMode shadowCastingMode){
            propertyBlock = new MaterialPropertyBlock();
            meshes = new Mesh[renderData.Length];
            renderParams = new RenderParams[renderData.Length];
            for(int i = 0; i < renderData.Length; i++){
                meshes[i] = renderData[i].mesh;
                renderParams[i] = new RenderParams(renderData[i].material);
                renderParams[i].matProps = propertyBlock;
                renderParams[i].shadowCastingMode = shadowCastingMode;
                
                renderParams[i].worldBounds = new Bounds(new float3(0), new float3(999999));
                renderParams[i].receiveShadows = true;
            }
            
        }
        public void Render(){
            if(gpuBuffer != null && gpuBuffer.IsValid()){
                if(gpuBuffer.count == 0) return;
                for(int i = 0; i < meshes.Length; i++){
                    Graphics.RenderMeshPrimitives(renderParams[i], meshes[i], 0, gpuBuffer.count);
                }
            }
        }
        public void PushData(){
            if(!data.IsCreated || data.Length == 0) return;
            if(gpuBuffer != null) gpuBuffer.Release();
            gpuBuffer = MemoryManager.GetInstancingBuffer(data.Length);
            gpuBuffer.SetData(data.AsArray(), 0, 0, data.Length);
            //grassMaterial.SetBuffer("positionBuffer", grassBuffer);
            propertyBlock.SetBuffer("Matrices", gpuBuffer);
        }
        public void AllocateData(){
            data = MemoryManager.GetInstancingData();
        }

        public void Dispose(){
            gpuBuffer?.Release();
            gpuBuffer = null;
            propertyBlock?.Clear();
            if(data.IsCreated){
                MemoryManager.ReturnInstanceData(data);
                data = default;
            }
        }
    }