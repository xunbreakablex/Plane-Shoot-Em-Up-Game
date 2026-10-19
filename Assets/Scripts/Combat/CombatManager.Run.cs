using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// CombatManager'in "asama yoneticisi" kismi: Stages.cs'deki dalga senaryosunu uygular,
// formasyonlari dogurur, asama sonu boss'u cikarir; asama tamam / zafer / yenilgiyi yonetir.
public partial class CombatManager
{
    // "Ignore Raycast" katmani: oyuncu ucagiyla fiziksel carpismaz, mermi/fuze/nisan isinlari yine vurur
    public const int EnemyLayer = 2;

    // Dusman govdesi olarak kullanilan model sayisi (modelPrefabs'in ilk N tanesi)
    public const int EnemyModelCount = 3;

    private StageDef stage;
    private int stageLoaded = -1;
    private int waveIndex;
    private bool bossSpawned;
    private bool midBossSpawned;
    private float waveClock;          // orta boss yasarken durur: dalgalar bekler
    private EnemyPlane boss;          // asama sonu boss
    private EnemyPlane midBoss;       // asama ortasi boss
    private float nextBossAdds;
    private int tutorialStage;

    public EnemyPlane Boss => boss;
    public EnemyPlane ActiveBoss => boss != null && !boss.IsDead ? boss : (midBoss != null && !midBoss.IsDead ? midBoss : null);
    public StageDef CurrentStage => stage;
    public int WaveCount => stage != null ? stage.waves.Length : 0;
    public int WavesSpawned => waveIndex;
    public bool BossSpawned => bossSpawned;

    private struct BannerItem
    {
        public string text;
        public Color color;
        public float duration;
    }

    private readonly Queue<BannerItem> bannerQueue = new Queue<BannerItem>();
    private BannerItem currentBanner;
    private float bannerStart;
    private bool bannerActive;

    // ------------------------------------------------------------- asama

    private void SetupStage(int index)
    {
        index = Mathf.Max(0, index);
        stage = Stages.Get(index);
        StageAtmosphere.Begin(index, stageLoaded < 0);
        stageLoaded = index;
        waveIndex = 0;
        waveClock = 0f;
        bossSpawned = false;
        midBossSpawned = false;
        boss = null;
        midBoss = null;
        nextBossAdds = 0f;

        foreach (var e in enemies)
            if (e != null && !e.IsDead) e.Vanish();
        enemies.Clear();
        Bullet.ClearEnemyBullets();

        bannerQueue.Clear();
        bannerActive = false;
        Announce($"AŞAMA {index + 1}: {stage.title.ToUpper()}", Color.white, 3.2f, true);
        Announce(stage.subtitle, new Color(0.85f, 0.9f, 1f), 2.6f);

        // Dusman ussu (ve gerekirse deniz gecisi icin isinlanma)
        PrepareStageWorld(index);
    }

    /// <summary>Asama tamam ekranindan "sonraki asama".</summary>
    public void NextStage()
    {
        int next = Mathf.Min(RunState.Stage + 1, RunState.LastStage);
        RunState.BeginStage(next);
        Medals.OnEndlessStage();
        SetupStage(next);

        // Asamalar arasi: kismen onarim (GameSettings.stageClearHeal), kalkan ve fuze dolumu
        GameSettings gs = GameSettings.Current;
        PlayerHealth ph = CurrentPlayerHealth;
        if (ph != null)
        {
            ph.Heal(Mathf.RoundToInt(ph.maxHealth * gs.stageClearHeal));
            ph.RechargeShield();
            ph.GrantInvulnerability(gs.stageStartInvulnerability);
        }
        if (gun != null) gun.RefillMissiles();
    }

    // ---------------------------------------------------------------- director

