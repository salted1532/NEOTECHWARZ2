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

## 후속 - "구조 전 유닛 단일선택 불가능" 진단 및 수정

사용자가 `Assets/prefabs/OC/RescueUnit/`에 `Cyborg Soldier (Rescue).prefab`/
`Heavy Assault Tank (Rescue).prefab` 두 Variant를 만들고 `Mission3.unity`에 8개(Cyborg Soldier x6,
Heavy Assault Tank x2) 배치 완료함. "선택은 되는데 조종은 안 되어야 하는데 지금은 선택 자체가 안 됨"
문제를 조사한 결과, 두 프리팹 모두에서 다음이 확인됨:

1. **루트 Layer/Tag가 여전히 `AllyOC`/`AllyOC`** - `doc/0459`에서 합의한 대로 "처음부터 정상
   `Unit`/`AttackUnit`" 상태가 안 돼 있었음. `UserControl`의 유닛 클릭은 `layerUnit`(Unit 레이어)만
   레이캐스트하므로, Layer가 `AllyOC`로 남아있으면 클릭 자체가 안 걸림 - **이게 "선택 불가" 버그의
   직접 원인**.
2. **`unitID`/`enemyDataUnitID`가 뒤바뀜** - `unitID`에 OC 로스터 번호(Cyborg Soldier=2,
   Heavy Assault Tank=6)가 그대로 남아있고 `enemyDataUnitID`는 0이었음. 이러면 `Start()`가
   `GetEnemyUnitData()`(OC 테이블) 대신 `GetUnitData(2)`/`GetUnitData(6)`(NTA 테이블, 엉뚱한 다른
   NTA 유닛 - `RTSUnitController.UnitID`에 따르면 각각 Marine/Wraith)를 조회해서 완전히 잘못된
   스탯이 적용됐을 것.
3. **자식 사거리 컴포넌트가 아직 `AllyAttackRange`** - `EnemyUnitController`→`UnitController`로
   바꾸면서 자식의 사거리 감지도 플레이어용 `AttackRange` 클래스로 같이 바꿔야 했는데 안 돼 있었음 -
   `UnitController.attackRange`가 null이라 공격 자체가 불가능한 상태.
4. **`FogRevealerAgent.sightRange`가 25** - 구조 전엔 1이어야 하는데 처음부터 25로 돼 있었음(구조
   후 값과 동일해서 사실상 시야 축소 효과가 아예 없는 상태).

### 적용한 수정 (두 프리팹 전부)

Unity Editor 다이나믹 코드로 `PrefabUtility.LoadPrefabContents`를 열어 직접 수정:
- 루트 `layer` → `Unit`(6), `tag` → `AttackUnit`.
- `unitID` ↔ `enemyDataUnitID` 값을 맞바꿈(`unitID=0`, `enemyDataUnitID`엔 원래 있던 OC 로스터 번호).
- 자식의 `AllyAttackRange` 컴포넌트를 제거하고 플레이어용 `AttackRange`를 추가, `UnitRange` 값
  그대로 이전(`doc/0448`에서 검증된 리플렉션 필드 복사 방식 재사용) - Cyborg Soldier `UnitRange=12`,
  Heavy Assault Tank `UnitRange=20` 확인됨.
- `FogRevealerAgent.sightRange` → `1`.

씬에 이미 배치된 8개 인스턴스는 전부 이 두 프리팹의 인스턴스라(개별 오버라이드 없음) 프리팹만
고치면 자동으로 반영됨 - 실제로 8개 인스턴스 전부 재확인해서 `layer=Unit tag=AttackUnit unitID=0
enemyDataUnitID=(2 또는 6) isRescueUnit=True AttackRange(player)=True`로 정상 반영된 것 확인함.
`Stage3Objectives.rescuedUnits`(8개)도 이미 연결돼 있는 것 확인함(사용자가 직접 연결함).

### 검증

- Unity 콘솔 Error 0건.
- `git status`: 두 프리팹 파일만 변경됨(씬 파일 등 부수 변경 없음).

## 후속 - "enemyDataUnitID가 이름도 가져오는지" 확인

`ApplyUnitData(UnitData data)`는 스탯만 적용하고(icon/공격력/장갑/체력 등) **이름은 전혀 건드리지
않음**. Info Panel 이름은 `RTSUnitController`가 별도로 `heroName`(있으면) 아니면
`GetUnitName(unitID)`(NTA 테이블, `unitID`로 조회)로 정함 - `enemyDataUnitID` 경로는 `unitID`가 0이라
그 조회가 항상 빈 문자열이 됨. 즉 `heroName`을 지우면 이름이 안 뜸(사용자가 정확히 그 상태를 만든 것).

### 수정

