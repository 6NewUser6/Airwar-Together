shader_type spatial;
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
