# CAT AnimationUtility

Unity Animation Window에 추가 기능을 주입하는 에디터 전용 플러그인.

---

## 파일 구조

```
Assets/Plugins/CAT/AnimationUtility/Editor/
├── Core/
│   ├── IAnimationToolModule.cs       인터페이스 — 모든 모듈의 계약
│   ├── AnimationWindowAccessor.cs    Reflection 래퍼 — 내부 상태 접근
│   └── AnimationUtilityManager.cs   중앙 관리자 — 모듈 자동 발견·등록
└── Modules/
    ├── AnimationAutoSaveModule.cs    애니메이션 에셋 자동 저장
    ├── AnimationSyncModule.cs        Hierarchy 선택 → Animation Window 자동 스크롤
    ├── AnimationOffsetModule.cs      루프 오프셋·키 추가·키 정리 UI
    └── AnimationParticleModule.cs    파티클 시뮬레이션 토글
```

---

## 아키텍처

### 자동 발견 흐름

```
[InitializeOnLoad]
AnimationUtilityManager (static constructor)
  └─ TypeCache.GetTypesDerivedFrom<IAnimationToolModule>()
       └─ UIOrder 기준 정렬
            └─ 각 모듈 Initialize(accessor)
                 └─ EditorApplication.update → Update()
                      └─ Animation Window 열리면 InjectUI(win)
                           └─ 각 모듈 InitUI(rootVisualElement)
```

`TypeCache`를 사용하므로 새 모듈 클래스만 추가하면 별도 등록 없이 자동으로 동작한다.

### 이벤트 등록 (통합 전 → 통합 후)

| 이벤트 | 통합 전 | 통합 후 |
|--------|---------|---------|
| `EditorApplication.update` | 2개 | Manager 1개 |
| `Selection.selectionChanged` | 1개 | Manager 1개 |
| UI 주입 로직 | 2곳 | Manager 1곳 |
| Reflection 중복 코드 | 3곳 | Accessor 1곳 |

---

## IAnimationToolModule 인터페이스

```csharp
public interface IAnimationToolModule
{
    string ModuleName { get; }
    int UIOrder { get; }           // 낮을수록 먼저 초기화

    void Initialize(AnimationWindowAccessor accessor);
    void InitUI(VisualElement container);  // container = rootVisualElement
    void OnUpdate();               // 매 프레임 (불필요하면 빈 메서드)
    void OnSelectionChanged();     // Selection 변경 시 (불필요하면 빈 메서드)
    void Dispose();
}
```

### UIOrder 현황

| 값 | 모듈 |
|----|------|
| 0   | AnimationSyncModule |
| 10  | AnimationOffsetModule |
| 20  | AnimationParticleModule |
| 100 | AnimationAutoSaveModule |

---

## AnimationWindowAccessor

Animation Window 내부 상태에 Reflection으로 접근하는 래퍼. 모듈에서 직접 Reflection을 작성할 필요 없음.

| 프로퍼티 / 메서드 | 설명 |
|-------------------|------|
| `Window` | Animation Window EditorWindow 참조 (캐싱, null이면 재탐색) |
| `ActiveClip` | 현재 선택된 AnimationClip |
| `ActiveRoot` | 애니메이션 루트 GameObject |
| `CurrentTime` | 현재 재생 시간 (초) |
| `CurrentFrame` | 현재 프레임 |
| `IsPlaying` | 재생 중 여부 |
| `GetState()` | AnimationWindowState 객체 |
| `GetAnimEditor()` | m_AnimEditor 객체 (캐싱) |
| `GetFieldDeep(obj, name)` | 베이스 클래스까지 탐색하는 Reflection 헬퍼 |
| `SmartScrollToID(id)` | Unity 6 / 2022 호환 TreeView 스크롤 |
| `ForceRefresh()` | Animation Window 강제 리프레시 |
| `InvalidateCache()` | Window/AnimEditor 캐시 초기화 |

**State 접근 방식 (fallback 순서):**
1. `window.state` property (공식 경로)
2. `window.m_AnimEditor.m_State` 필드 (내부 경로)

---

## 모듈 설명

### AnimationSyncModule (UIOrder: 0)

Hierarchy에서 GameObject를 선택하면 Animation Window의 해당 항목으로 자동 스크롤.

- **트리거:** `Selection.selectionChanged` — Hierarchy 창이 포커스된 경우에만 동작
- **UI:** 없음
- **핵심 로직:** `hierarchyData.GetRows()`로 행 탐색 → `hierarchyState.selectedIDs` 업데이트 → `SmartScrollToID()`

