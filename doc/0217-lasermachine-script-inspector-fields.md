# 0217 — LaserMachine 스크립트 인스펙터 노출 필드 분석

## 질문
"Laser Machine 스크립트를 읽고 분석해서 이 스크립트 컴포넌트의 인스펙서상 조절할수 있는게 어떤것들인지 알려줘"

## 조사 내용
`Assets/AssetFolder/LaserMachine/Core/Scripts/` 안의 3개 스크립트를 읽음:
- `LaserMachine.cs` — 씬 오브젝트에 붙는 `MonoBehaviour` (실제 레이저를 생성/제어)
- `LaserData.cs` — `[CreateAssetMenu]`로 만드는 `ScriptableObject` (레이저 프리셋 에셋)
- `LaserProperties.cs` — `[System.Serializable]` 순수 데이터 클래스, 위 두 곳에 공통으로 내장(embed)됨

읽기 전용 분석 작업이라 코드/에셋 변경 없음.

## 답변 (사용자에게 전달한 내용)
아래 "답변" 섹션을 그대로 채팅에 전달함.
