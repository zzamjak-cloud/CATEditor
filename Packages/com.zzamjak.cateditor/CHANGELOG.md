# Changelog

이 프로젝트의 주요 변경 사항을 기록합니다.

형식은 [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/)를 따르며,
버전은 [Semantic Versioning](https://semver.org/lang/ko/)을 따릅니다.

## [1.1.0] - 2026-08-17

### Added

- **Prefab Preview**: 프로젝트 뷰에서 프리팹 선택 시 미리보기 창 (`CAT > Utility > Prefab Preview`)
  - UI 프리팹: 레퍼런스 해상도 캔버스 기준 렌더링, 앵커 전용/Stretch/Graphic 없음 상태 안내, Rect 외곽선 표시
  - 이펙트 프리팹: 자동 반복 재생(1회성 포함), 재생/일시정지/속도 제어, 시간에 따른 최대 확산 기준 화면 맞춤
  - 배경 Dark/Checker/Light, 휠 줌/드래그 회전·팬, 선택 고정
- **HierarchyMarker 토글 메뉴**: `CAT > Hierarchy > Reference Markers` 로 참조 마커 전체 ON/OFF

### Changed

- **HierarchyMarker 증분 갱신**: 전체 씬 재스캔 대신 `ObjectChangeEvents` 기반으로 변경된 오브젝트만 재스캔.
  인스펙터에서 참조를 할당하면 마커가 즉시 갱신됨 (기존에는 하이어라키 구조가 바뀔 때까지 미반영)
- **에디터 성능 최적화 전반**
  - Window 탐색(`Resources.FindObjectsOfTypeAll`) 재시도 간격 제한 (Hierarchy/Animation 접근자)
  - 참조 필드가 없는 스크립트 타입은 스캔에서 제외 (타입 단위 캐시)
  - Transform/RectTransform/Image/RawImage/SpriteRenderer 커스텀 인스펙터의 리페인트당 할당 제거
  - 스프라이트 선택기: 스타일/검색 결과/라벨 캐싱, 썸네일 로드 중에만 리페인트
  - 프리팹 프리셋 드롭다운: 폴더 탐색을 `AssetDatabase.FindAssets("t:Folder")` 인덱스 검색으로 교체
  - 프리셋 버튼을 GUI 패스당 1회만 렌더링 (프리팹 편집 모드 중복 렌더링 제거)
  - SceneToolbar 툴바 주입 재시도 간격·횟수 제한
- **TransformResetter**: 다중 선택 지원 (`CanEditMultipleObjects`)

### Removed

- **AnimationAutoSave 모듈 제거**: 주기적 전체 에셋 스캔이 에디터 성능에 부담이 되어 삭제.
  애니메이션 에셋은 Unity 기본 저장(Ctrl+S)을 사용 (`Tools > Animation Auto Save` 메뉴도 함께 제거)

## [1.0.0] - 2026-08-15

### Added

- 최초 UPM 패키지 배포
- **Hierarchy 유틸리티**: 일괄 이름 변경(HierarchyRenamer), 참조 관계 마커(HierarchyMarker), 프리팹 프리셋 메뉴(HierarchyPresetMenu) — `IHierarchyToolModule` 모듈 시스템
- **Animation Window 확장**: 자동 저장(AutoSave), 선택 동기화(Sync), 루프 오프셋/키 정리(Offset), 파티클 시뮬레이션(Particle) — `IAnimationToolModule` 모듈 시스템
- **Shape Generator**: 원/다각형/별/그라디언트/노이즈 텍스처 생성 창 (`CAT > Utility > Shape Generator`)
- **SceneToolbar**: 메인 툴바 씬 전환 드롭다운
- **FavoriteFoldersWindow**: 즐겨찾기 폴더 창 (`CAT > Utility > Favorite`)
- **TransformResetter**: Transform/RectTransform 인스펙터 리셋 버튼
- **ImageFolderEditor**: 폴더 기준 스프라이트 선택기
- **CustomShortcuts**: Transform/TMP 복사-붙여넣기(F5/F6), 커스텀 UI 오브젝트 생성 단축키
