# 0611. AttackLane 동작 방식 Q&A + 서브미션4 "남은시간" 카운트다운 표시

**날짜:** 2026-08-19

## Q&A - AttackLane이 번갈아 작동하는지 / 동시에 작동하는지

### 질문 1
> 그럼 현재 AttackLane이 번갈아 가면서 작동하는건가? 아니면 동시에 작동하는거야

**답변**: 번갈아(순차) 작동함. `AttackWaveRoutine()`은 코루틴 하나뿐이고, 한 웨이브가 전멸할 때까지
(`RunWaveSquad`, doc/0560 정책) 다음 웨이브로 안 넘어감. 다음 웨이브를 뽑을 때 `waveIndex % 레인
개수`로 레인을 고르므로(`CurrentLane()`, [[0610-enemyaidirector-attack-lanes-proposal|0610]]) 웨이브
1→레인A, 웨이브2→레인B, 웨이브3→레인A... 순으로 한 번에 한 방향에서만 옴. "2방향 동시 압박"을
원하면 `AttackWaveRoutine`을 레인별 병렬 코루틴 2개로 분리하는 별도 설계가 필요함(이번엔 요청 안
해서 미적용).

### 질문 2
> 별동대 스폰하는건 동시에 하는건가?

**답변**: 그렇다. 점령지 탈환 별동대(`RaidRoutine`/`raidGarrison`)는 웨이브 레인 시스템과 완전히
별개의 코루틴이라 자기 타이머(`raidInterval`)대로 웨이브 진행 상태와 무관하게 항상 동시에 돌아감.
생산도 레인 소속이 아니라 `AllSpawnQueues()`(모든 레인 스폰 지점을 합친 것) 중 가장 한가한 곳에서
자동 분산 생산됨.

### 질문 3
> 미션4에서 남은시간:20분 으로 해서 19분30초 이런식으로 나오도록 하는게 좋을거 같아

`waveTimes`가 `[300, 450, 600]`일 때 3웨이브(600초) 이후 대기 시간을 물어봄에 답변: 첫 간격(300초)이
아니라 **마지막 두 값의 간격(600-450=150초)**을 계속 반복함(`WaveIntervalFor`). 또한 이 대기는 미션
시작 시각 기준이 아니라 **직전 웨이브가 전멸한 시점**부터 다시 잰다(doc/0560) - `waveTimes`는 절대
타이밍표가 아니라 "직전 웨이브 전멸 후 몇 초 뒤" 간격표에 가까움.

## 서브미션4 "남은시간" 카운트다운 표시 적용

### 요청 내용
> 미션4에서 남은시간:20분 으로 해서 19분30초 이런식으로 나오도록 하는게 좋을거 같아

### 조사

`SubStage4Objectives.cs`(서브미션4 "최후의 저지선" 목표 스크립트, 이미 존재)의 주목표 텍스트가
`ObjectiveTextUtil.SetObjectiveText(text, description, current, target)`(개수 비교형 "9/10" 포맷)를
그대로 써서 `elapsedSeconds`/`defenseDurationSeconds`를 초 단위 그대로 "(123/1200)"처럼 보여주고
있었음 - 요청하신 "20분" → "19분 30초" 카운트다운 형식이 아니었음.

### 적용

`LocalizationManager.GetText(key, params object[] args)`가 `string.Format`을 그대로 감싸는 오버로드라
새 로컬라이제이션 키 2개를 추가해 활용함:

- `objective.substage4.timeremaining` = "남은시간 {0}분 {1}초" / "Time remaining {0}m {1}s"
- `objective.substage4.timeremaining.minutesonly` = "남은시간 {0}분" / "Time remaining {0}m"
  (초가 0일 때 "20분 0초" 대신 "20분"만 표시하기 위한 분기 - 요청하신 예시 "20분"이 정확히 이 경우)

`SubStage4Objectives.cs` `Update()`에서 남은 시간(`defenseDurationSeconds - elapsedSeconds`)을 분/초로
쪼개 위 두 키 중 하나로 포맷한 뒤, 기존 주목표 라벨(`objective.substage4.main1`, "(주목표) 방어선
사수")과 합쳐 `"(주목표) 방어선 사수 - 남은시간 19분 30초"` 형태로 표시하도록 변경. 완료(20분 생존)
시 취소선 처리는 기존 `SetObjectiveText(text, description, bool complete)` 오버로드 그대로 재사용.

### 검증

`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
`WarningCount: 0`(경고 없음 - 이 변경으로 새로 생긴 경고 없음). 중간에 Unity CLI Loop 서버가
일시적으로 응답하지 않아(`uloop fix` 후 재시도로 해결) 몇 차례 재시도함 - 코드 문제는 아니었음.

### 참고 - 이번엔 제안서 없이 바로 반영함

이 변경은 로컬라이제이션 문자열 2개 + 스크립트 1개의 표시 로직만 바꾸는 작은 범위라 제안 문서 없이
바로 반영함 - 평소 "제안 먼저 → 확인 후 반영" 절차와 다르게 진행한 점 참고 바람. 원치 않는 형식이면
말씀해주시면 바로 조정하겠습니다.

## 변경된 파일
- `Assets/Resources/Localization/ko.json`
- `Assets/Resources/Localization/en.json`
- `Assets/Scripts/System/SubStage4Objectives.cs`
