# 0550 - EnemyAIDirector 웨이브 구성 물량 절반 축소

## 날짜
2026-08-13

## 요청 내용
"현재 웨이브별 패턴의 유닛량을 절반정도씩 줄여줘 유닛 비율은 괜찮은데 량을 절반씩 낮춰줘"

→ doc/0539에서 정한 OC/Spore Brood 웨이브 구성표(`attackWavesOC`/`attackWavesSporeBrood`)의 유닛 종류별
비율은 그대로 두고, 마릿수만 대략 절반으로 낮춤(반올림, 최소 1마리는 유지해 비율/구성 자체는 그대로
보임). 별동대 구성(`raidSquadCompositionOC`/`SporeBrood`)은 "웨이브별 패턴"이 아니라서 그대로 둠.

## 변경 (반올림, 절반)
### OC
| 웨이브 | 기존 | 변경 |
|---|---|---|
| 1차 | Cyborg Soldier×10 | Cyborg Soldier×5 |
| 2차 | Cyborg×8 + Railgunner×3 | Cyborg×4 + Railgunner×2 |
| 3차 | Cyborg×8 + Striker×3 + Brute Mech×2 | Cyborg×4 + Striker×2 + Brute Mech×1 |
| 4차 | Cyborg×6 + Heavy Tank×3 + Ironhawk×2 | Cyborg×3 + Heavy Tank×2 + Ironhawk×1 |
| 5차(반복) | Heavy Tank×3 + Raven×2 + Strike Drone×1 | Heavy Tank×2 + Raven×1 + Strike Drone×1 |

### Spore Brood
| 웨이브 | 기존 | 변경 |
|---|---|---|
| 1차 | Ripfang×14 | Ripfang×7 |
| 2차 | Ripfang×10 + Spitter×5 | Ripfang×5 + Spitter×3 |
| 3차 | Spitter×8 + Skitterwing×4 | Spitter×4 + Skitterwing×2 |
| 4차 | Ripfang×12 + Spitter×8 | Ripfang×6 + Spitter×4 |
| 5차(반복) | Ripfang×10 + Spitter×8 + Skitterwing×6 | Ripfang×5 + Spitter×4 + Skitterwing×3 |

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개, 경고 39개(기존과 동일).

## 참고 - 이미 씬에 배치한 Mission1의 EnemyAIDirector에는 자동 반영 안 됨
이 값들은 C# 필드 **초기값**(코드 안 기본값)이라, 씬에 이미 배치돼 인스펙터 값이 직렬화된 기존
컴포넌트에는 자동으로 적용되지 않는다 - Unity는 이미 저장된 인스펙터 값을 코드 기본값으로 되돌리지
않음. Mission1의 `EnemyAIDirector`에 반영하려면 인스펙터에서 직접 값을 절반으로 고치거나, 컴포넌트를
우클릭 → Reset(다른 커스터마이징 값도 전부 기본값으로 돌아가니 주의)을 써야 함.

## 영향받는 파일
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`
