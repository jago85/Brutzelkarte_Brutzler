using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrutzelProg
{
    static class CartDb
    {
        static List<RomInfo> _KnownRoms = new List<RomInfo>(new RomInfo[] {
            new RomInfo { Id = "3D", Save = SaveType.Eep16K, Cic = CicType.Cic6102, Name = "Doraemon 3 - Nobi Dai No Machi SOS" },
            new RomInfo { Id = "4W", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "40 Winks" },
            new RomInfo { Id = "A2", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "Virtual Pro Wrestling 2" },
            new RomInfo { Id = "AB", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Airboarder 64" },
            new RomInfo { Id = "AD", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Worms Armageddon" },
            new RomInfo { Id = "AF", Save = SaveType.FlashRam, Cic = CicType.Cic6102, Name = "Doubutsu no Mori" },
            new RomInfo { Id = "AG", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Aero Gauge" },
            new RomInfo { Id = "AL", Save = SaveType.Sram32, Cic = CicType.Cic6103, Name = "Super Smash Bros." },
            new RomInfo { Id = "AY", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "Aidyn Chronicles The First Mage" },
            new RomInfo { Id = "B7", Save = SaveType.Eep16K, Cic = CicType.Cic6105, Name = "Banjo-Tooie" },
            new RomInfo { Id = "BC", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Blast Corps" },
            new RomInfo { Id = "BD", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Bomberman Hero" },
            new RomInfo { Id = "BH", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Body Harvest" },
            new RomInfo { Id = "BK", Save = SaveType.Eep4K, Cic = CicType.Cic6103, Name = "Banjo-Kazooie" },
            new RomInfo { Id = "BM", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Bomberman 64" },
            new RomInfo { Id = "BN", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Bakuretsu Muteki Bangaioh" },
            new RomInfo { Id = "BV", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Bomberman 64: Second Attack" },
            new RomInfo { Id = "CC", Save = SaveType.FlashRam, Cic = CicType.Cic6102, Name = "Command & Conquer" },
            new RomInfo { Id = "CG", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Choro-Q 64 II" },
            new RomInfo { Id = "CH", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Chopper Attack" },
            new RomInfo { Id = "CK", Save = SaveType.FlashRam, Cic = CicType.Cic6102, Name = "NBA Courtside 2 featuring Kobe Bryant" },
            new RomInfo { Id = "CR", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Penny Racers" },
            new RomInfo { Id = "CT", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Chameleon Twist" },
            new RomInfo { Id = "CU", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Cruis'n USA" },
            new RomInfo { Id = "CW", Save = SaveType.Eep16K, Cic = CicType.Cic6106, Name = "Cruis'n World" },
            new RomInfo { Id = "CX", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Custom Robo" },
            new RomInfo { Id = "CZ", Save = SaveType.Eep16K, Cic = CicType.Cic6102, Name = "Custom Robo V2" },
            new RomInfo { Id = "D2", Save = SaveType.Eep16K, Cic = CicType.Cic6102, Name = "Doraemon 2 - Hikari no Shinden" },
            new RomInfo { Id = "D6", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "Densha de Go! 64" },
            new RomInfo { Id = "DA", Save = SaveType.FlashRam, Cic = CicType.Cic6102, Name = "Derby Stallion 64" },
            new RomInfo { Id = "DL", Save = SaveType.FlashRam, Cic = CicType.Cic6105, Name = "Legend of Zelda: Majora's Mask, The" },
            new RomInfo { Id = "DO", Save = SaveType.Eep16K, Cic = CicType.Cic6105, Name = "Donkey Kong 64" },
            new RomInfo { Id = "DP", Save = SaveType.Eep16K, Cic = CicType.Cic6105, Name = "Donkey Kong 64" },
            new RomInfo { Id = "DQ", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Donald Duck: Goin' Quackers" },
            new RomInfo { Id = "DR", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Doraemon: Mittsu no Seireiseki" },
            new RomInfo { Id = "DU", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Loony Toons: Duck Dodgers" },
            new RomInfo { Id = "DY", Save = SaveType.Eep4K, Cic = CicType.Cic6103, Name = "Diddy Kong Racing" },
            new RomInfo { Id = "DZ", Save = SaveType.Sram32x3,   Cic = CicType.Cic6102, Name = "Dezaemon 3D" },
            new RomInfo { Id = "EA", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "PGA European Tour" },
            new RomInfo { Id = "EP", Save = SaveType.Eep16K, Cic = CicType.Cic6102, Name = "Star Wars Episode 1 Racer" },
            new RomInfo { Id = "ER", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "AeroFighters Assault" },
            new RomInfo { Id = "EV", Save = SaveType.Eep16K, Cic = CicType.Cic6102, Name = "Neon Genesis Evangelion" },
            new RomInfo { Id = "F2", Save = SaveType.Eep16K, Cic = CicType.Cic6102, Name = "F-1 World Grand Prix II" },
            new RomInfo { Id = "FH", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Bass Hunter 64" },
            new RomInfo { Id = "FU", Save = SaveType.Eep16K, Cic = CicType.Cic6105, Name = "Conker's Bad Fur Day" },
            new RomInfo { Id = "FW", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "F-1 World Grand Prix" },
            new RomInfo { Id = "FX", Save = SaveType.Eep4K, Cic = CicType.Cic6101, Name = "Star Fox 64" },
            new RomInfo { Id = "FZ", Save = SaveType.Sram32, Cic = CicType.Cic6106, Name = "F-Zero X" },
            new RomInfo { Id = "GC", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "GT64 Championship Edition" },
            new RomInfo { Id = "GE", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "GoldenEye 007" },
            new RomInfo { Id = "GU", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Tsumi to Batsu" },
            new RomInfo { Id = "GV", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Glover" },
            new RomInfo { Id = "HA", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Bomberman 64" },
            new RomInfo { Id = "HP", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Heiwa Pachinko World 64" },
            new RomInfo { Id = "IC", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Indy Racing 2000" },
            new RomInfo { Id = "IJ", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Indiana Jones and the Infernal Machine" },
            new RomInfo { Id = "JD", Save = SaveType.FlashRam, Cic = CicType.Cic6105, Name = "Jet Force Gemini" },
            new RomInfo { Id = "JF", Save = SaveType.FlashRam, Cic = CicType.Cic6105, Name = "Jet Force Gemini" },
            new RomInfo { Id = "JM", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Earthworm Jim 3D" },
            new RomInfo { Id = "K2", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Snowboard Kids 2" },
            new RomInfo { Id = "K4", Save = SaveType.Eep4K, Cic = CicType.Cic6103, Name = "Kirby 64: The Crystal Shards" },
            new RomInfo { Id = "KA", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Fighters Destiny" },
            new RomInfo { Id = "KG", Save = SaveType.Sram32, Cic = CicType.Cic6103, Name = "Major League Baseball featuring Ken Griffey Jr." },
            new RomInfo { Id = "KI", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Killer Instinct Gold" },
            new RomInfo { Id = "KJ", Save = SaveType.FlashRam, Cic = CicType.Cic6103, Name = "Ken Griffey Jr's Slugfest" },
            new RomInfo { Id = "KT", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Mario Kart 64" },
            new RomInfo { Id = "LB", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Mario Party" },
            new RomInfo { Id = "LR", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Lode Runner 3D" },
            new RomInfo { Id = "M6", Save = SaveType.FlashRam, Cic = CicType.Cic6102, Name = "Megaman 64" },
            new RomInfo { Id = "M8", Save = SaveType.Eep16K, Cic = CicType.Cic6102, Name = "Mario Tennis" },
            new RomInfo { Id = "MF", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "Mario Golf" },
            new RomInfo { Id = "MG", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Racing Simulation" },
            new RomInfo { Id = "MI", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Mission: Impossible" },
            new RomInfo { Id = "ML", Save = SaveType.Eep4K, Cic = CicType.Cic6105, Name = "Mickey's Speedway USA" },
            new RomInfo { Id = "MO", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Monopoly" },
            new RomInfo { Id = "MQ", Save = SaveType.FlashRam, Cic = CicType.Cic6103, Name = "Paper Mario" },
            new RomInfo { Id = "MR", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Multi-Racing Championship" },
            new RomInfo { Id = "MU", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Big Mountain 2000" },
            new RomInfo { Id = "MV", Save = SaveType.Eep16K, Cic = CicType.Cic6102, Name = "Mario Party 3" },
            new RomInfo { Id = "MW", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Mario Party 2" },
            new RomInfo { Id = "MX", Save = SaveType.Eep16K, Cic = CicType.Cic6103, Name = "Excitebike 64" },
            new RomInfo { Id = "N6", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Dr. Mario 64" },
            new RomInfo { Id = "NA", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Star Wars Episode 1: Battle for Naboo" },
            new RomInfo { Id = "NB", Save = SaveType.Eep16K, Cic = CicType.Cic6103, Name = "Kobe Bryant in NBA Courtside" },
            new RomInfo { Id = "NC", Save = SaveType.None, Cic = CicType.Cic6102, Name = "Nightmare Creatures" },
            new RomInfo { Id = "NX", Save = SaveType.Eep16K, Cic = CicType.Cic6103, Name = "Excitebike 64" },
            new RomInfo { Id = "OB", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "Ogre Battle 64: Person of Lordly Caliber" },
            new RomInfo { Id = "OH", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Transformers Beast Wars" },
            new RomInfo { Id = "P2", Save = SaveType.FlashRam, Cic = CicType.Cic6103, Name = "Pokémon Stadium 2" },
            new RomInfo { Id = "P3", Save = SaveType.FlashRam, Cic = CicType.Cic6103, Name = "Pokémon Stadium 2" },
            new RomInfo { Id = "PD", Save = SaveType.Eep16K, Cic = CicType.Cic6105, Name = "Perfect Dark" },
            new RomInfo { Id = "PF", Save = SaveType.FlashRam, Cic = CicType.Cic6103, Name = "Pokémon Snap" },
            new RomInfo { Id = "PG", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Hey you, Pikachu!" },
            new RomInfo { Id = "PH", Save = SaveType.FlashRam, Cic = CicType.Cic6103, Name = "Pokémon Snap" },
            new RomInfo { Id = "PN", Save = SaveType.FlashRam, Cic = CicType.Cic6102, Name = "Pokémon Puzzle League" },
            new RomInfo { Id = "PO", Save = SaveType.FlashRam, Cic = CicType.Cic6103, Name = "Pokémon Stadium" },
            new RomInfo { Id = "PS", Save = SaveType.FlashRam, Cic = CicType.Cic6103, Name = "Pokémon Stadium" },
            new RomInfo { Id = "PW", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Pilotwings 64" },
            new RomInfo { Id = "RC", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Top Gear Overdrive" },
            new RomInfo { Id = "RE", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "Resident Evil 2" },
            new RomInfo { Id = "RI", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "New Tetris, The" },
            new RomInfo { Id = "RS", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Star Wars: Rogue Squadron" },
            new RomInfo { Id = "RZ", Save = SaveType.Eep16K, Cic = CicType.Cic6102, Name = "Ridge Racer 64" },
            new RomInfo { Id = "S6", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Star Soldier: Vanishing Earth" },
            new RomInfo { Id = "SA", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "AeroFighters Assault" },
            new RomInfo { Id = "SA", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Sonic Wings Assault" },
            new RomInfo { Id = "SC", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Starshot Space Circus" },
            new RomInfo { Id = "SI", Save = SaveType.FlashRam, Cic = CicType.Cic6102, Name = "Fushigi no Dungeon - Furai no Shiren 2" },
            new RomInfo { Id = "SM", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Super Mario 64" },
            new RomInfo { Id = "SQ", Save = SaveType.FlashRam, Cic = CicType.Cic6102, Name = "Starcraft 64" },
            new RomInfo { Id = "SU", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Rocket: Robot on Wheels" },
            new RomInfo { Id = "SV", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Space Station Silicon Valley" },
            new RomInfo { Id = "SW", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Star Wars: Shadows of the Empire" },
            new RomInfo { Id = "T9", Save = SaveType.FlashRam, Cic = CicType.Cic6102, Name = "Tigger's Honey Hunt" },
            new RomInfo { Id = "TB", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Transformers Beast Wars Metals" },
            new RomInfo { Id = "TC", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "64 Trump Collection" },
            new RomInfo { Id = "TE", Save = SaveType.Sram32, Cic = CicType.Cic6103, Name = "1080º Snowboarding" },
            new RomInfo { Id = "TJ", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Tom & Jerry in Fists of Furry" },
            new RomInfo { Id = "TM", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Mischief Makers" },
            new RomInfo { Id = "TN", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "All-Star Tennis '99" },
            new RomInfo { Id = "TP", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Tetrisphere" },
            new RomInfo { Id = "VB", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "Bass Rush" },
            new RomInfo { Id = "VL", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "V-Rally Edition '99" },
            new RomInfo { Id = "VP", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "Virtual Pro Wrestling" },
            new RomInfo { Id = "VY", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "V-Rally Edition '99" },
            new RomInfo { Id = "W2", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "WCW/NWO Revenge" },
            new RomInfo { Id = "W4", Save = SaveType.FlashRam, Cic = CicType.Cic6102, Name = "WWF: No Mercy" },
            new RomInfo { Id = "WC", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Wild Choppers" },
            new RomInfo { Id = "WI", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "ECW Hardcore Revolution" },
            new RomInfo { Id = "WL", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Waialae Country Club: True Golf Classics" },
            new RomInfo { Id = "WR", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Wave Race 64" },
            new RomInfo { Id = "WU", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Worms Armageddon" },
            new RomInfo { Id = "WX", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "WWF: Wrestlemania 2000" },
            new RomInfo { Id = "XO", Save = SaveType.Eep4K, Cic = CicType.Cic6102, Name = "Cruis'n Exotica" },
            new RomInfo { Id = "YS", Save = SaveType.Eep16K, Cic = CicType.Cic6106, Name = "Yoshi's Story" },
            new RomInfo { Id = "YW", Save = SaveType.Sram32, Cic = CicType.Cic6102, Name = "Harvest Moon 64" },
            new RomInfo { Id = "ZL", Save = SaveType.Sram32, Cic = CicType.Cic6105, Name = "Legend of Zelda: Ocarina of Time, The" },
            new RomInfo { Id = "ZS", Save = SaveType.FlashRam, Cic = CicType.Cic6105, Name = "Legend of Zelda: Majora's Mask, The" },
        });

        static CartDb()
        {
            _KnownRoms.Sort((x, y) => { return String.Compare(x.Id, y.Id); });
        }

        static public RomInfo GetRomById(string id)
        {
            RomInfo res = null;

            var info =_KnownRoms.Find((x) => {
                return String.Equals(x.Id, id);
            });

            if (info != null)
            {
                // return a copy
                res = new RomInfo(info);
            }

            return res;
        }
    }

    public enum SaveType
    {
        Unknown,
        None,
        Eep4K,
        Eep16K,
        Sram32,
        Sram32x3,
        FlashRam
    };

    public enum CicType
    {
        Unknown,
        Cic6101,
        Cic6102,
        Cic6103,
        Cic6105,
        Cic6106,
    };

    public class RomInfo
    {
        public string Id { get; set; }
        public SaveType Save { get; set; }
        public CicType Cic { get; set; }
        public string Name { get; set; }

        public RomInfo()
        {
            Id = "";
            Name = "";
            Save = SaveType.Unknown;
            Cic = CicType.Unknown;
        }

        public RomInfo(RomInfo other)
        {
            Id = other.Id;
            Name = other.Name;
            Save = other.Save;
            Cic = other.Cic;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("{ ");
            sb.Append("\"" + Id + "\", ");
            sb.Append(Save + ", ");
            sb.Append(Cic + ", ");
            sb.Append("\"" + Name + "\"");
            sb.Append(" }");

            return sb.ToString();
        }
    }
}
