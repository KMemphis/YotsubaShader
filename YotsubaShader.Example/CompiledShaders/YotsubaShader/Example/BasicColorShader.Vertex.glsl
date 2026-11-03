#version 450

struct _24
{
    vec4 _m0;
    vec4 _m1;
};

in VS_Input
{
    layout(location = 0) vec2 Position;
    layout(location = 8) vec4 Color;
} _input;


void main()
{
    _24 _output;
    _output._m0 = vec4(_input.Position.x, _input.Position.y, 0.0, 1.0);
    _output._m1 = _input.Color;
    _return = VS_Output(_output._m0, _output._m1);
}

