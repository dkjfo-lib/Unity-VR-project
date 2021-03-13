using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CableConnector : MonoBehaviour
{
    public Transform obj1;
    public Transform obj2;
    LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    void Update()
    {
        Vector3 diff = obj1.position - obj2.position;
        Vector3 midPoint = diff / 2 + obj2.position - Vector3.up * .2f;
        Vector3 mid1Point = Vector3.Scale((obj1.position - midPoint), new Vector3(1, .5f, 1)) + midPoint;
        Vector3 mid2Point = Vector3.Scale((obj2.position - midPoint), new Vector3(1, .5f, 1)) + midPoint;
        lineRenderer.SetPositions(new[]
        {
            obj1.position,
            mid1Point,
            midPoint,
            mid2Point,
            obj2.position,
        });
    }
}
