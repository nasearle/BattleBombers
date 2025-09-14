using UnityEngine;
using FishNet.Object;

public class NetworkObjectManager : NetworkBehaviour {
     public static NetworkObjectManager Instance { get; private set; }
     
     private void Awake() {
          Instance = this;
     }
     
     public void SpawnBomb(NetworkObject networkObject, Vector3 position) {
          if (IsServerStarted) {
               SpawnBombLocally(networkObject, position);
          } else {
               SpawnBombServerRpc(networkObject, position);
          }
          
     }

     private void SpawnBombLocally(NetworkObject networkObject, Vector3 position) {
          NetworkObject bomb = Instantiate(networkObject, position, Quaternion.identity);
          ServerManager.Spawn(bomb);
     }

     [ServerRpc(RequireOwnership = false)]
     private void SpawnBombServerRpc(NetworkObject networkObject, Vector3 position) {
          SpawnBombLocally(networkObject, position);
     }
}
