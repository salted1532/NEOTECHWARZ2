# 0512. Mission2 비콘 안내 텍스트("MissionObjectText") 번역 추가 제안

**날짜:** 2026-08-10

## 요청 내용

> 미션2에서 비콘안에 canvas를 따로 두어 MissionObjectText라고 하나 추가했는데 해당 텍스트는
> 유물을(미션오브젝트)를 일꾼을 통해 가져오라는 문구가 적힌 텍스트인데 이것도 번역에 추가해줘

## 조사 내용

- `Assets/Scenes/Missions/Mission2.unity`에서 `MissionObjectText`(fileID `496945822`) 확인 -
  `TextMeshProUGUI`(fileID `496945824`) 하나만 붙어있는 순수 정적 텍스트, 스크립트로 갱신되는
  게 아님(`LocalizedText`나 다른 컴포넌트 없음).
- 현재 하드코딩된 텍스트: `유물을 일꾼을 이용해 비콘으로 가져오세요.`
- `doc/0481`에서 설계한 로컬라이제이션 시스템(`LocalizationManager`, `LocalizedText`,
  `en.json`/`ko.json`)이 이미 이 프로젝트에 적용돼 있음 - 스크립트가 안 건드리는 정적 라벨은
  `LocalizedText` 컴포넌트를 붙이고 `key`만 채운 뒤 두 JSON 파일에 같은 키로 값을 추가하면 됨
  (코드 수정 불필요, `LocalizedText.cs`의 헤더 주석에 명시된 패턴).
- 기존 `objective.stage2.main1`("(주목표) 외계 유물 확보")은 목표 체크리스트 항목이라 이
  안내 문구와는 다른 텍스트 - 새 키가 필요함.

## 계획된 변경

- 새 키 `stage2.beaconhint` 추가:
  - `en.json`: `"Bring the relic back to the beacon using a worker."`
  - `ko.json`: `"유물을 일꾼을 이용해 비콘으로 가져오세요."` (기존 문구 그대로)
- `Mission2.unity`의 `MissionObjectText` 오브젝트에 `LocalizedText` 컴포넌트를 추가하고
  `target`을 기존 `TextMeshProUGUI`(fileID `496945824`)에, `key`를 `stage2.beaconhint`로 연결.
  (씬 파일을 직접 텍스트 편집으로 컴포넌트를 추가하는 건 fileID 참조를 새로 만들어야 해서
  실수 위험이 있음 - Unity 에디터 API로 `AddComponent` 후 필드를 연결하는 방식으로 진행 예정.)

## 변경 예정 파일

- `Assets/Resources/Localization/en.json`, `ko.json` (`stage2.beaconhint` 키 추가)
- `Assets/Scenes/Missions/Mission2.unity` (`MissionObjectText`에 `LocalizedText` 컴포넌트 부착)

---

## 적용 (사용자 승인 후)

> 이대로 진행시켜줘
> (곧이어) relic말고 Artifact로 해줘

### `en.json` / `ko.json`
```diff
+    { "key": "stage2.beaconhint", "value": "Bring the Artifact back to the beacon using a worker." },   # en.json
+    { "key": "stage2.beaconhint", "value": "유물을 일꾼을 이용해 비콘으로 가져오세요." },                        # ko.json
```
영어 값은 처음에 "relic"으로 넣었다가, 기존 `missionitem.artifact.name` 키가 "Artifact"로 되어있는
것과 표기를 맞춰달라는 요청에 따라 "Artifact"로 수정함.

