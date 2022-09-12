#ifndef VOLUME
#define VOLUME

struct appdata
{
    float4 vertex : POSITION;
};

struct v2f
{
    float4 vertex : SV_POSITION;
    float3 vs_eye : TEXCOORD0;
    float3 ws_eye : TEXCOORD1;
    UNITY_FOG_COORDS(1)
};

v2f vert_volume(appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.ws_eye = mul(UNITY_MATRIX_M, v.vertex) - _WorldSpaceCameraPos;
    o.vs_eye = mul((float3x3)UNITY_MATRIX_V, o.ws_eye);
    UNITY_TRANSFER_FOG(o, o.vertex);
    return o;
}

sampler2D _CameraDepthTexture;

float3 _BoundsMin;
float3 _BoundsMax;
float4 _SampleParam;
float4 _MarchParams;

sampler3D _Volume;

struct marchInput {
    float depth;
    float3 ro;
    float3 rd;
    float tmin, tmax;
};

// Smits’ method
bool intersect(float3 ro,float3 rd,out float t0,out float t1)
{
    const float e = 0.000001;
    float tymin, tymax, tzmin, tzmax;

    float rdix = 1 / (abs(rd.x) < e ? sign(rd.x)*e : rd.x);
    if (rdix >= 0) {
        t0 = (_BoundsMin.x - ro.x) * rdix;
        t1 = (_BoundsMax.x - ro.x) * rdix;
    }
    else {
        t0 = (_BoundsMax.x - ro.x) * rdix;
        t1 = (_BoundsMin.x - ro.x) * rdix;
    }

    float rdiy = 1 / (abs(rd.y) < e ? sign(rd.y) * e : rd.y);
    if (rdiy >= 0) {
        tymin = (_BoundsMin.y - ro.y) * rdiy;
        tymax = (_BoundsMax.y - ro.y) * rdiy;
    }
    else {
        tymin = (_BoundsMax.y - ro.y) * rdiy;
        tymax = (_BoundsMin.y - ro.y) * rdiy;
    }

    if ((t0 > tymax) || (tymin > t1))
        return false;
    
    if (tymin > t0)
        t0 = tymin;
    if (tymax < t1)
        t1 = tymax;

    float rdiz = 1 / (abs(rd.z) < e ? sign(rd.z) * e : rd.z);
    if (rdiz >= 0) {
        tzmin = (_BoundsMin.z - ro.z) * rdiz;
        tzmax = (_BoundsMax.z - ro.z) * rdiz;
    }
    else {
        tzmin = (_BoundsMax.z - ro.z) * rdiz;
        tzmax = (_BoundsMin.z - ro.z) * rdiz;
    }

    if ((t0 > tzmax) || (tzmin > t1))
        return false;

    if (tzmin > t0)
        t0 = tzmin;
    if (tzmax < t1)
        t1 = tzmax;

    return true;
}


marchInput getInput(v2f i)
{
    marchInput o = (marchInput)0;

    float2 suv = i.vertex.xy * (_ScreenParams.zw - 1);
    float depth = DECODE_EYEDEPTH(tex2D(_CameraDepthTexture,suv).r);

    o.ro = _WorldSpaceCameraPos;
    o.rd = normalize(i.ws_eye);

    if (intersect(o.ro, o.rd, o.tmin, o.tmax))
    {
        i.vs_eye = normalize(i.vs_eye);
        float l = -1.0 / i.vs_eye.z;
        o.tmin = max(_ProjectionParams.y * l, o.tmin);
        o.tmax = min(depth * l, o.tmax);
    }
    else
        o.tmax = o.tmin - 0.001;

    o.depth = depth;

    return o;
}

#define UVW(pos) (pos - _BoundsMin)*_SampleParam.xyz
#define SAMPLE(uv) tex3Dlod(_Volume,float4(uv,0))

#endif