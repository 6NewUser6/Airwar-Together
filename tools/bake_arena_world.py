"""Bake authored, editable 20 km arena assets. No runtime terrain generation.

Ground samples intentionally match SectorCatalog.GroundHeight in ArenaTypes.cs.
Run with Python 3 from any directory; generated OBJ, TRES and TSCN are source assets.
"""
from pathlib import Path
import math
import random
import json

ROOT = Path(__file__).resolve().parents[1] / "airwar-together"
ASSETS = ROOT / "assets/arena"
SCENES = ROOT / "scenes/arena"
for folder in (ASSETS / "terrain", ASSETS / "meshes", ASSETS / "materials", SCENES / "sectors", SCENES / "props"):
    folder.mkdir(parents=True, exist_ok=True)

COLLIDERS = []
PROP_BOXES = []


def rotate_y(pos, angle):
    x, y, z = pos
    return (math.cos(angle) * x + math.sin(angle) * z, y, -math.sin(angle) * x + math.cos(angle) * z)


def box_collider(sector, center, half_size):
    COLLIDERS.append({"sector": sector, "kind": "box", "center": [round(v, 4) for v in center], "size": [round(v, 4) for v in half_size]})


def sphere_collider(sector, center, radius):
    COLLIDERS.append({"sector": sector, "kind": "sphere", "center": [round(v, 4) for v in center], "radius": round(radius, 4)})


def community_bounds(asset):
    data = json.loads((ROOT / f"assets/community/kaykit_space_base/{asset}.gltf").read_text())
    ranges = [data['accessors'][p['attributes']['POSITION']] for m in data['meshes'] for p in m['primitives']]
    lo = [min(p['min'][axis] for p in ranges) for axis in range(3)]
    hi = [max(p['max'][axis] for p in ranges) for axis in range(3)]
    return tuple((lo[a] + hi[a]) * .5 for a in range(3)), tuple((hi[a] - lo[a]) * .5 for a in range(3))


def write(path, content):
    path.write_text(content, encoding="utf-8")


def height(sector, x, z):
    if sector == 0:
        return 180 + 100 * math.sin(x * .0009) * math.cos(z * .00075) + 85 * math.sin((x + z) * .002) + 120 * abs(math.sin(x * .0012 + math.sin(z * .001))) ** 6
    if sector == 1:
        return -45 + 38 * math.sin(x * .0016) * math.cos(z * .0011) + 85 * max(0, math.sin(x * .0007 + math.cos(z * .0013))) ** 6
    if sector == 2:
        return 110 + 100 * math.sin(x * .001) * math.cos(z * .0008) + 450 * math.exp(-((x - 1800) ** 2 + (z + 800) ** 2) / 2000000)
    return -10000


def color(hex_color):
    h = hex_color.lstrip("#")
    return "Color(" + ", ".join(str(round(int(h[n:n+2], 16) / 255, 6)) for n in (0, 2, 4)) + ", 1)"


def material(name, tint, metallic=0, emission=None):
    text = '[gd_resource type="StandardMaterial3D" format=3]\n\n[resource]\n'
    text += f'albedo_color = {color(tint)}\nmetallic = {metallic}\nroughness = 0.7\n'
    if emission:
        text += f'emission_enabled = true\nemission = {color(emission)}\nemission_energy_multiplier = 2.2\n'
    write(ASSETS / "materials" / f"{name}.tres", text)


def obj(path, verts, faces, normals=None):
    lines = ["# Airwar Together authored asset; metres, Y up, forward -Z"]
    lines.extend(f"v {x:.4f} {y:.4f} {z:.4f}" for x, y, z in verts)
    if normals:
        lines.extend(f"vn {x:.6f} {y:.6f} {z:.6f}" for x, y, z in normals)
    for f in faces:
        lines.append("f " + " ".join(f"{i+1}//{i+1}" if normals else str(i+1) for i in f))
    write(path, "\n".join(lines) + "\n")


