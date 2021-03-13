using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class UpdateValveProgress : MonoBehaviour
{
    public Valve targetValve;
    Text text;

    private void Start()
    {
        text = GetComponent<Text>();    
    }

    void Update()
    {
        text.text = $"VALVE OPEN\n{targetValve.valveOpenness * 100:f0}%";
    }
}
