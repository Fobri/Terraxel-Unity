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
            float _Cutoff;
            float _TextureStrength;

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {

                v2f o;
                
                unity_ObjectToWorld = Matrices[instanceID];
                float3 positionWorldSpace = mul(Matrices[instanceID], float4(v.vertex.xyz, 1));
                float3 normalWorldSpace = mul(v.normal, transpose(Matrices[instanceID]));
                o.uv = v.uv;
                o.pos = mul(UNITY_MATRIX_VP, float4(positionWorldSpace, 1));
                half nl = max(0, dot(normalWorldSpace, _WorldSpaceLightPos0.xyz));
                o.diff = nl * _LightColor0;
                o.ambient = ShadeSH9(half4(normalWorldSpace,1));
                TRANSFER_SHADOW(o)
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv);
                clip(albedo.a - _Cutoff);
                float4 col = lerp(_PrimaryCol, _SecondaryCol, i.uv.y);
                float a = SHADOW_ATTENUATION(i);
                fixed3 lighting = i.diff * a + i.ambient * clamp(a, 0.5,1);
                float4 ao = lerp(_AOColor, 1.0f, i.uv.y);
                float4 tip = lerp(0.0f, _TipColor, i.uv.y * i.uv.y * (1.0f + _Scale));
                float4 grassColor = lerp(col + tip, albedo, _TextureStrength) * float4(lighting,1) * ao;
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