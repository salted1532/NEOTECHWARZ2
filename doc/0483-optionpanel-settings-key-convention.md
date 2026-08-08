# 0483 - OptionPanel 향후 설정 추가 대비 (키 컨벤션 정리)

## 요청 내용
"optionpanel에서 추후에 추가될 그래픽 설정이나 게임설정같은 부가적인 설정에 관해서는 추가하기
좋도록 준비정도만 해줘 현재 optionpanel에서 번역할 부분은 없어"

그래픽/게임 설정 등을 지금 만들라는 게 아니라, 나중에 추가할 때 로컬라이즈가 쉽도록 최소한만
정리해두라는 요청 - 실제 새 설정 UI는 안 만듦(YAGNI).

## 변경 내용
기존 오디오 라벨 4개 키를 평평한 `ui.*` 네임스페이스에서 `settings.audio.*`로 옮김:
`ui.bgm`→`settings.audio.bgm`, `ui.voice`→`settings.audio.voice`, `ui.master`→`settings.audio.master`,
`ui.sfx`→`settings.audio.sfx`. (`en.json`/`ko.json`, `OptionPanel.prefab`의 4개 `LocalizedText.key`)

이렇게 컨벤션(`settings.<카테고리>.<항목>`)만 세워두면, 나중에 그래픽/게임 설정을 실제로 추가할 때
`settings.graphics.*`, `settings.game.*`로 자연스럽게 이어감. 새 라벨 오브젝트에 `LocalizedText`
붙이고 `key`만 이 패턴으로 채운 뒤 `en.json`/`ko.json`에 그 키의 값 한 줄씩 추가하면 끝 - 코드 수정
불필요(기존 `LocalizationManager`/`LocalizedText` 설계가 이미 그렇게 되어 있음). 컨벤션은
`LocalizedText.cs` 상단 주석에 적어둠.

컴파일 확인 완료(에러 0).
