using Halite3.hlt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

/// Adding a parameter?
/// 1. Add it to parameters
/// 2. Add a new entry to bounds dictionary
namespace Halite3
{
    // Genetically tuned parameters
    public enum Parameters {
        CARGO_TO_MOVE,
        TARGET_VALUE_TO_CREATE_SHIP,
        DROPOFF_DISTANCE,
        STAY_MULTIPLIER,
        HALITE_TO_SWITCH_COLLECT,
        SAFETY_THRESHOLD
    }

    public class HyperParameters {
        // Non tuned parameters.  Keeping here so I can easily find & tune them later
        public static List<Parameters> AllParameters = Enum.GetValues(typeof(Parameters)).Cast<Parameters>().ToList();
        private static bool HasPrinted = false;

        // returns a default set for testing
        public static HyperParameters GetDefaults() {
            var hp = new HyperParameters();
            foreach(var kvp in BoundDictionary) {
                hp.ParametersDictionary[kvp.Key] = kvp.Value.Seed;
            }
            return hp;
        }

        // Dynamic hyper parameters
        private class Bounds {
            public double Lower, Upper, Seed;
            public Bounds(double lower, double upper, double seed) {
                this.Lower = lower;
                this.Upper = upper;
                this.Seed = seed;
            }
        }

        private static readonly Dictionary<Parameters, Bounds> BoundDictionary = new Dictionary<Parameters, Bounds> {
            { Parameters.CARGO_TO_MOVE, new Bounds(0, 1.0, .9) },
            { Parameters.TARGET_VALUE_TO_CREATE_SHIP, new Bounds(0, 10000.0, 550.0) },
            { Parameters.DROPOFF_DISTANCE, new Bounds(0, 32, 14) },
            { Parameters.STAY_MULTIPLIER, new Bounds(0, 10, 3.0)},
            { Parameters.HALITE_TO_SWITCH_COLLECT, new Bounds(0, 1000, 70.0)},
            { Parameters.SAFETY_THRESHOLD, new Bounds(0, 1.0, .6)}
        };

        public static readonly Dictionary<Parameters, double> VarianceDictionary = new Dictionary<Parameters, double> {
            { Parameters.CARGO_TO_MOVE, 0.02 },
            { Parameters.TARGET_VALUE_TO_CREATE_SHIP, .07 },
            { Parameters.DROPOFF_DISTANCE, .05 },
            { Parameters.STAY_MULTIPLIER, .04 },
            { Parameters.HALITE_TO_SWITCH_COLLECT, .05},
            { Parameters.SAFETY_THRESHOLD, .03}
        };

        private Dictionary<Parameters, double> ParametersDictionary = new Dictionary<Parameters, double>();

        public double LowerBound(Parameters p) => BoundDictionary[p].Lower;
        public double UpperBound(Parameters p) => BoundDictionary[p].Upper;
        public double GetValue(Parameters p) => ParametersDictionary[p];
        public double this[Parameters param]
        {
            get {
                return ParametersDictionary[param];
            }
            set { 
                value = Math.Max(BoundDictionary[param].Lower, value);
                value = Math.Min(BoundDictionary[param].Upper, value);
                ParametersDictionary[param] = value;
            }
        }

        private HyperParameters() {}

        public HyperParameters(string file, bool initializeDictionary = true) {
            var lines = System.IO.File.ReadAllText(file).Split("\n").ToList();
            foreach(var line in lines) {
                try {
                    // parse the parameters
                    if(line.Trim().Length == 0)
                        continue;
                    var values = line.Split(",").ToList();
                    Parameters param = (Parameters)Enum.Parse(typeof(Parameters), values[0]);
                    var value = double.Parse(values[1]);

                    // Special edge cases...
                    if(param == Parameters.CARGO_TO_MOVE)
                        value = value * Constants.MAX_HALITE;
                    if(param == Parameters.DROPOFF_DISTANCE) {
                        int numCellsCovered = (int) (((value * value / 2) + (value / 2)) * 4.0) + 1;
                        int haliteCovered = numCellsCovered * 170;
                        int actualLayers=0;
                        while(true) {
                            actualLayers++;
                            var numCells = (((actualLayers * actualLayers / 2) + (actualLayers / 2)) * 4.0) + 1;
                            if(numCells * (GameInfo.Map.AverageHalitePerCell + 20) >= haliteCovered) {
                                value = actualLayers;
                                break;
                            }
                        }
                    }

                    ParametersDictionary.Add(param, value);
                    if(!HasPrinted)
                        Log.LogMessage(param.ToString("g") + ": "+ double.Parse(values[1]));
                } catch(ArgumentException) {}
            }

            foreach(var param in AllParameters) {
                if(!ParametersDictionary.ContainsKey(param)) {
                    ParametersDictionary.Add(param, BoundDictionary[param].Seed);
                    if(!HasPrinted)
                        Log.LogMessage(param.ToString("g") + ": "+ BoundDictionary[param].Seed);
                }
            }
            HasPrinted = true;
        }

        public void WriteToFile(string file) {
            string content = "";
            foreach(var kvp in ParametersDictionary) {
                content += ($"{kvp.Key.ToString("g")},{kvp.Value}\n");
            }
            using(StreamWriter sw = File.AppendText(file)) {
                sw.Write(content);              
            }
        }
    }
}
