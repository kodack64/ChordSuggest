using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChordSuggest {
	class VoicingManage {
		private VoicingManage() { }
		private static VoicingManage myInstance = null;
		public static void createInstance() {
			if (myInstance == null) myInstance = new VoicingManage();
		}
		public static VoicingManage getInstance() { return myInstance; }
		public int keepNotePolicyIndex { get; set; }
		public int nearToPolicyIndex { get; set; }
		public int expandToPolicyIndex { get; set; }
		public int baseNotePolicyIndex { get; set; }
		public int baseNearToPolicyIndex { get; set; }
		public enum BaseNotePolicy {
			_None,
			_1Oct,
			_1Oct_5th,
			_2Oct,
			_2Oct_5th
		}
		public enum KeepNotePolicy {
			_Nothing,
			_KeepKeyRoot,
			_KeepChordRoot,
			_KeepRootTop
		}
		public enum NearToPolicy {
			_Nothing,
			_First,
			_Previous
		}
		public enum BaseNearToPolicy {
			_Nothing,
			_First,
			_Previous,
			_Chord
		}
		public enum ExpandToPolicy {
			_Lower,
			_Upper,
			_Both
		}
		public KeepNotePolicy keepNotePolicy { get { return (KeepNotePolicy)(Enum.GetValues(typeof(KeepNotePolicy)).GetValue(keepNotePolicyIndex)); } }
		public NearToPolicy nearToPolicy { get { return (NearToPolicy)(Enum.GetValues(typeof(NearToPolicy)).GetValue(nearToPolicyIndex)); } }
		public ExpandToPolicy expandToPolicy { get { return (ExpandToPolicy)(Enum.GetValues(typeof(ExpandToPolicy)).GetValue(expandToPolicyIndex)); } }
		public BaseNotePolicy baseNotePolicy { get { return (BaseNotePolicy)(Enum.GetValues(typeof(BaseNotePolicy)).GetValue(baseNotePolicyIndex)); } }
		public BaseNearToPolicy baseNearToPolicy { get { return (BaseNearToPolicy)(Enum.GetValues(typeof(BaseNearToPolicy)).GetValue(baseNearToPolicyIndex)); } }
		public int minimumInterval { get; set; }
		private double lastChordCenter = 0;
		private double lastBase = 0;
		public void resetNearTo() {
			lastChordCenter = 0;
			lastBase = 0;
		}
		public int[] chordToNotes(Chord chord, int transpose) {
			List<int> chordNotes = chord.noteNumbers.ToList();
			for (int i = 0; i < chordNotes.Count; i++) chordNotes[i] += transpose;
			int chordRoot = chordNotes[0];
			int keyRoot = chord.root.id + transpose + ChordBasic.toneList[0].noteNumber;
			int oct = ChordBasic.toneCount;

			List<int> baseNotes = new List<int>();
			int baseNote = chord.baseNoteNumber + transpose - oct;
			// add base notes
			if (baseNotePolicy == BaseNotePolicy._1Oct) baseNotes.Add(baseNote);
			if (baseNotePolicy == BaseNotePolicy._1Oct_5th) { baseNotes.Add(baseNote); baseNotes.Add(baseNote + 7); }
			if (baseNotePolicy == BaseNotePolicy._2Oct) { baseNotes.Add(baseNote); baseNotes.Add(baseNote - oct); }
			if (baseNotePolicy == BaseNotePolicy._2Oct_5th) { baseNotes.Add(baseNote); baseNotes.Add(baseNote - oct); baseNotes.Add(baseNote + 7); }

			// keep note and extend
			List<int> rtn = new List<int>();

			// arrange chord notes
			if (nearToPolicy != NearToPolicy._Nothing && lastChordCenter != 0) {
				if (lastChordCenter != 0) {
					for (int i = 0; i < chordNotes.Count; i++) {
						while (Math.Abs(chordNotes[i] - lastChordCenter) > oct / 2) {
							chordNotes[i] -= Math.Sign(chordNotes[i] - lastChordCenter) * oct;
						}
					}
					if (nearToPolicy == NearToPolicy._Previous) lastChordCenter = chordNotes.Sum() * 1.0 / chordNotes.Count;
				}
				rtn.AddRange(chordNotes);
			} else {
				if (keepNotePolicy == KeepNotePolicy._Nothing) {
					// nothing
				}
				if (keepNotePolicy == KeepNotePolicy._KeepKeyRoot) {
					for (int i = 0; i < chordNotes.Count; i++) {
						while (Math.Abs(chordNotes[i] - keyRoot) > oct / 2) chordNotes[i] -= Math.Sign(chordNotes[i] - keyRoot) * oct;
					}
				}
				if (keepNotePolicy == KeepNotePolicy._KeepChordRoot) {
					for (int i = 0; i < chordNotes.Count; i++) {
						while (Math.Abs(chordNotes[i] - chordRoot) > oct / 2) chordNotes[i] -= Math.Sign(chordNotes[i] - chordRoot) * oct;
					}
				}
				if (keepNotePolicy == KeepNotePolicy._KeepRootTop) {
					chordNotes[0] += oct;
				}
				lastChordCenter = chordNotes.Sum() * 1.0 / chordNotes.Count;
				rtn.AddRange(chordNotes);
			}

			// rotate chord
			rtn = rtn.Distinct().ToList();
			rtn.Sort();
			if (expandToPolicy == ExpandToPolicy._Lower) {
				for (int i = rtn.Count - 1; i >= 1; i--) {
					if (rtn[i] - rtn[i - 1] < minimumInterval) {
						rtn[i - 1] -= oct;
						rtn.Sort();
						i++;
					}
				}
			} else if (expandToPolicy == ExpandToPolicy._Upper) {
				for (int i = 0; i + 1 < rtn.Count; i++) {
					if (rtn[i + 1] - rtn[i] < minimumInterval) {
						rtn[i + 1] += oct;
						rtn.Sort();
						i--;
					}
				}
			} else {
				for (int i = 0; i + 1 < rtn.Count; i++) {
					if (rtn[i + 1] - rtn[i] < minimumInterval && rtn[i + 1] >= chordRoot) {
						rtn[i + 1] += oct;
						rtn.Sort();
						i++;
					}
				}
				for (int i = rtn.Count - 1; i >= 1; i--) {
					if (rtn[i] - rtn[i - 1] < minimumInterval && rtn[i - 1] <= chordRoot) {
						rtn[i - 1] -= oct;
						rtn.Sort();
						i--;
					}
				}
			}

			if (baseNearToPolicy == BaseNearToPolicy._Chord) lastBase = lastChordCenter - oct;
			// arrange base notes
			if (baseNotes.Count > 0) {
				if (baseNearToPolicy != BaseNearToPolicy._Nothing && lastBase != 0) {
					while (Math.Abs(baseNotes[0] - lastBase) > oct / 2) {
						for (int i = 0; i < baseNotes.Count; i++) baseNotes[i] -= Math.Sign(baseNotes[i] - lastBase) * oct;
					}
					if (baseNearToPolicy == BaseNearToPolicy._Previous) lastBase = baseNotes[0];
					rtn.AddRange(baseNotes);
				} else {
					if (keepNotePolicy == KeepNotePolicy._Nothing) {
						// nothing
					}
					if (keepNotePolicy == KeepNotePolicy._KeepKeyRoot) {
						while (Math.Abs(baseNotes[0] - (keyRoot - oct)) > oct / 2) {
							for (int i = 0; i < baseNotes.Count; i++) baseNotes[i] -= Math.Sign(baseNotes[i] - (keyRoot - oct)) * oct;
						}
					}
					if (keepNotePolicy == KeepNotePolicy._KeepChordRoot) {
						while (Math.Abs(baseNotes[0] - (chordRoot - oct)) > oct / 2) {
							for (int i = 0; i < baseNotes.Count; i++) baseNotes[i] -= Math.Sign(baseNotes[i] - (chordRoot - oct)) * oct;
						}
					}
					if (keepNotePolicy == KeepNotePolicy._KeepRootTop) {
					}
					lastBase = baseNotes[0];
					rtn.AddRange(baseNotes);
				}
			}
			return rtn.Distinct().ToArray();
		}
	}
}
