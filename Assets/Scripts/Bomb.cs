using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEngine;

public class Bomb : NetworkBehaviour, IKnockable {
    [SerializeField] private float moveSpeed;
    [SerializeField] private float gravity;
    [SerializeField] private LayerMask environmentLayerMask;
    [SerializeField] private LayerMask bombLayerMask;
    [SerializeField] private LayerMask collisionLayerMask;
    [SerializeField] private float skinWidth;
    
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
        
        // Handle surface movement projection
        // HandleSurfaceMovement();
    
        // Handle ramp transitions
        // HandleRampTransitions();

        Vector3 totalMovement = Vector3.zero;
    
        if (_isMovingHorizontally && _horizontalDirection != Vector3.zero) {
            HandleBombCollisions();
            HandleEnvironmentCollisions();
            totalMovement += _horizontalDirection * moveSpeed;
        } else {
            _isMovingHorizontally = false;
        }
    
        if (!_isGrounded) {
            _verticalVelocity.y -= gravity;
            totalMovement += _verticalVelocity;
        }
        
        // check collisions with totalMovement
        // constrain it
        // handle isGrounded and isMoving flags, either here or in handler functions
    
        if (totalMovement != Vector3.zero) {
            transform.position += totalMovement * Time.fixedDeltaTime;
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

    private void HandleCollisions(Vector3 totalMovement) {
        // get all collisions with spherecastall
        // get closest collision
        // if within skin width distance then don't move
        // else move to that point minus the skinwidth
        // if it's a wall or floor then constrain movementDirection
        // check if any of the other hit points are within sphereColliderRadius + skinwidth of this point and if they
        // are walls or floor, constrain movement, then knock any bombs with that new movement
        //
        
        Vector3 direction = (totalMovement - transform.position).normalized;
        float distance = totalMovement.magnitude;

        RaycastHit[] hits = Physics.SphereCastAll(transform.position, _sphereColliderRadius, direction, distance, collisionLayerMask);

        RaycastHit[] hitsOrderedByDistance = hits.OrderBy(hit => hit.distance).ToArray();
        RaycastHit closestHit = hitsOrderedByDistance.First();

        // Only reposition if farther away than skin width. Otherwise, the bomb will be pushed away and this could cause
        // overlaps with other objects.
        if (closestHit.distance > skinWidth) {
            Vector3 sphereCenterOnHit = transform.position + (direction * closestHit.distance);
            Vector3 newPosition = sphereCenterOnHit - direction * skinWidth;
            transform.position = newPosition;
        }
        
        // If it's in the environment layer then constrain movement
        if (((1 << closestHit.collider.gameObject.layer) & environmentLayerMask) != 0) {
            // Project total movement onto the surface
            Vector3 constrainedMovement = Vector3.ProjectOnPlane(totalMovement, closestHit.normal);
    
            // Split back into horizontal and vertical components
            _horizontalDirection = new Vector3(constrainedMovement.x, 0, constrainedMovement.z).normalized;
            _verticalVelocity = new Vector3(0, constrainedMovement.y, 0);
        }

        // 6. Check if any other hit points are within sphereColliderRadius + skinwidth
        // and if they are walls or floor, constrain movement, then knock any bombs
        // foreach (RaycastHit hit in hits) {
        //     if (hit != closestHit) {
        //         float distanceToHit = Vector3.Distance(newPosition, hit.point);
        //
        //         if (distanceToHit <= _sphereColliderRadius + skinWidth) {
        //             if (hit.collider.CompareTag("Wall") || hit.collider.CompareTag("Floor")) {
        //                 // Constrain movement based on this surface too
        //                 _horizontalDirection = ConstrainMovement(_horizontalDirection, hit.normal);
        //             } else if (hit.collider.CompareTag("Bomb")) {
        //                 // Knock the bomb
        //                 hit.collider.GetComponent<Bomb>().Knock(_horizontalDirection);
        //             }
        //         }
        //     }
        // }
        
        
        // // Use SphereCastAll but filter results
        // RaycastHit[] hits = Physics.SphereCastAll(...);
        // if (hits.Length > 0)
        // {
        //     RaycastHit closestHit = hits.OrderBy(hit => hit.distance).First();
        //     transform.position = closestHit.point;
        //
        //     // Only knock bombs that are within a small radius of the hit point
        //     float knockRadius = bombRadius * 1.1f; // Slightly larger than bomb radius
        //     foreach (RaycastHit hit in hits)
        //     {
        //         if (hit.collider.CompareTag("Bomb") && 
        //             Vector3.Distance(hit.point, closestHit.point) <= knockRadius)
        //         {
        //             hit.collider.GetComponent<Bomb>().KnockBomb(velocity);
        //         }
        //     }
        // }
        //
        //
        //
        // // Get all hits from SphereCastAll
        // RaycastHit[] hits = Physics.SphereCastAll(transform.position, bombRadius, direction, distance, layerMask);
        //
        // if (hits.Length > 0)
        // {
        //     // Find closest hit
        //     RaycastHit closestHit = hits.OrderBy(hit => hit.distance).First();
        //
        //     // Calculate where the center of the moving bomb will be at the closest hit point
        //     Vector3 bombCenterAtHit = transform.position + direction * closestHit.distance;
        //
        //     // Move bomb to that position
        //     transform.position = bombCenterAtHit;
        //
        //     // Check which other hits are within knock radius of the bomb at that position
        //     float knockRadius = bombRadius * 2.0f; // Adjust this multiplier as needed
        //
        //     foreach (RaycastHit hit in hits)
        //     {
        //         if (hit.collider.CompareTag("Bomb"))
        //         {
        //             // Calculate distance from bomb center to this hit point
        //             float distanceToHit = Vector3.Distance(bombCenterAtHit, hit.point);
        //     
        //             // Only knock if within knock radius
        //             if (distanceToHit <= knockRadius)
        //             {
        //                 hit.collider.GetComponent<Bomb>().KnockBomb(velocity);
        //             }
        //         }
        //     }
        // }
        //
        //
        //
        //
        // // Use your existing HandleBombCollisions logic to get closestTouchPosition
        // // Then for the new SphereCastAll approach:
        //
        // if (foundCollision) {
        //     // Use the existing closestTouchPosition calculation
        //     Vector3 bombCenterAtHit = closestTouchPosition;
        //
        //     // Check which other bombs are within knock radius
        //     float knockRadius = _sphereColliderRadius * 2.0f; // Adjust as needed
        //
        //     foreach (Collider hitCollider in hitColliders) {
        //         if (hitCollider.TryGetComponent(out Bomb otherBomb) && otherBomb != this) {
        //             float distanceToBomb = Vector3.Distance(bombCenterAtHit, otherBomb.transform.position);
        //     
        //             if (distanceToBomb <= knockRadius) {
        //                 otherBomb.Knock(_horizontalDirection);
        //             }
        //         }
        //     }
        //
        //     // Move to the closest touch position and stop
        //     Vector3 separationDistance = _horizontalDirection * 0.05f;
        //     transform.position = closestTouchPosition - separationDistance;
        //     Stop();
        // }
        //
        // // After finding closest hit point
        // if (foundCollision) {
        //     float distanceToClosest = Vector3.Distance(transform.position, closestTouchPosition);
        //
        //     if (distanceToClosest <= skinWidth) {
        //         // Already within skin width, don't move at all
        //         Stop();
        //     } else {
        //         // Normal separation
        //         Vector3 separationDistance = _horizontalDirection * skinWidth;
        //         transform.position = closestTouchPosition - separationDistance;
        //         Stop();
        //     }
        // }
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
                    
                    Vector3 otherBombCenter = otherBomb.transform.position;
                    float otherBombRadius = otherBomb.GetComponent<SphereCollider>().radius;
                    
                    // Vector from line start to other bomb center
                    Vector3 toOtherBomb = otherBombCenter - lineStart;

                    // Project the vector onto the line direction
                    float projectionLength = Vector3.Dot(toOtherBomb, lineDirection);

                    // Distance from line to other bomb center
                    Vector3 closestPointOnLine = lineStart + lineDirection * projectionLength;
                    float distanceToLine = Vector3.Distance(otherBombCenter, closestPointOnLine);

                    // Combined radius of both bombs for touching
                    float combinedRadius = _sphereColliderRadius + otherBombRadius;

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
    
    private void HandleSurfaceMovement() {
        if (_isGrounded) {
            // Cast down to get the surface normal
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, _sphereColliderRadius + 0.1f, environmentLayerMask)) {
                Vector3 surfaceNormal = hit.normal;
                float slopeAngle = Vector3.Angle(Vector3.up, surfaceNormal);
            
                // Only project movement if slope is less than 45 degrees
                if (slopeAngle < 45f) {
                    // Project the movement direction onto the surface
                    Vector3 projectedDirection = Vector3.ProjectOnPlane(_horizontalDirection, surfaceNormal).normalized;
                    _horizontalDirection = projectedDirection;
                }
            }
        }
    }
    
