# 0310. 메인화면 파이어호크 대각선 순찰 비행

날짜: 2026-07-30

## 결과

승인됨. `Assets/Scripts/UI/MainMenuFlyby.cs` 생성 완료(아래 제안 코드 그대로).

## 요청 내용

> 메인화면을 움직이는 오브젝트들이 좀 있도록 하려고 하는데 현재 파이어호크들을 화면에서
> 날라다니도록 하려고 하는데 x-100 z-100에서 x 100 z 100으로 대각선으로 이동하도록 하는 코드
> 만들어줘 그리고 이동 완료하면 다시 -100 -100으로 이동하는데 이건 텔레포트 하고 약 10~20초
> 랜덤하게 기다렸다가 다시 날라가도록 하는 코드 만들어줘

## 조사 내용

- `Firehawk` 프리팹은 `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab`에 존재함(유닛 프리팹).
- `Assets/Scenes/MainScene.unity`를 확인했는데, 현재 씬에는 `Firehawk` 프리팹 인스턴스가 **아직
  배치되어 있지 않음**(prefab guid `76fc3c92bbeec1c46a37fc8722e51676` 기준으로 씬 파일에 참조
  0건). 즉 오브젝트 배치는 사람이 에디터에서 직접 하고, 이번 스크립트는 그 오브젝트에 붙일 이동
  로직만 작성하는 것으로 진행.
- 프로젝트에 이동/순찰 관련 기존 스크립트가 없음(`*move*`, `*patrol*` 검색 결과 없음) - 새로 작성.
- 기존 컨벤션(`MainMenuController.cs` 등)에 따라 이 스크립트도 `Assets/Scripts/UI/` 아래에 두는
  게 자연스러움(메인화면 전용 연출 스크립트이기 때문).
- 요청 사양 정리:
  1. 시작점 `(x=-100, y=?, z=-100)` → 도착점 `(x=100, y=?, z=100)`으로 대각선 이동
  2. 도착하면 시작점으로 **순간이동(텔레포트)**
  3. 10~20초 사이 랜덤 대기
  4. 다시 1번부터 반복
  - y값은 요청에 명시되지 않아, 오브젝트를 씬에 배치할 때의 초기 y(높이)를 그대로 유지하도록
    처리(비행 고도가 바뀌지 않게).

## 제안 코드 (신규 파일)

### `Assets/Scripts/UI/MainMenuFlyby.cs` (신규)

메인화면에 배치된 `Firehawk` 등 비행 오브젝트에 직접 부착. 시작/도착 지점과 속도, 대기 시간
범위는 인스펙터에서 조절 가능.

```csharp
using System.Collections;
using UnityEngine;

// 메인화면 배경 연출용: 대각선으로 날아간 뒤 시작점으로 텔레포트, 랜덤 대기 후 반복한다.
public class MainMenuFlyby : MonoBehaviour
{
    [SerializeField] private float startX = -100f;
    [SerializeField] private float startZ = -100f;
    [SerializeField] private float endX = 100f;
    [SerializeField] private float endZ = 100f;
    [SerializeField] private float speed = 20f;
    [SerializeField] private float minWaitSeconds = 10f;
    [SerializeField] private float maxWaitSeconds = 20f;

    private Vector3 startPoint;
    private Vector3 endPoint;

    private void OnEnable()
    {
        startPoint = new Vector3(startX, transform.position.y, startZ);
        endPoint = new Vector3(endX, transform.position.y, endZ);
        transform.position = startPoint;
        StartCoroutine(FlyLoop());
    }

    private IEnumerator FlyLoop()
    {
        while (true)
        {
            while (transform.position != endPoint)
            {
                transform.position = Vector3.MoveTowards(transform.position, endPoint, speed * Time.deltaTime);
                yield return null;
            }

            transform.position = startPoint; // 텔레포트
            yield return new WaitForSeconds(Random.Range(minWaitSeconds, maxWaitSeconds));
        }
    }
}
```

- `skipped`: 이동 방향으로 회전(비행체 머리 방향 맞추기), 여러 대가 동시에 배치될 때 경로/시작
  시간을 어긋나게 하는 기능(diversify) - 요청에 없었음. 필요하면 말씀해주세요(예: 회전은
  `transform.forward = (endPoint-startPoint).normalized`로 한 줄 추가 가능).

## 필요한 씬 작업 (코드 외)

1. `Firehawk` 프리팹 인스턴스를 `MainScene`에 배치(이미 배치되어 있다면 생략).
2. 배치한 오브젝트에 `MainMenuFlyby` 컴포넌트를 붙임.
3. 여러 마리를 띄우고 싶다면 오브젝트마다 컴포넌트를 각각 붙이면 됨(각자 독립적으로 랜덤
   대기하므로 자연스럽게 시간차가 생김). 단, 모두 같은 시작점(-100,-100)에서 시작하므로 동시에
   출발하면 겹쳐 보일 수 있음 - 필요하면 초기 대기 시간에 랜덤 오프셋을 주는 것도 가능(요청 시
   추가).

## 영향받는 파일

- `Assets/Scripts/UI/MainMenuFlyby.cs` (신규)

## 다음 단계

1. 위 스크립트를 이대로 생성해도 될지
2. y값(고도) 처리 방식(배치 시점의 y를 그대로 유지)이 괜찮은지, 아니면 y도 직접 지정하고 싶은지

확인 부탁드립니다.
