float4 GammaCorrect(float4 input)
{
    #ifdef UNITY_COLORSPACE_GAMMA
        return input;
    #else
        return pow(abs(input), 2.2f);
    #endif
}

void GammaCorrection_float(float4 InputColor, out float4 OutputColor)
{
    OutputColor = GammaCorrect(InputColor);
}