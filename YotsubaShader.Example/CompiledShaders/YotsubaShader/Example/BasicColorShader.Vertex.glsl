#version 450

struct VS_Output
{
    vec4 Position;
    vec4 Color;
};

layout(location = 0) in vec2 input_Position;
layout(location = 8) in vec4 input_Color;
layout(location = 8) out vec4 _return_Color;

void main()
{
    VS_Output _output;
    _output.Position = vec4(input_Position.x, input_Position.y, 0.0, 1.0);
    _output.Color = input_Color;
    gl_Position = _output.Position;
    _return_Color = _output.Color;
}

