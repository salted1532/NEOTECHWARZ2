# 0523. 공격력/방어력 툴팁 표시를 "합산 (기본값 +보너스)" 형식으로 변경 - 제안

**날짜:** 2026-08-11

## 요청 내용

> 공격력 방어력 표시가 "공격력: 합산 (원래공격력 +숫자)" 이런식으로 나오도록 해줘 방어력도 같은 방식으로

## 현재 상태

`UIController.cs:711~712`의 `FormatStatWithBonus()`가 공격력/방어력 툴팁 양쪽에서 공용으로 쓰이고
있고, 연구 보너스가 있으면 `"6 +2"`처럼 기본값과 보너스를 더하지 않고 나란히만 표기한다:

```csharp
private static string FormatStatWithBonus(int baseValue, int bonus) =>
    bonus > 0 ? $"{baseValue} +{bonus}" : baseValue.ToString();
```

이 함수 하나가 공격력(`infopanel.attacktooltip`)/방어력(`infopanel.armortooltip`) 툴팁 둘 다에서
호출되므로, 여기 하나만 고치면 요청하신 대로 공격력/방어력 둘 다 동일한 방식으로 바뀐다.

## 변경 제안

```diff
 private static string FormatStatWithBonus(int baseValue, int bonus) =>
-    bonus > 0 ? $"{baseValue} +{bonus}" : baseValue.ToString();
+    bonus > 0 ? $"{baseValue + bonus} ({baseValue} +{bonus})" : baseValue.ToString();
```

예: 기본 공격력 6, 연구 보너스 +2 → `"8 (6 +2)"`. 보너스가 0이면(연구 안 함/적 유닛 등) 기존과
동일하게 숫자만 표시.

## 변경 예정 파일
- `Assets/Scripts/UI/UIController.cs`

이대로 진행할까요?

---

## 적용 (사용자 승인 후)

> 이대로 진행시켜줘

제안대로 `FormatStatWithBonus()` 수정함. `npx uloop-cli compile` 성공 확인 (Error 0개, 기존
obsolete API 경고만 있음).

## 변경된 파일
- `Assets/Scripts/UI/UIController.cs`
