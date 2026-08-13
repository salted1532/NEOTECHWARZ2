# 0573 - arriveDistance를 전부 2로 통일 (NTA 포함)

## 요청 내용

> 아니다 다시 모든 프리팹의 값을 2로 바꾸자 NTA 포함

doc/0572에서 1.2로 통일했던 것을 되돌려 2로 재통일하되, 이번엔 doc/0571에서 "과거 인스펙터
튜닝값이라 존중"하며 제외했던 `NTA/Unit/*` 9개도 포함.

## 변경 범위 (제안)

1. 스크립트 기본값 3곳 `1.2f → 2f`로 되돌림:
   - `Assets/Scripts/Unit/UnitController.cs:161`
   - `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs:80`
   - `Assets/Scripts/FogOfWar/Ally/AllyController.cs:71`
2. doc/0572에서 1.2로 갱신한 24개 프리팹을 다시 2로.
3. `NTA/Unit/*.prefab` 9개(`UnitController`, 기존 `1.2` - doc/0571에서 존중해 제외했던 값)도
   이번엔 포함해서 2로 갱신.

합쳐서 총 33개 프리팹이 전부 `arriveDistance: 2`로 통일됨.

## 확인 요청

이대로 진행할까요? (스크립트 기본값 3곳 1.2→2 + 프리팹 33개 전부 2로 갱신, NTA 포함) → 승인.

## 구현 결과

### 스크립트 기본값 3곳 `1.2f → 2f`로 되돌림

- `Assets/Scripts/Unit/UnitController.cs:161`
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs:80`
- `Assets/Scripts/FogOfWar/Ally/AllyController.cs:71`

컴파일 확인: `npx uloop-cli compile --wait-for-domain-reload true` → `Success: true, ErrorCount: 0`.

### 프리팹 33개 갱신 - NTA/Unit 9개 신규 포함, 기존 24개 재갱신

`update_arrive_distance.csx`에 `NTA/Unit/*.prefab` 9개 경로를 추가하고 대상값을 `2f`로 바꿔
재실행. `git diff`로 최종 상태 확인(커밋 시점 대비):

```
arriveDistance: 2   ×33 (전부)
  - 기존 0.5 → 2  (24개: OC/Unit, OC/Ally/Unit, OC/RescueUnit, Spore_Brood/Unit, Test/TestEnemy)
  - 기존 1.2 → 2  (9개: NTA/Unit/* - doc/0571에서 존중해 제외했던 값, 이번엔 포함)
```

모든 유닛 프리팹(NTA/OC/OC Ally/Spore_Brood/Test)의 `arriveDistance`가 `2`로 완전히 통일됨.
컴파일 확인: `npx uloop-cli compile` → `Success: true, ErrorCount: 0, WarningCount: 0`.
