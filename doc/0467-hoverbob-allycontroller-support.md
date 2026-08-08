# 0467. HoverBob - AllyController(아군 OC) 공중유닛 지원 누락

**날짜:** 2026-08-08

## 요청 내용
> 아 적이랑 아군 hoverbob은 잘 작동하는데 AllyController를 사용하는 아군OC의 경우는 작동을 안하네

`doc/0466`에서 고친 Awake→Start 타이밍 문제와는 별개로, `HoverBob`이 애초에 `UnitController`/
`EnemyUnitController` 두 종류만 검사하고 `AllyController`(아군 OC 전용 컨트롤러)는 아예 검사 대상에
없었음 - 그래서 `Raven (Ally)`/`Strike Drone (Ally)` 등 `AllyController`를 쓰는 아군 OC 공중유닛은
루트에 컨트롤러가 있어도 `shouldBob` 판정에서 항상 빠짐.

## 적용

- `HoverBob.cs`: `allyController`(`AllyController`) 필드 추가, `Start()`에서
  `GetComponentInParent<AllyController>()`로 조회, `Update()`의 `shouldBob` 조건에
  `allyController.IsAirUnit()` 분기 추가(`AllyController.IsAirUnit()`은 `UnitController`/
  `EnemyUnitController`와 동일한 시그니처로 이미 존재).

## 검증 (Play Mode)

- `Raven (Ally).prefab`을 실제로 씬에 스폰한 뒤 한 프레임 대기 후 확인:
  `cached allyController != null: True`, `bobbing: True`.
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 관련 에러 6건은 무관하게 그대로).
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- `git status`: `HoverBob.cs`만 이 세션이 만든 변경. `Cyborg Soldier`/`Railgunner`/`Striker`/
  `Brute Mech`/`Heavy Assault Tank`/`Ironhawk`/`Raven`(적 버전) 프리팹에 잡힌 변경은 이 세션이
  만든 게 아니라 동시에 진행 중인 다른 세션(`doc/0460`)의 작업 - 건드리지 않음.

## 변경된 파일

- `Assets/Scripts/Animation/HoverBob.cs`
