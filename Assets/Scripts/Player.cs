using System;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour, IKnockable, IDamageable {
    [SerializeField] private NetworkObject bombNetworkObject;
    [SerializeField] private LayerMask bombLayerMask;
    [SerializeField] private Transform dropBombSpawnTransform;
    [SerializeField] private float playerCollisionRadius;
    
    [SerializeField] private float moveSpeed;
    [Tooltip("Degrees per second")]
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float gravity;

    [SerializeField] private float stunDuration;
    [SerializeField] private float recoveryDuration;
    
    [SerializeField] private float knockbackForce;
    [SerializeField] private float knockbackUpwardForce;
    [SerializeField] private float bombIgnoreCollisionDuration;
    
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Material redMaterial;
    [SerializeField] private Material yellowMaterial;
    
    private Material _defaultMaterial;

    private CharacterController _controller;

    private float _playerVerticalDisplacement;
    
    private bool _isGrounded;
    
    private Vector2 _moveDirection;
    private Vector3 _lastMoveDirection;
    
    private Vector3 _knockbackVelocity;

    private float _stunTimer;
    private float _recoveryTimer;
    
    private PlayerAttributes _playerAttributes;
    private int _activeBombCount;

    private enum State {
        Idle,
        Moving,
        Dropping,
        Kicking,
        Stunned,
        Recovering
    }
    
    private State _state;
    private State _previousState;
    
    private State CurrentState {
        get => _state;
        set {
            if (_state != value) {
                _previousState = _state;
                _state = value;
                OnStateChanged(_previousState, _state);
            }
        }
    }

    private void OnStateChanged(State oldState, State newState) {
        // Initialize timers when entering states
        if (newState == State.Recovering) {
            _recoveryTimer = recoveryDuration;
        } else if (newState == State.Stunned) {
            _stunTimer = stunDuration;
        }
        
        UpdatePlayerMaterial();
    }

    private void Start() {
        _playerAttributes = new PlayerAttributes(3, 3, 1, 20);
        _activeBombCount = 0;
    }

    private struct ReplicateData : IReplicateData {
        public readonly Vector2 InputVector;
        public readonly uint Tick;
        
        public ReplicateData(Vector2 inputVector, uint tick) : this() {
            InputVector = inputVector;
            Tick = tick;
        }
        
        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    private struct ReconcileData : IReconcileData {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float PlayerVerticalDisplacement;
        public readonly Vector3 KnockbackVelocity;
        public readonly float KnockbackTimer;
        public readonly float StunTimer;
        public readonly float RecoveryTimer;
        public readonly State State;
        
        public ReconcileData(Vector3 position, Quaternion rotation, float playerVerticalDisplacement,
            Vector3 knockbackVelocity, float stunTimer, float recoveryTimer, State state) : this() {
            Position = position;
            Rotation = rotation;
            PlayerVerticalDisplacement = playerVerticalDisplacement;
            KnockbackVelocity = knockbackVelocity;
            StunTimer = stunTimer;
            RecoveryTimer = recoveryTimer;
            State = state;
        }
        
        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
    
    public override void OnStartNetwork() {
        _controller = GetComponent<CharacterController>();
        
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
        HandleReplicate(CreateReplicateData());
        CreateReconcile();
    }
    
    private void HandleState() {
        switch (_state) {
            case State.Idle:
                break;
            case State.Stunned:
                _stunTimer -= (float)TimeManager.TickDelta;
                if (_stunTimer <= 0f) {
                    CurrentState = State.Recovering;
                }
                break;
            case State.Recovering:
                _recoveryTimer -= (float)TimeManager.TickDelta;
                if (_recoveryTimer <= 0f) {
                    CurrentState = State.Idle;
                }
                break;
        }
    }
    
    private void UpdatePlayerMaterial() {
        if (bodyRenderer == null) return;
        
        switch (_state) {
            case State.Stunned:
                bodyRenderer.material = redMaterial;
                break;
            case State.Recovering:
                bodyRenderer.material = yellowMaterial;
                break;
            case State.Idle:
            default:
                bodyRenderer.material = _defaultMaterial;
                break;
        }
    }
    
    private ReplicateData CreateReplicateData() {
        if (!IsOwner) {
            return default;
        }
        
        return new ReplicateData(_moveDirection, TimeManager.LocalTick);
    }

    [Replicate]
    private void HandleReplicate(ReplicateData data, ReplicateState state = ReplicateState.Invalid,
        Channel channel = Channel.Unreliable) {
        if (!state.ContainsReplayed()) {
            HandleState();
        }

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

        if (_state == State.Stunned) {
            horizontalMovement = _knockbackVelocity * (float)TimeManager.TickDelta;
            
            // Decay knockback velocity over time
            _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, (float)TimeManager.TickDelta / stunDuration);
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
            _knockbackVelocity, _stunTimer, _recoveryTimer, _state);
        ReconcileState(data);
    }
    
    [Reconcile]
    private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable) {
        transform.SetPositionAndRotation(data.Position, data.Rotation);
        _playerVerticalDisplacement = data.PlayerVerticalDisplacement;
        _knockbackVelocity = data.KnockbackVelocity;
        _stunTimer = data.StunTimer;
        _recoveryTimer = data.RecoveryTimer;
        
        CurrentState = data.State;
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
    
    private bool CanSpawnBomb() {
        return _activeBombCount < _playerAttributes.MaxBombs;
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
            if (!CanSpawnBomb()) {
                return;
            }
            
            Bomb.SpawnBomb(bombNetworkObject, dropBombSpawnTransform, this);
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
        if (_state == State.Recovering) {
            return;
        }
        
        KnockClientRpc(direction);
    }

    public void Damage() {
        if (_state == State.Recovering) {
            return;
        }
        
        _playerAttributes.DecreaseHealth(1);

        CurrentState = State.Recovering;
    }

    public void IncreaseActiveBombCount() {
        _activeBombCount++;
    }
    
    public void DecreaseActiveBombCount() {
        _activeBombCount = Mathf.Max(0, _activeBombCount - 1);
    }
    
    [ObserversRpc(RunLocally = true)]
    private void KnockClientRpc(Vector3 direction) {
        CurrentState = State.Stunned;
        _knockbackVelocity = direction.normalized * knockbackForce;
        _playerVerticalDisplacement = knockbackUpwardForce;
    }
    
    public CharacterController GetCharacterController() {
        return _controller;
    }
}