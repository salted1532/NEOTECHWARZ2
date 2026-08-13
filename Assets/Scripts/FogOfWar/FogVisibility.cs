using UnityEngine;
using FischlWorks_FogWar;

// 월드 좌표가 지금 안개에 가려져 있는지(Revealed/PreviouslyRevealed면 보임) 조회하는 공용 헬퍼.
// UserControl.IsRevealedByFog()와 동일한 로직 - 체력바/미니맵 마커/점령 타이머 등 여러 소비처가
// 각자 복제해 쓰던 걸 한 곳으로 모았다 (doc/0356/0358).
public static class FogVisibility
{
    public static bool IsRevealed(csFogWar fogWar, Vector3 worldPosition, int margin = 1)
    {
        if (fogWar == null) return true; // 안개가 없는 씬에서는 항상 보이는 것으로 취급

        Vector2Int center = fogWar.WorldToLevel(worldPosition);

        for (int x = -margin; x <= margin; x++)
        {
            for (int y = -margin; y <= margin; y++)
            {
                Vector2Int cell = new Vector2Int(center.x + x, center.y + y);
                if (!fogWar.CheckLevelGridRange(cell)) continue;

                Shadowcaster.LevelColumn.ETileVisibility visibility = fogWar.shadowcaster.fogField[cell.x][cell.y];

                if (visibility == Shadowcaster.LevelColumn.ETileVisibility.Revealed ||
                    visibility == Shadowcaster.LevelColumn.ETileVisibility.PreviouslyRevealed)
                    return true;
            }
        }
        return false;
    }

    // "지금 실제로 시야 안(Revealed)"인지만 본다 - IsRevealed와 달리 PreviouslyRevealed(반쯤 밝음)는 false.
    // EnemyUnitController가 "발견됨(discovered)" 판정에 쓴다 (doc/0567).
    public static bool IsCurrentlyVisible(csFogWar fogWar, Vector3 worldPosition, int margin = 1)
    {
        if (fogWar == null) return true; // 안개가 없는 씬에서는 항상 보이는 것으로 취급

        Vector2Int center = fogWar.WorldToLevel(worldPosition);

        for (int x = -margin; x <= margin; x++)
        {
            for (int y = -margin; y <= margin; y++)
            {
                Vector2Int cell = new Vector2Int(center.x + x, center.y + y);
                if (!fogWar.CheckLevelGridRange(cell)) continue;

                if (fogWar.shadowcaster.fogField[cell.x][cell.y] == Shadowcaster.LevelColumn.ETileVisibility.Revealed)
                    return true;
            }
        }
        return false;
    }
}
