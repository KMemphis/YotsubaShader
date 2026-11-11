#version 450

struct VS_Output
{
    vec4 Color;
};

layout(location = 8) in vec4 fragment_Color;
layout(location = 0) out vec4 _return;

void main()
{
    _return = fragment_Color;
}

