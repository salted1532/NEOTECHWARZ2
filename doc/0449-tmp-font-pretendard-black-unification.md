# 0449. 게임 내 모든 TextMeshPro 폰트를 Pretendard-Black SDF로 통일

**날짜:** 2026-08-07

## 요청 내용
> 현재 게임의 사용되는 모든 TextMeshPro의 폰트를 pretendard-Black SDF로 바꿔줘 그래야 한글폰트들이
> 적용 되거든 Canvas안에 사용된 TextMeshPro를 고치면 될거야 그럴려면 아마 Prefabs안에 Game안에
> GameManager안에 있는 canvas를 건드리는게 좋을거야

## 조사 내용

`m_fontAsset:` 필드로 프로젝트 전체를 검색해서 실제 게임에 쓰이는 TextMeshPro 사용처를 전부 확인함
(TextMesh Pro 패키지 자체의 "Examples & Extras" 샘플 씬/프리팹은 게임에 포함되지 않는 번들 예제라
제외, `Assets/Scenes/SampleScene.unity`도 `ProjectSettings/EditorBuildSettings.asset`의 빌드 씬
목록에 없는 유니티 기본 템플릿 씬이라 제외).

실제 게임에 포함된 파일과 현재 폰트:

| 파일 | LiberationSans SDF(기본 폰트, 한글 미지원) | Pretendard-Black SDF(이미 적용됨) |
|---|---|---|
| `Assets/prefabs/Game/GameManager.prefab` | 23 | 1 |
| `Assets/prefabs/UI/OptionPanel.prefab` | 4 | 0 |
| `Assets/prefabs/UI/Squadbutton.prefab` | 1 | 0 |
| `Assets/Scenes/MainScene/MainScene.unity` | 3 | 2 |
| `Assets/prefabs/UI/MissionObject.prefab` | 0 | 1 (이미 완료, 변경 불필요) |

사용자 말대로 대부분(23개)이 `GameManager.prefab` 하나에 몰려 있음(주문 패널/정보 패널/미니맵 등
게임 내내 떠 있는 UI가 대부분 여기 있는 Canvas 하위). 나머지 4개 파일에도 소수씩 있어서 "게임의
모든 TextMeshPro"를 정확히 만족하려면 이 4개 파일도 같이 고쳐야 함.

`m_fontAsset`뿐 아니라 `m_sharedMaterial`도 같이 바꿔야 함 — TMP는 폰트 에셋과 그 폰트 전용 머티리얼을
별도 필드로 참조하는데, 머티리얼은 폰트마다 내부 fileID가 다름(LiberationSans는 `2180264`,
Pretendard-Black SDF는 `-7267651362464213874`). `m_fontAsset`만 바꾸고 `m_sharedMaterial`을 그대로
두면 폰트와 안 맞는 머티리얼이 걸려서 깨져 보이거나 아예 렌더링이 안 될 수 있음 — 그래서 두 필드를
쌍으로 같이 치환함.

## 변경한 내용

4개 파일에서 다음 두 패턴을 정확히 텍스트 치환:

1. `m_sharedMaterial: {fileID: 2180264, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}`
   → `m_sharedMaterial: {fileID: -7267651362464213874, guid: 82cbbef41c7b30a49a2ed4607e4eec4e, type: 2}`
2. `guid: 8f586378b4e144a9851e7b34d9b748ee`(남은 것 = `m_fontAsset` 필드들, fileID는 두 폰트 다
   `11400000`으로 동일해서 안 건드림) → `guid: 82cbbef41c7b30a49a2ed4607e4eec4e`

## 검증

- 4개 파일 전부 `8f586378b4e144a9851e7b34d9b748ee`(LiberationSans) 참조 0건, `m_fontAsset`/
  `m_sharedMaterial` 개수 일치(짝이 안 맞는 곳 없음) 확인.
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 0`.
- Unity 콘솔 Error 로그 0건.

## 변경된 파일

- `Assets/prefabs/Game/GameManager.prefab` (23곳)
- `Assets/prefabs/UI/OptionPanel.prefab` (4곳)
- `Assets/prefabs/UI/Squadbutton.prefab` (1곳)
- `Assets/Scenes/MainScene/MainScene.unity` (3곳)
- (`Assets/prefabs/UI/MissionObject.prefab`은 이미 Pretendard였어서 변경 없음)
