# 0260 - gatherSFX가 재생되는 정확한 시점

**날짜:** 2026-07-28

## 요청 내용

> 유닛 사운드뱅크에서 Gather은 어느상황에서 나오는 소리야?

## 답변 요약

`UnitSoundBankSO.gatherSFX`는 `UnitController.GatherTick()`의 `GatherState.WaitingInQueue →
GatherState.Gathering` 전환 지점(`gatherTargetNode.IsTurnToGather(this)`가 true가 되는 순간)에서
`GetComponent<UnitAudio>()?.PlayGatherSFX()`로 원샷 재생된다 (doc/0255 구현 당시 `UnitController.cs`에
추가한 훅).

- 이동 중(`MovingToResource`)/대기 중(`WaitingInQueue`)/귀환 중(`MovingToBase`)/반납 중(`Depositing`)
  에는 재생되지 않는다.
- 자기 차례가 돌아와 실제 채취(`Gathering`)를 시작하는 순간에만 1회 재생.
- 루프 사운드가 아니라 원샷이라, `gatherDuration` 동안 지속되지 않고 채취 사이클이 새로 시작될 때마다
  (반납 후 복귀해서 다시 순서가 됐을 때 등) 매번 다시 한 번씩 재생된다.

프로젝트 코드는 변경하지 않음 (순수 Q&A, 기존 doc/0255 구현 내용 설명).

## 변경된 파일

없음.
