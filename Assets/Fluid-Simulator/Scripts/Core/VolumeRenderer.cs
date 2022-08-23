using UnityEngine;

public abstract class BaseVolumeRenderer : System.IDisposable
{
    [Range(50,200)]
    public int MaxSteps = 100;
    [Range(10, 50)]
    public int MinSteps = 10;
    [Range(1,50)]
    public float StepMulti = 1f;

    [SerializeField,HideInInspector]
    protected Material material;
    [SerializeField, HideInInspector]
    private GameObject gameObject;

    protected abstract string shader { get; }
    [SerializeField, HideInInspector]
    protected FluidSimulator simulator;
    protected Camera camera;

    float PixeWidth()
    {
        return Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 2f / Screen.width;
    }

    public void Initialize(FluidSimulator simulator)
    {
        camera = Camera.main;
        gameObject = new GameObject("Volume");
        var lv = gameObject.AddComponent<LightProbeProxyVolume>();
        lv.resolutionMode = LightProbeProxyVolume.ResolutionMode.Custom;
        lv.gridResolutionX = 16;
        lv.gridResolutionY = 16;
        lv.gridResolutionZ = 16;
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        material = new Material(Shader.Find(shader));

        var mf = GameObjectUtils.AddOrGetComponenet<MeshFilter>(gameObject);
        mf.sharedMesh = GameObjectUtils.GetPrimitiveMesh(PrimitiveType.Cube);

        var mr = GameObjectUtils.AddOrGetComponenet<MeshRenderer>(gameObject);
        mr.sharedMaterial = material;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.UseProxyVolume;

        Camera.main.depthTextureMode = DepthTextureMode.Depth;
        this.simulator = simulator;
    }

    public void Update()
    {
        if (simulator.Density == null)
            return;

        var bounds = simulator.Desc.bounds;

        gameObject.transform.position = bounds.center;
        gameObject.transform.localScale = bounds.size;

        material.SetVector("_BoundsMin", bounds.min);
        material.SetVector("_BoundsMax", bounds.max);
        material.SetVector("_SampleParam", new Vector3(
            (float)simulator.Desc.texelPerUnit / simulator.Desc.resolution.x,
            (float)simulator.Desc.texelPerUnit / simulator.Desc.resolution.y,
            (float)simulator.Desc.texelPerUnit / simulator.Desc.resolution.z));

        float maxLength = bounds.size.magnitude;

        material.SetVector("_MarchParams", new Vector4(
            maxLength / MaxSteps,
            maxLength / MinSteps,
            PixeWidth() * StepMulti));

        OnUpdate();
    }

    public void Dispose()
    {
        GameObjectUtils.SafeDestroy(material);
        material = null;

        GameObjectUtils.SafeDestroy(gameObject);
    }

    protected abstract void OnInitialize();
    protected abstract void OnUpdate();
    protected abstract void OnDispose();
}

[System.Serializable]
public class VolumeRenderer : BaseVolumeRenderer
{
    public enum LightingMode
    {
        Gradient,
        Marching
    }

    [Space]
    public LightingMode lightingMode;
    public LightShadows shadowMode;
    [Space]
    public float DensityMulti = 1f;
    public float LightingMulti = 1f;
    public float DensityLightingThrehold = 0.3f;
    public float indirectMulti = 1f;
    [Space]
    public int SmoothIterations;
    [Space]
    public int LightMarchIterations = 5;
    public float LightMarchStep = 0.1f;
    public float LightMarchLightMulti = 30f;
    [Space]
    public Color BaseColor;

    protected override string shader => "Unlit/Volume";

    protected override void OnInitialize()
    {

    }

    protected override void OnUpdate()
    {
        material.SetTexture("_Volume", simulator.Density);
        material.SetTexture("_Gradient", simulator.DensityGradient);

        material.SetColor("_BaseColor", BaseColor);
        material.SetVector("_VLightParams", new Vector4(
            DensityMulti,
            LightingMulti,
            DensityLightingThrehold,
            indirectMulti));
        material.SetVector("_LightMarchParams", new Vector4(
            LightMarchStep,
            LightMarchIterations,
            LightMarchLightMulti));

        switch (lightingMode)
        {
            case LightingMode.Gradient:
                material.EnableKeyword("LIGHT_GRAD");
                material.DisableKeyword("LIGHT_MARCH");
                simulator.RenderAttachmentSettings.enableGradient = true;
                break;
            case LightingMode.Marching:
                material.DisableKeyword("LIGHT_GRAD");
                material.EnableKeyword("LIGHT_MARCH");
                simulator.RenderAttachmentSettings.enableGradient = false;
                break;
        }

        simulator.RenderAttachmentSettings.smoothGradientIterations = SmoothIterations;

        switch (shadowMode)
        {
            case LightShadows.None:
                material.DisableKeyword("SOFT_SHADOW");
                material.DisableKeyword("HARD_SHADOW");
                break;
            case LightShadows.Hard:
                material.DisableKeyword("SOFT_SHADOW");
                material.EnableKeyword("HARD_SHADOW");
                break;
            case LightShadows.Soft:
                material.DisableKeyword("HARD_SHADOW");
                material.EnableKeyword("SOFT_SHADOW");
                break;
        }
    }

    protected override void OnDispose()
    {
        
    }
}

[System.Serializable]
public class VolumeDebugRenderer : BaseVolumeRenderer
{
    public enum Target
    {
        Density,
        Velocity,
        Pressure,
        Obstacles,
        ObstaclesVelocity
    }

    [Space]
    public Target target;
    [Range(0,1f)]
    public float alpha = 1f;
    public bool X = true, Y = true, Z = true;
    public Vector2 Range = new Vector2(-10, 10);
    public Vector3 Slice = Vector3.one;
    public FilterMode filterMode;

    protected override void OnInitialize() {}

    protected override string shader => "Unlit/VolumeDebug";

    protected override void OnUpdate()
    {
        Slice = Vector3.Max(Vector3.zero, Vector3.Min(Slice, Vector3.one));

        var size = simulator.Desc.bounds.size;
        size.Scale(Slice);
        material.SetVector("_BoundsMax", simulator.Desc.bounds.min + size);

        material.SetVector("_Mask", new Vector4(X ? 1 : 0, Y ? 1 : 0, Z ? 1 : 0, 1));
        material.SetVector("_Range", new Vector4(Range.x, Range.y, 0, 0));
        material.SetFloat("_Alpha", Mathf.Max(0.001f,alpha));
        var texture = GetTexture(target);
        texture.filterMode = filterMode;
        material.SetTexture("_Volume", GetTexture(target));
    }

    RenderTexture GetTexture(Target target)
    {
        switch (target)
        {
            case Target.Density:
                return simulator.Density;
            case Target.Velocity:
                return simulator.Velocity;
            case Target.Pressure:
                return simulator.Pressure;
            case Target.Obstacles:
                return simulator.Obstacles;
            case Target.ObstaclesVelocity:
                return simulator.ObstaclesVelocity;
        }
        return null;
    }

    protected override void OnDispose()
    {
        var array = System.Enum.GetValues(typeof(Target));
        foreach(var item in array)
            GetTexture((Target)item).filterMode = FilterMode.Bilinear;
    }
}