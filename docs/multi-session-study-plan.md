# FLX Viewer: Multi-Session Study Architecture Plan (v2)

## Background

Currently in FLX Viewer, Study is 1:1 mapped as **1 logging = 1 Study**.
In real experiment workflows, multiple logging sessions occur as a series within one experiment project:

```
1 Physical Experiment (= 1 Logical Study)
+-- [Logging 1] Leak Rate check (equipment only) -> vacuum -> pressure rise recording
+-- [Logging 2] Leak Rate check (with specimen) -> vacuum -> pressure rise recording
+-- [Logging 3] Flux measurement -> long-term permeation data logging
```

In the current structure, these 3 become separate Studies, making it impossible to view one experiment's results in one place.

**Goal**: Change Study to be a container for multiple Recording Sessions.
Enable Start/Stop/Continue/Finalize for Study, so multiple logging sessions and analysis results accumulate within one Study.

---

## State Machine

### Complete State Transition Diagram

```
Ready --[StartRecording]--> Recording --[StopRecording]--> Paused
                                |                            |  ^
                                |                            |  |
                                |[StopAndFinalize]           |  +--[StartRecording (Continue)]--> Recording
                                |                            |
                                v                            +--[FinalizeStudy]--> Done (terminal)
                              Done                           |
                                                             +--[AnalyzeStudy]--> (session select -> analyze)
```

### Actions by State

| State | Start | Stop | StopAndFinalize | Continue | Finalize | Analyze | Delete |
|-------|-------|------|-----------------|----------|----------|---------|--------|
| Ready | Y | - | - | - | - | - | Y |
| Recording | - | Y | Y | - | - | - | Y (Stop first) |
| Paused | - | - | - | Y | Y | Y | Y |
| Done | - | - | - | - | - | Y | Y |

> **StopAndFinalize**: Shortcut for single-session workflows. Recording -> Done directly. Equivalent to Stop then Finalize.

---

## Serialization / Restoration Rules

### ToRecord() Mapping
| Runtime State | Persisted StudyState |
|---------------|---------------------|
| Ready | `"Ready"` |
| Recording | `"Paused"` (writer closed on save, same as Paused) |
| Paused | `"Paused"` |
| Done | `"Done"` |

### Constructor Restoration Rules
| Persisted StudyState | Sessions | CsvFilePath | Runtime State |
|---------------------|----------|-------------|---------------|
| `"Paused"` | 1+ | any | Paused |
| `"Done"` | any | any (file exists) | Done |
| `"Done"` | any | any (file missing) | Done + IsBroken |
| `"Ready"` | empty | null | Ready |
| other (legacy) | empty | non-null (file exists) | Done (legacy compat) |
| other (legacy) | empty | non-null (file missing) | Done + IsBroken |

### Legacy Synthetic Session Rule
When Sessions is empty and CsvFilePath is non-null, constructor synthesizes one RecordingSession:
```csharp
Sessions.Add(new RecordingSession
{
    SessionId = "S01",
    Label = "(legacy)",
    CsvFilePath = record.CsvFilePath,
    StartTime = record.Metadata.StartTime ?? record.Metadata.CreatedAt,
    EndTime = record.Metadata.EndTime,
    SampleCount = record.RecordedSampleCount
});
```

---

## StudyMetadata Time Rules

| Field | When Set | Rule |
|-------|----------|------|
| `StartTime` | First session's `StartRecording()` | Set once. Do not overwrite if already has value |
| `EndTime` | `FinalizeStudy()` | Set only on Finalize. Not set on Stop/Pause |

---

## Implementation Plan

### Phase 1: Data Model Changes

#### 1-1. New Model: `RecordingSession`

**File**: `src/HVG2020B.Core/Models/RecordingSession.cs` (NEW)

```csharp
namespace HVG2020B.Core.Models;

public class RecordingSession
{
    public string SessionId { get; set; } = "";
    public string Label { get; set; } = "";
    public string CsvFilePath { get; set; } = "";
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public int SampleCount { get; set; }
}
```

#### 1-2. StudyRecord Extension (Backward Compatible)

**File**: `src/HVG2020B.Core/Models/StudyRecord.cs`

Add field:
```csharp
public List<RecordingSession> Sessions { get; set; } = new();
```

Keep `CsvFilePath` for legacy compatibility.

#### 1-3. StudyState Enum Extension

**File**: `src/HVG2020B.Viewer/ViewModels/StudyItem.cs`

```csharp
public enum StudyState { Ready, Recording, Paused, Done }
```

#### 1-4. FluxAnalysisResult Extension

**File**: `src/HVG2020B.Core/Models/FluxAnalysisResult.cs`

Add field:
```csharp
public string? SessionId { get; set; }
```

#### 1-5. App.xaml Resource Addition

**File**: `src/HVG2020B.Viewer/App.xaml`

```xml
<Color x:Key="PausedColor">#FF9800</Color>
<SolidColorBrush x:Key="PausedBrush" Color="{StaticResource PausedColor}"/>
```

---

### Phase 2: StudyItem Logic Changes

**File**: `src/HVG2020B.Viewer/ViewModels/StudyItem.cs`

#### 2-1. New Fields
- `RecordingSession? _currentSession`
- `ObservableCollection<RecordingSession> Sessions`

#### 2-2. StartRecording Changes
- Accept `Paused` state (for Continue)
- Create folder only on first session
- Create new `RecordingSession` per call with auto label "Session N"
- `StartTime` set only once (first session)
- `RecordedSampleCount` not reset on resume
- `_logTickCounters` cleared on each start (prevent data skip on resume)

