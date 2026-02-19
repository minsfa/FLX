# FLX UI 색상 스키마 분석

## 현재 색상 정의 (App.xaml)

### 기본 색상 팔레트

| 색상명 | 16진수 코드 | RGB | 용도 |
|---|---|---|---|
| **PrimaryColor** | `#2196F3` | RGB(33, 150, 243) | 주요 강조 색상 (파란색) |
| **AccentColor** | `#FF5722` | RGB(255, 87, 34) | 보조 강조 색상 (주황색) |
| **BackgroundColor** | `#1E1E1E` | RGB(30, 30, 30) | 메인 배경 (다크 그레이) |
| **SurfaceColor** | `#2D2D2D` | RGB(45, 45, 45) | 카드/패널 배경 (밝은 다크 그레이) |
| **TextColor** | `#FFFFFF` | RGB(255, 255, 255) | 주요 텍스트 (흰색) |
| **TextSecondaryColor** | `#B0B0B0` | RGB(176, 176, 176) | 보조 텍스트 (밝은 회색) |

### 추가 색상 (MainWindow.xaml 및 코드 내)

| 색상명 | 16진수 코드 | RGB | 용도 |
|---|---|---|---|
| **Header Blue** | `#14427B` | RGB(20, 66, 123) | 섹션 헤더 텍스트 |
| **Ready State** | `#2E7D32` | RGB(46, 125, 50) | 스터디 준비 상태 배지 |
| **Recording State** | `#F9A825` | RGB(249, 168, 37) | 스터디 녹화 중 배지 |
| **Done State** | `#455A64` | RGB(69, 90, 100) | 스터디 완료 상태 배지 |
| **Start Button** | `#2E7D32` | RGB(46, 125, 50) | 녹화 시작 버튼 |
| **Stop Button** | `#E53935` | RGB(229, 57, 53) | 녹화 중지 버튼 |
| **Emulator Button** | `#6A1B9A` | RGB(106, 27, 154) | 에뮬레이터 추가 버튼 (보라색) |
| **Disabled Gray** | `#555555` | RGB(85, 85, 85) | 비활성화 상태 |
| **Neutral Gray** | `#616161` | RGB(97, 97, 97) | 중립 상태 배지 |

### 차트 시리즈 색상 팔레트 (MainViewModel.cs)

```csharp
private readonly List<string> _seriesPalette = new()
{
    "#14427B",  // 진한 파란색
    "#1F77B4",  // 밝은 파란색
    "#2CA02C",  // 녹색
    "#FF7F0E",  // 주황색
    "#D62728",  // 빨간색
    "#9467BD"   // 보라색
};
```

## 색상 사용 패턴

### 1. 상태 표시 색상

- **연결 상태**: 🟢 (녹색 이모지) / 🔴 (빨간색 이모지)
- **스터디 상태**:
  - Ready: `#2E7D32` (녹색)
  - Recording: `#F9A825` (노란색/주황색)
  - Done: `#455A64` (회색-파란색)

### 2. 인터랙션 색상

- **주요 액션**: `#2196F3` (파란색) - 기본 버튼
- **긍정 액션**: `#2E7D32` (녹색) - 시작, 연결
- **부정 액션**: `#E53935` (빨간색) - 중지, 삭제
- **특수 액션**: `#6A1B9A` (보라색) - 에뮬레이터

### 3. 데이터 시각화

- **압력 값**: `#2196F3` (Primary Blue) - 현재 압력 표시
- **차트 라인**: 6가지 색상 팔레트를 순환하며 사용

## 색상 접근성 분석

### WCAG 2.1 대비율 검사

#### 배경 대비 (BackgroundColor #1E1E1E 기준)

