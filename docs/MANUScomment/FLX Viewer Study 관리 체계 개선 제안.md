# FLX Viewer Study 관리 체계 개선 제안

**작성자**: Manus AI
**날짜**: 2026년 2월 18일

## 1. 개요

본 문서는 FLX Viewer 애플리케이션의 Study 관리, 데이터 영속화, 그리고 사용자 경험(UX) 개선을 위한 종합적인 분석 및 제안을 담고 있습니다. 현재 시스템의 구조를 분석하고, LabVIEW, Origin과 같은 유사 계측 소프트웨어의 모범 사례와 최신 데스크톱 애플리케이션의 UX 패턴을 참고하여, 데이터의 안정성과 사용자 편의성을 모두 향상시킬 수 있는 실용적인 방안을 제시하는 것을 목표로 합니다.

## 2. 현행 구조 분석

`feature/multi-gauge-connection` 브랜치의 소스 코드 분석 결과, 현재 Study 관리 체계는 다음과 같이 요약할 수 있습니다.

| 항목 | 현재 구현 방식 |
|---|---|
| **Study ID** | `StudyId` (자동 생성: `STD-YYYYMMDD-HHmmss`) |
| **Study 이름** | `Title` (사용자 입력) |
| **폴더 구조** | `logs/{Title}/` (동일 `Title` 입력 시 폴더 충돌 발생) |
| **데이터 파일** | - `logs/{Title}/{StudyId}.csv`<br>- `logs/{Title}/{Title}.xlsx` (추정)<br>- `logs/{Title}/{StudyId}_analysis{N}.png` |
| **메타데이터** | `logs/studies.json` (모든 Study의 메타데이터와 분석 결과를 단일 파일로 관리) |

이 구조는 초기 단계에서는 직관적이지만, Study의 수가 증가하고 동일한 이름의 Study가 생성될 경우 데이터 무결성 문제가 발생할 잠재적 위험을 내포하고 있습니다.

## 3. 핵심 개선 제안

사용자의 검토 요청 사항과 리서치 결과를 바탕으로, 세 가지 주요 영역에 대한 개선안을 제안합니다.

### 3.1. Study 이름/ID/폴더 관리 체계

사용자 편의성과 데이터 안전성의 균형을 맞추기 위해, 내부 식별자와 사용자 표시 이름을 명확히 분리하고 폴더 구조를 재설계할 것을 제안합니다.

| 구분 | 현행 방식 | 제안 방식 | 기대 효과 |
|---|---|---|---|
| **고유 식별자** | `StudyId` (시간 기반) | **`StudyGuid`** (전역 고유 식별자, 예: `Guid.NewGuid()`) | 시간 기반 ID의 잠재적 충돌 가능성을 원천적으로 차단하고, 시스템의 모든 데이터 참조를 위한 안정적인 기본 키(Primary Key)를 확보합니다. |
| **사용자 표시 ID** | 없음 | **`StudyId`** (현재와 동일: `STD-YYYYMMDD-HHmmss`) | 사용자가 식별하기 쉬운 ID를 보조적으로 유지하여 편의성을 제공합니다. |
| **Study 이름** | `Title` (중복 불가) | **`Title`** (중복 허용) | 사용자가 자유롭게 Study 이름을 지정할 수 있도록 하여 편의성을 극대화합니다. 중복 이름은 시스템이 자동으로 처리합니다. |
| **폴더 구조** | `logs/{Title}/` | **`logs/{StudyGuid}/`** | 고유 식별자를 폴더명으로 사용하여 이름 중복으로 인한 파일 덮어쓰기 문제를 완벽하게 해결하고 데이터 무결성을 보장합니다. |
| **파일 명명** | `{StudyId}.csv` | `{StudyId}.csv` (유지) | 파일 이름은 사용자가 폴더 탐색 시에도 내용을 유추할 수 있도록 현재의 가독성 높은 ID를 유지합니다. |

**중복 이름 처리 UX**: 사용자가 기존과 동일한 `Title`로 새 Study를 생성할 경우, 백그라운드에서는 새로운 `StudyGuid`가 할당되어 별도의 폴더에 저장됩니다. UI에서는 동일한 이름의 Study들이 목록에 모두 표시되며, 생성 시간이나 `StudyId`와 같은 추가 정보로 구분할 수 있도록 합니다. 이는 Windows 파일 탐색기에서 동일 이름의 파일을 생성하면 `(2)`가 붙는 것과 유사한 사용자 경험을 제공합니다 [3].

### 3.2. Flux Results Dashboard UX

대시보드의 정보 탐색 효율을 높이기 위해 **세션(Session) 개념**을 도입하고, 일반적인 데스크톱 앱의 **Master-Detail UX 패턴**을 적용할 것을 제안합니다.

> **세션(Session) 개념 도입**: Origin 소프트웨어의 'Project'나 LabVIEW의 'Project'와 같이, 여러 개의 관련 Study를 그룹화하는 상위 개념입니다 [2]. 예를 들어, '특정 장비 성능 테스트' 세션 아래에 여러 번의 개별 측정 Study를 포함할 수 있습니다. 이는 데이터를 더욱 체계적으로 관리하고 탐색하는 데 도움을 줍니다.

