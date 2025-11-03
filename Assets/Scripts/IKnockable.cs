using UnityEngine;

public interface IKnockable {
    GameObject gameObject { get ; } 
    
    public void Knock(Vector3 direction);
}
