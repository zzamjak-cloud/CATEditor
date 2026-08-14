# HierarchyUtility

Unity Editor의 Hierarchy 창에 생산성 도구를 추가하는 에디터 플러그인입니다.
`IHierarchyToolModule` 인터페이스 기반의 모듈 시스템으로 구성되어 있으며, 새 기능을 파일 하나만 추가하면 자동으로 등록됩니다.

---

## 파일 구조

```
Assets/Plugins/CAT/HierarchyUtility/Editor/
├── Core/
│   ├── IHierarchyToolModule.cs       — 모듈 인터페이스
│   ├── HierarchyWindowAccessor.cs    — Hierarchy Window 참조 캐싱
│   └── HierarchyUtilityManager.cs    — 모듈 자동 발견 및 이벤트 통합 관리자
└── Modules/
    ├── HierarchyRenamerModule.cs     — 일괄 이름 변경 UI (UIOrder = 0)
    ├── HierarchyMarkerModule.cs      — 참조 관계 아이콘 표시 (UIOrder = 10)
    └── HierarchyPresetMenuModule.cs  — 프리팹 생성 드롭다운 메뉴 (UIOrder = 20)
```

---

## 기능

### 1. HierarchyRenamer — 일괄 이름 변경

Hierarchy 창 하단 오른쪽에 이름 변경 UI가 표시됩니다.
`▼` 버튼으로 접을 수 있으며, 상태는 EditorPrefs에 저장됩니다.

| 버튼 | 동작 |
|------|------|
| `Arr` | 선택된 오브젝트(또는 그 직계 자식)를 이름 기준으로 정렬 |
| `Rn` | 선택된 오브젝트 이름을 입력값으로 일괄 변경 |
| `Rp` | 이름에서 왼쪽 입력값을 오른쪽 입력값으로 치환 |
| `T_` | 이름 앞에 입력값 추가 (Prefix) |
| `_T` | 이름 뒤에 입력값 추가 (Suffix) |
| `Num` | 선택 순서대로 번호 부여 (`이름_00`, `이름_01`, ...) |

- **정렬(`Arr`)**: `_` 기준으로 분리된 다단계 정렬 적용 (숫자/문자 혼합 가능)
- **번호(`Num`)**: 오른쪽 숫자 필드로 자릿수 지정 (기본값 2)
- 모든 동작은 Undo 지원

---

### 2. HierarchyMarker — 참조 관계 아이콘 표시

씬 내 MonoBehaviour 컴포넌트의 직렬화 필드를 분석하여, **다른 오브젝트를 참조하고 있는 경우** 해당 오브젝트 행 오른쪽에 색상 아이콘을 표시합니다.

#### 아이콘 색상

| 색상 | 의미 |
|------|------|
| 초록 | 일반 스크립트에서 참조 중 |
| 주황 | 프리팹 루트 스크립트에서 참조 중 |

#### 참조 카운트

참조하는 컴포넌트가 2개 이상이면 아이콘 오른쪽에 숫자가 표시됩니다.

```
[오브젝트 이름]  ●3
```

#### 클릭 동작

아이콘을 클릭하면:
- 참조 중인 **모든 부모 오브젝트**가 Hierarchy에서 선택됨 (파란색 하이라이트)
- 대표 오브젝트(프리팹 루트 우선)로 스크롤 이동 (Ping)
- Console에 참조 컴포넌트 및 필드 이름 로그 출력

#### 지원하는 필드 타입

- 단일 참조: `GameObject`, `Component` 계열 (예: `CanvasGroup`, `Transform`)
- 배열: `GameObject[]`, `Component[]` 계열
- 제네릭 리스트: `List<GameObject>`, `List<Component>` 계열

#### 필터링

다음 네임스페이스의 컴포넌트는 스캔에서 제외됩니다:
- `UnityEngine.*`
- `UnityEditor.*`
- `TMPro.*`

> 플레이 모드 진입 시 비활성화되며, Prefab Stage 진입/종료 시 자동 갱신됩니다.

---

### 3. HierarchyPresetMenu — 프리팹 생성 드롭다운

Hierarchy 창 상단 오른쪽에 **▼ (폴더이름)** 버튼이 표시됩니다.
클릭 시 프로젝트 내 지정된 이름의 폴더에서 프리팹 목록을 드롭다운으로 표시합니다.

- 검색 및 스크롤 지원 (`AdvancedDropdown` 기반)
- 폴더 하위 구조를 계층적으로 표시
- 프리팹 선택 시 현재 선택된 오브젝트의 자식으로 생성 (없으면 씬 루트)
- Prefab Stage 모드에서도 동작
- Undo 지원

**기본 대상 폴더:** `Presets`
프로젝트 전체에서 해당 이름의 폴더를 모두 탐색합니다.

**폴더 이름 변경:**
- 버튼 **우클릭** → "폴더 이름 변경..." 메뉴 선택 (EditorPrefs에 저장, 코드 수정 불필요)
- 코드에서: `HierarchyPresetMenuModule.SetTargetFolderName(string)` 호출

---

## 모듈 추가 방법

1. `Editor/Modules/` 에 새 `.cs` 파일 생성
2. `IHierarchyToolModule` 인터페이스 구현
3. `UIOrder` 값 설정 (낮을수록 먼저 초기화)
4. **끝** — `HierarchyUtilityManager`가 `TypeCache`로 자동 발견 및 등록

```csharp
namespace CAT.HierarchyUtility
{
    public class MyNewModule : IHierarchyToolModule
    {
        public string ModuleName => "MyNewModule";
        public int UIOrder => 30;

        public void Initialize(HierarchyWindowAccessor accessor) { }
        public void InitUI(VisualElement container) { }
        public void OnHierarchyItemGUI(int instanceID, Rect selectionRect) { }
        public void OnUpdate() { }
        public void OnSelectionChanged() { }
        public void OnHierarchyChanged() { }
        public void Dispose() { }
    }
}
```

---

## IHierarchyToolModule 인터페이스

| 메서드 | 호출 시점 |
|--------|-----------|
| `Initialize(accessor)` | 에디터 로드 시 1회 |
| `InitUI(container)` | Hierarchy Window 열릴 때 1회 (rootVisualElement 주입) |
| `OnHierarchyItemGUI(instanceID, rect)` | 각 Hierarchy 아이템 IMGUI 렌더링 시 |
| `OnUpdate()` | `EditorApplication.update` (매 프레임) |
| `OnSelectionChanged()` | `Selection.selectionChanged` |
| `OnHierarchyChanged()` | `hierarchyChanged` + Prefab Stage 열림/닫힘 |
| `Dispose()` | 리소스 해제 시 |

---

## 네임스페이스

```
CAT.HierarchyUtility
```
