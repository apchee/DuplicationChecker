using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Media;

namespace DuplicationChecker.UserControls;

[INotifyPropertyChanged]
public partial class FileSummary
{
    public FileSummary() { }
    public int Id { get; set; }
    [ObservableProperty]
    private bool _IsChecked = false;    
    [ObservableProperty]
    private string? _FileName;
    [ObservableProperty]
    private float _Size;
    [ObservableProperty]
    private int _Group;
    [ObservableProperty]
    private string? _Extension;
    [ObservableProperty]
    private DateTime _LastModifiedDate;
    [ObservableProperty]
    private DateTime _CreatedDate;
    [ObservableProperty]
    private string? _HashCode;
    [ObservableProperty]
    private string? _FullName;
    [ObservableProperty]
    private string? _Directory;
    [ObservableProperty]
    private string? _SelectedFolder;
    [ObservableProperty]
    private Color _BackgroupColor;
    [ObservableProperty]
    private double? _TimeElipsed;

    [ObservableProperty]
    private string _NewFileName;
    [ObservableProperty]
    private bool _IsNew = false;
}
