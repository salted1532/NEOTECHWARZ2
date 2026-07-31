# 0331. 시야 경계 근처 적 유닛의 우클릭/A공격/스킬 판정 불가 버그 수정

**날짜:** 2026-07-31

## 요청

> 시야가 밝혀져서 보이는 적 유닛에 경우 우클릭이나 A공격, 스킬등이 사용가능하게 해줘 뭔가 경계에
> 애매하게 있는 애들이 가려진 판정이라서 그런거 같은데 한번 밝혀진 곳은 적유닛이 가려져 있지 않고
> 보이니깐 이부분 확인해서 수정해줘

## 원인

`Assets/Scripts/UserControl/UserControl.cs`의 `IsRevealedByFog(Vector3 worldPosition)`
([[0196]]/[[0197]]에서 도입)이 좌클릭/우클릭 선택·공격·스킬 지정·마우스 호버 커서 전부에서 "이 대상이
지금 안개에 가려져 있는가"를 판정하는 유일한 창구인데, 모든 호출부가 **레이캐스트가 실제로 맞은
지점(`RaycastHit.point`, 유닛 3D 모델 표면 위의 정확한 지점)**을 넘기고 있었음.

`csFogWar.CheckVisibility(pos, 0)`은 `pos`가 속한 **딱 한 칸의 안개 그리드 타일**만 확인한다
(여유 반경 없음). 유닛의 콜라이더/메쉬는 그 자체로 여러 그리드 타일에 걸칠 만큼 크기가 있을 수 있고,
클릭 각도나 카메라 시점에 따라 레이캐스트가 모델의 앞쪽/뒤쪽/모서리 등 다양한 지점을 맞춘다. 유닛의
"논리적 위치"(`transform.position`, 다른 시야 시스템이 전부 기준으로 삼는 바로 그 좌표)는 밝혀진
타일 안에 있어도, 화면상 클릭된 정확한 지점(모델 표면의 한 점)이 우연히 아직 안 밝혀진 인접 타일에
걸리면 `CheckVisibility`가 `false`를 반환 — 눈에는 또렷이 보이는 유닛인데 "가려짐" 판정이 나서
우클릭/A공격/스킬 지정이 전부 씹혔음. 사용자가 말한 "경계에 애매하게 있는 애들"이 정확히 이 현상.

## 수정

`UserControl.cs`의 `IsRevealedByFog(...)` 호출 10곳 전부, 인자를 `RaycastHit.point`(클릭/호버가
맞은 임의의 표면 지점) 대신 **대상 오브젝트 자신의 `transform.position`**으로 변경:

- 좌클릭 적 유닛/적 건물 선택+공격+스킬 지정 (2곳)
- 좌클릭 광물/가스 선택 (2곳)
- 우클릭 적 유닛/적 건물 공격 명령 (2곳)
- 우클릭 광물/가스 채취 명령 (2곳)
- 마우스 호버 커서 색 판정 (적 1곳, 자원 1곳)

이제 판정 기준이 "클릭이 우연히 어디를 맞췄는지"가 아니라 "그 유닛/건물/자원이 실제로 서 있는 좌표가
밝혀졌는지"로 통일되어, 화면에 또렷이 보이는 대상은 클릭 위치와 무관하게 항상 조작 가능함.

`npx uloop-cli compile`로 에러 0개 확인.

## 영향받는 파일

- `Assets/Scripts/UserControl/UserControl.cs`

## 후속: 반투명 경계 지대(안 보이는 안개↔완전히 밝혀진 시야 사이)에서도 여전히 판정 안 됨

**요청**: "안개에 가려진 판정은 잘 작동하는데 그 안개와 보여지는시야 부분 그 사이가 애매하게 반투명
안개란 말이지 근데 그경우 적 유닛은 보이게 되는데 판정은 안돼 그래서 그 애매한 부분에 대한 마진같은
느낌으로 여유를 좀 줘"

