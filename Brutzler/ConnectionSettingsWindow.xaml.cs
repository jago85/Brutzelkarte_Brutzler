using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
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

namespace Brutzler
{
    /// <summary>
    /// Interaktionslogik für ConnectionSettingsWindow.xaml
    /// </summary>
    public partial class ConnectionSettingsWindow : Window, INotifyPropertyChanged
    {
        public ConnectionSettingsWindow()
        {
            InitializeComponent();
            UpdatePorts();
            DataContext = this;
        }

        void UpdatePorts()
        {
            PortNames = SerialPort.GetPortNames();
        }

        string[] _PortNames;
        public string[] PortNames
        {
            get => _PortNames;
            set
            {
                _PortNames = value;
                OnPropertyChanged("PortNames");
            }
        }

        public string SelectedPort { get; set; }

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
            DialogResult = true;
        }
    }
}
