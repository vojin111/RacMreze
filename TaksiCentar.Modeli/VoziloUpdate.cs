using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaksiCentar.Modeli
{
    [Serializable]
    public class VoziloUpdate
    {
        public Guid VoziloId { get; set; }
        public int Port { get; set; }

        public double X { get; set; }
        public double Y { get; set; }

        public string Status { get; set; } // Slobodno, Odlazak, Voznja, Zavrseno

        public decimal Zarada { get; set; }        // ukupna zarada (ili delta, ali ovde ukupna)
        public double PredjenaKm { get; set; }     // ukupno km

        public Guid? ZadatakId { get; set; }       // koji zadatak (ako ima)
        public bool ZavrsetakVoznje { get; set; }  // true samo na kraju
    }
}
