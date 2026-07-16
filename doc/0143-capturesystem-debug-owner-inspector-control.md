# 0143. CaptureSystem 인스펙터에서 점령 상태 직접 조종 (디버그용)

## 날짜
2026-07-16

## 요청
`CaptureSystem`에서 점령 상태(중립/아군/적)를 인스펙터에서 직접 조종할 수 있게 해달라는 요청. 두 방식(드롭다운 필드 vs 컨텍스트 메뉴 버튼) 중 드롭다운 필드로 확인받음.

## 변경 파일
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs` (수정)

## 코드 변경

### 추가된 필드
```csharp
[Header("디버그 - 인스펙터에서 점령 상태 직접 조종 (테스트용)")]
[SerializeField] private CaptureOwner debugOwner = CaptureOwner.Neutral;
#if UNITY_EDITOR
private CaptureOwner lastSyncedDebugOwner = CaptureOwner.Neutral;
#endif
```

### `ApplyEffect()` 끝에 동기화 추가
```csharp
#if UNITY_EDITOR
        // 실제 상태(게임 진행에 의한 점령 등)가 바뀔 때마다 디버그 필드도 같이 맞춰서,
        // 인스펙터에 실제 상태가 항상 그대로 보이게 한다.
        debugOwner = owner;
        lastSyncedDebugOwner = owner;
#endif
```

### 신규 `OnValidate()`
```csharp
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (debugOwner == lastSyncedDebugOwner) return; // 실제 상태 동기화로 인한 변경이면 무시

        CaptureOwner forced = debugOwner;

        EditorApplication.delayCall += () =>
        {
            if (this == null) return;

            CurrentOwner = forced;
            captureTimer = forced == CaptureOwner.Neutral ? 0f : captureDuration;
            ApplyEffect(forced); // debugOwner/lastSyncedDebugOwner도 여기서 함께 동기화됨
            SetCaptureBarVisible(false);
        };
    }
#endif
```

## 동작 요약
- 인스펙터의 `Debug Owner` 드롭다운(Neutral/Ally/Enemy)을 바꾸면 즉시 그 상태로 강제 전환됨 — 이펙트(흰/초록/빨강), `TerritoryZone.Owner`(영토 외곽선 색), `CurrentOwner`가 전부 같이 바뀜.
- 반대로 실제 게임 진행(유닛이 30초간 머물러 점령 완료 등)으로 상태가 바뀌면 `ApplyEffect()`가 `debugOwner`도 같이 갱신하므로, 인스펙터에 항상 실제 상태가 그대로 보임 — 드롭다운이 실제 상태와 따로 노는 일이 없음.
- Play 모드에서도 동작(순수 필드 대입/`SetActive` 호출만 있어 `TerritoryZone`의 핀 생성 로직과 달리 씬 오브젝트 생성/삭제가 없음 — Play 모드 중 안전하게 사용 가능).

## 확인/테스트 필요
유니티 에디터에서 `Debug Owner`를 Ally/Enemy/Neutral로 바꿔가며 이펙트와 영토 외곽선 색이 즉시 반영되는지, 실제 점령 완료 후에도 드롭다운이 자동으로 Ally로 따라오는지 확인 필요.

## 비고
[[confirm_before_implementing]] — 드롭다운 필드 vs 컨텍스트 메뉴 버튼 중 방식을 사용자에게 확인(드롭다운 선택) 후 반영함.
