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
        CELL_VALUE_DEGRADATION,
        PERCENT_OF_AVERAGE_TO_IGNORE,
        DROPOFF_DISTANCE, 
        SHOULD_CRASH_SHIP,
        TOUCH_RATIO
    }

    public class HyperParameters {
        // Non tuned parameters.  Keeping here so I can easily find & tune them later
        public static List<Parameters> AllParameters = Enum.GetValues(typeof(Parameters)).Cast<Parameters>().ToList();

        private List<string> unusedLines;

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
            { Parameters.CELL_VALUE_DEGRADATION,   new Bounds(0, 1.0, .8)},
            { Parameters.PERCENT_OF_AVERAGE_TO_IGNORE, new Bounds(0, 1.0, .25)},
            { Parameters.DROPOFF_DISTANCE, new Bounds(0, 32, 14) },
            { Parameters.SHOULD_CRASH_SHIP, new Bounds(0, 2000, GameInfo.PlayerCount == 2 ? 400 : 900)},
            { Parameters.TOUCH_RATIO, new Bounds(0, 1.0, .8)}
        };

        public static readonly Dictionary<Parameters, double> VarianceDictionary = new Dictionary<Parameters, double> {
            { Parameters.CARGO_TO_MOVE, 0.001 },
            { Parameters.TARGET_VALUE_TO_CREATE_SHIP, .01 },
            { Parameters.CELL_VALUE_DEGRADATION, .01 },
            { Parameters.PERCENT_OF_AVERAGE_TO_IGNORE, .01 },
            { Parameters.DROPOFF_DISTANCE, .03 },
            { Parameters.SHOULD_CRASH_SHIP, .03 },
            { Parameters.TOUCH_RATIO, .03}
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

        public HyperParameters(string file) {
            unusedLines = new List<string>();
            var lines = System.IO.File.ReadAllText(file).Split("\n").ToList();
            foreach(var line in lines) {
                if(line.Trim().Length == 0)
                    continue;
                var values = line.Split(",").ToList();
                try {
                    Parameters param = (Parameters)Enum.Parse(typeof(Parameters), values[0]);
                    ParametersDictionary.Add(param, double.Parse(values[1]));
                    Log.LogMessage(param.ToString("g") + ": "+ double.Parse(values[1]));
                } catch (Exception) {
                    unusedLines.Add(line);
                }
            }

            foreach(var param in AllParameters) {
                if(!ParametersDictionary.ContainsKey(param)) {
                    ParametersDictionary.Add(param, BoundDictionary[param].Seed);
                    Log.LogMessage(param.ToString("g") + ": "+ BoundDictionary[param].Seed);
                }
            }
        }

        public void WriteToFile(string file) {
            string content = "";
            /* foreach(var line in unusedLines) {
                content += line + "\n";
            }*/
            foreach(var kvp in ParametersDictionary) {
                content += ($"{kvp.Key.ToString("g")},{kvp.Value}\n");
            }
            using(StreamWriter sw = File.AppendText(file)) {
                sw.Write(content);              
            }
        }
    }
}