#### 2-3. StopRecording -> Paused
- Close writer, add session to collection
- Do NOT set `Metadata.EndTime`

#### 2-4. FinalizeStudy()
- Set `Metadata.EndTime`
- State -> Done

#### 2-5. StopAndFinalize() (Shortcut)
- StopRecording() then FinalizeStudy()

#### 2-6. TryWriteReading
- Increment `_currentSession.SampleCount`
- Compute `RecordedSampleCount` from all sessions

#### 2-7. ToRecord / Constructor
- Serialize Sessions list
- Restore Paused state
- Legacy synthetic session

#### 2-8. Dispose
- Paused: writer already null, safe

---

### Phase 3: MainViewModel Changes

**File**: `src/HVG2020B.Viewer/ViewModels/MainViewModel.cs`

#### 3-1. StartStudyRecording: Allow Paused (Continue)
#### 3-2. StopStudyRecording: Auto Paused
#### 3-3. New: FinalizeStudyCommand
#### 3-4. New: StopAndFinalizeStudyCommand (Shortcut)
#### 3-5. AnalyzeStudy: Session selection dialog, per-session analysis
#### 3-6. DeleteStudy: Paused handling (writer already closed, safe)
#### 3-7. Excel Export: Pass sessions list
#### 3-8. Dispose: Paused studies safe (not in _recordingStudies)

**Analysis scope**: Always per-session. Cross-session merge is future work.
Therefore `StudyCsvParser` requires no changes.

---

### Phase 4: UI (MainWindow.xaml) Changes

**File**: `src/HVG2020B.Viewer/MainWindow.xaml`

#### 4-1. Button Changes by State
```
Ready     -> [Start]
Recording -> [Pause] [Stop & Done]
Paused    -> [Continue] [Done] [Analyze]
Done      -> [Analyze]
```

#### 4-2. Session Count Display (Recording/Paused/Done)
#### 4-3. Paused Badge Color (PausedBrush)
#### 4-4. Analyze + Open Folder: Enable for Paused

---

### Phase 5: StudyExcelExporter Extension

**File**: `src/HVG2020B.Viewer/Services/StudyExcelExporter.cs`

- Add `IReadOnlyList<RecordingSession>? sessions` parameter
- Per-session logging data tabs
- `WriteLoggingDataSheet` overload with sheet name parameter

---

## File Change Summary

| # | File | Change | Size |
|---|------|--------|------|
| 1 | `Core/Models/RecordingSession.cs` | **NEW** | S |
| 2 | `Core/Models/StudyRecord.cs` | Add Sessions field | S |
| 3 | `Core/Models/FluxAnalysisResult.cs` | Add SessionId field | S |
| 4 | `Viewer/App.xaml` | PausedColor/PausedBrush | S |
| 5 | `Viewer/ViewModels/StudyItem.cs` | Paused state, multi-session, serialization | **L** |
| 6 | `Viewer/ViewModels/MainViewModel.cs` | New commands, session selection, Paused handling | **L** |
| 7 | `Viewer/MainWindow.xaml` | Paused buttons, session count, badge, toolbar | M |
| 8 | `Viewer/Services/StudyExcelExporter.cs` | Sessions parameter, per-session tabs | S |

### No Changes Required

| File | Reason |
|------|--------|
| `Core/StudyCsvParser.cs` | Per-session analysis, single file interface unchanged |
| `Core/Models/StudyMetadata.cs` | No field changes (time rules in StudyItem logic) |
| `Core/Services/StudyStore.cs` | JSON auto-handles new Sessions field |
| `Viewer/ViewModels/ViewerState.cs` | App-level state, independent of Study state |
| `Viewer/FluxCalculationWindow.*` | Receives per-session data, no change needed |
| `Viewer/DeviceSelectionDialog.*` | Reused for session selection, no change needed |

---

## Backward Compatibility

1. Existing `studies.json` with no Sessions -> constructor creates synthetic session
2. Existing Done studies load as Done
3. Existing single-CSV analysis works via synthetic session
4. Existing simultaneous multi-Study recording unchanged
5. `RecordedSampleCount` property name preserved for XAML binding compat

---

## Test Scenarios

1. **Legacy Study Load**: Existing studies.json (no Sessions) -> synthetic session -> works
2. **Single Session + StopAndFinalize**: Start -> Stop&Done -> Done, Sessions.Count==1
3. **Single Session + Pause/Finalize**: Start -> Pause -> Finalize -> Done, Sessions.Count==1
4. **Multi Session**: Start -> Pause -> Continue -> Pause -> Finalize -> Sessions.Count==2, 2 CSVs
5. **Concurrent + Multi Session**: Study A & B recording, A pause/continue, B stop&done
6. **Analyze from Paused**: Session selection dialog -> analysis works
7. **App Restart with Paused**: Pause -> close app -> reopen -> Paused restored -> Continue works
8. **Excel Export**: Multi-session -> per-session logging tabs

---

## Implementation Order

1. `RecordingSession.cs` model (Phase 1-1)
2. `StudyRecord.cs` Sessions field (Phase 1-2)
3. `FluxAnalysisResult.cs` SessionId (Phase 1-4)
4. `App.xaml` PausedColor/PausedBrush (Phase 1-5)
5. `StudyItem.cs` full refactor (Phase 2)
6. `MainViewModel.cs` commands + Paused handling (Phase 3)
7. `MainViewModel.cs` AnalyzeStudy session selection + Excel export (Phase 3-5, 3-7)
8. `MainWindow.xaml` UI changes (Phase 4)
9. `StudyExcelExporter.cs` sessions support (Phase 5)
10. Backward compat test (legacy studies.json load)
11. `dotnet build` + `dotnet test` pass
