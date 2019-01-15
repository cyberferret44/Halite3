using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Halite3;
using Halite3.hlt;

namespace GeneticTuner
{
    public interface Specimen {
        void SpawnChildren();
        void Kill();
        HyperParameters GetHyperParameters();
        string Name();
    }

    public class FakeSpecimen : Specimen {
        public void SpawnChildren() {}
        public void Kill() {}
        public HyperParameters GetHyperParameters() { return null; }
        public string Name() { return "fake"; }
    }

    public class GeneticSpecimen : Specimen {
        private static Random random = new Random();
        private static int NUM_CHILDREN = 1; // population control level
        private HyperParameters hyperParameters;
        public HyperParameters GetHyperParameters() => hyperParameters;
        public string Name() => FilePath.Split(".")[0].Split("/").Last().Substring(0, 15);
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
                    hyperParameters = new HyperParameters(FilePath), // give it same hyper parameters as parent
                    FilePath = GameInfo.HyperParameterFolder + name + ".txt"  // generates a new name for the child specimen
                };

                // tune our hyperparameters
                foreach(var param in HyperParameters.AllParameters) {
                    double val = ((random.NextDouble() * 2 - 1) * HyperParameters.VarianceDictionary[param]);
                    double curValue = hyperParameters.GetValue(param);
                    child.hyperParameters[param] = curValue * (1.0 + val);
                }
                children.Add(child);
            }
        }

        public static Specimen RandomSpecimen() {
            var files = Directory.EnumerateFiles(GameInfo.HyperParameterFolder).ToArray();
            int randomOne = random.Next(0, files.Count());
            return new GeneticSpecimen(files[randomOne]);
        }

        public void SpawnChildren() {
            if(Directory.EnumerateFiles(GameInfo.HyperParameterFolder).Count() < 12) {
                foreach(var child in children) {
                    Halite3.hlt.Log.LogMessage("specimen file path " + child.FilePath);
                    child.hyperParameters.WriteToFile(child.FilePath);
                }
            }
        }

        public void Kill() {
            // minimum number of specimen
            if(Directory.EnumerateFiles(GameInfo.HyperParameterFolder).Count() > 8) {
                File.Delete(this.FilePath);
            }
        }
    }
}