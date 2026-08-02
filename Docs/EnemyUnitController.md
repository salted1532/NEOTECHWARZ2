# EnemyUnitController

`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

## 개요

적 유닛 컨트롤러(구 `EnemyController` — 선택 표시/스탯/사망 처리만 담당하고 AI가 없었다가, 이동/전투
AI까지 합쳐지면서 doc/0231에서 개명됨). 플레이어의 `UnitController`에 대응하는 적 진영 버전이지만
기능은 훨씬 단순하다: **자동 교전(사거리 내 감지), 이동, 공격-이동** 세 가지만 지원한다(지정 대상 강제
추격, 건설/채집, 특성 스킬, 포탑/레이저 연동은 없음 — 단, 포탑/레이저 자체가 붙어있으면 옵셔널
컴포넌트로 연동됨).

> **전략적 AI는 아직 없음** — 씬에 미리 배치되거나 스포너로 생성된 적이 사거리 안에 들어온 대상과
> 자동 교전하고, 공격받으면 그 방향으로 반격하러 가는 정도의 "전술적" AI만 있다. 건물 배치/유닛 생산
> 같은 "전략적" 판단은 없음(로드맵 참고).

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `enemyMarker` | `GameObject` (SerializeField) | 선택 시/공격 지정 피드백 시 켜지는 마커 |
| `icon`, `enemyName` | (SerializeField) | Info_panel 표시용 아이콘/이름 |
| `minimapIcon` | `SpriteRenderer` (SerializeField) | 미니맵 전용 y40대 스프라이트 마커(빨간 원) — 안개에 가려지면 `Update()`에서 매 프레임 꺼짐/켜짐 |
| `minimapFogVisibilityMargin` | `int` (SerializeField, 기본 1) | 안개 조회 시 주변 몇 칸까지 같이 확인할지 |
| `enemyUnitID` | `int` (SerializeField) | OC Unit Data SO(`EnemyUnitDataSO`)와 매칭되는 ID, `Start()`에서 이 값으로 스탯 조회 |
| `attackDamage`, `armor`, `attackType`, `armorType`, `sizeType` | (SerializeField) | 전투 스탯 — `UnitController`와 동일한 패턴 |
| `canAttackGround`, `canAttackAir` | `bool` (SerializeField) | 공격 시 대상 도메인 제한 |
| `attackDelivery` | `AttackDeliveryType` (SerializeField) | Hitscan/Projectile — Projectile인데 `ProjectileAttack`이 없으면 자동 Hitscan 폴백 |
| `bonusVersusArmorType`/`Percent` | (SerializeField) | 특정 장갑타입 상대 고유 추가 데미지 |
| `isAirUnit`, `moveSpeed`, `airCruiseAltitude`, `airGroundLayer` | (SerializeField) | 공중 유닛 이동 — NavMesh 대신 직접 좌표 보간, `UnitController`와 동일한 지형 추적 방식 |
| `attackRange` | `EnemyAttackRange` | 자식 컴포넌트, 사거리 내 대상 감지 |
| `turretController`, `laserBeamAttack`, `projectileAttack` | 옵셔널 컴포넌트 | 붙어있으면 자동 연동(포탑 조준/반동, 레이저 빔, 투사체 발사) |
| `fogWar` | `csFogWar` (private) | 미니맵 마커/선택 해제 판정용 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 자식/옵셔널 컴포넌트 캐싱, 공중 유닛이면 스폰 즉시 목표 고도로 상승 시작 |
| `Start()` | 마커 비활성화, `rtsController`/`fogWar` 캐싱, `enemyUnitID`로 OC Unit Data SO 조회해 스탯 자가 적용(`ApplyUnitData`) |
| `Update()` | 공중 유닛 이동/지상 유닛 도착 판정, `AttackMoveTick()`, `UpdateFogVisibility()` |
| `UpdateFogVisibility()` (private) | 안개 조회 결과 하나를 미니맵 마커 토글과 `RTSUnitController.ClearSelectedEnemyIfMatches()`(선택 중이면 안개 속으로 들어갈 때 자동 해제)에 함께 사용 |
| `MoveTo(destination)` | 이동 명령 — 진행 중인 공격 이펙트 정지 후 이동 |
| `AttackMoveTo(destination)` | 플레이어의 "A + 클릭"과 동일 — 이동 중 사거리에 상대가 들어오면 자동 교전, 끝나면 목적지로 이동 재개 |
| `ChaseTarget(pos)` | `EnemyAttackRange`가 사거리 밖 감지 대상에게 다가갈 때 호출 |
| `Attack(end, target)` | 사거리 안 대상 공격 — 몸체 회전(포탑 없으면), 데미지 적용(Hitscan/Projectile), 공격 이펙트/SFX/레이저/포탑 반동 재생 |
| `HandleAttacked(...)` (private, `HealthManager.OnDamaged` 구독) | 감지 범위 밖에서 공격받으면 공격자 위치로 공격-이동해서 반격(아군 공격에는 반응 안 함) |
| `SelectEnemy()` / `DeselectEnemy()` / `FlashMarker()` | 선택 마커 on/off, 공격 지정 피드백 깜빡임 |
| `IsIdle()` / `IsMove()` / `IsAttack()` / `IsAirUnit()` / `IsCurrentlyMoving()` | 상태 조회(애니메이터/이펙트가 폴링) |
| `ApplyUnitData(data)` | OC Unit Data SO 값으로 스탯 덮어쓰기(생산/씬 배치 공통 경로) |
| `Die()` | 선택 목록에서 제거 후 파괴 |

## 연관 컴포넌트

- **RTSUnitController**: `selectedEnemyList` 등록/해제(`ClearSelectedEnemyIfMatches`), OC 데이터 조회
- **HealthManager**: 데미지 이벤트 구독(반격 트리거), 사망 시 `IDestructible.Die()` 호출
- **FogVisibility**: 미니맵 마커 표시 여부, 선택 해제 판정
- **EnemyAttackRange**: 사거리 내 대상 자동 감지/교전