def bake_terrain():
    for sector, name in enumerate(("asterion", "nereid", "helios")):
        for tz in range(4):
            for tx in range(4):
                vertices, normals, faces = [], [], []
                for iz in range(41):
                    for ix in range(41):
                        x = -10000 + tx * 5000 + ix * 125
                        z = -10000 + tz * 5000 + iz * 125
                        vertices.append((x, height(sector, x, z), z))
                        dx = (height(sector, x + 1, z) - height(sector, x - 1, z)) * .5
                        dz = (height(sector, x, z + 1) - height(sector, x, z - 1)) * .5
                        length = math.sqrt(dx * dx + 1 + dz * dz)
                        normals.append((-dx / length, 1 / length, -dz / length))
                for z in range(40):
                    for x in range(40):
                        a = z * 41 + x
                        # Counter-clockwise viewed from above; OBJ importer preserves normals.
                        faces.extend(((a, a + 41, a + 1), (a + 1, a + 41, a + 42)))
                obj(ASSETS / "terrain" / f"{name}_{tx}_{tz}.obj", vertices, faces, normals)


def bake_rings():
    for name, radius, width in (("gate_ring", 120, 8), ("station_ring", 420, 26), ("planet_ring", 1900, 400)):
        verts, faces = [], []
        for i in range(96):
            angle = i / 96 * math.tau
            # Rings lie in XY so a ship can fly through in the default -Z direction.
            depth = 4 if name == "planet_ring" else width
            for r, z in ((radius - width / 2, -depth / 2), (radius + width / 2, -depth / 2), (radius - width / 2, depth / 2), (radius + width / 2, depth / 2)):
                verts.append((math.cos(angle) * r, math.sin(angle) * r, z))
        for i in range(96):
            a, b = i * 4, ((i + 1) % 96) * 4
            faces.extend(((a, b, b + 1, a + 1), (a + 2, a + 3, b + 3, b + 2), (a, a + 2, b + 2, b), (a + 1, b + 1, b + 3, a + 3)))
        obj(ASSETS / "meshes" / f"{name}.obj", verts, faces)


def bake_asteroids():
    for variant in range(4):
        rng = random.Random(884 + variant)
        verts, faces = [], []
        for row in range(9):
            phi = .015 + (math.pi - .03) * row / 8
            for col in range(14):
                theta = col / 14 * math.tau
                r = rng.uniform(.78, 1.22)
                verts.append((r * math.sin(phi) * math.cos(theta), r * math.cos(phi) * .8, r * math.sin(phi) * math.sin(theta) * .9))
        for row in range(8):
            for col in range(14):
                a, b = row * 14 + col, row * 14 + (col + 1) % 14
                faces.extend(((a, b, a + 14), (b, b + 14, a + 14)))
        obj(ASSETS / "meshes" / f"asteroid{variant}.obj", verts, faces)


def bake_rock_columns():
    for variant in range(3):
        verts, faces = [], []
        rng = random.Random(2100 + variant)
        for level, radius in ((0, 1), (.23, .92), (.32, .88), (.68, .66), (.75, .7), (1, .55)):
            for i in range(8):
                angle = i / 8 * math.tau
                r = radius * rng.uniform(.85, 1.08)
                verts.append((math.cos(angle) * r, level, math.sin(angle) * r))
        for row in range(5):
            for i in range(8):
                a, b = row * 8 + i, row * 8 + (i + 1) % 8
                faces.append((a, a + 8, b + 8, b))
        faces.append(tuple(range(47, 39, -1)))
        obj(ASSETS / "meshes" / f"column{variant}.obj", verts, faces)


