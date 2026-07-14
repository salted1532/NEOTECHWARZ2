# 0120 - 차량 이동 셰이크 애니메이션 + Animation 폴더 정리 (구현 완료)

## 날짜
2026-07-14

## 요청
"잘 작동하네 이제 차량에 추가할 애니메이션을 넣었으면 좋겠는데 hovebob 스크립트도 이펙트보단
Animation폴더를 만들어서 넣어주고 거기에다가 차량 애니메이션인데 차량 유닛이 이동할때 덜덜덜 떨리는
느낌의 애니메이션을 Dotween을 이용해서 만들어줘"

## 변경 1 - 폴더 정리
`Assets/Scripts/Effects/HoverBob.cs`(+ `.cs.meta`)를 새로 만든 `Assets/Scripts/Animation/` 폴더로
이동. `.meta` 파일을 같이 옮겨서 GUID가 그대로 유지되므로, 이미 프리팹에 붙여둔 `HoverBob` 컴포넌트
참조는 깨지지 않는다.

## 변경 2 - 신규: `Assets/Scripts/Animation/VehicleShake.cs`
`HoverBob`과 동일한 패턴(루트가 아니라 모델 자식 오브젝트에 부착, `UnitController`를
`GetComponentInParent`로 찾아 매 프레임 폴링)으로, `unitController.IsCurrentlyMoving()`이 true인 동안
`DOShakePosition`을 짧은 사이클(기본 0.2초)로 계속 이어붙여서 "덜덜덜" 떨리는 느낌을 낸다.

`SetLoops(-1)`로 무한 반복시키는 대신, 매 사이클을 `fadeOut: true`로 정확히 `basePosition`에 되돌린
뒤 `OnComplete`에서 다음 사이클을 새로 재생하는 방식을 택했다 - 이래야 여러 번 반복해도 누적 오차 없이
항상 같은 기준점에서 흔들린다. 정지하면 0.15초 동안 원래 위치로 부드럽게 복귀.

```csharp
private void PlayShakeCycle()
{
    shakeTween = transform.DOShakePosition(shakeCycleDuration, shakeStrength, vibrato, 90f, false, true)
        .OnComplete(() =>
        {
            if (shaking)
                PlayShakeCycle();
        });
}
```

인스펙터 노출 필드: `Shake Strength`(기본 0.03), `Vibrato`(기본 15), `Shake Cycle Duration`(기본 0.2초).

## 부착 대상 (사용자가 직접, HoverBob과 동일한 방식)
`isAirUnit`처럼 "차량인지" 구분하는 코드 플래그가 없어서, 어떤 유닛이 차량인지는 프리팹에 컴포넌트를
붙이는 사람이 판단해서 정하는 방식으로(=HoverBob과 동일하게 수동 부착) 진행함. `Assets/prefabs/NTA/Unit`
아래 유닛 중 이동하는 지상 유닛 후보:
- `Tier2/Pulsar Tank.prefab` - 궤도 전차, 확실한 차량
- `Tier2/Ranger Infantry Fighting Vehicle.prefab` - 확실한 차량
- `MainBase/Worker Drone.prefab` / `Tier1/Scout Drone.prefab` - 바퀴/궤도로 굴러다니는 드론이면 차량,
  다리로 걷는 형태면 제외 (모델을 봐야 판단 가능 - 사용자 확인 필요)
- `Tier1/Assault Trooper.prefab` - 보병(도보)이라 제외 추천
- `Tier3/Firehawk.prefab`, `Tier3/Guardian Drone.prefab` - 공중유닛(이미 HoverBob 부착 대상)이라 제외

HoverBob과 마찬가지로 각 프리팹의 **모델(비주얼) 자식 오브젝트**에 붙여야 한다(루트 아님).

## 확인 필요 사항
- Worker Drone / Scout Drone을 "차량"으로 볼지 여부(모델 형태에 따라 다름) - 사용자가 직접 확인 후 판단
- `shakeStrength`(0.03)/`vibrato`(15)/`shakeCycleDuration`(0.2초) 기본값이 감으로 괜찮은지 - 인게임에서
  보면서 프리팹별로 조정 가능

## 변경 파일
- `Assets/Scripts/Effects/HoverBob.cs`, `.meta` → `Assets/Scripts/Animation/HoverBob.cs`, `.meta` (이동)
- `Assets/Scripts/Animation/VehicleShake.cs` (신규)
