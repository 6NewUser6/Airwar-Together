shader_type canvas_item;

// Keep the hangar visible while giving the navigation text a quiet background.
void fragment() {
    float alpha = 0.96 * (1.0 - smoothstep(0.26, 0.68, UV.x));
    COLOR = vec4(0.015, 0.04, 0.075, alpha);
}
