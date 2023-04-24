using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Terraxel.DataStructures;
using Unity.Mathematics;

public struct BiomesGenerated
{
    public static readonly NativeInstanceData data0 = new NativeInstanceData(new float2(0.95000f, 1.00000f), new float3x2(new float3(1.00000f, 1.00000f, 1.00000f), new float3(1.20000f, 3.00000f, 1.20000f)), 2.00000f, 7, true);
	public static readonly NativeInstanceData data1 = new NativeInstanceData(new float2(0.95000f, 1.00000f), new float3x2(new float3(1.00000f, 1.00000f, 1.00000f), new float3(1.20000f, 3.00000f, 1.20000f)), 4.00000f, 7, true);
	public static readonly NativeInstanceData data2 = new NativeInstanceData(new float2(0.85000f, 1.00000f), new float3x2(new float3(0.30000f, 0.30000f, 0.30000f), new float3(0.50000f, 0.50000f, 0.50000f)), 13.40000f, 2, false);
	public static readonly NativeInstanceData data3 = new NativeInstanceData(new float2(0.85000f, 1.00000f), new float3x2(new float3(0.80000f, 0.80000f, 0.80000f), new float3(1.20000f, 1.20000f, 1.20000f)), 11.90000f, 2, false);
	public static readonly NativeInstanceData data4 = new NativeInstanceData();

     public static NativeInstanceData Get(int idx){
        switch(idx){
            case 0: return data0;case 1: return data1;case 2: return data2;case 3: return data3;case 4: return data4;
            default:
            return default;
        }
    }
}
