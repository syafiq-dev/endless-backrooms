# Builds backrooms-web.html: the browser version, with its shaders generated from the Unity shaders.
# All look presets (Resources/Presets/ShaderV1..V5) are built; the LOOK button cycles them.
# Run: python3 build.py  (from anywhere)
import base64, os, re

here = os.path.dirname(os.path.abspath(__file__))
res = os.path.join(here, '..', 'Assets', 'Backrooms', 'Resources')


def glsl(body):  # HLSL -> GLSL
    body = re.sub(r'SAMPLE_TEXTURE2D\((\w+), \w+, ', r'texture(\1, ', body)
    for a, b in [('float2', 'vec2'), ('float3', 'vec3'), ('float4', 'vec4'), ('frac(', 'fract('), ('lerp(', 'mix('), ('fmod(', 'mod(')]:
        body = body.replace(a, b)
    body = re.sub(r'saturate\(([^()]*(?:\([^()]*\)[^()]*)*)\)', r'clamp(\1, 0., 1.)', body)
    return re.sub(r'(?<![\w.])(\d+)(?![\w.])', r'\1.0', body)  # GLSL ES has no int->float promotion


def port(surface, vhs):
    src = open(os.path.join(res, surface)).read()
    vsrc = open(os.path.join(res, vhs)).read()
    return (glsl(src[src.index('            // Sine-free hash'):src.index('            half4 frag')]),
            glsl(vsrc[vsrc.index('// VHS-BEGIN'):vsrc.index('// VHS-END')]))


uri = lambda f: 'data:image/jpeg;base64,' + base64.b64encode(open(os.path.join(res, f), 'rb').read()).decode()
html = open(os.path.join(here, 'template.html')).read()
LOOKS = 5
# each look gets its own copy of the two fragment shader scripts (ids fs1, vhsfs1, fs2, ...)
for sid, mark in (('fs', '/*SHADE*/'), ('vhsfs', '/*VHS*/')):
    block = re.search(r'<script id="%s" .*?</script>\n' % sid, html, re.S).group(0)
    copies = ''
    for n in range(1, LOOKS + 1):
        shade, vhs = port('Presets/ShaderV%d/BackroomsSurfaceV%d.shader' % (n, n), 'Presets/ShaderV%d/BackroomsVHSV%d.shader' % (n, n))
        copies += block.replace('id="%s"' % sid, 'id="%s%d"' % (sid, n)).replace(mark, shade if sid == 'fs' else vhs)
    html = html.replace(block, copies)
html = html.replace('/*WALL*/', uri('backroom_wallpaper.jpg')).replace('/*CARPET*/', uri('carpet.jpg'))
open(os.path.join(here, 'backrooms-web.html'), 'w').write(html)
# Vercel serves public/ (see vercel.json): every push to GitHub redeploys the browser build.
os.makedirs(os.path.join(here, '..', 'public'), exist_ok=True)
open(os.path.join(here, '..', 'public', 'index.html'), 'w').write(html)
print('wrote backrooms-web.html', len(html) // 1024, 'KB')
