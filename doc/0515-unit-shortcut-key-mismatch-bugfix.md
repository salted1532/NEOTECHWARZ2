# 0515. 유닛 생산 단축키 불일치 버그 수정 제안

**날짜:** 2026-08-11

## 요청 내용

> IFV레인저 R키 단축키 작동안함
> 가디언 드론 G가 아니라 D로 단축키 되어있음
> 다른 유닛은 문제가 없는거로 보이긴 하는데 모두 한번씩 확인해줘

## 조사 내용

- 유닛 생산 단축키는 `UnitDataSO.UnitData.shortcutKey`(`Assets/Scripts/ScriptableObject/UnitDataSO.cs:140-142`)에
  `KeyCode` 정수값으로 저장되고, 실제 값은 코드가 아니라 각 진영 데이터 에셋(`.asset` YAML)에 들어있다.
  `ProductionSlot.cs:157`가 `Input.GetKey(shortcut)`로 이 값을 읽어 버튼을 대신 눌러준다.
- 에셋마다 `description` 필드에 `"shortcut key [<color=yellow>X</color>]"` 형태로 툴팁에 보여줄
  "의도한" 글자가 박혀 있어서, 이 텍스트와 실제 `shortcutKey` 정수값을 대조하면 불일치를 바로 찾을 수 있다.
  (KeyCode 알파벳 키는 소문자 아스키값과 동일: `a`=97 … `z`=122)
- **`Assets/Scripts/ScriptableObject/Data/NTA Unit Data SO.asset`** 전수 대조 결과:

  | 유닛 | tier | 툴팁에 적힌 글자 | 실제 `shortcutKey` | 상태 |
  |---|---|---|---|---|
  | Worker Drone | 0 | W | 119 (W) | 정상 |
  | Assault Trooper | 1 | A | 97 (A) | 정상 |
  | Scout Drone | 1 | D | 100 (D) | 정상 |
  | Sharpshooter | 1 | S | 115 (S) | 정상 |
  | **Ranger IFV** | 2 | **R** | **105 (I)** | **불일치 - 신고된 버그** |
  | Pulasr Tank | 2 | P | 112 (P) | 정상 |
  | SkyLancer | 2 | S | 115 (S) | 정상 (Sharpshooter와 글자는 겹치지만 tier가 달라 같은 생산 패널에 동시에 뜨지 않음) |
  | Firehawk | 3 | F | 102 (F) | 정상 |
  | **Guardian Drone** | 3 | **G** | **100 (D)** | **불일치 - 신고된 버그** |

  → 신고된 두 건 외에 다른 NTA 유닛은 전부 툴팁 글자와 실제 단축키가 일치함.
- **`OC Unit Data SO.asset`**, **`Spore Brood Unit Data SO.asset`**: 두 파일의 모든 유닛이
  `shortcutKey: 0`(`KeyCode.None`)으로, 애초에 단축키가 하나도 배정되어 있지 않음(버그가 아니라
  미배정 상태 - 이번 신고 범위 밖으로 판단, 필요하면 별도로 배정 작업 진행 가능).
- 고급유닛 특성(액티브 스킬) 쪽 `shortcutKey`(예: Sharpshooter Snipe=102/Cloak=99, SkyLancer Ground
  Bombardment=103, Guardian Drone Focused Barrage=102/Shield Deployment=98)는 툴팁에 글자가
  박혀있지 않고 order panel에서 동적으로 표시되는 방식이라 대조 불가 - 다만 값 자체는 서로 겹치지
  않고 정상 범위 내라 이상 징후는 없음.

## 원인

데이터 입력 실수로 추정. `Ranger IFV`는 `I`(105)가, `Guardian Drone`은 `D`(100)가 잘못 들어가 있음
(둘 다 유닛 이름의 다른 단어 첫 글자 - IFV/Drone - 와 우연히 일치하는 값이라 단순 오타로 보임).

## 계획된 변경

파일: `Assets/Scripts/ScriptableObject/Data/NTA Unit Data SO.asset`

- 235번째 줄, `Ranger IFV`의 `<shortcutKey>k__BackingField: 105` → `114` (KeyCode.R)
- 426번째 줄, `Guardian Drone`의 `<shortcutKey>k__BackingField: 100` → `103` (KeyCode.G)

데이터 값 2줄만 바뀌는 변경이라 코드/씬 수정은 없음.

## 변경 예정 파일

- `Assets/Scripts/ScriptableObject/Data/NTA Unit Data SO.asset`

---

## 적용 (사용자 승인 후)

> 네, 진행

제안대로 적용함.

### `NTA Unit Data SO.asset`

```diff
   - <unitName>k__BackingField: 'Ranger IFV '
     ...
-    <shortcutKey>k__BackingField: 105
+    <shortcutKey>k__BackingField: 114

   - <unitName>k__BackingField: 'Guardian Drone '
     ...
-    <shortcutKey>k__BackingField: 100
+    <shortcutKey>k__BackingField: 103
```

## 변경된 파일

- `Assets/Scripts/ScriptableObject/Data/NTA Unit Data SO.asset` (`Ranger IFV`, `Guardian Drone`의
  `shortcutKey` 값 수정 - 데이터 값 2줄만 변경, 코드/씬 수정 없음)
