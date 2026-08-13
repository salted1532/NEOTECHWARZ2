# 0541 - EnemyAIDirector 인스펙터에 현재 보유 건물/유닛 리스트 노출 설계안 → 구현 완료

## 날짜
2026-08-13

## 요청 내용
"EnemyAIDirector에서 현재 Faction의 건물리스트, 유닛리스트 어떤유닛,건물이 현재 가지고 있는지 볼수
있도록 인스펙터에 나오도록 해줘"

이 문서는 제안일 뿐, 아직 코드 수정 안 함.

## 조사 - 지금 EnemyAIDirector가 갖고 있는 "건물/유닛" 관련 데이터
| 필드 | 이미 인스펙터에 보이는가 | 내용 |
|---|---|---|
| `homeBuildings` | 예 (기존 `[SerializeField]`) | 이 director가 방어 트리거로 삼는 건물 목록 - 미션 제작자가 씬에서 직접 지정, 정적(런타임에 늘거나 줄지 않음) |
| `garrison` | **아니오** (`private readonly List<EnemyUnitController>`) | 공격 웨이브용 병력 풀 - 이 director가 스폰한 유닛(원정 나갔어도 죽기 전까진 포함) |
| `raidGarrison` | **아니오** (`private readonly List<EnemyUnitController>`) | 점령지 탈환 별동대용 병력 풀(doc/0538) - garrison과 별개 |

건물 쪽은 이 프로젝트에 "적 건물을 런타임에 새로 짓는" 시스템이 없음(그렙 결과 없음) - 미션 시작 시
씬에 미리 배치된 게 전부. 따라서 "건물리스트"는 이미 있는 `homeBuildings`가 사실상 전부이고 이미
인스펙터에 보임 - 추가로 할 일 없음.

유닛 쪽은 `garrison`/`raidGarrison`이 실제로 "이 director가 현재 보유한 유닛" 그 자체인데
`readonly` private라 인스펙터에 안 보임(Unity는 `readonly` 필드를 직렬화하지 않음).

## 설계안 - `garrison`/`raidGarrison`을 `[SerializeField]`로 노출
```csharp
[Header("<디버그> 현재 보유 병력 (런타임 전용 - Play 모드에서만 채워짐)")]
[SerializeField] private List<EnemyUnitController> garrison = new List<EnemyUnitController>();
[SerializeField] private List<EnemyUnitController> raidGarrison = new List<EnemyUnitController>();
```
`readonly` 제거 + `[SerializeField]` 추가만 하면 됨 - 로직(Add/Remove/RemoveAll)은 전부 그대로,
`readonly`는 필드 재할당만 막던 것이라 리스트 내용 조작에는 원래 영향 없었음. Play 모드에서 해당
GameObject를 선택하면 기본 인스펙터가 리스트를 그대로 보여줌(유닛 오브젝트 레퍼런스 - 클릭하면 어떤
유닛인지, 몇 개 있는지 바로 확인 가능).

## 옵션 - 얼마나 보기 좋게 만들지
1. **(권장, 최소 변경)** 위처럼 `readonly` 제거 + `[SerializeField]`만 추가. 기본 Unity 리스트
   인스펙터로 오브젝트 레퍼런스 배열이 보임 - 코드 두 줄 수정, 커스텀 에디터 없음.
2. **(추가 작업)** 커스텀 에디터(`EnemyAIDirectorEditor : Editor`)를 만들어 "unitID x count" 형태로
   집계해서 보여줌(예: "Cyborg Soldier x8, Railgunner x3"). 읽기는 더 편하지만 별도 에디터 스크립트
   파일이 늘어나고, 유닛 이름을 얻으려면 `UnitData`를 참조해야 함.

## 참고
`homeBuildings`는 별도 조치 없음(이미 인스펙터에 있음).

## 결정 사항 (2026-08-13, 사용자 확인 완료)
옵션 1(최소 변경, readonly 제거 + `[SerializeField]`)로 진행.

## 영향받는 파일 (구현 완료, 2026-08-13)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`

## 코드 변경

### 기존 코드
```csharp
// 이 director가 스폰한 유닛 전체(...). 공격 웨이브 전용 풀 - 점령지 탈환 별동대는 raidGarrison이라는
// 별도 풀을 쓴다(doc/0538, 두 시스템이 같은 인원을 놓고 경쟁하지 않도록).
private readonly List<EnemyUnitController> garrison = new List<EnemyUnitController>();

// 점령지 탈환 별동대 전용 병력 풀(doc/0538) - garrison과 완전히 분리, raidSquadComposition으로 유지.
private readonly List<EnemyUnitController> raidGarrison = new List<EnemyUnitController>();
```

### 변경 코드
```csharp
[Header("<디버그> 현재 보유 병력 (런타임 전용 - Play 모드에서만 채워짐)")]
[SerializeField] private List<EnemyUnitController> garrison = new List<EnemyUnitController>();

[SerializeField] private List<EnemyUnitController> raidGarrison = new List<EnemyUnitController>();
```
`readonly` 제거는 리스트 내용 조작(Add/Remove/RemoveAll) 로직에 영향 없음 - `readonly`는 필드
재할당만 막던 것이라 나머지 코드는 전부 그대로.

`homeBuildings`(건물리스트)는 기존 `[SerializeField]`로 이미 인스펙터에 보이고 있어 변경 없음.

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개, 경고 0개.

## 사용 방법
Play 모드에서 `EnemyAIDirector`가 붙은 GameObject를 선택하면 인스펙터의 "<디버그> 현재 보유 병력"
아래 `Garrison`(공격 웨이브용)과 `Raid Garrison`(별동대용) 리스트에 현재 보유한 `EnemyUnitController`
오브젝트 레퍼런스가 그대로 뜬다 - 각 항목을 클릭하면 어떤 유닛인지, 리스트 크기로 총 몇 마리인지 바로
확인 가능. 건물은 별도 조치 없이 기존 `Home Buildings` 필드로 확인.
