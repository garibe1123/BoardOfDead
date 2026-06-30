Board Of Dead - GameBoard 기반 Procedural City Board v10
========================================================

1. 이번 버전의 목적
-------------------

v10은 기존 GameBoardSettingManager가 생성한 실제 게임 노드를 기준으로
1x1 이동 칸 오버레이를 코드로 표시합니다.

중요:
- 새로운 바닥 지형을 생성하지 않습니다.
- 기존 바닥 Mesh, Plane, Terrain을 덮어쓰지 않습니다.
- Tilemap을 사용하지 않습니다.
- 실제 BoardSpacePrefab이 존재하는 위치에만 이동 칸 표시를 만듭니다.
- GameBoardSettingManager.cs, GridManager.cs, GameBoardPrefab.cs는 수정하지 않습니다.


2. 표시되는 타일
----------------

RoadSpaces:
- 도로 논리 노드 아래에 회색 1x1 타일이 표시됩니다.
- 보조 도로로 추가된 실제 BoardSpacePrefab 노드도 포함됩니다.

BuildingSpaces:
- 기존 BuildingSpace 논리 노드 아래에 1x1 타일이 표시됩니다.
- 역할 건물 색상을 찾으면 건물과 동일한 역할 색상으로 표시됩니다.

역할별 색상:
- Apartment: 아파트 색상
- ShoppingMall: 쇼핑몰 색상
- Hospital: 병원 색상
- Office: 오피스 색상
- Industrial: 산업 시설 색상
- Civic: 공공 시설 색상

역할을 찾지 못한 건물 노드는 Neutral Building Tile Color를 사용합니다.


3. 타일 구분 방식
-----------------

각 논리 칸은 두 겹의 평면 Mesh로 표시됩니다.

외곽:
- Movement Tile Border Color
- 기본 크기 0.96셀

안쪽 색상 면:
- 기본 크기 0.86셀
- 외곽선보다 0.0015만큼 위에 표시

이 구조 때문에 서로 붙어 있는 칸도 경계가 명확하게 보이며,
플레이어가 이동 전에 몇 칸인지 세기 쉬워집니다.


4. Inspector 설정
-----------------

Logical Movement Tile Overlay

Create Movement Tile Overlay
- 이동 칸 표시 전체 활성화 여부

Movement Tile Outer Size Cells
- 타일 외곽 크기
- 기본값: 0.96
- 1에 가까울수록 칸 사이 간격이 좁아집니다.

Movement Tile Inner Size Cells
- 실제 색상 면 크기
- 기본값: 0.86
- 값이 작을수록 외곽선이 두꺼워집니다.

Movement Tile Surface Offset
- 기존 바닥과 겹쳐 깜빡이는 Z-Fighting 방지 높이
- 기본값: 0.004
- 기존 바닥 높이에 따라 0.002~0.015 범위에서 조절하십시오.

Movement Tile Border Color
- 모든 논리 칸의 외곽선 색상

Road Movement Tile Color
- 도로 노드 타일 색상

Neutral Building Tile Color
- 역할을 찾지 못한 일반 건물 노드 타일 색상

Landmark Role Colors
- 역할 건물 Mesh 색상과 역할 건물 타일 색상에 함께 사용됩니다.


5. 적용 방법
------------

1) 기존 v9 폴더를 삭제합니다.

   BoardOfDead_ProceduralCityBoard_GameBoardBased_v9

2) v10의 SCRIPT 폴더를 Assets 아래에 복사합니다.

3) Unity 컴파일 완료 후 현재 보드가 이미 생성되어 있다면:

   Rebuild Visuals From Existing Board

4) 보드와 보조 도로까지 새로 생성하려면:

   Generate Board + Secondary Roads + City


6. 생성되는 오브젝트
--------------------

__ProceduralCityVisuals_UrbanLowRise_v10

하위에 다음 오브젝트가 추가됩니다.

- MovementTiles_Border_AllLogicalCells
- MovementTiles_Road
- MovementTiles_Building_Neutral
- MovementTiles_Landmark_Apartment
- MovementTiles_Landmark_ShoppingMall
- MovementTiles_Landmark_Hospital
- MovementTiles_Landmark_Office
- MovementTiles_Landmark_Industrial
- MovementTiles_Landmark_Civic

빈 종류는 GameObject를 만들지 않습니다.


7. 게임 로직과의 관계
----------------------

타일 오버레이는 시각 표시 전용입니다.

변경하지 않는 항목:
- BoardSpacePrefab
- NodeId
- ConnectedSpaces
- GridManager 등록 상태
- 이동 AP
- 도로 연결
- 건물 탐색
- 게임 이벤트

따라서 플레이어 이동 판정은 기존 코드 그대로 작동하고,
화면에서 논리 칸의 위치와 이동 거리를 확인할 수 있게 됩니다.


8. 성능
-------

타일마다 GameObject를 하나씩 만들지 않습니다.

- 전체 외곽선: 결합 Mesh 1개
- 도로 타일: 결합 Mesh 1개
- 일반 건물 타일: 결합 Mesh 1개
- 역할별 건물 타일: 역할당 결합 Mesh 최대 1개

따라서 수백~수천 개의 논리 칸이 있어도 GameObject 수가 급격히 늘지 않습니다.


9. 주의 사항
------------

- v9과 v10을 동시에 두면 클래스 중복 오류가 발생합니다.
- 기존 바닥이 타일보다 위에 있으면 Movement Tile Surface Offset을 높이십시오.
- 도로 Mesh는 타일보다 위에 표시되도록 기존 Road Surface Offset을 유지합니다.
- 역할 건물 타일 색상은 생성된 역할 건물의 SourceNodeId를 기준으로 연결됩니다.
- createDenseSmallBuildings를 끄면 역할 매핑이 없으므로 BuildingSpace 타일은 중립색으로 표시됩니다.