class Scene:
    def __init__(self, name):
        self.name = name
        self.ext = {}
        self.sub = []
        self.nodes = [f'[node name="{name}" type="Node3D"]\n']

    def resource(self, kind, path):
        key = (kind, path)
        if key not in self.ext:
            self.ext[key] = f"r{len(self.ext) + 1}"
        return f'ExtResource("{self.ext[key]}")'

    def node(self, name, kind, parent=".", props="", instance=None):
        header = f'[node name="{name}" ' + (f'parent="{parent}" instance={instance}' if instance else f'type="{kind}" parent="{parent}"') + "]\n"
        self.nodes.append(header + props + "\n")

    def mesh(self, name, mesh, material_name, pos=(0, 0, 0), scale=(1, 1, 1), rotation=None, parent=".", distance=0):
        mesh_ref = self.resource("Mesh", f"res://assets/arena/meshes/{mesh}.tres" if mesh in ("box", "cylinder", "sphere", "plane") else f"res://assets/arena/meshes/{mesh}.obj")
        mat = self.resource("Material", f"res://assets/arena/materials/{material_name}.tres")
        props = f"position = Vector3{pos}\nscale = Vector3{scale}\nmesh = {mesh_ref}\nmaterial_override = {mat}\ncast_shadow = 0\n"
        if rotation:
            props += f"rotation = Vector3{rotation}\n"
        if distance:
            props += f"visibility_range_end = {distance}\nvisibility_range_end_margin = 300.0\n"
        self.node(name, "MeshInstance3D", parent, props)

    def instance(self, name, path, pos, scale=(1, 1, 1), rotation=None):
        props = f"position = Vector3{pos}\nscale = Vector3{scale}\n"
        if rotation:
            props += f"rotation = Vector3{rotation}\n"
        self.node(name, None, props=props, instance=self.resource("PackedScene", path))

    def save(self, path):
        text = '[gd_scene format=3]\n\n'
        for (kind, resource_path), resource_id in self.ext.items():
            text += f'[ext_resource type="{kind}" path="{resource_path}" id="{resource_id}"]\n'
        text += "\n" + "\n".join(self.sub) + "\n" + "\n".join(self.nodes)
        write(path, text)


def primitives():
    meshes = {
        "box": ('BoxMesh', 'size = Vector3(1, 1, 1)'),
        "cylinder": ('CylinderMesh', 'top_radius = 0.5\nbottom_radius = 0.5\nheight = 1.0\nradial_segments = 16'),
        "sphere": ('SphereMesh', 'radius = 1.0\nheight = 2.0\nradial_segments = 24\nrings = 12'),
        "plane": ('PlaneMesh', 'size = Vector2(1, 1)'),
    }
    for name, (kind, fields) in meshes.items():
        write(ASSETS / "meshes" / f"{name}.tres", f'[gd_resource type="{kind}" format=3]\n\n[resource]\n{fields}\n')
    for args in (("alloy", "8d9ba9", .7), ("dark", "202e3b", .55), ("white", "c9d6dc", .35), ("orange", "c87737", .4), ("sandstone", "76553e", 0), ("rock", "5b6670", .1), ("cyan", "43d9f2", .25, "1f8eae"), ("amber", "f3a43a", .25, "bc6720"), ("red", "ec482b", .1, "c42610"), ("water", "193c54", .6), ("ice", "829aa4", .15), ("planet", "204c68", .15), ("planet_ring", "637282", .05)):
        material(*args)


