using CommunityToolkit.Mvvm.ComponentModel;
using HVG2020B.Core.Models;

namespace HVG2020B.Viewer.ViewModels;

public partial class StudyItem : ObservableObject
{
    public StudyItem(StudyMetadata metadata)
    {
        Metadata = metadata;
    }

    public StudyMetadata Metadata { get; }

    public string StudyId => Metadata.StudyId;

    public string Title => Metadata.Title;

    public string DevicesSummary => string.Join(", ", Metadata.DeviceIds);

    [ObservableProperty]
    private StudyState _state = StudyState.Ready;
}

public enum StudyState
{
    Ready,
    Recording,
    Done
}
