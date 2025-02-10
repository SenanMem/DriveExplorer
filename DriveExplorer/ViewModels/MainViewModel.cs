using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DriveExplorer.Commands;
using DriveExplorer.Models;

namespace DriveExplorer.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public ObservableCollection<string> Drives { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<DirectoryInfoModel> Results { get; set; } = new ObservableCollection<DirectoryInfoModel>();

        private string _selectedDrive;
        public string SelectedDrive
        {
            get => _selectedDrive;
            set
            {
                _selectedDrive = value;
                OnPropertyChanged();
                ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
            }
        }

        private CancellationTokenSource _cts;
        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }

        public MainViewModel()
        {
            LoadDrives();

            StartCommand = new RelayCommand(param => StartSearch(), param => CanStartSearch());
            PauseCommand = new RelayCommand(param => PauseSearch(), param => CanPauseSearch());
            ResumeCommand = new RelayCommand(param => ResumeSearch(), param => CanResumeSearch());
        }

        private void LoadDrives()
        {
            Drives.Clear();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                Drives.Add(drive.Name);
            }
        }


        private bool CanStartSearch() => !string.IsNullOrEmpty(SelectedDrive);
        private bool CanPauseSearch() => _cts != null;
        private bool CanResumeSearch() => _cts != null && _cts.IsCancellationRequested;

        private async void StartSearch()
        {
            if (string.IsNullOrEmpty(SelectedDrive)) return;

            _cts = new CancellationTokenSource();
            Results.Clear();

            await Task.Run(() => SearchLargeFiles(SelectedDrive, _cts.Token));
        }

        private void PauseSearch()
        {
            _cts?.Cancel();
        }

        private async void ResumeSearch()
        {
            if (string.IsNullOrEmpty(SelectedDrive)) return;

            _cts = new CancellationTokenSource();
            await Task.Run(() => SearchLargeFiles(SelectedDrive, _cts.Token));
        }

        private void SearchLargeFiles(string rootDirectory, CancellationToken token)
        {
            try
            {
                var directories = Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.TopDirectoryOnly);

                Parallel.ForEach(directories, new ParallelOptions { CancellationToken = token }, dir =>
                {
                    if (token.IsCancellationRequested) return;

                    try
                    {
                        var largeFiles = Directory.EnumerateFiles(dir)
                            .Select(f => new FileInfo(f))
                            .Where(f => f.Length > 10 * 1024 * 1024) 
                            .ToList();

                        if (largeFiles.Any())
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                Results.Add(new DirectoryInfoModel
                                {
                                    Directory = dir,
                                    FileCount = largeFiles.Count,
                                    TotalSize = largeFiles.Sum(f => f.Length) / 1024.0 / 1024.0
                                });
                            });
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine($"Access Denied: {dir} (Skipping)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error scanning directory {dir}: {ex.Message}");
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Top-level Access Denied: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("General Error: " + ex.Message);
            }
        }
    }
}
