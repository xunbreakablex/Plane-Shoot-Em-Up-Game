using UnityEngine;

// Ucus sahnesinde: hangarda secilen donanimi tum oyuncu ucaklarina uygular
// (gorunum, performans, can/kalkan, taretler) ve magaza upgrade'lerini bunlarin uzerine bindirir.
// Silahlari PlayerGun okur. Yeni oyunda hurda ve upgrade'ler sifirlanir. Savas sirasinda yeni bir slot
// acilirsa (boss yenildi) donanim aninda yeniden uygulanir.
public class LoadoutApplier : MonoBehaviour
{
    public FlightTestManager flight;

    private void Start()
    {
        // Yeni oyun: hurda, upgrade'ler, skor ve asama sifirlanir. Hangardan "devam"la gelindiyse
        // kampanya (skor, guc, bomba, hurda, upgrade'ler) kaldigi asamadan surer.
        bool resumed = RunState.TryContinue(out int resumeStage);
        if (resumed)
        {
            RunState.BeginStage(resumeStage);
        }
        else
        {
            RunUpgrades.Reset();
            RunState.NewCampaign();
        }

        // Ucus ozellikleri her sahne baslangicinda kataloga gore uygulanir: sahnede serilestirilmis eski
        // degerler kalabiliyordu (Setup yeniden calistirilmadikca yeni ayarlar etkisiz kaliyordu).
        if (flight != null && flight.planes != null)
        {
            for (int i = 0; i < flight.planes.Length && i < AircraftCatalog.Count; i++)
            {
                if (flight.planes[i] != null)
                    flight.planes[i].ApplyPreset(AircraftCatalog.Get(i).preset);
            }
        }

        ApplyAll(true);

        RunUpgrades.Changed += Refresh;
        Unlocks.Changed += OnUnlocksChanged;
        Refresh();

        // Devam: asamadan cikarkenki can + asama arasi onarim ("sonraki asama" ile ayni); kalkan dolu
        if (resumed && flight != null && flight.planes != null)
        {
            float heal = GameSettings.Current.stageClearHeal;
            foreach (var plane in flight.planes)
            {
                PlayerHealth h = plane != null ? plane.GetComponent<PlayerHealth>() : null;
                if (h == null) continue;
                float frac = Mathf.Min(1f, RunState.ContinueHealthFraction + heal);
                h.SetState(Mathf.CeilToInt(frac * h.maxHealth), h.maxShield);
            }
        }
    }

    private void OnDestroy()
    {
        RunUpgrades.Changed -= Refresh;
        Unlocks.Changed -= OnUnlocksChanged;
    }

    private void ApplyAll(bool resetHealth)
    {
        if (flight == null || flight.planes == null) return;

        for (int i = 0; i < flight.planes.Length; i++)
        {
            PlaneController plane = flight.planes[i];
            if (plane == null) continue;

            PlaneLoadout loadout = LoadoutStore.Get(i);
            LoadoutStats stats = LoadoutMath.Compute(loadout);

            PlaneBuilder.Apply(plane.gameObject, i, loadout, true);
            // Gaza gore egzoz alevi + duman (kahramanda buyu parildamasi); cakilinca susar
            EngineFx.Attach(plane.gameObject, i, () => plane.IsCrashed ? -1f : plane.Throttle);

            // Uygulama sirasinda ucagin hizi/gazi bozulmasin
            float speed = plane.Speed;
            float throttle = plane.Throttle;
            plane.ApplyStats(stats);
            if (!resetHealth && plane.isActiveAndEnabled && speed > 1f) plane.InheritState(speed, throttle);

            PlayerHealth health = plane.GetComponent<PlayerHealth>();
            if (health == null) health = plane.gameObject.AddComponent<PlayerHealth>();
            health.Configure(Mathf.RoundToInt(stats.hp), stats.shieldMax, stats.shieldRegen);
        }
    }

    // Savas sirasinda slot acildi / kilitler sifirlandi. Donanim yeniden uygulanir ama can/kalkan
    // orani korunur (eskiden Configure cani tamamen dolduruyordu: gizli bedava onarim).
    private void OnUnlocksChanged()
    {
        int n = flight != null && flight.planes != null ? flight.planes.Length : 0;
        var healthFrac = new float[n];
        var shieldFrac = new float[n];
        var hadShield = new bool[n];
        for (int i = 0; i < n; i++)
        {
            PlayerHealth h = flight.planes[i] != null ? flight.planes[i].GetComponent<PlayerHealth>() : null;
            healthFrac[i] = h != null ? h.HealthFraction : 1f;
            shieldFrac[i] = h != null ? h.ShieldFraction : 1f;
            hadShield[i] = h != null && h.maxShield > 0f;
        }

        LoadoutStore.Resanitize();
        ApplyAll(false);
        Refresh();

        for (int i = 0; i < n; i++)
        {
            PlayerHealth h = flight.planes[i] != null ? flight.planes[i].GetComponent<PlayerHealth>() : null;
            if (h == null) continue;
            // Yeni acilan kalkan jeneratoru dolu baslar
            float shield = hadShield[i] ? shieldFrac[i] * h.maxShield : h.maxShield;
            h.SetState(Mathf.CeilToInt(healthFrac[i] * h.maxHealth), shield);
        }
    }

    // Upgrade veya hurda degisince: kalici etkileri (can/kalkan/itki/yatis) tum ucaklara uygula
    private void Refresh()
    {
        if (flight == null || flight.planes == null) return;

        UpgradeMods m = RunUpgrades.Mods;
        foreach (var plane in flight.planes)
        {
            if (plane == null) continue;
            plane.SetUpgradeMultipliers(m.thrustMul, m.rollMul);

            PlayerHealth health = plane.GetComponent<PlayerHealth>();
            if (health != null) health.ApplyUpgrades(m);
        }
    }
}
