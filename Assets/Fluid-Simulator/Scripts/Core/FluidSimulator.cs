using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum AdvectionType
{
    BFECC,
    Back
}

[System.Serializable]
public class FluidSimulator : System.IDisposable
{
    [System.Serializable]
    public struct SpatialDesc
    {
        public Vector3Int resolution;
        public Vector3 texelSize;
        public Vector3Int groupCount;
        public int texelPerUnit;
        public Bounds bounds;
    }

    [System.Serializable]
    public class RenderAttachmentSetting
    {
        public bool enableGradient;
        public int smoothGradientIterations;
        public bool enableSDF;
        public float sdfThreshold;
    }

    [SerializeField]
    public AdvectionType densityAdvection;
    [SerializeField]
    public int JacobianIterations = 20;
    [SerializeField]
    public float voticity = 0.1f;
    [SerializeField]
    public float dissipation;

    public SpatialDesc Desc => desc;
    public RenderAttachmentSetting RenderAttachmentSettings => renderAttachmentSetting;

    public RenderTexture Density => density_0;
    public RenderTexture Velocity => velocity_0;
    public RenderTexture Pressure => pressure;
    public RenderTexture Obstacles => obstacles;
    public RenderTexture ObstaclesVelocity => obstacles_velocity;
    public RenderTexture DensityGradient => temp_vector;
    public RenderTexture SDF => sdf;

    [SerializeField]
    private ComputeShader compute;

    [SerializeField]
    private RenderTexture obstacles;
    [SerializeField]
    private RenderTexture obstacles_velocity;
    [SerializeField]
    private RenderTexture density_0;
    [SerializeField]
    private RenderTexture density_1;
    [SerializeField]
    private RenderTexture velocity_0;
    [SerializeField]
    private RenderTexture velocity_1;
    [SerializeField]
    private RenderTexture pressure;
    [SerializeField]
    private RenderTexture temp_vector;
    [SerializeField]
    private RenderTexture temp_scalar;
    [SerializeField]
    private RenderTexture sdf;

    [SerializeField]
    private SpatialDesc desc;
    [SerializeField]
    private RenderAttachmentSetting renderAttachmentSetting = new RenderAttachmentSetting();

    private const int GROUP_SIZE = 8;

    private static string[] exKernels = new string[]
    {
        "EX_SPH_SCA",
        "EX_SPH_DIR",
        "EX_SPH_OMN",
        "EX_SPH_VOR",
        "EX_BOX_SCA",
        "EX_BOX_DIR",
        "EX_BOX_OMN",
        "EX_BOX_VOR"
    };
    private static string[] obsKernels = new string[]
    {
        "OBS_SPH",
        "OBS_BOX"
    };

    private System.Action<IShape>[] shapeSetups;

    public FluidSimulator(int texelPerUnit, Bounds bounds, ComputeShader compute)
    {
        this.compute = compute;
        desc = GetSpatialDesc(bounds, texelPerUnit);

        shapeSetups = new System.Action<IShape>[]
        {
            SetSphere,
            SetBox
        };

        InitRT();
    }

    public static SpatialDesc GetSpatialDesc(Bounds bounds, int texelPerUnit)
    {
        var desc = new SpatialDesc();
        var res = bounds.size * texelPerUnit;
        res.Scale(Vector3.one * (1f / GROUP_SIZE));
        desc.groupCount = Vector3Int.CeilToInt(res);
        desc.resolution = desc.groupCount * GROUP_SIZE;
        desc.texelSize = new Vector3(1f / res.x, 1f / res.y, 1f / res.z);
        desc.texelPerUnit = texelPerUnit;
        desc.bounds = new Bounds()
        {
            min = bounds.min,
            max = bounds.min + desc.resolution / texelPerUnit
        };
        return desc;
    }

