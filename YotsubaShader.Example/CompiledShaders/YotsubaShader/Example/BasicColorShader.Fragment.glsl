#version 450

struct _25
{
    vec4 _m0;
    vec4 _m1;
};

in FS_Input
{
    layout(location = 8) vec4 Color;
} _input;

out vec4 _return;

void main()
{
    _return = _input.Color;
}

