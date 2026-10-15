using UnityEngine;

// Efekt prefab'larini belirli sure sonra temizler.
public class AutoDestroy : MonoBehaviour
{
    public float lifetime = 8f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
