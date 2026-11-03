struct FS_Input
{
    float4 Color;
};

struct _24
{
    float4 _m0;
    float4 _m1;
};

static FS_Input _input;
static float4 _return;

struct SPIRV_Cross_Input
{
    float4 FS_Input_Color : TEXCOORD8;
};

struct SPIRV_Cross_Output
{
    float4 _return : COLOR0;
};

void FS_inner()
{
    _return = _input.Color;
}

SPIRV_Cross_Output FS(SPIRV_Cross_Input stage_input)
{
    _input.Color = stage_input.FS_Input_Color;
    FS_inner();
    SPIRV_Cross_Output stage_output;
    stage_output._return = float4(_return);
    return stage_output;
}
