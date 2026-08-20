# 0643. 모든 스테이지 주목표/서브목표 달성 시 Mission Success 사운드 재생

## 요청 내용

> 주목표 달성 시 성공 사운드 출력 Mission Success 사운드클립이 작동하도록 하면 될거 같아
> 주목표, 서브목표 달성시 사운드 클립 작동하도록

## 조사 내용

`GlobalVoiceBankSO.missionSuccess` + `SoundManager.PlayMissionSuccessVoice()`는 이미 존재하고
클립도 등록되어 있음(doc/0464). 현재는 **Stage2Objectives.cs 한 곳에만** 연결되어 있고(doc/0464,
doc/0465), 나머지 스테이지(Stage0/1/3/4/5, SubStage1~4) 9개 스크립트는 전혀 재생되지 않음.

`Assets/Scripts/System/*Objectives.cs` 10개 파일을 전수 조사함.

### 주목표 (main) - 전 스테이지 공통 훅으로 일괄 처리 가능

10개 스크립트 전부 주목표 달성 조건을 판정한 뒤 예외 없이 `StageManager.Instance?.ReportVictory()`
하나만 호출한다. `StageManager.ReportVictory()`는 자체적으로 `Result != InProgress`면 즉시
반환하는 가드가 있어서 **정확히 1회만 실행됨이 이미 보장**되어 있다(`StageManager.cs:62-67`).

→ 스테이지마다 따로 "1회성 플래그"를 만드는 대신(9개 파일에 중복 로직 반복), `ReportVictory()`
안에 사운드 호출을 딱 한 줄 추가하면 10개 스테이지 전부의 주목표 달성 사운드가 한 번에 해결됨
(root-cause 방식 - 모든 호출부가 이미 이 한 지점을 거쳐감). Stage2Objectives.cs가 현재 갖고 있는
주목표 전용 재생 로직(`artifactSuccessSfxPlayed`)은 이제 중복이라 제거.

### 서브목표 (sub) - 스테이지마다 완료 판정 방식이 달라서 개별 처리 필요

서브목표는 스테이지마다 자체 `Update()`에서 불리언을 계산할 뿐 공통으로 거치는 지점이 없어서,
Stage2가 이미 쓰고 있는 패턴(목표별 `bool alreadyPlayed` 플래그 + "최초 1회만 재생" 헬퍼)을
스테이지별로 그대로 복제해야 함. 단, **서브목표 성격이 스테이지마다 다르므로 전부 똑같이 걸면 안 됨**:

| 스테이지 | 서브목표 | 성격 | 사운드 연결 |
|---|---|---|---|
| Stage0 | 적 전멸 / 광물 1000 | 조건 재평가형(다시 깨질 수 있음, 체크리스트용) | **연결** - 최초로 조건 달성한 순간 1회만 |
| Stage1 | 광물 2000 / 레이더 기지 점령 / 적 건물 전멸 | 조건 재평가형 | **연결** - 최초 달성 시 1회만 (3개 각각) |
| Stage2 | 연구 데이터 반납 | 1회성(한 번 반납하면 되돌릴 수 없음) | 이미 연결됨(doc/0465), 변경 없음 |
| Stage3 | 생존자 구조 | 1회성("되돌리지 않는다"는 주석 명시) | **연결** |
| Stage4 | OC 사령부 생존 | **시작부터 완료 상태(생존=완료)이고, 파괴되는 순간 "영구 미완료"로 바뀜** - 뭔가를 "달성"하는 순간이 없고 오히려 실패 확정 순간만 있음 | **제외** - 성공 사운드를 걸 "달성 시점"이 없음 (반대로 붙이면 실패를 성공음으로 알리는 꼴) |
| Stage5 | 없음(NTA 단독 작전) | - | 해당 없음 |
| SubStage1 | 적 정찰대 전멸 | 조건 재평가형 | **연결** |
| SubStage2 | OC 회수팀 전멸 | 조건 재평가형 | **연결** |
| SubStage3 | 없음 | - | 해당 없음 |
| SubStage4 | 최소 병력 5기 이상 생존 | **Stage4와 동일하게 시작부터 만족 상태(생존 인원 감시용)이고 시간이 지나며 깎이기만 함 - "달성 순간"이 없음** | **제외** (Stage4와 동일 이유) |

→ 조건이 재평가형(리셋될 수 있음)이어도 "최초 1회만 재생"으로 통일한다 - 체크리스트 표시 자체는
계속 재평가되지만(요청 사항, 취소선이 다시 사라질 수 있음), 성공 사운드는 "처음 달성한 순간의
보상"이라 한 번 울리면 충분하고 조건이 왔다갔다할 때마다 반복 재생되면 오히려 시끄럽다.

## 변경 계획

