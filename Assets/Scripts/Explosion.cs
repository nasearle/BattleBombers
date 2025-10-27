using System;
using FishNet.Object;
using UnityEngine;

public class Explosion : NetworkBehaviour {
    [SerializeField] private float explosionRadius;
    [SerializeField] private float maxExplosionRadius;
    [SerializeField] private float expansionSpeed;
    [SerializeField] private float durationMax;
    [SerializeField] private LayerMask damageableLayer;
    [SerializeField] private SphereCollider bombSphereCollider;
    [SerializeField] private GameObject explosionVisualGameObject;

    private float _durationTimer;

    private void OnEnable() {
        explosionRadius = bombSphereCollider.radius;
        explosionVisualGameObject.transform.localScale = Vector3.one * bombSphereCollider.radius * 2.0f;
        _durationTimer = durationMax;
        
        explosionVisualGameObject.SetActive(true);
    }

    private void OnDisable() {
        explosionVisualGameObject.SetActive(false);
    }

    private void Update() {
        if (!IsServerStarted) {
            return;
        }
        
        _durationTimer -= Time.deltaTime;

        if (_durationTimer <= 0) {
            DestroySelf();
        }
    }

    private void FixedUpdate() {
        if (!IsServerStarted) {
            return;
        }
        
        if (explosionRadius < maxExplosionRadius) {
            explosionRadius += expansionSpeed * Time.deltaTime;
            
            float scaleValue = explosionRadius * 2.0f;

            // Replace this visual with an animation that is triggered on all clients with an RPC
            explosionVisualGameObject.transform.localScale = new Vector3(scaleValue, scaleValue, scaleValue);
        }
        
        // Detect objects within the explosion radius
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius, damageableLayer);
        foreach (var hitCollider in hitColliders) {
            // Check if the object has a damageable component
            if (hitCollider.TryGetComponent(out IDamageable damageable)) {
                damageable.Damage();
            }
        }
    }
    
    private void DestroySelf() {
        Despawn();
    }

    void OnDrawGizmos() {
        // Visualize the explosion radius in the editor
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
