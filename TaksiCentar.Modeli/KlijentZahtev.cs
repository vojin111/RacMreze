using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaksiCentar.Modeli
{
    [Serializable]
    public class KlijentZahtev
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string PocetnaTacka { get; set; }
        public string KrajnjaTacka { get; set; }

        public double X { get; set; } // koordinate klijenta
        public double Y { get; set; } // koordinate klijenta

        public double KrajX { get; set; } // koordinate krajnje tacke
        public double KrajY { get; set; }

        public string Status { get; set; } = "Ceka"; // Ceka, Prihvaceno, Zavrseno
        public double EtaMin { get; set; }
    }
}
