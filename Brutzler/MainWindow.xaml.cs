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
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using IniParser;
using IniParser.Model;
using IniParser.Parser;

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
        const int RomPartitionSize = 2 * 1024 * 1024;
        const int BootMemoryOffset = RomMemorySize;
        const int BootMemorySize = 8 * 1024 * 1024;
        const int BootSectorSize = 4 * 1024;
        const int BootPageSize = 256;

        const int SaveRamSize = 256 * 1024;
        const int SaveRamFragmentSize = 1024;

        const string DfsConfigPath = "/brutzelkarte/config.ini";
        const int DfsOffset = 0x200000;

        string ComPort = "";
        public ObservableCollection<RomListViewItem> RomList { get; set; }

        FlashManager _FlashManager = new FlashManager(RomMemorySize / RomPartitionSize, RomPartitionSize);
        SaveRamManager _SaveRamManager = new SaveRamManager(SaveRamSize, SaveRamFragmentSize);

        public MainWindow()
        {
            InitializeComponent();

            Title = "BRUTZLER v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

            RomList = new ObservableCollection<RomListViewItem>();
            RomList.CollectionChanged += RomList_CollectionChanged;
            DataContext = this;

            ComPort = Settings.Default.ComPort;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(ComPort))
            {
                ShowConnectionSettings();
            }

            if ((Settings.Default.ConnectOnStartup)
                && (!String.IsNullOrEmpty(ComPort)))
            {
                LoadCardContent();
            }
        }

        private void RomList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateOffsets();
        }

        void AddRomDialog()
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Multiselect = true;
            d.Filter = "rom files (*.v64;*.z64;*.n64)|*.v64;*.z64;*.n64|all files|*.*";
            if (d.ShowDialog(this).Value == true)
            {
                AddRoms(d.FileNames);
            }
        }

        void AddRoms(string[] files)
        {
            if (files == null)
                return;

            foreach (string fileName in files)
            {
                string extension = System.IO.Path.GetExtension(fileName).ToLower();
                if ((extension == ".v64")
                        || (extension == ".z64")
                        || (extension == ".n64"))
                {
                    AddRom(fileName);
                }
            }
        }

        void AddRom(string fileName)
        {
            BrutzelConfig config = GetRomConfig(fileName);
            if (config != null)
            {
                RomListViewItem item = new RomListViewItem(config)
                {
                    FileName = fileName
                };
                RomList.Add(item);
            }
        }

        void RemoveRom(RomListViewItem item)
        {
            // Return memory if reserved
            if (item.Config.FlashPartitions != null)
            {
                _FlashManager.ReturnPartitions(item.Config.FlashPartitions);
                item.Config.FlashPartitions = null;
            }
            if (item.IsSaveRamAllocated)
            {
                _SaveRamManager.Return((int)item.SaveOffset * SaveRamFragmentSize);
            }
            RomList.Remove(item);
        }

        private void RemoveAllRoms()
        {
            while (RomList.Count > 0)
                RemoveRom(RomList[0]);
        }

        private void RemoveFlashedRoms()
        {
            var flashedRoms = RomList.Where(r => r.IsFlashed == true ).ToArray();
            foreach (var rom in flashedRoms)
                RemoveRom(rom);
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

            // Deallocate the SRAM if the Save Type has changed
            if ((item.IsSaveRamAllocated) && (item.Save != wnd.SelectedSave))
            {
                MessageBoxResult result = MessageBox.Show("This will clear the Save memory for this game!\r\n\r\nAre you sure?", "Save will be lost", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
                if (result != MessageBoxResult.OK)
                {
                    return;
                }
                _SaveRamManager.Return(item.SaveOffset * SaveRamFragmentSize);
                item.IsSaveRamAllocated = false;
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
                config.RomSize = GetFileSize(fileName);
                config.RomCrc = GetFileCrc(fileName);
            }
            catch (Exception)
            {
                return null;
            }
            string fullId = "";
            for (int i = 0; i < 4; i++)
            {
                char c = (char)bootSector[i+ 0x3B];
                if (c == '\0')
                    break;
                fullId += c;
            }
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

            switch (GetRomEndianess(bootSector))
            {
                case RomEndianess.BigEndian:
                    break;
                case RomEndianess.ByteSwapped:
                    SwapArray(bootSector);
                    break;
                case RomEndianess.LittleEndian:
                case RomEndianess.WordSwapped:
                    throw new Exception("Unhandled ROM endianess");
                default:
                    throw new Exception("Invalid ROM");
            }

            return bootSector;
        }

        int GetFileSize(string filename)
        {
            FileInfo info = new FileInfo(filename);
            return (int)info.Length;
        }

        UInt32 GetFileCrc(string filename)
        {
            byte[] buffer = File.ReadAllBytes(filename);

            if (GetRomEndianess(buffer) == RomEndianess.ByteSwapped)
            {
                SwapArray(buffer);
            }

            return Crc32Algorithm.Compute(buffer);
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
            int flashLevel = 0;
            int saveLevel = 0;
            for (int i = 0; i < RomList.Count; i++)
            {
                flashLevel += (byte)Math.Ceiling((float)RomList[i].Size / 1024 / 1024);
                saveLevel += (byte)Math.Ceiling((float)GetSaveSize(RomList[i].Save) / 1024);
            }
            FlashLevel = flashLevel;
            SramLevel = saveLevel;
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
                int autoBootIndex = -1;
                var autoBootRom = RomList.FirstOrDefault((e) => {
                    return e.IsAutoBoot;
                });
                if (autoBootRom != null)
                {
                    autoBootIndex = RomList.IndexOf(autoBootRom);
                }

                IniData iniData = new IniData();
                iniData.Sections.AddSection("CART");
                iniData["CART"].AddKey("NUM_ROMS", RomList.Count.ToString());
                iniData["CART"].AddKey("AUTOBOOT", autoBootIndex.ToString());

                for (int i = 0; i < RomList.Count; i++)
                {
                    RomList[i].Config.WriteToIni(iniData, i);
                }

                // Write INI data to to file
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    StreamIniDataParser streamIniDataParser = new StreamIniDataParser();
                    streamIniDataParser.WriteData(writer, iniData);
                }
            }

            byte[] dfsData = dfs.GetImage();
            return dfsData;
        }

        IniData ReadConfigFromCart()
        {
            Brutzelkarte cart = new Brutzelkarte(ComPort);

            cart.Open();
            cart.ReadVersion();
            cart.SendAddr((BootMemoryOffset + DfsOffset) / 4);

            MemoryStream ms = new MemoryStream();

            var dat = cart.ReadFlashPage();
            SwapArray(dat);
            ms.Write(dat, 0, dat.Length);

            int sectorCount = (int)Utils.ReadArrayBigEndianUint32(dat, (int)DfsSector.SECTOR_SIZE - 8);

            for (int i = 0; i < sectorCount; i++)
            {
                dat = cart.ReadFlashPage();
                SwapArray(dat);
                ms.Write(dat, 0, dat.Length);
            }

            byte[] configData = ms.ToArray();

            IniData iniDat = null;
            try
            {
                DragonFs fs = DragonFs.CreateFromStream(new MemoryStream(configData));
                var file = fs.OpenFile(DfsConfigPath, FileAccess.Read);

                StreamReader reader = new StreamReader(file);
                String iniString = reader.ReadToEnd();

                IniDataParser parser = new IniDataParser();
                iniDat = parser.Parse(iniString);
            }
            catch (Exception)
            {
                
            }

            cart.Close();

            return iniDat;
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

        List<byte[]> GetPages(string filename, int maxBytes, int pageSize)
        {
            List<byte[]> dataList = new List<byte[]>();

            using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read))
            {
                while (fs.Position < fs.Length)
                {

                    byte[] data = new byte[pageSize];
                    fs.Read(data, 0, data.Length);
                    dataList.Add(data);
                    if (fs.Position >= maxBytes)
                        break;
                }
            }
            return dataList;
        }

        RomEndianess GetRomEndianess(byte[] firstPage)
        {
            UInt32 firstWord = 0;
            firstWord |= (UInt32)firstPage[3];
            firstWord |= (UInt32)firstPage[2] << 8;
            firstWord |= (UInt32)firstPage[1] << 16;
            firstWord |= (UInt32)firstPage[0] << 24;

            switch (firstWord)
            {
                case 0x80371240:
                    return RomEndianess.BigEndian;
                case 0x37804012:
                    return RomEndianess.ByteSwapped;
                case 0x12408037:
                    return RomEndianess.WordSwapped;
                case 0x40123780:
                    return RomEndianess.LittleEndian;
            }

            return RomEndianess.Unknown;
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

        private void ShowConnectionSettings()
        {
            ConnectionSettingsWindow wnd = new ConnectionSettingsWindow();
            wnd.Owner = this;
            if (wnd.ShowDialog().Value == true)
            {
                ComPort = wnd.SelectedPort;
                Settings.Default.ComPort = wnd.SelectedPort;
                Settings.Default.ConnectOnStartup = wnd.ConnectOnStart;
                Settings.Default.Save();
            }
        }

        private void LoadCardContent()
        {
            IniData iniDat;
            try
            {
                iniDat = ReadConfigFromCart();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (iniDat != null)
            {
                RemoveFlashedRoms();

                int numRoms = int.Parse(iniDat.Sections["CART"].GetKeyData("NUM_ROMS").Value);

                for (int romIndex = 0; romIndex < numRoms; romIndex++)
                {
                    BrutzelConfig config = BrutzelConfig.CreateFromIniIniData(iniDat, romIndex);
                    List<FlashPartition> partitionList = new List<FlashPartition>();
                    for (int i = 0; i < 32; i++)
                    {
                        if (config.RomSize <= i * _FlashManager.PartitionSize)
                            break;

                        string sectionName = "ROM" + romIndex.ToString();
                        string mappingKey = "MAPPING" + i.ToString();
                        byte mapping = byte.Parse(iniDat[sectionName].GetKeyData(mappingKey).Value);
                        FlashPartition partition = _FlashManager.GetPartition(mapping);
                        partitionList.Add(partition);

                    }
                    config.FlashPartitions = partitionList.ToArray();

                    RomListViewItem item = new RomListViewItem(config);
                    // Allocate SaveMem if needed
                    int saveSize = GetSaveSize(config.Save);
                    if (saveSize > 0)
                    {
                        _SaveRamManager.AllocAt(config.SaveOffset * SaveRamFragmentSize, saveSize);
                        item.IsSaveRamAllocated = true;
                    }

                    item.IsFlashed = true;
                    RomList.Add(item);
                }

                int autobootIndex = int.Parse(iniDat.Sections["CART"].GetKeyData("AUTOBOOT").Value);
                if (autobootIndex >= 0)
                {
                    RomList[autobootIndex].IsAutoBoot = true;
                }
            }
            else
            {
                MessageBox.Show("Could not load the config from the cart.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        bool CheckDeviceSelected()
        {
            if (String.IsNullOrEmpty(ComPort))
            {
                MessageBox.Show("No device selected.\r\nPlease go into Connection Settings and select a device.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
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
            AddRomDialog();
            UpdateOffsets();
        }

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            RomListViewItem item = menu.DataContext as RomListViewItem;
            RemoveRom(item);
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
                arg.Size = SaveRamSize;
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
                arg.DataList = GetPages(d.FileName, SaveRamSize, 256);
                arg.Offset = 0;
                RunTaskInProgressWindow("Restore SRAM", Action_WriteSramData, arg);
            }
        }

        private void MenuItem_ClearSave_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            RomListViewItem item = menu.DataContext as RomListViewItem;

            if (item.Save == SaveType.None)
            {
                MessageBox.Show("This ROM does not have Save RAM ", "No Save RAM", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (item.IsSaveRamAllocated == false)
            {
                MessageBox.Show("This ROM does not have Save RAM allocated.\r\nPlease FLASH the ROM first.", "Save RAM not allocated", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string msgText = String.Format("This clears the savegame memory for:\r\n\r\n    {0}\r\n\r\nAre you sure?", item.Name);
            MessageBoxResult result = MessageBox.Show(msgText, "Erase Savegame", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (result == MessageBoxResult.OK)
            {
                int size = GetSaveSize(item.Save);
                WriteSramWorkerArgument arg = new WriteSramWorkerArgument();
                arg.Offset = item.SaveOffset * SaveRamFragmentSize;
                arg.DataList = new List<byte[]>();
                while (size > 0)
                {
                    // Clear FRAM to 0xff, all others to 0x00
                    byte[] clearData = Enumerable.Repeat((byte)((item.Save == SaveType.FlashRam) ? 0xFF : 0x00), 256).ToArray();
                    arg.DataList.Add(clearData);
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
            {
                MessageBox.Show("This ROM does not have Save RAM ", "No Save RAM", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (item.IsSaveRamAllocated == false)
            {
                MessageBox.Show("This ROM does not have Save RAM allocated.\r\nPlease FLASH the ROM first.", "Save RAM not allocated", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenFileDialog d = new OpenFileDialog();
            d.Filter = String.Format("savegame file (*.{0})|*.{0}|all files|*.*", GetSaveExtension(item.Save));
            if (d.ShowDialog().Value == true)
            {
                WriteSramWorkerArgument arg = new WriteSramWorkerArgument();
                arg.DataList = GetPages(d.FileName, GetSaveSize(item.Save), 256);
                arg.Offset = item.SaveOffset * SaveRamFragmentSize;
                RunTaskInProgressWindow("Restore Savegame", Action_WriteSramData, arg);
            }
        }

        private void MenuItem_BackupSave_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            RomListViewItem item = menu.DataContext as RomListViewItem;

            if (item.Save == SaveType.None)
            {
                MessageBox.Show("This ROM does not have Save RAM ", "No Save RAM", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (item.IsSaveRamAllocated == false)
            {
                MessageBox.Show("This ROM does not have Save RAM allocated.\r\nPlease FLASH the ROM first.", "Save RAM not allocated", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog d = new SaveFileDialog();
            d.Filter = String.Format("savegame file (*.{0})|*.{0}|all files|*.*", GetSaveExtension(item.Save));
            if (d.ShowDialog().Value == true)
            {
                ReadSramWorkerArgument arg = new ReadSramWorkerArgument();
                arg.Offset = item.SaveOffset * SaveRamFragmentSize;
                arg.Size = GetSaveSize(item.Save);
                arg.FileName = d.FileName;
                RunTaskInProgressWindow("Backup Savegame", Action_ReadSramData, arg);
            }
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

        private void MenuItem_LoadFromCartClick(object sender, RoutedEventArgs e)
        {
            if (!CheckDeviceSelected())
            {
                return;
            }
            LoadCardContent();
        }

        private void MenuItem_ConnectionSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowConnectionSettings();
        }

        private void MenuItem_AboutClick(object sender, RoutedEventArgs e)
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var info = asm.GetName();
            string copyright = "";
            foreach (var attr in asm.GetCustomAttributes(false))
            {
                if (attr.GetType() == typeof(System.Reflection.AssemblyCopyrightAttribute))
                {
                    copyright = ((System.Reflection.AssemblyCopyrightAttribute)attr).Copyright;
                }
            }
            string text = String.Format("{0} v{1}\r\n{2}", info.Name, info.Version.ToString(3), copyright);
            MessageBox.Show(text, "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Buttons

        private void Button_AddRom_Click(object sender, RoutedEventArgs e)
        {
            AddRomDialog();
        }

        private void Button_DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            RemoveAllRoms();
        }

        private void Button_WriteFlash_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckDeviceSelected())
            {
                return;
            }
            if (FlashLevel > FlashSize)
            {
                MessageBox.Show("The ROMs in the list require too much FLASH.\r\nPlease remove some ROMs.", "Not enough FLASH", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (SramLevel > SaveRamSize / 1024)
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

                // get total sectors-to-delete count
                int sectorsToDelete = 0;
                foreach (var item in RomList)
                {
                    if (item.IsFlashed)
                        continue;

                    // if FlashPartitions is null, grab free memory from the manager
                    if (item.Config.FlashPartitions == null)
                    {
                        int partitionCount = (int)Math.Ceiling((double)item.Size / RomPartitionSize);
                        item.Config.FlashPartitions = _FlashManager.GetPartitions(partitionCount);
                    }
                    sectorsToDelete += item.Config.FlashPartitions.Length * RomPartitionSize / RomSectorSize;
                }

                // Assign the Save RAM from the manager
                foreach (var item in RomList)
                {
                    if (item.IsSaveRamAllocated)
                        continue;

                    int saveSize = GetSaveSize(item.Save);
                    if (saveSize > 0)
                    {
                        try
                        {
                            item.SaveOffset = (byte)(_SaveRamManager.Alloc(saveSize) / SaveRamFragmentSize);
                        }
                        catch (SaveRamFragmentedException)
                        {
                            // SaveRam must be defragmented
                            DefragSaveRam(cart, progress, ct);

                            // Alloc should now be possible
                            item.SaveOffset = (byte)(_SaveRamManager.Alloc(saveSize) / SaveRamFragmentSize);
                        }

                        info.ActionText = "Clear Save";
                        progress.Report(info);

                        // Clear the SaveRam
                        int addr = item.SaveOffset * SaveRamFragmentSize;
                        byte[] sramClearData = Enumerable.Repeat((byte)((item.Save == SaveType.FlashRam) ? 0xFF : 0x00), 256).ToArray();
                        for (int i = 0; i < saveSize / sramClearData.Length; i++)
                        {
                            cart.WriteSram(addr, sramClearData);
                            if (cart.PendingAck > MaxPendingAck)
                            {
                                cart.WaitAck();
                            }
                            info.ProgressPercent = 100 * (i + 1) * sramClearData.Length / saveSize;
                            addr += sramClearData.Length;
                        }
                        while (cart.PendingAck > 0)
                        {
                            cart.WaitAck();
                        }

                        // SaveRam is now allocated
                        item.IsSaveRamAllocated = true;
                    }
                }

                ct.ThrowIfCancellationRequested();

                // Erase Flash all partitions
                int sectorsDeleted = 1;
                foreach (var item in RomList)
                {
                    if (item.IsFlashed)
                        continue;

                    foreach (var partition in item.Config.FlashPartitions)
                    {
                        int bytesToDelete = RomPartitionSize;
                        int addr = RomPartitionSize * partition.Offset;

                        while (bytesToDelete > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            info.ActionText = String.Format("Erasing {0} / {1}", sectorsDeleted, sectorsToDelete);
                            info.ProgressPercent = 100 * (sectorsDeleted) / sectorsToDelete;
                            progress.Report(info);

                            cart.EraseSector(addr);
                            cart.WaitAck();

                            addr += RomSectorSize;
                            bytesToDelete -= RomSectorSize;
                            sectorsDeleted++;
                        }
                    }
                }


                // Write the roms
                foreach (var item in RomList)
                {
                    ct.ThrowIfCancellationRequested();

                    if (item.IsFlashed)
                        continue;

                    FileInfo fileInfo = new FileInfo(item.FileName);
                    info.ActionText = "Loading ROM: " + fileInfo.Name;
                    progress.Report(info);

                    // Load the ROM
                    List<byte[]> pageList;
                    try
                    {
                        pageList = GetPages(item.FileName, item.Size, RomPageSize);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                        continue;
                    }
                    if (GetRomEndianess(pageList[0]) == RomEndianess.BigEndian)
                    {
                        MakeByteswapped(pageList);
                    }

                    // Write ROM data
                    int partitionIndex = 0;
                    int bytesLeftInPartition = RomPartitionSize;
                    int addr = item.Config.FlashPartitions[partitionIndex].Offset * RomPartitionSize;

                    int pagesWritten = 0;
                    foreach (byte[] dataPage in pageList)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (bytesLeftInPartition == 0)
                        {
                            partitionIndex++;
                            bytesLeftInPartition = RomPartitionSize;
                            addr = item.Config.FlashPartitions[partitionIndex].Offset * RomPartitionSize;
                        }

                        cart.WritePage(addr, dataPage);
                        if (cart.PendingAck > MaxPendingAck)
                        {
                            cart.WaitAck();
                        }

                        bytesLeftInPartition -= dataPage.Length;

                        addr += dataPage.Length;
                        pagesWritten++;
                        info.ActionText = String.Format("Writing {0} {1} / {2} KB", item.Name, pagesWritten * 256 / 1024, pageList.Count * 256 / 1024);
                        info.ProgressPercent = 100 * pagesWritten / pageList.Count;
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
                        // Don't want to break the config
                        //ct.ThrowIfCancellationRequested();

                        cart.EraseSector(addr);
                        cart.WaitAck();
                        addr += BootSectorSize;
                        erasedBytes += BootSectorSize;
                    }

                    // Write config data
                    addr = BootMemoryOffset + DfsOffset;
                    while (ms.Position < ms.Length)
                    {
                        // Don't want to break the config
                        //ct.ThrowIfCancellationRequested();

                        byte[] pageData = new byte[256];
                        ms.Read(pageData, 0, pageData.Length);
                        SwapArray(pageData);
                        cart.WritePage(addr, pageData);
                        cart.WaitAck();
                        addr += pageData.Length;
                    }

                    foreach (RomListViewItem item in RomList)
                    {
                        item.IsFlashed = true;
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

        void DefragSaveRam(Brutzelkarte cart, IProgress<ProgressWindowInfo> progress, CancellationToken ct)
        {
            ProgressWindowInfo info = new ProgressWindowInfo();
            info.ActionText = "Defragmenting SRAM (Read)";
            progress.Report(info);

            // Read the SRAM
            int pageCnt = SaveRamSize / 256;
            byte[] readBuffer = new byte[pageCnt * 256];

            cart.SendAddr(0);
            for (int i = 0; i < pageCnt; i++)
            {
                ct.ThrowIfCancellationRequested();

                byte[] page = cart.ReadSramPage();
                page.CopyTo(readBuffer, i * 256);
                info.ActionText = String.Format("Defragmenting SRAM (Read) {0} / {1} KiB", (i + 1) * 256 / 1024, SaveRamSize / 1024);
                info.ProgressPercent = i * 100 / pageCnt;
                progress.Report(info);
            }

            // Defrag
            byte[] writeBuffer = new byte[readBuffer.Length];
            readBuffer.CopyTo(writeBuffer, 0);
            _SaveRamManager.DefragmentMem((item, newOffset) => {
                // Find the RomList-Item with this SaveOffset and change it to the new offset
                var listItem = RomList.Where(x => (x.IsSaveRamAllocated && (x.SaveOffset == (item.Offset / SaveRamFragmentSize)))).First();
                listItem.SaveOffset = (byte)(newOffset / SaveRamFragmentSize);

                // Copy the SRAM buffer to the new buffer
                Array.Copy(readBuffer, item.Offset, writeBuffer, newOffset, item.Size);
            });

            // Write back
            info.ActionText = "Defragmenting SRAM (Write)";
            info.ProgressPercent = 0;
            progress.Report(info);

            MemoryStream ms = new MemoryStream(writeBuffer);
            byte[] pageBuffer = new byte[256];
            while (ms.Position < SaveRamSize)
            {
                // Don't allow interrupting the write back process
                //ct.ThrowIfCancellationRequested();

                ms.Read(pageBuffer, 0, pageBuffer.Length);
                cart.WriteSram((int)ms.Position, pageBuffer);
                if (cart.PendingAck > MaxPendingAck)
                {
                    cart.WaitAck();
                }

                info.ActionText = String.Format("Defragmenting SRAM (Write) {0} / {1} KiB", ms.Position / 1024, SaveRamSize / 1024);
                info.ProgressPercent = (int)(ms.Position * 100 / SaveRamSize);
                progress.Report(info);
            }
            while (cart.PendingAck > 0)
            {
                cart.WaitAck();
            }
        }

        void Action_UpdateBootloader(IProgress<ProgressWindowInfo> progress, CancellationToken ct, object argument)
        {
            string fileName = (string)argument;

            ProgressWindowInfo info = new ProgressWindowInfo();
            info.ActionText = "Connecting";
            progress.Report(info);

            List<byte[]> pageList = GetPages(fileName, BootMemorySize, BootPageSize);
            if (GetRomEndianess(pageList[0]) == RomEndianess.BigEndian)
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
                RemoveRom(item);
            }
        }

        #endregion

        private void LbRoms_DragEnter(object sender, DragEventArgs e)
        {

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        private void LbRoms_Drop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            AddRoms(files);
        }
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
        public int Size
        {
            get => _Config.RomSize;
            set
            {
                if (value != _Config.RomSize)
                {
                    _Config.RomSize = value;
                    OnPropertyChanged("Size");
                }
            }
        }

        public uint Crc
        {
            get => _Config.RomCrc;
            set
            {
                if (value != _Config.RomCrc)
                {
                    _Config.RomCrc = value;
                    OnPropertyChanged("Crc");
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

        private bool _IsFlashed = false;
        public bool IsFlashed
        {
            get => _IsFlashed;
            set
            {
                if (value != _IsFlashed)
                {
                    _IsFlashed = value;
                    OnPropertyChanged("IsFlashed");
                }
            }
        }

        private bool _IsSaveRamAllocated;
        public bool IsSaveRamAllocated
        {
            get => _IsSaveRamAllocated;
            set
            {
                if (value != _IsSaveRamAllocated)
                {
                    _IsSaveRamAllocated = value;
                    OnPropertyChanged("IsSaveRamAllocated");
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

    public enum RomEndianess
    {
        Unknown,
        BigEndian,  // z64
        ByteSwapped, // v64
        LittleEndian,
        WordSwapped
    }

    public class FlashPartition
    {
        public byte Offset { get; set; }

        public bool Used { get; set; }

        public override string ToString()
        {
            return string.Format("Offset: {0}, Used: {1}", Offset, Used);
        }
    }

    public class FlashManager
    {
        List<FlashPartition> _FlashList = new List<FlashPartition>();
        int _PartitionSize;

        public FlashManager(int partitionCount, int partitionSize)
        {
            _PartitionSize = partitionSize;
            for (int i = 0; i < partitionCount; i++)
            {
                FlashPartition item = new FlashPartition();
                item.Used = false;
                item.Offset = (byte)(i);
                _FlashList.Add(item);
            }
        }

        // For testing: Random partition order 
        //public FlashManager(int partitionCount)
        //{
        //    List<int> indexList = new List<int>();
        //    for (int i = 0; i < partitionCount; i++)
        //    {
        //        indexList.Add(i);
        //    }

        //    while (indexList.Count > 0)
        //    {
        //        var rand = new Random();
        //        int nextIndex = indexList[rand.Next(0, indexList.Count - 1)];
        //        indexList.Remove(nextIndex);

        //        FlashPartition item = new FlashPartition();
        //        item.Dirty = true;
        //        item.Used = false;
        //        item.Offset = (byte)nextIndex;
        //        _FlashList.Add(item);
        //    }
        //}

        public FlashPartition[] GetPartitions(int count)
        {
            if (count < 1)
                throw new ArgumentException("Must be greater than 0.", "count");

            List<FlashPartition> returnList = new List<FlashPartition>();
            foreach (var item in _FlashList)
            {
                if (item.Used == false)
                {
                    returnList.Add(item);
                    count--;
                    if (count == 0)
                        break;
                }
            }

            if (count > 0)
            {
                // not enough free items
                return null;
            }

            foreach (var item in returnList)
                item.Used = true;
            return returnList.ToArray();

        }

        public FlashPartition GetPartition(byte index)
        {
            var partition = _FlashList[index];

            if (partition.Used)
                throw new InvalidOperationException("Partition already in use");

            partition.Used = true;
            return partition;
        }

        public void ReturnPartition(byte index)
        {
            if (_FlashList[index].Used == false)
                throw new InvalidOperationException("Partition is not in use");

            _FlashList[index].Used = false;
        }

        public void ReturnPartition(FlashPartition partition)
        {
            ReturnPartition(partition.Offset);
        }

        public void ReturnPartitions(FlashPartition[] partitions)
        {
            foreach (var item in partitions)
                ReturnPartition(item);
        }

        public int FreePartitionCount
        {
            get { return _FlashList.Count((item) => { return (item.Used == false); }); }
        }

        public int UsedPartitionCount
        {
            get { return _FlashList.Count((item) => { return (item.Used == true); }); }
        }

        public int PartitionSize
        {
            get => _PartitionSize;
        }
    }
}
