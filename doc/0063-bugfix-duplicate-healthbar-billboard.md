# 0063. 버그수정: 체력바가 조금 닳아 보이는 문제 - HealthBarBillboard 중복 부착

**날짜:** 2026-07-12

## 요청 내용
> 새로 생성된 유닛에서만 발견되는 건지 모르겠는데, 체력은 그대로인데 체력바만 조금 닳아있는 버그가 있다. 확인해달라.

## 조사 과정
1. `HealthManager.Awake()`/`UpdateHealthSlider()`, `UnitSpawner.Spawn()`, `UnitController.Awake()`를 먼저 훑었지만, 스폰 시점에 실제 체력을 깎거나 최대체력을 나중에 바꾸는 코드는 없었다(사용자 말대로 "체력 값 자체"는 문제가 없었음).
2. 코드만으로는 원인을 특정할 수 없어, `Assets/prefabs/UI/HealthBar.prefab`과 이를 사용하는 유닛 프리팹(`Worker Drone.prefab`)을 YAML 텍스트로 직접 열어서 확인했다.
3. `HealthBar.prefab` 구조:
   ```
   HealthBar (루트)          ← HealthBarBillboard 부착
    └─ Canvas (World Space)  ← HealthBarBillboard가 여기에도 중복 부착!
        └─ Slider
   ```
   `HealthBarBillboard`(같은 스크립트, guid `599c37d1...`)가 **부모(HealthBar)와 자식(Canvas) 두 곳 모두**에 붙어 있었다.

## 원인
`HealthBarBillboard.LateUpdate()`는 매 프레임 `transform.rotation`(월드 회전)을 `Quaternion.Euler(카메라의 X, 0, 0)`으로 직접 대입한다. 이게 부모와 자식 양쪽에서 동시에 실행되면:
- 부모(HealthBar)가 자기 월드 회전을 `(카메라X, 0, 0)`으로 맞춘다.
- 자식(Canvas)도 자기 월드 회전을 `(카메라X, 0, 0)`으로 맞추려 하는데, 자식의 "월드 회전"을 직접 대입하면 Unity는 `부모의 월드 회전 × 자식의 로컬 회전 = 지정한 값`이 되도록 자식의 로컬 회전을 역산한다. 부모가 이미 `(카메라X, 0, 0)`이므로 자식의 로컬 회전은 강제로 **항등회전(0,0,0)**이 되어버린다.
- 그런데 `Canvas`는 원래 `m_LocalRotation`이 X축 **-90도**로 authored돼 있었다(체력바 평면을 올바른 각도로 눕혀서 보여주기 위한 고정 보정값). 이 보정값이 두 번째 빌보드 스크립트 때문에 매 프레임 지워지고 있었다.

그 결과 체력바 캔버스가 의도한 각도가 아니라 살짝 다른 각도로 렌더링되어, 가로 막대(Fill)가 카메라 각도에 따라 비스듬히 보이면서 실제 체력과 무관하게 "조금 닳아있는" 것처럼 보였다. `HealthBar.prefab` 자체(공용 프리팹)의 문제라 이 프리팹을 쓰는 모든 유닛/건물에 공통으로 있던 문제이며, 새로 생성된 유닛에만 국한된 문제는 아니었다(사용자도 "새로 생성된 유닛에서만인지 모르겠다"고 확인해주신 대로).

## 수정 내용
`Assets/prefabs/UI/HealthBar.prefab`에서 `Canvas` 오브젝트에 중복 부착돼 있던 `HealthBarBillboard` 컴포넌트를 제거했다. `HealthBar`(루트) 오브젝트에만 남겨둬서, `Canvas`의 원래 로컬 -90도 보정이 유지된 채로 부모(루트)만 카메라 쪽으로 회전하고 자식은 그 상대 각도를 그대로 따라가도록 했다.

이 프리팹은 각 유닛/건물 프리팹에 **중첩 프리팹(Nested Prefab)** 으로 포함돼 있으므로, 공용 프리팹 하나만 고치면 이를 쓰는 모든 유닛/건물에 자동으로 반영된다(개별 유닛 프리팹을 하나하나 고칠 필요 없음).

## 변경 예정 파일
- `Assets/prefabs/UI/HealthBar.prefab`

## 상태
**적용 완료 (단, 어느 오브젝트에 남길지가 반대였음)** — 실제로는 `Canvas`가 아니라 `HealthBar`(부모)에 스크립트를 남겨서 카메라 반대 방향을 보는 문제가 이어졌다. [[0064-bugfix-healthbar-billboard-wrong-object|0064]]에서 `Canvas`로 옮겨서 바로잡음.
