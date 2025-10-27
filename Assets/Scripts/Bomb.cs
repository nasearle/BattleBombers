using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEngine;

public class Bomb : NetworkBehaviour, IKnockable, IDamageable {
    private const string BOMB_LAYER_NAME = "Bomb";
    private const string NON_COLLIDABLE_LAYER_NAME = "NonCollidable";
    
    [SerializeField] private float moveSpeed;
    [SerializeField] private float gravity;
    [SerializeField] private LayerMask environmentLayerMask;
    [SerializeField] private LayerMask collisionLayerMask;
    [SerializeField] private float skinWidth;
    
    [SerializeField] private float testSphereColliderRadius = 0.5f;
    [SerializeField] private float testFloorColliderCastDistance = 0.1f;

    [SerializeField] private GameObject bombVisualGameObject;

    [SerializeField] private GameObject explosionGameObject;
    [SerializeField] private float detonationTimerMax = 5f;
    
    private float _detonationTimer;
    
    private Vector3 _horizontalDirection;
    private Vector3 _verticalVelocity;
    private bool _isMovingHorizontally;
    private Collider _sphereCollider;
    private float _sphereColliderRadius;
    
    private Player _ignoredPlayer;
    private float _ignorePlayerTimer;
    
    private bool _isExploding;

    private void Awake() {
        _sphereCollider = GetComponent<SphereCollider>();
        _sphereColliderRadius = GetComponent<SphereCollider>().radius;
    }

    private void ResetPooledObject() {
        if (!IsServerStarted) {
            return;
        }
        
        _horizontalDirection = Vector3.zero;
        _verticalVelocity = Vector3.zero;
        _isMovingHorizontally = false;
        _detonationTimer = detonationTimerMax;
        _isExploding = false;
        
        _sphereCollider.enabled = true;
        
        bombVisualGameObject.SetActive(true);
        explosionGameObject.SetActive(false);
        
        gameObject.layer = LayerMask.NameToLayer(BOMB_LAYER_NAME);
    }
    
    private void Start() {
        ResetPooledObject();
    }

    private void OnEnable() {
        ResetPooledObject();
    }

    private void Update() {
        if (!IsServerStarted) {
            return;
        }
        
        _detonationTimer -= Time.deltaTime;

        if (_detonationTimer <= 0f && !_isExploding) {
            Explode();
        }
    }

    private void FixedUpdate() {
        if (!IsServerStarted) {
            return;
        }

        if (_isExploding) {
            return;
        }
        
        if (_ignorePlayerTimer > 0f) {
            _ignorePlayerTimer -= Time.fixedDeltaTime;
            if (_ignorePlayerTimer <= 0f) {
                _ignoredPlayer = null;
            }
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

    public static void SpawnBomb(NetworkObject networkObject, Transform bombSpawnTransform) {
        NetworkObjectManager.Instance.SpawnBomb(networkObject, bombSpawnTransform);
    }
    
    public void Damage() {
        if (_isExploding) {
            return;
        }
        
        Explode();
    }

    private void Explode() {
        _isExploding = true;
        
        bombVisualGameObject.SetActive(false);
        _sphereCollider.enabled = false;
        
        gameObject.layer = LayerMask.NameToLayer(NON_COLLIDABLE_LAYER_NAME);
        
        explosionGameObject.SetActive(true);
    }

    public void Knock(Vector3 direction) {
        _horizontalDirection += direction;
        _horizontalDirection.Normalize();
        _isMovingHorizontally = true;
    }

    private void Stop() {
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
            
            if (_ignoredPlayer != null) {
                Collider ignoredCollider = _ignoredPlayer.GetCharacterController();
                hits = hits.Where(hit => hit.collider != ignoredCollider).ToArray();
            }

            if (hits.Length == 0) {
                return remainingMove;
            }

            RaycastHit[] hitsOrderedByDistance = hits.OrderBy(hit => hit.distance).ToArray();
            RaycastHit closestHit = hitsOrderedByDistance.First();

            Vector3 moveToContact = direction * closestHit.distance;
            Vector3 bombPositionAtHit = transform.position + moveToContact;
            
            // Move the bomb a skin's width away from colliding object. Don't move the bomb if it was already within a
            // skin's width from the object before it moved, otherwise the bomb will be pushed away and this could
            // cause overlaps with other objects.
            Vector3 skinWidthAdjustment = Vector3.zero;
            if (closestHit.distance > skinWidth) {
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

    public bool IsMoving() {
        return _isMovingHorizontally;
    }
    
    public void SetIgnoredPlayer(Player player, float duration) {
        _ignoredPlayer = player;
        _ignorePlayerTimer = duration;
    }
}
