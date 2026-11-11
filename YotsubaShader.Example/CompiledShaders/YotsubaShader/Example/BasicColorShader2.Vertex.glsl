#version 450

struct VS_Output
{
    vec4 Color;
};

layout(location = 0) in vec2 vertex_Position;
layout(location = 8) in vec4 vertex_Color;
layout(location = 8) out vec4 _return_Color;

void main()
{
    VS_Output vertexOutput = VS_Output(vec4(0.0));
    float _47 = float(0);
    float _48 = float(0);
    float _49 = float(0);
    float _50 = float(0);
    vec4 vec = vec4(_47, _48, _49, _50);
    int arr[5] = int[](0, 0, 0, 0, 0);
    arr[0] = 1;
    arr[1] = 3;
    arr[2] = 4;
    arr[3] = 5;
    arr[4] = 6;
    for (int _foreach_index_0 = 0; _foreach_index_0 < 5; _foreach_index_0++)
    {
        int VARIABLE = arr[_foreach_index_0];
        float _69 = float(1);
        vec = vec4(vertex_Position.x, _69, 0.0, 1.0);
    }
    for (int _foreach_index_1 = 0; _foreach_index_1 < 5; _foreach_index_1++)
    {
        int VARIABLE_1 = arr[_foreach_index_1];
        float _85 = float(1);
        vec = vec4(vertex_Position.x, _85, 0.0, 1.0);
    }
    gl_Position = vec;
    vertexOutput.Color = vertex_Color;
    _return_Color = vertexOutput.Color;
}

