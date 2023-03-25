using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class Noise
{
    public static float3 mod289(float3 x) {
    return x - math.floor(x * (1.0f / 289.0f)) * 289.0f;
    }

    public static float2 mod289(float2 x) {
    return x - math.floor(x * (1.0f / 289.0f)) * 289.0f;
    }

    public static float3 permute(float3 x) {
    return mod289(((x*34.0f)+10.0f)*x);
    }
    static readonly float4 C = new float4(0.211324865405187f,  // (3.0-sqrt(3.0))/6.0
                            0.366025403784439f,  // 0.5*(sqrt(3.0)-1.0)
                            -0.577350269189626f,  // -1.0 + 2.0 * C.x
                            0.024390243902439f); // 1.0 / 41.0
    public static float snoise(float2 v)
    {
        
        // First corner
        float2 i  = math.floor(v + math.dot(v, C.yy) );
        float2 x0 = v -   i + math.dot(i, C.xx);

        // Other corners
        float2 i1;
        //i1.x = step( x0.y, x0.x ); // x0.x > x0.y ? 1.0 : 0.0
        //i1.y = 1.0 - i1.x;
        i1 = (x0.x > x0.y) ? new float2(1.0f, 0.0f) : new float2(0.0f, 1.0f);
        // x0 = x0 - 0.0 + 0.0 * C.xx ;
        // x1 = x0 - i1 + 1.0 * C.xx ;
        // x2 = x0 - 1.0 + 2.0 * C.xx ;
        float4 x12 = x0.xyxy + C.xxzz;
        x12.xy -= i1;

        // Permutations
        i = mod289(i); // Avoid truncation effects in permutation
        float3 p = permute( permute( i.y + new float3(0.0f, i1.y, 1.0f ))
                + i.x + new float3(0.0f, i1.x, 1.0f ));

        float3 m = math.max(0.5f - new float3(math.dot(x0,x0), math.dot(x12.xy,x12.xy), math.dot(x12.zw,x12.zw)), 0.0f);
        m = m*m ;
        m = m*m ;

        // Gradients: 41 points uniformly over a line, mapped onto a diamond.
        // The ring size 17*17 = 289 is close to a multiple of 41 (41*7 = 287)

        float3 x = 2.0f * (p * C.www - math.floor(p * C.www)) - 1.0f;
        float3 h = math.abs(x) - 0.5f;
        float3 ox = math.floor(x + 0.5f);
        float3 a0 = x - ox;

        // Normalise gradients implicitly by scaling m
        // Approximation of: m *= inversesqrt( a0*a0 + h*h );
        m *= 1.79284291400159f - 0.85373472095314f * ( a0*a0 + h*h );

        // Compute final noise value at P
        float3 g = 0;
        g.x  = a0.x  * x0.x  + h.x  * x0.y;
        g.yz = a0.yz * x12.xz + h.yz * x12.yw;
        return 130.0f * math.dot(m, g);
    }
}
