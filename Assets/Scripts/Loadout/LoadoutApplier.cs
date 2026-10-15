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
        if (RunState.TryContinue(out int resumeStage))
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

    // Savas sirasinda slot acildi / kilitler sifirlandi
    private void OnUnlocksChanged()
    {
        LoadoutStore.Resanitize();
        ApplyAll(false);
        Refresh();
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
