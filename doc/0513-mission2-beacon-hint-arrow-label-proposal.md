# 0513. Mission2 비콘 안내 문구에 "Beacon ↓" 라벨 추가 제안

**날짜:** 2026-08-10

## 요청 내용

> 이게 인게임 상에서 비콘 위에 존재하는 문구라서 beacon 하고 -> 이런식으로 아래 화살표를 하나
> 추가해서 이게 비콘이다 하고 알려주도록 문구 수정해줘

`MissionObjectText`(doc/0512)는 비콘 바로 위에 떠 있는 인게임 캔버스 텍스트라, "이게 비콘이다"를
바로 알 수 있도록 첫 줄에 "Beacon ↓"(아래 화살표로 바로 밑의 오브젝트를 가리킴) 라벨을 추가하고
싶다는 요청.

## 계획된 변경

`stage2.beaconhint` 키 값만 수정(씬/코드 변경 없음, doc/0512에서 이미 `LocalizedText`로 연결해둔
동일 키). 첫 줄에 라벨을 추가하고, 본문에서는 "비콘/beacon"이 중복되니 "여기로/here"로 바꿈:

| | 기존 | 변경 |
|---|---|---|
| en | `Bring the Artifact back to the beacon using a worker.` | `Beacon ↓\nBring the Artifact back here using a worker.` |
| ko | `유물을 일꾼을 이용해 비콘으로 가져오세요.` | `비콘 ↓\n유물을 일꾼을 이용해 이곳으로 가져오세요.` |

`\n`은 다른 키들(`cmd.move.desc` 등)과 동일하게 JSON 문자열 안에 그대로 써서 TMP가 줄바꿈으로
렌더링하게 함.

## 변경 예정 파일
- `Assets/Resources/Localization/en.json`, `ko.json` (`stage2.beaconhint` 값만 수정)

---

## 적용 (사용자 승인 후)

> 진행시켜줘

제안대로 `en.json`/`ko.json`의 `stage2.beaconhint` 값만 수정. `git diff` 확인 결과 두 파일 다
해당 줄 한 줄씩만 깨끗하게 바뀜(씬 파일은 이번엔 건드리지 않아 doc/0511·0512에서 겪은 다른 씬
동반 변경 이슈 없음).

## 변경된 파일
- `Assets/Resources/Localization/en.json`, `ko.json` (`stage2.beaconhint` 값 수정)

## 후속 - 줄 순서 반대로 (같은 날 추가 요청)

> 설명이랑 비콘 화살표를 서로 줄위치를 반대로 해줘

"Beacon ↓"/"비콘 ↓"가 화살표로 바로 아래 비콘을 더 직접적으로 가리키도록, 안내 문구를 1줄,
"Beacon ↓"/"비콘 ↓"를 2줄로 순서 변경.

| | 변경 후 |
|---|---|
| en | `Bring the Artifact back here using a worker.\nBeacon ↓` |
| ko | `유물을 일꾼을 이용해 이곳으로 가져오세요.\n비콘 ↓` |

`git diff` 확인 결과 두 파일 다 해당 줄 한 줄씩만 깨끗하게 바뀜.

### 변경된 파일
- `Assets/Resources/Localization/en.json`, `ko.json` (`stage2.beaconhint` 값 재수정)
