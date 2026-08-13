# 0558 - 배치형 방어 유닛 재생산 1회로 제한

## 날짜
2026-08-13

## 요청 내용
"현재 적AI에서 프리팹으로 미리 배치된 유닛의 경우 죽었을때 본인의 위치로 같은 유닛이 생산되어
이동하도록 하는데 추가 생산된 유닛까지 죽으면 더이상 해당 유닛에 대한 추가생산은 안하도록 함"

→ 원본 배치 유닛이 죽어서 재생산된 "대체 유닛"까지 또 죽으면, 그 슬롯은 더 이상 재생산하지 않는다.
현재는 `EnemyAIDirector.RespawnDeadDefenseUnits()`가 `slot.current == null`이기만 하면 무한히 다시
세운다(doc/0552 설계 당시엔 횟수 제한이 없었음) - 재생산된 유닛이 또 죽어도 계속 생산된다.

## 원인 확인
`Assets/Scripts/System/EnemyAIDirector.cs`
- `DefenseSlot`(250행 부근): `current`만 들고 있고 "이미 한 번 재생산했는지" 여부를 기록하지 않음.
- `RespawnDeadDefenseUnits()`(671~686행): `slot.current != null`이면 skip, 아니면(죽었으면) 무조건
  다시 `Instantiate` - 몇 번째 재생산인지 구분이 없어 대체 유닛이 죽어도 또 채워짐.

`Assets/Scripts/System/AllyAIDirector.cs`에도 완전히 동일한 패턴(`DefenseSlot`/
`RespawnDeadDefenseUnits`, doc/0552 주석에 "EnemyAIDirector와 동일한 패턴"이라 명시)이 있어 아군 쪽도
같은 문제를 갖고 있음. 요청은 "적AI"만 언급했으나 같은 코드가 복제된 것이므로 함께 확인 필요.

## 설계안
`DefenseSlot`에 1회 재생산 여부를 기록하는 플래그를 추가한다.

```csharp
private class DefenseSlot
{
    public int unitID;
    public Vector3 position;
    public Quaternion rotation;
    public EnemyUnitController current;
    public bool respawned; // 원본이 죽어 이미 한 번 대체 생산됐는지 - true면 그 대체 유닛이 죽어도 더 이상 생산하지 않음
}
```

```csharp
private void RespawnDeadDefenseUnits()
{
    foreach (DefenseSlot slot in defenseSlots)
    {
        if (slot.current != null || slot.respawned)
            continue;

        UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(slot.unitID) : null;
        if (data == null || data.Prefab == null)
            continue;

        GameObject spawned = Instantiate(data.Prefab, slot.position, slot.rotation);
        if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
            slot.current = unit;
        slot.respawned = true;
    }
}
```

- 원본 사망 → 1회 재생산 → `respawned = true`로 표시.
- 대체 유닛까지 사망(`slot.current == null` 다시 됨) → `respawned`가 이미 true라 더 이상 생산 안 함.
- `AllyAIDirector.cs`도 동일 구조라 같은 방식으로 고칠 수 있음(요청 범위 확인 후 진행).

## 영향받는 파일
- `Assets/Scripts/System/EnemyAIDirector.cs` - `DefenseSlot`에 `respawned` 필드 추가,
  `RespawnDeadDefenseUnits()` 조건 수정.
- (범위 확인 시) `Assets/Scripts/System/AllyAIDirector.cs` - 동일 패턴 동일 수정.

## 확인 결과
사용자에게 물어본 결과 "둘 다 고침" 선택 - `EnemyAIDirector.cs`/`AllyAIDirector.cs` 둘 다 위 설계안대로
`respawned` 플래그를 추가해 수정함(코드 그대로 적용, 설계 변경 없음).

## 변경 상세
### `EnemyAIDirector.cs`
- `DefenseSlot`에 `public bool respawned;` 추가.
- `RespawnDeadDefenseUnits()` 조건을 `slot.current != null` → `slot.current != null || slot.respawned`로
  변경, `Instantiate` 이후 `slot.respawned = true;` 추가.
- 관련 주석 3곳(`defenseUnits` 헤더, `DefenseSlot` 선언 위, `RespawnDeadDefenseUnits()` 위) 갱신.

### `AllyAIDirector.cs`
- 동일하게 `DefenseSlot.respawned` 추가, `RespawnDeadDefenseUnits()` 조건/플래그 설정 동일 적용.
- 동일한 주석 3곳 갱신.

## 컴파일 확인
`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
`WarningCount: 0`.

## 남은 작업
없음 - 씬에 이미 등록된 `defenseUnits`는 코드만 바뀌므로 추가 설정 불필요.
