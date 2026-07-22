## 2026-07-22

### 요청 내용

> 이대로 진행해줘 (`doc/0207` 승인)

### 코드 변경

`doc/0207`에서 제안한 대로 구현.

#### `Assets/Scripts/ScriptableObject/UnitDataSO.cs`

**변경 코드** (`attackRange` 필드 옆에 추가)
```csharp
    [field: SerializeField]
    public int attackRange { get; private set; }

    // 공격 1회 후 다음 공격까지 걸리는 시간(초). 값이 작을수록 더 빨리(자주) 공격한다.
    [field: SerializeField]
    public float attackSpeed { get; private set; }
```
(겸사겸사 `armorType`/`sizeType` 위 주석이 `doc/0205` 이전 내용 그대로 남아있어서 "실제 전투 계산에는 프리팹 값이 쓰인다"는 낡은 설명을 "스폰 시 ApplyUnitData가 이 값을 그대로 반영한다"로 갱신함.)

#### `Assets/Scripts/Unit/UnitController.cs`

**변경 코드** (`ApplyUnitData` 안)
```csharp
        timeBetweenAttacks = data.attackSpeed;
```

### 마이그레이션 중 발견한 사항

`New Unit Data SO.asset`을 다시 읽어보니, 지난 턴 이후 **사용자가 에디터에서 유닛 ID를 1~9로 순서대로 재정렬**했고(이전엔 8,9가 Sharpshooter/SkyLancer로 끼워져 있던 비연속 번호였음 — 프리팹 unitID를 맞추기로 한 작업의 일환으로 보임), `attackRange`/`attackDamge`/`hp` 등 여러 수치도 추가로 튜닝해둔 상태였음. ID 숫자가 바뀌어도 프리팹 참조(guid)는 그대로라, 각 유닛 항목을 guid로 다시 매칭해서 `doc/0207`에 정리해둔 유닛-값 매핑 그대로 정확한 `attackSpeed`를 채워 넣음.

### 요약/영향받는 파일

- `Assets/Scripts/ScriptableObject/UnitDataSO.cs` — `attackSpeed` 필드 추가
- `Assets/Scripts/Unit/UnitController.cs` — `ApplyUnitData`에서 `timeBetweenAttacks` 반영
- `Assets/Scripts/ScriptableObject/New Unit Data SO.asset` — 9개 유닛 전체에 `attackSpeed` 값 채움(기존 프리팹 값 그대로 마이그레이션, 밸런스 변화 없음)
