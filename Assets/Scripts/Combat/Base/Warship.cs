using UnityEngine;

// Deniz gecisindeki savas gemisi: duz bir rotada yavasca yol alir. Gemi yok olunca batar (alcalir ve yatar).
// Govde, top kulesi ve uçaksavarlar BaseBuilder.BuildShip'te kurulur.
public class Warship : MonoBehaviour
{
    private Vector3 start;
    private Vector3 dir;
    private float speed;
    private float born;
    private EnemyPlane hull;
    private float sink;

    public void Setup(Vector3 heading, float sailSpeed, EnemyPlane hullUnit)
    {
        start = transform.position;
        dir = heading.normalized;
        speed = sailSpeed;
        hull = hullUnit;
        born = Time.time;
    }

    private void Update()
    {
        if (GameState.Paused) return;

        if (hull == null || hull.IsDead)
        {
            // Batiyor: yavasca alcalir ve yatar
            sink += Time.deltaTime;
            transform.position += Vector3.down * (3f * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, Mathf.Min(sink * 3f, 22f));
            if (sink > 9f) Destroy(gameObject);
            return;
        }

        Vector3 pos = start + dir * (speed * (Time.time - born));
        pos.y = start.y + Mathf.Sin(Time.time * 0.9f + born) * 0.5f;
        transform.position = pos;
    }
}
