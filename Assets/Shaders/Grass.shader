Shader "Unlit/GrassBladeIndirect"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _TextureStrength("Texture Strength", Range(0,1)) = 0
        _PrimaryCol ("Primary Color", Color) = (1, 1, 1)
        _SecondaryCol ("Secondary Color", Color) = (1, 0, 1)
        _AOColor ("AO Color", Color) = (1, 0, 1)
        _TipColor ("Tip Color", Color) = (0, 0, 1)
        _Scale ("Scale", Range(0.0, 2.0)) = 0.0
        _Cutoff ("Cutoff", Range(0,1)) = 0.5
        _MeshDeformationLimitLow ("Mesh Deformation low limit", Range(0.0, 5.0)) = 0.08
        _MeshDeformationLimitTop ("Mesh Deformation top limit", Range(0.0, 5.0)) = 2.0
        _WindNoiseScale ("Wind Noise Scale", float) = 0.0
        _WindStrength ("Wind Strength", float) = 1.0
        _WindSpeed ("Wind Speed", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {

        Pass
        {
            Tags { "RenderType"="Opaque" "LightMode"="ForwardBase" }
            LOD 100
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_instancing
			#pragma target 4.5

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "AutoLight.cginc"
            
            

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                fixed4 diff : COLOR0; // diffuse lighting color
                fixed3 ambient : COLOR1;
                LIGHTING_COORDS(1,2)
            };

            

            StructuredBuffer<float4x4> Matrices;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _PrimaryCol, _SecondaryCol, _AOColor, _TipColor;
            float _Scale;
            float _MeshDeformationLimitLow;
            float _MeshDeformationLimitTop;
            float4 _WindSpeed;
            float _WindStrength;
            float _WindNoiseScale;
            float _Cutoff;
            float _TextureStrength;

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {

                v2f o;

                //applying transformation matrix
                float3 positionWorldSpace = mul(Matrices[instanceID], float4(v.vertex.xyz, 1));
                float3 normalWorldSpace = mul(v.normal, transpose(Matrices[instanceID]));

                //move world UVs by time
                //float4 worldPos = float4(positionWorldSpace, 1);
                //float2 worldUV = worldPos.xz + _WindSpeed * _Time.y; 

                //creating noise from world UVs
                //float noise = 0;
                //Unity_SimpleNoise_float(worldUV, _WindNoiseScale, noise);
                //noise -= .5;

                //to keep bottom part of mesh at its position
                o.uv = v.uv;
                //float smoothDeformation = smoothstep(_MeshDeformationLimitLow, _MeshDeformationLimitTop, o.uv.y);
                //float distortion = smoothDeformation * noise;

                //apply distortion
                //positionWorldSpace.xz += distortion * _WindStrength * normalize(_WindSpeed);
                o.pos = mul(UNITY_MATRIX_VP, float4(positionWorldSpace, 1));
                //TRANSFER_VERTEX_TO_FRAGMENT(o);
                half nl = max(0, dot(normalWorldSpace, _WorldSpaceLightPos0.xyz));
                // factor in the light color
                o.diff = nl * _LightColor0;
                o.ambient = ShadeSH9(half4(normalWorldSpace,1));
                TRANSFER_SHADOW(o)
                //UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv);
                clip(albedo.a - _Cutoff);
                float4 col = lerp(_PrimaryCol, _SecondaryCol, i.uv.y);
                // compute shadow attenuation (1.0 = fully lit, 0.0 = fully shadowed)
                // darken light's illumination with shadow, keep ambient intact
                fixed3 lighting = i.diff * SHADOW_ATTENUATION(i) + i.ambient;
                float4 ao = lerp(_AOColor, 1.0f, i.uv.y);
                float4 tip = lerp(0.0f, _TipColor, i.uv.y * i.uv.y * (1.0f + _Scale));
                float4 grassColor = lerp(col + tip, albedo, _TextureStrength) * float4(lighting,1) * ao;
                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);
                return grassColor;
            }
            ENDCG
        }
        Pass
        {
            
            Tags {"LightMode"="ShadowCaster"}
            LOD 100
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
			#pragma target 4.5
            #include "UnityCG.cginc"

            
            StructuredBuffer<float4x4> Matrices;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                V2F_SHADOW_CASTER;
            };
            
            sampler2D _MainTex;
            float _Cutoff;

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                o.uv = v.uv;
                unity_ObjectToWorld = Matrices[instanceID];
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv);
                clip(albedo.a - _Cutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}

/*//generated by shadergraph
            inline float Unity_SimpleNoise_RandomValue_float (float2 uv) {
                  return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453);
            }
            //generated by shadergraph
            inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t) {
                 return (1.0-t)*a + (t*b);
            }
            //generated by shadergraph
            inline float Unity_SimpleNoise_ValueNoise_float (float2 uv) {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                uv = abs(frac(uv) - 0.5);
                float2 c0 = i + float2(0.0, 0.0);
                float2 c1 = i + float2(1.0, 0.0);
                float2 c2 = i + float2(0.0, 1.0);
                float2 c3 = i + float2(1.0, 1.0);
                float r0 = Unity_SimpleNoise_RandomValue_float(c0);
                float r1 = Unity_SimpleNoise_RandomValue_float(c1);
                float r2 = Unity_SimpleNoise_RandomValue_float(c2);
                float r3 = Unity_SimpleNoise_RandomValue_float(c3);

                float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
                float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
                float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
                return t;
            }
            //generated by shadergraph
            void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out) {
                float t = 0.0;

                float freq = pow(2.0, float(0));
                float amp = pow(0.5, float(3-0));
                t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                freq = pow(2.0, float(1));
                amp = pow(0.5, float(3-1));
                t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                freq = pow(2.0, float(2));
                amp = pow(0.5, float(3-2));
                t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                Out = t;
            }*/