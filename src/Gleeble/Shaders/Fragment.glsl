#version 450

layout(location = 0) in vec4 fs_in_Color;

layout(location = 0) out vec4 out_Color;

void main() {
    out_Color = fs_in_Color;
}
