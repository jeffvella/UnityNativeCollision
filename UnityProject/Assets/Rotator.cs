using UnityEngine;
using Unity.Mathematics;

public class Rotator : MonoBehaviour
{
    public float3 Angle;
    public float Speed;

    void Update()
    {
        transform.rotation = transform.rotation * math.slerp(quaternion.identity, quaternion.Euler(Angle), Time.deltaTime * Speed);
    }
}
