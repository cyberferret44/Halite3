using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Halite3;

namespace GeneticTuner
{
    /// This setup allows the game to detect server vs localhost and use
    /// a real specimen when locally tuning, and a fake one when running on
    /// the server
    public interface Specimen {
        void SpawnChildren();
        void Kill();
        HyperParameters GetHyperParameters();
    }

    public class FakeSpecimen : Specimen {
        public void SpawnChildren() {}
        public void Kill() {}
        public HyperParameters GetHyperParameters() { return null; }
    }

    public class GeneticSpecimen : Specimen {
        public static readonly string SPECIMEN_FOLDER = "Halite3/GeneticTuner/Specimen";
        private static Random random = new Random();
        private static int NUM_CHILDREN = 1; // population control level
        private HyperParameters hyperParameters;
        public HyperParameters GetHyperParameters() => hyperParameters;
        private string Name() => FilePath.Split(".")[0].Split("\\").Last();
        private string FilePath;

        private List<GeneticSpecimen> children = new List<GeneticSpecimen>();

        public GeneticSpecimen(string file) {
            FilePath = file;
            hyperParameters = new HyperParameters(FilePath);
            CreateChildren();
        }
        private GeneticSpecimen() {}

        /// Create children and spawn children need to be separate
        /// in case any competing speciemen were generated from the same
        /// seed file; we don't want the losing specimen to call Kill()
        /// while the winner's children are trying to generate their
        /// seed hyperparamters from the same file. 
        private void CreateChildren() {
            for(int i=0; i<NUM_CHILDREN; i++) {
                string name = "Specimen-" + Guid.NewGuid();
                var child = new GeneticSpecimen() {
                    hyperParameters = new HyperParameters(FilePath),
                    FilePath = SPECIMEN_FOLDER + "\\" + name + ".txt"
                };

                // tune our hyperparameters
                foreach(var param in Enum.GetValues(typeof(Parameters)).Cast<Parameters>()) {
                    double multiplier = 1.0 + ((random.NextDouble() - .5) / 10.0);
                    double curValue = hyperParameters.GetValue(param);
                    child.hyperParameters[param] = curValue * multiplier;
                }
                children.Add(child);
            }
        }

        public static Specimen RandomSpecimen() {
            var files = Directory.EnumerateFiles(SPECIMEN_FOLDER).ToArray();
            int randomOne = random.Next(0, files.Count());
            return new GeneticSpecimen(files[randomOne]);
        }

        public void SpawnChildren() {
            foreach(var child in children) {
                child.hyperParameters.WriteToFile(child.FilePath);
            }
        }

        public void Kill() {
            File.Delete(this.FilePath);
        }
    }
}