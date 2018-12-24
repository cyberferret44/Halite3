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
        PERCENT_OF_AVERAGE_TO_IGNORE,
        DROPOFF_DISTANCE, 
        SHOULD_CRASH_SHIP,
        TOUCH_RATIO
    }

    public class HyperParameters {
        // Non tuned parameters.  Keeping here so I can easily find & tune them later
        public static List<Parameters> AllParameters = Enum.GetValues(typeof(Parameters)).Cast<Parameters>().ToList();

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
            { Parameters.CARGO_TO_MOVE, new Bounds(0, 1.0, .85) },
            { Parameters.TARGET_VALUE_TO_CREATE_SHIP, new Bounds(0, 10000.0, 550.0) },
            { Parameters.PERCENT_OF_AVERAGE_TO_IGNORE, new Bounds(0, 1.0, .25)},
            { Parameters.DROPOFF_DISTANCE, new Bounds(0, 32, 14) },
            { Parameters.SHOULD_CRASH_SHIP, new Bounds(0, 2000, GameInfo.PlayerCount == 2 ? 400 : 900)},
            { Parameters.TOUCH_RATIO, new Bounds(0, 1.0, .8)}
        };

        public static readonly Dictionary<Parameters, double> VarianceDictionary = new Dictionary<Parameters, double> {
            { Parameters.CARGO_TO_MOVE, 0.001 },
            { Parameters.TARGET_VALUE_TO_CREATE_SHIP, .1 },
            { Parameters.PERCENT_OF_AVERAGE_TO_IGNORE, .03 },
            { Parameters.DROPOFF_DISTANCE, .05 },
            { Parameters.SHOULD_CRASH_SHIP, .05 },
            { Parameters.TOUCH_RATIO, .05}
        };

        private Dictionary<Parameters, double> ParametersDictionary = new Dictionary<Parameters, double>();

        public double LowerBound(Parameters p) => BoundDictionary[p].Lower;
        public double UpperBound(Parameters p) => BoundDictionary[p].Upper;
        public double GetValue(Parameters p) => ParametersDictionary[p];
        public double this[Parameters param]
        {
            get { 
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

        public HyperParameters(string file, bool initializeDictionary = true) {
            var lines = System.IO.File.ReadAllText(file).Split("\n").ToList();
            foreach(var line in lines) {
                try {
                    if(line.Trim().Length == 0)
                        continue;
                    var values = line.Split(",").ToList();
                    Parameters param = (Parameters)Enum.Parse(typeof(Parameters), values[0]);
                    ParametersDictionary.Add(param, double.Parse(values[1]));
                    if(initializeDictionary)
                        Log.LogMessage(param.ToString("g") + ": "+ double.Parse(values[1]));
                } catch(ArgumentException) {}
            }

            foreach(var param in AllParameters) {
                if(!ParametersDictionary.ContainsKey(param)) {
                    ParametersDictionary.Add(param, BoundDictionary[param].Seed);
                    if(initializeDictionary)
                        Log.LogMessage(param.ToString("g") + ": "+ BoundDictionary[param].Seed);
                }
            }

            if(initializeDictionary)
                BuildVarianceDictionary();
        }

        public void BuildVarianceDictionary() {
            VarianceDictionary.Clear();
            var values = new Dictionary<Parameters, List<double>>();
            foreach(var param in AllParameters) {
                values.Add(param, new List<double>());
            }

            var files = Directory.EnumerateFiles(GameInfo.HyperParameterFolder).ToArray();
            foreach(var file in files) {
                var hparams = new HyperParameters(file, false);
                foreach(var param in AllParameters) {
                    values[param].Add(hparams[param]);
                }
            }

            Log.LogMessage("Variance Dictionary......");
            foreach(var kvp in values) {
                List<double> vals = kvp.Value.OrderBy(x => x).ToList();
                double lowerAvg = vals.GetRange(0, vals.Count/2).Average();
                double upperAvg = vals.GetRange(vals.Count/2, vals.Count/2).Average();
                VarianceDictionary[kvp.Key] = (upperAvg - lowerAvg) * 2 + (GameInfo.IsLocal ? upperAvg * .05 : 0.005); // enforce a minimum variance
                Log.LogMessage(kvp.Key.ToString("g") + ": "+ VarianceDictionary[kvp.Key]);
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
