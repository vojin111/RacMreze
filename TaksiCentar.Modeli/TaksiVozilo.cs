using System;

namespace TaksiCentar.Modeli
{
    [Serializable]
    public class TaksiVozilo
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public double X { get; set; }
        public double Y { get; set; }

        public string Status { get; set; } = "Slobodno"; // Slobodno, Zauzeto, Vožnja...
        public double PredjenaKm { get; set; }
        public decimal Zarada { get; set; }

        public override string ToString()
        {
            return $"Vozilo {Id} Pos=({X},{Y}) Status={Status} Km={PredjenaKm} Zarada={Zarada}";
        }
    }
}