using System.Collections.Generic;
using UnityEngine;

public class BombCollisionManager : MonoBehaviour {
    public static BombCollisionManager Instance { get; private set; }
    
    [SerializeField] private LayerMask environmentLayerMask;

    private struct CollisionPair {
        public Bomb bombA;
        public Bomb bombB;
        
        public CollisionPair(Bomb a, Bomb b) {
            bombA = a;
            bombB = b;
        }
    }

    private List<CollisionPair> _currentFrameCollisions = new List<CollisionPair>();
    private HashSet<string> _processedPairsThisFrame = new HashSet<string>();

    private void Awake() {
        Instance = this;
    }

    public void RegisterCollision(Bomb bombA, Bomb bombB) {
        // Create ordered pair to avoid duplicates
        string pairKey = CreatePairKey(bombA, bombB);
        
        if (!_processedPairsThisFrame.Contains(pairKey)) {
            _currentFrameCollisions.Add(new CollisionPair(bombA, bombB));
            _processedPairsThisFrame.Add(pairKey);
        }
    }

    private string CreatePairKey(Bomb bombA, Bomb bombB) {
        // Ensure consistent ordering to avoid duplicates
        int idA = bombA.GetInstanceID();
        int idB = bombB.GetInstanceID();

        if (idA < idB) {
            return $"{idA}_{idB}";
        } else {
            return $"{idB}_{idA}";
        }
    }

    private void FixedUpdate() {
        ProcessAllCollisions();
        ClearFrameData();
    }

    private void ProcessAllCollisions() {
        if (_currentFrameCollisions.Count == 0)
            return;

        ProcessMovementTransfers();
        // ApplySeparation();
    }

    private void ProcessMovementTransfers() {
        foreach (var pair in _currentFrameCollisions) {
            ProcessCollisionPair(pair);
        }
    }

    private void ProcessCollisionPair(CollisionPair pair) {
        bool aMoving = pair.bombA.IsMoving();
        bool bMoving = pair.bombB.IsMoving();
        
        if (aMoving && bMoving) {
            // Both moving - both stop
            pair.bombA.Stop();
            pair.bombB.Stop();
        } else if (aMoving) {
            // A moving, B stationary - transfer to B
            pair.bombB.Knock(pair.bombA.GetMoveDirection());
            pair.bombA.Stop();
        } else if (bMoving) {
            // B moving, A stationary - transfer to A
            pair.bombA.Knock(pair.bombB.GetMoveDirection());
            pair.bombB.Stop();
        }
        // If neither moving, do nothing (separation will handle it)
    }
    
    private void ApplySeparation() {
        foreach (var pair in _currentFrameCollisions) {
            // Only separate if both bombs are stationary
            if (!pair.bombA.IsMoving() && !pair.bombB.IsMoving()) {
                ApplySeparationForce(pair.bombA, pair.bombB);
            }
        }
    }

    private void ApplySeparationForce(Bomb bombA, Bomb bombB) {
        Vector3 direction = (bombA.transform.position - bombB.transform.position).normalized;
        float separationDistance = 0.1f;

        if (bombA.BombCanMove(direction, separationDistance)) {
            bombA.transform.position += direction * separationDistance;
        }

        if (bombB.BombCanMove(-direction, separationDistance)) {
            bombB.transform.position -= direction * separationDistance;
        }
    }

    private void ClearFrameData() {
        _currentFrameCollisions.Clear();
        _processedPairsThisFrame.Clear();
    }
}