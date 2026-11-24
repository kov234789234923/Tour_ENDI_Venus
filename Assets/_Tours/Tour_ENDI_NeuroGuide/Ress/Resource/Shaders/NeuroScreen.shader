Shader "Neuro/Animated Screen"
{
    Properties
    {
        [MainTexture] _BaseMap("Emotion Atlas", 2D) = "black" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _ScreenState("Eye openness / Face brightness", Vector) = (1,1,0,0)
        [HideInInspector] _EyeLayout("Eye band layout", Vector) = (0.371,0.94,0.5016,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ScreenUnlit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float4 _ScreenState;
                float4 _EyeLayout;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 cell = frac(input.uv * 4.0);
                float2 origin = input.uv - cell * 0.25;
                half3 face = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                // Empty lower margin supplies the skin's own dark screen background.
                half3 background = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, origin + float2(cell.x, 0.04) * 0.25).rgb;
                float open = saturate(_ScreenState.x);
                if (open < 0.999)
                {
                    if (cell.y > _EyeLayout.x && cell.y < _EyeLayout.y)
                    {
                        float sourceY = _EyeLayout.z + (cell.y - _EyeLayout.z) / max(open, 0.025);
                        float visible = step(_EyeLayout.x, sourceY) * step(sourceY, _EyeLayout.y);
                        float2 sourceUV = origin + float2(cell.x, clamp(sourceY, _EyeLayout.x, _EyeLayout.y)) * 0.25;
                        face = lerp(background, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sourceUV).rgb, visible);
                    }
                    else if (cell.y <= _EyeLayout.x) face = lerp(background, face, open);
                }
                return half4(lerp(background, face, saturate(_ScreenState.y)) * _BaseColor.rgb, 1);
            }
            ENDHLSL
        }
    }
}
