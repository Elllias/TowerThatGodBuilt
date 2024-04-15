using UnityEngine; 

public class RopeNode : MonoBehaviour{
    public bool canMove;
    public Vector3 velocity;
    public SphereCollider sphere;
    public int[] neighbourIndexes;
    
    private void Awake(){
        sphere = GetComponent<SphereCollider>();
    }
}