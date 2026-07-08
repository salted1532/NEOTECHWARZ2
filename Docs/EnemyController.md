# EnemyController

`Assets/Scripts/Enemy/EnemyController.cs`

## 개요

적 유닛에 부착되는 컨트롤러. 선택 표시(마커), 공격 지정 피드백(마커 깜빡임), Info_panel에 필요한 아이콘/이름/공격력/방어력 조회, 사망 처리를 담당한다. `UnitController`/`BuildingController`와 동일한 마커 패턴을 그대로 따른다.

> **AI 로직은 없음** — 이 클래스는 마커/스탯/아이콘 데이터와 선택·피드백 처리만 담당하며, 스스로 이동하거나 공격하는 동작은 구현되어 있지 않다(플레이어 유닛이 `AttackRange`/`UnitController`를 통해 일방적으로 공격하는 대상). Enemy AI는 로드맵의 미구현 항목.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `enemyMarker` | `GameObject` (SerializeField) | 선택 시/공격 지정 피드백 시 켜지는 마커 (평소엔 꺼져있음) |
| `icon` | `Sprite` (SerializeField) | Info_panel에 표시할 아이콘 |
| `enemyName` | `string` (SerializeField) | Info_panel에 표시할 이름 |
| `attackDamage`, `armor` | `int` (SerializeField) | 전투 스탯 - `UnitController`와 동일한 패턴으로 Info_panel 호버 툴팁에 표시 |
| `flashInterval`, `flashCount` | `float`, `int` (SerializeField) | 공격 명령(우클릭/A 모드) 피드백 깜빡임 간격/횟수 (기본 0.3초 × 3회) |
| `flashRoutine` | `Coroutine` | 진행 중인 깜빡임 코루틴 (중복 방지용) |
| `rtsController` | `RTSUnitController` | `Start()`에서 획득, 선택 상태 조회/정리에 사용 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 마커 비활성화, `rtsController` 획득 |
| `FlashMarker()` | 공격 명령(우클릭/A 모드)을 받았을 때 "이 적이 대상"임을 마커 깜빡임으로 피드백. 좌클릭 선택 마커와 같은 오브젝트를 재사용하므로 끝나면 실제 선택 상태로 복원 |
| `FlashMarkerRoutine()` (private) | 깜빡임 코루틴 본체 |
| `SelectEnemy()` / `DeselectEnemy()` | 좌클릭 선택 시 마커 on/off |
| `GetIcon()` / `GetEnemyName()` / `GetAttackDamage()` / `GetArmor()` | Info_panel 표시용 데이터 조회 |
| `Die()` | 선택 목록(`selectedEnemyList`)에서 제거 후 파괴. `HealthManager`의 `IDestructible` 구현체로 호출됨 |

## 연관 컴포넌트

- **HealthManager**: 사망 시 `IDestructible.Die()`로 이 컴포넌트의 `Die()`를 호출
- **RTSUnitController**: `selectedEnemyList` 등록/해제, Info_panel용 데이터 조회
- **UserControl**: 좌클릭 시 `ClickSelectEnemy`, 우클릭/A 모드 시 `AttackSelectedUnits` + `FlashMarker()` 호출
- **AttackRange / UnitController**: 플레이어 유닛이 이 컴포넌트가 붙은 오브젝트를 공격 대상으로 삼아 `HealthManager.GetDamage`를 호출(이 클래스 자체는 공격 로직을 갖지 않음)
