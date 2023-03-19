Shader "Custom/Grass"
{
	Properties
	{
        
        [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
		_Color("Color", Color) = (1, 1, 1, 1)
        _WaveSpeed("Wave Speed", float) = 1.0
        _WaveAmp("Wave Amp", float) = 1.0
        _HeightFactor("Height Factor", float) = 1.0
		_HeightCutoff("Height Cutoff", float) = 1.2
        _WindTex("Wind Texture", 2D) = "white" {}
        _WorldSize("World Size", vector) = (1, 1, 1, 1)
        _WindSpeed("Wind Speed", vector) = (1, 1, 1, 1)
	}

	SubShader
	{
        Tags{ "Queue"="Transparent" "RenderType" = "Transparent"}
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lod 100
		Pass
		{

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag addShadow
			#pragma multi_compile_fwdbase // shadows
            #pragma multi_compile_instancing
            #pragma target 3.5
            //#pragma instancing_options
            #include "UnityCG.cginc"
			
			// Properties
            sampler2D _WindTex;
            float4 _WindTex_ST;
			float4 _Color;
			float4 _LightColor0; // provided by Unity
            float4 _WorldSize;
            float _WaveSpeed;
            float _WaveAmp;
            float _HeightFactor;
			float _HeightCutoff;
            float4 _WindSpeed;
            sampler2D _MainTex;

            StructuredBuffer<float4x4> positionBuffer;

			struct vertexInput
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
                float2 texCoord : TEXCOORD0;
			};

			struct vertexOutput
			{
				float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 diff : COLOR0; // diffuse lighting color
                //float2 sp : TEXCOORD0; // test sample position
			};
/*v2f vert(appdata_base v, uint instanceID : SV_InstanceID)
        {
            v2f o;
            float4 wpos = mul(positionBuffer[instanceID], v.vertex);
            o.pos = mul(UNITY_MATRIX_VP, wpos);
            o.color = _Color;
            return o;
        }*/

			vertexOutput vert(vertexInput input, uint instanceID : SV_InstanceID)
			{
				vertexOutput output;
                unity_ObjectToWorld = positionBuffer[instanceID];
                //float4 wpos = mul(positionBuffer[instanceID], input.vertex);
                output.pos = UnityObjectToClipPos(input.vertex);
                output.uv = input.texCoord;

                //float4 normal4 = float4(input.normal, 0.0);
				//output.normal = normalize(mul(normal4, unity_WorldToObject).xyz);

                half3 worldNormal = UnityObjectToWorldNormal(input.normal);
                // dot product between normal and light direction for
                // standard diffuse (Lambert) lighting
                half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
                // factor in the light color
                output.diff = nl * _LightColor0;

                // get vertex world position
                //float4 worldPos = mul(input.vertex, unity_ObjectToWorld);
                // normalize position based on world size
                //float2 samplePos = wpos.xz/_WorldSize.xz;
                // scroll sample position based on time
                //samplePos += _Time.x * _WindSpeed.xy;
                // sample wind texture
                //float windSample = tex2Dlod(_WindTex, float4(samplePos, 0, 0));
                
				//output.sp = samplePos; // test sample position

                // 0 animation below _HeightCutoff
                //float heightFactor = input.vertex.y > _HeightCutoff;
				// make animation stronger with height
				//heightFactor = heightFactor * pow(input.vertex.y, _HeightFactor);

                // apply wave animation
                //output.pos.z += sin(_WaveSpeed*windSample)*_WaveAmp * heightFactor;
                //output.pos.x += cos(_WaveSpeed*windSample)*_WaveAmp * heightFactor;

				return output;
			}

			float4 frag(vertexOutput input) : COLOR
			{
				// normalize light dir
				//float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);

				// apply lighting
				//float ramp = clamp(dot(input.normal, lightDir), 0.001, 1.0);
				//float3 lighting = tex2D(_RampTex, float2(ramp, 0.5)).rgb;

                fixed4 col = tex2D(_MainTex, input.uv);
                col.rgb *= input.diff;

                //return float4(frac(input.sp.x), 0, 0, 1); // test sample position

				//float3 rgb = _LightColor0.rgb * lighting * _Color.rgb;
				return col;
			}

			ENDCG
		}

	}
}