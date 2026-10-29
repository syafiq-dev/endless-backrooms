// PRESET ShaderV3: cleaner Hi8 tape: sharper, less colour smear and snow, harder vignette.
// Camcorder/VHS look, applied to the low-res frame the game camera renders (see VhsCamera.cs).
// Luma stays fairly sharp while colour smears sideways (that's what VHS does to chroma), lines wobble,
// a tracking band rolls up the screen, the bottom edge has head-switching noise, and lights bloom.
Shader "Backrooms/VHS V3"
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
            float _NV; // 1 = night vision mode (VhsCamera.cs)
            float _Danger; // 0-1, how close the entity is (Entity.cs): the tape breaks up as it closes in

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
                shift += _NV * (jitter - 0.5) * px.x * 2.5; // NightShot's cranked gain: shakier lines
                uv.x += shift;
                // The entity close by: lines tear sideways and a fast band rolls through.
                float tear = _Danger * _Danger;
                float fast = smoothstep(0.1, 0, abs(uv.y - frac(-t * 0.6)));
                uv.x += (vhash(float2(row, floor(t * 24) + 7)) - 0.5) * px.x * 8 * tear + fast * tear * (vhash(float2(row, floor(t * 50))) - 0.5) * 0.06;

                // Luma: three taps, fairly sharp.
                float y = luma(tap(uv)) * 0.5 + luma(tap(uv + float2(px.x, 0))) * 0.25 + luma(tap(uv - float2(px.x, 0))) * 0.25;
                // Chroma: smeared wide and pulled to the right.
                float2 c = chroma(tap(uv)) + chroma(tap(uv + float2(px.x * 1, 0))) + chroma(tap(uv + float2(px.x * 2, 0)))
                         + chroma(tap(uv - float2(px.x * 1, 0))) + chroma(tap(uv + float2(px.x * 3, 0)));
                c *= 0.2; // Hi8: much less colour smear
                float3 col = float3(y + 0.956 * c.x + 0.621 * c.y, y - 0.272 * c.x - 0.647 * c.y, y - 1.106 * c.x + 1.703 * c.y);
                // ...and the colour slips off the shapes.
                col.r = lerp(col.r, tap(uv + float2(px.x * 7, 0)).r, tear * 0.7);
                col.b = lerp(col.b, tap(uv - float2(px.x * 5, 0)).b, tear * 0.7);

                // Bloom: bright spots glow outward (cheap 8-tap ring).
                float3 glow = tap(uv + float2(px.x * 6, 0)) + tap(uv - float2(px.x * 6, 0)) + tap(uv + float2(0, px.y * 6)) + tap(uv - float2(0, px.y * 6))
                            + tap(uv + px * 4) + tap(uv - px * 4) + tap(uv + float2(px.x, -px.y) * 4) + tap(uv + float2(-px.x, px.y) * 4);
                col += max(glow * 0.125 - 0.75, 0) * 0.6;

                // Tape: milky blacks, snow, scanlines, brighter noise inside the tracking band.
                col = col * 0.96 + 0.02;
                float snow = vhash(uv * _SrcSize + frac(t) * 911);
                col += (snow - 0.5) * (0.04 + band * 0.08 + head * 0.3 + tear * 0.3);
                col *= 1 - tear * step(0.93, vhash(float2(floor(t * 12), 3))) * 0.5; // the picture dips now and then
                col *= 0.97 + 0.03 * sin(uv.y * _SrcSize.y * 3.14159);

                // Night vision: grayscale, gain pushed up, like a cheap camcorder's NightShot.
                // Like an old tape at night: crawling with static, coarse grain, streaky lines, white dropouts, a slow flicker.
                float nvl = 1 - exp(-luma(col) * 1.8);
                float grain = vhash(floor(uv * _SrcSize * 0.5) + frac(t * 1.7) * 517) - 0.5; // 2 px clumps
                float streak = vhash(float2(row, floor(t * 24))) - 0.5; // whole lines a bit brighter or darker
                float drop = step(0.9993, vhash(float2(floor(uv.x * _SrcSize.x / 6), row) + floor(t * 30))); // white dropout dashes
                float nvg = nvl * (0.92 + 0.08 * sin(t * 37)) + (snow - 0.5) * 0.16 + grain * 0.12 + streak * 0.06 + drop * 0.7;
                nvg = nvg * 0.85 + 0.06; // milky blacks
                col = lerp(col, float3(nvg, nvg, nvg), _NV);

                // Vignette, a bit stronger in the corners like a cheap camcorder lens.
                float2 v = uv - 0.5;
                col *= 1 - dot(v, v) * 1.5;
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
