using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEngine;
using MoreMountains.Feedbacks;

public class Bomb : NetworkBehaviour, IKnockable, IDamageable {
    private const string BombLayerName = "Bomb";
    private const string NonCollidableLayerName = "NonCollidable";
    private const int MaxCollisionIterations = 5;
    
    [SerializeField] private float moveSpeed;
    [SerializeField] private float gravity;
    [SerializeField] private LayerMask environmentLayerMask;
    [SerializeField] private LayerMask collisionLayerMask;
    [SerializeField] private float skinWidth;
    
    [SerializeField] private float bounceVelocity;
    [SerializeField] private float bounceThreshold = -0.2f;
    
    [SerializeField] private float testSphereColliderRadius = 0.5f;
    [SerializeField] private float testFloorColliderCastDistance = 0.1f;

    [SerializeField] private GameObject bombVisualGameObject;

    [SerializeField] private GameObject explosionGameObject;
    [SerializeField] private float detonationTimerMax = 5f;
    
    [SerializeField] private MMFeedbacks collisionFeedback;
    [SerializeField] private MMFeedbacks idleFeedback;
    
    private float _detonationTimer;
    
    private Vector3 _horizontalDirection;
    private Vector3 _verticalVelocity;
    private bool _isMovingHorizontally;
    private Collider _sphereCollider;
    private float _sphereColliderRadius;
    
    private Player _ignoredPlayer;
    private float _ignorePlayerTimer;
    
    private bool _isExploding;
    private bool _isGrounded;

    private Player _owner;

    private void Awake() {
        _sphereCollider = GetComponent<SphereCollider>();
        _sphereColliderRadius = GetComponent<SphereCollider>().radius;
    }

    private void ResetPooledObject() {
        _horizontalDirection = Vector3.zero;
        _verticalVelocity = Vector3.zero;
        _isMovingHorizontally = false;
        _detonationTimer = detonationTimerMax;
        _isExploding = false;
        
        _sphereCollider.enabled = true;
        
        bombVisualGameObject.SetActive(true);
        explosionGameObject.SetActive(false);
        
        gameObject.layer = LayerMask.NameToLayer(BombLayerName);
    }
    
    private void Start() {
        ResetPooledObject();
    }

    private void OnEnable() {
        ResetPooledObject();
        idleFeedback?.PlayFeedbacks();
    }

    private void OnDisable() {
        _owner?.DecreaseActiveBombCount();
        _owner = null;
        idleFeedback?.StopFeedbacks();
    }

