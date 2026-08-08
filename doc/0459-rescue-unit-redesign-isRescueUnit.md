# 0459. 구조 유닛 설계 변경 - `RescueSuppressor` 제거하고 `UnitController.isRescueUnit`으로

**날짜:** 2026-08-08

## 요청 내용
> 현재 확인된건 일단 구조전에는 fog Revealer Agent의 시야값이 1로 되어있다가 구조시 25로 정상적으로
> 돌아오도록 하고 유닛의 이름의 경우는 OC 이름으로만 바꾸는게 좋을거 같아 unitcontroller에
> 예외처리를 isRescueUnit 이라는 bool 필드를 통해서 이게 구조 유닛이면 현재 구조전 조종이 안되는걸
> 막아주면 될거 같고 비콘에 유닛이 닿으면 구조시 해당 bool을 변경하고 조종이 가능한 원리로
> 변경하게 좋을거 같고 현재 잘못된 부분이 있는지 확인해줘

## 조사 - 검토 결과 (구현 전 회신)

`doc/0458`의 `RescueSuppressor`(컴포넌트 전체 비활성화 + Layer/Tag 전환) 대신, `UnitController` 안에
`isRescueUnit` 플래그를 두는 방향으로 확인함. 검토 결과:

1. **가드 위치**: `UnitController`의 명령 진입점 13개가 전부 이미 `if (isConstructing) return;`로
   시작함 - `isRescueUnit`도 정확히 같은 자리에 얹으면 됨(기존 관례 재사용).
2. **자동교전은 그대로 작동**(이 가드는 명령 진입점만 막고 `AttackRange`의 자동교전 호출은 안 거침) -
   사용자 확인: 의도한 대로.
3. **Layer/Tag**: `isRescueUnit`만으로는 클릭 자체가 안 막힘/안 풀림(클릭은 Layer로 판정) - 사용자
   확인: 처음부터 정상 `Unit`/`AttackUnit`으로 두기로 함(레이어 전환 없음, 클릭/선택은 항상 가능하고
   명령만 `isRescueUnit`으로 막음).
4. `FogRevealerAgent.sightRange`는 세터가 없어 외부에서 못 바꿈 - `SetSightRange(int)` 추가 필요(사용자
   확인).
5. 유닛 표시 이름은 이미 있는 `heroName` 필드(영웅 유닛용, doc/0304)로 공짜 해결 - 사용자가 직접 적용함.
6. **발견한 버그**: Layer/Tag를 처음부터 정상 `Unit`으로 두기로 하면서, 구조 대상 유닛도
   `UnitController.Start()`가 정상적으로 돌아 `rtsController.UnitList`에 자기 자신을 등록하게 됨.
   그런데 `Stage3Objectives.IsAnyUnitWithinRadius`는 "비콘 rescueRadius 안에 아무 아군 유닛이나
   있으면 완료"로 판정하는데, 구조 대상 유닛 자신이 **처음부터 비콘 근처에 배치돼 있으므로** 이
   조건을 자기 자신만으로 즉시 충족해버림 - 플레이어가 아무것도 안 해도 미션 시작하자마자 서브목표가
   완료돼버리는 버그. 구조 대상 유닛 자신을 판정에서 제외하도록 수정함(아래 참고).

## 구현 결과

### 1) `RescueSuppressor.cs` 삭제

씬 어디에도 실제로 붙어있지 않은 상태였고(확인함), 로직이 전부 `UnitController`로 흡수됨 -
`doc/0458`에서 만들었던 파일이지만 이제 안 씀. 참고: 이 파일은 사용자가 그 사이 직접
"유닛 구조 추가" 커밋에 포함시켜뒀던 걸 확인함 - 지금 삭제로 그 커밋 위에 새 커밋이 얹히는 형태가 됨.

### 2) `UnitController.cs`

- `isRescueUnit`(bool, 기본 false), `rescuedMarker`(구조 후 Green 마커, 비워두면 항상 기존
  `unitMarker` 사용), `rescuedSightRange`(기본 25) 필드 추가.
- `fogRevealerAgent` 필드 추가 - `Awake()`에서 `GetComponent<FogRevealerAgent>()`로 조회.
- 명령 진입점 13곳: `if (isConstructing) return;` → `if (isConstructing || isRescueUnit) return;`.
- 선택 마커 관련 3곳(`SelectUnit`/`DeselectUnit`/`FlashMarker`/`FlashMarkerRoutine`)을 새 `ActiveMarker`
  프로퍼티(`isRescueUnit`이거나 `rescuedMarker`가 없으면 `unitMarker`, 아니면 `rescuedMarker`) 기준으로
  변경 - 구조 전엔 Yellow(`unitMarker`), 구조 후엔 Green(`rescuedMarker`)이 선택 시 표시됨.
- `Start()`에서 `rescuedMarker`도 시작 시 꺼둠(있는 경우).
- 신규 `public void Rescue()`: `isRescueUnit = false`, `unitMarker` 끄고 `rescuedMarker`는 현재 선택
  상태에 맞춰 켜고, `fogRevealerAgent.SetSightRange(rescuedSightRange)` 호출.

### 3) `FogRevealerAgent.cs`

`public void SetSightRange(int newRange)` 추가 - 기존 private `ReplaceRevealer`를 그대로 재사용(등록
지웠다 새 값으로 재등록). `fogWar`가 없으면(안개 없는 씬 등) 조용히 넘어감.

### 4) `Stage3Objectives.cs`

- `rescuedUnit`(단일 `RescueSuppressor`) → `rescuedUnits`(`List<UnitController>`)로 변경 - 실제로 8개
  유닛이 배치돼 있어서.
- 서브목표 완료 시 `rescuedUnits` 전부에 대해 `Rescue()` 호출.
- **버그 수정**: `IsAnyUnitWithinRadius`가 `rescuedUnits`에 포함된 유닛 자신은 판정에서 제외하도록
  수정(위 "발견한 버그" 참고) - 이제 실제로 다른 아군 유닛이 접근했을 때만 완료됨.

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- Unity 콘솔 Error 0건.

## 남은 것 (씬/프리팹 쪽, 확인만 - 손 안 댐)

`Assets/prefabs/OC/RescueUnit/`에 `Cyborg Soldier (Rescue).prefab`/`Heavy Assault Tank (Rescue).prefab`
Variant가 이미 만들어져 있는 걸 확인함(사용자가 직접 진행 중) - `Mission3.unity`도 계속 수정 중인 상태라
이번 턴에서는 건드리지 않음. `Stage3Objectives.rescuedUnits` 필드에 실제 8개 인스턴스를 연결하는 작업이
아직 필요함.

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/FogOfWar/FogRevealerAgent.cs`
- `Assets/Scripts/System/Stage3Objectives.cs`
- `Assets/Scripts/Unit/RescueSuppressor.cs` (삭제)
- `Assets/Scripts/Unit/RescueSuppressor.cs.meta` (삭제)
