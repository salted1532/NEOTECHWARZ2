# 0465. 구조 완료 시 마커 깜빡임(3회) + SFX 인스펙터 필드 추가

**날짜:** 2026-08-08

## 요청 내용
> 구조 되었을때 초록색으로 마커가 바뀌고나서 마커가 깜박거리는거 3번하고 sfx 사운드도 하나 나올수
> 있도록 인스펙터 필드를 만들어줘

## 조사

`UnitController.Rescue()`(doc/0459/0462)는 `rescuedMarker`(초록 효과)를 영구히 켜기만 하고 별도
연출은 없었음. 이미 존재하던 `FlashMarker()`/`FlashMarkerRoutine()`(공격 대상 지정 피드백용,
`markerFlashCount=3`/`markerFlashInterval=0.3f`)가 정확히 같은 모양(마커를 N회 켰다 껐다 하다가
선택 상태로 복원)이라 그대로 재사용할 수 있음 - 단, 구조 피드백은 공격 지정 피드백과 의도가 달라
별도 필드로 분리해달라는 요청이라 새 필드를 추가함.

사운드는 이 프로젝트의 기존 오디오 시스템(`SoundManager.PlaySFX(SoundClipSet, Vector3)`)을 그대로
따름 - `SoundClipSet`은 `[System.Serializable]` 일반 클래스라 별도 에셋 없이 인스펙터에 클립
리스트/볼륨/피치변동 필드가 바로 나온다(다른 `UnitAudio` 메서드들과 동일 패턴). `SoundManager.
PlayFromPool`이 `set == null`/클립 없음을 이미 안전하게 처리하므로 null 체크 없이 그대로 호출.

## 적용한 변경

### `Assets/Scripts/Unit/UnitController.cs`
- 새 인스펙터 필드 3개 추가 (`rescuedMarker` 근처):
  ```csharp
  [SerializeField] private float rescueFlashInterval = 0.3f;
  [SerializeField] private int rescueFlashCount = 3;
  [SerializeField] private SoundClipSet rescueSfx;
  ```
- `FlashMarkerRoutine()`을 `count`/`interval` 매개변수를 받도록 일반화(기존 `FlashMarker()`는
  `markerFlashCount`/`markerFlashInterval`을 그대로 넘겨 호출 - 동작 변화 없음).
- `Rescue()`: `rescuedMarker.SetActive(true)` 이후 `FlashMarkerRoutine(rescueFlashCount,
  rescueFlashInterval)`을 새로 시작(이미 진행 중인 깜빡임이 있으면 중단 후 재시작, `FlashMarker()`와
  동일한 안전장치)하고, `SoundManager.Instance?.PlaySFX(rescueSfx, transform.position)` 호출.

`unitMarker` 자체를 깜빡이는 방식이라(기존 `FlashMarker()`와 동일), `rescuedMarker`가 이미 켜진
상태에서 마커가 껐다 켜졌다 할 때마다 초록으로 보인다. 깜빡임이 끝나면 기존 로직 그대로 현재 선택
상태에 맞춰 복원됨.

## 검증
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).

## 남은 작업
- 인스펙터에서 `rescueSfx`의 `SoundClipSet.clips`에 실제 클립을 채워 넣어야 소리가 남(현재는 비어있어
  무음 - 코드는 정상 동작).

## 변경된 파일
- `Assets/Scripts/Unit/UnitController.cs`