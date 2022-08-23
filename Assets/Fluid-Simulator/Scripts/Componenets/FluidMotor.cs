using UnityEngine;

public enum MotorType
{
    Direction,
    Omni,
    Vortex
}

public class FluidMotor : MonoBehaviour
{
    public MotorType motorType;
    public float value = 1f;

    public ShapeType shapeType;
    [SerializeReference]
    public IShape shape = new Sphere(Vector3.zero,0.5f,Matrix4x4.identity);

    private FluidField field;

    private void OnDisable()
    {
        if (field != null)
            field.RemoveMotor(this);
    }

    private void OnEnable()
    {
        UpdateShape();

        field = FluidField.GetFeild(gameObject);

        if (field != null)
            field.AddMotor(this);
    }

    private void Update()
    {
        UpdateShape();
    }

    private void UpdateShape()
    {
        ShapeUtility.SetShape(ref shape, shapeType, transform);
        ShapeUtility.UpdateShape(ref shape, shapeType, transform);
    }

    private void OnDrawGizmos()
    {
        ShapeUtility.SetShape(ref shape, shapeType, transform);
        ShapeUtility.ShapeGizmos(transform, shape, Color.blue);
    }
}