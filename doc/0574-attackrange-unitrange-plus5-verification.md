# 0574. AttackRange 반경이 사거리+5로 되어있는지 전수 확인

- 날짜: 2026-08-14

## 요청 내용

- "현재 NTA 프리팹들의 AttackRange가 유닛의 사거리의 +5로 잘 되어있는지 확인해줘"
- 이어서: "모든 유닛들의 AttackRange의 범위가 사거리의 +5로 되어있는지 확인" (NTA뿐 아니라 전체 유닛으로 범위 확장)
- 이어서: "적유닛 외계종족 포함" (OC 적대유닛/아군, Spore Brood 외계종족까지 포함)

## 조사 내용

`AttackRange.cs` / `EnemyAttackRange.cs`의 `DetectionRangeMargin = 5f`와, 각 유닛 프리팹의 `AttackRange`(또는 `EnemyAttackRange`) 자식 GameObject에 붙은 `CapsuleCollider.m_Radius` / `UnitRange` 필드를 비교. 추가로 각 유닛의 `UnitDataSO.attackRange`(실제 사거리 스탯)와 프리팹에 박힌 `UnitRange` 값도 비교(스폰 시 `ApplyUnitData()`가 `UnitRange = data.attackRange`로 덮어쓰고 `EnsureDetectionRadius()`가 `Mathf.Max`로만 반경을 키우는 구조이기 때문).

대상 프리팹 23개: NTA 9종, OC 적대(Enemy) 9종(+ Ally 변형은 Prefab Variant로 값 상속, Rescue 변형 2종도 값 상속), Spore Brood 3종(Ripfang/Spitter/Skitterwing).

### 결과 1 - radius = 프리팹 UnitRange + 5

23개 전부 정확히 일치. 예외 없음.

### 결과 2 - 프리팹 UnitRange가 UnitDataSO의 실제 attackRange와 다른 유닛 (5개)

| 유닛 | 소속 | SO attackRange | 프리팹 UnitRange | 프리팹 radius | 사거리+5였다면 |
|---|---|---|---|---|---|
| Worker Drone | NTA | 4 | 2 | 7 | 9 |
| Brute Mech | OC | 6 | 2 | 7 | 11 |
| Ripfang | Spore Brood | 2 | 12 | 17 | 7 |
| Spitter | Spore Brood | 13 | 12 | 17 | 18 |
| Skitterwing | Spore Brood | 11 | 18 | 23 | 16 |

나머지 18개(Assault Trooper, Scout Drone, Sharpshooter, Ranger IFV, Pulsar Tank, SkyLancer, Firehawk, Guardian Drone, Nanobot Repair, Cyborg Soldier, Striker, Railgunner, Heavy Assault Tank, Ironhawk, Raven, Strike Drone, Cyborg Soldier (Rescue), Heavy Assault Tank (Rescue))는 SO attackRange와 프리팹 UnitRange가 정확히 일치.

### 영향 분석

`ApplyUnitData()`가 스폰 시 항상 `UnitRange = data.attackRange`로 덮어쓰고, `EnsureDetectionRadius()`는 `Mathf.Max`라 반경을 줄이지 않는다.

- Worker Drone / Brute Mech: 프리팹 UnitRange < 실제 사거리 → 런타임에 자동으로 정상 반경(9, 11)으로 보정됨. 실질적 문제 없음.
- Ripfang / Spitter / Skitterwing: 프리팹 UnitRange가 실제 사거리와 다른 채로 남아, 특히 Ripfang/Skitterwing은 실제 사거리보다 훨씬 큰 감지 반경(17, 23)이 런타임에도 그대로 유지됨 (실제 필요한 반경 7, 16보다 큼). 최근 커밋 "외계종족 수치 조정"에서 SO의 사거리 스탯만 바뀌고 프리팹 AttackRange 컴포넌트 값은 미갱신인 것으로 추정.

## 코드 변경

사용자 확인("수정해줘") 후, 5개 프리팹의 `AttackRange`/`EnemyAttackRange` 자식 오브젝트에서 `CapsuleCollider.m_Radius`와 `UnitRange`를 실제 사거리+5에 맞게 수정.

### Worker Drone.prefab (NTA)

기존 코드:
```yaml
  m_Radius: 7
  ...
  UnitRange: 2
```
변경 코드:
```yaml
  m_Radius: 9
  ...
  UnitRange: 4
```

### Brute Mech.prefab (OC)

기존 코드:
```yaml
  m_Radius: 7
  ...
  UnitRange: 2
```
변경 코드:
```yaml
  m_Radius: 11
  ...
  UnitRange: 6
```

### Ripfang.prefab (Spore Brood)

기존 코드:
```yaml
  m_Radius: 17
  ...
  UnitRange: 12
```
변경 코드:
```yaml
  m_Radius: 7
  ...
  UnitRange: 2
```

### Spitter.prefab (Spore Brood)

기존 코드:
```yaml
  m_Radius: 17
  ...
  UnitRange: 12
```
변경 코드:
```yaml
  m_Radius: 18
  ...
  UnitRange: 13
```

### Skitterwing.prefab (Spore Brood)

기존 코드:
```yaml
  m_Radius: 23
  ...
  UnitRange: 18
```
변경 코드:
```yaml
  m_Radius: 16
  ...
  UnitRange: 11
```

## 요약 / 남은 작업

- 5개 프리팹 모두 UnitDataSO/Spore Brood Unit Data SO의 실제 사거리와 UnitRange가 일치하도록, radius도 사거리+5로 맞춰 수정 완료.
- 나머지 18개 유닛은 기존부터 이미 정상이라 손대지 않음.

## 변경된 파일

- Assets/prefabs/NTA/Unit/MainBase/Worker Drone.prefab
- Assets/prefabs/OC/Unit/Tier2/Brute Mech.prefab
- Assets/prefabs/Spore_Brood/Unit/Ripfang.prefab
- Assets/prefabs/Spore_Brood/Unit/Spitter.prefab
- Assets/prefabs/Spore_Brood/Unit/Skitterwing.prefab
