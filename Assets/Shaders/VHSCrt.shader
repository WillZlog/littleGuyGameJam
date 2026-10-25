Shader "Little Guy/VHS CRT"
{
    Properties
    {
        _Strength ("Effect Strength", Range(0, 1)) = 1
        _Curvature ("Screen Curvature", Range(0, 0.3)) = 0.09
        _Vignette ("Corner Darkness", Range(0, 0.8)) = 0.35
        _ChromaticAberration ("Edge Color Separation", Range(0, 0.012)) = 0.003
        _CornerRadius ("Rounded Corners", Range(0, 0.3)) = 0.1
        _Scanlines ("Scanline Strength", Range(0, 0.2)) = 0.025
        _Grain ("Tape Grain", Range(0, 0.08)) = 0.009
        _Wobble ("Tape Wobble", Range(0, 0.003)) = 0.0002
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "VHS CRT"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength, _Curvature, _Vignette, _ChromaticAberration;
                float _CornerRadius, _Scanlines, _Grain, _Wobble;
            CBUFFER_END
            float _TVBootAmount, _TVBootReveal, _TVBootLine, _TVBootFlicker, _TVBootTime;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 originalUV = input.texcoord;
                float2 centered = originalUV * 2.0 - 1.0;
                float edge = saturate(dot(centered, centered) * 0.5);

                // Cross-axis barrel warp keeps the middle sharp and bends the edges like a CRT.
                float2 warped = centered * (1.0 + _Curvature * centered.yx * centered.yx);
                float2 uv = warped * 0.5 + 0.5;
                uv.x += sin(uv.y * 13.0 + _Time.y * 0.7) * _Wobble * edge;
                float2 split = centered * _ChromaticAberration * edge * edge;
                half3 color;
                color.r = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + split, 0).r;
                color.g = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).g;
                color.b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - split, 0).b;

                color *= 1.0 - _Vignette * smoothstep(0.12, 1.0, edge);
                float scanline = 0.5 + 0.5 * sin(originalUV.y * _ScreenParams.y * 3.14159265);
                color *= 1.0 - _Scanlines * scanline;
                float frame = floor(_Time.y * 24.0);
                float grain = frac(sin(dot(floor(input.positionCS.xy), float2(12.9898, 78.233))
                    + frame * 37.719) * 43758.5453) - 0.5;
                color = max(0.0, color + grain * _Grain);

                // A feathered bezel prevents clamped colors from smearing outside the curved screen.
                float2 q = abs(centered) - (0.995 - _CornerRadius);
                float distanceToCorner = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _CornerRadius;
                float bezel = 1.0 - smoothstep(-0.006, 0.002, distanceToCorner);
                float2 sourceBorder = smoothstep(0.0, 0.006, uv) * (1.0 - smoothstep(0.994, 1.0, uv));
                color *= bezel * sourceBorder.x * sourceBorder.y;
                half3 original = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, originalUV, 0).rgb;
                half3 screen = lerp(original, color, _Strength);
                if (_TVBootAmount > 0.001)
                {
                    float fromCenter = abs(originalUV.y - 0.5);
                    float reveal = 1.0 - smoothstep(_TVBootReveal * 0.5, _TVBootReveal * 0.5 + 0.004, fromCenter);
                    reveal *= step(0.0001, _TVBootReveal);
                    float noise = frac(sin(dot(floor(input.positionCS.xy), float2(39.346, 11.135))
                        + floor(_TVBootTime * 30.0) * 19.19) * 47453.5453);
                    half3 powered = (screen + noise * 0.09 * (1.0 - _TVBootReveal)) * reveal * (1.0 - _TVBootFlicker);
                    float glow = exp(-fromCenter * 450.0) * (1.0 - smoothstep(0.34, 0.48, abs(originalUV.x - 0.5)));
                    powered += half3(0.72, 0.88, 0.77) * glow * _TVBootLine;
                    screen = lerp(screen, powered, _TVBootAmount);
                }
                return half4(screen, 1.0);
            }
            ENDHLSL
        }
    }
}
