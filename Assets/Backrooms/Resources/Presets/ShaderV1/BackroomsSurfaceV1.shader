// PRESET ShaderV1 (saved 2026-10-28): the greener mono-yellow look.
// One unlit shader for the whole level. No real lights: every cell gets up to nine fake fluorescent
// panels in its drop ceiling, computed here from world position. That keeps it cheap on phones
// no matter how big the maze is. Corner shadows come from the cell's open-side mask (uv0.x).
// Fog and flicker are done here too; the camcorder look is a separate low-res pass (BackroomsVHS.shader).
Shader "Backrooms/Surface V1"
{
    Properties
    {
        _WallTex ("Wallpaper", 2D) = "white" {}
        _CarpetTex ("Carpet", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Set from BackroomsGame.cs
            float _CellSize, _WallHeight, _FogDensity;
            float4 _FogColor;
            float _Debug; // 1 = paint each room's variant number on its floor
            float _Seed;  // per run, moves the low-power zones
            // Mapped in world space (metres), so no UVs needed. Textures from the 2022 project.
            TEXTURE2D(_WallTex); SAMPLER(sampler_WallTex);
            TEXTURE2D(_CarpetTex); SAMPLER(sampler_CarpetTex);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float mask : TEXCOORD2; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.mask = i.uv.x;
                return o;
            }

            // Sine-free hash: sin() hashes band badly on mobile GPUs.
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            // Soft shadow from a closed side of the cell, d = distance to it.
            float sideAO(float closed, float d, float2 inward, float2 n)
            {
                float own = step(0.5, dot(n, inward)); // a wall doesn't shadow itself
                return 1 - closed * (1 - own) * (1 - smoothstep(0, 0.7, d)) * 0.35;
            }

            // Debug: distance to a line segment, and a 7-segment digit (0-9) in a 1 x 2 box.
            float seg(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                return length(pa - ba * saturate(dot(pa, ba) / dot(ba, ba)));
            }

            float digit(float2 p, float d)
            {
                float k = 1e3;
                if (d != 1 && d != 4) k = min(k, seg(p, float2(0, 2), float2(1, 2)));                    // top
                if (d != 5 && d != 6) k = min(k, seg(p, float2(1, 2), float2(1, 1)));                    // upper right
                if (d != 2) k = min(k, seg(p, float2(1, 1), float2(1, 0)));                              // lower right
                if (d != 1 && d != 4 && d != 7) k = min(k, seg(p, float2(0, 0), float2(1, 0)));          // bottom
                if (d == 0 || d == 2 || d == 6 || d == 8) k = min(k, seg(p, float2(0, 0), float2(0, 1))); // lower left
                if (d != 1 && d != 2 && d != 3 && d != 7) k = min(k, seg(p, float2(0, 1), float2(0, 2))); // upper left
                if (d != 0 && d != 1 && d != 7) k = min(k, seg(p, float2(0, 1), float2(1, 1)));          // middle
                return 1 - smoothstep(0.14, 0.22, k);
            }

            // EXIT in 14 cm letters, q in metres from the middle of the sign.
            float exitText(float2 q)
            {
                float2 p = q / 0.07 + float2(3.35, 1); // letters 1 x 2 units
                float k = seg(p, float2(0, 0), float2(0, 2));                                      // E
                k = min(k, seg(p, float2(0, 2), float2(1, 2)));
                k = min(k, seg(p, float2(0, 1), float2(0.8, 1)));
                k = min(k, seg(p, float2(0, 0), float2(1, 0)));
                k = min(k, seg(p, float2(1.9, 0), float2(2.9, 2)));                                // X
                k = min(k, seg(p, float2(1.9, 2), float2(2.9, 0)));
                k = min(k, seg(p, float2(4.3, 0), float2(4.3, 2)));                                // I
                k = min(k, seg(p, float2(5.7, 2), float2(6.7, 2)));                                // T
                k = min(k, seg(p, float2(6.2, 0), float2(6.2, 2)));
                return 1 - smoothstep(0.12, 0.2, k);
            }

            // Light state of a room: x = panel brightness (flicker, dead, low power, dark room), y = ambient, z = power (0 = bad zone).
            float3 roomLight(float2 cell, float t)
            {
                float h = hash(cell * 7.13 + 1.7);
                float power = smoothstep(0.2, 0.32, vnoise(cell * 0.35 + _Seed)); // low-power zones: ~1 room in 8
                // Steady almost everywhere (~3% dead, ~1% flickering); about half flicker inside bad-power zones.
                float on = 0.97 + 0.03 * sin(t * 100 + h * 50);
                if (h < 0.03) on = 0.35;
                else if (h < lerp(0.5, 0.01, power)) on = hash(float2(floor(t * 14 + h * 97), h)) > 0.35 ? 1 : 0.45;
                on *= lerp(0.3, 1, power);
                // Dark rooms: ~1 room in 20, in small clusters, with the power nearly gone (10% brightness).
                float dark = lerp(0.1, 1, smoothstep(0.14, 0.16, vnoise(cell * 0.5 + _Seed * 1.7 + 50)));
                return float3(on * dark, lerp(0.8, 1, on) * lerp(0.35, 1, power) * dark, power);
            }

            // Ceiling panels of one room: one slot per 3 x 3 tiles (9 per room), but each slot rolls its own fitting so the
            // grid doesn't repeat: ~20% empty, ~40% long 1x2 troffers (all running one way within a room),
            // the rest square, some nudged a tile over. w fades a neighbouring room's light in; on is that room's light state.
            void panels(float2 cell, float3 p, float3 n, float w, float on, inout float direct, inout float pool, inout float glow)
            {
                float s = _CellSize, H = _WallHeight, tile = s / 9;
                for (float bx = 0; bx < 3; bx++)
                    for (float bz = 0; bz < 3; bz++)
                    {
                        float2 slot = cell * 3 + float2(bx, bz);
                        float r = hash(slot * 1.31 + 7.7);
                        if (r < 0.2) continue;
                        float2 nd = (floor(float2(hash(slot + 11.3), hash(slot + 17.9)) * 3) - 1) * step(hash(slot * 2.17 + 3.1), 0.3);
                        float2 hs = float2(0.42, 0.42); // half size in tiles
                        if (r < 0.6)
                        {
                            float2 ax = hash(cell + 5.5) < 0.5 ? float2(1, 0) : float2(0, 1);
                            nd = nd * (1 - ax) + ax * (hash(slot + 23.1) < 0.5 ? 0.5 : -0.5); // spans the middle tile and a neighbour
                            hs += ax * 0.5;
                        }
                        float2 c = cell * s + (float2(bx, bz) + 0.5) * 3 * tile + nd * tile;
                        float area = hs.x * hs.y / 0.1764; // a 1x2 throws twice the light
                        float3 L = float3(c.x, H - 0.02, c.y) - p;
                        float d = length(L);
                        direct += w * on * area * (saturate(dot(n, L / d)) * 0.6 + 0.4) / (1 + 0.5 * d * d);
                        float2 dl = p.xz - c;
                        pool += w * area * exp(-dot(dl, dl) * 0.5);
                        glow = min(glow, max(max(abs(dl.x) - hs.x * tile, abs(dl.y) - hs.y * tile), 0));
                    }
            }

            float3 shade(float3 p, float3 n, float mask, float t)
            {
                float s = _CellSize, H = _WallHeight, tile = s / 9; // 6 m cell -> 0.67 m ceiling tiles

                // Which cell this pixel belongs to. Nudge along the normal so a wall picks the cell it faces.
                float2 cell = floor((p.xz + n.xz * 0.05) / s);
                float2 local = p.xz - cell * s; // 0..s inside the cell
                // Soft shadows where walls meet. Mask bits: 1 N(+z), 2 E(+x), 4 S, 8 W; set = open.
                float m = fmod(round(mask), 16); // low 4 bits: open sides; the rest: room variant
                float openN = fmod(m, 2), openE = fmod(floor(m / 2), 2), openS = fmod(floor(m / 4), 2), openW = floor(m / 8);
                float ao = sideAO(1 - openN, s - local.y, float2(0, -1), n.xz)
                         * sideAO(1 - openS, local.y, float2(0, 1), n.xz)
                         * sideAO(1 - openE, s - local.x, float2(-1, 0), n.xz)
                         * sideAO(1 - openW, local.x, float2(1, 0), n.xz);

                // Light blends across open sides so rooms don't meet in a hard line: near an open edge the room
                // next door adds its panels and its light state, fading from half-and-half at the edge to nothing
                // at this room's middle. Closed sides never blend, so light still doesn't leak through walls.
                float2 toX = float2(local.x < s * 0.5 ? -1 : 1, 0), toZ = float2(0, local.y < s * 0.5 ? -1 : 1);
                float fz = (toZ.y < 0 ? openS : openN) * (abs(local.y - s * 0.5) / (s * 0.5));
                float fx = (toX.x < 0 ? openW : openE) * (abs(local.x - s * 0.5) / (s * 0.5)); // 1 at the edge, 0 mid-room
                float3 lit = roomLight(cell, t), litX = roomLight(cell + toX, t), litZ = roomLight(cell + toZ, t);
                float wx = fx * 0.5, wz = fz * 0.5; // the shared state is half-and-half right at the edge
                float on = lit.x; // this room's panels, for the glow of its own fittings
                float amb = lit.y * (1 - wx - wz) + litX.y * wx + litZ.y * wz;
                float power = lit.z * (1 - wx - wz) + litX.z * wx + litZ.z * wz;

                float direct = 0, pool = 0, glow = 1e3; // glow = distance to the nearest panel's edge
                panels(cell, p, n, 1, lit.x, direct, pool, glow);
                if (fx > 0) panels(cell + toX, p, n, fx, litX.x, direct, pool, glow);
                if (fz > 0) panels(cell + toZ, p, n, fz, litZ.x, direct, pool, glow);
                direct *= 0.37;

                float3 albedo;
                float emissive = 0;
                float raw = round(mask), letters = 0;
                if (raw >= 512)
                {
                    // Exit sign plate (Props.ExitDoor): the door sits mid-wall, so lay the letters out from the cell centre.
                    letters = exitText(float2(dot(p.xz - (cell + 0.5) * s, float2(-n.z, n.x)), p.y - 2.45));
                    albedo = float3(0.05, 0.05, 0.05);
                    ao = 1;
                }
                else if (raw >= 256)
                {
                    // Props (Props.cs): flat paint colours, lit like the room around them.
                    float pm = raw - 256;
                    albedo = pm < 0.5 ? float3(0.45, 0.28, 0.14)  // wood
                           : pm < 1.5 ? float3(0.55, 0.56, 0.55)  // metal
                           : pm < 2.5 ? float3(0.75, 0.08, 0.06)  // stop-sign red
                           : pm < 3.5 ? float3(0.90, 0.90, 0.88)  // white
                           : float3(0.12, 0.12, 0.12);            // dark trim
                    ao = 1;
                }
                else if (n.y > 0.5)
                {
                    // Moist tan carpet: the old carpet texture pulled toward tan, plus faint stains.
                    float3 carpet = SAMPLE_TEXTURE2D(_CarpetTex, sampler_CarpetTex, p.xz / 2.0).rgb;
                    float stain = smoothstep(0.6, 0.85, vnoise(p.xz * 0.5 + 13));
                    float weave = (carpet.r + carpet.g + carpet.b) / 3 / 0.39; // ~1 on average; keep its detail but use our tan
                    albedo = float3(0.70, 0.63, 0.36) * pow(weave, 1.5) * (1 - 0.18 * stain);
                    // Light pools on the carpet under each panel.
                    albedo *= 0.8 + 0.3 * min(pool, 2);
                    // Damp: dark water stains creeping out from the walls.
                    float dWall = min(min(lerp(s - local.y, 1e3, openN), lerp(local.y, 1e3, openS)),
                                      min(lerp(s - local.x, 1e3, openE), lerp(local.x, 1e3, openW)));
                    float damp = (1 - smoothstep(0.1, 0.9, dWall)) * smoothstep(0.35, 0.7, vnoise(p.xz * 1.3 + 5));
                    albedo *= 1 - damp * float3(0.35, 0.38, 0.5);
                }
                else if (n.y < -0.5)
                {
                    // Drop ceiling: cream tiles, thin grid lines, glowing panel with a soft halo.
                    float2 g = abs(frac(local / tile) - 0.5);
                    float grid = 1 - smoothstep(0.46, 0.495, max(g.x, g.y)) * 0.35;
                    albedo = float3(0.58, 0.56, 0.36) * grid; // dimmer olive, so the panels pop like in the footage
                    // Some tiles are water-stained; a rare few are missing and show the dark void above.
                    float2 tid = floor(p.xz / tile);
                    float th = hash(tid + 3.7);
                    if (th > 0.92) albedo *= lerp(float3(1, 1, 1), float3(0.7, 0.6, 0.4), smoothstep(0.3, 0.7, vnoise(p.xz * 3 + th * 9)));
                    float missing = th > 0.995 && glow > 0 ? 1 : 0; // ~1 in 200 tiles, never a light
                    emissive = (glow <= 0 ? 3.0 : 0) + 0.35 * exp(-glow * 3);
                    if (missing > 0) { albedo *= 0.04; emissive = 0; }
                }
                else
                {
                    // Mono-yellow chevron wallpaper (the old texture), lightly mottled; darker baseboard.
                    float u = dot(p.xz, float2(-n.z, n.x));
                    float mottle = vnoise(float2(u, p.y) * 7);
                    float3 paper = SAMPLE_TEXTURE2D(_WallTex, sampler_WallTex, float2(u, p.y) / 1.25).rgb; // one repeat every 1.25 m
                    float pattern = (paper.r + paper.g + paper.b) / 3 / 0.54; // ~1 on average; squared so the bright light doesn't wash it out
                    albedo = float3(0.86, 0.80, 0.36) * pattern * pattern * (0.92 + 0.1 * mottle);
                    // Wear: darker seams every roll width (53 cm) and brown water stains rising from the floor.
                    float sx = frac(u / 0.53);
                    albedo *= 0.9 + 0.1 * smoothstep(0, 0.02, sx) * smoothstep(1, 0.98, sx);
                    float wet = smoothstep(0.6, 0.8, vnoise(float2(u * 0.8, p.y * 0.6) + 31)) * (1 - smoothstep(0.3, 2.0, p.y));
                    albedo *= 1 - wet * float3(0.25, 0.28, 0.45);
                    ao *= lerp(0.75, 1, smoothstep(0, 1.5, p.y));
                    if (p.y < 0.1) albedo *= 0.8;
                }

                float3 lightCol = lerp(float3(1.0, 0.75, 0.45), float3(1.0, 0.94, 0.70), power); // low power runs warm
                float3 col = albedo * lightCol * (0.45 * amb + direct * 1.6) * ao + lightCol * emissive * on;
                col = 1 - exp(-col * 1.5); // soft shoulder: washed-out highlights instead of hard clipping
                col = pow(max(col, 0), float3(0.95, 0.95, 1.35)); // camcorder grade: pull blue out, everything leans yellow-green
                col = lerp(col, float3(1.0, 0.12, 0.08), letters); // on emergency power: ignores flicker and low power
                if (_Debug > 0 && n.y > 0.5 && raw < 256) // variant number on the floor, just in front of the cell centre
                {
                    float2 dp = float2(local.x - s * 0.5, local.y - s * 0.5 - 0.6) / 0.45; // 0.45 x 0.9 m digit, reads upright facing +z
                    col = lerp(col, float3(0.25, 1.0, 0.3), digit(dp, floor(round(mask) / 16)));
                }
                return col;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float3 col = shade(i.positionWS, normalize(i.normalWS), i.mask, t);

                // Fog
                float dist = distance(i.positionWS, _WorldSpaceCameraPos);
                col = lerp(_FogColor.rgb, col, exp(-_FogDensity * dist));

                // Grain and vignette happen in the VHS pass (BackroomsVHS.shader).

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
