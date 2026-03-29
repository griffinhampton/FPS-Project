Shader "Custom/BlockVertexColor"
{
    SubShader
    {
        Tags
        {
            "RenderType"       = "Opaque"
            "RenderPipeline"   = "UniversalPipeline"
            "Queue"            = "Geometry"
        }

        // ── Forward pass — vertex colours + basic directional light ───
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                o.color      = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                Light mainLight = GetMainLight();
                half  NdotL     = saturate(dot(normalize(input.normalWS),
                                               mainLight.direction));
                half3 ambient   = half3(0.35, 0.35, 0.40);
                half3 lighting  = mainLight.color.rgb * NdotL * 0.65 + ambient;
                return half4(input.color.rgb * lighting, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow-caster pass (chunks cast shadows) ──────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest   LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        // ── Depth-only pass (needed for some URP effects) ─────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
