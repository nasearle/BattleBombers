using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour, IKnockable {
    [SerializeField] private NetworkObject bombNetworkObject;
    [SerializeField] private LayerMask bombLayerMask;
    [SerializeField] private Transform dropBombSpawnTransform;
    [SerializeField] private float playerCollisionRadius;
    
    [SerializeField] private float moveSpeed;
    [Tooltip("Degrees per second")]
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float gravity;
    
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float knockbackUpwardForce = 5f;
    [SerializeField] private float knockbackDuration = 0.5f;
    [SerializeField] private float bombIgnoreCollisionDuration = 0.3f;
    
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Material redMaterial;
    
    private Material _defaultMaterial;

    private CharacterController _controller;
    private Collider _playerCollider;

    private float _playerVerticalDisplacement;
    
    private bool _isGrounded;
    
    private Vector2 _moveDirection;
    private Vector3 _lastMoveDirection;
    
    private Vector3 _knockbackVelocity;
    private float _knockbackTimer;

    public enum State {
        Idle,
        Moving,
        Dropping,
        Kicking,
        Stunned,
        Recovering
    }
    
    private State _state;
    
    public struct ReplicateData : IReplicateData {
        public Vector2 InputVector;
        
        public ReplicateData(Vector2 inputVector) : this() {
            InputVector = inputVector;
        }
        
        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct ReconcileData : IReconcileData {
        public Vector3 Position;
        public Quaternion Rotation;
        public float PlayerVerticalDisplacement;
        public Vector3 KnockbackVelocity;
        public float KnockbackTimer;
        public State State;
        
        public ReconcileData(Vector3 position, Quaternion rotation, float playerVerticalDisplacement,
            Vector3 knockbackVelocity, float knockbackTimer, State state) : this() {
            Position = position;
            Rotation = rotation;
            PlayerVerticalDisplacement = playerVerticalDisplacement;
            KnockbackVelocity = knockbackVelocity;
            KnockbackTimer = knockbackTimer;
            State = state;
        }
        
        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
    
    public override void OnStartNetwork() {
        _controller = GetComponent<CharacterController>();
        _playerCollider = _controller;
        
        if (bodyRenderer != null) {
            _defaultMaterial = bodyRenderer.material;
        }
        
        TimeManager.OnTick += TimeManager_OnTick;
        
        NetworkManager.CacheObjects(bombNetworkObject, 10, true);
    }

    public override void OnStopNetwork() {
        base.OnStopNetwork();
        TimeManager.OnTick -= TimeManager_OnTick;
    }
    
    private void TimeManager_OnTick() {
        HandleMovementReplicate(CreateReplicateData());
        CreateReconcile();
    }
    
    private ReplicateData CreateReplicateData() {
        if (!IsOwner) {
            return default;
        }
        
        return new ReplicateData(_moveDirection);
    }

    [Replicate]
    private void HandleMovementReplicate(ReplicateData data, ReplicateState state = ReplicateState.Invalid,
        Channel channel = Channel.Unreliable) {

        Vector3 moveDir = new Vector3(data.InputVector.x, 0, data.InputVector.y);
        
        _isGrounded = _controller.isGrounded;
        
        // Apply Gravity
        _playerVerticalDisplacement -= gravity * (float)TimeManager.TickDelta;

        // Reset vertical displacement if grounded and not falling. A small negative number keeps the player touching
        // the floor so the isGrounded check is consistent.
        if (_isGrounded &&  _playerVerticalDisplacement < 0f) {
            _playerVerticalDisplacement = -0.1f;
        }

        Vector3 gravitationalMovement = new Vector3(0, _playerVerticalDisplacement, 0);

        Vector3 horizontalMovement = new Vector3();
        // Handle knockback
        if (_knockbackTimer > 0f) {
            _knockbackTimer -= (float)TimeManager.TickDelta;
            horizontalMovement = _knockbackVelocity * (float)TimeManager.TickDelta;
            
            // Decay knockback velocity over time
            _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, (float)TimeManager.TickDelta / knockbackDuration);
            
            if (_knockbackTimer <= 0f) {
                _state = State.Idle;
                _knockbackVelocity = Vector3.zero;
                UpdatePlayerMaterial();
            }
        } else if (moveDir != Vector3.zero) {
            _lastMoveDirection = moveDir;
            transform.forward = Vector3.Slerp(transform.forward, moveDir, rotationSpeed * (float)TimeManager.TickDelta);
            horizontalMovement = moveDir * (moveSpeed * (float)TimeManager.TickDelta);
        }
        
        _controller.Move(horizontalMovement + gravitationalMovement);
    }
    
    public override void CreateReconcile() {
        transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
        ReconcileData data = new ReconcileData(position, rotation, _playerVerticalDisplacement, 
            _knockbackVelocity, _knockbackTimer, _state);
        ReconcileState(data);
    }
    
    [Reconcile]
    private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable) {
        transform.SetPositionAndRotation(data.Position, data.Rotation);
        _playerVerticalDisplacement = data.PlayerVerticalDisplacement;
        _knockbackVelocity = data.KnockbackVelocity;
        _knockbackTimer = data.KnockbackTimer;
        _state = data.State;
    }

    private Bomb GetBombTouchingPlayer() {
        Collider[] hitColliders = Physics.OverlapSphere(dropBombSpawnTransform.position, playerCollisionRadius, bombLayerMask);
        foreach (Collider hitCollider in hitColliders) {
            if (hitCollider.TryGetComponent(out Bomb bomb)) {
                return bomb;
            }
        }

        return null;
    }

    private void OnMove(InputValue value) {
        if (!IsOwner) {
            return;
        }
        
        _moveDirection = value.Get<Vector2>();
    }

    private void OnDrop(InputValue value) {
        if (!IsOwner) {
            return;
        }

        OnDropServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void OnDropServerRpc() {
        Bomb bomb = GetBombTouchingPlayer();
        if (bomb == null) {
            Bomb.SpawnBomb(bombNetworkObject, dropBombSpawnTransform);
        } else {
            if (!bomb.IsMoving()) {
                bomb.SetIgnoredPlayer(this, bombIgnoreCollisionDuration);
                bomb.Knock(_lastMoveDirection);
            }
        }
    }

    // private void OnKick(InputValue value) {
    //     IKnockable knockableObject = GetBombTouchingPlayer();
    //     if (knockableObject != null) {
    //         knockableObject.Knock(_lastMoveDirection);
    //     }
    // }
    
    private void OnDrawGizmosSelected() {
        // Set the color of the gizmo
        Gizmos.color = Color.red; 

        // Draw a wireframe sphere using the same center and radius
        Gizmos.DrawWireSphere(dropBombSpawnTransform.position, playerCollisionRadius);
    }

    public void Knock(Vector3 direction) {
        KnockClientRpc(direction);
    }
    
    [ObserversRpc(RunLocally = true)]
    private void KnockClientRpc(Vector3 direction) {
        if (_state != State.Stunned) {
            _state = State.Stunned;
            _knockbackVelocity = direction.normalized * knockbackForce;
            _knockbackTimer = knockbackDuration;
            _playerVerticalDisplacement = knockbackUpwardForce;
            UpdatePlayerMaterial();
        }
    }
    
    private void UpdatePlayerMaterial() {
        if (bodyRenderer == null) return;
        
        switch (_state) {
            case State.Stunned:
                bodyRenderer.material = redMaterial;
                break;
            case State.Idle:
            default:
                bodyRenderer.material = _defaultMaterial;
                break;
        }
    }
    
    public CharacterController GetCharacterController() {
        return _controller;
    }
}