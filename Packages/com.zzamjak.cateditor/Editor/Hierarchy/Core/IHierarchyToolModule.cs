using UnityEngine;
using UnityEngine.UIElements;

namespace CAT.HierarchyUtility
{
    // 하이어라키 유틸리티 모듈 인터페이스.
    // 이 인터페이스를 구현하면 HierarchyUtilityManager가 TypeCache로 자동 발견·등록.
    // 새 기능 추가: Editor/Modules/ 에 새로운 .cs 파일 생성 후 이 인터페이스 구현만 하면 됨.
    public interface IHierarchyToolModule
    {
        // 모듈 이름 (디버그/로그용)
        string ModuleName { get; }

        // UI 렌더 순서 (낮을수록 먼저 초기화됨)
        int UIOrder { get; }

        // 모듈 초기화. HierarchyWindowAccessor를 저장하여 이후 사용.
        void Initialize(HierarchyWindowAccessor accessor);

        // Hierarchy Window의 rootVisualElement에 자신의 UI를 추가.
        // UI가 없는 모듈은 빈 메서드로 구현.
        void InitUI(VisualElement container);

        // hierarchyWindowItemOnGUI 콜백 (각 아이템 IMGUI 렌더링).
        // IMGUI 렌더링이 불필요하면 빈 메서드로 구현.
        void OnHierarchyItemGUI(int instanceID, Rect selectionRect);

        // 매 프레임 호출 (EditorApplication.update).
        // 프레임 업데이트가 불필요하면 빈 메서드로 구현.
        void OnUpdate();

        // Selection 변경 시 호출 (Selection.selectionChanged).
        // 선택 변경 처리가 불필요하면 빈 메서드로 구현.
        void OnSelectionChanged();

        // 하이어라키 변경 시 호출 (hierarchyChanged + prefabStageOpened/Closing 통합).
        // 하이어라키 변경 처리가 불필요하면 빈 메서드로 구현.
        void OnHierarchyChanged();

        // 모듈 정리 (리소스 해제 등).
        void Dispose();
    }
}