**UX 개선 방안**:

1.  **Master-Detail 뷰 적용**: 대시보드 화면을 분할하여, 왼쪽에는 전체 분석 결과(`FluxAnalysisResult`) 목록을 `DataGrid`로 표시하고, 오른쪽에는 선택된 항목의 상세 정보를 표시합니다. 이는 WPF의 표준적인 Master-Detail 패턴을 따릅니다 [4].
2.  **행(Row) 클릭 시 상호작용**: 
    *   **단일 클릭**: 해당 행을 선택하고, 오른쪽 상세 뷰에 관련 정보(계산 파라미터, 결과 요약, 분석 차트 이미지 등)를 표시합니다.
    *   **더블 클릭**: 해당 분석이 포함된 Study의 상세 화면으로 이동하거나, 분석에 사용된 원본 CSV 파일을 여는 등의 빠른 작업을 위한 단축키로 활용합니다. 명시적인 '상세 보기' 버튼을 함께 제공하여 기능의 발견 가능성을 높이는 것이 중요합니다 [5].
3.  **데이터 필터링 및 정렬**: `DataGrid`의 각 컬럼 헤더에 필터링 및 정렬 기능을 추가하여 사용자가 원하는 데이터를 쉽게 찾을 수 있도록 지원합니다.

### 3.3. Study 생명주기 관리

애플리케이션의 상태와 디스크의 파일 상태를 일관되게 유지하고, 데이터 삭제 및 정리를 위한 안정적인 정책을 수립합니다.

**동기화 전략**:
- **단일 진실 공급원(Single Source of Truth)**: `studies.json` 파일(또는 향후 도입될 데이터베이스)을 모든 메타데이터의 마스터로 간주합니다. 앱 시작 시 이 파일을 로드하여 상태를 구성합니다.
- **파일 유효성 검사**: 앱 시작 시 또는 주기적으로 백그라운드에서 메타데이터에 기록된 파일 경로(`CsvFilePath` 등)가 실제로 존재하는지 확인하고, 파일이 없는 경우 UI에 '파일 없음' 또는 '경로 오류'와 같은 상태를 표시하여 사용자에게 알립니다.

**삭제 처리**: 데이터 복구 가능성과 저장 공간 효율성을 모두 고려하여 **Soft Delete**를 기본 정책으로 채택할 것을 제안합니다 [6].

- **Soft Delete (논리적 삭제)**: 사용자가 Study를 삭제하면, 실제 파일을 삭제하는 대신 메타데이터에 `IsDeleted = true`와 같은 플래그를 설정합니다. UI에서는 이 플래그를 기준으로 해당 Study를 목록에서 숨깁니다. 이는 실수로 인한 삭제를 방지하고, 필요한 경우 쉽게 복구할 수 있는 장점이 있습니다.
- **Hard Delete (물리적 삭제)**: '휴지통 비우기'와 같이 별도의 관리 기능을 통해 사용자가 명시적으로 영구 삭제를 선택할 때만 파일과 메타데이터를 완전히 제거합니다.

**Orphan 데이터 정리**: 메타데이터는 삭제되었으나 파일 시스템에 남아있는 '고아 파일'을 정리하기 위해, 앱 종료 또는 특정 시점에 주기적으로 메타데이터에 존재하지 않는 Study 폴더(`logs/{StudyGuid}/`)를 찾아내어 지정된 임시 폴더로 이동시키거나 사용자에게 삭제를 제안하는 기능을 추가할 수 있습니다.

## 4. 결론 및 제언

제안된 개선안들은 FLX Viewer의 확장성과 안정성을 크게 향상시키면서, 사용자에게는 더 직관적이고 유연한 경험을 제공할 것입니다. 특히 **고유 ID(`StudyGuid`) 기반의 폴더 관리**와 **Soft Delete 정책**은 데이터 무결성을 확보하는 데 핵심적인 역할을 할 것입니다. 또한 **세션 개념**과 **Master-Detail UX** 도입은 향후 데이터 관리 기능이 고도화될 수 있는 기반을 마련할 것입니다.

## 5. 참고 자료

[1] National Instruments. (2023). *Writing Data-Management-Ready TDMS Files*. https://www.ni.com/en/support/documentation/supplemental/12/writing-data-management-ready-tdms-files.html
[2] OriginLab. (n.d.). *The Origin Project File*. https://www.originlab.com/doc/Origin-Help/Origin-Project-File
[3] Spolsky, J. (2004). *User Interface Design for Programmers*. Apress.
[4] Microsoft. (2021). *How to: Use the Master-Detail Pattern with Hierarchical Data*. https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-use-the-master-detail-pattern-with-hierarchical-data
[5] UX Stack Exchange. (2014). *Can I make double-clicking on a table row to open a details view intuitive?*. https://ux.stackexchange.com/questions/58598/can-i-make-double-clicking-on-a-table-row-to-open-a-details-view-intuitive
[6] Bisht, S. S. (2023). *Understanding Soft Delete and Hard Delete in Software Development*. Medium. https://surajsinghbisht054.medium.com/understanding-soft-delete-and-hard-delete-in-software-development-best-practices-and-importance-539a935d71b5
