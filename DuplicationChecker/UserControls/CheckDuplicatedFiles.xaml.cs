using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuplicationChecker.Tools.Extension;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace DuplicationChecker.UserControls;



[INotifyPropertyChanged]
public partial class CheckDuplicatedFiles : UserControl
{        
    public CheckDuplicatedFiles()
    {
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>
    /// Only has checksum calculated item can be put to _Raw_Files for saving time
    /// </summary>
    private Dictionary<int, FileSummary> _RawFiles = new();

    /// <summary>
    /// Folrders user selects
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Folder> _SelectedFolders = new ObservableCollection<Folder>();

    /// <summary>
    /// Collection of Directories of the files relative to user's selected folders.
    /// This is just for filtering files
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Folder> _Directories = new ObservableCollection<Folder>();

    /// <summary>
    /// Pattern for loading file recursively
    /// </summary>
    private string _ExtensionPattern = "*";
    Dictionary<string, List<FileSummary>> _GroupByHash = new();

    /// <summary>
    /// All files loaded
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<FileSummary> _Files = new ObservableCollection<FileSummary>();
    [ObservableProperty]
    private FileSummary _SelectedFile;
    [ObservableProperty]
    private int? _TotalFiles;
    [ObservableProperty]
    private int? _ProcessingCount;
    [ObservableProperty]
    private int _DuplicationFileNum;
    [ObservableProperty]
    private int? _MinGroupId;
    [ObservableProperty]
    private int? _MaxGroupId;
    [ObservableProperty]
    private bool _WithChecksum=true;

    [ObservableProperty]
    private string _KeyWords;

    private bool _IsRunning = false;
    private bool _IsCancled = false;
    private async void CheckDuplication(object sender, RoutedEventArgs e)
    {
        ProcessingCount = 0;
        TotalFiles = 0;
        _IsCancled = false;
        WithChecksum = true;
        _IsRunning = true;
        foreach (var folder in _SelectedFolders)
        {
            await LoadFiles(folder.FolderName);            
        }
        ReloadRemainingDuplication();
        _IsRunning = false;
        CollectDirectories();
    }

    [RelayCommand]
    private async void LoadFiles()
    {        
        _IsCancled = false;
        WithChecksum = false;
        Reset();
        foreach (var folder in _SelectedFolders)
        {
            await LoadFiles(folder.FolderName);
        }
        //var result = (from item in Files orderby item.Directory select item).ToList();
        //Files.Clear();
        //Files.AddRange(result);
        TotalFiles = Files.Count;
        ProcessingCount = Files.Count;
        CollectDirectories();
    }

    [RelayCommand]
    private void ClearFiles()
    {
        _IsCancled = true;
        Reset();
    }

    [ObservableProperty]
    private string _SrcText;
    [ObservableProperty]
    private string _DestText;

    [ObservableProperty]
    private bool _IsRegex = false;
    [ObservableProperty]
    private bool _ConfirmRename = false;


    [RelayCommand]
    private void RenameSelectedFileName()
    {
        if(SrcText == DestText) return;
        if(string.IsNullOrEmpty(SrcText) || SrcText.Trim().Length == 0) return;
        Rename(IsRegex);
        //SrcText = "";
    }
    [RelayCommand]
    private void RegularRenameSelectedFileName()
    {
        foreach (var item in Files)
        {
            if (!item.IsNew || !item.IsChecked) continue;
            var newFullFileName = Path.Combine(item.Directory, item.NewFileName);
            File.Move(item.FullName, newFullFileName);
            item.FileName = item.NewFileName;
            item.FullName = newFullFileName;
            item.NewFileName = null;
            item.IsNew = false;
        }
        ConfirmRename = false;
    }

    private void Rename(bool isRegex=false)
    {
        foreach (var item in Files)
        {
            if (!item.IsChecked) continue;
            if (!isRegex)
            {
                if (!item.FileName.Contains(SrcText))
                    continue;
            }
            try
            {
                string newFileName = null;
                if (!isRegex)
                {
                    newFileName = item.FileName.Replace(SrcText, DestText);
                    var newFullFileName = Path.Combine(item.Directory, newFileName);
                    File.Move(item.FullName, newFullFileName);
                    item.FileName = newFileName;
                    item.FullName = newFullFileName;
                }
                else
                {
                    Regex regex = new Regex(SrcText);
                    newFileName = regex.Replace(item.FileName, DestText);
                    if (newFileName == item.FileName)
                        continue;
                    item.NewFileName = newFileName;
                    item.IsNew = true;
                    continue;
                }                
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }
        if (isRegex)
        {
            var needConfirm = Files.Any(x => x.IsNew);
            if (needConfirm)
            {                
                string messageBoxText = "Do you want to save changes?";
                string caption = "Rename";
                MessageBoxButton button = MessageBoxButton.YesNoCancel;
                MessageBoxImage icon = MessageBoxImage.Warning;
                MessageBoxResult result;

                result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.No);
                if(result == MessageBoxResult.Yes)
                {
                    ConfirmRename = true;
                }
                foreach (var item in Files)
                {
                    if (!item.IsNew)
                        item.IsChecked = false;
                }
            }
        }
    }


    [RelayCommand]
    private void Search()
    {
        if (string.IsNullOrWhiteSpace(KeyWords)) return;
        Files.Clear();
        var word = KeyWords.ToLower();
        var result = (from item in _RawFiles.Values where item.FileName.ToLower().Contains(word) select item).ToList();
        if (result.Count > 0)
        {            
            Files.AddRange(result);
        }

        TotalFiles = Files.Count;
        ProcessingCount = Files.Count;
    }

    [RelayCommand]
    private void DeleteCheckedFiles()
    {
        if (_IsRunning) return;
        var del = (from item in Files where item.IsChecked select item).ToList();
        foreach (var item in del)
        {
            if (MinGroupId == null && MaxGroupId == null)
            {
                DeleteFile(item);
            }
            else if (item.Group >= MinGroupId && item.Group <= MaxGroupId)
            {
                DeleteFile(item);
            }
        }
        TotalFiles = Files.Count;
        //ReloadRemainingDuplication();
    }


    /// <summary>
    /// Context menu
    /// </summary>
    [RelayCommand]
    private void DeleteCurrentFile()
    {
        if (SelectedFile == null)
        {
            MessageBox.Show("Please select a file firstly");
            return;
        }
        DeleteFile(SelectedFile);
    }

    [RelayCommand]
    private void MoveFilesTo()
    {
        string? targetDir = null;
        var vfbd = new VistaFolderBrowserDialog();
        var result = vfbd.ShowDialog();
        if (result.HasValue && result.Value)
        {
            targetDir = vfbd.SelectedPath;
        }
        if (targetDir == null || string.IsNullOrEmpty(targetDir)) {
            MessageBox.Show("Please selected a target folder.");
            return;
        };

        var sfs = (from item in Files where item.IsChecked select item).ToList();
        foreach (var item in sfs)
        {
            var targetFile = Path.Combine(targetDir, item.FileName);
            if (File.Exists(targetFile))
            {
                MessageBox.Show($"Target file is existing: {targetFile}");
                continue;
            }
            File.Move(item.FullName, targetFile);
            var fi = new FileInfo(targetFile);
            item.Directory = fi.DirectoryName;
            item.FileName = fi.Name;
            item.FullName = fi.FullName;

            //Files.Remove(item);
            //if (!string.IsNullOrEmpty(item.HashCode))
            //{
            //    if (_GroupByHash.ContainsKey(item.HashCode))
            //    {
            //        _GroupByHash[item.HashCode].Remove(item);
            //    }
            //}
        }
    }
    [RelayCommand]
    private void CopyFilesTo()
    {
        string? targetDir = null;
        var vfbd = new VistaFolderBrowserDialog();
        var result = vfbd.ShowDialog();
        if (result.HasValue && result.Value)
        {
            targetDir = vfbd.SelectedPath;
        }
        if (targetDir == null || string.IsNullOrEmpty(targetDir)) return;

        var sfs = (from item in Files where item.IsChecked select item).ToList();
        foreach (var item in sfs)
        {
            var targetFile = Path.Combine(targetDir, SelectedFile.FileName);
            if (File.Exists(targetFile))
                continue;
            File.Copy(item.FullName, targetFile);
        }
    }


    private void DeleteFile(FileSummary item)
    {
        _RawFiles.Remove(item.Id);
        File.Delete(item.FullName);
        Files.Remove(item);
        if (_GroupByHash.Count> 0 && item.HashCode!=null && _GroupByHash.ContainsKey(item.HashCode))
        {
            _GroupByHash[item.HashCode].Remove(item);
        }
    }

    private void NewOldCheck(object sender, RoutedEventArgs e)
    {
        if (_IsRunning) return;
        RadioButton radioButton = sender as RadioButton;
        if(radioButton == null) return;
        var v = radioButton.Tag.ToString();
        NewOldCheck(v);
    }

    private void Reset()
    {
        ProcessingCount = 0;
        TotalFiles = 0;
        Files.Clear();
        _GroupByHash.Clear();
        _Directories.Clear();
        _RawFiles.Clear();
    }


    private List<int> _DateTimeSelectedIds = new();
    private void DateTimeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _DateTimeSelectedIds.Clear();
        if (_IsRunning) return;
        if (Files.Count == 0) return;

        ComboBox cb = sender as ComboBox;
        var selection = (cb.SelectedItem as ComboBoxItem).Tag.ToString();
        var ids = NewOldCheck(selection);
        if (ids.Count > 0)
            _DateTimeSelectedIds.AddRange(ids);
    }