### AnimationOffsetModule (UIOrder: 10)

Animation Window 우하단에 2행 툴바를 주입.

```
[ GameObject명 ][ Frame/Time ][ offset값 ][ R ][ Clean ]
[ +All ][ +Pos ][ +Rot ][ +Sca ]          [ Position ][ Rotation ][ Scale ]
```

| 버튼 | 기능 |
|------|------|
| Frame/Time 토글 | Offset 입력 단위 전환 (프레임 ↔ 초) |
| R | Offset 값 0으로 리셋 |
| Clean | 값 변화 없는 중간 키프레임 일괄 제거 |
| +All/+Pos/+Rot/+Sca | 현재 Transform 값으로 0프레임·마지막 프레임에 키 추가 |
| Position/Rotation/Scale | 선택된 오브젝트의 해당 속성 키를 Offset만큼 시간축 이동 |

- RectTransform 자동 감지 (`m_AnchoredPosition`, `localEulerAnglesRaw.z`)
- Quaternion / Euler 회전 방식 자동 감지
- 모든 편집에 Undo 지원

### AnimationParticleModule (UIOrder: 20)

Animation Window 상단에 파티클 시뮬레이션 토글 버튼 주입.

- 활성화 시: 애니메이션 프레임 진행에 따라 `m_IsActive`가 켜지는 오브젝트의 ParticleSystem을 자동 시뮬레이션
- 비활성화 시: 파티클 처리 없이 일반 재생

### AnimationAutoSaveModule (UIOrder: 100)

애니메이션 관련 에셋(AnimationClip, AnimatorController 등)이 변경되면 자동으로 저장.

- **트리거:** `Undo.postprocessModifications` — 애니메이션 관련 오브젝트 변경 감지 시 `_pendingSave` 플래그 설정
- **저장 주기:** 2초 간격 (변경이 없으면 실행하지 않음)
- **대상:** AnimationClip, AnimatorController, AnimatorOverrideController, AnimatorState, AnimatorStateMachine, AnimatorTransitionBase, BlendTree
- **ON/OFF:** `Tools > Animation Auto Save > Enabled` 메뉴 토글 (EditorPrefs 저장)
- **UI:** 없음

---

## 새 모듈 추가 방법

1. `Editor/Modules/` 에 새 `.cs` 파일 생성
2. `IAnimationToolModule` 구현
3. `UIOrder` 설정 (기존: 0, 10, 20, 100)
4. **끝** — `TypeCache`가 자동 발견·등록

```csharp
namespace CAT.AnimationUtility
{
    public class MyNewModule : IAnimationToolModule
    {
        public string ModuleName => "MyNewModule";
        public int UIOrder => 30;

        private AnimationWindowAccessor _accessor;

        public void Initialize(AnimationWindowAccessor accessor) { _accessor = accessor; }
        public void InitUI(VisualElement container) { /* UI 없으면 빈 메서드 */ }
        public void OnUpdate() { }
        public void OnSelectionChanged() { }
        public void Dispose() { }
    }
}
```

---

## 주의사항

- **에디터 전용:** 모든 코드는 `Editor/` 폴더 안에 있으며 런타임에 포함되지 않음
- **네임스페이스 충돌:** `namespace CAT.AnimationUtility` 내부에서 `AnimationUtility.xxx`를 호출하면 컴파일 에러 발생. `using AnimUtil = UnityEditor.AnimationUtility;` 별칭 사용 필요
- **UI 위치:** `InitUI`의 `container`는 `rootVisualElement` 자체임. `Position.Absolute`는 Animation Window 전체 기준으로 적용됨

---

## 버전 이력

### v1.2.0 (2026-03-27)
- `AnimationAutoSaveModule` 추가 — 애니메이션 에셋 자동 저장
- `AnimSyncModule` → `AnimationSyncModule`로 파일명·클래스명 통일

### v1.1.0 (2026-02-22)
- 모듈 시스템으로 전환 (`IAnimationToolModule` 인터페이스)
- `AnimationWindowAccessor`로 Reflection 코드 통합
- `AnimationUtilityManager`가 `TypeCache`로 모듈 자동 발견
- `EditorApplication.update` 단일 등록으로 통합

### v1.0.0
- `AnimSyncTool.cs` — Hierarchy 자동 스크롤
- `AnimationOffset.cs` — Offset/키 편집 UI
- `AnimationParticleSimulator.cs` — 파티클 시뮬레이션
