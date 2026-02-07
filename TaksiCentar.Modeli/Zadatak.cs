using System;

namespace TaksiCentar.Modeli
{
    [Serializable]
    public class Zadatak
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid KlijentId { get; set; }
        public Guid VoziloId { get; set; }

        public string StatusZadatka { get; set; } = "Aktivan"; // Aktivan, Zavrsen
        public double PredjenaRazdaljina { get; set; }        // km

        public DateTime VremeKreiranja { get; set; } = DateTime.Now;
        public TimeSpan Trajanje { get; set; } = TimeSpan.Zero;

        public override string ToString()
        {
            return $"Zadatak {Id}: Klijent={KlijentId} Vozilo={VoziloId} " +
                   $"Status={StatusZadatka} Km={PredjenaRazdaljina}";
        }
    }
}