# 0659 - 장식 오브젝트 게임 시작 시 랜덤 Y 회전 스크립트

## 요청
나무/풀 같은 장식 오브젝트가 게임 시작 시 랜덤한 Y축 회전(0~360도)을 갖도록 하는 간단한 스크립트.

## 참고
`ResourceNode.cs`(`Assets/Scripts/Resource/ResourceNode.cs:81`)의 `Awake()`에 이미 동일한 목적의 한 줄이 있음:
```csharp
transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
```
이 프로젝트의 기존 컨벤션 그대로, 별도 컴포넌트로 분리해 나무/풀 등 아무 프리팹에나 붙일 수 있게 만듦.

## 구현
`Assets/Scripts/Utility/RandomYRotation.cs` 신규 생성:
```csharp
using UnityEngine;

public class RandomYRotation : MonoBehaviour
{
    private void Awake()
    {
        transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
    }
}
```
사용법: 나무/풀 프리팹에 이 컴포넌트를 추가하면 끝. Inspector 노출 필드 없음 (요청이 "0~360 고정 범위"라 커스터마이즈 옵션 불필요 - 나중에 범위를 조절하고 싶다면 그때 `[SerializeField] float minY, maxY` 추가).

## 결과
컴파일 확인: 에러 0, 기존 경고(무관)만 유지.
