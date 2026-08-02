# FogVisibility

`Assets/Scripts/FogOfWar/FogVisibility.cs`

## 개요

월드 좌표가 지금 안개(`csFogWar`)에 가려져 있는지 조회하는 공용 정적 헬퍼. `UserControl.IsRevealedByFog()`가
쓰던 것과 동일한 로직(Revealed/PreviouslyRevealed 둘 다 "보임"으로 인정)을, 미니맵 마커/체력바/점령
타이머/이펙트 스폰 등 여러 소비처가 각자 복제해 쓰던 걸 한 곳으로 모았다(doc/0356/0358/0359/0360/0361/0362).

이 헬퍼가 필요한 이유는 이 프로젝트의 안개가 셰이더 마스크나 카메라 컬링이 아니라 **실제 3D Plane**
(`csFogWar.fogPlane`, Y ≈ `levelMidPoint.y + fogPlaneHeight`, 씬 설정 기준 Y≈1)으로 구현돼 있기
때문이다. 카메라가 이 평면보다 높이 있는 한, Y가 이 평면보다 높은 오브젝트(미니맵 마커 Y40~50대, 체력바,
점령 타이머 슬라이더 등)로의 시선은 절대 이 평면 아래로 내려갈 일이 없어 물리적으로 가려질 수 없다 —
그래서 안개 상태를 직접 조회해서 렌더러를 켜고 끄는 방식으로 대신한다.

## 메소드

| 메소드 | 설명 |
|---|---|
| `IsRevealed(csFogWar fogWar, Vector3 worldPosition, int margin = 1)` | 해당 좌표(및 주변 `margin`칸)가 Revealed 또는 PreviouslyRevealed면 `true`. `fogWar`가 null이면(안개 없는 씬) 항상 `true`. |

## 소비처

- **`EnemyUnitController`**: 미니맵 마커(`minimapIcon`) 토글, 선택 중인 적 유닛이 안개에 가려지면 자동 선택 해제
- **`EnemyBuildingController`**: 미니맵 마커 토글, 선택 중인 적 건물이 안개에 가려지면 자동 선택 해제
- **`HealthManager`**: 체력바가 안개에 가려진 위치면 숨김(풀피가 아닐 때만 매 프레임 확인)
- **`CaptureSystem`**: 점령 타이머 슬라이더가 안개에 가려진 위치면 숨김
- **`EffectPlayer.Spawn()`**: 안개에 가려진 위치에서는 이펙트(공격/피격/사망/이착륙/건설 등)를 아예 스폰하지 않음 — `SpawnAtPoints`/`PlayHit`이 전부 이 메소드를 거치므로 한 곳만 고치면 전체 적용됨
- **`UnitEffects`**: 이동 트레일(지속형 이펙트)은 매 프레임 안개 상태를 재확인해서, 이동 중 안개 속으로 들어가면 꺼지고 나오면 다시 켜짐(발사 후 잊기 이펙트와 달리 스폰 시점 한 번으로는 부족하기 때문)
