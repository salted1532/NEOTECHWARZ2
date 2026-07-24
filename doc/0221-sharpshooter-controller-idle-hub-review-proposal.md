# 0221 - Sharpshooter.controller idle 허브 구조 점검 및 수정 (적용 완료)

날짜: 2026-07-24

## 요청 내용

사용자가 `Assets\Animation\Sharpshooter.controller`에 Idle state를 직접 추가하고
"idle -> Fire or Walk"로 가는 구조가 맞는 것 같다고 판단, 현재 상태를 점검하고 다음에
뭘 해야 할지 확인해달라고 요청.

## 조사 내용

`Assets\Animation\Sharpshooter.controller` 확인 결과:

- **State 3개**: `idle`(기본 상태), `Walk`, `Fire`
- **Motion 연결 확인** (guid로 각 fbx의 서브 클립과 대조, 전부 정상):
  - idle → `rifle aiming idle.fbx`
  - Walk → `walking.fbx`
  - Fire → `firing rifle.fbx`
- **전이 구조**: idle → Fire, idle → Walk, Walk → idle, Fire → idle (허브형).
  사용자가 의도한 "idle → Fire or Walk" 구조와 토폴로지는 일치함.
- **Animator 컴포넌트 연결**: `unit_Infantry_Light_A_yup.prefab`의 Animator 컴포넌트
  `m_Controller`가 이 controller의 guid(`02859eaa1c4f36d479f0163b46cd8ae3`)를 정확히 참조하고 있음
  → 컨트롤러 자체는 프리팹에 제대로 연결된 상태.

### 발견한 문제 3가지

1. **Parameters가 하나도 없음** (`m_AnimatorParameters: []`). 4개 전이 전부
   `m_Conditions: []` — 즉 게임 상태(이동 중인지, 발사했는지)와 전혀 연결되지 않고
   ExitTime(전부 0.75)만으로 자동 순환하는 상태.
2. **idle의 전이 순서 문제**: idle의 `m_Transitions`가 `[→Fire, →Walk]` 순서인데
   둘 다 조건이 없으므로, ExitTime에 도달하면 Unity는 리스트에서 먼저 오는 전이(→Fire)를
   항상 선택함. 결과적으로 idle은 사실상 Walk로 못 가고 idle↔Fire만 반복 재생됨
   (지금 에디터에서 Play 해보면 계속 사격 애니메이션만 반복되고 걷기는 절대 안 나올 것).
3. **Walk/Fire → idle도 조건 없이 ExitTime 0.75 고정**이라, 유닛이 계속 이동 중이어도
   0.75초 지점마다 강제로 idle로 끊겼다가 다시 튀는 현상이 생김. idle → Walk, idle → Fire도
   `Has Exit Time: 1`이라 idle 클립이 75% 재생될 때까지 기다려야 전환되므로, 이동 명령을
   내려도 즉시 반응하지 않고 최대 1클립 길이만큼 지연됨.

## 제안하는 변경 (기존 코드 → 변경 코드)

`Assets\Animation\Sharpshooter.controller`에 대해:

### 1) Parameters 추가

기존:
```yaml
  m_AnimatorParameters: []
```

변경:
```yaml
  m_AnimatorParameters:
  - m_Name: IsMoving
    m_Type: 4          # bool
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
  - m_Name: Fire
    m_Type: 9           # trigger
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
```

### 2) idle → Walk 전이 (fileID -6914177174667225604)

기존:
```yaml
  m_Conditions: []
  ...
  m_ExitTime: 0.75
  m_HasExitTime: 1
```

변경:
```yaml
  m_Conditions:
  - m_ConditionMode: 1   # If (bool true)
    m_ConditionEvent: IsMoving
    m_EventTreshold: 0
  ...
  m_HasExitTime: 0
```

### 3) idle → Fire 전이 (fileID 649983045404128812)

기존:
```yaml
  m_Conditions: []
  ...
  m_ExitTime: 0.75
  m_HasExitTime: 1
```

