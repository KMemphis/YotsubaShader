struct VS_Input
{
    float2 Position;
    float4 Color;
};

struct _25
{
    float4 _m0;
    float4 _m1;
};

static VS_Input _input;

struct SPIRV_Cross_Input
{
    float2 VS_Input_Position : TEXCOORD0;
    float4 VS_Input_Color : TEXCOORD8;
};

void VS_inner()
{
    _25 _output;
    _output._m0 = float4(_input.Position.x, _input.Position.y, 0.0f, 1.0f);
    _output._m1 = _input.Color;
    VS_Output _49 = { _output._m0, _output._m1 };
    _return = _49;
}

void VS(SPIRV_Cross_Input stage_input)
{
    _input.Position = stage_input.VS_Input_Position;
    _input.Color = stage_input.VS_Input_Color;
    VS_inner();
}