    private void HandleRampTransitions() {
        if (_isGrounded && _isMovingHorizontally) {
            // Check if we're transitioning between surfaces
            Vector3 currentSurface = GetCurrentSurfaceNormal();
            Vector3 nextSurface = GetNextSurfaceNormal();
        
            if (currentSurface != nextSurface) {
                // Smoothly transition between surface normals
                Vector3 blendedNormal = Vector3.Slerp(currentSurface, nextSurface, 0.5f);
                Vector3 projectedDirection = Vector3.ProjectOnPlane(_horizontalDirection, blendedNormal).normalized;
                _horizontalDirection = projectedDirection;
            }
        }
    }

    private Vector3 GetCurrentSurfaceNormal() {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, _sphereColliderRadius + 0.1f, environmentLayerMask)) {
            return hit.normal;
        }
        return Vector3.up;
    }

    private Vector3 GetNextSurfaceNormal() {
        Vector3 nextPos = transform.position + _horizontalDirection * (moveSpeed * Time.fixedDeltaTime);
        if (Physics.Raycast(nextPos, Vector3.down, out RaycastHit hit, _sphereColliderRadius + 0.1f, environmentLayerMask)) {
            return hit.normal;
        }
        return Vector3.up;
    }
    
    private void HandleFloorCollisions() {
        float fallDistance = Mathf.Abs(_verticalVelocity.y * Time.fixedDeltaTime);
        float castDistance = fallDistance + 0.12f; // Small buffer
        float sphereCastRadius = _sphereColliderRadius - 0.1f; // Small buffer
        
        if (Physics.SphereCast(
                transform.position,
                sphereCastRadius,
                Vector3.down,
                out RaycastHit hit,
                castDistance,
                environmentLayerMask
            )) {
            
            Vector3 slopeNormal = hit.normal;
            Vector3 bombCenter = hit.point + slopeNormal * _sphereColliderRadius;
            float newY = bombCenter.y + 0.01f; // Small buffer
            // transform.position = bombCenter;
            
            // Hit the floor while falling
            // float newY = hit.point.y + _sphereColliderRadius + 0.01f; // Small buffer
            transform.position = new Vector3(
                transform.position.x,
                newY,
                transform.position.z
            );
            
            _verticalVelocity.y = 0;
            _isGrounded = true;
        } else {
            _isGrounded = false;
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
                environmentLayerMask
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
                _horizontalDirection.Normalize();
            }
        }
        
        // Check Z-axis movement
        if (Mathf.Abs(_horizontalDirection.z) > 0.01f) {
            Vector3 zDirection = new Vector3(0, 0, _horizontalDirection.z).normalized;
            if (!BombCanMove(zDirection, sphereCastDistance)) {
                _horizontalDirection.z = 0;
                _horizontalDirection.Normalize();
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
