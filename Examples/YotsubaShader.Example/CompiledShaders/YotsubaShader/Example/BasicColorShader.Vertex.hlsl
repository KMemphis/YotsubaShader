struct VS_Output
{
    float4 Position;
    float4 Color;
};

static float4 gl_Position;
static float2 input_Position;
static float4 input_Color;
static float4 _return_Color;

struct SPIRV_Cross_Input
{
    float2 input_Position : POSITION;
    float4 input_Color : COLOR0;
};

struct SPIRV_Cross_Output
{
    float4 _return_Color : COLOR0;
    float4 gl_Position : SV_Position;
};

float4 NewVector4(float x, float y, float z)
{
    float4 _this;
    _this.x = x;
    _this.y = y;
    _this.z = z;
    _this.w = float(1);
    return _this;
}

void VS_inner()
{
    VS_Output _output;
    _output.Position = NewVector4(input_Position.x, input_Position.y, 2.0f);
    _output.Color = input_Color;
    gl_Position = _output.Position;
    _return_Color = _output.Color;
}

SPIRV_Cross_Output VS(SPIRV_Cross_Input stage_input)
{
    input_Position = stage_input.input_Position;
    input_Color = stage_input.input_Color;
    VS_inner();
    SPIRV_Cross_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output._return_Color = _return_Color;
    return stage_output;
}
