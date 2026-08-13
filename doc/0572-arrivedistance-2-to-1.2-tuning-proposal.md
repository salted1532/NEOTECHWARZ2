# 0572 - arriveDistance 기본값을 2 → 1.2로 낮추는 제안

## 요청 내용

> arriveDistance: 0.5 → 1.2 정도로 변경해도 될거 같아
> 도착시 명령 종료만 잘된다면

## 확인

doc/0571에서 이미 "도착 시 명령 종료(`isStopped = true`)"는 값과 무관하게 동작하는 별개
메커니즘으로 구현되어 있음(doc/0399 패턴) - `arriveDistance`는 그 판정에 들어가는 "얼마나
가까워야 도착으로 칠지"의 문턱값일 뿐, 값을 2에서 1.2로 낮춰도 도착 시 명령 종료 로직 자체는
그대로 동작함. 즉 1.2로 낮추는 건 "더 타이트하게" 판정하는 튜닝이고, 회피로 밀려난 유닛이
`remainingDistance`/실거리가 1.2 밑으로 못 내려가면 doc/0571이 고치려던 증상(계속 비비적거림)이
재발할 위험이 2일 때보다는 있음 - 다만 NTA/Unit 프리팹 9개가 과거부터 실제로 1.2로 운영되어 온
값이라 실사용상 문제없었을 가능성이 높음.

## 변경 범위 (제안)

1. 스크립트 기본값 3곳을 `2f → 1.2f`로:
   - `Assets/Scripts/Unit/UnitController.cs:161`
   - `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs:80`
   - `Assets/Scripts/FogOfWar/Ally/AllyController.cs:71`
2. doc/0571에서 방금 `2`로 갱신한 24개 프리팹의 직렬화 값을 `1.2`로 재갱신 (같은
   `PrefabUtility.LoadPrefabContents` + `SerializedObject` 방식).
3. `NTA/Unit/*.prefab` 9개는 이미 `1.2`라 변경 없음 - 결과적으로 전 유닛 프리팹이 `1.2`로 통일됨.

## 확인 요청

이대로 진행할까요? (스크립트 기본값 3곳 + 프리팹 24개 재갱신) → 승인.

## 구현 결과

### 스크립트 기본값 3곳 `2f → 1.2f`

```diff
- private float arriveDistance = 2f;
+ private float arriveDistance = 1.2f;
```
- `Assets/Scripts/Unit/UnitController.cs:161`
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs:80`
- `Assets/Scripts/FogOfWar/Ally/AllyController.cs:71`

컴파일 확인: `npx uloop-cli compile --wait-for-domain-reload true` → `Success: true, ErrorCount: 0,
WarningCount: 40`(기존 경고 그대로, 이번 변경과 무관).

### 프리팹 24개 재갱신 - doc/0571에서 방금 `2`로 갱신했던 것과 동일한 24개를 다시 `1.2`로

같은 `update_arrive_distance.csx`(`prop.floatValue = 1.2f`로만 수정)를 재실행. `git diff`로
최종 상태 확인(커밋 시점 대비 `0.5 → 1.2`로 일관됨 - 중간의 `2` 단계는 아직 커밋 전이라
diff엔 안 남음):

```
Assets/prefabs/OC/RescueUnit/*.prefab (2개)          : 0.5 → 1.2
Assets/prefabs/OC/Unit/*.prefab (9개)                : 0.5 → 1.2
Assets/prefabs/OC/Ally/Unit/*.prefab (9개)           : 0.5 → 1.2
Assets/prefabs/Spore_Brood/Unit/*.prefab (3개)       : 0.5 → 1.2
Assets/prefabs/Test/TestEnemy.prefab                 : 0.5 → 1.2
```

`Assets/prefabs/NTA/Unit/*.prefab` 9개는 원래부터 `1.2`라 변경 없음 - 결과적으로 이제 모든
유닛 프리팹의 `arriveDistance`가 `1.2`로 통일됨.

참고: 이번에도 실행 도구가 보고한 `Result` 문자열의 "old value" 컬럼은 `2`로 잘못 표시됐다
(doc/0571에서도 동일 현상 - 원인 미상, `SerializedProperty` 캐싱 관련으로 추정). 실제 갱신
결과는 `git diff`로 직접 확인함 - 파일에 반영된 값은 정확하다.
