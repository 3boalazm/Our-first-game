Shader "Paperio/TrailGlow_URP"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 1.5
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 2
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 0.5
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.3
        _Alpha ("Alpha", Range(0, 1)) = 0.9
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        
        LOD 100
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                float fogFactor : TEXCOORD3;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _BaseColor;
                float4 _EmissionColor;
                float _EmissionStrength;
                float _FresnelPower;
                float _FresnelIntensity;
                float _PulseSpeed;
                float _PulseIntensity;
                float _Alpha;
            CBUFFER_END
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseColor = IN.color.a > 0 ? IN.color : (_Color.a > 0 ? _Color : _BaseColor);
                
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);
                
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                fresnel *= _FresnelIntensity;
                
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseIntensity;
                
                half3 emission = _EmissionColor.rgb * _EmissionStrength * pulse;
                emission += baseColor.rgb * fresnel * pulse;
                
                Light mainLight = GetMainLight();
                float ndotl = max(0.4, dot(normalWS, mainLight.direction));
                
                half3 finalColor = baseColor.rgb * ndotl + emission;
                
                finalColor = MixFog(finalColor, IN.fogFactor);
                
                return half4(finalColor, _Alpha * baseColor.a);
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Unlit"
}
