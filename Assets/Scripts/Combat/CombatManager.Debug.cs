using System.Collections.Generic;
using UnityEngine;

// CombatManager'in test/ayar komutlari: Atomic Console'dan (GameCheats) cagrilir.
public partial class CombatManager
{
    public static EnemyKind KindFor(BossType type)
    {
        switch (type)
        {
            case BossType.Dragon:
            case BossType.MagicalGirl: return EnemyKind.FinalBoss;
            case BossType.Jet:
            case BossType.Crab:
            case BossType.Zeppelin:
            case BossType.Demon:
            case BossType.Angel: return EnemyKind.MidBoss;
            default: return EnemyKind.Boss;
        }
    }

    public EnemyPlane DebugSpawnBoss(BossType type)
    {
        return SpawnBossEncounter(type, KindFor(type));
    }

    public int DebugClearEnemies()
    {
        int n = 0;
        foreach (var e in new List<EnemyPlane>(enemies))
        {
            if (e == null || e.IsDead) continue;
            e.Vanish();
            n++;
        }
        Bullet.ClearEnemyBullets();
        return n;
    }

    public void DebugJumpToStage(int index)
    {
        if (RunState.Ended) return;
        index = Mathf.Clamp(index, 0, Stages.All.Length - 1);
        RunState.BeginStage(index);
        SetupStage(index);
    }

    public void DebugSkipStage()
    {
        if (RunState.Ended || RunState.StageIsClear) return;
        OnBossDefeated(null);
    }
}
