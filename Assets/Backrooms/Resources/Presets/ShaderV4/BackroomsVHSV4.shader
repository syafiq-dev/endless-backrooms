// PRESET ShaderV4: soft tape with a warm cast (from V2).
// Camcorder/VHS look, applied to the low-res frame the game camera renders (see VhsCamera.cs).
// Luma stays fairly sharp while colour smears sideways (that's what VHS does to chroma), lines wobble,
// a tracking band rolls up the screen, the bottom edge has head-switching noise, and lights bloom.
Shader "Backrooms/VHS V4"
{
    Properties
    {
        _MainTex ("Frame", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float2 _SrcSize; // frame size in pixels, set from VhsCamera.cs

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            // VHS-BEGIN (build.py ports everything down to VHS-END for the browser version)
            float vhash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float3 tap(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb; }
            float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }
            float2 chroma(float3 c) { return float2(dot(c, float3(0.596, -0.274, -0.322)), dot(c, float3(0.211, -0.523, 0.312))); }

            float3 vhs(float2 uv, float t)
            {
                float2 px = 1 / _SrcSize;
                float row = floor(uv.y * _SrcSize.y); // ("line" is reserved in HLSL)

                // Tracking: every line jitters a little; a noisy band rolls up the screen every ~20s.
                float bandPos = frac(t * 0.05);
                float band = smoothstep(0.05, 0, abs(uv.y - bandPos));
                float jitter = vhash(float2(row, floor(t * 30)));
                float shift = (jitter - 0.5) * px.x * 0.8 + band * (jitter - 0.5) * 0.012;
                // Head-switching noise along the bottom edge.
                float head = smoothstep(0.03, 0, uv.y);
                shift += head * (0.015 + 0.02 * vhash(float2(row, t)));
                uv.x += shift;

                // Luma: three taps, fairly sharp.
                float y = luma(tap(uv)) * 0.4 + (luma(tap(uv + float2(px.x, 0))) + luma(tap(uv - float2(px.x, 0)))) * 0.2
                        + (luma(tap(uv + float2(px.x * 2, 0))) + luma(tap(uv - float2(px.x * 2, 0)))) * 0.1; // softer, like a cheap lens
                // Chroma: smeared wide and pulled to the right.
                float2 c = chroma(tap(uv + float2(px.x * 1, 0))) + chroma(tap(uv + float2(px.x * 3, 0)))
                         + chroma(tap(uv + float2(px.x * 5, 0))) + chroma(tap(uv - float2(px.x * 1, 0)))
                         + chroma(tap(uv + float2(px.x * 7, 0)));
                c *= 0.2 * 1.15; // average, slightly oversaturated
                float3 col = float3(y + 0.956 * c.x + 0.621 * c.y, y - 0.272 * c.x - 0.647 * c.y, y - 1.106 * c.x + 1.703 * c.y);

                // Bloom: bright spots glow outward (cheap 8-tap ring).
                float3 glow = tap(uv + float2(px.x * 9, 0)) + tap(uv - float2(px.x * 9, 0)) + tap(uv + float2(0, px.y * 9)) + tap(uv - float2(0, px.y * 9))
                            + tap(uv + px * 6) + tap(uv - px * 6) + tap(uv + float2(px.x, -px.y) * 6) + tap(uv + float2(-px.x, px.y) * 6);
                col += max(glow * 0.125 - 0.6, 0) * 1.1;

                // Tape: milky blacks, snow, scanlines, brighter noise inside the tracking band.
                col = col * 0.86 + 0.08; // hazier
                col *= float3(1.04, 1.0, 0.92); // warm cast
                float snow = vhash(uv * _SrcSize + frac(t) * 911);
                col += (snow - 0.5) * (0.07 + band * 0.15 + head * 0.4);
                col *= 0.96 + 0.04 * sin(uv.y * _SrcSize.y * 3.14159);

                // Vignette, a bit stronger in the corners like a cheap camcorder lens.
                float2 v = uv - 0.5;
                col *= 1 - dot(v, v) * 1.1;
                return col;
            }
            // VHS-END

            half4 frag(Varyings i) : SV_Target
            {
                return half4(vhs(i.uv, _Time.y), 1);
            }
            ENDHLSL
        }
    }
}
