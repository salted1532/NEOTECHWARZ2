# 0437. ResourceNode 게임 시작 시 랜덤 Y축 회전 (제안)

**날짜:** 2026-08-05

## 요청 내용
> 게임 시작시 자원이 자신의 y축 회전을 랜덤한 값으로 가지고 시작했으면 좋겠어

## 현재 구조

`Assets/Scripts/Resource/ResourceNode.cs`의 `Awake()`는 `initialAmount = remainingAmount;`만 하고
회전은 건드리지 않음 — 씬/프리팹에 배치된 회전값 그대로 시작함. 콜라이더는 `CapsuleCollider`(루트,
Y축 정렬)라서 Y축 회전은 콜라이더 모양에 영향 없음(캡슐이 Y축 기준 대칭이라 안전).

## 제안하는 변경

`Assets/Scripts/Resource/ResourceNode.cs`의 `Awake()`에 한 줄 추가:
```csharp
transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
```
기존 회전(X/Z 기울기 등)은 그대로 두고 월드 Y축 기준으로만 랜덤 스핀을 더함. 씬에 배치된 모든
`ResourceNode`(Ore/Gas 공통)에 적용됨 — 노드별 처리라 스포너 스크립트는 따로 없음.

## 구현 (승인 후 적용됨)

**Before:**
```csharp
private void Awake()
{
    initialAmount = remainingAmount;
}
```

**After:**
```csharp
private void Awake()
{
    initialAmount = remainingAmount;
    transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
}
```

## 검증

- `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`, `WarningCount: 34`(전부 기존에도 있던
  무관한 경고, 새로 추가된 경고 없음).

## 영향받는 파일

- `Assets/Scripts/Resource/ResourceNode.cs`