    private void UpdateDirector()
    {
        if (stageLoaded < 0 || stageLoaded != RunState.Stage)
        {
            if (RunState.Ended || RunState.StageIsClear) return;
            SetupStage(RunState.Stage);
        }

        if (RunState.Ended || RunState.StageIsClear) return;
        if (flight == null || flight.Current == null || stage == null) return;

        float t = RunState.StageTime;
        UpdateTutorialHints(t);

        // Dalga saati: orta boss yasarken durur (boss savasi kalabaliga bogulmasin)
        bool midAlive = midBoss != null && !midBoss.IsDead;
        if (!midAlive) waveClock += Time.deltaTime;

        // Asama ortasi boss
        if (!midBossSpawned && waveClock >= stage.midBossTime)
        {
            midBossSpawned = true;
            SpawnBossEncounter(stage.midBoss, EnemyKind.MidBoss);
        }

        // Senaryo dalgalari
        if (!midAlive)
        {
            while (waveIndex < stage.waves.Length && stage.waves[waveIndex].time <= waveClock)
            {
                SpawnWave(stage.waves[waveIndex]);
                waveIndex++;
            }
        }

        // Dalgalar bitince (ve ortalik sakinlesince ya da sure dolunca) asama sonu boss. Dusman ussu varsa
        // komuta merkezi yikilmis olmali (yikilmazsa ek bekleme sonunda komutan yine de gelir).
        if (!bossSpawned && midBossSpawned && !midAlive && waveIndex >= stage.waves.Length)
        {
            float lastTime = stage.waves[stage.waves.Length - 1].time;
            bool calm = AirEnemyCount() <= 3;
            bool baseDone = baseCleared || waveClock > lastTime + bossWaitAfterLastWave + GameSettings.Current.baseBossFallbackSeconds;
            if (baseDone && (calm || waveClock > lastTime + bossWaitAfterLastWave))
            {
                bool final = RunState.IsLastStage;
                SpawnBossEncounter(stage.boss, final ? EnemyKind.FinalBoss : EnemyKind.Boss);
            }
        }

        // Boss savasi: periyodik sürü takviyesi
        EnemyPlane active = ActiveBoss;
        if (active != null && Time.time >= nextBossAdds)
        {
            nextBossAdds = Time.time + (active.IsStageBoss ? 16f : 22f);
            int n = active.Kind == EnemyKind.FinalBoss ? 3 : 2;   // boss takviyesi az: boss'a odaklan
            Vector3 c = active.transform.position + Random.onUnitSphere * 40f;
            Vector3 toPlayer = PlayerTransform.position - c;
            SpawnFormationGroup(EnemyKind.Swarmer, n, c, toPlayer.normalized, Formation.V, -1);
        }
    }

    // Ilk iki oyunda kisa yonlendirme
    private void UpdateTutorialHints(float t)
    {
        if (RunState.Runs > 2 || RunState.Stage > 0) return;

        if (tutorialStage == 0 && t > 3f)
        {
            tutorialStage = 1;
            Announce(ControlSettings.FireTutorial, Color.white, 4f);
        }
        else if (tutorialStage == 1 && t > 12f)
        {
            tutorialStage = 2;
            Announce("Rozetli düşmanı vur: güçlendirme düşürür, üstünden geç", PowerUp.KindColors[0], 4f);
        }
        else if (tutorialStage == 2 && t > 24f)
        {
            tutorialStage = 3;
            Announce("B: bomba — ekranı ve düşman mermilerini temizler", PowerUp.KindColors[3], 4f);
        }
        else if (tutorialStage == 3 && t > 38f && baseDef.enabled && !baseCleared)
        {
            tutorialStage = 4;
            Announce("Üssü bas: KOMUTA MERKEZİNİ yıkınca uçaksavarlar susar ve komutan gelir", new Color(1f, 0.82f, 0.4f), 5f);
        }
    }

    // ----------------------------------------------------------- dalga dogurma

    private static float StrafeSpeed(EnemyKind kind)
    {
        return kind == EnemyKind.Heavy ? 58f : 86f;
    }

