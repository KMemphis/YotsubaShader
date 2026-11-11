struct VS_Output
{
    float4 Position;
    float4 Color;
};

static float4 input_Color;
static float4 _return;

struct SPIRV_Cross_Input
{
    float4 input_Color : COLOR0;
};

struct SPIRV_Cross_Output
{
    float4 _return : SV_Target0;
};

void FS_inner()
{
    _return = input_Color;
}

SPIRV_Cross_Output FS(SPIRV_Cross_Input stage_input)
{
    input_Color = stage_input.input_Color;
    FS_inner();
    SPIRV_Cross_Output stage_output;
    stage_output._return = _return;
    return stage_output;
}