### `Mission2.unity`
uloop 에디터 API(`Undo.AddComponent<LocalizedText>`)로 `MissionObjectText`(fileID 496945822)에
`LocalizedText` 컴포넌트(fileID 496945826)를 추가하고 `target`을 기존
`TextMeshProUGUI`(fileID 496945824), `key`를 `stage2.beaconhint`로 연결 - 텍스트 직접 편집으로
컴포넌트를 새로 만들지 않아 fileID 참조 실수 위험을 피함. 씬에 반영된 최종 블록:
```yaml
--- !u!114 &496945826
MonoBehaviour:
  m_GameObject: {fileID: 496945822}
  m_Script: {fileID: 11500000, guid: 3b21f21ae842ea245bd2716532ff75e3, type: 3}
  m_EditorClassIdentifier: Assembly-CSharp::LocalizedText
  target: {fileID: 496945824}
  legacyTarget: {fileID: 0}
  key: stage2.beaconhint
```

## 검증

- `SerializedObject` 재조회 + 저장된 `.unity` 파일 직접 읽기 두 가지 방식으로 `key`/`target` 값
  확인 완료.
- Unity 콘솔: 이 작업으로 새로 발생한 에러 없음(기존에 있던 `PinPoint_0~3`의 missing script
  경고 등은 무관).
- `en.json`/`ko.json`은 각각 한 줄만 깨끗하게 추가됨 (`git diff`로 확인).

### ⚠ 확인 필요 - `Mission1~4.unity`에 예상보다 큰 diff 재발생 (doc/0511과 동일 현상)

이번에도 `Mission2.unity`(제가 실제로 건드린 씬)뿐 아니라 `Mission1`, `Mission3`, `Mission4`까지
수백~천 줄대 diff가 함께 발생함. 표본으로 몇 개를 직접 대조해본 결과:
- 사라진 `Material`(`!u!21`) 블록들은 `LineRenderer` 등이 쓰는 내장(임베디드) 재질로, 삭제된
  fileID를 참조하던 `m_Materials.Array.data[0]` 오버라이드는 전부 **새로 생성된 다른 fileID**를
  정상적으로 가리키도록 같이 갱신되어 있음 - 참조가 끊긴(깨진) 곳은 못 찾음.
- 즉 Unity 에디터가 저장 시점에 임베디드 애셋(머티리얼 등)의 fileID를 재생성하며 씬을 다시
  직렬화하는 것으로 보이고, 표본 확인 범위에서는 내용 손실은 없어 보임 - 다만 4개 씬 전체를
  전수 검증한 건 아니라서 확답은 어려움.
- `Assets/TextMesh Pro/Fonts/Pretendard/Pretendard-Black SDF.asset`(폰트 아틀라스)도 같이
  갱신됨 - 폰트가 쓰이는 씬을 열면 아틀라스가 재생성되는 것으로 보이는 별개의 자동 동작.
- `Assets/Resources/PerformanceTestRunInfo.json`(.meta 포함), `PerformanceTestRunSettings.json`
  (.meta 포함)이 새로 생김 - 이번 작업 중 제가 만든 적 없음, Unity Test Framework가 뭔가의
  계기로 생성한 것으로 추정 - 필요 없으면 삭제하셔도 될 것 같음.

**결론적으로 제가 의도한 변경(Mission2의 `LocalizedText` 컴포넌트 + JSON 키 2줄)은 정상 반영됐지만,
그 저장 과정에서 에디터가 열려있던 다른 미션 씬들의 임베디드 애셋도 함께 재직렬화한 것으로 보입니다.
커밋 전에 `git diff`로 `Mission1/3/4.unity`를 한 번 훑어봐 주시고, 원치 않는 부분이 있으면 말씀해주세요.**

## 변경된 파일

- `Assets/Resources/Localization/en.json`, `ko.json` (`stage2.beaconhint` 키 추가)
- `Assets/Scenes/Missions/Mission2.unity` (`MissionObjectText`에 `LocalizedText` 컴포넌트 부착 +
  위에서 설명한 임베디드 애셋 재직렬화 동반)
- (부수적, 이번 작업이 직접 만든 변경은 아님) `Mission1/3/4.unity`, 폰트 아틀라스 애셋 - 위 확인
  필요 항목 참고
