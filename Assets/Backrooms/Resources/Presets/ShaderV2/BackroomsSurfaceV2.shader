// PRESET ShaderV2: warmer and brighter, closer to the original footage.
// One unlit shader for the whole level. No real lights: every cell gets up to nine fake fluorescent
// panels in its drop ceiling, computed here from world position. That keeps it cheap on phones
// no matter how big the maze is. Corner shadows come from the cell's open-side mask (uv0.x).
// Fog and flicker are done here too; the camcorder look is a separate low-res pass (BackroomsVHS.shader).
Shader "Backrooms/Surface V2"
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
            float _Seed;  // per run, moves the low-power zones
            float _NV;    // 1 = night vision: an infrared lamp on the camera (VhsCamera.cs)
            float4 _ExitPos; // the EXIT sign, whose red glow lights the dark around it
            float _Omen;     // 0-1: the lights around you stutter and dim for a few seconds, a warning before the entity (Entity.cs)
            float4 _EntPos;  // the entity's chest (xyz) and how strong its black aura is (w, 0 = no entity) (Entity.cs)
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

            // Distance to a line segment (for the EXIT letters).
            float seg(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                return length(pa - ba * saturate(dot(pa, ba) / dot(ba, ba)));
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
                // Blackouts: big patches (~1 room in 12) where the power is all but gone: 2% light, most panels dead, a few faint.
                float black = smoothstep(0.17, 0.2, vnoise(cell * 0.3 + _Seed * 2.3 + 90));
                float faint = hash(cell * 3.1 + 4.4) < 0.2 ? 0.12 : 0;
                return float3(lerp(faint, on * dark, black), lerp(0.8, 1, on) * lerp(0.35, 1, power) * dark * lerp(0.02, 1, black), power);
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

            // Rare wet carpet (~1% of the floor): irregular soaked patches, on rotated noise so they never line up with the room grid.
            // The game code mirrors this (wetAt) so footsteps squelch on them.
            float wetness(float2 xz)
            {
                float2 r = float2(xz.x * 0.8 + xz.y * 0.6, xz.y * 0.8 - xz.x * 0.6);
                return smoothstep(0.92, 0.96, vnoise(r * 0.09 + _Seed * 1.3 + 70) + vnoise(r * 0.9 + 3.3) * 0.12 - 0.06);
            }

            float3 shade(float3 p, float3 n, float mask, float t)
            {
                float s = _CellSize, H = _WallHeight, tile = s / 9; // 6 m cell -> 0.67 m ceiling tiles

                // Which cell this pixel belongs to. Nudge along the normal so a wall picks the cell it faces.
                float2 cell = floor((p.xz + n.xz * 0.05) / s);
                float2 local = p.xz - cell * s; // 0..s inside the cell
                // Wrong-way rooms: a copy of the room next door borrows that room's cell, so its lights, stains and tiles repeat.
                float3 wpos = p; // the real position, for fog and the lights you carry
                float twin = floor(round(mask) / 1024);
                mask -= twin * 1024;
                float2 shift = twin < 0.5 ? float2(0, 0) : twin < 1.5 ? float2(0, 1) : twin < 2.5 ? float2(1, 0) : twin < 3.5 ? float2(0, -1) : float2(-1, 0);
                cell += shift; p.xz += shift * s;
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
                           : pm < 4.5 ? float3(0.12, 0.12, 0.12)  // dark trim
                           : pm < 5.5 ? float3(0.36, 0.33, 0.20)  // worn olive sofa fabric
                           : pm < 6.5 ? float3(0.62, 0.42, 0.40)  // faded rose armchair fabric
                           : float3(0.004, 0.004, 0.004);         // the entity's black wire
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
                    float wet = wetness(p.xz);
                    albedo *= 1 - wet * float3(0.55, 0.5, 0.35);
                    emissive += wet * 0.12 * min(pool, 2); // a dull sheen under the lights
                }
                else if (n.y < -0.5 && p.y > H - 0.05) // archway beam undersides fall through to paint
                {
                    // Drop ceiling: cream tiles, thin grid lines, glowing panel with a soft halo.
                    float2 g = abs(frac(local / tile) - 0.5);
                    float grid = 1 - smoothstep(0.46, 0.495, max(g.x, g.y)) * 0.35;
                    albedo = float3(0.72, 0.68, 0.48) * grid; // pale cream tiles, like the footage
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
                    albedo = float3(0.92, 0.82, 0.42) * pattern * pattern * (0.92 + 0.1 * mottle);
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
                // The omen: within ~12 m of you the lights stutter between dim and dimmer.
                float stutter = hash(float2(floor(t * 13), 7.3)) > 0.45 ? 0.35 : 0.8;
                col *= 1 - _Omen * smoothstep(14, 5, length(_WorldSpaceCameraPos - wpos)) * (1 - stutter);
                // Red spill from the EXIT sign: emergency power, so it shows best in the dark.
                float3 toSign = _ExitPos.xyz - wpos; float dSign = length(toSign);
                col += albedo * float3(1.0, 0.1, 0.06) * (saturate(dot(n, toSign / dSign)) * 0.7 + 0.3) * 0.3 / (1 + dSign * dSign * 0.8) * ao;
                col *= 1 + _NV * 9; // NightShot gain: fine in the dark, but lit rooms blow out to white, so you have to switch back
                // Night vision: an infrared lamp on the camera.
                float3 toCam = _WorldSpaceCameraPos - wpos; float dCam = length(toCam);
                col += albedo * _NV * (saturate(dot(n, toCam / dCam)) * 0.8 + 0.2) * 1.0 / (1 + dCam * dCam * 0.12);
                col = 1 - exp(-col * 1.8); // brighter exposure, soft shoulder: washed-out highlights instead of hard clipping
                col = pow(max(col, 0), float3(0.9, 0.97, 1.25)); // camcorder grade: warm yellow rather than green
                col = lerp(col, float3(1.0, 0.12, 0.08), letters); // on emergency power: ignores flicker and low power
                // Fog takes the light of where it is, so blackouts fade to black instead of glowing yellow.
                // The entity's black aura: darkens whatever you see through the air around it (closest point of the view ray to its chest).
                float3 rd = wpos - _WorldSpaceCameraPos;
                float3 ea = _WorldSpaceCameraPos + rd * saturate(dot(_EntPos.xyz - _WorldSpaceCameraPos, rd) / dot(rd, rd)) - _EntPos.xyz;
                ea.y *= 0.55; // taller than it is wide
                float aura = _EntPos.w * exp(-dot(ea, ea) * 1.6) * (0.7 + 0.3 * vnoise(wpos.xz * 4 + wpos.y * 3 + t * 3)); // smoky edge
                return lerp(_FogColor.rgb * lerp(0.03, 1, saturate(amb * 1.2)) * (1 + _NV * 4), col, exp(-_FogDensity * dCam)) * (1 - 0.95 * aura);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float3 col = shade(i.positionWS, normalize(i.normalWS), i.mask, t);

                // Fog is done in shade(), lit by the room it is in.

                // Grain and vignette happen in the VHS pass (BackroomsVHS.shader).

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
