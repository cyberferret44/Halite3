using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Halite3;
using Halite3.hlt;

namespace GeneticTuner
{
    /// Creates a CSV from our specimen for processing in sheets
    public class SpecimenExaminer {
        private static readonly string SPECIMEN_CSV = "Halite3/GeneticTuner/SpecimenAnalysis.csv";
        public static void GenerateCSVFromSpecimenFolder() {
            List<GeneticSpecimen> specimens = new List<GeneticSpecimen>();
            foreach(var f in Directory.EnumerateFiles("Halite3/GeneticTuner/Specimen")) {
                specimens.Add(new GeneticSpecimen(f, "Halite3/"));
            }

            string output = "";
            var hParams = HyperParameters.AllParameters;
            for(int i=0; i<hParams.Count; i++) {
                if(i>0)
                    output += ",";
                output += hParams[i].ToString();
            }
            output += "\n";
            foreach(var s in specimens) {
                for(int i=0; i<hParams.Count; i++) {
                    if(i>0)
                        output += ",";
                    output += s.GetHyperParameters()[hParams[i]];
                }
                output += "\n";
            }
            try {
                using(StreamWriter sw = File.AppendText(SPECIMEN_CSV)) {
                    sw.Write(output); 
                }
            } catch (System.IO.IOException) {}
        }
    }
}