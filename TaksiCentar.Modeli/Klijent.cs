using System;

namespace TaksiCentar.Modeli
{
    [Serializable]
    public class Klijent
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string PocetnaTacka { get; set; }
        public string KrajnjaTacka { get; set; }

        public string StatusZahteva { get; set; } = "Ceka"; // Ceka, Prihvaceno, Zavrseno

        public override string ToString()
        {
            return $"Klijent {Id}: {PocetnaTacka} -> {KrajnjaTacka} Status={StatusZahteva}";
        }
    }
}