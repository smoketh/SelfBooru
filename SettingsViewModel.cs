using LiteDB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;

namespace SelfBooru
{
    internal class SettingsViewModel : BindableObject
    {
        private readonly LiteDatabase _db;
        private ImageScanner _scanner;
        private string _status = "Idle";
        private string _outputDir;
        private bool _outputDirChangedDuringScan;

        public string OutputDir
        {
            get => _outputDir;
            set
            {
                //System.Diagnostics.Debug.WriteLine($"Trying to set outputdir to {value}");
                if (_outputDir != value)
                {
                    _outputDir = value;
                    App.Preferences.OutputDir = value;
                    
                    OnPropertyChanged();
                    //System.Diagnostics.Debug.WriteLine($"set outputdir to {value}");
                    //if (_scanner.IsScanning)
                    //{
                    //    _outputDirChangedDuringScan = true;
                    //    //Status = "Warning: Output directory changed during scanning. Cancel & restart to scan new folder.";
                    //}
                    //if(debounceTimer.Enabled)
                    //{
                    //    debounceTimer.Stop();
                    //    
                    //}
                    //_buttonsEnabled = false;
                    //OnPropertyChanged(nameof(ButtonScanEnabled));
                    //OnPropertyChanged(nameof(ButtonCancelEnabled));
                    //debounceTimer.Start();
                    //SetupScanner();
                }
            }
        }
        bool _buttonsEnabled=true;
        public bool ButtonScanEnabled
        {
            get
            {
                return _buttonsEnabled && (_scanner==null || !_scanner.IsScanning);
            }
        }
        public bool ButtonCancelEnabled
        {
            get
            {
                return _buttonsEnabled && (_scanner != null && _scanner.IsScanning);
            }
        }

        public bool IsScanning 
        {
            get
            {
                if (_scanner == null) return false;
                return _scanner.IsScanning;
            }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public ICommand StartScanCommand { get; }
        public ICommand CancelScanCommand { get; }

        public ICommand CheckAndDeleteBrokenEntriesCommand { get; }

        Task? scanningTask;
        //System.Timers.Timer debounceTimer;


        async void SetupScanner()
        {
            if(_scanner!=null && _scanner.IsScanning )
            {
                if (scanningTask != null)
                {
                    await scanningTask;
                }
                else
                {
                    throw new InvalidOperationException("Scanning task isn't initialized and yet scanner is running");
                }
            }

            if (_scanner==null || _scanner._outputDir!=_outputDir )
            {
                if(_scanner!=null)
                {
                    _scanner.Dispose();
                }
                _scanner = new ImageScanner(_db, _outputDir);
                _scanner.Progress += msg => MainThread.BeginInvokeOnMainThread(() =>
                {
                    Status = msg;
                    OnPropertyChanged(nameof(ButtonScanEnabled));
                    OnPropertyChanged(nameof(ButtonCancelEnabled));
                    OnPropertyChanged(nameof(IsScanning));
                });
            }
        }

        public SettingsViewModel()
        {
            var app = Application.Current as App;
            var db = app?.db ?? throw new InvalidOperationException("Application has no db loaded");
            _db = db;
            //debounceTimer = new System.Timers.Timer(1000);
            //debounceTimer.AutoReset = false;
            //debounceTimer.Elapsed += SetupScannerDebounced;
            _outputDir = App.Preferences.OutputDir;
            //_scanner = new ImageScanner(db, _outputDir);
            //if(!string.IsNullOrWhiteSpace(_outputDir))
            //    SetupScanner();

            StartScanCommand = new Command(async () =>
            {
                SetupScanner();
                
                if (!_scanner!.IsScanning)
                {
                    _outputDirChangedDuringScan = false;
                    Status = "Starting scan...";
                    OnPropertyChanged(nameof(ButtonScanEnabled));
                    OnPropertyChanged(nameof(ButtonCancelEnabled));
                    OnPropertyChanged(nameof(IsScanning));
                    scanningTask= _scanner.StartScanAsync();
                    await scanningTask;
                    scanningTask = null;
                    OnPropertyChanged(nameof(ButtonScanEnabled));
                    OnPropertyChanged(nameof(ButtonCancelEnabled));
                    OnPropertyChanged(nameof(IsScanning));
                    if (_outputDirChangedDuringScan)
                    {
                        Status = "Scan complete, but directory was changed during the run.";
                    }
                }
            });

            CheckAndDeleteBrokenEntriesCommand = new Command(async () =>
            {
                SetupScanner();
                
                if (!_scanner!.IsScanning)
                {
                    Status = "Starting broken entries scan...";
                    OnPropertyChanged(nameof(ButtonScanEnabled));
                    OnPropertyChanged(nameof(ButtonCancelEnabled));
                    OnPropertyChanged(nameof(IsScanning));
                    scanningTask= _scanner.StartBrokenScanAsync();
                    await scanningTask;
                    scanningTask = null;
                    OnPropertyChanged(nameof(ButtonScanEnabled));
                    OnPropertyChanged(nameof(ButtonCancelEnabled));
                    OnPropertyChanged(nameof(IsScanning));
                    if (_outputDirChangedDuringScan)
                    {
                        Status = "Scan complete, but directory was changed during the run.";
                    }

                }
            }
            );

            CancelScanCommand = new Command(() =>
            {
                _scanner?.Cancel();
                OnPropertyChanged(nameof(ButtonScanEnabled));
                OnPropertyChanged(nameof(ButtonCancelEnabled));
                OnPropertyChanged(nameof(IsScanning));
            });
        }

        /*private void SetupScannerDebounced(object? sender, ElapsedEventArgs e)
        {
            _buttonsEnabled = true;
            OnPropertyChanged(nameof(ButtonScanEnabled));
            OnPropertyChanged(nameof(ButtonCancelEnabled));
            SetupScanner();
            //throw new NotImplementedException();
        }*/

        //public event PropertyChangedEventHandler? PropertyChanged;
        // protected void OnPropertyChanged(string propertyName) =>
        //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
