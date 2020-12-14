using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Brutzler;
using IniParser;
using IniParser.Model;

namespace BrutzelProg
{

    public enum TvType
    {
        Pal = 0,
        Ntsc = 1,
        Unknown = -1
    }

    public class BrutzelConfig
    {
        public string FullId { get; set; }
        public string Id
        {
            get
            {
                string id = "";
                if (FullId.Length >= 4)
                {
                    id += FullId[1];
                    id += FullId[2];
                }
                return id;
            }
        }
        public string Name { get; set; }
        public TvType Tv { get; set; }
        public CicType Cic { get; set; }
        public SaveType Save { get; set; }
        public FlashPartition[] FlashPartitions;
        public byte SaveOffset { get; set; } // in KiB
        public int RomSize { get; set; } // in Bytes
        public uint RomCrc { get; set; }

        public BrutzelConfig()
        {
            FullId = "";
            Name = "";
            Tv = TvType.Unknown;
            Cic = CicType.Unknown;
            Save = SaveType.Unknown;
            SaveOffset = 0;
        }

        private string GetTvString()
        {
            switch (Tv)
            {
                case TvType.Ntsc:
                    return "NTSC";
                case TvType.Pal:
                    return "PAL";
            }

            throw new Exception("Unknown TV");
        }

        private static TvType GetTvFromString(string tv)
        {
            switch (tv)
            {
                case "NTSC":
                    return TvType.Ntsc;
                case "PAL":
                    return TvType.Pal;
            }
            throw new Exception("Unknown TV");
        }

        private string GetCicString()
        {
            switch (Cic)
            {
                case CicType.Cic6101:
                    return "6101";
                case CicType.Cic6102:
                    return "6102";
                case CicType.Cic6103:
                    return "6103";
                case CicType.Cic6105:
                    return "6105";
                case CicType.Cic6106:
                    return "6106";
            }

            throw new Exception("Unknown CIC");
        }

        private static CicType GetCicFromString(string cic)
        {
            switch (cic)
            {
                case "6101":
                    return CicType.Cic6101;
                case "6102":
                    return CicType.Cic6102;
                case "6103":
                    return CicType.Cic6103;
                case "6105":
                    return CicType.Cic6105;
                case "6106":
                    return CicType.Cic6106;
            }
            throw new Exception("Unknown CIC");
        }

        private string GetSaveString()
        {
            switch (Save)
            {
                case SaveType.None:
                    return "OFF";
                case SaveType.Eep4K:
                    return "EEP4K";
                case SaveType.Eep16K:
                    return "EEP16K";
                case SaveType.Sram32:
                    return "SRAM32";
                case SaveType.Sram32x3:
                    return "SRAM32x3";
                case SaveType.FlashRam:
                    return "FLASHRAM";
            }

            throw new Exception("Unknown CIC");
        }

        private static SaveType GetSaveFromString(string save)
        {
            switch (save)
            {
                case "OFF":
                    return SaveType.None;
                case "EEP4K":
                    return SaveType.Eep4K;
                case "EEP16K":
                    return SaveType.Eep16K;
                case "SRAM32":
                    return SaveType.Sram32;
                case "SRAM32x3":
                    return SaveType.Sram32x3;
                case "FLASHRAM":
                    return SaveType.FlashRam;
            }
            throw new Exception("Unknown CIC");
        }

        public void WriteToIni(IniData iniData, int romIndex)
        {
            string sectionName = "ROM" + romIndex.ToString();
            iniData.Sections.AddSection(sectionName);
            iniData[sectionName].AddKey("FULL_ID", FullId);
            iniData[sectionName].AddKey("ID", Id);
            iniData[sectionName].AddKey("NAME", Name);
            iniData[sectionName].AddKey("TV", GetTvString());
            iniData[sectionName].AddKey("CIC", GetCicString());
            iniData[sectionName].AddKey("SAVE", GetSaveString());
            iniData[sectionName].AddKey("SAVE_OFFSET", SaveOffset.ToString());
            iniData[sectionName].AddKey("ROM_SIZE", RomSize.ToString());
            iniData[sectionName].AddKey("ROM_CRC", RomCrc.ToString("X08"));

            for (int i = 0; i < 32; i++)
            {
                string mappingKey = "MAPPING" + i.ToString();
                byte mapping = 0;
                if (FlashPartitions.Length > i)
                    mapping = FlashPartitions[i].Offset;
                iniData[sectionName].AddKey(mappingKey, mapping.ToString());
            }

        }

        public static BrutzelConfig CreateFromIniIniData(IniData iniData, int romIndex)
        {
            string sectionName = "ROM" + romIndex.ToString();
            BrutzelConfig cfg = new BrutzelConfig();
            cfg.FullId = iniData[sectionName].GetKeyData("FULL_ID").Value;
            cfg.Name = iniData[sectionName].GetKeyData("NAME").Value;
            cfg.Tv = GetTvFromString(iniData[sectionName].GetKeyData("TV").Value);
            cfg.Cic = GetCicFromString(iniData[sectionName].GetKeyData("CIC").Value);
            cfg.Save = GetSaveFromString(iniData[sectionName].GetKeyData("SAVE").Value);
            cfg.SaveOffset = byte.Parse(iniData[sectionName].GetKeyData("SAVE_OFFSET").Value);
            cfg.RomSize = int.Parse(iniData[sectionName].GetKeyData("ROM_SIZE").Value);
            cfg.RomCrc = uint.Parse(iniData[sectionName].GetKeyData("ROM_CRC").Value, System.Globalization.NumberStyles.HexNumber);

            // Flash partitions must be assigned externally

            return cfg;
        }
    }
}
