using System;
using UnityEngine;

public class TrailDelay : MonoBehaviour
{
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private SphereCollider bulletCollider;
    [SerializeField] private float trailDelay;

    private float timer;
    private void OnEnable()
    {
        timer = 0;
        
        trailRenderer.enabled = false;
        bulletCollider.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(timer >= trailDelay)
        {
            trailRenderer.enabled = true;
            bulletCollider.enabled = true;
            this.enabled = false;
        }
        else
            timer += Time.deltaTime;
    }
}
