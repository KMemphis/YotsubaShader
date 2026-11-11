struct VS_Output
{
    float4 Position;
    float4 Color;
};

void Compute_inner()
{
}

[numthreads(3, 3, 3)]
void Compute()
{
    Compute_inner();
}
