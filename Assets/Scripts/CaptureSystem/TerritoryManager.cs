using System.Collections.Generic;
using UnityEngine;

// 씬에 흩어져 있는 모든 TerritoryZone을 한 곳에 등록해두고, "이 좌표가 (어떤 영역이든) 특정 소유자의
// 영토 안에 있는가?"를 한 번에 질의할 수 있게 해준다. 여러 영토가 겹치는 경우 합집합으로 취급한다.
public static class TerritoryManager
{
    private static readonly List<TerritoryZone> zones = new List<TerritoryZone>();

    public static void Register(TerritoryZone zone)
    {
        if (!zones.Contains(zone)) zones.Add(zone);
    }

    public static void Unregister(TerritoryZone zone) => zones.Remove(zone);

    public static bool IsInsideTerritory(Vector3 worldPos, CaptureOwner owner)
    {
        foreach (TerritoryZone zone in zones)
        {
            if (zone == null || zone.Owner != owner) continue;
            if (zone.Contains(worldPos)) return true;
        }
        return false;
    }

    public static bool IsInsideAlliedTerritory(Vector3 worldPos) => IsInsideTerritory(worldPos, CaptureOwner.Ally);
}