    private List<int> _FolderSelectedIds = new();
    private void FolderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _FolderSelectedIds.Clear();
        var ids = FolderSelectionChanged();
        if (ids.Count > 0)
            _FolderSelectedIds.AddRange(ids);
    }

    private List<int> _DirectorySelectedIds = new();
    private void DirectorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _DirectorySelectedIds.Clear();
        var ids = DirectorySelectionChanged();
        if (ids.Count > 0)
            _DirectorySelectedIds.AddRange(ids);
    }

    [RelayCommand]
    private void IntersectionSelection()
    {
        var ids = _DateTimeSelectedIds.Intersect(_FolderSelectedIds).ToList();
        ids = ids.Intersect(_DirectorySelectedIds).ToList();
        Combine(ids);
    }

    [RelayCommand]
    private void UnionSelection()
    {
        var ids = _DateTimeSelectedIds.Union(_FolderSelectedIds).ToList();
        ids = ids.Union(_DirectorySelectedIds).ToList();
        Combine(ids);
    }

    [RelayCommand]
    private void AddFolders()
    {
        var sf = new SelectFolders();
        if(SelectedFolders.Count > 0)
        {
            sf.Folders.AddRange(SelectedFolders);
        }
        sf.ExtensionPattern = _ExtensionPattern;
        var result = sf.ShowDialog();
        if (result.HasValue && result.Value)
        {
            if (sf.IsClearPreviouseFolder)
            {
                SelectedFolders.Clear();
            }
            if (sf.Folders != null && sf.Folders.Count > 0)
            {
                var tmp = (from item in sf.Folders select item.FolderName.Trim()).ToList().Distinct().ToList();
                foreach (var item in tmp)
                {
                    SelectedFolders.Add(new Folder { FolderName = item });
                }
            }
            var extensions = sf.ExtensionPattern;
            if (!string.IsNullOrWhiteSpace(extensions))
            {
                _ExtensionPattern = extensions.Trim();
            }            
        }
        LoadFiles();
    }



    [RelayCommand]
    private void OpenFile()
    {
        if(SelectedFile == null)
        {
            MessageBox.Show("Please select a file firstly");
            return;
        }
        ProcessStartInfo startInfo = new ProcessStartInfo(SelectedFile.FullName) { UseShellExecute = true };
        Process.Start(startInfo);
    }

    [RelayCommand]
    private void OpenDirectory()
    {
        if (SelectedFile == null)
        {
            MessageBox.Show("Please select a file firstly");
            return;
        }
        ProcessStartInfo startInfo = new ProcessStartInfo(SelectedFile.Directory) { UseShellExecute = true };
        Process.Start(startInfo);
    }

    [RelayCommand]
    private void CopyFullFileNameToClipboard()
    {
        if (SelectedFile == null)
        {
            MessageBox.Show("Please select a file firstly");
            return;
        }
        Clipboard.SetDataObject(SelectedFile.FullName);
    }


    private HashSet<int> DirectorySelectionChanged()
    {
        var ids = new HashSet<int>();
        var ss = (from item in Directories where item.IsChecked select item.FolderName).ToHashSet();
        if (ss.Count == 0) return ids;
        foreach (var item in Files)
        {
            if (ss.Contains(item.Directory))
            {
                item.IsChecked = true;
                ids.Add(item.Id);
            }
            else
            {
                item.IsChecked = false;
            }
        }
        return ids;
    }
    private HashSet<int> NewOldCheck(string checkedOption)
    {
        var ids = new HashSet<int>();
        if (checkedOption == "None") return ids;
        if (checkedOption == "Unselect")
        {
            foreach (var item in Files)
            {
                item.IsChecked = false;
            }
        }
        else if (checkedOption == "New")
        {
            foreach (var item in _GroupByHash)
            {
                foreach (var file in item.Value)
                {
                    file.IsChecked = true;
                    ids.Add(file.Id);
                }
                var first = item.Value.First();
                first.IsChecked = false;
                ids.Remove(first.Id);
            }
        }
        else if(checkedOption == "Old")
        {
            foreach (var item in _GroupByHash)
            {
                foreach (var file in item.Value)
                {
                    file.IsChecked = true;
                    ids.Add(file.Id);
                }
                var last = item.Value.Last();
                last.IsChecked = false;
                ids.Remove(last.Id);
            }
        }
        else if (checkedOption == "All")
        {
            foreach (var file in Files)
            {
                file.IsChecked = true;
                ids.Add(file.Id);
            }
        }
        return ids;
    }

    private void Combine(ICollection<int> ids)
    {
        Dictionary<int, FileSummary> ffs = new();
        foreach (var item in Files)
        {
            item.IsChecked = false;
            ffs[item.Id] = item;
        }
        if (ids.Count > 0)
        {
            foreach (var id in ids)
            {
                ffs[id].IsChecked = true;
            }
        }
    }

    private async Task LoadFiles(string dir)
    {
        if (ProcessingCount == null)
            ProcessingCount = 0;

        var allFiles = new List<string>(await GetFilesAsync(dir, _ExtensionPattern));
        TotalFiles += allFiles.Count;
        foreach (var file in allFiles)
        {
            if (_IsCancled)
                return;

            if (!File.Exists(file))
                continue;

            FileInfo fileInfo = new FileInfo(file);
            var summary = new FileSummary
            {
                Id = GenerateNewId(),
                FileName = fileInfo.Name,
                FullName = fileInfo.FullName,
                Directory = fileInfo.DirectoryName,
                Size = fileInfo.Length,
                SelectedFolder = dir,
                Extension = fileInfo.Extension,
                LastModifiedDate = fileInfo.LastWriteTime,
                CreatedDate = fileInfo.CreationTime,
            };

            ProcessingCount++;

            if (WithChecksum)
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                string? md5 = await ChecksumMD5(file);
                if (string.IsNullOrEmpty(md5))
                    continue;

                summary.HashCode = md5;
                stopWatch.Stop();
                summary.TimeElipsed = stopWatch.Elapsed.TotalSeconds;


            }
            if (!string.IsNullOrEmpty(summary.HashCode))
            {
                if (!_GroupByHash.ContainsKey(summary.HashCode))
                    _GroupByHash[summary.HashCode] = new List<FileSummary>();
                _GroupByHash[summary.HashCode].Add(summary);
            }
            Files.Add(summary);
            _RawFiles[summary.Id] = summary;
        }
        _IsCancled=false;
    }
    

    private void ReloadRemainingDuplication()
    {

        int gid = 0;
        foreach (var key in _GroupByHash.Keys)
        {
            if (_GroupByHash[key].Count < 2)
            {
                _GroupByHash.Remove(key);
            }
            else
            {
                ++gid;
                foreach (var item in _GroupByHash[key])
                {
                    item.Group = gid;
                    if (gid % 2 == 0)
                    {
                        item.BackgroupColor = Colors.LightGreen;
                    }
                    else
                    {
                        item.BackgroupColor = Colors.White;
                    }
                }
            }
        }
        MinGroupId = 1;
        MaxGroupId = gid;


        Files.Clear();
        foreach (var key in _GroupByHash.Keys)
        {
            var items = _GroupByHash[key];
            items.Sort((a, b) => a.CreatedDate.CompareTo(b.CreatedDate));
            Files.AddRange(items);
        }

        DuplicationFileNum = Files.Count;
    }

    private HashSet<int> FolderSelectionChanged()
    {
        var ids = new HashSet<int>();
        var ss = (from item in SelectedFolders where item.IsChecked select item.FolderName).ToHashSet();
        if (ss.Count == 0) return ids;
        foreach (var item in Files)
        {
            if (ss.Contains(item.SelectedFolder))
            {
                item.IsChecked = true;
                ids.Add(item.Id);
            }
            else
            {
                item.IsChecked = false;
            }
        }
        return ids;
    }

    private void CollectDirectories()
    {
        var dirs = (from item in Files select item.Directory).ToHashSet().ToList();
        dirs.Sort();
        var ff = (from item in dirs select new Folder(item)).ToList();
        Directories.AddRange(ff);
    }

    public static Task<IEnumerable<string>> GetFilesAsync(string path, string searchPattern = null)
    {
        return Task.Run(()=>GetFiles(path, searchPattern));
    }

    static IEnumerable<string> GetFiles(string path, string searchPattern=null)
    {
        Queue<string> queue = new Queue<string>();
        queue.Enqueue(path);
        while (queue.Count > 0)
        {
            path = queue.Dequeue();
            try
            {
                foreach (string subDir in Directory.GetDirectories(path))
                {
                    queue.Enqueue(subDir);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            string[] files = null;
            try
            {
                files = Directory.GetFiles(path, searchPattern);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            if (files != null)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    yield return files[i];
                }
            }
        }
    }

    /// <summary>
    /// 
    /// What is the fastest way to create a checksum for large files in C#
    /// ===================================================================
    /// 
    /// https://stackoverflow.com/questions/1177607/what-is-the-fastest-way-to-create-a-checksum-for-large-files-in-c-sharp
    /// 
    /// FileStream reads 4096 bytes at a time by default, But you can specify any other value using the FileStream constructor:
    /// new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 16 * 1024 * 1024)
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    private static string GetChecksum(string filePath)
    {

        using (var stream = new BufferedStream(File.OpenRead(filePath), 1200000))
        {
            SHA256Managed sha = new SHA256Managed();
            byte[] checksum = sha.ComputeHash(stream);
            return BitConverter.ToString(checksum).Replace("-", String.Empty);
        }
    }

    /// <summary>
    /// Invoke the windows port of md5sum.exe. It's about two times as fast as the .NET implementation (at least on my machine using a 1.2 GB file)
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public static string Md5SumByProcess(string file)
    {
        var p = new Process();
        p.StartInfo.FileName = "md5sum.exe";
        p.StartInfo.Arguments = file;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
        p.WaitForExit();
        string output = p.StandardOutput.ReadToEnd();
        return output.Split(' ')[0].Substring(1).ToUpper();
    }

    public static Task<string?> ChecksumMD5(string file)
    {
        return Task.Run(() =>
        {
            string output;
            using (var md5 = MD5.Create())
            {
                try
                {
                    using (var stream = File.OpenRead(file))
                    {
                        byte[] checksum = md5.ComputeHash(stream);
                        output = BitConverter.ToString(checksum).Replace("-", String.Empty).ToLower();
                        return output;
                    }
                }
                catch (Exception)
                {
                }
                return null;
            }
        });        
    }

    private int _id = 0;
    private int GenerateNewId()
    {
        lock (this)
        {
            ++_id;
            return _id;
        }
    }


    /// <summary>
    /// Edit DataGrid Cell
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void DataGridEle_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if(e.Column.Header == "FileName")
        {

        }
    }

    private void DataGridEle_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        var fn = e.Column.Header.ToString();
        if (fn != "FileName")
            return;
        var editBox = e.EditingElement as TextBox;
        var newContent = editBox.Text;

        var fs = e.Row.DataContext as FileSummary;
        var newFile = Path.Combine(fs.Directory, newContent);
        if(newFile != fs.FullName)
        {
            if (File.Exists(newFile))
            {
                MessageBox.Show($"{newFile} is existing!");
            }
            else
            {
                File.Move(fs.FullName, newFile);
                var fi = new FileInfo(newFile);
                fs.FullName = fi.FullName;
                fs.FileName = fi.Name;
            }
        }
    }

    private void DataGridEle_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {

    }
}

[INotifyPropertyChanged]
public partial class Folder
{
    public Folder() { }
    public Folder(string f)
    {
        this.FolderName = f;
    }
    [ObservableProperty]
    private bool _IsChecked = false;
    [ObservableProperty]
    private string _FolderName;
}