def props():
    # A complete modular base, not loose geometry generated by gameplay.
    scene = Scene("MiningOutpost")
    scene.mesh("RaisedFoundation", "cylinder", "dark", (0, -8, 0), (860, 16, 860), distance=7000)
    for i, (x, z) in enumerate(((-260, -230), (260, -230), (-260, 230), (260, 230))):
        scene.mesh(f"FoundationPier{i}", "box", "alloy", (x, -206, z), (58, 396, 58), distance=5000)
        PROP_BOXES.append(((x, -206, z), (29, 198, 29)))
    # Slender bands approximate the circular deck while preserving outside corners.
    for i in range(10):
        z = -430 + (i + .5) * 86
        half_x = math.sqrt(430 * 430 - z * z)
        PROP_BOXES.append(((0, -8, z), (half_x, 8, 43)))
    scene.mesh("LandingApron", "cylinder", "dark", (0, 1, 0), (220, 2, 220), distance=5000)
    scene.mesh("LandingBeacon", "gate_ring", "cyan", (0, 3, 0), (.78, .78, .25), (math.pi / 2, 0, 0), distance=5000)
    for i, (x, z) in enumerate(((-160, -90), (160, -100), (-150, 100), (150, 110))):
        asset = f"basemodule_{'ABCE'[i]}"
        scene.instance(f"Habitat{i}", f"res://assets/community/kaykit_space_base/{asset}.gltf", (x, 0, z), (34, 34, 34))
        center, half = community_bounds(asset)
        PROP_BOXES.append((tuple((x, 0, z)[a] + center[a] * 34 for a in range(3)), tuple(v * 34 for v in half)))
    scene.mesh("OperationsSpire", "box", "dark", (0, 70, -180), (34, 140, 34), distance=7000)
    scene.mesh("ControlDeck", "cylinder", "white", (0, 125, -180), (82, 25, 82), distance=7000)
    scene.mesh("ControlGlass", "cylinder", "cyan", (0, 125, -180), (84, 8, 84), distance=3500)
    scene.mesh("Antenna", "cylinder", "alloy", (0, 162, -180), (3, 50, 3), distance=6000)
    PROP_BOXES.extend((((0, 1, 0), (110, 1, 110)), ((0, 70, -180), (17, 70, 17)), ((0, 125, -180), (41, 12.5, 41)), ((0, 162, -180), (1.5, 25, 1.5))))
    for i in range(4):
        scene.mesh(f"Tank{i}", "cylinder", "orange", (-255 + i * 37, 28, -190), (26, 56, 26), distance=3500)
        PROP_BOXES.append(((-255 + i * 37, 28, -190), (13, 28, 13)))
    for i in range(6):
        scene.mesh(f"SolarArray{i}", "box", "dark", (-220 + i * 88, 17, 230), (73, 2, 44), (.25, 0, 0), distance=3000)
        scene.mesh(f"SolarSupport{i}", "cylinder", "alloy", (-220 + i * 88, 8, 230), (3, 16, 3), distance=2000)
        PROP_BOXES.append(((-220 + i * 88, 17, 230), (36.5, 6.5, 22)))
    for i, (asset, pos, scale) in enumerate((("drill_structure", (-320, 30, 10), 55), ("cargodepot_A", (280, 0, 90), 38), ("lander_A", (0, 3, 0), 40), ("structure_tall", (280, 0, -110), 44))):
        scene.instance(f"Industrial{i}", f"res://assets/community/kaykit_space_base/{asset}.gltf", pos, (scale, scale, scale))
        center, half = community_bounds(asset)
        PROP_BOXES.append((tuple(pos[a] + center[a] * scale for a in range(3)), tuple(v * scale for v in half)))
    scene.save(SCENES / "props" / "MiningOutpost.tscn")
    gate = Scene("TransitGate")
    gate.mesh("ArmoredRing", "gate_ring", "dark", scale=(1.08, 1.08, 1.8), distance=10000)
    gate.mesh("InnerFlux", "gate_ring", "cyan", scale=(1, 1, .35), distance=10000)
    for i in range(8):
        a = i / 8 * math.tau
        gate.mesh(f"Segment{i}", "box", "white", (math.cos(a) * 132, math.sin(a) * 132, 0), (14, 28, 35), (0, 0, a), distance=8000)
    gate.save(SCENES / "props" / "TransitGate.tscn")
    station = Scene("DawnStation")
    station.mesh("HabitatRing", "station_ring", "white", distance=15000)
    station.mesh("FluxRing", "station_ring", "cyan", scale=(1.06, 1.06, .14), distance=15000)
    station.mesh("CentralSpine", "cylinder", "dark", (0, 0, 0), (105, 920, 105), (math.pi / 2, 0, 0), distance=15000)
    station.mesh("CommandCore", "sphere", "alloy", (0, 0, 0), (150, 150, 200), distance=15000)
    for i in range(6):
        a = i / 6 * math.tau
        station.mesh(f"Spoke{i}", "box", "dark", (math.cos(a) * 240, math.sin(a) * 240, 0), (360, 13, 30), (0, 0, a), distance=15000)
        station.mesh(f"Collector{i}", "box", "planet", (math.cos(a) * 660, math.sin(a) * 660, 0), (380, 190, 8), (0, 0, a), distance=12000)
    station.save(SCENES / "props" / "DawnStation.tscn")


