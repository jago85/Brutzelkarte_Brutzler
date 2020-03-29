using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Interaktionslogik für SetRtcWindow.xaml
    /// </summary>
    public partial class SetRtcWindow : Window
    {
        public SetRtcWindow()
        {
            InitializeComponent();
            SetNow();
        }

        private void Button_Now_Click(object sender, RoutedEventArgs e)
        {
            SetNow();
        }

        void SetNow()
        {
            DateTime dateTime = DateTime.Now;
            DpDate.SelectedDate = dateTime.Date;
            TbTime.Text = dateTime.TimeOfDay.ToString("hh\\:mm\\:ss", new CultureInfo("en-US"));
        }

        private void Button_Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime selectedDate = DpDate.SelectedDate.Value;
                TimeSpan selectedTime = TimeSpan.Parse(TbTime.Text);
                SelectedDateTime = new DateTime(selectedDate.Year,
                    selectedDate.Month, selectedDate.Day,
                    selectedTime.Hours,
                    selectedTime.Minutes,
                    selectedTime.Seconds);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public DateTime SelectedDateTime { get; private set; }
    }
}
