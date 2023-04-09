using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Terraxel.DataStructures;
using Unity.Mathematics;

public struct BiomesGenerated
{
    public static readonly NativeInstanceData data0 = new NativeInstanceData(new float2(0.9500f, 1.0000f), new float3x2(new float3(1.0000f, 1.0000f, 1.0000f), new float3(1.2000f, 3.0000f, 1.2000f)), 0.2000f, 3);
	public static readonly NativeInstanceData data1 = new NativeInstanceData(new float2(0.9500f, 1.0000f), new float3x2(new float3(1.0000f, 1.0000f, 1.0000f), new float3(1.2000f, 3.0000f, 1.2000f)), 0.3000f, 3);
	public static readonly NativeInstanceData data2 = new NativeInstanceData(new float2(0.8500f, 1.0000f), new float3x2(new float3(0.3000f, 0.3000f, 0.3000f), new float3(0.5000f, 0.5000f, 0.5000f)), 4.0000f, 2);
	public static readonly NativeInstanceData data3 = new NativeInstanceData(new float2(0.8500f, 1.0000f), new float3x2(new float3(0.8000f, 0.8000f, 0.8000f), new float3(1.2000f, 1.2000f, 1.2000f)), 5.9000f, 2);
	public static readonly NativeInstanceData data4 = new NativeInstanceData();

     public static NativeInstanceData Get(int idx){
        switch(idx){
            case 0: return data0;case 1: return data1;case 2: return data2;case 3: return data3;case 4: return data4;
            default:
            return default;
        }
    }
}