- 위 수정으로 "클릭 위치 vs 유닛 위치" 문제는 해결됐지만, 남은 문제는 안개 자체의 3단계 상태
  (`Shadowcaster.LevelColumn.ETileVisibility`: `Hidden`(완전 불투명) / `PreviouslyRevealed`(예전에
  밝혀졌던 곳 - 반투명, 지형은 보이지만 유닛은 안 보여야 정상인 상태) / `Revealed`(현재 시야 안,
  완전히 밝음)) 때문. `CheckVisibility(pos, 0)`은 오직 `Revealed`만 `true`로 치므로, `Revealed`와
  `Hidden` 사이의 반투명(`PreviouslyRevealed`류) 경계 타일에 유닛이 걸쳐 있으면 여전히 "안 보임"
  판정이 남아있었음.
- `csFogWar.CheckVisibility(pos, additionalRadius)`는 `additionalRadius > 0`이면 그 좌표 주변
  `(additionalRadius+2) × (additionalRadius+2)` 타일을 훑어서 그 중 하나라도 `Revealed`면 `true`를
  반환하는 여유 반경 기능을 이미 갖고 있었음(에셋 코드 수정 없이 인자만 넘기면 됨) — 요청한 "마진"에
  정확히 해당하는 기존 기능.
- `UserControl.cs`에 `[SerializeField] private int fogVisibilityMargin = 1;` 추가, `IsRevealedByFog()`가
  `fogWar.CheckVisibility(worldPosition, 0)` 대신 `fogWar.CheckVisibility(worldPosition, fogVisibilityMargin)`을
  호출하도록 변경. 기본값 1칸 여유(3×3 타일 검사)로 반투명 경계 지대까지 커버 — 필요하면 인스펙터에서
  값만 조정하면 됨(코드 재수정 불필요).
- `npx uloop-cli compile`로 에러 0개 확인.

## 후속2: 마진이 아니라 "반투명(PreviouslyRevealed) 타일 자체"를 인정해야 함

**요청**: "Revealed이 0.5일때도 판정이 되도록 해줘 정확히는 한번 밝혀진 곳은 반투명 상태로 안개가
지는데 이 부분에 있는 적유닛도 판정이 가능하도록 했으면 좋겠어 그게 아마 Revealed tile opacity인거
같은데 현재 0.5로 설정해뒀는데 이런 타일도 판정이 들어가도록"

- `fogVisibilityMargin`(경계 타일 여유)은 "Revealed 타일에 인접했는지"만 봐주는 우회책이라, 적이
  주변에 `Revealed` 타일이 하나도 없는 `PreviouslyRevealed` 지역 한복판에 있으면 여전히 막혔음.
  사용자가 지목한 `csFogWar.revealedTileOpacity`(기본 `0.5`)는 `Shadowcaster.FogField.GetColors()`가
  `PreviouslyRevealed` 타일의 안개 알파값으로 쓰는 바로 그 값 — 이 프로젝트는 "한 번 밝혀졌던 곳"을
  완전히 까맣게 가리지 않고 그 값만큼 반투명하게 표시하므로, 적 유닛이 그 위에 있어도 화면엔 (흐릿하게)
  보임. 반면 유닛 렌더러 자체를 안개 상태에 따라 껐다 켰다 하는 로직은 이 프로젝트에 따로 없음(안개
  플레인의 알파 오버레이만으로 가림) — 즉 "화면에 보이는가"와 "판정 가능한가"가 서로 다른 기준을 쓰고
  있던 게 근본 원인.
- `IsRevealedByFog()`를 `fogWar.CheckVisibility()`(오직 `Revealed`만 인정) 호출 대신,
  `fogWar.shadowcaster.fogField`를 직접 읽어 `Revealed`와 `PreviouslyRevealed`를 **둘 다** "보임"으로
  인정하도록 재작성([[territory-permanent-vision-design]] 0175/`TerritoryFogReveal.cs`와 동일한
  "에셋은 안 건드리고 public API(`shadowcaster.fogField`, `WorldToLevel`, `CheckLevelGridRange`)만
  사용" 패턴). `fogVisibilityMargin`(기본 1)은 여전히 유지 - 이제는 "Revealed 타일에 인접"이 아니라
  "Revealed 또는 PreviouslyRevealed 타일에 인접"으로 의미가 넓어짐. 완전히 가려진 `Hidden` 타일만
  여전히 차단됨.
- `npx uloop-cli compile`/`get-logs`로 에러 0개 확인.