### `StageManager.cs` - 주목표 공통 훅
```diff
     public void ReportVictory()
     {
         if (Result != StageResult.InProgress) return;
         Result = StageResult.Victory;
+        SoundManager.Instance?.PlayMissionSuccessVoice(); // 주목표 달성 나레이션 - 모든 스테이지가 이 지점을 거쳐가므로 한 곳만 훅(doc/0643)
         OnVictory?.Invoke();
     }
```

### `Stage2Objectives.cs` - 중복 제거
`artifactSuccessSfxPlayed` 필드와 `PlayMissionSuccessSfxOnce(artifactDelivered, ref
artifactSuccessSfxPlayed);` 호출 삭제(이제 `ReportVictory()`가 대신 처리). `dataDelivered`(서브목표)
쪽은 그대로 유지.

### `Stage0Objectives.cs` / `Stage1Objectives.cs` / `Stage3Objectives.cs` / `SubStage1Objectives.cs`
/ `SubStage2Objectives.cs` - 서브목표 사운드 추가

Stage2Objectives의 기존 헬퍼 패턴을 그대로 복제 (파일마다 아래 형태의 private 헬퍼 1개 + 서브목표
개수만큼 `bool ...SfxPlayed` 필드):
```csharp
private void PlayMissionSuccessSfxOnce(bool objectiveComplete, ref bool alreadyPlayed)
{
    if (!objectiveComplete || alreadyPlayed)
        return;
    alreadyPlayed = true;
    SoundManager.Instance?.PlayMissionSuccessVoice();
}
```
- Stage0: `enemiesCleared`, `oreSecured`(신규 계산 필요, 현재는 인라인) 각각에 대해 호출
- Stage1: `oreSecured`(신규 계산 필요), `radarCaptured`, `allEnemyBuildingsDestroyed` 각각에 대해 호출
- Stage3: `survivorsRescued`
- SubStage1: `scoutsEliminated`
- SubStage2: `recoveryTeamEliminated`

### 변경하지 않는 파일
- `Stage4Objectives.cs`, `SubStage4Objectives.cs` - 위 표의 이유로 서브목표에 사운드 미연결
- `Stage5Objectives.cs`, `SubStage3Objectives.cs` - 서브목표 자체가 없음

## 영향받는 파일
- `Assets/Scripts/System/StageManager.cs`
- `Assets/Scripts/System/Stage0Objectives.cs`
- `Assets/Scripts/System/Stage1Objectives.cs`
- `Assets/Scripts/System/Stage2Objectives.cs` (중복 제거만)
- `Assets/Scripts/System/Stage3Objectives.cs`
- `Assets/Scripts/System/SubStage1Objectives.cs`
- `Assets/Scripts/System/SubStage2Objectives.cs`

C# 코드만 변경, `GlobalVoiceBankSO`/사운드 에셋은 이미 연결되어 있어 추가 작업 없음.

이대로 진행해도 될까요? (Stage4/SubStage4는 "달성 순간"이 없는 감시형 서브목표라 제외했는데, 혹시
다른 의도가 있으면 알려주세요.)

## 적용 결과

사용자 승인 후 제안대로 7개 파일 전부 그대로 적용:
- `StageManager.cs`: `ReportVictory()` 안에 `SoundManager.Instance?.PlayMissionSuccessVoice();`
  한 줄 추가 - 10개 스테이지 전부의 주목표 달성 사운드가 이 한 지점에서 해결됨.
- `Stage2Objectives.cs`: 이제 중복인 `artifactSuccessSfxPlayed` 필드와 그 호출 삭제, 서브목표
  (`dataDelivered`) 쪽만 유지.
- `Stage0Objectives.cs`: `enemiesCleared`/`oreSecured` 각각에 최초 1회 재생 훅 추가.
- `Stage1Objectives.cs`: 인라인으로만 쓰이던 `oreSecured` 불리언을 변수로 뽑아내고, `oreSecured`/
  `radarCaptured`/`allEnemyBuildingsDestroyed` 각각에 훅 추가.
- `Stage3Objectives.cs`: `survivorsRescued`에 훅 추가.
- `SubStage1Objectives.cs`: `scoutsEliminated`에 훅 추가 (주목표 `ReportVictory()` 직후 `return`이
  있어서, 같은 프레임에 주목표·서브목표가 동시에 완료되는 경우도 놓치지 않도록 훅을 그 `return`
  앞쪽에 배치).
- `SubStage2Objectives.cs`: `recoveryTeamEliminated`에 훅 추가.

`npx uloop-cli compile` 결과 `Success: true, ErrorCount: 0, WarningCount: 0`. `git status`로
의도한 7개 파일만 변경됐음을 확인.

## 변경된 파일
- `Assets/Scripts/System/StageManager.cs`
- `Assets/Scripts/System/Stage0Objectives.cs`
- `Assets/Scripts/System/Stage1Objectives.cs`
- `Assets/Scripts/System/Stage2Objectives.cs`
- `Assets/Scripts/System/Stage3Objectives.cs`
- `Assets/Scripts/System/SubStage1Objectives.cs`
- `Assets/Scripts/System/SubStage2Objectives.cs`
