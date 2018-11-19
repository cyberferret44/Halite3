using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Halite3;

namespace GeneticTuner
{
    public class Specimen {
        public static readonly string SPECIMEN_FOLDER = "C:\\Users\\Chase\\Desktop\\Halite3_CSharp_None\\Halite3\\GeneticTuner\\Specimen";
        public static Random random = new Random(); // TODO make this gaussian
        public HyperParameters hyperParameters;
        public string Name() => FilePath.Split(".")[0].Split("\\").Last();
        public string FilePath;

        private Specimen(string file) {
            FilePath = file;
            hyperParameters = new HyperParameters(FilePath);
        }
        private Specimen() {}

        public static Specimen RandomSpecimen() {
            var files = Directory.EnumerateFiles(SPECIMEN_FOLDER).ToArray();
            int randomOne = random.Next(0, files.Count());
            return new Specimen(files[randomOne]);
        }

        public Specimen SpawnChild() {
            string name = "Specimen-" + Guid.NewGuid();
            return SpawnChild(name);
        }

        public Specimen SpawnChild(string name) {
            Specimen child = new Specimen();
            child.hyperParameters = new HyperParameters(FilePath);
            child.FilePath = SPECIMEN_FOLDER + "\\" + name + ".txt";

            // tune our hyperparameters
            foreach(var param in Enum.GetValues(typeof(Parameters)).Cast<Parameters>()) {
                double multiplier = 1.0 + ((random.NextDouble() - .5) / 10.0);
                double curValue = hyperParameters.GetValue(param);
                child.hyperParameters[param] = curValue * multiplier;
            }

            //write to file
            child.hyperParameters.WriteToFile(child.FilePath);

            return child;
        }

        public void Kill() {

        }
    }
}