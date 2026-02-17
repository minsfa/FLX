# FLX UI 디자인 패턴 분석

## UI 구조 개요

### MainWindow 레이아웃

```
┌─────────────────────────────────────────────────────────────────┐
│  HVG-2020B Viewer                                               │
├─────────────┬─────────────┬───────────────────────────────────┤
│  Devices    │  Studies    │  Chart Area                       │
│  (300px)    │  (300px)    │  (Flexible)                       │
│             │             │                                   │
│  🟢 Dev1    │  Study 1    │  ┌─────────────────────────────┐ │
│  7.66E+02   │  [Ready]    │  │                             │ │
│  📈 🗑      │  ▶ Start    │  │   Pressure vs Time          │ │
│             │             │  │   (ScottPlot Chart)         │ │
│  🔴 Dev2    │  Study 2    │  │                             │ │
│  ---        │  [Recording]│  │                             │ │
│  📈 🗑      │  ■ Stop     │  └─────────────────────────────┘ │
│             │             │                                   │
│  🔍 연결감지 │  + New Study│  [Log Scale] [Clear] [Export]  │
│  🧪 Emulator│             │                                   │
└─────────────┴─────────────┴───────────────────────────────────┘
```

### FluxCalculationWindow 레이아웃

```
┌─────────────────────────────────────────────────────────────────┐
│  Permeance & Flux Calculation                                   │
├─────────────────────┬───────────────────────────────────────────┤
│  Input Parameters   │  Pressure vs Time Chart                   │
│  ┌────────────────┐ │  ┌─────────────────────────────────────┐ │
│  │ Membrane Area  │ │  │                                     │ │
│  │ Temperature    │ │  │   [Linear Regression Line]          │ │
│  │ Feed Pressure  │ │  │   [Selected Time Range]             │ │
│  │ Chamber Volume │ │  │                                     │ │
│  └────────────────┘ │  └─────────────────────────────────────┘ │
│                     │                                           │
│  Time Range         │  Shift+Click: Set Start                   │
│  ┌────────────────┐ │  Ctrl+Click: Set End                      │
│  │ Start: [====] │ │                                           │
│  │ End:   [====] │ │                                           │
│  └────────────────┘ │                                           │
│                     │                                           │
│  Results            │                                           │
│  ┌────────────────┐ │                                           │
│  │ Flux: 1.23E-5  │ │                                           │
│  │ Permeance: ... │ │                                           │
│  │ R²: 0.9987     │ │                                           │
│  └────────────────┘ │                                           │
│                     │                                           │
│  [Calculate] [CSV] │                                           │
└─────────────────────┴───────────────────────────────────────────┘
```

## 디자인 패턴 분석

### 1. 레이아웃 패턴

#### ✅ 장점
- **3열 그리드 레이아웃**: 명확한 정보 계층 구조
- **고정 폭 사이드바**: 디바이스 및 스터디 목록이 일정한 공간 확보
- **유연한 차트 영역**: 창 크기에 따라 자동 조정

#### ⚠️ 개선 필요
- **반응형 디자인 부족**: 작은 화면에서 3열이 너무 좁아질 수 있음
- **최소 너비 제약**: MinWidth="900"이지만, 각 패널의 최소 너비가 명시되지 않음
- **스크롤 영역 불명확**: 디바이스/스터디 목록이 많을 때 스크롤 동작이 명확하지 않음

### 2. 시각적 계층 구조

#### 현재 구조
```
Level 1: Window Background (#1E1E1E)
  └─ Level 2: Surface Panels (#2D2D2D)
      └─ Level 3: Card Items (#1E1E1E)
          └─ Level 4: Content
```

#### ✅ 장점
- 명확한 시각적 깊이 표현
- 카드 기반 UI로 정보 그룹화

#### ⚠️ 개선 필요
- **그림자 효과 부족**: 깊이감이 색상 차이로만 표현됨
- **호버 효과 부족**: 인터랙티브 요소의 피드백이 약함

### 3. 타이포그래피

#### 현재 사용 폰트 크기

| 요소 | 크기 | 용도 |
|---|---|---|
| 헤더 | 14pt | "Devices", "Studies" |
| 일반 텍스트 | 12pt (기본) | 대부분의 UI 텍스트 |
| 압력 값 | 18pt Bold | 현재 압력 표시 |
| 결과 값 | 14pt Bold | Flux 계산 결과 |
| 보조 정보 | 11pt | 포트명, 스터디 ID |

