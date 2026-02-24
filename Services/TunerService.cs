using System.Collections.Generic;

namespace GuitarToolkit.Services
{
    public class TunerService
    {
        public float ReferenceA { get; set; } = 440f; // эталон

        // Строи: название → ноты струн от 6й до 1й
        public static readonly Dictionary<string, string[]> Tunings = new()
        {
            { "Стандарт (EADGBe)",  new[] { "E2", "A2", "D3", "G3", "B3", "E4" } },
            { "Drop D (DADGBe)",    new[] { "D2", "A2", "D3", "G3", "B3", "E4" } },
            { "Open G (DGDGBd)",    new[] { "D2", "G2", "D3", "G3", "B3", "D4" } },
            { "Open D (DADf#Ad)",   new[] { "D2", "A2", "D3", "F#3","A3", "D4" } },
            { "DADGAD",             new[] { "D2", "A2", "D3", "G3", "A3", "D4" } },
            { "Half Step Down",     new[] { "Eb2","Ab2","Db3","Gb3","Bb3","Eb4" } },
            { "Full Step Down",     new[] { "D2", "G2", "C3", "F3", "A3", "D4" } },
        };

        // Частота ноты с учётом текущего эталона
        public float NoteToFrequency(string noteName)
        {
            var noteMap = new Dictionary<string, int>
            {
                {"C",0},{"C#",1},{"Db",1},{"D",2},{"D#",3},{"Eb",3},
                {"E",4},{"F",5},{"F#",6},{"Gb",6},{"G",7},{"G#",8},
                {"Ab",8},{"A",9},{"A#",10},{"Bb",10},{"B",11}
            };

            // Парсим ноту и октаву, например "E2" → E, 2
            string note = noteName.Length > 2 ? noteName[..^1] : noteName[..^1];
            int octave = int.Parse(noteName[^1..]);

            if (!noteMap.TryGetValue(note, out int semitone)) return 0;

            // A4 = ReferenceA, MIDI номер A4 = 69
            int midi = (octave + 1) * 12 + semitone;
            return ReferenceA * MathF.Pow(2, (midi - 69) / 12f);
        }
    }
}