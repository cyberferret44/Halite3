using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Halite3;

namespace GeneticTuner
{
    /// Creates a CSV from our specimen for processing in sheets
    public class SpecimenExaminer {
        private static readonly string SPECIMEN_CSV = "Halite3\\GeneticTuner\\SpecimenAnalysis.csv";
        public static void GenerateCSVFromSpecimenFolder() {
            List<GeneticSpecimen> specimens = new List<GeneticSpecimen>();
            foreach(var f in Directory.EnumerateFiles(GeneticSpecimen.SPECIMEN_FOLDER)) {
                specimens.Add(new GeneticSpecimen(f));
            }

            string output = "";
            foreach(var s in specimens) {
                output += s.GetHyperParameters()[Parameters.CARGO_TO_MOVE] + ","
                + s.GetHyperParameters()[Parameters.TURNS_TO_SAVE] + "\n";
            }
            try {
                using(StreamWriter sw = File.AppendText(SPECIMEN_CSV)) {
                    sw.Write(output); 
                }
            } catch (System.IO.IOException) {}
        }
    }
}