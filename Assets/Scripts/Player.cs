using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour {
    [SerializeField] private NetworkObject bombNetworkObject;
    [SerializeField] private float moveSpeed;
    [Tooltip("Degrees per second")]
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float gravity;

    private CharacterController _controller;
    private Vector3 _playerVelocity;
    private bool _isGrounded;
    
    private Vector3 _moveDirection;

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
            Quaternion targetRotation = Quaternion.LookRotation(_moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            // transform.rotation = targetRotation;
            _controller.Move(_moveDirection * (moveSpeed * Time.deltaTime));
        }

        // Apply Gravity
        _playerVelocity.y += gravity * Time.deltaTime;
        _controller.Move(_playerVelocity * Time.deltaTime);
    }

    private void OnMove(InputValue value) {
        Vector2 moveInput = value.Get<Vector2>();
        _moveDirection = new Vector3(moveInput.x, 0.0f, moveInput.y);
    }

    private void OnDrop(InputValue value) {
        Bomb.SpawnBomb(bombNetworkObject, transform.position);
    }
}