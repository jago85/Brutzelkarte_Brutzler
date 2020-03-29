using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public byte RomOffset { get; set; }  // in MiB
        public byte SaveOffset { get; set; } // in KiB

        public BrutzelConfig()
        {
            FullId = "";
            Name = "";
            Tv = TvType.Unknown;
            Cic = CicType.Unknown;
            Save = SaveType.Unknown;
            RomOffset = 0;
            SaveOffset = 0;
        }

        public byte[] GetBytes()
        {
            MemoryStream ms = new MemoryStream();

            byte[] id = new byte[2];
            if (!String.IsNullOrEmpty(Id))
            {
                for (int i = 0; i < id.Length; i++)
                {
                    if (i >= Id.Length)
                        break;
                    id[i] = (byte)Id[i];
                }
            }
            ms.WriteByte((byte)Id[0]);
            ms.WriteByte((byte)Id[1]);
            ms.WriteByte(0);

            byte[] name = new byte[32];
            if (!String.IsNullOrEmpty(Name))
            {
                for (int i = 0; i < name.Length; i++)
                {
                    if (i >= Name.Length)
                        break;
                    name[i] = (byte)Name[i];
                }
            }
            for (int i = 0; i < name.Length; i++)
            {
                ms.WriteByte(name[i]);
            }
            ms.WriteByte(0);

            ms.WriteByte((byte)Tv);
            ms.WriteByte((byte)Cic);
            ms.WriteByte((byte)Save);
            ms.WriteByte(RomOffset);
            ms.WriteByte(SaveOffset);

            return ms.ToArray();
        }
    }
}
