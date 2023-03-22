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
        public RenderParams renderParams;
        public MaterialPropertyBlock propertyBlock;
        public Mesh mesh;

        public InstancedRenderer(Material material, Mesh mesh, ShadowCastingMode shadowCastingMode){
            this.mesh = mesh;
            renderParams = new RenderParams(material);
            propertyBlock = new MaterialPropertyBlock();
            renderParams.matProps = propertyBlock;
            renderParams.shadowCastingMode = shadowCastingMode;
            
            renderParams.worldBounds = new Bounds(new float3(0), new float3(999999));
            renderParams.receiveShadows = true;
        }
        public void Render(){
            if(gpuBuffer != null && gpuBuffer.IsValid()){
                if(gpuBuffer.count == 0) return;
                Graphics.RenderMeshPrimitives(renderParams, mesh, 0, gpuBuffer.count);
            }
        }
        public void PushData(){
            PushData(data);
            //MemoryManager.ReturnInstanceData(data);
        }
        public void PushData(NativeList<InstanceData> data){
            if(!data.IsCreated || data.Length == 0) return;
            gpuBuffer = new ComputeBuffer(data.Length, sizeof(float) * 16);
            gpuBuffer.SetData(data.AsArray());
            //grassMaterial.SetBuffer("positionBuffer", grassBuffer);
            renderParams.matProps.SetBuffer("Matrices", gpuBuffer);
        }
        public void AllocateData(){
            data = MemoryManager.GetInstancingData();
        }

        public void Dispose(){
            gpuBuffer?.Release();
            gpuBuffer = null;
            renderParams.matProps?.Clear();
            if(data.IsCreated){
                MemoryManager.ReturnInstanceData(data);
                data = default;
            }
        }
    }