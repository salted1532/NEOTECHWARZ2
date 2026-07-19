# 0176 - 점령지 시야가 반투명하게 보이는 문제 - 원인 및 타이밍 훅 수정

## 요청

"근데 지금이건 FogRevealerAgent에서 작동하는거 처럼 뭔가 계속해서 작동하는게 아니라 한번 밝혀진
지형에서 다시 보고있지 않아서 반투명정도로 나오는 상태로 보이네 왜그럴까" — [[territory-permanent-vision-design]](0175)로
구현한 점령지 강제 시야가, `FogRevealerAgent`처럼 또렷하게 유지되지 않고 흐릿(반투명)하게 보이는 이유를
질문. 사용자가 원인 설명 후 "그래 수정해도 괜찮아"로 승인.

## 원인

`csFogWar`의 실제 안개 갱신 순서(매 갱신 주기, `UpdateFogField()` 내부):

1. `shadowcaster.ResetTileVisibility()` — 전체 타일 리셋
2. 각 `FogRevealer` 시야만큼 `Revealed` 마킹
3. `UpdateFogPlaneTextureTarget()` — **바로 이 시점의 데이터를 텍스처로 구움**

`TerritoryFogReveal`은 `LateUpdate()`에서 동작하는데, Unity 실행 순서상 모든 오브젝트의 `Update()`가
끝난 뒤에야 `LateUpdate()`가 실행된다. 따라서 매 프레임:

- `csFogWar.Update()`가 먼저 돌면서 점령지 타일이 아직 안개인 상태로 텍스처가 구워짐
- `TerritoryFogReveal.LateUpdate()`가 뒤늦게 그 타일을 `Revealed`로 고침 → 이미 그 프레임 텍스처엔
  반영 안 됨
- 다음 주기엔 `ResetTileVisibility()`가 또 먼저 실행되며 방금 고친 값도 리셋됨 → 또 늦게 고침 → 반복

즉 데이터상으로는 계속 `Revealed`를 쓰지만, 실제로 텍스처가 구워지는 순간에는 항상 리셋 직후(고쳐지기
전) 값만 캡처되어, 고쳐진 값이 텍스처에 반영되는 타이밍을 계속 놓친다. `FogRevealer`는 3단계가 끝나기
*전에*(2번 단계에서) 반영되므로 항상 그 프레임에 바로 캡처되어 또렷하게 나오는 것과 대조된다.

## 수정 방향

`FogRevealer`와 동일한 지점(리셋 이후, 텍스처를 굽기 전)에 점령지 반영이 끼어들도록, `csFogWar.cs`
(에셋)에 훅 이벤트를 하나 추가한다. [[territory-permanent-vision-design]](0175)에서는 "에셋은 안
건드린다"는 원칙을 세웠지만, 이 타이밍 문제는 텍스처를 굽기 직전 지점에 개입해야만 풀리는 구조라
에셋에 최소한의 훅만 추가하는 쪽으로 방향을 바꾼다(0173에서 grid cap을 이미 수정한 전례와 동일한
수준의 최소 개입).

## 계획된 코드 변경

### 1. `Assets/AssetFolder/AOSFogWar/csFogWar.cs`

Before:
```csharp
        private void UpdateFogField()
        {
            shadowcaster.ResetTileVisibility();

            foreach (FogRevealer fogRevealer in fogRevealers)
            {
                fogRevealer.GetCurrentLevelCoordinates(this);

                shadowcaster.ProcessLevelData(
                    fogRevealer._CurrentLevelCoordinates,
                    Mathf.RoundToInt(fogRevealer._SightRange / unitScale));
            }

            UpdateFogPlaneTextureTarget();
        }
```

After:
```csharp
        // FogRevealer 반영이 끝난 직후, 텍스처를 굽기 직전 지점에서 다른 시스템(예: 점령지 강제 시야)이
        // fogField를 추가로 손볼 수 있게 열어주는 훅. LateUpdate 등 이후 타이밍에서 반영하면 이번 주기의
        // 텍스처 캡처를 놓치게 되므로, 반드시 이 시점에 반영해야 한다.
        public event Action OnBeforeFogTextureUpdate;

        private void UpdateFogField()
        {
            shadowcaster.ResetTileVisibility();

            foreach (FogRevealer fogRevealer in fogRevealers)
            {
                fogRevealer.GetCurrentLevelCoordinates(this);

                shadowcaster.ProcessLevelData(
                    fogRevealer._CurrentLevelCoordinates,
                    Mathf.RoundToInt(fogRevealer._SightRange / unitScale));
            }

            OnBeforeFogTextureUpdate?.Invoke();

            UpdateFogPlaneTextureTarget();
        }
```

(`Action`은 이미 파일 상단의 `using System;`으로 사용 가능 — 새 using 불필요.)

### 2. `Assets/Scripts/FogOfWar/TerritoryFogReveal.cs`

Before:
```csharp
public class TerritoryFogReveal : MonoBehaviour
{
    private csFogWar fogWar;

    private void Start()
    {
        fogWar = FindFirstObjectByType<csFogWar>();

        if (fogWar == null)
            Debug.LogWarning($"{name}: csFogWar를 씬에서 찾지 못해 영토 시야를 반영하지 못합니다.", this);
    }

    // csFogWar.Update()(안개 갱신)가 끝난 뒤에 실행되도록 보장하기 위해 LateUpdate 사용
    private void LateUpdate()
    {
        if (fogWar == null) return;

        foreach (TerritoryZone zone in TerritoryManager.Zones)
        {
            if (zone == null || zone.Owner != CaptureOwner.Ally) continue;

            RevealZone(zone);
        }
    }

    private void RevealZone(TerritoryZone zone) { ... }
}
```

After:
```csharp
public class TerritoryFogReveal : MonoBehaviour
{
    private csFogWar fogWar;

    private void Start()
    {
        fogWar = FindFirstObjectByType<csFogWar>();

        if (fogWar == null)
        {
            Debug.LogWarning($"{name}: csFogWar를 씬에서 찾지 못해 영토 시야를 반영하지 못합니다.", this);
            return;
        }

        // FogRevealer가 반영되는 시점과 동일한 타이밍(텍스처를 굽기 직전)에 걸어야
        // 그 프레임의 텍스처에 바로 반영된다. LateUpdate로는 항상 한 프레임 늦어 반투명하게 보였었다.
        fogWar.OnBeforeFogTextureUpdate += RevealAlliedZones;
    }

    private void OnDestroy()
    {
        if (fogWar != null)
            fogWar.OnBeforeFogTextureUpdate -= RevealAlliedZones;
    }

    private void RevealAlliedZones()
    {
        foreach (TerritoryZone zone in TerritoryManager.Zones)
        {
            if (zone == null || zone.Owner != CaptureOwner.Ally) continue;

            RevealZone(zone);
        }
    }

    private void RevealZone(TerritoryZone zone) { ... /* 기존과 동일 */ }
}
```

`RevealZone()` 내부 로직(바운딩 박스 계산 → 그리드 셀 순회 → `zone.Contains()` 확인 → `fogField`
강제 `Revealed`)은 그대로 유지 — 호출 시점만 `LateUpdate()`(매 프레임 폴링)에서
`OnBeforeFogTextureUpdate` 이벤트(실제 안개 갱신 주기에 맞춰서만) 구독으로 바뀐다.

## 확인 필요

이대로 진행합니다(사용자 승인 완료 - "그래 수정해도 괜찮아").
