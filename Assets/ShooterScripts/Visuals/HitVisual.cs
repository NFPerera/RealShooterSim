using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RealShooter.Ballistics.Visuals
{
    public class HitVisual : MonoBehaviour
    {
        [SerializeField] private GameObject hitVisualPrefab;
        private void Start()
        {
            PhysicsManager.Instance.OnProjectileHit += OnProjectileHit;
        }

        private void OnProjectileHit(Projectile arg1, RaycastHit arg2)
        {
            var vis = Instantiate(hitVisualPrefab);

            vis.transform.position = arg1.Position;

            StartCoroutine(VisualDespawn(vis));
        }


        private IEnumerator VisualDespawn(GameObject go)
        {
            yield return new WaitForSeconds(2f);
            
            Destroy(go);
            
            
        }
    }
}