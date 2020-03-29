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

namespace BrutzelProg
{
    /// <summary>
    /// Interaktionslogik für RomConfigWindow.xaml
    /// </summary>
    public partial class RomConfigWindow : Window
    {
        RomConfigViewModel _ViewModel;

        public RomConfigWindow(string id, string name, CicType defaultCic, TvType defaultTv, SaveType defaultSave)
        {
            InitializeComponent();
            _ViewModel = new RomConfigViewModel
            {
                GameId = id,
                GameName = name,
                Cic = (int)defaultCic - 1,
                Tv = (int)defaultTv,
                Save = (int)defaultSave - 1
            };
            DataContext = _ViewModel;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SelectedGameName = _ViewModel.GameName;
            SelectedCic = GetCic();
            SelectedTv = GetTv();
            SelectedSave = GetSave();

            if (SelectedCic == CicType.Unknown)
            {
                ShowWarning("Please select a CIC type!");
            }
            else if (SelectedTv == TvType.Unknown)
            {
                ShowWarning("Please select a TV type!");
            }
            else if (SelectedSave == SaveType.Unknown)
            {
                ShowWarning("Please select a Save type!");
            }
            else
            {
                DialogResult = true;
            }
        }

        private void ShowWarning(string msg)
        {
            MessageBox.Show(msg, "Config Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        CicType GetCic()
        {
            CicType res;

            switch (_ViewModel.Cic)
            {
                case 0:
                    res = CicType.Cic6101;
                    break;

                case 1:
                    res = CicType.Cic6102;
                    break;

                case 2:
                    res = CicType.Cic6103;
                    break;

                case 3:
                    res = CicType.Cic6105;
                    break;

                case 4:
                    res = CicType.Cic6106;
                    break;

                default:
                    res = CicType.Unknown;
                    break;
            }

            return res;
        }

        TvType GetTv()
        {
            TvType res;

            switch (_ViewModel.Tv)
            {
                case 0:
                    res = TvType.Pal;
                    break;

                case 1:
                    res = TvType.Ntsc;
                    break;

                default:
                    res = TvType.Unknown;
                    break;
            }

            return res;
        }

        SaveType GetSave()
        {
            SaveType res;

            switch (_ViewModel.Save)
            {
                case 0:
                    res = SaveType.None;
                    break;

                case 1:
                    res = SaveType.Eep4K;
                    break;

                case 2:
                    res = SaveType.Eep16K;
                    break;

                case 3:
                    res = SaveType.Sram32;
                    break;

                case 4:
                    res = SaveType.Sram32x3;
                    break;

                case 5:
                    res = SaveType.FlashRam;
                    break;

                default:
                    res = SaveType.Unknown;
                    break;
            }

            return res;
        }

        public string SelectedGameName { get; private set; }

        public CicType SelectedCic { get; private set; }

        public TvType SelectedTv { get; private set; }

        public SaveType SelectedSave { get; private set; }
    }

    public class RomConfigViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        string _GameName;
        int _Cic;
        int _Tv;
        int _Save;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string GameId
        {
            get;
            set;
        }

        public string GameName
        {
            get => _GameName;
            set
            {
                if (string.Compare(value, _GameName) != 0)
                {
                    _GameName = value ?? "";
                    OnPropertyChanged("GameName");
                }
            }
        }

        public int Cic
        {
            get { return _Cic; }
            set {
                if (value != _Cic)
                {
                    _Cic = value;
                    OnPropertyChanged("Cic");
                }
            }
        }

        public int Tv
        {
            get { return _Tv; }
            set
            {
                if (value != _Tv)
                {
                    _Tv = value;
                    OnPropertyChanged("Tv");
                }
            }
        }

        public int Save
        {
            get { return _Save; }
            set
            {
                if (value != _Save)
                {
                    _Save = value;
                    OnPropertyChanged("Save");
                }
            }
        }
    }
}
