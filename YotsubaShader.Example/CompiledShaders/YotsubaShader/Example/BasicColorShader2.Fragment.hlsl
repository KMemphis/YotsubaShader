struct VS_Output
{
    float4 Color;
};

static const VS_Output _24 = { 0.0f.xxxx };
static const int _42[5] = { 0, 0, 0, 0, 0 };

static float4 fragment_Color;
static float4 _return;

struct SPIRV_Cross_Input
{
    float4 fragment_Color : COLOR0;
};

struct SPIRV_Cross_Output
{
    float4 _return : SV_Target0;
};

void FS_inner()
{
    _return = fragment_Color;
}

SPIRV_Cross_Output FS(SPIRV_Cross_Input stage_input)
{
    fragment_Color = stage_input.fragment_Color;
    FS_inner();
    SPIRV_Cross_Output stage_output;
    stage_output._return = _return;
    return stage_output;
}
