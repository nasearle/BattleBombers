using FishNet.Object;
using UnityEngine;

public class Bomb : NetworkBehaviour {
    private Vector3 _moveDirection;

    public static void SpawnBomb(NetworkObject networkObject, Vector3 position) {
        NetworkObjectManager.Instance.SpawnBomb(networkObject, position);
    }

    private void Knock(Vector3 direction) {
        
    }
}
