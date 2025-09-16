using System;
using FishNet.Object;
using UnityEngine;

public class Bomb : NetworkBehaviour, IKnockable {
    [SerializeField] private float moveSpeed;
    
    private Vector3 _moveDirection;

    private void Update() {
        if (_moveDirection != Vector3.zero) {
            transform.position += _moveDirection * (moveSpeed * Time.deltaTime);
        }
    }

    public static void SpawnBomb(NetworkObject networkObject, Vector3 position) {
        NetworkObjectManager.Instance.SpawnBomb(networkObject, position);
    }

    public void Knock(Vector3 direction) {
        _moveDirection = direction;
    }
}
