using FischlWorks_FogWar;
using UnityEngine;

// 아군(CaptureOwner.Ally)이 점령한 TerritoryZone 내부는 그 안에 FogRevealer(유닛/건물)가 없어도
// 항상 밝게 보이도록, csFogWar의 fogField를 강제로 Revealed 처리한다.
//
// csFogWar.OnBeforeFogTextureUpdate 이벤트(FogRevealer 반영 직후, 텍스처를 굽기 직전 시점)에 맞춰
// 반영해야 한다 - LateUpdate 등 이후 타이밍에 반영하면 그 주기의 텍스처 캡처를 이미 놓친 뒤라 값이
// 계속 리셋되며 반투명하게 보이는 문제가 있었다([[territory-fog-reveal-timing-fix]] 0176).
//
// 점령이 풀리면(Owner가 Ally가 아니게 되면) 그 다음 갱신 주기부터 해당 zone은 더 이상 강제 반영하지
// 않으므로, csFogWar가 ResetTileVisibility()로 되돌린 뒤 그 자리를 비추는 FogRevealer가 없으면
// 자연히 다시 안개가 낀다 - 별도의 "해제 시 되돌리기" 로직이 필요 없다.
public class TerritoryFogReveal : MonoBehaviour
{
    private csFogWar fogWar;

    private void Start()
    {
        fogWar = FindFirstObjectByType<csFogWar>();

        if (fogWar == null)
        {
            Debug.LogWarning($"{name}: csFogWar를 씬에서 찾지 못해 영토 시야를 반영하지 못합니다.", this);
            return;
        }

        fogWar.OnBeforeFogTextureUpdate += RevealAlliedZones;
    }

    private void OnDestroy()
    {
        if (fogWar != null)
            fogWar.OnBeforeFogTextureUpdate -= RevealAlliedZones;
    }

    private void RevealAlliedZones()
    {
        foreach (TerritoryZone zone in TerritoryManager.Zones)
        {
            if (zone == null || zone.Owner != CaptureOwner.Ally) continue;

            RevealZone(zone);
        }
    }

    private void RevealZone(TerritoryZone zone)
    {
        Vector2[] polygon = zone.GetPolygonXZ();
        if (polygon.Length < 3) return;

        float minX = polygon[0].x, maxX = polygon[0].x;
        float minZ = polygon[0].y, maxZ = polygon[0].y;

        foreach (Vector2 p in polygon)
        {
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
        }

        Vector2Int gridMin = fogWar.WorldToLevel(new Vector3(minX, 0, minZ));
        Vector2Int gridMax = fogWar.WorldToLevel(new Vector3(maxX, 0, maxZ));

        for (int gx = gridMin.x; gx <= gridMax.x; gx++)
        {
            for (int gy = gridMin.y; gy <= gridMax.y; gy++)
            {
                Vector2Int cell = new Vector2Int(gx, gy);
                if (!fogWar.CheckLevelGridRange(cell)) continue;

                Vector3 cellWorldPos = new Vector3(fogWar.GetWorldX(gx), 0, fogWar.GetWorldY(gy));
                if (!zone.Contains(cellWorldPos)) continue;

                fogWar.shadowcaster.fogField[gx][gy] = Shadowcaster.LevelColumn.ETileVisibility.Revealed;
            }
        }
    }
}
