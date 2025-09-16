using FishNet.Object;
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
    
    private Vector3 _moveDirection;
    private Vector3 _lastMoveDirection;

    private Vector3 _playerBottomVerticalOffset = new Vector3(0, 0.5f, 0);

    void Start() {
        _controller = GetComponent<CharacterController>();
    }

    void Update() {
        _isGrounded = _controller.isGrounded;

        // Reset vertical velocity if grounded and not falling
        if (_isGrounded && _playerVelocity.y < 0) {
            _playerVelocity.y = -0.5f;
        }

        if (_moveDirection != Vector3.zero) {
            _lastMoveDirection = _moveDirection;
            Quaternion targetRotation = Quaternion.LookRotation(_moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            // transform.rotation = targetRotation;
            _controller.Move(_moveDirection * (moveSpeed * Time.deltaTime));
        }

        // Apply Gravity
        _playerVelocity.y += gravity * Time.deltaTime;
        _controller.Move(_playerVelocity * Time.deltaTime);
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
        Vector2 moveInput = value.Get<Vector2>();
        _moveDirection = new Vector3(moveInput.x, 0.0f, moveInput.y);
    }

    private void OnDrop(InputValue value) {
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

    // private void OnTriggerEnter(Collider other) {
    //     if (other.TryGetComponent(out IKnockable knockableObject)) {
    //         knockableObject.Knock(_lastMoveDirection);
    //     }
    // }
}