using System;
using UnityEngine;

public class PlayerAttributes {
    public event EventHandler OnHealthChanged;
    
    public int MaxHealth { get; private set; }

    private int _currentHealth;
    public int CurrentHealth {
        get => _currentHealth;
        private set {
            _currentHealth = value;
            OnHealthChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int BombPower { get; private set; }
    public int MaxBombs { get; private set; }

    public PlayerAttributes(int maxHealth, int startingHealth, int bombPower, int maxBombs) {
        MaxHealth = maxHealth;
        CurrentHealth = startingHealth;
        BombPower = bombPower;
        MaxBombs = maxBombs;
    }

    // Public methods to control updates
    public void IncreaseHealth(int amount) {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount); // Enforce MaxHealth limit
    }

    public void DecreaseHealth(int amount) {
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount); // Enforce minimum of 0
    }

    public void IncreaseBombPower(int amount) {
        BombPower += amount;
    }

    public void IncreaseMaxBombs(int amount) {
        MaxBombs += amount;
    }

    // Example of applying a power-up
    public void ApplyPowerUp(int extraHealth, int extraBombPower, int extraMaxBombs) {
        IncreaseHealth(extraHealth);
        IncreaseBombPower(extraBombPower);
        IncreaseMaxBombs(extraMaxBombs);
    }


}
