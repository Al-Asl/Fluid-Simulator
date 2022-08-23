Shader "Unlit/Volume"
{
    Properties
    {

    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "LightMode" = "ForwardBase" }
        ZTest always ZWrite off Cull front
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_volume
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ HARD_SHADOW SOFT_SHADOW
            #pragma multi_compile LIGHT_GRAD LIGHT_MARCH

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "./Volume.cginc"
            #include "./Shadow.cginc"

            sampler3D _Gradient;

            float3 _BaseColor;
            float4 _VLightParams;
            float4 _LightMarchParams;

            float rand(float co) { return frac(sin(co * (91.3458)) * 47453.5453); }
            float rand(float2 co) { return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453); }
            float rand(float3 co) { return rand(co.xy + rand(co.z)); }

            float3 grad(float3 uv)
            {
                return tex3Dlod(_Gradient, float4(uv, 0)).rgb;
            }

            float3 SampleGI(float3 normal,float3 wpos)
            {
                float3 gi = 0;
                #if UNITY_LIGHT_PROBE_PROXY_VOLUME
                        gi = max(0, SHEvalLinearL0L1_SampleProbeVolume(float4(normal,1),wpos));
                    #if defined(UNITY_COLORSPACE_GAMMA)
                            gi = LinearToGammaSpace(gi);
                    #endif
                #endif
                return gi;
            }

            float getShadow(float3 wpos,float depth)
            {
#if defined(SOFT_SHADOW)
                return getSoftShadow(wpos, depth);
#elif defined(HARD_SHADOW)
                return getHardShadow(wpos, depth);
#endif
                return 1;
            }

            float getStep(float t) { return  min(_MarchParams.y, _MarchParams.x + t * _MarchParams.z); }

            fixed4 frag(v2f i) : SV_Target
            {
                marchInput mi = getInput(i);

                float t = mi.tmin;
                float step = getStep(t);
                float4 col = 0;
                col.rgb = _BaseColor;

                [loop]
                for (int j = 0; j < 200; j++)
                {
                    if (t > mi.tmax)
                        break;
                    if (col.a > 0.95)
                        break;

                    float3 p = mi.ro + mi.rd * t;
                    float3 uv = UVW(p + rand(p)*step);

                    float d = SAMPLE(uv).r;
                    float4 c = 1; 
                    c.a = d * step * _VLightParams.x;

                    if (d > _VLightParams.z) 
                    {
                        float3 norm = float3(0,1,0);
#if defined(LIGHT_GRAD)
                        norm = -grad(uv);
                        c.rgb = saturate(dot(norm, _WorldSpaceLightPos0));
#elif defined(LIGHT_MARCH)
                        float la = 0;
                        float ldd = _LightMarchParams.x;
                        for (int k = _LightMarchParams.y; k > 0; k--)
                        {
                            float3 luv = uv - ldd * k * _WorldSpaceLightPos0;
                            float ld = SAMPLE(luv).r;
                            la += ld * ldd * (1 - la) * _LightMarchParams.z;
                        }
                        c.rgb = la;
#endif
                        c.rgb *= d * _VLightParams.y * _LightColor0 * getShadow(p,t) * _BaseColor;
                        c.rgb += SampleGI(norm, p) * _VLightParams.w;
                    }

                    c.rgb *= c.a;
                    col += c * (1 - col.a);

                    step = getStep(t);
                    t += step;
                }
                col.rgb = 1 - exp(-0.7 * col.rgb);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return saturate(col);
            }
            ENDCG
        }
    }
}