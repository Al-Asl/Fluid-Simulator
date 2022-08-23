using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour
{
    public float Speed = 720;
    

    void Update()
    {
        transform.Rotate(transform.up,Speed*Time.deltaTime);
    }
}
