shader_type spatial;
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
