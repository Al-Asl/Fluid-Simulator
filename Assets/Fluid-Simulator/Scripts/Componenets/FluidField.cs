using System.Collections.Generic;
using UnityEngine;

public class FluidField : MonoBehaviour
{
    public enum RenderType
    {
        Default,
        Debug
    }

    [SerializeField]
    public ComputeShader compute;

    [SerializeField]
    public RenderType renderType;
    [Space,SerializeField]
    public int targetFrameRate = 30;
    [SerializeField]
    public int MinFrameRate = 16;
    [SerializeField]
    public int texelPerUnit = 16;
    [SerializeField]
    public Vector3 size = Vector3.one;
    [Space,SerializeField]
    public AdvectionType densityAdvection;
    [SerializeField]
    public int jacobianIterations = 20;
    [SerializeField]
    public float vorticity = 0.1f;
    [SerializeField]
    public float dissipation = 0.1f;

    [Space, SerializeReference]
#pragma warning disable CS0108
    private BaseVolumeRenderer renderer = new VolumeRenderer();
#pragma warning restore CS0108

    [SerializeField,HideInInspector]
    private FluidSimulator fluidSimulator;
    private float lastUpdate;

    [SerializeField,HideInInspector]
    private RenderType _renderType;

    private List<FluidInjector> injectors = new List<FluidInjector>();
    private List<FluidMotor> motors = new List<FluidMotor>();
    private List<FluidCollider> colliders = new List<FluidCollider>();
    private List<Vector3> collidersLastPos = new List<Vector3>();
    private List<Vector3> collidersVelocities = new List<Vector3>();
    private List<System.Action> nextUpdate = new List<System.Action>();

    private Bounds bounds => new Bounds(transform.position + size * 0.5f, size);

    private void OnEnable()
    {
        fluidSimulator = new FluidSimulator(texelPerUnit,bounds,compute);
        renderer.Initialize(fluidSimulator);

        lastUpdate = Time.time;
    }

    private void Update()
    {
        SwitchRenderer();

        float dt = 1f / targetFrameRate;
        if (Time.time - lastUpdate >= dt)
        {
            dt = Mathf.Min(1f / MinFrameRate, Time.time - lastUpdate);
            lastUpdate = Time.time;

            UpdateVelocities(dt);

            fluidSimulator.densityAdvection = densityAdvection;
            fluidSimulator.JacobianIterations = jacobianIterations;
            fluidSimulator.voticity = vorticity;
            fluidSimulator.dissipation = dissipation;

            fluidSimulator.Update(dt, injectors, motors, colliders, collidersVelocities);

            ExecuteActions();
        }

        renderer.Update();
        UpdateInfo();
    }

    /// <summary>
    /// run the action in the next fluid update
    /// </summary>
    public void ExecuteInNextUpdate(System.Action action)
    {
        nextUpdate.Add(action);
    }

    private void ExecuteActions()
    {
        for (int i = 0; i < nextUpdate.Count; i++)
            nextUpdate[i]();
        nextUpdate.Clear();
    }

    public static FluidField GetFeild(GameObject target)
    {
        var field = target.GetComponentInParent<FluidField>();
        if (field != null)
            return field;
        else
            return FindObjectOfType<FluidField>();
    }

    private void SwitchRenderer()
    {
        if (renderType != _renderType)
        {
            renderer?.Dispose();
            switch (renderType)
            {
                case RenderType.Default:
                    renderer = new VolumeRenderer();
                    break;
                case RenderType.Debug:
                    renderer = new VolumeDebugRenderer();
                    break;
            }
            renderer.Initialize(fluidSimulator);
            _renderType = renderType;
        }
    }

    void UpdateVelocities(float dt)
    {
        for (int i = 0; i < colliders.Count; i++)
        {
            var p = colliders[i].shape.bounds.center;
            var lp = collidersLastPos[i];

            collidersVelocities[i] = (p - lp) / dt;

            collidersLastPos[i] = p;
        }
    }

    public void AddInjector(FluidInjector injector)
    {
        injectors.Add(injector);
    }

    public void RemoveInjector(FluidInjector injector)
    {
        injectors.Remove(injector);
    }

    public void AddMotor(FluidMotor motor)
    {
        motors.Add(motor);
    }

    public void RemoveMotor(FluidMotor motor)
    {
        motors.Remove(motor);
    }

    public void AddCollider(FluidCollider collider)
    {
        colliders.Add(collider);
        collidersLastPos.Add(collider.shape.bounds.center);
        collidersVelocities.Add(Vector3.zero);
    }

    public void RemoveCollider(FluidCollider collider)
    {
        var index = colliders.FindIndex((a) => a == collider);
        if(index > -1)
        {
            colliders.RemoveAt(index);
            collidersLastPos.RemoveAt(index);
            collidersVelocities.RemoveAt(collidersVelocities.Count - 1);
        }
    }

    private void OnDisable()
    {
        fluidSimulator?.Dispose();
        fluidSimulator = null;

        renderer.Dispose();

        injectors.Clear();
        motors.Clear();
        colliders.Clear();
        collidersLastPos.Clear();
        collidersVelocities.Clear();
    }

#if UNITY_EDITOR
    private System.Text.StringBuilder info = new System.Text.StringBuilder();
#endif

    private void UpdateInfo()
    {
#if UNITY_EDITOR
        info.Clear();
        var density = fluidSimulator.Density;
        Vector3Int res = fluidSimulator.Desc.resolution;
        float size = res.x * res.y * res.z * 16/(8*1024*1024);
        info.AppendLine($"texture resolution : {res}");
        info.AppendLine($"density size : {size * 2} MB");
        info.AppendLine($"velocity size : {size * 8} MB");
        info.AppendLine($"obstacles size : {size * 4.5f} MB");
        info.AppendLine($"other size : {size * 6} MB");
        info.AppendLine($"total size : {size * 20.5f} MB");
        info.AppendLine($"colliders {colliders.Count}, injectors {injectors.Count}, motors {motors.Count}");
#endif
    }

    public override string ToString()
    {
#if UNITY_EDITOR
        return info.ToString();
#else
        return base.ToString();
#endif
    }

    private void OnDrawGizmos()
    {
        //TODO : move it to editor script, and add handle control
#if UNITY_EDITOR
        var bounds = FluidSimulator.GetSpatialDesc(this.bounds, texelPerUnit).bounds;
        HandleEx.DrawBounds(bounds, Color.yellow);
#endif
        Gizmos.color = Color.cyan;
        DrawGrid(texelPerUnit);
    }

    private void DrawGrid(float res)
    {
        var pos = transform.position;
        float step = 1 / res;
        for (int i = 0; i <= res; i++)
        {
            Gizmos.DrawLine(pos + new Vector3(step * i, 0, 0),pos + new Vector3(step * i, 0, 1f));
            Gizmos.DrawLine(pos + new Vector3(0, 0, step * i),pos +new Vector3(1f, 0, step * i));
        }
    }
}