변경:
```yaml
  m_Conditions:
  - m_ConditionMode: 6   # trigger
    m_ConditionEvent: Fire
    m_EventTreshold: 0
  ...
  m_HasExitTime: 0
```
(순서 문제 해결: 이제 idle의 두 전이가 서로 다른 조건으로 갈리므로 리스트 순서와 무관하게
정확한 쪽으로 전이됨.)

### 4) Walk → idle 전이 (fileID -6293888172441673504)

기존:
```yaml
  m_Conditions: []
  m_HasExitTime: 1
```

변경:
```yaml
  m_Conditions:
  - m_ConditionMode: 2   # IfNot (bool false)
    m_ConditionEvent: IsMoving
    m_EventTreshold: 0
  m_HasExitTime: 0
```

### 5) Fire → idle 전이 (fileID 1330897869674452680)

조건은 그대로 없음 유지(발사 애니메이션은 끝까지 재생 후 복귀하는 게 자연스러움), 다만
ExitTime을 클립이 거의 끝날 때까지 재생되도록 올림:

기존:
```yaml
  m_ExitTime: 0.75
  m_TransitionDuration: 0.25
```

변경:
```yaml
  m_ExitTime: 0.9
  m_TransitionDuration: 0.1
```

## 남은 작업

- 위 컨트롤러 수정을 반영해도, **실제로 `IsMoving`/`Fire` 파라미터 값을 매 프레임 채워주는
  스크립트가 아직 없음** — 지난 요청(`doc/0220`)에서 제안한 대로 `UnitController.IsCurrentlyMoving()`을
  폴링해서 `Animator.SetBool("IsMoving", ...)`, 공격 시점에 `Animator.SetTrigger("Fire")`를
  호출하는 작은 컴포넌트(`UnitAnimatorDriver` 등)가 있어야 실제로 상태 전환이 일어남.
  컨트롤러만 고쳐놓으면 파라미터가 항상 기본값(false)이라 계속 idle에 머무름.
- 컨트롤러 파일은 YAML 텍스트라 직접 수정도 가능하지만, `m_Type`/`m_ConditionMode` 같은 enum
  값 오타 위험이 있어 Unity 에디터의 Animator 창에서 직접 설정하는 걸 권장할 수도 있음 —
  사용자에게 직접 수정 여부 확인 필요.

## 적용 결과

사용자가 "내가 YAML 직접 수정" 대신 Claude가 직접 수정하는 옵션을 선택 → 위 제안 그대로
`Assets\Animation\Sharpshooter.controller`에 적용 완료. 단, Fire 트리거 조건의
`m_ConditionMode`는 제안 초안의 `6`이 아니라 `1`(If)이 맞음 — Unity의 Trigger 파라미터는
Bool의 true 체크와 동일하게 `If`(1)로 직렬화되고, `6`은 Equals(정수 비교)용이라 트리거에는
쓰이지 않음. 실제 적용 시 이 부분을 바로잡아서 반영함.

## 변경된 파일

- `Assets\Animation\Sharpshooter.controller`
  - `m_AnimatorParameters`에 `IsMoving`(Bool), `Fire`(Trigger) 추가
  - idle→Walk 전이: Condition `IsMoving == true` 추가, `m_HasExitTime: 0`
  - idle→Fire 전이: Condition `Fire` 트리거 추가, `m_HasExitTime: 0`
  - Walk→idle 전이: Condition `IsMoving == false` 추가, `m_HasExitTime: 0`
  - Fire→idle 전이: `m_ExitTime: 0.75→0.9`, `m_TransitionDuration: 0.25→0.1` (조건 없음 유지)

## 남은 작업 (변경 없음)

- `IsMoving`/`Fire` 파라미터를 실제로 채워주는 스크립트(`UnitAnimatorDriver` 등)가 아직 없음.
  이게 없으면 파라미터가 항상 기본값(false)이라 idle에서 벗어나지 못함 — 다음 단계로 필요.
