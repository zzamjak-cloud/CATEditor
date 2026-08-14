using UnityEngine;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 도형 생성기 인터페이스
    /// 새로운 도형 추가 시 이 인터페이스를 구현
    /// </summary>
    public interface IShapeGenerator
    {
        /// <summary>
        /// UI에 표시될 도형 이름
        /// </summary>
        string ShapeName { get; }

        /// <summary>
        /// 도형 설정 UI를 그림
        /// </summary>
        void DrawSettingsGUI();

        /// <summary>
        /// 텍스처 생성
        /// </summary>
        /// <returns>생성된 Texture2D (호출자가 Destroy 책임)</returns>
        Texture2D Generate();

        /// <summary>
        /// 저장될 파일 이름 반환 (확장자 포함)
        /// </summary>
        string GetFileName();

        /// <summary>
        /// 생성될 텍스처 크기
        /// </summary>
        Vector2Int GetTextureSize();

        /// <summary>
        /// Sprite Border 값 반환 (Left, Bottom, Right, Top)
        /// 9-slice 용도로 사용
        /// </summary>
        Vector4 GetSpriteBorder();
    }
}
