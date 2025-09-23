using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour {
    [SerializeField] private NetworkObject bombNetworkObject;
    [SerializeField] private LayerMask bombLayerMask;
    [SerializeField] private float playerCollisionRadius;
    
    [SerializeField] private float moveSpeed;
    [Tooltip("Degrees per second")]
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float gravity;

    private CharacterController _controller;
    private Vector3 _playerVelocity;
    private bool _isGrounded;
    
    private Vector2 _moveDirection;
    private Vector3 _lastMoveDirection;

    private Vector3 _playerBottomVerticalOffset = new Vector3(0, 0.5f, 0);
    
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
        
        public ReconcileData(Vector3 position, Quaternion rotation) : this() {
            Position = position;
            Rotation = rotation;
        }
        
        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
    
    public override void OnStartNetwork() {
        base.OnStartNetwork();
        _controller = GetComponent<CharacterController>();
        TimeManager.OnTick += TimeManager_OnTick;
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

        // Reset vertical velocity if grounded and not falling
        if (_isGrounded && _playerVelocity.y < 0) {
            _playerVelocity.y = -0.5f;
        }

        if (moveDir != Vector3.zero) {
            _lastMoveDirection = moveDir;
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * (float)TimeManager.TickDelta);
            // transform.rotation = targetRotation;
            _controller.Move(moveDir * (moveSpeed * (float)TimeManager.TickDelta));
        }

        // Apply Gravity
        _playerVelocity.y += gravity * (float)TimeManager.TickDelta;
        _controller.Move(_playerVelocity * (float)TimeManager.TickDelta);
    }
    
    public override void CreateReconcile() {
        if (TimeManager.LocalTick % 3 == 0) {
            transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
            ReconcileData data = new ReconcileData(position, rotation);
            ReconcileState(data);
        }
    }
    
    [Reconcile]
    private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable) {
        transform.SetPositionAndRotation(data.Position, data.Rotation);
    }

    private IKnockable GetBombTouchingPlayer() {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position - _playerBottomVerticalOffset, playerCollisionRadius, bombLayerMask);
        foreach (Collider hitCollider in hitColliders) {
            if (hitCollider.TryGetComponent(out IKnockable knockableObject)) {
                return knockableObject;
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
        IKnockable knockableObject = GetBombTouchingPlayer();
        if (knockableObject == null) {
            Bomb.SpawnBomb(bombNetworkObject, transform.position - _playerBottomVerticalOffset);
        } else {
            knockableObject.Knock(_lastMoveDirection);
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
        Gizmos.DrawWireSphere(transform.position - _playerBottomVerticalOffset, playerCollisionRadius);
    }

    private void OnTriggerEnter(Collider other) {
        if (!IsOwner) {
            return;
        }
        
        if (other.TryGetComponent(out Bomb bomb)) {
            if (bomb.IsMoving()) {
                // knock player back and stun
            }
        }
    }
}