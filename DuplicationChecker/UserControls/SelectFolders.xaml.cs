using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DuplicationChecker.UserControls;

[INotifyPropertyChanged]
public partial class SelectFolders : Window
{
    public SelectFolders()
    {
        InitializeComponent();
        DataContext = this;
    }

    [ObservableProperty]
    private ObservableCollection<Folder> _Folders = new();
    [ObservableProperty]
    private string _ExtensionPattern;
    [ObservableProperty]
    private bool _IsClearPreviouseFolder = true;
    [ObservableProperty]
    private Folder _SelectedFolder;

    /// <summary>
    /// https://github.com/ookii-dialogs/ookii-dialogs-wpf#vista-style-common-file-dialogs
    /// </summary>
    [RelayCommand]
    private void SelectFolder()
    {
        var vfbd = new VistaFolderBrowserDialog();
        var result = vfbd.ShowDialog();
        if (result.HasValue && result.Value)
        {
            Folders.Add(new Folder(vfbd.SelectedPath));
        }
    }

    [RelayCommand]
    private void RemoveFolder()
    {
        if (SelectedFolder !=null){
            Folders.Remove(SelectedFolder);
        }
    }

    private void okButton_Click(object sender, RoutedEventArgs e)=> DialogResult = true;
    

    private void cancelButton_Click(object sender, RoutedEventArgs e)=> DialogResult = false;

    private void RemoveFolder(object sender, RoutedEventArgs e)
    {
        RemoveFolder();
    }
}
