using UnityEngine;
using FishNet.Object;

public class NetworkObjectManager : NetworkBehaviour {
     public static NetworkObjectManager Instance { get; private set; }
     
     private void Awake() {
          Instance = this;
     }
     
     public void SpawnBomb(NetworkObject networkObject, Transform bombSpawnTransform) {
          if (IsServerStarted) {
               SpawnBombLocally(networkObject, bombSpawnTransform);
          } else {
               SpawnBombServerRpc(networkObject, bombSpawnTransform);
          }
          
     }

     private void SpawnBombLocally(NetworkObject networkObject, Transform bombSpawnTransform) {
          // NetworkObject bomb = Instantiate(networkObject, position, Quaternion.identity);
          NetworkObject bomb = NetworkManager.GetPooledInstantiated(networkObject, true);
          bomb.transform.position = bombSpawnTransform.position;
          ServerManager.Spawn(bomb);
     }

     [ServerRpc(RequireOwnership = false)]
     private void SpawnBombServerRpc(NetworkObject networkObject, Transform playerTransform) {
          SpawnBombLocally(networkObject, playerTransform);
     }
}
