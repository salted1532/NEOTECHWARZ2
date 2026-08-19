# 0617 - 브리핑 인물 로스터화 + 미션3 발언자 3인 제한 (제안)

## 요청
1. 브리핑룸에 나오는 인물들을 정리
2. 인물별 이미지를 넣을 수 있게 하고 연결
3. `Docs/Campaign.md` 확인 후 각 브리핑당 최대 3인물만 이야기하도록 줄이기

## 조사
`Docs/Campaign.md`의 브리핑 대사에서 미션별 등장(발언) 인물 수를 셈:

| 미션 | 발언 인물 | 인원수 |
|---|---|---|
| 0 Boot Camp | 아드리안, 부관 | 2 |
| 1 Border Conflict | 부관, 셀레나, 아드리안 | 3 |
| sub1 Flanking Strike | 아드리안, 별동대장, 부관 | 3 |
| 2 Unknown Signal | 부관, 아드리안, 정찰병 | 3 |
| sub2 Wreckage Search | 정찰병, 아드리안, 별동대장 | 3 |
| **3 Invasion** | 부관, 정찰병, **병사**, 아드리안 | **4 (초과)** |
| sub3 Search & Rescue | 부관, 아드리안, 구조대장 | 3 |
| 4 United Front | 셀레나, 아드리안, 부관 | 3 |
| sub4 Last Line of Defense | 셀레나, 아드리안, 방어부대장 | 3 |
| 5 Final Offensive | 부관, 셀레나, 아드리안 | 3 |

미션 3만 4인 초과. 전체 캠페인에서 반복 등장하는 고유 인물은 7명 (아드리안, 셀레나, 부관, 정찰병, 별동대장, 구조대장, 방어부대장) - "병사"는 미션 3에만 1줄 등장.

## 제안

### 1. Campaign.md 미션3 대사 수정
"병사"의 대사를 "정찰병" 대사에 합쳐 발언자를 3명(부관/정찰병/아드리안)으로 줄임:
```diff
 > **부관**: "OC의 주력 식민 행성 전역에서 구조 요청이 들어오고 있습니다."
 >
-> **정찰병**: "확인 결과... 공격한 것은 우리 병력이 아닙니다."
->
-> **병사**: "저건... 인간이 아니다."
+> **정찰병**: "확인 결과... 공격한 것은 우리 병력이 아닙니다. 저건... 인간이 아닙니다."
 >
 > **부관**: "미확인 외계 생명체가 OC 기지를 공격하고 있습니다. 피해 규모는 상상을 초월합니다."
 >
 > **아드리안**: "생존자를 구조한다. 지금은 인간끼리 싸울 때가 아니다."
```

### 2. 인물 로스터 (BriefingCharacter)
현재 `BriefingEntry`는 미션마다 `portrait1/2/3` Sprite를 직접 들고 있어서, 같은 인물이 여러 미션에 반복 등장할 때마다 같은 이미지를 매번 다시 할당해야 함. 대신 `BriefingRoomController`에 인물 로스터를 추가:

```csharp
[System.Serializable]
public class BriefingCharacter
{
    public string characterKey; // "adrian", "selena", "adjutant", "scout", "detachment_leader", "rescue_leader", "defense_leader"
    public string displayName;  // 인스펙터 표기용, 실제 표시는 로컬라이제이션 키로 별도 처리 가능
    public Sprite portrait;
}
```

`BriefingEntry`의 `portrait1/2/3 : Sprite` 필드를 `speaker1Key/speaker2Key/speaker3Key : string`으로 교체. `ApplySelection()`에서 로스터를 키로 찾아 스프라이트를 채움. 로스터 7개 항목을 등록하되 `portrait` 필드는 이미지 에셋이 아직 없어 빈 채로 둠(구조만 준비, 나중에 인스펙터에서 채움).

### 3. 각 미션 항목의 인물 키 배정 (기존 10개 BriefingEntry에 반영)
위 표 기준으로 미션당 3명(미션0은 2명, 나머지 빈 슬롯은 비움)까지 `speaker1/2/3Key`에 채움. 미션3은 수정된 대사 기준 부관/정찰병/아드리안 3명.