    public void Update(float dt,List<FluidInjector> injectors,List<FluidMotor> motors,
        List<FluidCollider> colliders,List<Vector3> collidersVelocities)
    {
        compute.SetInts("size", desc.resolution.x , desc.resolution.y , desc.resolution.z);
        compute.SetVector("hsize", (Vector3)(desc.resolution - Vector3Int.one ) * 0.5f );
        compute.SetVector("texel_size", desc.texelSize);
        compute.SetFloat("time_step", dt);

        UpdateObstacles(colliders, collidersVelocities);

        Advect(dt);

        Vorticity();

        ExternalForce(dt, injectors, motors);

        Project();

        Swap(ref density_0, ref density_1);

        RenderingAttachment(dt);
    }

    private void UpdateObstacles(List<FluidCollider> colliders, List<Vector3> collidersVelocities)
    {
        SetBoundary(obstacles);
        Clear(obstacles_velocity, Vector4.zero);

        for (int i = 0; i < colliders.Count; i++)
        {
            var collider = colliders[i];
            AddObstacle(obstacles, obstacles_velocity, collider.shape, collider.shapeType, collidersVelocities[i]);
        }
    }

    private void Advect(float dt)
    {
        float d = 1f / (1f + dissipation * dt);

        Advect(velocity_0, velocity_0, velocity_1, d);

        switch (densityAdvection)
        {
            case AdvectionType.BFECC:
                Advect(velocity_0, density_0,   temp_vector,  1f);
                Advect(velocity_0, temp_vector, temp_scalar,  1f , false);
                BFECC( velocity_0, density_0,   temp_scalar,  density_1,d);
                break;
            case AdvectionType.Back:
                Advect(velocity_0, density_0, density_1, d);
                break;
        }
    }

    private void Vorticity()
    {
        if (voticity <= 0)
            return;

        Curl(velocity_1, temp_vector);
        Confinement(temp_vector, velocity_1, voticity);
    }

    private void ExternalForce(float dt,List<FluidInjector> injectors, List<FluidMotor> motors)
    {
        foreach (var injector in injectors)
        {
            AddExternals(density_1, injector.shape, (int)injector.shapeType, injector.amount * dt , 0);
        }

        foreach (var motor in motors)
        {
            AddExternals(velocity_1, motor.shape, (int)motor.shapeType, motor.value * dt * desc.texelPerUnit, (int)motor.motorType + 1);
        }
    }

    private void Project()
    {
        Divergence(velocity_1, temp_vector);

        Clear(pressure, Vector4.zero);
        for (int i = 0; i < JacobianIterations; i++)
        {
            Jacobian(temp_vector, pressure, temp_scalar);
            Swap(ref pressure, ref temp_scalar);
        }

        Project(velocity_1, pressure, velocity_0);
    }

    private void Swap(ref RenderTexture a, ref RenderTexture b)
    {
        var t = a;
        a = b;
        b = t;
    }

    private void SetObstacleTextures(int kernel)
    {
        compute.SetTexture(kernel, "obstacles", obstacles);
        compute.SetTexture(kernel, "obstacles_velocity", obstacles_velocity);
    }

    private void SetBox(IShape shape)
    {
        var box = (Box)shape;
        var m = box.localToWorld * Matrix4x4.TRS(box.center, Quaternion.identity, box.size);
        var center = ((Vector3)m.GetColumn(3) - desc.bounds.min) * desc.texelPerUnit;
        var extents = m.lossyScale * 0.5f * Desc.texelPerUnit;
        var localToWorld = Matrix4x4.TRS(center, m.rotation, Vector3.one);

        compute.SetVector("box_extent", extents);
        compute.SetMatrix("local_to_world", localToWorld);
        compute.SetMatrix("world_to_local", localToWorld.inverse);
    }

