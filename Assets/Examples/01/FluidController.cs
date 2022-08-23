using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidController : MonoBehaviour
{
    public FluidField field;
    public GameObject a, b;
    [SerializeField,HideInInspector]
    private StarterAssets.StarterAssetsInputs input;

    private void OnEnable()
    {
        input = GetComponent<StarterAssets.StarterAssetsInputs>();
    }

    private void Update()
    {
        if(input.ActionA && !a.activeSelf)
        {
            a.SetActive(true);
            field.ExecuteInNextUpdate(() =>
            {
                a.SetActive(false);
            });
            input.ActionA = false;
        }

        if (input.ActionB && !b.activeSelf)
        {
            b.SetActive(true);
            field.ExecuteInNextUpdate(() =>
            {
                b.SetActive(false);
            });
            input.ActionB = false;
        }
    }
}
