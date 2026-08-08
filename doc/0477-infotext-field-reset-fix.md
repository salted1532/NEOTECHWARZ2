# 0477 - infoText 연결이 초기화된 문제 수정

## 질문
"폰트 크기 조절하니깐 출력이 안되는데 확인좀"

## 원인
폰트 크기와는 무관함. `UIController.infoText`(doc/0476에서 연결)가 `Assets/prefabs/Game/GameManager.prefab`
안에서 `{fileID: 0}`(미할당)으로 되돌아가 있었음 — `ShowInfoPanel`은 `if (infoText != null) infoText.text = ...`
로 null 가드가 걸려있어서 조용히 아무 것도 안 쓰고 넘어갔고, 결과적으로 에디터에 미리 입력해둔 플레이스홀더
"New Text"만 계속 보이는 상태였음(폰트 크기를 바꿔도 어차피 그 텍스트는 코드가 안 건드리니 체감 변화가 없었음).

**추정 경위**: doc/0476에서 프리팹 YAML 파일에 직접 `infoText: {fileID: ...}`를 써넣었는데, 그 시점에
유니티 에디터가 이미 이 프리팹을 메모리에 들고 있었다면(디스크 변경을 자동으로 못 알아챔), 이후 사용자가
폰트 크기를 바꾸고 에디터가 프리팹을 저장하면서 "에디터가 메모리에 들고 있던(= infoText 연결 없는) 상태"로
덮어썼을 가능성이 높음.

## 조치
`infoText: {fileID: 847192614712833648}`로 다시 연결. 유니티에서 재확인:
- `AssetDatabase.Refresh()` 후 `UIController.infoText`가 다시 `InfoText`(TextMeshProUGUI)로 정상 resolve됨
- 혹시 또 덮어써질까봐 `PrefabStageUtility.GetCurrentPrefabStage()`로 확인 - 현재 GameManager.prefab이 에디터에서
  Prefab Mode로 열려있는 상태가 아님을 확인(덮어써질 위험 없음)

## 참고
같은 문제가 재발할 수 있는 조건은 "내가 파일을 직접 고칠 때 유니티 에디터가 그 에셋을 이미 메모리에 열어두고
있는 경우"임 - 에디터에서 그 프리팹을 만지고 있지 않을 때 고치거나, 고친 직후 에디터에서 리로드/새로고침하는
습관을 들이면 좋음.