    private void SetSphere(IShape shape)
    {
        var sphere = (Sphere)shape;
        var m = sphere.localToWorld * Matrix4x4.TRS(sphere.center, Quaternion.identity, Vector3.one * sphere.radius * 2);
        var center = ((Vector3)m.GetColumn(3) - desc.bounds.min) * desc.texelPerUnit;
        var extents = m.lossyScale * 0.5f * Desc.texelPerUnit;
        var localToWorld = Matrix4x4.TRS(center, m.rotation, Vector3.one);

        compute.SetVector("sphere_scale", extents);
        compute.SetMatrix("local_to_world", localToWorld);
        compute.SetMatrix("world_to_local", localToWorld.inverse);
    }

    private void SetBoundary(RenderTexture target)
    {
        Clear(target, Vector4.zero);

        var kernel = compute.FindKernel("BOUN");
        compute.SetTexture(kernel, "output0", target);

        Matrix4x4 m = Matrix4x4.identity;
        compute.SetMatrix("boundary_id_mat", m);
        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, 1);

        m.m23 = desc.resolution.z - 1;
        compute.SetMatrix("boundary_id_mat", m);
        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, 1);

        m = Matrix4x4.identity;
        m.SetColumn(2, new Vector4(0, 1, 0, 0));
        m.SetColumn(1, new Vector4(0, 0, 1, 0));
        compute.SetMatrix("boundary_id_mat", m);
        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.z, 1);

        m.m13 = desc.resolution.y - 1;
        compute.SetMatrix("boundary_id_mat", m);
        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.z, 1);

        m = Matrix4x4.identity;
        m.SetColumn(2, new Vector4(1, 0, 0, 0));
        m.SetColumn(0, new Vector4(0, 0, 1, 0));
        compute.SetMatrix("boundary_id_mat", m);
        compute.Dispatch(kernel, desc.groupCount.z, desc.groupCount.y, 1);

        m.m03 = desc.resolution.x - 1;
        compute.SetMatrix("boundary_id_mat", m);
        compute.Dispatch(kernel, desc.groupCount.z, desc.groupCount.y, 1);
    }

    private void Clear(RenderTexture output,Vector4 value)
    {
        var kernel = compute.FindKernel("CLR");
        compute.SetTexture(kernel, "output0", output);
        compute.SetVector("clear_value", value);
        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void AddObstacle(RenderTexture obstacles, RenderTexture obstacles_velocity, IShape shape,ShapeType shapeType,Vector3 velocity)
    {
        var kernel = compute.FindKernel(obsKernels[(int)shapeType]);
        compute.SetTexture(kernel, "output0", obstacles);
        compute.SetTexture(kernel, "output1", obstacles_velocity);
        shapeSetups[(int)shapeType](shape);
        compute.SetVector("obstacle_velocity_value", velocity);

        ComputeOnShape(kernel, shape);
    }

    private void Advect(RenderTexture velocity,RenderTexture toAdvect, RenderTexture target, float modulate , bool forward = true)
    {
        var kernel = compute.FindKernel("BCK_ADVECT");
        SetObstacleTextures(kernel);
        compute.SetTexture(kernel, "input0", velocity);
        compute.SetTexture(kernel, "input1", toAdvect);
        compute.SetTexture(kernel, "output0", target);
        compute.SetFloat("modulate", modulate);
        compute.SetFloat("advect_forward", forward ? 1f : -1f);

        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void BFECC(RenderTexture velocity, RenderTexture phi_prime_n, RenderTexture phi_prime_c ,RenderTexture target, float modulate = 1f)
    {
        var kernel = compute.FindKernel("BFECC");
        SetObstacleTextures(kernel);
        compute.SetTexture(kernel, "input0", velocity);
        compute.SetTexture(kernel, "input1", phi_prime_n);
        compute.SetTexture(kernel, "input2", phi_prime_c);
        compute.SetTexture(kernel, "output0", target);
        compute.SetFloat("modulate", modulate);

        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void Curl(RenderTexture velocity, RenderTexture target)
    {
        var kernel = compute.FindKernel("CURL");
        compute.SetTexture(kernel, "input0", velocity);
        compute.SetTexture(kernel, "output0", target);
        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void Confinement(RenderTexture curl, RenderTexture target, float amount)
    {
        var kernel = compute.FindKernel("CONFIN");
        compute.SetTexture(kernel, "input0", curl);
        compute.SetTexture(kernel, "output0", target);
        compute.SetFloat("modulate", amount);
        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void AddExternals(RenderTexture target,IShape shape,
        int shapeType,float value,int motorType)
    {
        var kernel = compute.FindKernel(exKernels[motorType + shapeType*4]);
        SetObstacleTextures(kernel);
        compute.SetTexture(kernel, "output0", target);
        shapeSetups[shapeType](shape);
        compute.SetFloat("external_value", value);
        compute.SetFloat("modulate", 2.4f);

        ComputeOnShape(kernel, shape);
    }

    private void Divergence(RenderTexture source,RenderTexture target)
    {
        var kernel = compute.FindKernel("DIV");
        SetObstacleTextures(kernel);
        compute.SetTexture(kernel, "input0", source);
        compute.SetTexture(kernel, "output0", target);

        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void Jacobian(RenderTexture divergence, RenderTexture source, RenderTexture target)
    {
        var kernel = compute.FindKernel("JACOB");
        SetObstacleTextures(kernel);
        compute.SetVector("jacob_params", new Vector2(-1f, 1f / 6));
        compute.SetTexture(kernel, "input0", divergence);
        compute.SetTexture(kernel, "input1", source);
        compute.SetTexture(kernel, "output0", target);

        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void Project(RenderTexture velocity, RenderTexture pressure, RenderTexture target,float modulate = 1f)
    {
        var kernel = compute.FindKernel("PROJ");
        SetObstacleTextures(kernel);
        compute.SetTexture(kernel, "input0", velocity);
        compute.SetTexture(kernel, "input1", pressure);
        compute.SetTexture(kernel, "output0", target);
        compute.SetFloat("modulate", modulate);

        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void RenderingAttachment(float dt)
    {
        if(renderAttachmentSetting.enableGradient)
        {
            Gradient(density_0, temp_vector);
            for (int i = 0; i < renderAttachmentSetting.smoothGradientIterations; i++)
            {
                Diffuse(temp_vector, velocity_1, dt);
                Swap(ref temp_vector, ref velocity_1);
            }
            Clear(velocity_1, Color.black);
        }

        if(renderAttachmentSetting.enableSDF)
        {
            var res = Mathf.Max(desc.resolution.x, desc.resolution.y, desc.resolution.z);
            var iter = (int)Mathf.Log(Mathf.NextPowerOfTwo(res),2);

            Step(density_0, sdf, renderAttachmentSetting.sdfThreshold);
            for (int i = iter - 1; i >= 0 ; i--)
            {
                JFA(sdf, velocity_1, i);
                Swap(ref sdf, ref velocity_1);
            }
            Clear(velocity_1, Color.black);
        }
    }

    private void Step(RenderTexture source,RenderTexture target,float value)
    {
        var kernel = compute.FindKernel("JFA_INIT");
        SetObstacleTextures(kernel);
        compute.SetFloat("modulate", value);
        compute.SetTexture(kernel, "input0", source);
        compute.SetTexture(kernel, "output0", target);

        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void JFA(RenderTexture source, RenderTexture target, int pow)
    {
        var kernel = compute.FindKernel("JFA");
        SetObstacleTextures(kernel);
        compute.SetInt("jfa_step", (int)Mathf.Pow(2, pow));
        compute.SetTexture(kernel, "input0", source);
        compute.SetTexture(kernel, "output0", target);

        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void AmbOc(RenderTexture sdf, RenderTexture gradient, RenderTexture target)
    {
        var kernel = compute.FindKernel("AMBOC");
        SetObstacleTextures(kernel);
        compute.SetTexture(kernel, "input0", sdf);
        compute.SetTexture(kernel, "input1", gradient);
        compute.SetTexture(kernel, "output0", target);

        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void Diffuse(RenderTexture source, RenderTexture target, float diffuseFactor)
    {
        var kernel = compute.FindKernel("JACOB");
        SetObstacleTextures(kernel);
        compute.SetVector("jacob_params", new Vector2(1f / diffuseFactor, 1f / (1f / diffuseFactor + 6)));
        compute.SetTexture(kernel, "input0", source);
        compute.SetTexture(kernel, "input1", source);
        compute.SetTexture(kernel, "output0", target);

        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    private void Gradient(RenderTexture source, RenderTexture target)
    {
        var kernel = compute.FindKernel("GRAD");
        SetObstacleTextures(kernel);
        compute.SetTexture(kernel, "input0", source);
        compute.SetTexture(kernel, "output0", target);

        compute.Dispatch(kernel, desc.groupCount.x, desc.groupCount.y, desc.groupCount.z);
    }

    //run compute on shape boundary
    private void ComputeOnShape(int kernel,IShape shape)
    {
        var bounds = ToBoundsInTexel(shape);
        if (bounds.size.x < 1 || bounds.size.y < 1 || bounds.size.z < 1)
            return;
        compute.SetInts("offset", bounds.min.x, bounds.min.y, bounds.min.z);
        var g_count = Vector3Int.CeilToInt((Vector3)bounds.size / GROUP_SIZE);
        compute.Dispatch(kernel, g_count.x, g_count.y, g_count.z);
    }

    private BoundsInt ToBoundsInTexel(IShape shape)
    {
        BoundsInt res = new BoundsInt()
        {
            min = Vector3Int.FloorToInt((shape.bounds.min - desc.bounds.min) * desc.texelPerUnit),
            max = Vector3Int.FloorToInt((shape.bounds.max - desc.bounds.min) * desc.texelPerUnit)
        };
        res.min = Vector3Int.Max(Vector3Int.zero, res.min);
        res.max = Vector3Int.Min(desc.resolution, res.max + Vector3Int.one);
        return res;
    }

    public void Dispose()
    {
        obstacles?.Release();
        obstacles_velocity?.Release();
        density_0?.Release();
        density_1?.Release();
        velocity_0?.Release();
        velocity_1?.Release();
        pressure?.Release();
        temp_scalar?.Release();
        temp_vector?.Release();
        sdf?.Release();
    }

    private void InitRT()
    {
        RenderTextureDescriptor desc = new RenderTextureDescriptor()
        {
            dimension = TextureDimension.Tex3D,
            width = this.desc.resolution.x,
            height = this.desc.resolution.y,
            volumeDepth = this.desc.resolution.z,
            enableRandomWrite = true,
            depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None,
            depthBufferBits = 0,
            msaaSamples = 1
        };

        desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
        density_0 = new RenderTexture(desc);
        density_0.Create();
        density_1 = new RenderTexture(desc);
        density_1.Create();
        temp_scalar = new RenderTexture(desc);
        temp_scalar.Create();
        pressure = new RenderTexture(desc);
        pressure.Create();

        desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
        obstacles_velocity = new RenderTexture(desc);
        obstacles_velocity.Create();
        velocity_0 = new RenderTexture(desc);
        velocity_0.Create();
        velocity_1 = new RenderTexture(desc);
        velocity_1.Create();
        temp_vector = new RenderTexture(desc);
        temp_vector.Create();
        sdf = new RenderTexture(desc);
        sdf.Create();

        desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
        obstacles = new RenderTexture(desc);
        obstacles.Create();

        Clear(density_0, Color.black);
        Clear(velocity_0, Color.black);

#if FS_DEBUG
        density_0.name = "d_0";
        density_1.name = "d_1";
        pressure.name = "p";
        temp_scalar.name = "t_s";

        obstacles_velocity.name = "o_v";
        velocity_0.name = "v_0";
        velocity_1.name = "v_1";
        temp_vector.name = "t_v";

        obstacles.name = "o";
#endif
    }
}