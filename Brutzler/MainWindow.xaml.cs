using BrutzelProg;
using Brutzler.Properties;
using Crc32;
using DragonFS;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Linq;

namespace Brutzler
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        const int MaxPendingAck = 6;

        const int RomMemoryOffset = 0;
        const int RomMemorySize = 64 * 1024 * 1024;
        const int RomSectorSize = 256 * 1024;
        const int RomPageSize = 256;
        const int BootMemoryOffset = RomMemorySize;
        const int BootMemorySize = 8 * 1024 * 1024;
        const int BootSectorSize = 4 * 1024;
        const int BootPageSize = 256;

        const string DfsConfigPath = "/brutzelkarte/config";
        const int DfsOffset = 0x200000;

        string ComPort = "";
        public ObservableCollection<RomListViewItem> RomList { get; set; }

        string _ProjectFile = "";

        public MainWindow()
        {
            InitializeComponent();

            RomList = new ObservableCollection<RomListViewItem>();
            RomList.CollectionChanged += RomList_CollectionChanged;
            DataContext = this;

            ComPort = Settings.Default.ComPort;
        }

        private void RomList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateOffsets();
        }

        void AddRom()
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Multiselect = true;
            d.Filter = "rom files (*.v64;*.z64)|*.v64;*.z64|all files|*.*";
            if (d.ShowDialog(this).Value == true)
            {
                foreach (string fileName in d.FileNames)
                {
                    BrutzelConfig config = GetRomConfig(fileName);
                    if (config != null)
                    {
                        FileInfo info = new FileInfo(fileName);
                        RomListViewItem item = new RomListViewItem(config)
                        {
                            Size = (int)info.Length,
                            FileName = fileName
                        };
                        RomList.Add(item);
                    }
                }
            }
        }

        void EditRom(RomListViewItem item)
        {
            BrutzelConfig config = item.Config;
            RomConfigWindow wnd = new RomConfigWindow(config.FullId, config.Name, config.Cic, config.Tv, config.Save)
            {
                Owner = this
            };
            if (wnd.ShowDialog().Value == false)
            {
                return;
            }

            item.Name = wnd.SelectedGameName;
            item.Cic = wnd.SelectedCic;
            item.Tv = wnd.SelectedTv;
            item.Save = wnd.SelectedSave;

            UpdateOffsets();
        }

        BrutzelConfig GetRomConfig(string fileName)
        {
            BrutzelConfig config = new BrutzelConfig();

            byte[] bootSector;
            try
            {
                bootSector = GetBootSectorFromFile(fileName);
            }
            catch (Exception)
            {
                return null;
            }
            string fullId = "";
            fullId += (char)bootSector[0x3B];
            fullId += (char)bootSector[0x3C];
            fullId += (char)bootSector[0x3D];
            fullId += (char)bootSector[0x3E];
            config.FullId = fullId;
            config.Cic = GetCic(bootSector);
            config.Tv = GetTv(bootSector);

            RomInfo romInfo = GetRomInfo(bootSector);
            if (romInfo != null)
            {
                config.Name = romInfo.Name;
                config.Save = romInfo.Save;
            }
            else
            {
                string romName = "";
                for (int i = 0; i < 20; i++)
                {
                    char c = (char)bootSector[i + 32];
                    if (c == '\0')
                        break;
                    romName += c;
                }
                config.Name = romName.Trim();

                RomConfigWindow wnd = new RomConfigWindow(config.FullId, config.Name, config.Cic, config.Tv, config.Save)
                {
                    Owner = this
                };
                if (wnd.ShowDialog().Value == false)
                {
                    return null;
                }

                config.Name = wnd.SelectedGameName;
                config.Cic = wnd.SelectedCic;
                config.Tv = wnd.SelectedTv;
                config.Save = wnd.SelectedSave;
            }

            return config;
        }

        RomInfo GetRomInfo(byte[] header)
        {
            string id = "";
            id += (char)header[0x3C];
            id += (char)header[0x3D];

            RomInfo ri = CartDb.GetRomById(id);

            return ri;
        }

        byte[] GetBootSectorFromFile(string filename)
        {
            // Buffer for Cart Info and bootcode
            byte[] bootSector = new byte[4096];

            using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read))
            {
                int bytesRead = fs.Read(bootSector, 0, bootSector.Length);
                if (bootSector.Length > bytesRead)
                {
                    throw new Exception("File to small");
                }
            }

            UInt32 init = 0;
            init |= (UInt32)(bootSector[0] << 24);
            init |= (UInt32)(bootSector[1] << 16);
            init |= (UInt32)(bootSector[2] << 8);
            init |= (UInt32)(bootSector[3] << 0);

            if (init == 0x80371240)
            {
                // ok
            }
            else if (init == 0x37804012)
            {
                SwapArray(bootSector);
            }
            else
            {
                throw new Exception("Invalid ROM");
            }

            return bootSector;
        }

        CicType GetCic(byte[] bootSector)
        {
            CicType res = CicType.Unknown;

            byte[] bootcode = new byte[4032];

            Array.Copy(bootSector, 64, bootcode, 0, bootcode.Length);

            uint crc = Crc32Algorithm.Compute(bootcode);
            switch (crc)
            {
                case 0x6170A4A1:    // Starfox 64 (6101)
                case 0x009E9EA3:    // Lylat Wars (7102)
                    res = CicType.Cic6101;
                    break;

                case 0x90BB6CB5:
                    res = CicType.Cic6102;
                    break;

                case 0x0B050EE0:
                    res = CicType.Cic6103;
                    break;

                case 0x98BC2C86:
                    res = CicType.Cic6105;
                    break;

                case 0xACC8580A:
                    res = CicType.Cic6106;
                    break;
            }

            return res;
        }

        TvType GetTv(byte[] header)
        {
            TvType res = TvType.Unknown;

            char tvChar = (char)header[0x3E];
            switch (tvChar)
            {
                case 'A':
                case 'E':
                case 'J':
                    res = TvType.Ntsc;
                    break;

                case 'P':
                case 'D':
                    res = TvType.Pal;
                    break;
            }
            return res;
        }

        void UpdateOffsets()
        {
            int romOffset = 0;
            int saveOffset = 0;
            for (int i = 0; i < RomList.Count; i++)
            {
                RomList[i].RomOffset = (byte)romOffset;
                RomList[i].SaveOffset = (byte)saveOffset;

                romOffset += (byte)Math.Ceiling((float)RomList[i].Size / 1024 / 1024);
                saveOffset += (byte)Math.Ceiling((float)GetSaveSize(RomList[i].Save) / 1024);
            }

            FlashLevel = romOffset;
            SramLevel = saveOffset;
        }

        int GetSaveSize(SaveType save)
        {
            int res = 0;
            switch (save)
            {
                case SaveType.None:
                    res = 0;
                    break;
                case SaveType.Eep4K:
                    res = 512;
                    break;
                case SaveType.Eep16K:
                    res = 2048;
                    break;
                case SaveType.Sram32:
                    res = 32 * 1024;
                    break;
                case SaveType.Sram32x3:
                    res = 3 * 32 * 1024;
                    break;
                case SaveType.FlashRam:
                    res = 128 * 1024;
                    break;
            }
            return res;
        }

        string GetSaveExtension(SaveType save)
        {
            string res = "";
            switch (save)
            {
                case SaveType.Eep4K:
                    res = "eep";
                    break;
                case SaveType.Eep16K:
                    res = "eep";
                    break;
                case SaveType.Sram32:
                    res = "sra";
                    break;
                case SaveType.Sram32x3:
                    res = "sra";
                    break;
                case SaveType.FlashRam:
                    res = "fla";
                    break;
            }
            return res;
        }

        byte[] GetConfig()
        {
            DragonFs dfs = new DragonFs();
            using (DfsFileStream fs = dfs.OpenFile(DfsConfigPath, FileAccess.Write))
            {
                byte autoBootIndex = 0xff;
                var autoBootRom = RomList.FirstOrDefault((e) => {
                    return e.IsAutoBoot;
                });
                if (autoBootRom != null)
                {
                    autoBootIndex = (byte)RomList.IndexOf(autoBootRom);
                }

                fs.WriteByte((byte)RomList.Count); // number of roms
                fs.WriteByte(autoBootIndex); // autoboot index
                foreach (RomListViewItem item in RomList)
                {
                    byte[] configBytes = item.Config.GetBytes();
                    fs.Write(configBytes, 0, configBytes.Length);
                }
            }
            return dfs.GetImage();
        }

        void SwapArray(byte[] data)
        {
            byte[] buffer = new byte[4];
            for (int i = 0; i < data.Length; i += 4)
            {
                buffer[0] = data[i + 1];
                buffer[1] = data[i + 0];
                buffer[2] = data[i + 3];
                buffer[3] = data[i + 2];
                buffer.CopyTo(data, i);
            }
        }

        List<byte[]> GetPages(string filename, int maxBytes)
        {
            List<byte[]> dataList = new List<byte[]>();

            using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read))
            {
                while (fs.Position < fs.Length)
                {

                    byte[] data = new byte[256];
                    fs.Read(data, 0, data.Length);
                    dataList.Add(data);
                    if (fs.Position >= maxBytes)
                        break;
                }
            }
            return dataList;
        }

        List<byte[]> GetPages(byte[] data)
        {
            List<byte[]> list = new List<byte[]>();
            using (MemoryStream ms = new MemoryStream(data))
            {
                while (ms.Position < ms.Length)
                {
                    byte[] pageData = new byte[256];
                    ms.Read(pageData, 0, pageData.Length);
                    list.Add(pageData);
                }
            }
            return list;
        }

        bool IsCartBigEndian(byte[] firstPage)
        {
            UInt32 firstWord = 0;
            firstWord |= (UInt32)firstPage[3];
            firstWord |= (UInt32)firstPage[2] << 8;
            firstWord |= (UInt32)firstPage[1] << 16;
            firstWord |= (UInt32)firstPage[0] << 24;

            return (firstWord == 0x80371240);
        }

        void MakeByteswapped(List<byte[]> dataList)
        {
            byte[] buffer = new byte[4];
            foreach (var dataItem in dataList)
            {
                SwapArray(dataItem);
            }
        }

        void RunTaskInProgressWindow(string title, Action<IProgress<ProgressWindowInfo>, CancellationToken, object> action, object argument)
        {
            ProgressWindow wnd = new ProgressWindow(action, argument)
            {
                Owner = this,
                Title = title
            };
            wnd.ShowDialog();
        }

        #region Menu Items

        private void MenuItem_Edit_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            RomListViewItem item = menu.DataContext as RomListViewItem;
            EditRom(item);
        }

        private void MenuItem_AddRom_Click(object sender, RoutedEventArgs e)
        {
            AddRom();
        }

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            RomListViewItem item = menu.DataContext as RomListViewItem;
            RomList.Remove(item);
        }
        private void MenuItem_SetAutoboot(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            RomListViewItem item = menu.DataContext as RomListViewItem;

            // Remember autoboot to be able to toggle it
            bool currentSetting = item.IsAutoBoot;

            // Clear autoboot
            foreach (var romItem in RomList)
            {
                romItem.IsAutoBoot = false;
            }
            item.IsAutoBoot = !currentSetting;
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuItem_UpdateBootloader_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "rom files (*.v64;*.z64)|*.v64;*.z64|all files|*.*";
            if (d.ShowDialog().Value == true)
            {
                RunTaskInProgressWindow("Update Bootloader", Action_UpdateBootloader, d.FileName);
            }
        }

        private void MenuItem_UpdateFpga_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "binary files (*.bin)|*.bin";
            if (d.ShowDialog().Value == true)
            {
                RunTaskInProgressWindow("Update FPGA", Action_UpdateFpga, d.FileName);
            }
        }

        private void MenuItem_ClearSram_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("This clears the complete SRAM memory\r\nAll savegames on the cartridge will be lost.\r\n\r\nAre you sure?", "Erase all Saves", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (result == MessageBoxResult.OK)
            {
                // Create a List of pages with all byte 0x00
                WriteSramWorkerArgument arg = new WriteSramWorkerArgument();
                arg.DataList = new List<byte[]>();
                arg.Offset = 0;
                for (int i = 0; i < 1024; i++)
                {
                    arg.DataList.Add(new byte[256]);
                }
                RunTaskInProgressWindow("Clear all Saves", Action_WriteSramData, arg);
            }
        }

        private void MenuItem_DumpSramClick(object sender, RoutedEventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "binary file (*.bin)|*.bin|all files|*.*";
            if (d.ShowDialog().Value == true)
            {
                ReadSramWorkerArgument arg = new ReadSramWorkerArgument();
                arg.Offset = 0;
                arg.Size = 256 * 1024;
                arg.FileName = d.FileName;
                RunTaskInProgressWindow("Dump SRAM", Action_ReadSramData, arg);
            }
        }

        private void MenuItem_RestoreSramClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "binary file (*.bin)|*.bin|all files|*.*";
            if (d.ShowDialog().Value == true)
            {
                WriteSramWorkerArgument arg = new WriteSramWorkerArgument();
                arg.DataList = GetPages(d.FileName, SramSize * 1024);
                arg.Offset = 0;
                RunTaskInProgressWindow("Restore SRAM", Action_WriteSramData, arg);
            }
        }

        private void MenuItem_ClearSave_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            RomListViewItem item = menu.DataContext as RomListViewItem;
            if (item.Save == SaveType.None)
                return;
            string msgText = String.Format("This clears the savegame memory for:\r\n\r\n    {0}\r\n\r\nAre you sure?", item.Name);
            MessageBoxResult result = MessageBox.Show(msgText, "Erase Savegame", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (result == MessageBoxResult.OK)
            {
                int size = GetSaveSize(item.Save);
                WriteSramWorkerArgument arg = new WriteSramWorkerArgument();
                arg.Offset = item.SaveOffset * 1024;
                arg.DataList = new List<byte[]>();
                while (size > 0)
                {
                    arg.DataList.Add(new byte[256]);
                    size -= Math.Min(size, 256);
                }
                RunTaskInProgressWindow("Clear Save", Action_WriteSramData, arg);
            }
        }

        private void MenuItem_RestoreSave_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            RomListViewItem item = menu.DataContext as RomListViewItem;
            if (item.Save == SaveType.None)
                return;
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = String.Format("savegame file (*.{0})|*.{0}|all files|*.*", GetSaveExtension(item.Save));
            if (d.ShowDialog().Value == true)
            {
                WriteSramWorkerArgument arg = new WriteSramWorkerArgument();
                arg.DataList = GetPages(d.FileName, GetSaveSize(item.Save));
                arg.Offset = item.SaveOffset * 1024;
                RunTaskInProgressWindow("Restore Savegame", Action_WriteSramData, arg);
            }
        }

        private void MenuItem_BackupSave_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            RomListViewItem item = menu.DataContext as RomListViewItem;
            if (item.Save == SaveType.None)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = String.Format("savegame file (*.{0})|*.{0}|all files|*.*", GetSaveExtension(item.Save));
            if (d.ShowDialog().Value == true)
            {
                ReadSramWorkerArgument arg = new ReadSramWorkerArgument();
                arg.Offset = item.SaveOffset * 1024;
                arg.Size = GetSaveSize(item.Save);
                arg.FileName = d.FileName;
                RunTaskInProgressWindow("Backup Savegame", Action_ReadSramData, arg);
            }
        }

        private void MenuItem_FlashOneRom_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            RomListViewItem item = menu.DataContext as RomListViewItem;

            EraseAndWriteFlashWorkerArgument arg = new EraseAndWriteFlashWorkerArgument
            {
                Offset = RomMemoryOffset + item.RomOffset * 1024 * 1024,
                SectorSize = RomSectorSize,

                // the data will be loaded in asynchronously in the worker
                OnLoadData = new LoadData(() => {
                    List<byte[]> pageList = GetPages(item.FileName, item.Size);
                    if (IsCartBigEndian(pageList[0]))
                    {
                        MakeByteswapped(pageList);
                    }
                    return pageList;
                }) 
            };
            RunTaskInProgressWindow("Flash one ROM", Action_EraseAndWriteFlash, arg);
        }

        private void MenuItem_EraseRomFlash_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("This erases the complete ROM memory.\r\n\r\nAre you sure?", "Erase all ROMs", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (result == MessageBoxResult.OK)
            {
                EraseAndWriteFlashWorkerArgument arg = new EraseAndWriteFlashWorkerArgument
                {
                    Offset = RomMemoryOffset,
                    Size = RomMemorySize,
                    SectorSize = RomSectorSize
                };
                RunTaskInProgressWindow("Erase all ROMs", Action_EraseAndWriteFlash, arg);
            }
        }

        private void MenuItem_EraseBootFlash_Click(object sender, RoutedEventArgs e)
        {
            EraseAndWriteFlashWorkerArgument arg = new EraseAndWriteFlashWorkerArgument();
            arg.Offset = BootMemoryOffset;
            arg.Size = BootMemorySize;
            arg.SectorSize = BootSectorSize;
            RunTaskInProgressWindow("Erase Bootloader", Action_EraseAndWriteFlash, arg);
        }

        private void MenuItem_SetRtc_Click(object sender, RoutedEventArgs e)
        {
            SetRtcWindow wnd = new SetRtcWindow();
            wnd.Owner = this;
            if (wnd.ShowDialog().Value == true)
            {
                RunTaskInProgressWindow("Set RTC", Action_SetRtc, wnd.SelectedDateTime);
            }
        }

        private void MenuItem_UpdateConfig_Click(object sender, RoutedEventArgs e)
        {
            if (RomList.Count > 0)
            {
                EraseAndWriteFlashWorkerArgument arg = new EraseAndWriteFlashWorkerArgument();
                arg.OnLoadData = new LoadData(() => {
                    byte[] config = GetConfig();
                    SwapArray(config);
                    return GetPages(config);
                });
                arg.Offset = BootMemoryOffset + DfsOffset;
                arg.SectorSize = BootSectorSize;
                RunTaskInProgressWindow("Update Config", Action_EraseAndWriteFlash, arg);
            }
        }

        private void MenuItem_SaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (RomList.Count > 0)
            {
                if (String.IsNullOrEmpty(_ProjectFile))
                {
                    SaveProjectAs();
                }
                else
                {
                    SaveProject(_ProjectFile);
                }
            }
            else
            {
                MessageBox.Show("Nothing to save", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuItem_SaveProjectAs_Click(object sender, RoutedEventArgs e)
        {
            if (RomList.Count > 0)
            {
                SaveProjectAs();
            }
            else
            {
                MessageBox.Show("Nothing to save", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProjectAs()
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "BRUTZLER project (*.bpj)|*.bpj|all files|*.*";
            if (d.ShowDialog().Value == true)
            {
                _ProjectFile = d.FileName;
                SaveProject(_ProjectFile);
            }
        }

        private void SaveProject(string filename)
        {
            try
            {
                var xml = new XElement("Roms", RomList.Select(x => new XElement("Rom",
                    new XAttribute("GameId", x.FullId),
                    new XAttribute("GameName", x.Name),
                    new XAttribute("FileName", x.FileName),
                    new XAttribute("TvType", x.Tv),
                    new XAttribute("CicType", x.Cic),
                    new XAttribute("SaveType", x.Save),
                    new XAttribute("RomSize", x.Size),
                    new XAttribute("IsAutoBoot", x.IsAutoBoot))));
                using (var writer = File.CreateText(filename))
                {
                    writer.WriteLine(xml);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuItem_LoadProject_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "BRUTZLER project (*.bpj)|*.bpj|all files|*.*";
            if (d.ShowDialog(this).Value == true)
            {
                try
                {
                    using (var reader = File.OpenText(d.FileName))
                    {
                        using (XmlReader nodeReader = XmlReader.Create(reader))
                        {
                            RomList.Clear();
                            nodeReader.MoveToContent();
                            XElement xRoot = XElement.Load(nodeReader, LoadOptions.SetLineInfo);
                            foreach (XElement elem in xRoot.Descendants())
                            {
                                RomListViewItem item = new RomListViewItem();
                                item.FullId = elem.Attribute("GameId").Value;
                                item.Name = elem.Attribute("GameName").Value;
                                item.FileName = elem.Attribute("FileName").Value;
                                item.Tv = (TvType)Enum.Parse(typeof(TvType), elem.Attribute("TvType").Value);
                                item.Cic = (CicType)Enum.Parse(typeof(CicType), elem.Attribute("CicType").Value);
                                item.Save = (SaveType)Enum.Parse(typeof(SaveType), elem.Attribute("SaveType").Value);
                                item.Size = int.Parse(elem.Attribute("RomSize").Value);
                                item.IsAutoBoot = false;
                                XAttribute isAutoBootAttr = elem.Attribute("IsAutoBoot");
                                if (isAutoBootAttr != null)
                                {
                                    item.IsAutoBoot = bool.Parse(isAutoBootAttr.Value);
                                }
                                RomList.Add(item);
                            }
                        }
                    }
                    _ProjectFile = d.FileName;
                }
                catch (Exception ex)
                {
                    RomList.Clear();
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                UpdateOffsets();
            }
        }

        private void MenuItem_ConnectionSettings_Click(object sender, RoutedEventArgs e)
        {
            ConnectionSettingsWindow wnd = new ConnectionSettingsWindow();
            wnd.Owner = this;
            if (wnd.ShowDialog().Value == true)
            {
                ComPort = wnd.SelectedPort;
                Settings.Default.ComPort = wnd.SelectedPort;
                Settings.Default.Save();
            }
        }

        #endregion

        #region Buttons

        private void Button_AddRom_Click(object sender, RoutedEventArgs e)
        {
            AddRom();
        }

        private void Button_DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            RomList.Clear();
            FlashLevel = 0;
            SramLevel = 0;
        }

        private void Button_WriteFlash_Click(object sender, RoutedEventArgs e)
        {
            if (FlashLevel > FlashSize)
            {
                MessageBox.Show("The ROMs in the list require too much FLASH.\r\nPlease remove some ROMs.", "Not enough FLASH", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (SramLevel > SramSize)
            {
                MessageBox.Show("The ROMs in the list require too much SRAM.\r\nPlease remove some ROMs.", "Not enough SRAM", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            RunTaskInProgressWindow("Write Flash", Action_FlashRoms, null);
        }

        private void Button_ItemUp_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LbRoms.SelectedIndex;

            if (selectedIndex > 0)
            {
                RomListViewItem itemToMoveUp = RomList[selectedIndex];
                RomList.Move(selectedIndex, selectedIndex - 1);
                LbRoms.SelectedIndex = selectedIndex - 1;
            }
        }

        private void Button_ItemDown_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LbRoms.SelectedIndex;

            if (selectedIndex + 1 < RomList.Count)
            {
                RomListViewItem itemToMoveDown = RomList[selectedIndex];
                RomList.Move(selectedIndex, selectedIndex + 1);
                LbRoms.SelectedIndex = selectedIndex + 1;
            }
        }

        #endregion

        #region Binding Properties
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        int _FlashLevel = 0;
        public int FlashLevel
        {
            get => _FlashLevel;
            set
            {
                if (value != _FlashLevel)
                {
                    _FlashLevel = value;
                    OnPropertyChanged("FlashLevel");
                }
            }
        }

        public int FlashSize => RomMemorySize / 1024 / 1024;

        int _SramLevel = 0;
        public int SramLevel
        {
            get => _SramLevel;
            set
            {
                if (value != _SramLevel)
                {
                    _SramLevel = value;
                    OnPropertyChanged("SramLevel");
                }
            }
        }

        public int SramSize = 256;

        RomListViewItem SelectedItem { get; set; }
        #endregion

        #region Worker Methods
        
        void Action_FlashRoms(IProgress<ProgressWindowInfo> progress, CancellationToken ct, object argument)
        {
            ProgressWindowInfo info = new ProgressWindowInfo();
            info.ActionText = "Connecting";
            progress.Report(info);

            var cart = new Brutzelkarte(ComPort);
            try
            {
                cart.Open();

                // Erase Flash
                int sectorCount = (int)Math.Ceiling((double)FlashLevel * 1024 * 1024 / RomSectorSize);
                for (int i = 0; i < sectorCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    info.ActionText = String.Format("Erasing sector {0} / {1}", i + 1, sectorCount);
                    info.ProgressPercent = (i + 1) * 100 / sectorCount;
                    progress.Report(info);

                    cart.EraseSector(i * RomSectorSize);
                    if (cart.PendingAck > MaxPendingAck)
                    {
                        cart.WaitAck();
                    }
                }
                while (cart.PendingAck > 0)
                {
                    cart.WaitAck();
                }

                // Write the roms
                foreach (RomListViewItem item in RomList)
                {
                    ct.ThrowIfCancellationRequested();

                    int addr = item.Config.RomOffset * 1024 * 1024;
                    FileInfo fileInfo = new FileInfo(item.FileName);
                    info.ActionText = "Loading ROM: " + fileInfo.Name;
                    info.ProgressPercent = (int)((Int64)addr * 100 / (FlashLevel * 1024 * 1024));
                    progress.Report(info);

                    // Load the ROM
                    List<byte[]> pageList;
                    try
                    {
                        pageList = GetPages(item.FileName, item.Size);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                        continue;
                    }
                    if (IsCartBigEndian(pageList[0]))
                    {
                        MakeByteswapped(pageList);
                    }

                    // Write ROM data
                    foreach (byte[] dataPage in pageList)
                    {
                        ct.ThrowIfCancellationRequested();

                        cart.WritePage(addr, dataPage);
                        if (cart.PendingAck > MaxPendingAck)
                        {
                            cart.WaitAck();
                        }
                        addr += dataPage.Length;
                        info.ActionText = String.Format("Writing {0} / {1} KiB", addr / 1024, FlashLevel * 1024);
                        info.ProgressPercent = (int)((Int64)addr * 100 / (FlashLevel * 1024 * 1024));
                        progress.Report(info);
                    }
                    while (cart.PendingAck > 0)
                    {
                        cart.WaitAck();
                    }
                }
                
                info.ActionText = "Updating Config";
                info.ProgressPercent = 100;
                progress.Report(info);

                // Update the Config
                byte[] configBytes = GetConfig();
                using (MemoryStream ms = new MemoryStream(configBytes))
                {
                    // Erase config flash
                    int addr = BootMemoryOffset + DfsOffset;
                    int erasedBytes = 0;
                    while (erasedBytes < ms.Length)
                    {
                        ct.ThrowIfCancellationRequested();

                        cart.EraseSector(addr);
                        cart.WaitAck();
                        addr += BootSectorSize;
                        erasedBytes += BootSectorSize;
                    }

                    // Write config data
                    addr = BootMemoryOffset + DfsOffset;
                    while (ms.Position < ms.Length)
                    {
                        ct.ThrowIfCancellationRequested();

                        byte[] pageData = new byte[256];
                        ms.Read(pageData, 0, pageData.Length);
                        SwapArray(pageData);
                        cart.WritePage(addr, pageData);
                        cart.WaitAck();
                        addr += pageData.Length;
                    }
                }
            }
            catch (OperationCanceledException)
            { }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                cart.Close();
            }
        }

        void Action_UpdateBootloader(IProgress<ProgressWindowInfo> progress, CancellationToken ct, object argument)
        {
            string fileName = (string)argument;

            ProgressWindowInfo info = new ProgressWindowInfo();
            info.ActionText = "Connecting";
            progress.Report(info);

            List<byte[]> pageList = GetPages(fileName, BootMemorySize);
            if (IsCartBigEndian(pageList[0]))
            {
                MakeByteswapped(pageList);
            }

            int totalSize = pageList.Count * 256;
            int sectorCount = (int)Math.Ceiling((double)totalSize / BootSectorSize);

            int addr = BootMemoryOffset;

            var cart = new Brutzelkarte(ComPort);
            try
            {
                cart.Open();

                // Erase the flash
                for (int i = 0; i < sectorCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    info.ActionText = String.Format("Erasing sector {0} / {1}", i + 1, sectorCount);
                    info.ProgressPercent = (i + 1) * 100 / sectorCount;
                    progress.Report(info);

                    cart.EraseSector(addr + i * BootSectorSize);
                    if (cart.PendingAck > MaxPendingAck)
                    {
                        cart.WaitAck();
                    }
                }
                while (cart.PendingAck > 0)
                {
                    cart.WaitAck();
                }

                // Write the data
                int bytesWritten = 0;
                foreach (byte[] dataPage in pageList)
                {
                    ct.ThrowIfCancellationRequested();

                    cart.WritePage(addr, dataPage);
                    if (cart.PendingAck > MaxPendingAck)
                    {
                        cart.WaitAck();
                    }
                    addr += dataPage.Length;
                    bytesWritten += dataPage.Length;
                    info.ActionText = String.Format("Writing {0} / {1} KiB", bytesWritten / 1024, totalSize / 1024);
                    info.ProgressPercent = (int)((Int64)bytesWritten * 100 / totalSize);
                    progress.Report(info);
                }
                while (cart.PendingAck > 0)
                {
                    cart.WaitAck();
                }
            }
            catch (OperationCanceledException)
            { }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                cart.Close();
            }
        }

        void Action_EraseAndWriteFlash(IProgress<ProgressWindowInfo> progress, CancellationToken ct, object argument)
        {
            EraseAndWriteFlashWorkerArgument arg = (EraseAndWriteFlashWorkerArgument)argument;

            ProgressWindowInfo info = new ProgressWindowInfo();

            // if OnLOadData is set, load the data and set the Size
            List<byte[]> dataList = null;
            if (arg.OnLoadData != null)
            {
                info.ActionText = "Loading data";
                progress.Report(info);
                dataList = arg.OnLoadData();
                arg.Size = dataList.Count * 256;
            }

            info.ActionText = "Connecting";
            progress.Report(info);
            
            var cart = new Brutzelkarte(ComPort);
            try
            {
                cart.Open();

                int sectorCount = (int)Math.Ceiling((double)arg.Size / arg.SectorSize);

                // Erase the flash
                for (int i = 0; i < sectorCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    info.ActionText = String.Format("Erasing sector {0} / {1}", i + 1, sectorCount);
                    info.ProgressPercent = (i + 1) * 100 / sectorCount;
                    progress.Report(info);

                    cart.EraseSector(arg.Offset + i * arg.SectorSize);
                    if (cart.PendingAck > MaxPendingAck)
                    {
                        cart.WaitAck();
                    }
                }
                while (cart.PendingAck > 0)
                {
                    cart.WaitAck();
                }

                if (dataList != null)
                {
                    int addr = arg.Offset;
                    int bytesWritten = 0;
                    foreach (byte[] dataPage in dataList)
                    {
                        ct.ThrowIfCancellationRequested();

                        cart.WritePage(addr, dataPage);
                        if (cart.PendingAck > MaxPendingAck)
                        {
                            cart.WaitAck();
                        }
                        addr += dataPage.Length;
                        bytesWritten += dataPage.Length;
                        info.ActionText = String.Format("Writing {0} / {1} KiB", bytesWritten / 1024, arg.Size / 1024);
                        info.ProgressPercent = (int)((Int64)bytesWritten * 100 / arg.Size);
                        progress.Report(info);
                    }
                    while (cart.PendingAck > 0)
                    {
                        cart.WaitAck();
                    }
                }
            }
            catch (OperationCanceledException)
            { }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                cart.Close();
            }
        }

        void Action_UpdateFpga(IProgress<ProgressWindowInfo> progress, CancellationToken ct, object argument)
        {
            string filename = (string)argument;

            ProgressWindowInfo info = new ProgressWindowInfo();

            var cart = new Brutzelkarte(ComPort);
            try
            {
                info.ActionText = "Connecting";
                progress.Report(info);

                cart.Open();
                cart.ReadVersion();

                List<byte[]> dataList = new List<byte[]>();
                using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read))
                {
                    while (fs.Position < fs.Length)
                    {
                        // TODO: Check max bitstream size
                        // For the test the size was 176 KiB
                        if (fs.Position > 250 * 1024)
                            break;
                        byte[] data = new byte[16];
                        fs.Read(data, 0, data.Length);
                        dataList.Add(data);
                    }
                }

                // Read Device ID
                cart.WriteEfb8Bit(0x70, 0x80);
                cart.WriteEfb32Bit(0x71, 0xE0000000);
                UInt32 id = cart.ReadEfb32Bit(0x73);
                cart.WriteEfb8Bit(0x70, 0x00);

                info.ActionText = "Erasing configuration";
                progress.Report(info);

                // Enable Configuration Interface (Transparent Mode)
                cart.WriteEfb8Bit(0x70, 0x80);
                cart.WriteEfb32Bit(0x71, 0x74080000);
                cart.WriteEfb8Bit(0x70, 0x00);

                // Erase Flash CFG
                cart.WriteEfb8Bit(0x70, 0x80);
                cart.WriteEfb32Bit(0x71, 0x0E040000);
                cart.WriteEfb8Bit(0x70, 0x00);

                // Poll for completion
                UInt32 status = 0;
                do
                {
                    cart.WriteEfb8Bit(0x70, 0x80);
                    cart.WriteEfb32Bit(0x71, 0x3C000000);
                    status = cart.ReadEfb32Bit(0x73);
                    cart.WriteEfb8Bit(0x70, 0x00);
                } while ((status & (1 << 12)) != 0);

                info.ActionText = "Writing configuration";
                progress.Report(info);

                // Reset Configuration Flash Address
                cart.WriteEfb8Bit(0x70, 0x80);
                cart.WriteEfb32Bit(0x71, 0x46000000);
                cart.WriteEfb8Bit(0x70, 0x00);

                // Write the file
                for (int i = 0; i < dataList.Count; i++)
                {
                    byte[] data = dataList[i];

                    // Program Page
                    cart.WriteEfb8Bit(0x70, 0x80);
                    cart.WriteEfb32Bit(0x71, 0x70000001);
                    cart.WriteEfb(0x71, data, 0, 16);
                    cart.WriteEfb8Bit(0x70, 0x00);

                    // Poll for completion
                    do
                    {
                        cart.WriteEfb8Bit(0x70, 0x80);
                        cart.WriteEfb32Bit(0x71, 0x3C000000);
                        status = cart.ReadEfb32Bit(0x73);
                        cart.WriteEfb8Bit(0x70, 0x00);
                    } while ((status & (1 << 12)) != 0);
                    
                    info.ProgressPercent = (i + 1) * 100 / dataList.Count;
                    progress.Report(info);
                }

                info.ActionText = "Completing configuration";
                progress.Report(info);

                // Program DONE
                cart.WriteEfb8Bit(0x70, 0x80);
                cart.WriteEfb32Bit(0x71, 0x5E000000);
                cart.WriteEfb8Bit(0x70, 0x00);

                // Poll for completion
                do
                {
                    cart.WriteEfb8Bit(0x70, 0x80);
                    cart.WriteEfb32Bit(0x71, 0x3C000000);
                    status = cart.ReadEfb32Bit(0x73);
                    cart.WriteEfb8Bit(0x70, 0x00);
                } while ((status & (1 << 12)) != 0);

                // Disable Configuration
                cart.WriteEfb8Bit(0x70, 0x80);
                cart.WriteEfb(0x71, new byte[] { 0x26, 0x00, 0x00 }, 0, 3);
                cart.WriteEfb8Bit(0x70, 0x00);

                // Bypass
                cart.WriteEfb8Bit(0x70, 0x80);
                cart.WriteEfb32Bit(0x71, 0xFFFFFFFF);
                cart.WriteEfb8Bit(0x70, 0x00);

                // Refresh
                cart.WriteEfb8Bit(0x70, 0x80);
                cart.WriteEfb(0x71, new byte[] { 0x79, 0x00, 0x00 }, 0, 3);
                cart.WriteEfb8Bit(0x70, 0x00);
            }
            catch (OperationCanceledException)
            { }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                cart.Close();
            }
        }

        void Action_WriteSramData(IProgress<ProgressWindowInfo> progress, CancellationToken ct, object argument)
        {
            WriteSramWorkerArgument arg = (WriteSramWorkerArgument)argument;
            ProgressWindowInfo info = new ProgressWindowInfo();

            var cart = new Brutzelkarte(ComPort);
            try
            {
                info.ActionText = "Connecting";
                progress.Report(info);

                cart.Open();

                int totalSize = arg.DataList.Count * 256;
                int addr = arg.Offset;

                int bytesWritten = 0;
                foreach (byte[] dataListItem in arg.DataList)
                {
                    ct.ThrowIfCancellationRequested();

                    cart.WriteSram(addr, dataListItem);
                    if (cart.PendingAck > MaxPendingAck)
                    {
                        cart.WaitAck();
                    }
                    addr += dataListItem.Length;
                    bytesWritten += dataListItem.Length;

                    info.ActionText = String.Format("Writing {0} / {1} KiB", bytesWritten / 1024, totalSize / 1024);
                    info.ProgressPercent = bytesWritten * 100 / totalSize;
                    progress.Report(info);
                }
                while (cart.PendingAck > 0)
                {
                    cart.WaitAck();
                }
            }
            catch (OperationCanceledException)
            { }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                cart.Close();
            }
        }

        void Action_ReadSramData(IProgress<ProgressWindowInfo> progress, CancellationToken ct, object argument)
        {
            ReadSramWorkerArgument arg = (ReadSramWorkerArgument)argument;
            ProgressWindowInfo info = new ProgressWindowInfo();

            var cart = new Brutzelkarte(ComPort);
            try
            {
                info.ActionText = "Connecting";
                progress.Report(info);

                cart.Open();

                int pageCnt = (int)Math.Ceiling((decimal)arg.Size / 256);
                byte[] readBuffer = new byte[pageCnt * 256];

                cart.SendAddr(arg.Offset / 2);
                for (int i = 0; i < pageCnt; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    byte[] page = cart.ReadSramPage();
                    page.CopyTo(readBuffer, i * 256);
                    info.ActionText = String.Format("Reading {0} / {1} KiB", (i + 1) * 256 / 1024, arg.Size / 1024);
                    info.ProgressPercent = i * 100 / pageCnt;
                    progress.Report(info);
                }

                ct.ThrowIfCancellationRequested();

                using (FileStream fs = File.Open(arg.FileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(readBuffer, 0, readBuffer.Length);
                }
            }
            catch (OperationCanceledException)
            { }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                cart.Close();
            }
        }

        void Action_SetRtc(IProgress<ProgressWindowInfo> progress, CancellationToken ct, object argument)
        {
            DateTime rtcTime = (DateTime)argument;

            ProgressWindowInfo info = new ProgressWindowInfo();

            var cart = new Brutzelkarte(ComPort);
            try
            {
                info.ActionText = "Connecting";
                progress.Report(info);

                cart.Open();

                info.ActionText = "Setting time";
                progress.Report(info);

                cart.SetRtc(rtcTime);

                info.ProgressPercent = 100;
                progress.Report(info);
            }
            catch (OperationCanceledException)
            { }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                cart.Close();
            }
        }

        #endregion

        #region Command Methods

        private void DeleteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void DeleteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int selectedIndex = LbRoms.SelectedIndex;

            if (selectedIndex >= 0)
            {
                RomListViewItem item = RomList[selectedIndex];
                RomList.Remove(item);
            }
        }

        #endregion
    }

    public class RomListViewItem : INotifyPropertyChanged
    {
        BrutzelConfig _Config;

        public RomListViewItem()
        {
            _Config = new BrutzelConfig();
        }

        public RomListViewItem(BrutzelConfig config)
        {
            if (config != null)
            {
                _Config = config;
            }
            else
            {
                _Config = new BrutzelConfig();
            }
        }

        public BrutzelConfig Config
        {
            get => _Config;
        }

        public string FullId
        {
            get => _Config.FullId;
            set
            {
                if (value != _Config.FullId)
                {
                    _Config.FullId = value;
                    OnPropertyChanged("FullId");
                }
            }
        }

        public string Name
        {
            get => _Config.Name;
            set
            {
                if (value != _Config.Name)
                {
                    _Config.Name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        int _Size = 0;
        public int Size
        {
            get => _Size;
            set
            {
                if (value != _Size)
                {
                    _Size = value;
                    OnPropertyChanged("Size");
                }
            }
        }

        public TvType Tv
        {
            get => _Config.Tv;
            set
            {
                if (value != _Config.Tv)
                {
                    _Config.Tv = value;
                    OnPropertyChanged("Tv");
                }
            }
        }

        public CicType Cic
        {
            get => _Config.Cic;
            set
            {
                if (value != _Config.Cic)
                {
                    _Config.Cic = value;
                    OnPropertyChanged("Cic");
                }
            }
        }

        public SaveType Save
        {
            get => _Config.Save;
            set
            {
                if (value != _Config.Save)
                {
                    _Config.Save = value;
                    OnPropertyChanged("Save");
                }
            }
        }

        public byte RomOffset
        {
            get => _Config.RomOffset;
            set
            {
                if (value != _Config.RomOffset)
                {
                    _Config.RomOffset = value;
                    OnPropertyChanged("RomOffset");
                }
            }
        }

        public byte SaveOffset
        {
            get => _Config.SaveOffset;
            set
            {
                if (value != _Config.SaveOffset)
                {
                    _Config.SaveOffset = value;
                    OnPropertyChanged("SaveOffset");
                }
            }
        }

        private string _FileName = "";
        public string FileName
        {
            get => _FileName;
            set
            {
                if (value != _FileName)
                {
                    _FileName = value;
                    OnPropertyChanged("FileName");
                }
            }
        }

        private bool _IsAutoBoot = false;
        public bool IsAutoBoot
        {
            get => _IsAutoBoot;
            set
            {
                if (value != _IsAutoBoot)
                {
                    _IsAutoBoot = value;
                    OnPropertyChanged("IsAutoBoot");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ByteToMbConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int bytes = (int)value;
            int mBytes = (int)Math.Ceiling((double)bytes / 1024 / 1024);
            return String.Format("{0} MiB", mBytes);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MemoryOverflowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int mBytes = (int)value;
            return (mBytes > (Int32)parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // https://www.rhyous.com/2011/02/22/binding-visibility-to-a-bool-value-in-wpf/
    public class BoolToVisibleOrHidden : IValueConverter
    {
        #region Constructors
        /// <summary>
        /// The default constructor
        /// </summary>
        public BoolToVisibleOrHidden() { }
        #endregion

        #region IValueConverter Members
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool bValue = (bool)value;
            if (bValue)
                return Visibility.Visible;
            else
                return Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Visibility visibility = (Visibility)value;

            if (visibility == Visibility.Visible)
                return true;
            else
                return false;
        }
        #endregion
    }

    class WriteSramWorkerArgument
    {
        public int Offset { get; set; }
        public List<byte[]> DataList { get; set; }
    }

    class ReadSramWorkerArgument
    {
        public int Offset { get; set; }
        public int Size { get; set; }
        public string FileName { get; set; }
    }


    delegate List<byte[]> LoadData();
    class EraseAndWriteFlashWorkerArgument
    {
        public int Offset { get; set; }
        public int Size { get; set; }
        public int SectorSize { get; set; }
        public LoadData OnLoadData;
    }
}