    private static float FormationSpacing(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Heavy: return 60f;
            case EnemyKind.Fighter: return 42f;
            default: return 26f;
        }
    }

    // Komutan telsizi: her dalgada kisa bir satir (neden vurdugunu soyler); art arda gelmesin diye araligi var
    private float nextRadio;

    private void Radio(EnemyKind kind, int count)
    {
        if (Time.time < nextRadio) return;
        nextRadio = Time.time + 14f;
        string line;
        switch (kind)
        {
            case EnemyKind.Swarmer: line = count > 1 ? "Komutan: avcı sürüsü peşinde, silkele ve indir!" : "Komutan: tek avcı yaklaşıyor, hallet."; break;
            case EnemyKind.Fighter: line = count > 1 ? $"Komutan: {count} avcı havalandı, üsse ulaşmadan önünü kes!" : "Komutan: bir avcı seni hedef aldı, kanadını vur!"; break;
            case EnemyKind.Heavy: line = "Komutan: bombardıman uçağı rotada, geçmesine izin verme!"; break;
            case EnemyKind.Kamikaze: line = "Komutan: KAMİKAZE! Yaklaşmadan vur, kaç!"; break;
            case EnemyKind.Sniper: line = "Komutan: nişancı uzaktan vuruyor, yan yan yaklaş."; break;
            default: return;
        }
        Announce(line, new Color(0.75f, 0.95f, 1f), 3.2f);
    }

    private void SpawnWave(WaveDef w)
    {
        Transform pt = PlayerTransform;
        if (pt == null) return;
        Radio(w.kind, w.count);

        Vector3 playerPos = pt.position;
        Vector3 vel = PlayerVelocity;
        Vector3 fwd = Vector3.ProjectOnPlane(pt.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        switch (w.formation)
        {
            case Formation.Pincer:
            {
                // Sol ve sagdan iki grup
                int leftN = w.count / 2;
                int rightN = w.count - leftN;
                for (int side = -1; side <= 1; side += 2)
                {
                    int n = side < 0 ? leftN : rightN;
                    if (n <= 0) continue;
                    Vector3 origin = playerPos + right * (side * 520f) + fwd * 380f;
                    origin.y = Mathf.Max(GroundHeight(origin) + 130f, playerPos.y + Random.Range(-10f, 40f));
                    Vector3 aim = playerPos + vel * 4f;
                    SpawnFormationGroup(w.kind, n, origin, (aim - origin).normalized, Formation.Line,
                        side > 0 ? w.drop : -1);
                }
                break;
            }

            case Formation.Ring:
            {
                // Her yonden tek tek, ayni anda merkeze yonelir
                float start = Random.value * 360f;
                for (int i = 0; i < w.count; i++)
                {
                    float a = (start + 360f * i / w.count) * Mathf.Deg2Rad;
                    Vector3 origin = playerPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 640f;
                    origin.y = Mathf.Max(GroundHeight(origin) + 130f, playerPos.y + Random.Range(-30f, 50f));
                    Vector3 aim = playerPos + vel * 5f;
                    SpawnEnemy(w.kind, origin, (aim - origin).normalized, i == w.count - 1 ? w.drop : -1);
                }
                break;
            }

            default:
            {
                // Onden gelen formasyon
                float yaw = Random.Range(-30f, 30f);
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * fwd;
                Vector3 origin = playerPos + dir * Random.Range(650f, 750f);
                origin.y = Mathf.Max(GroundHeight(origin) + 130f, playerPos.y + Random.Range(-15f, 50f));
                Vector3 aim = playerPos + vel * 6f + right * Random.Range(-30f, 30f);
                SpawnFormationGroup(w.kind, w.count, origin, (aim - origin).normalized, w.formation, w.drop);
                break;
            }
        }
    }

    // Bir formasyonu heading yonunde uctugu varsayilan koordinatta yerlestirir
    private void SpawnFormationGroup(EnemyKind kind, int count, Vector3 origin, Vector3 heading, Formation formation, int drop)
    {
        heading.y *= 0.3f;
        if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
        heading.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, heading);
        float spacing = FormationSpacing(kind);

        for (int i = 0; i < count; i++)
        {
            float off = i - (count - 1) * 0.5f;
            Vector3 pos = origin;

            switch (formation)
            {
                case Formation.V:
                    pos += right * (off * spacing) - heading * (Mathf.Abs(off) * spacing * 0.8f);
                    break;
                case Formation.Column:
                    pos -= heading * (i * spacing * 1.5f);
                    break;
                case Formation.Scatter:
                    pos += Random.insideUnitSphere * spacing * 2.5f;
                    break;
                default:    // Line
                    pos += right * (off * spacing);
                    break;
            }

            SpawnEnemy(kind, pos, heading, i == count - 1 ? drop : -1);
        }
    }

    /// <summary>Kodla uretilen boss govdesini dogurur (orta boss, asama sonu boss ya da test).</summary>
    public EnemyPlane SpawnBossEncounter(BossType bossType, EnemyKind kind)
    {
        Transform pt = PlayerTransform;
        if (pt == null || bossLit == null) return null;

        bool stageBoss = kind >= EnemyKind.Boss;
        if (stageBoss)
        {
            bossSpawned = true;
            RunState.EnterBossFight();
        }

        Vector3 fwd = Vector3.ProjectOnPlane(pt.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 pos = pt.position + Quaternion.AngleAxis(Random.Range(-25f, 25f), Vector3.up) * fwd * 640f;
        pos.y = Mathf.Max(GroundHeight(pos) + 170f, pt.position.y + 20f);

        GameObject go = BossBuilder.Build(bossType, bossLit, bossGlow != null ? bossGlow : bossLit);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((pt.position - pos).normalized, Vector3.up));
        go.transform.localScale = Vector3.one * BossBrain.ScaleOf(bossType);
        SetLayerRecursively(go, EnemyLayer);

        float endlessMul = RunState.Endless ? 1f + 0.12f * (RunState.Stage - Stages.All.Length + 1) : 1f;   // sonsuz modda bosslar her turda guclenir
        int hits = Mathf.Max(1, Mathf.RoundToInt(BossBrain.BaseHits(bossType) * Difficulty.BossHitsMul * endlessMul));

        var enemy = go.AddComponent<EnemyPlane>();
        var brain = go.AddComponent<BossBrain>();
        brain.type = bossType;

        int carrier = kind == EnemyKind.MidBoss ? (int)PickupKind.Power : -1;
        enemy.InitScripted(this, hits, kind, BossBrain.TitleOf(bossType), BossBrain.RadiusOf(bossType),
            BossBrain.RamOf(bossType), carrier);
        brain.Init(this, enemy);

        enemies.Add(enemy);
        RunState.RegisterSpawn(1);

        if (stageBoss) boss = enemy; else midBoss = enemy;
        nextBossAdds = Time.time + 10f;

        Color c = kind == EnemyKind.FinalBoss ? new Color(1f, 0.3f, 0.25f)
            : (stageBoss ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.65f, 0.25f));
        string prefix = kind == EnemyKind.FinalBoss ? "SON BOSS: " : (stageBoss ? "BOSS: " : "ORTA BOSS: ");
        Announce(prefix + BossBrain.TitleOf(bossType) + "!", c, 3.5f, true);
        if (uiAudio != null) uiAudio.PlayOneShot(Sfx.BossRoar, 0.9f);

        return enemy;
    }

    private EnemyPlane SpawnEnemy(EnemyKind kind, Vector3 pos, Vector3 heading, int drop)
    {
        if (modelPrefabs == null || modelPrefabs.Length == 0 || flight == null) return null;

        // Dusman modeli: oyuncunun kullandigi disindaki iki model
        // (Sadece ilk uc model dusman olur: WW1, WW2, planor; jetler oyuncuya ozel)
        var choices = new List<int>();
        for (int i = 0; i < modelPrefabs.Length && i < EnemyModelCount; i++)
            if (i != flight.CurrentIndex && modelPrefabs[i] != null) choices.Add(i);
        if (choices.Count == 0)
            for (int i = 0; i < modelPrefabs.Length && i < EnemyModelCount; i++)
                if (modelPrefabs[i] != null) choices.Add(i);
        if (choices.Count == 0) return null;
        int model = choices[Random.Range(0, choices.Count)];

        EnemyKindInfo info = EnemyKinds.Get(kind);
        bool strafe = kind == EnemyKind.Heavy;   // yalniz bombardiman duz gecis yapar; avcilar dogfight

        Vector3 look = heading.sqrMagnitude > 0.01f ? heading.normalized : Vector3.forward;
        GameObject go = Instantiate(modelPrefabs[model], pos, Quaternion.LookRotation(look, Vector3.up));
        go.name = "Enemy_" + kind + "_" + modelPrefabs[model].name;
        go.transform.localScale = Vector3.one * info.scale;

        Tint(go, info.tint, drop >= 0);
        SetLayerRecursively(go, EnemyLayer);

        foreach (var trail in go.GetComponentsInChildren<TrailRenderer>())
            trail.emitting = false;

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();

        float cruise;
        if (strafe) cruise = StrafeSpeed(kind);   // formasyon bozulmasin: ayni hiz
        else cruise = (model < modelCruise.Length ? modelCruise[model] : 55f) * info.speedMul * Random.Range(0.92f, 1.08f);
        float turn = (model < modelTurn.Length ? modelTurn[model] : 70f) * info.turnMul;

        var enemy = go.AddComponent<EnemyPlane>();
        enemy.Init(this, HitsFor(kind), cruise, turn, kind,
            strafe ? EnemyBehavior.Strafe : EnemyBehavior.Chase, look, drop);

        enemies.Add(enemy);
        RunState.RegisterSpawn(1);
        return enemy;
    }

    // Hedef oldurme suresi (sn): oyuncu hedefin uzerinde ates ederken, mermilerin yarisi isabet ederse.
    // Can sabit vurus sayisi degil, oyuncunun o anki ates gucune (donanim + guc seviyesi + upgrade) gore
    // hesaplanir: 4 silahli ucak dusmani 0,2 sn'de buharlastirmasin, 2 silahli ucak da sonsuza kadar ugrasmasin.
    private static float TimeToKill(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Swarmer: return 1.0f;
            case EnemyKind.Heavy: return 3.5f;
            case EnemyKind.Kamikaze: return 0.6f;
            case EnemyKind.Sniper: return 1.8f;
            default: return 1.5f;     // avci
        }
    }

    private int HitsFor(EnemyKind kind)
    {
        float dps = 12f;
        if (flight != null) dps = Mathf.Max(8f, PlayerGun.TheoreticalDps(LoadoutStore.Get(flight.CurrentIndex)));
        // Asama ilerledikce %12 daha dayanikli
        float grow = 1f + 0.12f * RunState.Stage;
        float hits = dps * 0.5f * TimeToKill(kind) * grow * Difficulty.HitsMul;
        return Mathf.Max(kind == EnemyKind.Kamikaze ? 2 : 4, Mathf.RoundToInt(hits));
    }

    public static void Tint(GameObject go, Color tint, bool carrier)
    {
        // Guclendirme tasiyicilari belirgin acik renkte
        Color target = carrier ? Color.Lerp(tint, new Color(1f, 0.9f, 0.4f), 0.45f) : tint;

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is TrailRenderer || r is ParticleSystemRenderer) continue;
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i].color = Color.Lerp(mats[i].color, target, 0.5f);
            r.materials = mats;
        }
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    // --------------------------------------------------------------- sonuclar

    // Boss yenildi: son asamaysa zafer, degilse asama tamam
    private void OnBossDefeated(EnemyPlane defeated)
    {
        if (RunState.Ended || RunState.StageIsClear) return;

        // Ekrani temizle: kalan dusmanlar ve mermiler yok olur
        Bullet.ClearEnemyBullets();
        foreach (var e in new List<EnemyPlane>(enemies))
        {
            if (e == null || e.IsDead || e == defeated) continue;

            // Us finali: kalan yer birimleri aninda degil, birkac saniyeye yayilan patlamalarla gider
            if (e.IsGround) SpawnExplosionBurst(e.transform.position, e.HitRadius, 1, Random.Range(0.1f, 2.4f));
            e.Vanish();
        }

        bool lastStage = RunState.IsLastStage;
        RunState.ClearStage();
        Medals.OnStageClear();
        if (uiAudio != null) uiAudio.PlayOneShot(Sfx.Victory, 0.8f);

        if (lastStage)
        {
            RunState.End(RunPhase.Won);
            Announce("OPERASYON TAMAM!", new Color(1f, 0.9f, 0.3f), 5f, true);
        }
        else
        {
            Announce("AŞAMA TAMAM!", new Color(0.5f, 1f, 0.6f), 3f, true);
        }
    }

    /// <summary>PlayerHealth ucak yok olunca cagirir.</summary>
    public void OnPlayerDied()
    {
        Streak = 0;
        if (RunState.Ended) return;

        // Asama tamamlandiktan sonra (Asama Tamam ekrani acilmadan) yere carpmak kazanilmis asamayi
        // kayba cevirmesin: ucak normal sekilde yeniden dogar, oyun devam eder
        if (RunState.StageIsClear) return;

        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null) return;

        int revivesLeft = RunUpgrades.Mods.revives - RunState.RevivesUsed;
        if (revivesLeft > 0)
        {
            RunState.RevivesUsed++;
            StartCoroutine(ReviveRoutine(plane));
            return;
        }

        // Oyun bitti: otomatik yeniden dogusu iptal et, sonuc ekrani RunResultUI'da acilir
        plane.CancelRespawn();
        RunState.End(RunPhase.Lost);
    }

    private IEnumerator ReviveRoutine(PlaneController plane)
    {
        plane.CancelRespawn();
        RunState.LosePower();

        Vector3 pos = plane.transform.position;
        Vector3 fwd = Vector3.ProjectOnPlane(plane.transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;

        yield return new WaitForSeconds(1.2f);

        pos.y = Mathf.Max(pos.y, GroundHeight(pos) + 220f);
        plane.ReviveAt(pos, Quaternion.LookRotation(fwd.normalized, Vector3.up));

        // Sok dalgasi: dusman mermileri temizlenir, yakindaki herkes ciddi hasar alir
        Bullet.ClearEnemyBullets();
        SpawnExplosion(pos);
        var snapshot = new List<EnemyPlane>(enemies);
        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;
            if ((e.transform.position - pos).sqrMagnitude < 250f * 250f)
                e.TakeHit(e.IsBoss ? 10 : 12, e.transform.position);
        }

        Announce("YENİDEN DOĞUŞ!", new Color(0.5f, 1f, 0.6f), 2.6f, true);
    }

    // ----------------------------------------------------------------- banner

    // urgent: siradaki ipuclarini atlar, hemen gosterilir (boss, zafer gibi)
    public void Announce(string text, Color color, float duration = 2.4f, bool urgent = false)
    {
        if (urgent)
        {
            bannerQueue.Clear();
            bannerActive = false;
        }
        bannerQueue.Enqueue(new BannerItem { text = text, color = color, duration = duration });
    }

    private void UpdateBanner()
    {
        float now = Time.time;
        if (bannerActive && now - bannerStart > currentBanner.duration) bannerActive = false;

        if (!bannerActive && bannerQueue.Count > 0)
        {
            currentBanner = bannerQueue.Dequeue();
            bannerStart = now;
            bannerActive = true;
        }
    }
}