def environments():
    palettes = (
        ("asterion", "322b2b", "cba678", "635449", "f3d9ad", "b17e50", .00011),
        ("nereid", "152f46", "a4c1d0", "3e617a", "dceff4", "85afc3", .00013),
        ("helios", "271d29", "c17957", "5e4055", "ffbc82", "895040", .00017),
    )
    for name, sky, horizon, ground, sun, fog, density in palettes:
        text = '[gd_resource type="Environment" load_steps=3 format=3]\n\n'
        text += f'[sub_resource type="ProceduralSkyMaterial" id="SkyMaterial"]\nsky_top_color = {color(sky)}\nsky_horizon_color = {color(horizon)}\nground_bottom_color = {color(ground)}\nground_horizon_color = {color(horizon)}\nsky_curve = 0.22\nsun_angle_max = 10.0\n'
        text += '[sub_resource type="Sky" id="Sky"]\nsky_material = SubResource("SkyMaterial")\n\n'
        text += f'[resource]\nbackground_mode = 2\nsky = SubResource("Sky")\nambient_light_source = 2\nambient_light_color = {color(horizon)}\nambient_light_energy = 0.25\nreflected_light_source = 2\ntonemap_mode = 2\nfog_enabled = true\nfog_light_color = {color(fog)}\nfog_density = {density}\nfog_sky_affect = 0.2\n'
        write(ASSETS / "materials" / f"{name}_environment.tres", text)
    write(ASSETS / "materials" / "space_sky.gdshader", '''shader_type sky;
uniform vec3 zenith : source_color = vec3(0.006, 0.012, 0.032);
float hash(vec3 p) { return fract(sin(dot(p, vec3(37.17, 91.53, 13.91))) * 43758.5453); }
void sky() {
    vec3 ray = normalize(EYEDIR);
    float band = exp(-abs(ray.y * 2.0 + ray.x * 0.45) * 9.0);
    float cloud = 0.5 + 0.5 * sin(ray.x * 18.0 + sin(ray.z * 9.0) * 2.0);
    vec3 haze = mix(vec3(0.02, 0.035, 0.08), vec3(0.11, 0.035, 0.12), cloud) * band;
    vec3 cell = floor(ray * 430.0);
    vec3 local = fract(ray * 430.0) - vec3(0.5);
    float star = step(0.994, hash(cell)) * pow(max(0.0, 1.0 - length(local) * 2.3), 9.0);
    float dust = step(0.998, hash(floor(ray * 900.0))) * 0.18;
    COLOR = zenith + haze + vec3(star * 2.8 + dust);
}
''')
    write(ASSETS / "materials" / "space_environment.tres", '''[gd_resource type="Environment" load_steps=4 format=3]
[ext_resource type="Shader" path="res://assets/arena/materials/space_sky.gdshader" id="1"]
[sub_resource type="ShaderMaterial" id="SkyMaterial"]
shader = ExtResource("1")
[sub_resource type="Sky" id="Sky"]
sky_material = SubResource("SkyMaterial")
[resource]
background_mode = 2
sky = SubResource("Sky")
ambient_light_source = 3
ambient_light_color = Color(0.28, 0.36, 0.58, 1)
ambient_light_energy = 0.7
tonemap_mode = 2
''')
    write(ASSETS / "materials" / "terrain.gdshader", '''shader_type spatial;
render_mode specular_disabled;
uniform vec4 low_color : source_color = vec4(0.33, 0.17, 0.09, 1.0);
uniform vec4 high_color : source_color = vec4(0.65, 0.43, 0.24, 1.0);
uniform vec4 cliff_color : source_color = vec4(0.24, 0.18, 0.15, 1.0);
uniform float height_scale = 0.003;
varying vec3 world;
varying vec3 terrain_normal;
void vertex() { world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; terrain_normal = MODEL_NORMAL_MATRIX * NORMAL; }
void fragment() {
    float grain = sin(world.x * 0.009 + sin(world.z * .006) * 2.0) * 0.025;
    float strata = 0.5 + 0.5 * sin(world.y * 0.04 + sin(world.x * 0.002));
    float blend = clamp(world.y * height_scale, 0.0, 1.0);
    vec3 land = mix(low_color.rgb, high_color.rgb, blend);
    land *= 0.88 + strata * 0.18 + grain;
    float cliff = 1.0 - smoothstep(0.83, 0.99, normalize(terrain_normal).y);
    ALBEDO = mix(land, cliff_color.rgb, cliff * 0.65);
    ROUGHNESS = 0.97;
}
''')
    for name, low, high, cliff, hs in (("asterion", "443023", "946c4b", "624833", .0025), ("nereid", "34505a", "7b9da9", "547887", .012), ("helios", "231e25", "71606a", "3d3037", .0018)):
        write(ASSETS / "materials" / f"{name}_terrain.tres", f'''[gd_resource type="ShaderMaterial" load_steps=2 format=3]
[ext_resource type="Shader" path="res://assets/arena/materials/terrain.gdshader" id="1"]
[resource]
shader = ExtResource("1")
shader_parameter/low_color = {color(low)}
shader_parameter/high_color = {color(high)}
shader_parameter/cliff_color = {color(cliff)}
shader_parameter/height_scale = {hs}
''')
    write(ASSETS / "materials" / "planet_body.gdshader", '''shader_type spatial;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p); f = f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1.0,0.0)),f.x),mix(hash(i+vec2(0.0,1.0)),hash(i+vec2(1.0)),f.x),f.y);
}
void fragment() {
    vec2 p = UV * vec2(18.0,9.0);
    float n = noise(p) * .6 + noise(p * 2.0) * .25 + noise(p * 4.0) * .15;
    vec3 sea = vec3(.018,.082,.14);
    vec3 land = mix(vec3(.10,.16,.12),vec3(.24,.28,.22),noise(p*3.0));
    float ice = smoothstep(.32,.48,abs(UV.y-.5));
    vec3 ground = mix(sea,land,smoothstep(.48,.56,n));
    ground = mix(ground,vec3(.70,.83,.87),ice);
    float cloud = smoothstep(.60,.78,noise(p*1.7+vec2(3.0,7.0)));
    ALBEDO = mix(ground,vec3(.80,.86,.89),cloud*.75);
    float rim = pow(1.0-max(0.0,dot(NORMAL,VIEW)),4.0);
    EMISSION = vec3(.04,.16,.27)*rim;
    ROUGHNESS = .78;
    SPECULAR = .25;
}
''')
    write(ASSETS / "materials" / "planet_body.tres", '''[gd_resource type="ShaderMaterial" load_steps=2 format=3]
[ext_resource type="Shader" path="res://assets/arena/materials/planet_body.gdshader" id="1"]
[resource]
shader = ExtResource("1")
''')