    public override void OnStartNetwork() {
        // Set on server when the bomb is spawned.
        _owner?.IncreaseActiveBombCount();
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

        _isGrounded = IsGrounded(out RaycastHit groundHit);
    
        if (_isMovingHorizontally && _horizontalDirection != Vector3.zero) {
            // Project horizontal move if grounded
            if (_isGrounded) {
                _horizontalDirection = Vector3.ProjectOnPlane(_horizontalDirection, groundHit.normal).normalized;
            }
            
            Vector3 horizontalMove = _horizontalDirection * (moveSpeed * Time.fixedDeltaTime);

            totalMovement += horizontalMove;
        } else {
            _isMovingHorizontally = false;
        }
    
        if (!_isGrounded) {
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

    public static void SpawnBomb(NetworkObject networkObject, Transform bombSpawnTransform, Player player) {
        NetworkObjectManager.Instance.SpawnBomb(networkObject, bombSpawnTransform, player);
    }
    
    public void Damage() {
        if (_isExploding) {
            return;
        }
        
        Explode();
    }

    private void Explode() {
        _isExploding = true;
        _sphereCollider.enabled = false;
        gameObject.layer = LayerMask.NameToLayer(NonCollidableLayerName);

        ExplodeClientRpc();
    }

    [ObserversRpc(RunLocally = true)]
    private void ExplodeClientRpc() {
        bombVisualGameObject.SetActive(false);
        explosionGameObject.SetActive(true);
    }

    public void Knock(Vector3 direction) {
        _horizontalDirection += new Vector3(direction.x, 0, direction.z);
        _horizontalDirection.Normalize();
        _isMovingHorizontally = true;
    }

    private void Stop() {
        _horizontalDirection = Vector3.zero;
        _isMovingHorizontally = false;
    }

    private Vector3 HandleCollisions(Vector3 desiredMove) {
        Vector3 remainingMove = desiredMove;
        
        int iterationCount = 0;
        while (remainingMove.magnitude > 0f && iterationCount < MaxCollisionIterations) {
            iterationCount++;
            
            // if (iterationCount >= MaxCollisionIterations) {
            //     Debug.LogWarning("Max collision iterations reached. Remaining move might not be accurate.");
            // }

            Vector3 direction = remainingMove.normalized;
            float distance = remainingMove.magnitude;
            
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, _sphereColliderRadius, direction, distance, collisionLayerMask);

            // Filter out hits with the bomb's own collider
            hits = hits.Where(hit => hit.collider != _sphereCollider).ToArray();
            
            // Filter out the ignored player (player who kicks the bomb is ignored for a bit)
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
            AdjustPositionForSkinWidth(bombPositionAtHit, direction, closestHit.distance);
            
            // Might be more accurate to add skinWidth here?
            remainingMove -= moveToContact;
            
            ProcessCollisionHits(hitsOrderedByDistance, direction, ref remainingMove);
        }
        
        return remainingMove;
    }

    private Vector3 ProcessCollisionHits(RaycastHit[] hits, Vector3 direction, ref Vector3 remainingMove) {
        foreach (RaycastHit hit in hits) {
            if (((1 << hit.collider.gameObject.layer) & environmentLayerMask) != 0) {
                 // Object is part of the environment
                remainingMove = Vector3.ProjectOnPlane(remainingMove, hit.normal);

                // Split back into horizontal and vertical components
                _horizontalDirection = new Vector3(remainingMove.x, 0, remainingMove.z).normalized;
                _verticalVelocity = new Vector3(0, remainingMove.y, 0);
            } else if (hit.collider.TryGetComponent(out IKnockable knockable)) {
                // Handle collisions with knockable objects
                HandleKnockableCollision(hit, knockable, direction, ref remainingMove);
            }
        }
        return remainingMove;
    }

    private void HandleKnockableCollision(RaycastHit hit, IKnockable knockable, Vector3 direction, ref Vector3 remainingMove) {
        Vector3 hitPointRelativeToBomb = transform.InverseTransformPoint(hit.point);
        if (!_isGrounded && hitPointRelativeToBomb.y <= bounceThreshold) {
            _verticalVelocity = new Vector3(0, bounceVelocity, 0);
            Vector3 bounceDirection = new Vector3(remainingMove.x, bounceVelocity, remainingMove.z).normalized;
            remainingMove = bounceDirection * remainingMove.magnitude;
        } else {
            knockable.Knock(remainingMove.normalized);
            float distance = Vector3.Distance(transform.position, knockable.gameObject.transform.position);
            float thisRadius = _sphereColliderRadius;
            if (knockable.gameObject.TryGetComponent<Collider>(out Collider knockableCollider)) {
                if (knockableCollider is SphereCollider sphere) {
                    float knockableRadius = sphere.radius;
                    
                    float combinedRadii = thisRadius + knockableRadius;
                    float overlap = combinedRadii - distance;
                    if (overlap > 0) {
                        CorrectBombOverlap(overlap, direction, knockable);
                    }
                }
            }
            Stop();
            remainingMove = Vector3.zero;
            PlayCollisionFeedbackClientRpc();
        }
    }

    private void CorrectBombOverlap(float overlap, Vector3 direction, IKnockable knockable) {
        Vector3 otherBombCenter = knockable.gameObject.transform.position;
        // Calculate vector from other bomb to this bomb
        Vector3 separationVector = transform.position - otherBombCenter;
        
        // Project separation vector onto movement direction
        Vector3 directionNormalized = direction.normalized;
        float projectionLength = Vector3.Dot(separationVector, directionNormalized);
        
        float separationBuffer = 0.01f;
        // Calculate how much to move back along direction
        // We need to move back by at least the overlap amount
        // But we also need to account for the angle between separation and direction
        float moveBackDistance = (overlap + separationBuffer) / Mathf.Max(Mathf.Abs(projectionLength / separationVector.magnitude), 0.01f);
        
        // Move back along direction
        transform.position -= directionNormalized * moveBackDistance;
    }

    [ObserversRpc(RunLocally = true)]
    private void PlayCollisionFeedbackClientRpc() {
        collisionFeedback?.PlayFeedbacks();
    }
    
    private void AdjustPositionForSkinWidth(Vector3 bombPositionAtHit, Vector3 direction, float closestHitDistance) {
        if (closestHitDistance > skinWidth) {
            Vector3 skinWidthAdjustment = direction * skinWidth;
            Vector3 adjustedPosition = bombPositionAtHit - skinWidthAdjustment;
            transform.position = adjustedPosition;
        }
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
    
    public void SetOwner(Player player) {
        _owner = player;
    }
    
    public void SetIgnoredPlayer(Player player, float duration) {
        _ignoredPlayer = player;
        _ignorePlayerTimer = duration;
    }
}
