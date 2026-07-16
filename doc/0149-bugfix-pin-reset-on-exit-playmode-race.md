# 0149. 버그 수정: Play 모드 빠져나올 때 핀이 원점으로 리셋되는 레이스 컨디션

## 날짜
2026-07-16

## 요청
`TestScene`에서 `doc/0134`/`0138`에서 고쳤던 "Play 모드 껐다 켜면 핀이 다시 생성되는" 버그가 그대로 재발. `LineRenderer`의 6개 위치값이 전부 같은 좌표로 들어가 있는 것도 확인됨(핀 6개가 전부 겹쳐 있다는 뜻).

## 원인
`doc/0134`에서 넣은 가드가 `if (Application.isPlaying) return;` 하나뿐이었다. 이건 Play 모드 **"안에 있을 때"**만 막아준다 — Play를 **빠져나오는 도중**(Unity가 씬을 에디터 상태로 복원하는 과정)에도 `OnValidate`가 한 번 더 불리는데, 이 시점엔 이미 `Application.isPlaying`이 `false`로 바뀐 뒤라 가드를 그냥 통과해버린다. 마침 이 복원 도중엔 `pinPoints` 리스트의 `Transform` 참조가 일시적으로 덜 복원된 상태일 수 있어, `SyncPinPoints()`가 이걸 "빈 슬롯"으로 오판 → 이미 벌려놨던 핀들 자리에 전부 새 핀(위치 `Vector3.zero`, 즉 부모 오브젝트와 같은 좌표)을 만들어 덮어써버렸다. 그 결과가 지금 확인된 "6개 핀이 전부 같은 좌표"다.

## 수정

### 기존 코드
```csharp
private void OnValidate()
{
    if (Application.isPlaying) return; // Play 모드 중/전환 시점엔 씬 편집용 동기화를 돌리지 않는다

    EditorApplication.delayCall += SyncPinPoints;
}
```

### 변경 코드
```csharp
private void OnValidate()
{
    // isPlaying만 검사하면 Play 모드를 "빠져나오는 도중"(이미 isPlaying=false로 바뀌었지만 씬 복원이
    // 끝나기 전)에 걸린 OnValidate를 못 막는다 - isPlayingOrWillChangePlaymode는 그 전환 구간 전체를
    // true로 유지해줘서 이 틈을 막는다.
    if (EditorApplication.isPlayingOrWillChangePlaymode) return;

    // OnValidate 안에서 바로 씬을 건드리면 경고/에러가 날 수 있어 다음 에디터 틱으로 미루고,
    // 그 시점에도 다시 한번 전환 여부를 확인한다(그 사이에 Play가 눌렸을 수 있음).
    EditorApplication.delayCall += () =>
    {
        if (this == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        SyncPinPoints();
    };
}
```
`EditorApplication.isPlayingOrWillChangePlaymode`는 `isPlaying`이 true이거나 Play 모드 진입/종료가 예정·진행 중인 전체 구간에서 true를 유지하는 유니티 표준 플래그라, `isPlaying` 단독 검사보다 훨씬 넓게(전환 전/중/후 복원 시점까지) 막아준다. 예약된 `delayCall` 콜백 안에서도 다시 한번 확인해서, 그 사이 Play 버튼이 눌린 경우까지 방어했다.

## 영향받는 파일
- `Assets/Scripts/CaptureSystem/TerritoryZone.cs` (수정)

## ⚠️ 남은 작업 (씬 편집, 코드 아님)
`TestScene`의 `Capture_Point` 인스턴스는 이미 이 버그 때문에 핀 6개가 전부 같은 좌표(부모 위치)로 뭉쳐 있는 상태다. 코드 수정만으로는 이미 망가진 데이터는 안 고쳐지므로, 에디터에서 `PinPoint_0`~`5`를 다시 하나씩 원하는 위치로 드래그해서 다각형을 새로 그려야 한다.

## 확인/테스트 필요
핀을 다시 벌려놓은 뒤 Play 버튼을 여러 번 껐다 켜보면서 핀 위치가 유지되는지(더 이상 원점으로 안 돌아가는지) 확인 필요.

## 비고
[[confirm_before_implementing]] — 재발한 기존 승인 버그의 원인 보강 수정이라 별도 확인 없이 바로 반영. `doc/0134`, `doc/0138`, `doc/0146`(재적용)의 연장선.
