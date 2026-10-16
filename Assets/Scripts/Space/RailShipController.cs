using UnityEngine;
using UnityEngine.InputSystem;

// Uzay prototipi: oyuncu gemisi koridor (SpaceRail) boyunca kendiliginden ilerler; WASD (ve ok tuslari /
// gamepad sol cubuk) yalnizca kesitte iki eksende hareket ettirir: A/D sag-sol, W/S yukari-asagi.
// Ucus fizigi kapalidir (PlaneController.externalControl). Meteor duvarina degmek hasar verir ve
// gemiyi koridorun icine iter.
[RequireComponent(typeof(PlaneController))]
public class RailShipController : MonoBehaviour
{
    private const float ShipRadius = 4f;
    private const float MeteorHitCooldown = 0.8f;

    private PlaneController plane;
    private Rigidbody rb;
    private PlayerHealth health;
    private float nextMeteorHit;

    private void Awake()
    {
        plane = GetComponent<PlaneController>();
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        plane.externalControl = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // kinematikte desteklenen mod
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        plane.Respawned += OnRespawned;
        Snap();
    }

    private void OnDisable()
    {
        plane.Respawned -= OnRespawned;
    }

    // Yeniden dogus: koridorun ortasinda, ayni ilerleme mesafesinde
    private void OnRespawned()
    {
        SpaceRail rail = SpaceRail.Instance;
        if (rail == null) return;
        rail.Offset = Vector2.zero;
        rail.OffsetVelocity = Vector2.zero;
        Snap();
    }

    private void Snap()
    {
        SpaceRail rail = SpaceRail.Instance;
        if (rail == null) return;

        Vector3 pos = rail.PointAt(rail.Distance, rail.Offset);
        Quaternion rot = Quaternion.LookRotation(rail.Forward, rail.Up);
        transform.SetPositionAndRotation(pos, rot);
        rb.position = pos;
        rb.rotation = rot;
    }

    private Vector2 ReadMove()
    {
        Vector2 m = Vector2.zero;
        if (plane.IsCrashed || GameState.Paused) return m;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) m.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) m.x -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) m.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) m.y -= 1f;
        }

        var pad = Gamepad.current;
        if (pad != null) m += pad.leftStick.ReadValue();

        return Vector2.ClampMagnitude(m, 1f);
    }

    private void FixedUpdate()
    {
        SpaceRail rail = SpaceRail.Instance;
        if (rail == null) return;

        SpacePrototypeSettings cfg = GameSettings.Current.space;
        float dt = Time.fixedDeltaTime;

        // Kesitte hareket: ivmeli, elips seklindeki koridorun icinde
        Vector2 vel = Vector2.MoveTowards(rail.OffsetVelocity, ReadMove() * cfg.strafeSpeed, cfg.strafeAcceleration * dt);
        Vector2 off = rail.Offset + vel * dt;

        float a = cfg.corridorHalfWidth - ShipRadius;
        float b = cfg.corridorHalfHeight - ShipRadius;
        Vector2 clamped = SpaceRail.ClampToEllipse(off, a, b);
        if (clamped != off)
        {
            // Duvara dogru olan hiz bileseni sifirlanir (duvar boyunca kayar)
            Vector2 n = new Vector2(clamped.x / (a * a), clamped.y / (b * b)).normalized;
            float vn = Vector2.Dot(vel, n);
            if (vn > 0f) vel -= n * vn;
            off = clamped;
        }

        rail.Offset = off;
        rail.OffsetVelocity = vel;
        rail.Distance += cfg.forwardSpeed * dt;

        Vector3 pos = rail.PointAt(rail.Distance, off);

        // Gorsel: hareket yonune hafif burun cevirir ve yatar
        float bank = cfg.strafeSpeed > 0.1f ? vel.x / cfg.strafeSpeed * cfg.bankAngle : 0f;
        float fs = Mathf.Max(cfg.forwardSpeed, 1f);
        Vector3 look = rail.Forward + rail.Right * (vel.x / fs * 0.6f) + rail.Up * (vel.y / fs * 0.6f);
        Quaternion target = Quaternion.LookRotation(look, rail.Up) * Quaternion.Euler(0f, 0f, -bank);
        Quaternion rot = Quaternion.Slerp(rb.rotation, target, 1f - Mathf.Exp(-12f * dt));

        rb.MovePosition(pos);
        rb.MoveRotation(rot);
        plane.SetExternalVelocity(rail.Forward * cfg.forwardSpeed + rail.Right * vel.x + rail.Up * vel.y);

        CheckMeteors(rail, cfg, pos);
    }

    private void CheckMeteors(SpaceRail rail, SpacePrototypeSettings cfg, Vector3 pos)
    {
        if (plane.IsCrashed || Time.time < nextMeteorHit) return;
        if (!rail.TryMeteorContact(pos, ShipRadius, out Vector3 center)) return;

        nextMeteorHit = Time.time + MeteorHitCooldown;

        if (health == null) health = GetComponent<PlayerHealth>();
        if (health != null && cfg.meteorDamage > 0) health.TakeHit(cfg.meteorDamage, pos, true);

        // Koridorun icine geri it (meteordan uzaga)
        Vector3 away = pos - center;
        Vector2 away2 = new Vector2(Vector3.Dot(away, rail.Right), Vector3.Dot(away, rail.Up));
        if (away2.sqrMagnitude < 0.01f) away2 = -rail.Offset;
        if (away2.sqrMagnitude > 0.0001f)
            rail.OffsetVelocity = away2.normalized * cfg.meteorKnockback;

        if (CombatManager.Instance != null) CombatManager.Instance.SpawnHitSpark(pos + away.normalized * -ShipRadius);
    }
}
