struct VS_Output
{
    float4 Color;
};

static const VS_Output _24 = { 0.0f.xxxx };
static const int _42[5] = { 0, 0, 0, 0, 0 };

static float4 gl_Position;
static float2 vertex_Position;
static float4 vertex_Color;
static float4 _return_Color;

struct SPIRV_Cross_Input
{
    float2 vertex_Position : POSITION;
    float4 vertex_Color : COLOR0;
};

struct SPIRV_Cross_Output
{
    float4 _return_Color : COLOR0;
    float4 gl_Position : SV_Position;
};

void VS_inner()
{
    VS_Output vertexOutput = _24;
    float4 vec = float4(float(0), float(0), float(0), float(0));
    int arr[5] = _42;
    arr[0] = 1;
    arr[1] = 3;
    arr[2] = 4;
    arr[3] = 5;
    arr[4] = 6;
    for (int _foreach_index_0 = 0; _foreach_index_0 < 5; _foreach_index_0++)
    {
        int VARIABLE = arr[_foreach_index_0];
        vec = float4(vertex_Position.x, float(1), 0.0f, 1.0f);
    }
    for (int _foreach_index_1 = 0; _foreach_index_1 < 5; _foreach_index_1++)
    {
        int VARIABLE_1 = arr[_foreach_index_1];
        vec = float4(vertex_Position.x, float(1), 0.0f, 1.0f);
    }
    gl_Position = vec;
    vertexOutput.Color = vertex_Color;
    _return_Color = vertexOutput.Color;
}

SPIRV_Cross_Output VS(SPIRV_Cross_Input stage_input)
{
    vertex_Position = stage_input.vertex_Position;
    vertex_Color = stage_input.vertex_Color;
    VS_inner();
    SPIRV_Cross_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output._return_Color = _return_Color;
    return stage_output;
}
