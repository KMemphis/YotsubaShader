struct _24
{
    float4 _m0;
    float4 _m1;
};

void Compute_inner()
{
}

[numthreads(3, 3, 3)]
void Compute()
{
    Compute_inner();
}
