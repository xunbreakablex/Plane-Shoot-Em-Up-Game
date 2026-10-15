using UnityEngine;

// Pervane objesine ekle; kendi ekseni etrafında (Z) döndürür.
public class Propeller : MonoBehaviour
{
    [SerializeField] private float rpm = 1200f;
    [SerializeField] private Vector3 localAxis = Vector3.forward;

    public float Rpm
    {
        get => rpm;
        set => rpm = value;
    }

    private void Update()
    {
        transform.Rotate(localAxis, rpm * 6f * Time.deltaTime, Space.Self);
    }
}