## 범위 밖
- 초상화 이미지 에셋 자체 제작/수급 - 로스터 구조만 만들고 실제 그림은 나중에 사용자가 연결.
- 대사 텍스트를 실제 로컬라이제이션 JSON에 채워 넣는 작업 - doc/0616에서 이미 "남은 작업(사용자 몫)"으로 분류됨, 이번 요청 범위 아님(발언자 목록/이미지 슬롯 구조가 요청 핵심).

## 구현 진행
- `Docs/Campaign.md` 미션3 대사 수정 완료 (병사 대사를 정찰병에 병합).
- `BriefingRoomController.cs` 리팩터링 완료: `characterRoster`(List<BriefingCharacter>) 추가, `BriefingEntry.portrait1/2/3`을 `speaker1/2/3Key`(string)로 교체, `FindPortrait()`로 로스터 조회. 컴파일 성공.
- 씬 배선(로스터 7명 + 미션별 발언자 키) 작업 진행 중.

## 추가 결정 - 단역 인물 이미지 통합 (최종)
1차로 `별동대장`/`구조대장`/`방어부대장`을 공용 키로 묶기로 했다가, 사용자가 범위를 넓혀 확정:
**주요 인물 3명(부관/아드리안/셀레나 카터)만 개별 이미지를 쓰고, 나머지(정찰병 포함 대장 계열 전부)는 하나의 공용 이미지를 쓴다.**

로스터를 7개에서 4개로 축소:
- `adrian` - 아드리안 콜린스
- `selena` - 셀레나 카터
- `adjutant` - 부관
- `soldier` - 나머지 전원(정찰병/별동대장/구조대장/방어부대장) 공용

### 최종 speaker key 매핑 (기존 10개 briefingEntries)

| missionNumber | isSubMission | speaker1Key | speaker2Key | speaker3Key |
|---|---|---|---|---|
| 0 | false | adrian | adjutant | (empty) |
| 1 | false | adjutant | selena | adrian |
| 1 | true (sub1) | adrian | soldier | adjutant |
| 2 | false | adjutant | adrian | soldier |
| 2 | true (sub2) | soldier | adrian | soldier |
| 3 | false | adjutant | soldier | adrian |
| 3 | true (sub3) | adjutant | adrian | soldier |
| 4 | false | selena | adrian | adjutant |
| 4 | true (sub4) | selena | adrian | soldier |
| 5 | false | adjutant | selena | adrian |

(sub2는 원래 정찰병+별동대장 2명이 모두 단역이라 speaker1/3이 둘 다 `soldier`로 겹침 - 같은 공용 이미지가 두 슬롯에 표시됨, 의도된 결과.)

doc/0618은 이 결정 이전에 서브에이전트가 자동 작성한 7키 버전이라 superseded 처리.

## 상태
완료. Campaign.md 미션3 대사 수정, `BriefingRoomController` 리팩터링(컴파일 성공), `Briefing_Room.unity`에 로스터 4명 + 미션 10개 발언자 키를 위 표대로 배선하고 저장까지 완료 (YAML 재확인함).

## 추가 수정 - 초상화/맵 참조를 자식 Image로 재배선
`speakerPortraitImage/2/3`, `mapImage` GameObject 자체의 Image는 배경 프레임이고, 실제 그림을 넣을 곳은 그 자식 `Image` 오브젝트라는 걸 사용자가 확인. `BriefingRoomController`의 UI 참조 4개를 각 부모가 아닌 자식 `Image`(예: `Canvas/speakerPortraitImage/Image`)로 재배선하고 씬 저장 완료.

## 남은 작업 (사용자 몫)
- `characterRoster`의 각 `portrait` Sprite - 이미지 에셋이 없어 비어있음. 인스펙터에서 4명(아드리안/셀레나/부관/soldier) 이미지 연결 필요.
- doc/0616에서 남은 대사 텍스트(placeholder) 교체도 여전히 진행 전.
