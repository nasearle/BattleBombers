using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class Bomb : NetworkBehaviour, IKnockable {
    [SerializeField] private float moveSpeed;
    
    private Vector3 _moveDirection;
    private bool _isMoving;
    private int _instanceID;
    private HashSet<int> _processedCollisionsThisFrame = new HashSet<int>();

    private void Start() {
        _instanceID = GetInstanceID();
    }

    private void FixedUpdate() {
        _processedCollisionsThisFrame.Clear();
        
        if (_isMoving && _moveDirection != Vector3.zero) {
            transform.position += _moveDirection * (moveSpeed * Time.fixedDeltaTime);
        } else {
            _isMoving = false;
        }
    }

    public static void SpawnBomb(NetworkObject networkObject, Vector3 position) {
        NetworkObjectManager.Instance.SpawnBomb(networkObject, position);
    }

    public void Knock(Vector3 direction) {
        _moveDirection = direction;
        _isMoving = true;
    }

    public void Stop() {
        _moveDirection = Vector3.zero;
        _isMoving = false;
    }

    private void OnTriggerStay(Collider other) {
        if (other.gameObject.TryGetComponent<Bomb>(out Bomb otherBomb)) {
            BombCollisionManager.Instance.RegisterCollision(this, otherBomb);
        }
    }

    public bool IsMoving() {
        return _isMoving;
    }

    public Vector3 GetMoveDirection() {
        return _moveDirection;
    }
}
