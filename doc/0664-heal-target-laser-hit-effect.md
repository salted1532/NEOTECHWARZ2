# 0664 - 힐 대상 쪽에 레이저 피격 이펙트 추가

## 요청
힐 닿는 부분(회복받는 대상 쪽)에도 레이저 공격 이펙트가 나오도록.

## 참고한 기존 패턴
건물 수리(doc/0658)가 이미 똑같은 요구를 겪었다 - `BuildingEffects.PlayRepairSpark(Vector3 workerPosition)`가
새 프리팹을 안 만들고 기존 `hitEffects.GetPrefab(AttackEffectType.Laser)`(공격 시 이미 쓰는 레이저 피격
파티클)를 그대로 재사용해서 `EffectPlayer.PlayHit()`으로 재생한다 - 콜라이더 표면 중 "그 방향(수리하는
일꾼 쪽)을 향한 지점"을 자동 계산해준다. 유닛 쪽엔 이 짝(`UnitEffects.PlayHealSpark`)이 없어서 추가.

## 구현
- `UnitEffects.cs`에 `PlayHealSpark(Vector3 healerPosition)` 추가 - `BuildingEffects.PlayRepairSpark`와
  동일하게 `hitEffects.GetPrefab(AttackEffectType.Laser)`를 그대로 재사용(새 프리팹 없음).
- `UnitController.cs`:
  - `BeginHeal()`에서 새 대상을 잡을 때 `healTargetEffects = target.GetComponent<UnitEffects>();` 캐싱
    (`repairAudio`/`repairEffects`를 `BeginRepair()`에서 캐싱하는 것과 동일 패턴).
  - `HealTick()`의 틱 시점(`healTickTimer <= 0f`, `unitAudio?.PlayHealTick()` 바로 다음)에
    `healTargetEffects?.PlayHealSpark(transform.position)` 호출 - `transform`은 치유 중인 힐러 자신의
    위치(=`PlayRepairSpark`가 받는 `workerPosition`과 동일 역할), 대상 쪽 콜라이더 표면 중 힐러를 향한
    지점에서 재생된다.
  - `StopHeal()`에서 `healTargetEffects = null;` 정리.

사운드(doc/0663의 `PlayHealTick`)와 같은 틱 주기(`healTickInterval`, 기본 0.5초)로 같이 나온다 - 매 프레임이
아니라 틱마다라 너무 잦지 않음.

## 결과
`Medic Drone.prefab`의 `UnitEffects.hitEffects.laserHitPrefab`은 Assault Trooper 복제 시점부터 이미
연결돼 있어(doc/0661 검수 당시 확인) 별도 프리팹 연결 작업 불필요. 컴파일 에러 0.
