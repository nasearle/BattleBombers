using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class Bomb : NetworkBehaviour, IKnockable {
    [SerializeField] private float moveSpeed;
    [SerializeField] private float gravity;
    [SerializeField] private LayerMask environmentLayerMask;
    [SerializeField] private LayerMask bombLayerMask;
    [SerializeField] private LayerMask floorLayerMask;
    
    
    [SerializeField] private float testSphereColliderRadius = 0.5f;
    [SerializeField] private float testFloorColliderCastDistance = 0.1f;
    
    private Vector3 _horizontalDirection;
    private Vector3 _verticalVelocity;
    private Vector3 _velocity;
    private bool _isMovingHorizontally;
    private bool _isGrounded;
    private float _sphereColliderRadius;

    private void Awake() {
        _sphereColliderRadius = GetComponent<SphereCollider>().radius;
    }

    private void FixedUpdate() {
        if (!IsServerStarted) {
            return;
        }
        
        HandleFloorCollisions();
        
        if (!_isGrounded) {
            _verticalVelocity.y -= gravity;
        }

        if (_isMovingHorizontally && _horizontalDirection != Vector3.zero) {
            HandleBombCollisions();
            HandleEnvironmentCollisions();
        }

        Vector3 totalMovement = Vector3.zero;
    
        if (_isMovingHorizontally && _horizontalDirection != Vector3.zero) {
            totalMovement += _horizontalDirection * (moveSpeed * Time.fixedDeltaTime);
        } else {
            _isMovingHorizontally = false;
        }
    
        if (!_isGrounded) {
            totalMovement += _verticalVelocity * Time.fixedDeltaTime;
        }
    
        if (totalMovement != Vector3.zero) {
            transform.position += totalMovement;
        }
    }

    public static void SpawnBomb(NetworkObject networkObject, Vector3 position) {
        NetworkObjectManager.Instance.SpawnBomb(networkObject, position);
    }

    public void Knock(Vector3 direction) {
        _horizontalDirection += direction;
        _horizontalDirection.Normalize();
        _isMovingHorizontally = true;
    }

    public void Stop() {
        _horizontalDirection = Vector3.zero;
        _isMovingHorizontally = false;
    }

    private void HandleBombCollisions() {
        Vector3 targetPosition = transform.position + _horizontalDirection * (moveSpeed * Time.fixedDeltaTime);
        Collider[] hitColliders = Physics.OverlapSphere(targetPosition, _sphereColliderRadius, bombLayerMask);
        
        Vector3 closestTouchPosition = Vector3.zero;
        float closestDistance = float.MaxValue;
        bool foundCollision = false;
        
        foreach (Collider hitCollider in hitColliders) {
            if (hitCollider.TryGetComponent(out Bomb otherBomb) && otherBomb != this) {
                if (!otherBomb.IsMoving()) {
                    Vector3 lineStart = transform.position;
                    Vector3 lineDirection = _horizontalDirection.normalized;
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
                        otherBomb.Knock(_horizontalDirection);
                    }
                } else {
                    otherBomb.Knock(_horizontalDirection);
                    Stop();
                }
            }
        }
        
        // Move to the closest touch position and stop
        if (foundCollision) {
            Vector3 separationDistance = _horizontalDirection * 0.05f;
            transform.position = closestTouchPosition - separationDistance;
            Stop();
        }
    }
    
    private void HandleFloorCollisions() {
        if (_isGrounded) {
            Collider[] overlapping = Physics.OverlapSphere(
                transform.position,
                _sphereColliderRadius,
                floorLayerMask
            );
            
            if (overlapping.Length > 0) {
                _isGrounded = true;
            } else {
                _isGrounded = false;
            }
        } else {
            float fallDistance = Mathf.Abs(_verticalVelocity.y * Time.fixedDeltaTime);
            float castDistance = fallDistance + 0.1f; // Small buffer
            
            if (Physics.SphereCast(
                    transform.position,
                    _sphereColliderRadius,
                    Vector3.down,
                    out RaycastHit hit,
                    castDistance,
                    floorLayerMask
                )) {
                // Hit the floor while falling
                float newY = hit.point.y + _sphereColliderRadius;
                transform.position = new Vector3(
                    transform.position.x,
                    newY,
                    transform.position.z
                );
                _verticalVelocity.y = 0;
                _isGrounded = true;
            }
        }
    }
    
    private void OnDrawGizmos() {
        Gizmos.DrawWireSphere(transform.position, testSphereColliderRadius);

        if (Physics.SphereCast(
                transform.position,
                testSphereColliderRadius,
                Vector3.down,
                out RaycastHit hit,
                testFloorColliderCastDistance,
                floorLayerMask
            )) {
            Gizmos.color = Color.green;
            Vector3 sphereCastMidpoint = transform.position + (Vector3.down * testFloorColliderCastDistance);
            Gizmos.DrawWireSphere(sphereCastMidpoint, testSphereColliderRadius);
            Gizmos.DrawSphere(hit.point, 0.1f);
            Debug.DrawLine(transform.position, sphereCastMidpoint, Color.green);
        } else {
            Gizmos.color = Color.red;
            Vector3 sphereCastMidpoint = transform.position + (Vector3.down * testFloorColliderCastDistance);
            Gizmos.DrawWireSphere(sphereCastMidpoint, testSphereColliderRadius);
            Debug.DrawLine(transform.position, sphereCastMidpoint, Color.red);
        }
    }
    
    private void HandleEnvironmentCollisions() {
        float sphereCastDistance = moveSpeed * Time.fixedDeltaTime;
        
        // Check X-axis movement
        if (Mathf.Abs(_horizontalDirection.x) > 0.01f) {
            Vector3 xDirection = new Vector3(_horizontalDirection.x, 0, 0).normalized;
            if (!BombCanMove(xDirection, sphereCastDistance)) {
                _horizontalDirection.x = 0;
            }
        }
        
        // Check Z-axis movement
        if (Mathf.Abs(_horizontalDirection.z) > 0.01f) {
            Vector3 zDirection = new Vector3(0, 0, _horizontalDirection.z).normalized;
            if (!BombCanMove(zDirection, sphereCastDistance)) {
                _horizontalDirection.z = 0;
            }
        }
    }
    
    public bool BombCanMove(Vector3 direction, float sphereCastDistance) {
        Vector3 castDirection = direction.normalized;
        
        Collider[] touchingWall = Physics.OverlapSphere(
            transform.position + castDirection * sphereCastDistance,
            _sphereColliderRadius,
            environmentLayerMask
        );
        
        return !(touchingWall.Length > 0);
    }

    public bool IsMoving() {
        return _isMovingHorizontally;
    }

    // TODO: remove this
    public Vector3 GetMoveDirection() {
        return _horizontalDirection;
    }

    public float GetMoveSpeed() {
        return moveSpeed;
    }
}
