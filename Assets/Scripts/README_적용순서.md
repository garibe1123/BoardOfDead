# Compatibility Patch 02 적용 순서

이번 패치는 현재 Console에 나온 다음 참조 불일치를 해결합니다.

- `PlayerData.PlayerPresetId`
- `PlayerData.PlayerName`
- `PlayerData.GetItemAmount`
- `PlayerData.TrySpendAP`
- `PlayerData.RemoveItem`
- `PlayerData.CurrentVehicleInstanceId`
- `PlayerData.SetVehicle`
- `TurnManager.OnAllTurnsCompleted`
- `TurnManager.BeginRoundQueue`
- `RadioEventManager.ProcessEndOfRound`
- `CardSOBJ.CardType`
- `CardSOBJ.VehicleSOBJ`
- `CardSOBJ.EscapeRouteSOBJ`

## 반드시 교체할 파일

아래 파일을 같은 경로에 덮어씁니다.

```text
Assets/Scripts/01_SOBJ/CardSOBJ.cs
Assets/Scripts/02_Data/PlayerData.cs
Assets/Scripts/03_Manager/TurnManager.cs
Assets/Scripts/03_Manager/RadioEventManager.cs
```

## 중복 파일 금지

Project 창에서 아래 이름을 검색했을 때 각 파일이 하나만 존재해야 합니다.

```text
CardSOBJ
PlayerData
TurnManager
RadioEventManager
```

다른 폴더에 같은 클래스가 있으면 새 패치를 넣기 전에 제거합니다.

## 패치 특징

이번 파일은 기존 시스템을 삭제하지 않고 양쪽 API를 모두 유지합니다.

```text
기존 GameSessionData + TimelineManager
신규 PlayerManager + 자동 테스트 보드
```

따라서 `TimelineManager`, `VehicleManager`, `EscapeManager`,
`SearchManager`, `PlayerPrefab`을 별도로 수정할 필요가 없습니다.

## 적용 후

Unity에서 다음 순서로 처리합니다.

```text
1. Console Clear
2. Assets → Reimport All
3. 가장 첫 오류 확인
```

현재 오류 목록은 이 네 파일의 API 불일치로 연쇄 발생한 것이므로,
교체 후 오류 수가 크게 줄어야 정상입니다.
