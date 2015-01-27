using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ChordSuggest {
	using ChordProgression = List<Chord>;
	using ChordProgressions = List<List<Chord>>;

	class ChordProgressionSuggest {
		private ChordProgressions progressions = new ChordProgressions();
		private double[,] defaultWeight;
		private double[,] suggestWeight;
		public bool ignoreSlashChord { get; set; }
		public void initialize() {
			ignoreSlashChord = true;
		}
		public void read(List<string> fileList) {
			progressions.Clear();
			foreach (string fileName in fileList) {
				StreamReader sr = new StreamReader(ChordProgressionDatabase.folder+fileName);
				char[] separater = new char[] { ' ', ',', '\t', '|' };
				int lineCount = 1;
				while (!sr.EndOfStream) {
					string[] progressionString = sr.ReadLine().Split(separater).Where(p => p.Length > 0).ToArray();
					ChordProgression progression = new List<Chord>();
					foreach (string str in progressionString) {
						var chord = Chord.createChordFromChordName(str);
						if (chord != null) {
							if (!ignoreSlashChord) progression.Add(chord);
							else {
								if (MainWindow.hasHarmony(chord.harmony)) {
									progression.Add(chord);
								} else {
									chord.harmony.baseNote = 0;
									chord.harmony.uniqueIdRecalculation();
									progression.Add(chord);
								}
							}
						} else {
							MainWindow.Write("***Warning*** " + str + " (@" + fileName + ":line" + lineCount.ToString() + ")is skipped\n");
						}
					}
					lineCount++;
					if (progression.Count > 0) progressions.Add(progression);
				}
			}

			defaultWeight = new double[ChordBasic.toneCount,MainWindow.harmonyCount];
			suggestWeight = new double[ChordBasic.toneCount, MainWindow.harmonyCount];
			int sum = 0;
			foreach (ChordProgression cp in progressions) { 
				sum += cp.Count;
				foreach (Chord chord in cp) {
					if (MainWindow.hasHarmony(chord.harmony)) {
						defaultWeight[chord.root.id, MainWindow.getPadHarmonyId(chord.harmony)] += 1.0;
					}
				}
			}
			weightBalancing(ref defaultWeight);
			MainWindow.Write("\t" + progressions.Count.ToString() + " chord progressions with "+sum.ToString()+" chords loaded\n");
		}
		public void suggestNextChord(Chord kc) {
			for (int i = 0; i < ChordBasic.toneCount; i++) {
				for (int j = 0; j < MainWindow.harmonyCount; j++) {
					suggestWeight[i, j] = 0;
				}
			}
			foreach (ChordProgression cp in progressions) {
				for (int i = 0; i + 1 < cp.Count; i++) {
					if (cp[i].Equals(kc) && MainWindow.hasHarmony(cp[i+1].harmony)) {
						suggestWeight[cp[i + 1].root.id , MainWindow.getPadHarmonyId(cp[i + 1].harmony)] += 1;
					}
				}
			}
			weightBalancing(ref suggestWeight);
		}
		public void suggestNextChord(List<Chord> kcs) {
			for (int i = 0; i < ChordBasic.toneCount; i++) {
				for (int j = 0; j < MainWindow.harmonyCount; j++) {
					suggestWeight[i, j] = 0;
				}
			}
			foreach (ChordProgression cp in progressions) {
				for (int i = cp.Count - 1; i - 1 >= 0; i--) {
					int matchCount = 0;
					while (true) {
						if (i - 1 - matchCount < 0) {
							break;
						}
						if (!cp[i - 1 - matchCount].Equals(kcs[matchCount])) {
							break;
						}
						matchCount++;
						continue;
					}
					if (matchCount > 0) {
						if (MainWindow.hasHarmony(cp[i].harmony)) {
							suggestWeight[cp[i].root.id, MainWindow.getPadHarmonyId(cp[i].harmony)] += matchCount;
						}
					}
				}
			}
			weightBalancing(ref suggestWeight);
		}
		public double getDefaultChordWeight(int rootId,int harmonyId) {
			return defaultWeight[rootId, harmonyId];
		}
		public double getSuggestChordWeight(int rootId,int harmonyId) {
			return suggestWeight[rootId, harmonyId];
		}
		private void weightBalancing(ref double[,] weight) {
			double maxWeight = double.MinValue;
			double minWeight = double.MaxValue;
			for (int i = 0; i < ChordBasic.toneCount; i++) {
				for (int j = 0; j < MainWindow.harmonyCount; j++) {
					maxWeight = Math.Max(maxWeight, weight[i, j]);
					minWeight = Math.Min(minWeight, weight[i, j]);
				}
			}
			if (maxWeight != minWeight) {
				for (int i = 0; i < ChordBasic.toneCount; i++) {
					for (int j = 0; j < MainWindow.harmonyCount; j++) {
						weight[i, j] = Math.Log(weight[i, j] - minWeight + 1) / Math.Log(maxWeight - minWeight + 1);
					}
				}
			} else {
				for (int i = 0; i < ChordBasic.toneCount; i++) {
					for (int j = 0; j < MainWindow.harmonyCount; j++) {
						weight[i, j] = 0;
					}
				}
			}
		}
	}
}
