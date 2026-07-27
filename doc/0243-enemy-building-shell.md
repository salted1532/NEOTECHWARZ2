# 0243 - 적 건물을 체력만 있는 "껍데기"로 구현

## 배경

캠페인은 실시간 경제/생산 AI가 아니라 정해진 스크립트(대본)대로 유닛을 배치·스폰하는 방식이 될 예정이라,
적 건물이 `BuildingController`처럼 실제 생산 큐/자원 소모/건설 그리드를 가질 필요가 없다는 논의 끝에,
지금은 체력만 있는 "껍데기"로 만들고 나중에(스커미시에서 OC를 플레이 가능한 진영으로 만들 때) 실제
기능을 붙이기로 함.

## 구현

`Assets/Scripts/Enemy/EnemyBuildingController.cs` 신규 - 최소한의 컴포넌트:
- `IDestructible` 구현: `Die()`는 그냥 `Destroy(gameObject)`만 함 (인구수 반환/자원 환불/생산 대기열
  정리 없음 - 애초에 그런 기능 자체가 없음)
- `buildingName`/`icon` 필드는 나중에 Info_panel 등에 연동할 때 바로 쓸 수 있도록 미리 넣어뒀지만
  지금은 어디서도 참조 안 함 (선택/정보창 연동 자체가 없음)

실제 데미지/체력 처리는 기존 `HealthManager`가 그대로 담당한다 (`GetDamage()`, `OnDamaged`/`OnDeath`
이벤트 등 전부 재사용) - `EnemyBuildingController`는 그 사망 콜백(`IDestructible.Die()`)만 구현.

클래스 이름을 처음부터 `EnemyBuildingController`(`BuildingController`와 대응)로 잡아둬서, 나중에 생산
큐 등 실제 기능이 필요해질 때 이 클래스를 확장하기만 하면 되고 다시 이름을 갈아엎을 필요가 없게 함
(예전에 `EnemyController`→`EnemyUnitController`로 갈아엎었던 것과 같은 상황을 피하려는 의도).

## 아직 안 된 것 - 실제 프리팹은 만들 수 없음

`Assets/prefabs/OC/Building/` 폴더가 아직 완전히 비어있어서(모델이 하나도 없음) 실제 프리팹은 만들지
못했음. 모델이 준비되면 아래 구성으로 프리팹을 만들면 됨:

- 모델(메쉬) + Collider (플레이어 유닛이 감지/공격할 수 있도록)
- `m_TagString: Enemy` (플레이어 쪽 `AttackRange`가 "Enemy" 태그만 감지하므로 - `EnemyUnitController`가
  없어도 `CanEngage()`의 도메인 판정이 자동으로 "지상"으로 취급해서 정상적으로 공격 대상이 됨, [[0240]])
- `HealthManager` (체력 값 지정)
- `EnemyBuildingController` (이 커밋에서 추가한 스크립트)

## 변경 파일

- `Assets/Scripts/Enemy/EnemyBuildingController.cs` (신규)