#### ⚠️ 개선 필요
- **폰트 크기 일관성 부족**: 명시적인 타이포그래피 스케일 없음
- **폰트 패밀리 미지정**: 시스템 기본 폰트 사용
- **가독성**: 작은 폰트(11pt)가 다크 배경에서 읽기 어려울 수 있음

### 4. 아이콘 사용

#### 현재 사용 아이콘
- 🟢🔴 연결 상태
- 📈 차트 토글
- 🗑 삭제
- 🔍 스캔
- 🧪 에뮬레이터
- ▶ 시작
- ■ 중지
- ✕ 닫기

#### ✅ 장점
- 이모지 사용으로 빠른 구현
- 직관적인 의미 전달

#### ⚠️ 개선 필요
- **일관성 부족**: 이모지와 유니코드 기호 혼용
- **크기 조정 불가**: 이모지는 폰트 크기에 종속
- **플랫폼 의존성**: 이모지 렌더링이 OS마다 다름
- **권장 사항**: Material Design Icons 또는 Fluent UI Icons 사용

### 5. 상태 표시 패턴

#### 디바이스 상태
```
🟢 Device-001  COM3
7.666E+02 Torr
[📈] [🗑]
```

#### 스터디 상태
```
Study Title
study-id-123
[Ready/Recording/Done] | Device-001, Device-002
1,234 samples | path/to/file.csv
[▶ Start] / [■ Stop] [✕]
```

#### ✅ 장점
- 색상 + 텍스트 + 아이콘으로 다중 표현
- 상태별 버튼 동적 표시/숨김

#### ⚠️ 개선 필요
- **진행률 표시 부족**: Recording 상태에서 시간 경과나 샘플 수 증가가 실시간으로 보이지 않음
- **에러 상태 부재**: 연결 실패, 쓰기 오류 등의 에러 상태 표시 없음

### 6. 인터랙션 패턴

#### 버튼 스타일

| 버튼 유형 | 배경색 | 호버 색상 | 용도 |
|---|---|---|---|
| Primary | #2196F3 | #1976D2 | 기본 액션 |
| Success | #2E7D32 | - | 시작, 연결 |
| Danger | #E53935 | - | 중지, 삭제 |
| Special | #6A1B9A | - | 에뮬레이터 |
| Disabled | #555555 | - | 비활성화 |

#### ⚠️ 개선 필요
- **호버 효과 불완전**: 일부 버튼만 호버 색상 정의
- **포커스 표시 없음**: 키보드 네비게이션 지원 부족
- **클릭 피드백 없음**: 눌림 효과(pressed state) 없음

### 7. 입력 컨트롤

#### TextBox 스타일
- 기본 WPF 스타일 사용
- 다크 테마에 맞지 않는 라이트 배경

#### Slider 스타일
- 기본 WPF 스타일 사용
- FluxCalculationWindow에서 시간 범위 선택에 사용

#### CheckBox 스타일
- 기본 WPF 스타일 사용
- 스터디 생성 시 디바이스 선택

#### ⚠️ 개선 필요
- **일관성 없는 스타일**: 모든 입력 컨트롤이 기본 스타일 사용
- **다크 테마 미적용**: 입력 필드가 흰색 배경으로 표시됨
- **검증 피드백 없음**: 잘못된 입력에 대한 시각적 피드백 부재

## UI/UX 문제점 요약

### 심각도: 높음 🔴

1. **FluxCalculationWindow 테마 불일치**: 라이트 테마 요소가 다크 테마 앱에 혼재
2. **입력 컨트롤 스타일 부재**: TextBox, Slider 등이 기본 스타일로 표시
3. **에러 상태 표시 없음**: 연결 실패, 파일 쓰기 오류 등의 피드백 부재

### 심각도: 중간 🟡

4. **호버/포커스 효과 불완전**: 일부 인터랙티브 요소만 피드백 제공
5. **타이포그래피 일관성 부족**: 폰트 크기와 굵기가 체계적이지 않음
6. **아이콘 시스템 부재**: 이모지 사용으로 일관성 및 확장성 제한
7. **반응형 디자인 부족**: 작은 화면에서 레이아웃 문제 가능성

### 심각도: 낮음 🟢

8. **그림자 효과 부족**: 시각적 깊이감 약함
9. **애니메이션 부재**: 상태 전환 시 부드러운 전환 없음
10. **키보드 네비게이션 미흡**: 접근성 고려 부족

## 개선 제안

### 1. 통합 디자인 시스템 구축