def sector_scenes():
    for sector, (name, title, sun_color) in enumerate((("asterion", "Asterion", "ffe0b1"), ("nereid", "Nereid", "e1f4ff"), ("helios", "Helios", "ffc296"), ("space", "DeepSpace", "d2e3ff"))):
        s = Scene(title)
        env = s.resource("Environment", f"res://assets/arena/materials/{name}_environment.tres")
        s.node("Environment", "WorldEnvironment", props=f"environment = {env}\n")
        s.node("Sun", "DirectionalLight3D", props=f"rotation_degrees = Vector3(-33, -38, -12)\nlight_color = {color(sun_color)}\nlight_energy = 0.95\nshadow_enabled = true\ndirectional_shadow_max_distance = 1400.0\n")
        s.node("RimLight", "DirectionalLight3D", props="rotation_degrees = Vector3(24, 140, 0)\nlight_color = Color(0.28, 0.48, 0.68, 1)\nlight_energy = 0.45\n")
        s.node("Terrain", "Node3D")
        if sector < 3:
            mat = s.resource("Material", f"res://assets/arena/materials/{name}_terrain.tres")
            for z in range(4):
                for x in range(4):
                    mesh = s.resource("ArrayMesh", f"res://assets/arena/terrain/{name}_{x}_{z}.obj")
                    s.node(f"Chunk{x}{z}", "MeshInstance3D", "Terrain", f"mesh = {mesh}\nmaterial_override = {mat}\ncast_shadow = 1\n")
            if sector in (0, 1):
                # Monolith clusters create cover and narrow three-dimensional flight routes.
                for i in range(22):
                    x = (-1 if i % 2 == 0 else 1) * (420 + (i // 2 % 3) * 110)
                    z = -600 - (i // 2) * 440
                    floor = max(0, height(sector, x, z)) if sector == 1 else height(sector, x, z)
                    width, depth = 95 + i % 3 * 24, 100 + i % 4 * 16
                    column_height = 190 + i % 5 * 42
                    s.mesh(f"{'Sandstone' if sector == 0 else 'IceSpire'}{i}", f"column{i % 3}", "sandstone" if sector == 0 else "ice", (x, floor - 8, z), (width, column_height, depth), distance=9500)
                    # Stacked narrow boxes follow the taper without closing the flight route.
                    for step, fraction in enumerate((.95, .81, .64)):
                        box_collider(sector, (x, floor - 8 + column_height * (step + .5) / 3, z), (width * fraction, column_height / 6, depth * fraction))
            for i, (x, z) in enumerate(((0, -1700), (2800, 1700), (-3500, -2700), (5800, -5300), (-6000, 5000))):
                # The outpost sits on an elevated deck with deep piers, above every
                # terrain sample under its footprint. Its buildings cannot be buried.
                floor = max(height(sector, x + math.cos(a / 24 * math.tau) * r, z + math.sin(a / 24 * math.tau) * r) for r in (0, 215, 430) for a in range(24)) + 12
                if sector == 1:
                    floor = max(12, floor)
                s.instance(f"Outpost{i}", "res://scenes/arena/props/MiningOutpost.tscn", (x, floor + 4, z), rotation=(0, i * .74, 0))
                angle = i * .74
                for center, half in PROP_BOXES:
                    turned = rotate_y(center, angle)
                    box_collider(sector, (x + turned[0], floor + 4 + turned[1], z + turned[2]), (abs(math.cos(angle)) * half[0] + abs(math.sin(angle)) * half[2], half[1], abs(math.sin(angle)) * half[0] + abs(math.cos(angle)) * half[2]))
            # Navigate through actual rings along a low-altitude circuit.
            for i, (x, z) in enumerate(((0, -700), (450, -1450), (-400, -2400), (200, -3400), (1200, -4200))):
                floor = max(0, height(sector, x, z)) if sector == 1 else height(sector, x, z)
                s.instance(f"FlightGate{i}", "res://scenes/arena/props/TransitGate.tscn", (x, floor + 230, z), rotation=(0, i * -.11, 0))
                for step in range(64):
                    a = step / 64 * math.tau
                    ringpos = rotate_y((math.cos(a) * 126, math.sin(a) * 126, 0), i * -.11)
                    sphere_collider(sector, (x + ringpos[0], floor + 230 + ringpos[1], z + ringpos[2]), 11)
            if sector == 1:
                s.mesh("Ocean", "plane", "water", (0, 0, 0), (24000, 1, 24000))
                for i in range(24):
                    x, z = math.sin(i * 2.4) * (1100 + i * 280), math.cos(i * 2.4) * (1100 + i * 260)
                    if height(sector, x, z) < 4:
                        s.mesh(f"IceFloe{i}", "cylinder", "ice", (x, 2, z), (110 + i * 3, 5, 75 + i), rotation=(0, i, .04), distance=6000)
                        rx, rz = (110 + i * 3) * .5, (75 + i) * .5
                        box_collider(sector, (x, 2, z), (abs(math.cos(i)) * rx + abs(math.sin(i)) * rz, 2.5 + rx * .04, abs(math.sin(i)) * rx + abs(math.cos(i)) * rz))
            if sector == 2:
                # Low lava flats lie under the same server-authoritative danger altitude.
                s.mesh("LavaSea", "plane", "red", (0, 42, 0), (22000, 1, 22000))
                for i in range(12):
                    a = i / 12 * math.tau
                    x, z = 1800 + math.cos(a) * 1300, -800 + math.sin(a) * 1300
                    floor = height(sector, x, z)
                    s.mesh(f"RefineryStack{i}", "cylinder", "dark", (x, floor + 100, z), (45, 200, 45), distance=7000)
                    s.mesh(f"StackTip{i}", "cylinder", "amber", (x, floor + 203, z), (47, 7, 47), distance=5000)
                    box_collider(sector, (x, floor + 101, z), (23.5, 104, 23.5))
        else:
            s.instance("DawnStation", "res://scenes/arena/props/DawnStation.tscn", (0, 400, -3400))
            s.instance("TransitGate", "res://scenes/arena/props/TransitGate.tscn", (-1000, 400, -1400), (1.7, 1.7, 1.7), (0, .5, 0))
            s.mesh("NereidPlanet", "sphere", "planet_body", (-7300, -2800, -8000), (2200, 2200, 2200))
            s.mesh("OrbitalRing", "planet_ring", "planet_ring", (-7300, -2800, -8000), (1.45, 1.45, 1.45), (1.1, .3, .4))
            box_collider(3, (0, 400, -3400), (53, 53, 460))
            sphere_collider(3, (0, 400, -3400), 150)
            sphere_collider(3, (-7300, -2800, -8000), 2200)
            for step in range(64):
                a = step / 64 * math.tau
                sphere_collider(3, (math.cos(a) * 420, 400 + math.sin(a) * 420, -3400), 27)
                p = rotate_y((math.cos(a) * 126 * 1.7, math.sin(a) * 126 * 1.7, 0), .5)
                sphere_collider(3, (-1000 + p[0], 400 + p[1], -1400 + p[2]), 19)
            for i in range(6):
                a = i / 6 * math.tau
                for segment in range(8):
                    distance = 82.5 + segment * 45
                    box_collider(3, (math.cos(a) * distance, 400 + math.sin(a) * distance, -3400), (abs(math.cos(a)) * 22.5 + abs(math.sin(a)) * 6.5, abs(math.sin(a)) * 22.5 + abs(math.cos(a)) * 6.5, 15))
                for segment in range(8):
                    distance = 493.75 + segment * 47.5
                    box_collider(3, (math.cos(a) * distance, 400 + math.sin(a) * distance, -3400), (abs(math.cos(a)) * 23.75 + abs(math.sin(a)) * 95, abs(math.sin(a)) * 23.75 + abs(math.cos(a)) * 95, 4))
            rng = random.Random(31416)
            for i in range(96):
                a = i * 2.399963
                radius = rng.uniform(1600, 7500)
                pos = (math.cos(a) * radius, rng.uniform(-1300, 1500), math.sin(a) * radius - 1000)
                size = rng.uniform(30, 190)
                s.mesh(f"Asteroid{i:02}", f"asteroid{i % 4}", "rock", pos, (size, size, size), (rng.random() * 3, rng.random() * 3, rng.random() * 3), distance=12000)
                sphere_collider(3, pos, size * 1.22)
        s.save(SCENES / "sectors" / f"{title}.tscn")


def root_scene():
    write(SCENES / "ArenaWorld.tscn", '''[gd_scene format=3]
[ext_resource type="Script" path="res://scripts/Arena/ArenaWorld.cs" id="1"]
[ext_resource type="PackedScene" path="res://scenes/arena/sectors/Asterion.tscn" id="2"]
[ext_resource type="PackedScene" path="res://scenes/ships/spectre.tscn" id="3"]
[ext_resource type="Material" path="res://assets/arena/materials/cyan.tres" id="4"]
[sub_resource type="BoxMesh" id="ShotMesh"]
size = Vector3(0.7, 0.7, 18)
[node name="ArenaWorld" type="Node3D"]
script = ExtResource("1")
ShotMesh = SubResource("ShotMesh")
ShotMaterial = ExtResource("4")
[node name="Sector" parent="." instance=ExtResource("2")]
[node name="Actors" type="Node3D" parent="."]
[node name="Projectiles" type="Node3D" parent="."]
[node name="PreviewShip" parent="." instance=ExtResource("3")]
position = Vector3(0, 610, 180)
scale = Vector3(3, 3, 3)
[node name="Camera" type="Camera3D" parent="."]
position = Vector3(100, 650, 360)
rotation_degrees = Vector3(-13, 12, 0)
current = true
fov = 78.0
near = 0.5
far = 24000.0
''')


if __name__ == "__main__":
    bake_terrain()
    bake_rings()
    bake_asteroids()
    bake_rock_columns()
    primitives()
    environments()
    props()
    sector_scenes()
    root_scene()
    write(ASSETS / "collision_world.json", json.dumps(COLLIDERS, separators=(",", ":")) + "\n")
    print("Baked 4 arena sectors, 48 terrain chunks, 3 modular props and shared materials.")