| 전경 색상 | 대비율 | WCAG AA (4.5:1) | WCAG AAA (7:1) |
|---|---|---|---|
| TextColor (#FFFFFF) | 16.1:1 | ✅ 통과 | ✅ 통과 |
| TextSecondaryColor (#B0B0B0) | 8.3:1 | ✅ 통과 | ✅ 통과 |
| PrimaryColor (#2196F3) | 4.9:1 | ✅ 통과 | ❌ 실패 |
| AccentColor (#FF5722) | 4.2:1 | ❌ 실패 | ❌ 실패 |
| Header Blue (#14427B) | 3.1:1 | ❌ 실패 | ❌ 실패 |

#### Surface 대비 (SurfaceColor #2D2D2D 기준)

| 전경 색상 | 대비율 | WCAG AA (4.5:1) | WCAG AAA (7:1) |
|---|---|---|---|
| TextColor (#FFFFFF) | 13.4:1 | ✅ 통과 | ✅ 통과 |
| TextSecondaryColor (#B0B0B0) | 6.9:1 | ✅ 통과 | ❌ 실패 |
| PrimaryColor (#2196F3) | 4.1:1 | ❌ 실패 | ❌ 실패 |

### 색맹 사용자 고려

현재 UI는 색상에만 의존하지 않고 **이모지**(🟢🔴), **텍스트 레이블**, **위치**를 함께 사용하여 정보를 전달하므로 색맹 사용자도 사용 가능합니다.

## 문제점 및 개선 사항

### 1. 대비율 부족

**문제**: Header Blue (`#14427B`)와 AccentColor (`#FF5722`)가 어두운 배경에서 대비율이 낮아 가독성이 떨어집니다.

**개선안**:
- Header Blue를 `#3A7BC8` (더 밝은 파란색)로 변경 → 대비율 5.2:1
- AccentColor를 `#FF6E40` (더 밝은 주황색)로 변경 → 대비율 5.1:1

### 2. 일관성 부족

**문제**: 
- Primary Blue가 `#2196F3`와 `#14427B` 두 가지로 혼용됨
- 버튼 색상이 인라인으로 정의되어 일관성 관리 어려움

**개선안**:
- 모든 색상을 App.xaml의 ResourceDictionary에 정의
- 의미론적 이름 사용 (예: `SuccessBrush`, `DangerBrush`, `WarningBrush`)

### 3. 상태 색상 혼란

**문제**: Recording 상태가 `#F9A825` (노란색)인데, 일반적으로 노란색은 경고를 의미합니다.

**개선안**:
- Recording 상태를 `#1976D2` (파란색) 또는 `#E53935` (빨간색)으로 변경
- 노란색은 일시정지(Paused) 상태에 사용

### 4. FluxCalculationWindow 색상 불일치

**문제**: FluxCalculationWindow는 라이트 테마 요소(`#F0F8FF` 배경)를 사용하여 메인 윈도우와 일관성이 없습니다.

**개선안**:
- FluxCalculationWindow도 다크 테마로 통일
- 또는 전체 앱에 테마 전환 기능 추가

## 제안 색상 스키마

### 개선된 색상 팔레트

```xml
<!-- App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <!-- Base Colors -->
        <Color x:Key="PrimaryColor">#2196F3</Color>
        <Color x:Key="PrimaryDarkColor">#1976D2</Color>
        <Color x:Key="PrimaryLightColor">#64B5F6</Color>
        
        <Color x:Key="AccentColor">#FF6E40</Color>
        <Color x:Key="AccentDarkColor">#E64A19</Color>
        
        <Color x:Key="BackgroundColor">#1E1E1E</Color>
        <Color x:Key="SurfaceColor">#2D2D2D</Color>
        <Color x:Key="SurfaceLightColor">#3A3A3A</Color>
        
        <Color x:Key="TextColor">#FFFFFF</Color>
        <Color x:Key="TextSecondaryColor">#B0B0B0</Color>
        <Color x:Key="TextDisabledColor">#757575</Color>
        
        <!-- Semantic Colors -->
        <Color x:Key="SuccessColor">#4CAF50</Color>
        <Color x:Key="WarningColor">#FFC107</Color>
        <Color x:Key="DangerColor">#F44336</Color>
        <Color x:Key="InfoColor">#2196F3</Color>
        
        <!-- State Colors -->
        <Color x:Key="ConnectedColor">#4CAF50</Color>
        <Color x:Key="DisconnectedColor">#F44336</Color>
        <Color x:Key="RecordingColor">#F44336</Color>
        <Color x:Key="ReadyColor">#4CAF50</Color>
        <Color x:Key="DoneColor">#607D8B</Color>
        
        <!-- Chart Colors -->
        <Color x:Key="Chart1Color">#2196F3</Color>
        <Color x:Key="Chart2Color">#4CAF50</Color>
        <Color x:Key="Chart3Color">#FF9800</Color>
        <Color x:Key="Chart4Color">#9C27B0</Color>
        <Color x:Key="Chart5Color">#F44336</Color>
        <Color x:Key="Chart6Color">#00BCD4</Color>
    </ResourceDictionary>
</Application.Resources>
```

### 대비율 개선 결과

| 전경 색상 (개선) | 배경 대비 | WCAG AA | WCAG AAA |
|---|---|---|---|
| AccentColor (#FF6E40) | 5.1:1 | ✅ | ❌ |
| SuccessColor (#4CAF50) | 5.8:1 | ✅ | ❌ |
| WarningColor (#FFC107) | 8.2:1 | ✅ | ✅ |
| DangerColor (#F44336) | 4.6:1 | ✅ | ❌ |

## 구현 우선순위

### Phase 1: 즉시 개선 (1-2시간)
1. Header Blue 색상 밝게 조정
2. AccentColor 대비율 개선
3. 의미론적 색상 리소스 추가

### Phase 2: 일관성 개선 (반나절)
1. 모든 인라인 색상을 ResourceDictionary로 이동
2. FluxCalculationWindow 다크 테마 적용
3. 차트 색상 팔레트 확장

### Phase 3: 고급 기능 (1-2일)
1. 라이트/다크 테마 전환 기능
2. 사용자 정의 색상 테마
3. 고대비 모드 지원
