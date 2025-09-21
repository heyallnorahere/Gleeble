#version 450

layout(location = 0) in vec2 in_Position;
layout(location = 1) in vec4 in_Color;

layout(location = 0) out vec4 vs_out_Color;

void main() {
    gl_Position = vec4(in_Position, 0, 1);
    vs_out_Color = in_Color;
}
