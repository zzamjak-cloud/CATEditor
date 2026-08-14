using UnityEngine.UIElements;

namespace CAT.AnimationUtility
{
    // 애니메이션 유틸리티 모듈 인터페이스.
    // 이 인터페이스를 구현하면 AnimationUtilityManager가 TypeCache로 자동 발견·등록.
    // 새 기능 추가: Editor/Modules/ 에 새로운 .cs 파일 생성 후 이 인터페이스 구현만 하면 됨.
    public interface IAnimationToolModule
    {
        // 모듈 이름 (디버그/로그용)
        string ModuleName { get; }

        // UI 렌더 순서 (낮을수록 먼저 초기화됨)
        int UIOrder { get; }

        // 모듈 초기화. AnimationWindowAccessor를 저장하여 이후 사용.
        void Initialize(AnimationWindowAccessor accessor);

        // Animation Window의 rootVisualElement에 자신의 UI를 추가.
        // UI가 없는 모듈은 빈 메서드로 구현.
        void InitUI(VisualElement container);

        // 매 프레임 호출 (EditorApplication.update).
        // 프레임 업데이트가 불필요하면 빈 메서드로 구현.
        void OnUpdate();

        // Hierarchy에서 선택 변경 시 호출 (Selection.selectionChanged).
        // 선택 변경 처리가 불필요하면 빈 메서드로 구현.
        void OnSelectionChanged();

        // 모듈 정리 (리소스 해제 등).
        void Dispose();
    }
}
