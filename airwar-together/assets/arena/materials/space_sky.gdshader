shader_type sky;
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
