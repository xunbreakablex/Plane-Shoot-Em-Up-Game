using UnityEngine;
using UnityEngine.InputSystem;

// Takip kamerasi: roll'u tamamen takip etmez, fare sag tus / sag analog ile etrafa bakilir,
// yuksek G ve hizda hafif titrer, C ile mesafe degisir.
public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public float rotationLag = 6f;
    public float baseFov = 60f;
    public float speedFovBoost = 18f;
    public float maxSpeedForFov = 90f;

    private static readonly Vector3[] Offsets =
    {
        new Vector3(0f, 3.2f, -11f),   // yakin takip
        new Vector3(0f, 5.5f, -20f),   // uzak takip
        new Vector3(0f, 1.1f, -4.5f),  // alcak / kuyruk ustu
    };

    private Camera cam;
    private PlaneController plane;
    private Quaternion rot = Quaternion.identity;
    private Vector3 smoothUp = Vector3.up;
    private int offsetIndex;
    private Vector2 look;
    private float distanceScale = 1f;
    private float zoomBlend, zoomVel;   // 0 = normal, 1 = tam yakinlastirma (sag tik)
    private float smoothedFov;          // hiz artisli FOV (yakinlastirma uygulanmadan)

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        plane = target != null ? target.GetComponent<PlaneController>() : null;
        if (target != null)
        {
            rot = Quaternion.LookRotation(target.forward, Vector3.up);
            smoothUp = Vector3.up;
        }
    }

    private void Update()
    {
        if (GameState.Paused) return;

        var kb = Keyboard.current;
        if (kb != null && kb.cKey.wasPressedThisFrame)
            offsetIndex = (offsetIndex + 1) % Offsets.Length;

        // Sag tik: nisan alinan bolgeye (ekran ortasi) yakinlastirir; GameSettings.aimZoom / aimZoomTime
        var mouse = Mouse.current;
        bool zoomHeld = mouse != null && mouse.rightButton.isPressed && !GameState.UiOpen;
        float zoomTarget = zoomHeld ? 1f : 0f;
        zoomBlend = Mathf.SmoothDamp(zoomBlend, zoomTarget, ref zoomVel, GameSettings.Current.aimZoomTime);

        // Etrafa bakma yalniz gamepad sag cubugunda (sag tik artik zoom)
        Vector2 lookIn = Vector2.zero;
        var pad = Gamepad.current;
        if (pad != null)
            lookIn += pad.rightStick.ReadValue() * 90f * Time.deltaTime;

        if (lookIn.sqrMagnitude > 0.0001f)
        {
            look.x = Mathf.Clamp(look.x + lookIn.x, -150f, 150f);
            look.y = Mathf.Clamp(look.y - lookIn.y, -60f, 60f);
        }
        else
        {
            look = Vector2.Lerp(look, Vector2.zero, 4f * Time.deltaTime);
        }
    }

    private const float LevelGain = 4f;        // yatis hatasi (derece) basina duzeltme hizi (derece/sn)
    private const float LevelMaxRate = 70f;    // kameranin ufka donme hizi siniri (derece/sn)

    // Kamerayi ufka dogru yavasca dondurur; dikeye yaklastikca etkisi azalir (orada ufuk belirsiz).
    private void LevelCamera(float dt)
    {
        Vector3 f = rot * Vector3.forward;
        float horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - f.y * f.y));
        float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.5f, horizontal));
        if (weight <= 0f) return;

        Vector3 levelUp = Vector3.up - f * f.y;
        if (levelUp.sqrMagnitude < 1e-4f) return;
        levelUp.Normalize();

        float err = Vector3.SignedAngle(rot * Vector3.up, levelUp, f);
        float maxStep = LevelMaxRate * dt;
        float step = Mathf.Clamp(err * LevelGain * dt, -maxStep, maxStep) * weight;
        rot = Quaternion.AngleAxis(step, f) * rot;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;

        // Kamera yonu ucagin burnuna "en kisa donusle" (swing) gider. Eskiden her kare
        // LookRotation(forward, Vector3.up) kuruluyordu: tam dikeyde yon bir anda 180 derece donuyor,
        // loopta ve hizli donuslerde kamera sicriyordu. Yatis ekseni ise yalnizca ufka dogru
        // sinirli hizla duzeltilir (loopta kamera ucakla birlikte asilir, sonra yumusakca dogrulur).
        Vector3 fwd = target.forward;
        Quaternion swung = Quaternion.FromToRotation(rot * Vector3.forward, fwd) * rot;
        // FARE semasinda burun fareyle hizli doner; kamera daha siki takip eder ki nisan ekranda kaybolmasin
        float lag = ControlSettings.MouseSteering ? Mathf.Max(rotationLag, 9f) : rotationLag;
        rot = Quaternion.Slerp(rot, swung, 1f - Mathf.Exp(-lag * dt));
        LevelCamera(dt);

        Quaternion lookRot = Quaternion.Euler(look.y, look.x, 0f);
        float speed = plane != null ? plane.Speed : 0f;

        // Hizli giderken kamera biraz uzaklasir
        float wantScale = 1f + Mathf.Clamp01(speed / maxSpeedForFov) * 0.25f;
        distanceScale = Mathf.Lerp(distanceScale, wantScale, 2f * dt);

        Vector3 offset = Offsets[offsetIndex];
        offset.z *= distanceScale;

        Vector3 pos = target.position + rot * lookRot * offset;
        // Gorus "yukarisi" ucagin yatisini %25 takip eder; hizli yatan ucaklarda ekran savrulmasin diye
        // yumusatilir. Slerp yerine dogrusal karisim: ters yonlerde (ters ucus) yon cevirmez.
        Vector3 camUp = rot * Vector3.up;
        Vector3 wantUp = camUp * 0.75f + target.up * 0.25f;
        if (wantUp.sqrMagnitude < 1e-4f) wantUp = camUp;
        wantUp.Normalize();
        smoothUp = Vector3.Lerp(smoothUp, wantUp, 1f - Mathf.Exp(-9f * dt));
        if (smoothUp.sqrMagnitude < 1e-4f) smoothUp = wantUp;
        smoothUp.Normalize();

        Vector3 lookPoint = target.position + fwd * 8f;
        Quaternion finalRot = Quaternion.LookRotation(lookPoint - pos, smoothUp);

        // Titreme: yuksek G ve yuksek hizda. Esik ucagin G sinirina gore olceklenir (jetler 8-10 G'yi
        // rahat cekiyor, her donuste titremesin) ve titresim rastgele degil yumusak gurultudur.
        if (plane != null)
        {
            float gThreshold = Mathf.Max(3f, plane.maxG * 0.55f);
            float shake = Mathf.Clamp01((Mathf.Abs(plane.GForce) - gThreshold) / 4f) * 0.07f
                          + Mathf.Clamp01((speed / maxSpeedForFov) - 0.7f) * 0.03f;
            if (plane.IsStalling) shake += 0.04f;
            if (shake > 0f)
            {
                float n = Time.time * 11f;
                Vector3 noise = new Vector3(
                    Mathf.PerlinNoise(n, 0.31f) - 0.5f,
                    Mathf.PerlinNoise(n, 7.77f) - 0.5f,
                    Mathf.PerlinNoise(n, 13.13f) - 0.5f) * 2f;
                pos += noise * shake;
            }
        }

        transform.SetPositionAndRotation(pos, finalRot);

        if (cam != null)
        {
            float t = Mathf.Clamp01(speed / maxSpeedForFov);
            if (smoothedFov <= 0f) smoothedFov = cam.fieldOfView;
            smoothedFov = Mathf.Lerp(smoothedFov, baseFov + t * speedFovBoost, 3f * dt);

            // Yakinlastirma gercek buyutme carpani (tan tabanli): 1,35 = nisan bolgesi 1,35 kat buyuk
            float zoom = Mathf.Lerp(1f, Mathf.Max(GameSettings.Current.aimZoom, 1f), zoomBlend);
            float halfTan = Mathf.Tan(smoothedFov * 0.5f * Mathf.Deg2Rad) / zoom;
            cam.fieldOfView = 2f * Mathf.Atan(halfTan) * Mathf.Rad2Deg;
        }
    }
}
