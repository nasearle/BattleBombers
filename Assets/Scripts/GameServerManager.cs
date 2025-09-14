using System;
using FishNet;
using FishNet.Managing;
using UnityEngine;

public class GameServerManager : MonoBehaviour {
    private NetworkManager _networkManager;

    private void Awake() {
        _networkManager = InstanceFinder.NetworkManager;
    }

    private void Start() {
        StartServer();
        StartClient();
    }

    private void StartServer() {
        _networkManager.ServerManager.StartConnection();
    }

    private void StartClient() {
        _networkManager.ClientManager.StartConnection();
    }
}
