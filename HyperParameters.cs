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
        INSPIRED_RATIO,
        SHIPS_PER_DROPOFF,
        SAFETY_RATIO
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
            { Parameters.INSPIRED_RATIO, new Bounds(0, 4, 1.8) },
            { Parameters.SHIPS_PER_DROPOFF, new Bounds(5, 30, 15) },
            { Parameters.SAFETY_RATIO, new Bounds(.4, 2.0, .6) }
        };

        public static readonly Dictionary<Parameters, double> VarianceDictionary = new Dictionary<Parameters, double> {
            { Parameters.CARGO_TO_MOVE, 0.01 },
            { Parameters.TARGET_VALUE_TO_CREATE_SHIP, .02 },
            { Parameters.DROPOFF_DISTANCE, .07 },
            { Parameters.STAY_MULTIPLIER, .02 },
            { Parameters.HALITE_TO_SWITCH_COLLECT, .02 },
            { Parameters.INSPIRED_RATIO, .02 },
            { Parameters.SHIPS_PER_DROPOFF, .05 },
            { Parameters.SAFETY_RATIO, .01 }
        };

        private Dictionary<Parameters, double> ParametersDictionary = new Dictionary<Parameters, double>();

        public double LowerBound(Parameters p) => BoundDictionary[p].Lower;
        public double UpperBound(Parameters p) => BoundDictionary[p].Upper;
        public double GetValue(Parameters p) => ParametersDictionary[p];
        public double this[Parameters param]
        {
            get { 
                if(param == Parameters.CARGO_TO_MOVE)
                    return ParametersDictionary[param] * Constants.MAX_HALITE;
                if(param == Parameters.DROPOFF_DISTANCE) {
                    var value = ParametersDictionary[param];
                    int numCellsCovered = (int) (((value * value / 2) + (value / 2)) * 4.0) + 1;
                    int haliteCovered = numCellsCovered * 170;
                    int actualLayers=0;
                    while(true) {
                        actualLayers++;
                        var numCells = (((actualLayers * actualLayers / 2) + (actualLayers / 2)) * 4.0) + 1;
                        if(numCells * (GameInfo.Map.AverageHalitePerCell + 20) >= haliteCovered) {
                            return actualLayers;
                        }
                    }
                } else  {
                    return ParametersDictionary[param];
                }
            }
            set { 
                value = Math.Max(BoundDictionary[param].Lower, value);
                value = Math.Min(BoundDictionary[param].Upper, value);
                ParametersDictionary[param] = value;
            }
        }

        private HyperParameters() {}

        public HyperParameters(string file, bool initializeDictionary = true) {
            bool shouldPrint = !HasPrinted;
            HasPrinted = true;
            var lines = System.IO.File.ReadAllText(file).Split("\n").ToList();
            foreach(var line in lines) {
                try {
                    if(line.Trim().Length == 0)
                        continue;
                    var values = line.Split(",").ToList();
                    Parameters param = (Parameters)Enum.Parse(typeof(Parameters), values[0]);
                    ParametersDictionary.Add(param, double.Parse(values[1]));
                    if(shouldPrint)
                        Log.LogMessage(param.ToString("g") + ": "+ double.Parse(values[1]));
                } catch(ArgumentException) {}
            }

            foreach(var param in AllParameters) {
                if(!ParametersDictionary.ContainsKey(param)) {
                    ParametersDictionary.Add(param, BoundDictionary[param].Seed);
                    if(shouldPrint)
                        Log.LogMessage(param.ToString("g") + ": "+ BoundDictionary[param].Seed);
                }
            }
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
