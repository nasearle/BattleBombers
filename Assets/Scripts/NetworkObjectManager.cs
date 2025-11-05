using UnityEngine;
using FishNet.Object;

public class NetworkObjectManager : NetworkBehaviour {
     public static NetworkObjectManager Instance { get; private set; }
     
     private void Awake() {
          Instance = this;
     }
     
     public void SpawnBomb(NetworkObject networkObject, Transform bombSpawnTransform, Player player) {
          if (IsServerStarted) {
               SpawnBombLocally(networkObject, bombSpawnTransform, player);
          } else {
               SpawnBombServerRpc(networkObject, bombSpawnTransform, player);
          }
          
     }

     private void SpawnBombLocally(NetworkObject networkObject, Transform bombSpawnTransform, Player player) {
          NetworkObject bomb = NetworkManager.GetPooledInstantiated(networkObject, true);
          bomb.transform.position = bombSpawnTransform.position;
          Bomb bombScript = bomb.GetComponent<Bomb>();
          bombScript.SetOwner(player);
          ServerManager.Spawn(bomb);
     }

     [ServerRpc(RequireOwnership = false)]
     private void SpawnBombServerRpc(NetworkObject networkObject, Transform playerTransform, Player player) {
          SpawnBombLocally(networkObject, playerTransform, player);
     }
}
