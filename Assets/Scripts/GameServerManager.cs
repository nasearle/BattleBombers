using System;
using FishNet;
using FishNet.Managing;
using Unity.Multiplayer.Playmode;
using UnityEngine;


public class GameServerManager : MonoBehaviour {
    private NetworkManager _networkManager;

    private void Awake() {
        _networkManager = InstanceFinder.NetworkManager;
    }

    private void Start() {
#if UNITY_EDITOR
        if (CurrentPlayer.ReadOnlyTags()[0] == "host") {
            StartServer();
            StartClient();
        } else if (CurrentPlayer.ReadOnlyTags()[0] == "server") {
            StartServer();
        } else if (CurrentPlayer.ReadOnlyTags()[0].Contains("client")) {
            StartClient();
        }
#endif
    }

    private void StartServer() {
        _networkManager.ServerManager.StartConnection();
    }

    private void StartClient() {
        _networkManager.ClientManager.StartConnection();
    }
}
