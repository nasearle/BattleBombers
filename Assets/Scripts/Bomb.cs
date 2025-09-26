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
        
        Vector3 closestTouchPosition = Vector3.zero;
        float closestDistance = float.MaxValue;
        bool foundCollision = false;
        
        foreach (Collider hitCollider in hitColliders) {
            if (hitCollider.TryGetComponent(out Bomb otherBomb) && otherBomb != this) {
                if (!otherBomb.IsMoving()) {
                    Vector3 lineStart = transform.position;
                    Vector3 lineDirection = _moveDirection.normalized;
                    float movingSphereRadius = _sphereColliderRadius;
                    
                    Vector3 stationarySphereCenter = otherBomb.transform.position;
                    float stationarySphereRadius = otherBomb.GetComponent<SphereCollider>().radius;
                    
                    // Vector from line start to stationary sphere center
                    Vector3 toStationary = stationarySphereCenter - lineStart;

                    // Project onto the line direction
                    float projectionLength = Vector3.Dot(toStationary, lineDirection);

                    // Distance from line to stationary sphere center
                    Vector3 closestPointOnLine = lineStart + lineDirection * projectionLength;
                    float distanceToLine = Vector3.Distance(stationarySphereCenter, closestPointOnLine);

                    // Combined radius for touching
                    float combinedRadius = movingSphereRadius + stationarySphereRadius;

                    // Check if the line gets close enough for spheres to touch
                    if (distanceToLine <= combinedRadius) {
                        // Calculate where the moving sphere first touches the stationary sphere
                        float halfChord = Mathf.Sqrt(combinedRadius * combinedRadius - distanceToLine * distanceToLine);
                        float t = projectionLength - halfChord;
    
                        if (t > 0) {
                            Vector3 touchPosition = lineStart + lineDirection * t;
                            float distanceToTouch = Vector3.Distance(transform.position, touchPosition);
                            
                            // Keep track of the closest touch position
                            if (distanceToTouch < closestDistance) {
                                closestDistance = distanceToTouch;
                                closestTouchPosition = touchPosition;
                                foundCollision = true;
                            }
                        }
                        otherBomb.Knock(_moveDirection);
                    }
                } else {
                    otherBomb.Knock(_moveDirection);
                }
            }
        }
        
        // Move to the closest touch position and stop
        if (foundCollision) {
            transform.position = closestTouchPosition;
            Stop();
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
