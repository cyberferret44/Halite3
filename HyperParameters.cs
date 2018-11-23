using Halite3.hlt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Halite3
{
    public enum Parameters {
        CARGO_TO_MOVE,
        TURNS_TO_SAVE
    }

    public class HyperParameters {
        private class Bounds {
            public double Lower, Upper;
            public Bounds(double lower, double upper) {
                this.Lower = lower;
                this.Upper = upper;
            }
        }

        private static readonly Dictionary<Parameters, Bounds> BoundDictionary = new Dictionary<Parameters, Bounds> {
            { Parameters.CARGO_TO_MOVE, new Bounds(0, 1000) },
            { Parameters.TURNS_TO_SAVE, new Bounds(0, 400) }  //todo change to percent of turns to save
        };

        private Dictionary<Parameters, double> ParametersDictionary = new Dictionary<Parameters, double>();

        public double LowerBound(Parameters p) => BoundDictionary[p].Lower;
        public double UpperBound(Parameters p) => BoundDictionary[p].Upper;
        public double GetValue(Parameters p) => ParametersDictionary[p];
        public double this[Parameters param]
        {
            get { return ParametersDictionary[param]; }
            set { 
                value = Math.Max(BoundDictionary[param].Lower, value);
                value = Math.Min(BoundDictionary[param].Upper, value);
                ParametersDictionary[param] = value;
            }
        }

        public HyperParameters(string file) {
            var lines = System.IO.File.ReadAllText(file).Split("\n");
            for(int i=0; i<Enum.GetNames(typeof(Parameters)).Length; i++) {
                string[] values = lines[i].Split(",");
                Parameters param = (Parameters)Enum.Parse(typeof(Parameters), values[0]);
                ParametersDictionary.Add(param, double.Parse(values[1]));
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
