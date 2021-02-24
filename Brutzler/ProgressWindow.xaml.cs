using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace Brutzler
{
    /// <summary>
    /// Interaktionslogik für ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow : Window, INotifyPropertyChanged
    {
        CancellationTokenSource _Cts = new CancellationTokenSource();
        Progress<ProgressWindowInfo> _Progress;
        Action<IProgress<ProgressWindowInfo>, CancellationToken, object> _Action;
        object _Argument;
        Task _Task;

        public ProgressWindow(Action<IProgress<ProgressWindowInfo>, CancellationToken, object> action, object argument)
        {
            InitializeComponent();
            _Progress = new Progress<ProgressWindowInfo>(new Action<ProgressWindowInfo>((arg) => 
            {
                ActionText = arg.ActionText;
                ProgressValue = arg.ProgressPercent / 100.0;
            }));
            _Action = action;
            _Argument = argument;
            DataContext = this;
        }

        async Task RunAction()
        {
            ProgressState = TaskbarItemProgressState.Normal;
            await Task.Run(() => 
            {
                _Action(_Progress, _Cts.Token, _Argument);
            }, _Cts.Token);
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_Cts != null)
            {
                _Cts.Cancel();
            }
            ButtonCancel.Content = "Canceling...";
            ButtonCancel.IsEnabled = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        double _ProgressValue = 0.0;
        TaskbarItemProgressState _ProgressState;
        string _ActionText = "";

        public double ProgressValue
        {
            get { return _ProgressValue; }
            set
            {
                if (value != _ProgressValue)
                {
                    _ProgressValue = value;
                    OnPropertyChanged("ProgressValue");
                }
            }
        }

        public TaskbarItemProgressState ProgressState
        {
            get { return _ProgressState; }
            set
            {
                if (value != _ProgressState)
                {
                    _ProgressState = value;
                    OnPropertyChanged("ProgressState");
                }
            }
        }

        public string ActionText
        {
            get { return _ActionText; }
            set
            {
                if (_ActionText != value)
                {
                    _ActionText = value;
                    OnPropertyChanged("ActionText");
                }
            }
        }

        public CancellationToken Token => _Cts.Token;

        public Progress<ProgressWindowInfo> Progress => _Progress;

        async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _Task = RunAction();
                await _Task;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            _Task = null;
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            ProgressState = TaskbarItemProgressState.None;
            if (_Task != null)
            {
                _Cts.Cancel();
                e.Cancel = true;
            }
        }
    }

    public class ProgressWindowInfo
    {
        public ProgressWindowInfo()
        {
            ActionText = "";
            ProgressPercent = 0;
        }

        public string ActionText { get; set; }
        public int ProgressPercent { get; set; }
    }
}
