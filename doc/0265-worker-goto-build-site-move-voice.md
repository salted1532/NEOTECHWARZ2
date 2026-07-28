# 0265 - 건설 위치로 이동하는 일꾼에게 이동 명령 음성 재생

**날짜:** 2026-07-28

## 요청 내용

> 건설모드에서 일꾼이 건물을 지으러 갈때는 명령음성중 하나 나오면 될듯

건설 모드에서 건물을 배치하면 일꾼이 그 위치까지 걸어가는데, 이때도 기존 명령 음성 카테고리
(선택/이동/공격명령) 중 하나가 재생되면 좋겠다는 요청. "건설 위치로 이동"은 실질적으로 이동 명령과
같은 성격이라 별도 카테고리를 새로 만들지 않고 기존 `moveVoice`를 그대로 재사용하기로 함.

## 코드 변경

### `Assets/Scripts/BuildSystem/PlacementSystem.cs`

`PlaceStructure()`에서 자원/인구 체크를 통과해 실제로 일꾼을 건설 위치로 보내는 지점
(`worker.GoBuild(...)` 호출 직전)에 이동 명령 음성 재생을 추가.

Before:
```csharp
        // 클릭한 자리에 일꾼이 도착할 때까지 남아있을 고정 고스트를 생성
        GameObject ghost = preview.SpawnConstructionGhost(data.Prefab, spawnPos);

        worker.GoBuild(
```

After:
```csharp
        // 클릭한 자리에 일꾼이 도착할 때까지 남아있을 고정 고스트를 생성
        GameObject ghost = preview.SpawnConstructionGhost(data.Prefab, spawnPos);

        worker.GetComponent<UnitAudio>()?.PlayMoveVoice(); // 건설 위치로 이동을 시작하므로 이동 명령 음성 재생

        worker.GoBuild(
```

`UnitAudio.PlayMoveVoice()`는 이미 doc/0262~0264에서 만든 `SoundManager.PlayOrderVoice` 경로를 그대로
타므로, 이동 중이던 다른 명령 음성이 재생 중이었다면 같은 규칙(같은 유닛 종류면 안 끊김, 다른 종류
선택 시에만 끊김)이 그대로 적용된다 - 추가 분기 없이 기존 인프라를 그대로 재사용.

## 요약/영향받는 파일

- `Assets/Scripts/BuildSystem/PlacementSystem.cs`: 건설 위치로 일꾼을 보내는 시점에
  `UnitAudio.PlayMoveVoice()` 호출 한 줄 추가.
- 새 SoundClipSet 카테고리 추가 없음(`UnitSoundBankSO.moveVoice`를 건설 이동에도 재사용).
