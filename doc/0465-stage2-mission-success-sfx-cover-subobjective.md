# 0465. 스테이지 2 성공 SFX - 서브목표(연구 데이터) 완료 시에도 재생

**날짜:** 2026-08-08

## 요청 내용
> 주목표 말고도 서브목표 완료시에도 나오도록 해줘

`doc/0464`에서는 주목표(유물) 반납 완료 = 승리(`ReportVictory`) 시점에만 성공 SFX가 울렸음.
서브목표(연구 데이터) 반납 완료 시에도 각각 독립적으로 울리도록 확장.

## 적용

- `Stage2Objectives.cs`: 목표별 1회성 재생 가드를 `artifactSuccessSfxPlayed`/`dataSuccessSfxPlayed`
  두 개로 분리하고, 중복 로직을 `PlayMissionSuccessSfxOnce(bool objectiveDelivered, ref bool
  alreadyPlayed)` 헬퍼로 묶음.
- `Update()`에서 `artifactDelivered`는 그대로 `ReportVictory()`를 호출하고(승리 조건은 안 바꿈),
  이어서 `artifactDelivered`/`dataDelivered` 각각에 대해 `PlayMissionSuccessSfxOnce`를 호출 -
  두 목표 모두 반납되면 SFX가 목표당 정확히 1번씩, 총 2번 울림.

## 검증 (Play Mode, Mission2)

- 리플렉션으로 `dataDelivered`만 강제로 `true`로 설정(유물은 안 건드림) 후 한 프레임 대기, 확인:
  `StageManager.Instance.Result=InProgress`(승리 아님, 의도대로), `dataSuccessSfxPlayed=True`,
  `artifactSuccessSfxPlayed=False` - 서브목표만으로도 독립적으로 재생 트리거되고 승리 조건에는
  영향 없음을 확인.
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 관련 에러 6건은 무관하게 그대로).
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- `git status`: `Global Voice Bank SO.asset`에 `doc/0464`의 `missionSuccess` 필드가 이번에
  디스크로 실제 직렬화됨(+5줄, 정상). `Mission2.unity`에도 변경이 잡혔지만 diff 확인 결과 이
  세션이 만든 변경이 아니라 사용자가 에디터에서 동시에 진행 중이던 별도 작업(`PinPoint_3` 오브젝트
  추가, `researchData` 필드 연결, 머티리얼 교체 등) - 건드리지 않음.

## 변경된 파일

- `Assets/Scripts/System/Stage2Objectives.cs` (목표별 1회성 SFX 가드 분리 + 헬퍼 추출)
