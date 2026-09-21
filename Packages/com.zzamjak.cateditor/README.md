# CATEditor

Unity 에디터 생산성 도구 모음 패키지입니다. Hierarchy / Animation Window 확장, 프리팹 프리뷰, 씬 툴바, 즐겨찾기 폴더, 커스텀 단축키 등을 제공합니다.

> 셰이프 텍스처 생성기는 2.0.0 부터 [ShapeGenerator](https://github.com/zzamjak-cloud/ShapeGenerator) 패키지로 분리되었습니다 (`openupm add com.zzamjak.shapegenerator`).

- **에디터 전용**: 모든 코드는 에디터에서만 동작하며 빌드에 포함되지 않습니다.
- **요구 버전**: Unity 6 (6000.0) 이상, uGUI 2.0 (TextMeshPro 포함)

---

## 설치

### Unity Package Manager (Git URL)

1. Unity 에디터에서 `Window > Package Manager` 열기
2. `+` 버튼 → `Install package from git URL...` 선택
3. 아래 URL 입력:

```
https://github.com/zzamjak-cloud/CATEditor.git?path=/Packages/com.zzamjak.cateditor#v2.1.1
```

특정 버전 대신 최신 상태를 받으려면 `#v2.1.1` 태그를 생략합니다.

### manifest.json 직접 편집

`Packages/manifest.json`의 `dependencies`에 추가:

```json
{
  "dependencies": {
    "com.zzamjak.cateditor": "https://github.com/zzamjak-cloud/CATEditor.git?path=/Packages/com.zzamjak.cateditor#v2.1.1"
  }
}
```

---

## 기능

### 1. Hierarchy 유틸리티 (`Editor/Hierarchy`)

Hierarchy 창에 생산성 도구를 추가합니다. `IHierarchyToolModule` 기반 모듈 시스템으로, 새 모듈 파일 하나만 추가하면 자동 등록됩니다.

| 모듈 | 기능 |
|------|------|
| HierarchyRenamer | Hierarchy 창 하단의 일괄 이름 변경 UI (접기 상태는 EditorPrefs에 저장) |
| HierarchyMarker | 오브젝트 간 참조 관계를 아이콘으로 표시 |
| HierarchyPresetMenu | 프리팹 생성 드롭다운 메뉴 |

상세 문서: [Editor/Hierarchy/README.md](Editor/Hierarchy/README.md)

### 2. Animation Window 확장 (`Editor/Animation`)

Animation Window에 추가 기능을 주입합니다. `IAnimationToolModule` 기반 모듈 시스템입니다.

| 모듈 | 기능 |
|------|------|
| AnimationAutoSave | 애니메이션 에셋 자동 저장 |
| AnimationSync | Hierarchy 선택 → Animation Window 자동 스크롤 |
| AnimationOffset | 루프 오프셋 · 키 추가 · 키 정리 UI |
| AnimationParticle | 애니메이션 미리보기 중 파티클 시뮬레이션 토글 |

상세 문서: [Editor/Animation/README.md](Editor/Animation/README.md)

### 3. 유틸리티 (`Editor/Utility`)

| 도구 | 기능 |
|------|------|
| SceneToolbar | 메인 툴바에 씬 전환 드롭다운과 플레이 시작 씬 선택 버튼 추가 |
| FavoriteFoldersWindow | `CAT > Utility > Favorite` — 자주 쓰는 폴더 즐겨찾기 창 |
| TransformResetter | Transform / RectTransform 인스펙터에 리셋 버튼 추가 |
| ImageFolderEditor | Image 인스펙터의 스프라이트 선택기를 폴더 기준으로 필터링 |
| CustomShortcuts | 아래 표 참조 |

**CustomShortcuts 단축키 / 메뉴**

| 메뉴 | 단축키 | 기능 |
|------|--------|------|
| `CAT > Create > Image` | `Cmd/Ctrl+Alt+I` | Image 생성 |
| `CAT > Create > Raw Image` | `Cmd/Ctrl+Alt+R` | RawImage 생성 |
| `CAT > Create > TextMeshPro Text` | `Cmd/Ctrl+Alt+T` | RaycastTarget 꺼진 TMP 텍스트 생성 |
| `CAT > Create > Square Sprite` | `Cmd/Ctrl+Alt+S` | 사각형 스프라이트 생성 |
| `CAT > Transform > Copy Transform` | `F5` | Transform 값 복사 |
| `CAT > Transform > Paste Transform` | `Shift+F5` | Transform 값 붙여넣기 |
| `CAT > Transform > Copy TMP` | `F6` | TextMeshPro 설정 복사 |
| `CAT > Transform > Paste TMP` | `Shift+F6` | TextMeshPro 설정 붙여넣기 |

생성 메뉴는 선택 중인 오브젝트를 부모로 삼습니다. 2.1.0 부터 Hierarchy 우클릭(`GameObject >`) 메뉴에서는 노출되지 않습니다.

---

## 폴더 구조

```
com.zzamjak.cateditor/
├── Editor/
│   ├── Animation/       Animation Window 확장 (Core + Modules)
│   ├── Hierarchy/       Hierarchy 유틸리티 (Core + Modules)
│   ├── Preview/         프리팹 프리뷰 (UI / 이펙트)
│   └── Utility/         씬 툴바, 즐겨찾기 폴더, 단축키 등
└── Runtime/             런타임 코드 없음 (어셈블리 골격만 유지)
```

## 라이선스

CATEditor는 **GNU General Public License v3.0 only (GPL-3.0-only)** 로 배포됩니다.

- Copyright (c) 2026 zzamjak. 모든 저작권 고지는 보존되어야 합니다.
- 수정본과 파생 배포물은 GPL-3.0-only로 공개되어야 하며, 대응 소스 코드를 함께 제공해야 합니다.
- 저작권 고지를 제거하거나, 소스 비공개 독점물로 재라이선스하거나, GPL-3.0-only 권리를 제한하는 방식으로 배포할 수 없습니다.

자세한 내용은 [LICENSE.md](LICENSE.md)와 [NOTICE.md](NOTICE.md)를 확인하세요.
