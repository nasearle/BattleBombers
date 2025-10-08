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
    private Collider _sphereCollider;
    private float _sphereColliderRadius;

    private void Awake() {
        _sphereCollider = GetComponent<SphereCollider>();
        _sphereColliderRadius = GetComponent<SphereCollider>().radius;
    }

    private void FixedUpdate() {
        if (!IsServerStarted) {
            return;
        }

        Vector3 totalMovement = Vector3.zero;
        
        RaycastHit groundHit;
        bool isGrounded = IsGrounded(out groundHit);
    
        if (_isMovingHorizontally && _horizontalDirection != Vector3.zero) {
            Vector3 horizontalMove = _horizontalDirection * (moveSpeed * Time.fixedDeltaTime);

            // Project horizontal move if grounded
            if (isGrounded) {
                horizontalMove = Vector3.ProjectOnPlane(horizontalMove, groundHit.normal);
            }

            totalMovement += horizontalMove;
        } else {
            _isMovingHorizontally = false;
        }
    
        if (!isGrounded) {
            _verticalVelocity.y -= gravity;
            totalMovement += _verticalVelocity * Time.fixedDeltaTime;
        } else {
            _verticalVelocity.y = 0;
        }
        
        totalMovement = HandleCollisions(totalMovement);
        
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

    private Vector3 HandleCollisions(Vector3 desiredMove) {
        Vector3 remainingMove = desiredMove;
        
        while (remainingMove.magnitude > 0f) {
            Vector3 direction = remainingMove.normalized;
            float distance = remainingMove.magnitude;
            float castRadius = _sphereColliderRadius;
            
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, castRadius, direction, distance, collisionLayerMask);

            // Filter out hits with the bomb's own collider
            hits = hits.Where(hit => hit.collider != _sphereCollider).ToArray();

            if (hits.Length == 0) {
                return remainingMove;
            }
            
            Debug.Log("hits.Length: " + hits.Length);

            RaycastHit[] hitsOrderedByDistance = hits.OrderBy(hit => hit.distance).ToArray();
            RaycastHit closestHit = hitsOrderedByDistance.First();

            Vector3 moveToContact = direction * closestHit.distance;
            Vector3 bombPositionAtHit = transform.position + moveToContact;
            
            // Only reposition if farther away than skin width. Otherwise, the bomb will be pushed away and this could cause
            // overlaps with other objects.
            Vector3 skinWidthAdjustment = Vector3.zero;
            if (closestHit.distance > skinWidth) {
                Debug.Log("closest hit within skin width");
                skinWidthAdjustment = direction * skinWidth;
                Vector3 adjustedPosition = bombPositionAtHit - skinWidthAdjustment;
                transform.position = adjustedPosition;
            }
            
            // Might be more accurate to add skinWidth here?
            remainingMove -= moveToContact;
            
            foreach (RaycastHit hit in hitsOrderedByDistance) {
                if (((1 << hit.collider.gameObject.layer) & environmentLayerMask) != 0) {
                    remainingMove = Vector3.ProjectOnPlane(remainingMove, hit.normal);

                    // Split back into horizontal and vertical components
                    _horizontalDirection = new Vector3(remainingMove.x, 0, remainingMove.z).normalized;
                    _verticalVelocity = new Vector3(0, remainingMove.y, 0);
                } 
                
                if (hit.collider.TryGetComponent(out IKnockable knockable)) {
                    knockable.Knock(remainingMove.normalized);
                    Stop();
                    remainingMove = Vector3.zero;
                }
            }
        }
        
        return remainingMove;
    }

    private bool IsGrounded(out RaycastHit hit) {
        float castDistance = skinWidth + 0.05f;
        if (Physics.SphereCast(
                transform.position,
                _sphereColliderRadius,
                Vector3.down,
                out hit,
                castDistance,
                environmentLayerMask
            )) {
            return true;
        }

        hit = default;
        return false;
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
