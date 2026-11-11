#version 450

struct VS_Output
{
    vec4 Position;
    vec4 Color;
};

layout(location = 8) in vec4 input_Color;
layout(location = 0) out vec4 _return;

void main()
{
    _return = input_Color;
}