```xml
<!-- Styles.xaml (새 파일) -->
<ResourceDictionary>
    <!-- Typography Scale -->
    <Style x:Key="H1" TargetType="TextBlock">
        <Setter Property="FontSize" Value="24"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
    </Style>
    
    <Style x:Key="H2" TargetType="TextBlock">
        <Setter Property="FontSize" Value="18"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
    </Style>
    
    <Style x:Key="Body" TargetType="TextBlock">
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
    </Style>
    
    <Style x:Key="Caption" TargetType="TextBlock">
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
    </Style>
    
    <!-- Button Styles -->
    <Style x:Key="PrimaryButton" TargetType="Button">
        <Setter Property="Background" Value="{StaticResource PrimaryBrush}"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="Padding" Value="16,8"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border"
                            Background="{TemplateBinding Background}"
                            CornerRadius="4"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="{StaticResource PrimaryDarkBrush}"/>
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="border" Property="RenderTransform">
                                <Setter.Value>
                                    <ScaleTransform ScaleX="0.98" ScaleY="0.98"/>
                                </Setter.Value>
                            </Setter>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="border" Property="Background" Value="#555555"/>
                            <Setter Property="Foreground" Value="#888888"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    
    <!-- TextBox Style -->
    <Style x:Key="DarkTextBox" TargetType="TextBox">
        <Setter Property="Background" Value="{StaticResource SurfaceLightBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="BorderBrush" Value="#555555"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="8,6"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TextBox">
                    <Border x:Name="border"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="4">
                        <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsFocused" Value="True">
                            <Setter TargetName="border" Property="BorderBrush" Value="{StaticResource PrimaryBrush}"/>
                            <Setter TargetName="border" Property="BorderThickness" Value="2"/>
                        </Trigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="BorderBrush" Value="#777777"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    
    <!-- Card Style -->
    <Style x:Key="Card" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
        <Setter Property="CornerRadius" Value="8"/>
        <Setter Property="Padding" Value="16"/>
        <Setter Property="Effect">
            <Setter.Value>
                <DropShadowEffect Color="Black" Opacity="0.3" BlurRadius="10" ShadowDepth="2"/>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>
```

### 2. 아이콘 라이브러리 도입

**권장**: MahApps.Metro.IconPacks 또는 Material Design In XAML Toolkit

```xml
<!-- NuGet 패키지 설치 -->
<!-- MahApps.Metro.IconPacks -->

<!-- 사용 예시 -->
<iconPacks:PackIconMaterial Kind="Connection" Foreground="Green"/>
<iconPacks:PackIconMaterial Kind="ChartLine" Foreground="{StaticResource PrimaryBrush}"/>
<iconPacks:PackIconMaterial Kind="Delete" Foreground="Red"/>
```

### 3. 상태 피드백 개선

```xml
<!-- 에러 메시지 표시 -->
<Border Background="#D32F2F" CornerRadius="4" Padding="12,8" Margin="0,8,0,0">
    <Border.Style>
        <Style TargetType="Border">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding HasError}" Value="True">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Border.Style>
    <StackPanel Orientation="Horizontal">
        <iconPacks:PackIconMaterial Kind="AlertCircle" Foreground="White" Margin="0,0,8,0"/>
        <TextBlock Text="{Binding ErrorMessage}" Foreground="White"/>
    </StackPanel>
</Border>

<!-- 로딩 인디케이터 -->
<ProgressBar IsIndeterminate="True" Height="4" Margin="0,8,0,0">
    <ProgressBar.Style>
        <Style TargetType="ProgressBar">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsLoading}" Value="True">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </ProgressBar.Style>
</ProgressBar>
```

### 4. FluxCalculationWindow 다크 테마 적용

```xml
<!-- FluxCalculationWindow.xaml 수정 -->
<Window Background="{StaticResource BackgroundBrush}">
    <Grid Margin="16">
        <!-- GroupBox 스타일 재정의 -->
        <GroupBox Header="Input Parameters" Margin="0,0,0,16">
            <GroupBox.Style>
                <Style TargetType="GroupBox">
                    <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
                    <Setter Property="BorderBrush" Value="#555555"/>
                    <Setter Property="BorderThickness" Value="1"/>
                    <Setter Property="Padding" Value="12"/>
                </Style>
            </GroupBox.Style>
            <!-- ... -->
        </GroupBox>
        
        <!-- Results 배경 변경 -->
        <GroupBox Header="Results" Background="{StaticResource SurfaceColor}">
            <!-- ... -->
        </GroupBox>
    </Grid>
</Window>
```

## 참고 디자인 시스템

- [Material Design 3](https://m3.material.io/)
- [Fluent Design System](https://www.microsoft.com/design/fluent/)
- [Atlassian Design System](https://atlassian.design/)
- [Carbon Design System](https://carbondesignsystem.com/)