`UnitController.Start()`에서 `ApplyUnitData` 직후, `enemyDataUnitID > 0`이고 `heroName`이 비어있으면
OC 데이터의 `unitName`으로 자동 채우도록 추가함 - 이제부터는 `heroName`을 비워둬도 자동으로 OC
이름이 뜸(직접 다른 이름을 쓰고 싶으면 `heroName`에 채워두면 그게 우선됨, 기존 영웅 유닛 관례 그대로).

## 변경된 파일 (추가)

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/prefabs/OC/RescueUnit/Cyborg Soldier (Rescue).prefab`
- `Assets/prefabs/OC/RescueUnit/Heavy Assault Tank (Rescue).prefab`

## 후속 - 유닛 사운드(음성/SFX)도 NTA에서 가져오기

### 요청 내용
> 현재 가져온거에서 유닛 사운드(음성,SFX등)은 NTA쪽에서 가져올수 있나? 결국엔 같은 ID라서 가져와도
> 무방할거야

### 조사

`UnitAudio.Awake()`가 사운드뱅크를 조회하는 방식: `unitController != null`이면
`rtsController.GetUnitData(unitController.GetUnitID())`(NTA 테이블, `unitID`로 조회)만 봄 - 구조
유닛은 `unitID=0`이라 이것도 항상 실패해서 **지금 이 유닛들은 사운드가 완전히 없는 상태**였음(이름
문제와 동일한 원인). `doc/0441`에 이미 나와 있듯 OC 로스터는 NTA와 완전히 동일한 번호로 재스킨된
구조라 - Cyborg Soldier(OC ID 2)와 Marine(NTA ID 2)처럼 - `enemyDataUnitID`를 그대로 NTA `unitID`로
다시 조회해도 스탯 밸런스 의미상 어긋나지 않음(요청하신 "같은 ID라서 무방하다"는 전제가 맞음).

### 적용

- `UnitController.cs`: `GetEnemyDataUnitID()` getter 추가.
- `UnitAudio.cs`: OC 쪽 조회 결과에 `soundBank`가 없고 `enemyDataUnitID`가 설정돼 있으면,
  `enemyDataUnitID`를 NTA `unitID`로 다시 조회해서 사운드뱅크만 대신 가져오도록 폴백 추가(스탯은
  이미 OC 값으로 적용된 상태라 안 건드림 - 사운드뱅크 조회만 대체).

### 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- Unity 콘솔: 새로 발생한 Error 없음(기존에 있던 Editor Inspector 관련 에러 6건은 그대로 - 제 코드와
  무관).

### 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Audio/UnitAudio.cs`

## 후속 - "비콘에 유닛을 가져가도 인식을 못함" 진단

### 요청 내용
> 현재 비콘에 유닛을 가져가도 인식을 못하는거 같은데 확인좀

### 조사

Play Mode에서 직접 확인함. 먼저 지상 유닛(Worker Drone)을 비콘 위치로 옮겨보니 `survivorsRescued`가
정상적으로 `true`로 전환되고 8개 유닛 전부 `Rescue()`가 호출됨(에러 없음) - 메커니즘 자체는 정상.

문제는 **공중 유닛**이었음(사용자가 직접 짚어냄: "공중유닛이라 안닿았나보네"). `IsAnyUnitWithinRadius`가
`Vector3.Distance`(Y축 포함 3D 거리)로 판정하는데, 공중 유닛(Firehawk 등)은 `airCruiseAltitude`만큼
항상 떠 있어서(확인한 실측값 약 5~10) 비콘 바로 위까지 가도 Y축 차이만으로 `rescueRadius`(기본 2)를
넘어버려 절대 인식될 수 없었음. Firehawk를 비콘 XZ 좌표로 정확히 옮긴 뒤(Y는 그대로 띄운 채) 재확인해서
이 가설을 확정함.

### 적용한 수정

`Stage3Objectives.IsAnyUnitWithinRadius`를 수평 거리(XZ만) 기준으로 변경 - Y축(고도) 차이는 무시.
지상 유닛에게는 원래 동작과 동일(둘 다 Y가 거의 같으므로), 공중 유닛도 비콘 바로 위로 오면 정상
인식됨.

### 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`.
- Play Mode 재확인: Firehawk를 비콘 XZ 좌표로 옮기고(고도는 그대로) `survivorsRescued`가 `true`로
  전환됨을 확인.
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 에러 6건은 무관하게 그대로).

### 변경된 파일

- `Assets/Scripts/System/Stage3Objectives.cs`

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/FogOfWar/FogRevealerAgent.cs`
- `Assets/Scripts/System/Stage3Objectives.cs`
- `Assets/Scripts/Unit/RescueSuppressor.cs` (삭제)
- `Assets/Scripts/Unit/RescueSuppressor.cs.meta` (삭제)
