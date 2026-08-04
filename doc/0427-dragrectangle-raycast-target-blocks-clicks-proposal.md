# 0427. 진짜 원인 발견 및 수정 - 드래그 박스(DragRectangle)가 클릭을 가로챔

- 날짜: 2026-08-04

## 요청 내용

> 버튼이 pressed unpressed에 대해서 디버그 로그 추가해서 테스트를 한번더 진행시켜줘 내가 누를때는 거의 10번 넘게 눌러야 1번 부대 선택 되거든?
> (이어서) 확인좀해줘

## 조사 내용

[[0426-controlgrouppanel-pointer-down-up-exit-log-proposal]]로 추가한 PRESSED/UNPRESSED 로그로
스크립트 클릭(누르고 즉시 뗌, 같은 프레임)을 테스트하니 100% 성공했다. 그런데 이건 "누르고 있는
시간이 0에 가깝다"는 뜻이라, 손으로 직접 누를 때(누르는 동안 실제 시간차 + 미세한 마우스 움직임이
있는 "진짜 클릭")만 실패한다는 사용자 보고와 결정적인 차이가 있었다.

여기서 `UserControl.cs`의 `HandleMouse()`를 다시 보니:
```csharp
if (Input.GetMouseButton(0))
{
    end = Input.mousePosition;
    DrawDragRectangle();
}
```
이 블록은 **마우스가 눌려있는 동안 매 프레임, UI 위에서 누르기 시작했는지 여부와 무관하게** 실행된다.
즉 부대선택 버튼을 누른 채로 손이 1px이라도 떨리면, 그 즉시 `dragRectangle`(드래그 박스 미리보기
이미지)이 `start`(누른 지점)~`end`(현재 지점) 사이의 작은 사각형으로 리사이즈된다.

그리고 `GameManager.prefab`의 `DragRectangle` Image 컴포넌트를 확인해보니 **`Raycast Target`이
켜져 있었다(`m_RaycastTarget: 1`)**. `DragRectangle`은 Canvas의 자식 순서상 부대선택 버튼 행
(`HorizontalLayoutGroup`, 맨 앞)보다 뒤에 있어(= 렌더링 시 그 위에 그려짐) 레이캐스트에서도
우선권을 가진다.

**직접 재현/증명**: 부대선택 버튼 위치에 `DragRectangle`을 6x6px 크기로(진짜 클릭 중 손떨림 정도)
겹쳐두고 그 좌표에 `GraphicRaycaster.Raycast()`를 실제로 실행해봤다.
```
TopHit=DragRectangle
All=[DragRectangle(depth=2) | Text (TMP)(depth=1) | Squadbutton(Clone)(depth=0)]
```
버튼(depth=0, 맨 아래)이 아니라 **DragRectangle(depth=2, 맨 위)이 레이캐스트에서 이긴다.**

Unity UI 클릭 판정 규칙(이전 조사에서 확인): `onClick`은 뗄 때 그 좌표를 다시 레이캐스트해서
**누를 때와 같은 오브젝트여야만** 발동한다. 손으로 눌러서 실제 시간차 + 미세 이동이 생기면
`DragRectangle`이 커지면서 버튼 위를 가로채고, 뗄 때의 레이캐스트가 버튼이 아니라
`DragRectangle`에 맞아버려서 `onClick`이 조용히 씹힌다. `PRESSED`(누를 때는 버튼이 맞음)는
찍히지만 `클릭됨`/`SelectControlGroup` 로그는 안 찍히는 정확한 그림이 나온다.

**이건 부대선택 버튼만의 문제가 아니라, 마우스를 누른 채로 살짝이라도 움직이면 화면의 모든
UI 버튼에 똑같이 영향을 준다** - "10번 눌러야 1번 성공"은 손떨림이 없을 때만 성공했다는 뜻과
정확히 일치한다.

## 코드/에셋 변경 (적용됨)

`DragRectangle`은 순수 시각적 미리보기용 박스이고, 그 자체가 클릭 대상이 될 이유가 전혀 없다.
`Raycast Target`을 꺼서 애초에 어떤 레이캐스트도 가로채지 못하게 하면, 부대선택 버튼뿐 아니라
게임 전체의 모든 버튼에 대해 이 문제가 한 번에 해결된다 (진짜 근본 원인 수정, 코드 변경 없음).

**기존 설정** (`Assets/prefabs/Game/GameManager.prefab`, `DragRectangle`의 `Image` 컴포넌트):
```yaml
  m_RaycastTarget: 1
```

**변경 설정**:
```yaml
  m_RaycastTarget: 0
```

(에디터에서는: `GameManager` 프리팹 → `Canvas/DragRectangle` → Image 컴포넌트 → `Raycast Target`
체크 해제, 와 동일한 변경)

## 요약 / 영향받는 파일

- **진짜 원인**: 드래그 박스(`DragRectangle`)의 Image가 `Raycast Target = true`라서, 마우스를
  누른 채로(버튼 포함 UI 위에서도) 아주 살짝만 움직여도 그 즉시 버튼 위를 덮는 얇은 사각형이
  생기고, 뗄 때의 클릭 판정용 레이캐스트가 버튼 대신 이 드래그 박스에 맞아 클릭이 무효화된다.
  실제로 좌표를 겹쳐 레이캐스트해서 확인함(`DragRectangle`이 `Squadbutton`을 이김).
- 지금까지의 0423(재배치 타이밍)/0425(IsBuildMode 게이트) 가설은 부수적으로 존재하는 훨씬 작은
  리스크였고, "10번 중 9번 실패"를 설명하는 주된 원인은 이것이다.
- 수정: `DragRectangle`의 `Raycast Target`을 끈다. 코드 변경 없음, 프리팹 설정 1개 변경.
- 부대선택 버튼뿐 아니라 화면의 모든 버튼(생산 슬롯, Option 등)에 같은 방식으로 영향을 주고 있었을
  가능성이 높다 - 이번 수정으로 전체적으로 클릭 안정성이 좋아질 것으로 예상.
- 영향받는 파일: `Assets/prefabs/Game/GameManager.prefab` (적용 완료)

## 적용 후 재검증

같은 방식으로 6x6px 드래그 박스를 부대선택 버튼 위에 겹쳐두고 다시 레이캐스트해보니:
```
RaycastTarget=False  TopHit=Text (TMP)  All=[Text (TMP)(depth=1) | Squadbutton(Clone)(depth=0)]
```
`DragRectangle`이 더 이상 레이캐스트 결과에 아예 나타나지 않는다 - 버튼(과 그 라벨 텍스트, 버튼의
자식이라 정상적으로 버튼 클릭으로 처리됨)만 남았다. 수정이 의도대로 동작함을 확인.
