# 0517. 건물 완공 시 체력이 최대치보다 1 모자라게 끝나는 버그 수정 제안

**날짜:** 2026-08-11

## 요청 내용

> 1까지 떨어지는게 아니라 500짜리 건물이 499로 완공될때가 있어

`doc/0516`에서 조사했던 "체력이 1로 뚝 떨어지는" 시나리오(SkyLancer 지상 폭격 오사)가 아니라, **공격을 전혀
안 받아도 최대체력보다 정확히 조금(예: 500 중 499) 모자란 채로 완공되는 별개의 문제**였음. `doc/0516`의
가설은 이 증상에는 해당하지 않으므로 기각하고, 아래 원인으로 정정한다.

## 원인

`Assets/Scripts/Building/BaseStructure.cs:90-114`

```csharp
private void Update()
{
    ...
    remainingBuildTime -= Time.deltaTime;

    if (healthManager != null)
    {
        healAccumulator += healthPerSecond * Time.deltaTime;

        if (healAccumulator >= 1f)
        {
            int wholeHeal = Mathf.FloorToInt(healAccumulator);
            healAccumulator -= wholeHeal;
            healthManager.Heal(wholeHeal);
        }
    }

    if (remainingBuildTime <= 0f)
        CompleteConstruction();
}
```

- 체력은 `HealthManager.Heal(int)`이 정수만 받기 때문에, 매 프레임 들어오는 소수점 값을 `healAccumulator`에
  누적해뒀다가 **1.0을 넘는 순간에만** 정수부(`FloorToInt`)를 떼서 `Heal()`을 호출하는 구조다. 나머지 소수부는
  다음 프레임을 위해 `healAccumulator`에 남겨둔다.
- 문제는 **건설이 끝나는 바로 그 마지막 프레임**: `remainingBuildTime`이 0 이하가 되면 그 즉시
  `CompleteConstruction()`이 호출되고 오브젝트가 파괴되는데, 그 시점에 `healAccumulator`에 아직 1.0을 못
  넘긴 소수부(예: 0.97)가 남아있으면 **그 몫은 영원히 `Heal()`로 넘어가지 못하고 그냥 버려진다.**
- 건설 시간 내내 매 프레임 소수점이 누적되다 보니, 마지막에 하필 딱 "거의 1.0 직전"에서 끊기는 경우가 흔히
  생기고, 그 결과가 딱 "최대체력보다 1 적게" 완공되는 형태로 나타난다. (몫이 어중간하면 2 이상 모자랄 수도
  있지만, `healAccumulator`는 항상 1 미만이라 최대 손실은 항상 1 미만 - 정수 기준으로는 정확히 1만큼만
  모자라게 보이는 것도 이 때문)
- 이건 `doc/0053`이 의도한 "건설 중 피해를 입으면 체력이 낮게 이어짐" 동작과는 무관한, 순수 반올림 손실
  버그다. 공격을 전혀 안 받아도 항상 이만큼은 손해를 본다.

## 계획된 수정

건설이 끝나는 그 프레임에 한해서, 아직 반영 안 된 `healAccumulator`의 나머지(소수부)까지 올림해서 마저
적용한다. `HealthManager.Heal()`은 이미 `maxHealth`를 넘지 않게 클램프하므로 살짝 더 넣어도 안전함.

```csharp
if (remainingBuildTime <= 0f)
{
    if (healthManager != null && healAccumulator > 0f)
        healthManager.Heal(Mathf.CeilToInt(healAccumulator)); // 마지막 프레임에 남은 소수부 손실 방지

    CompleteConstruction();
}
```

- 건설 중 피해를 입어서 정상적으로 최대체력보다 낮게 끝나야 하는 경우(`doc/0053`)는 그대로 유지됨 - 이
  변경은 "이론상 다 채웠어야 할 몫인데 반올림 때문에 못 받은 나머지"만 마저 채워주는 것뿐, 데미지로 인한
  손실분까지 메꿔주지는 않음(`healAccumulator`는 데미지와 무관하게 시간 경과분만 담고 있음).

## 변경 예정 파일

- `Assets/Scripts/Building/BaseStructure.cs`

---

## 적용 (사용자 승인 후)

> 네, 진행

제안대로 적용함.

### `BaseStructure.cs`

```diff
         if (remainingBuildTime <= 0f)
-            CompleteConstruction();
+        {
+            if (healthManager != null && healAccumulator > 0f)
+                healthManager.Heal(Mathf.CeilToInt(healAccumulator)); // 마지막 프레임에 남은 소수부 손실 방지 (doc/0517)
+
+            CompleteConstruction();
+        }
     }
```

## 검증

- `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0` (기존에 있던 37개 경고는 이번 변경과 무관한
  프로젝트 전역의 obsolete API 경고 - 그대로 유지됨).

## 변경된 파일

- `Assets/Scripts/Building/BaseStructure.cs`
