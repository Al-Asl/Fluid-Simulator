Shader "Unlit/VolumeDebug"
{
    Properties
    {

    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        ZWrite off ZTest always Cull front
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_volume
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "./Volume.cginc"

            float2 _Range;
            float3 _Mask;
            float _Alpha;

            fixed4 frag(v2f i) : SV_Target
            {
                marchInput mi = getInput(i);

                float t = mi.tmin;
                float4 col = 0;

                [loop]
                for (int j = 0; j < 200; j++)
                {
                    if (t > mi.tmax)
                        break;
                    if (col.a > 0.99)
                        break;

                    float3 uv = UVW(mi.ro + mi.rd * t);
                    float4 c = SAMPLE(uv);
                    c.rgb = saturate((c.rgb - _Range.x) / (_Range.y - _Range.x));
                    c.w = 1;

                    col += c * (1 - col.a) * _Alpha;

                    t += _MarchParams.x;
                }

                return col*float4(_Mask,1);
            }
            ENDCG
        }
    }
}