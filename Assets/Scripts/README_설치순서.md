# Board of Dead 전체 교체 설치 순서

## 1. 기존 중복 코드 삭제

아래 이름의 기존 스크립트가 프로젝트에 남아 있으면 새 코드와 충돌합니다.

- `GameEnums.cs`
- `DefaultPlayerSettingData.cs`
- `PlayerData.cs`
- `PlayerBoardPrefab.cs`
- `BoardSpacePrefab.cs`
- `DistrictPrefab.cs`
- `GridManager.cs`
- `PlayerManager.cs`
- `CardManager.cs`
- `TurnManager.cs`
- `SearchManager.cs`
- `RadioEventManager.cs`
- `BoardCameraController.cs`
- `GameBoardSettingManager.cs`

기존 보드 생성 코드 폴더를 백업한 뒤 제거하고 이 묶음을 넣는 방식이 가장 안전합니다.

## 2. 씬 최소 구성

```text
BoardSystem
└── GameBoardSettingManager

Main Camera
```

다른 Manager는 없어도 `GameBoardSettingManager`가 자동 추가합니다.

## 3. 즉시 테스트

프리팹을 하나도 넣지 않아도 다음이 자동 생성됩니다.

- 지구 5개
- 지구별 6x6 공간
- 지구별 2x1 건물 3개
- 유기적인 내부 도로
- 인접 지구 사이 1~2개 도로
- 테스트 플레이어 1~6명
- 기본 시작 도로
- 카메라 자동 포커스

## 4. DistrictPrefab 연결

각 지구 전용 프리팹 루트에 `DistrictPrefab`을 붙입니다.

```text
ResidentialDistrict
├── DistrictPrefab
├── 배경 모델
├── Animator
└── 장식
```

Inspector에 연결:

- Building Prefabs: 2x1 건물 프리팹
- Road Dead End
- Road Straight
- Road Corner
- Road T Junction
- Road Cross

프리팹 기본 방향:

- Dead End: 북쪽
- Straight: 남북
- Corner: 북동
- T Junction: 북동서
- 건물: 로컬 X축 방향으로 2칸

## 5. 카메라

자동으로 Main Camera에 `BoardCameraController`를 붙입니다.

- WASD / 방향키: 이동
- 우클릭 드래그: 이동
- 휠: 줌
- 보드 생성 직후 자동 중앙 정렬

## 6. 테스트 Context Menu

Play 상태에서 `GameBoardSettingManager` 우측 메뉴:

- `Test/Move Current Player To First Connected`
- `Test/Search Current Player`
- `Test/End Current Turn`

마지막 플레이어의 턴을 종료하면:

1. 기존 라디오 카드 지속시간 감소
2. 랜덤 건물 3개에 라디오 카드 배치
3. 다음 라운드 시작
