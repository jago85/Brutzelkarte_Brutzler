using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Brutzler.Properties;
using FTD2XX_NET;
using static FTD2XX_NET.FTDI;

namespace Brutzler
{
    /// <summary>
    /// Interaktionslogik für ConnectionSettingsWindow.xaml
    /// </summary>
    public partial class ConnectionSettingsWindow : Window, INotifyPropertyChanged
    {
        FT_DEVICE_INFO_NODE[] _Devices;

        public ConnectionSettingsWindow()
        {
            InitializeComponent();
            SelectedIndex = -1;
            UpdatePorts();
            LoadConfig();
            DataContext = this;
        }

        void UpdatePorts()
        {
            FTDI ftdi = new FTDI();
            FT_STATUS status;
            uint numDevices = 0;
            status = ftdi.GetNumberOfDevices(ref numDevices);

            // filter valid devices
            // opened devices can not be displayed
            List<string> names = new List<string>((int)numDevices);
            List<FT_DEVICE_INFO_NODE> validDevices = new List<FT_DEVICE_INFO_NODE>((int)numDevices);
            if (numDevices > 0)
            {
                FT_DEVICE_INFO_NODE[] devices = new FT_DEVICE_INFO_NODE[numDevices];
                ftdi.GetDeviceList(devices);
                foreach (var d in devices)
                {
                    if ((d.Type != FT_DEVICE.FT_DEVICE_UNKNOWN)
                        && (!String.IsNullOrEmpty(d.SerialNumber)))
                    {
                        validDevices.Add(d);
                        names.Add(String.Format("{0} ({1})", d.SerialNumber, d.Description));
                    }
                }
            }

            _Devices = validDevices.ToArray();
            PortNames = names.ToArray();
        }

        void LoadConfig()
        {
            ConnectOnStart = Settings.Default.ConnectOnStartup;

            // Pre-select current device if present
            for (int i = 0; i < _Devices.Length; i++)
            {
                if (_Devices[i].SerialNumber.Equals(Settings.Default.ComPort))
                {
                    SelectedIndex = i;
                    break;
                }
            }
        }

        string[] _PortNames;
        public string[] PortNames
        {
            get => _PortNames;
            set
            {
                _PortNames = value;
                OnPropertyChanged(nameof(PortNames));
            }
        }

        bool _ConnectOnStart;
        public bool ConnectOnStart
        {
            get => _ConnectOnStart;
            set
            {
                if (_ConnectOnStart != value)
                {
                    _ConnectOnStart = value;
                    OnPropertyChanged(nameof(ConnectOnStart));
                }
            }
        }

        public string SelectedPort { get; set; }

        public int SelectedIndex { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Button_UpdatePorts_Click(object sender, RoutedEventArgs e)
        {
            UpdatePorts();
        }

        private void Button_Ok_Click(object sender, RoutedEventArgs e)
        {
            if ((SelectedIndex >= 0)
                && (SelectedIndex < _Devices.Length))
            {
                SelectedPort = _Devices[SelectedIndex].SerialNumber;
            }
            else
            {
                SelectedPort = "";
            }

            DialogResult = true;
        }
    }
}
