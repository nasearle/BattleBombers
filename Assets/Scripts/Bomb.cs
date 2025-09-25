using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class Bomb : NetworkBehaviour, IKnockable {
    [SerializeField] private float moveSpeed;
    [SerializeField] private LayerMask environmentLayerMask;
    [SerializeField] private LayerMask bombLayerMask;
    
    private Vector3 _moveDirection;
    private bool _isMoving;
    private float _sphereColliderRadius;

    private void Awake() {
        _sphereColliderRadius = GetComponent<SphereCollider>().radius;
    }

    private void FixedUpdate() {
        if (!IsServerStarted) {
            return;
        }
        
        if (_isMoving && _moveDirection != Vector3.zero) {
            HandleBombCollisions();
            
            HandleEnvironmentCollisions();
            
            if (_moveDirection != Vector3.zero) {
                transform.position += _moveDirection * (moveSpeed * Time.fixedDeltaTime);
            } else {
                _isMoving = false;
            }
        } else {
            _isMoving = false;
        }
    }

    public static void SpawnBomb(NetworkObject networkObject, Vector3 position) {
        NetworkObjectManager.Instance.SpawnBomb(networkObject, position);
    }

    public void Knock(Vector3 direction) {
        _moveDirection += direction;
        _moveDirection.Normalize();
        _isMoving = true;
    }

    public void Stop() {
        _moveDirection = Vector3.zero;
        _isMoving = false;
    }

    private void HandleBombCollisions() {
        Vector3 targetPosition = transform.position + _moveDirection * (moveSpeed * Time.fixedDeltaTime);
    
        Collider[] hitColliders = Physics.OverlapSphere(targetPosition, _sphereColliderRadius, bombLayerMask);
        
        foreach (Collider hitCollider in hitColliders) {
            if (hitCollider.TryGetComponent(out Bomb otherBomb) && otherBomb != this) {
                if (!otherBomb.IsMoving()) {
                    otherBomb.Knock(_moveDirection);
                    Stop();
                } else {
                    otherBomb.Knock(_moveDirection);
                }
            }
        }
    }
    
    private void HandleEnvironmentCollisions() {
        float sphereCastDistance = moveSpeed * Time.fixedDeltaTime;
        
        // Check X-axis movement
        if (Mathf.Abs(_moveDirection.x) > 0.01f) {
            Vector3 xDirection = new Vector3(_moveDirection.x, 0, 0).normalized;
            if (!BombCanMove(xDirection, sphereCastDistance)) {
                _moveDirection.x = 0;
            }
        }
        
        // Check Z-axis movement
        if (Mathf.Abs(_moveDirection.z) > 0.01f) {
            Vector3 zDirection = new Vector3(0, 0, _moveDirection.z).normalized;
            if (!BombCanMove(zDirection, sphereCastDistance)) {
                _moveDirection.z = 0;
            }
        }
    }
    
    public bool BombCanMove(Vector3 direction, float sphereCastDistance) {
        Vector3 origin = transform.position;
        Vector3 castDirection = direction.normalized;
        
        return !Physics.SphereCast(
            origin,
            _sphereColliderRadius,
            castDirection,
            out RaycastHit hit,
            sphereCastDistance,
            environmentLayerMask
        );
    }

    public bool IsMoving() {
        return _isMoving;
    }

    public Vector3 GetMoveDirection() {
        return _moveDirection;
    }

    public float GetMoveSpeed() {
        return moveSpeed;
    }
}
