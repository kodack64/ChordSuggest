using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;

namespace ChordSuggest {

	public class Chord {
		public Chord(Tone _root, Harmony _harmony) {
			root = _root;
			harmony = _harmony;
		}
		public static Chord createChordFromChordName(string chordName) {
			string rootString;
			string notationString;
			if (chordName.Length == 1) {
				rootString = chordName;
				notationString = "";
			} else if (chordName[1] != '#' && chordName[1] !='b') {
				rootString = chordName.Substring(0, 1);
				notationString = chordName.Substring(1);
			} else {
				rootString = chordName.Substring(0, 2);
				notationString = chordName.Substring(2);
			}

			Tone root = Tone.getToneFromKeyName(rootString);
			if (root == null) return null;
			Harmony harmony = Harmony.createHarmony(notationString,root.id);
			if(harmony == null) return null;
			else return new Chord(root, harmony);
		}

		public Tone root{get;private set;}
		public Harmony harmony{get;private set;}
		//objと自分自身が等価のときはtrueを返す
		public override bool Equals(object obj) {
			if (obj == null || this.GetType() != obj.GetType()) {
				return false;
			}
			var chord = (Chord)obj;
			return (chord.root.Equals(this.root) && chord.harmony.Equals(this.harmony));
		}
		public override int GetHashCode() {
			return root.id;
		}
		private int[] noteNumbers_=null;
		public int[] noteNumbers {
			get {
				if (noteNumbers_ == null) {
					noteNumbers_ = new int[harmony.toneStepList.Count];
					for (int i = 0; i < harmony.toneStepList.Count; i++) {
						noteNumbers_[i] = harmony.toneStepList[i];
					}
					for(int i=0;i<noteNumbers_.Length;i++){
						noteNumbers_[i] += root.id + ChordBasic.toneList[0].noteNumber;
					}
				}
				return noteNumbers_;
			}
		}
		public int baseNoteNumber {
			get { return root.id + harmony.baseNote + ChordBasic.toneList[0].noteNumber; }
		}
		public override string ToString(){
			string str = root.name;
			for (int i = 0; i < harmony.notations.Count; i++) str += harmony.notations[i].name;
			if (harmony.baseNote != 0) {
				str += "/" + ChordBasic.toneList[(harmony.baseNote+ChordBasic.toneCount+root.id)%ChordBasic.toneCount].name;
			}
			return str;
		}
		public string ToString(int transpose) {
			string str = ChordBasic.toneList[(root.id+transpose)%ChordBasic.toneCount].name;
			for (int i = 0; i < harmony.notations.Count; i++) str += harmony.notations[i].name;
			if (harmony.baseNote != 0) {
				str += "/" + ChordBasic.toneList[(harmony.baseNote + ChordBasic.toneCount + root.id + transpose) % ChordBasic.toneCount].name;
			}
			return str;
		}
		public string ToStringMinor(int transpose) {
			var tone = ChordBasic.toneList[(root.id + transpose) % ChordBasic.toneCount];
			string str;
			if (tone.name.Contains('#')) str = tone.subname;
			else str = tone.name;
			for (int i = 0; i < harmony.notations.Count; i++) str += harmony.notations[i].name;
			if (harmony.baseNote != 0) {
				str += "/" + ChordBasic.toneList[(harmony.baseNote + ChordBasic.toneCount + root.id + transpose) % ChordBasic.toneCount].name;
			}
			return str;
		}
		public void transpose(int transposeKey) {
			root = ChordBasic.toneList[((root.id + transposeKey) % ChordBasic.toneCount)];
		}
	}
	public class Tone {
		public static int octaveInterval;
		public enum IntervalPrefix {
			perfect,
			major,
			minor,
			augmented,
			diminished
		}
		public int id { get; private set; }
		public int octaveShift { get; private set; }
		public string name { get; private set; }
		public string subname { get; private set; }
		public int noteNumber { get; private set; }
		public string intervalName { get; private set; }
		public IntervalPrefix intervalPrefix { get; private set; }
		public int interval { get; private set; }
		public Tone(int _id, string _name,string _subname, int _noteNumber, string _intervalName) {
			id = _id;
			name = _name;
			subname = _subname;
			noteNumber = _noteNumber;
			intervalName = _intervalName;
			if (_intervalName[0] == 'P') intervalPrefix = IntervalPrefix.perfect;
			else if (_intervalName[0] == 'm') intervalPrefix = IntervalPrefix.minor;
			else if (_intervalName[0] == 'M') intervalPrefix = IntervalPrefix.major;
			else if (_intervalName[0] == 'A') intervalPrefix = IntervalPrefix.augmented;
			else if (_intervalName[0] == 'd') intervalPrefix = IntervalPrefix.diminished;
			else throw new Exception();
			interval = int.Parse(_intervalName.Substring(1));
			octaveInterval = Math.Max(octaveInterval,interval+1);
		}
		public override bool Equals(object obj) {
			if (obj == null || this.GetType() != obj.GetType()) {
				return false;
			}
			var tone = (Tone)obj;
			return (tone.id == this.id);
		}
		public override int GetHashCode() {
			return id;
		}
		public static int getIdFromKeyName(string name) {
			return ChordBasic.toneList.Find(m => m.name==name || (m.subname.Length>0 && m.subname==name)).id;
		}
		public static Tone getToneFromKeyName(string name) {
			return ChordBasic.toneList.Find(m => m.name == name || (m.subname.Length > 0 && m.subname == name));
		}
		public static int getStepFromIntervalName(string name) {
			int intervalNum = int.Parse(name.Substring(1));
			int octaveNum = (intervalNum-1) / (octaveInterval-1);
			intervalNum = (intervalNum-1)%(octaveInterval-1)+1;
			string shiftedName = name.Substring(0, 1) + intervalNum.ToString();
			return ChordBasic.toneList.Find(m => m.intervalName==shiftedName).id+octaveNum*ChordBasic.toneCount;
		}
	}
	public class Harmony {
		public int baseNote=0;
		public List<int> toneStepList = new List<int>();
		public List<Notation> notations;
		public string originalNoationName;
		private Harmony(List<Notation> _notations,int _baseNote=0) {
			notations = _notations;			
			baseNote = _baseNote;

			for (int i = 0; i < notations.Count; i++) {
				for (int j = 0; j < notations[i].work.Length; j++) {
					string[] operation = notations[i].work[j].Split(' ');
					if (operation.Length != 2) continue;

					int targetStep = Tone.getStepFromIntervalName(operation[1]);
					if (operation[0] == "add") {
						toneStepList.Add(targetStep);
					}else if(operation[0]=="del"){
						toneStepList.Remove(targetStep);
					}else if(operation[0]=="inc"){
						int targetIndex = toneStepList.FindIndex(m => m == targetStep);
						toneStepList[targetIndex]++;
					}else if(operation[0]=="dec"){
						int targetIndex = toneStepList.FindIndex(m => m == targetStep);
						toneStepList[targetIndex]--;
					}
				}
			}
		}
		public static Harmony createHarmony(string notationName,int rootId=0) {
			string currentNotation = notationName;
			int baseNote=0;

			string[] onChordSplitter = { "/", "on" };
			var sp = currentNotation.Split(onChordSplitter,StringSplitOptions.None);
			if (sp.Length > 1) {
				Tone baseTone = Tone.getToneFromKeyName(sp[1]);
				if (baseTone == null) return null;
				baseNote = (baseTone.id + ChordBasic.toneCount - rootId) % ChordBasic.toneCount;
				currentNotation = sp[0];
			}
			currentNotation = currentNotation.Replace("(", "");
			currentNotation = currentNotation.Replace(")", "");

			bool[] used = new bool[ChordBasic.notationList.Count];
			List<Notation> notations = new List<Notation>();
			while(true){
				var matchedList = ChordBasic.notationList.FindAll(m => 
					(m.name.Length<=currentNotation.Length) && (m.name==currentNotation.Substring(currentNotation.Length-m.name.Length)) && !used[m.id]);
				if (matchedList.Count == 0) break;
				matchedList.Sort((n1,n2)=> (n1.name.Length<n2.name.Length?1:-1));
				used[matchedList[0].id]=true;
				currentNotation = currentNotation.Substring(0,currentNotation.Length-matchedList[0].name.Length);
				notations.Insert(0,matchedList[0]);
			}
			if (currentNotation.Length != 0) return null;
			else return new Harmony(notations,baseNote) { originalNoationName=notationName};
		}
		private long uniqueId_ = -1;
		public long uniqueId {
			get {
				if (uniqueId_ == -1) {
					long rtn = 0;
					foreach (var tone in toneStepList) {
						rtn += (1<<tone);
					}
					rtn += (baseNote<<(ChordBasic.toneCount*2));
					uniqueId_ = rtn;
				}
				return uniqueId_;
			}
		}
		public void uniqueIdRecalculation() {
			long rtn = 0;
			foreach (var tone in toneStepList) {
				rtn += (1 << tone);
			}
			rtn += (baseNote << (ChordBasic.toneCount * 2));
			uniqueId_ = rtn;
		}
		public override bool Equals(object obj) {
			if (obj == null || this.GetType() != obj.GetType()) {
				return false;
			}
			var harmony = (Harmony)obj;
			return (harmony.uniqueId == this.uniqueId);
		}
		public override int GetHashCode() {
			return (int)uniqueId;
		}
	}

	public class Notation {
		public int id { get; private set; }
		public string name{get;private set;}
		public string[] work{get;private set;}
		public Notation(int _id,string _name,string _works) {
			id = _id;
			name = _name;
			work = _works.Split(',');
		}
	}

	class ChordBasic {
		public static List<Tone> toneList {get;private set;}
		public static List<Notation> notationList {get;private set;}

		public static void initialize() {
			toneList = new List<Tone>();
			notationList = new List<Notation>();
			XmlReader xml;
			// load root list
			xml = XmlReader.Create(new StreamReader("Resource/ChordTone.xml"));
			while (xml.Read()) {
				if (xml.NodeType == XmlNodeType.Element && xml.Name == "tone") {
					int id, noteNumber;
					string name, intervalName,subname;
					id = int.Parse(xml.GetAttribute("id"));
					name = xml.GetAttribute("name");
					subname = xml.GetAttribute("subname");
					noteNumber = int.Parse(xml.GetAttribute("number"));
					intervalName = xml.GetAttribute("interval");
					toneList.Add(new Tone(id, name,subname, noteNumber, intervalName));
				}
			}
			toneList.Sort((r1,r2)=> (r1.id>r2.id)?1:-1);
			xml.Close();

			// load chord list
			xml = XmlReader.Create(new StreamReader("Resource/ChordNotation.xml"));
			while (xml.Read()) {
				if (xml.NodeType == XmlNodeType.Element && xml.Name == "chord") {
					string name = xml.GetAttribute("name");
					string work = xml.GetAttribute("work");
					notationList.Add(new Notation(notationList.Count,name, work));
				}
			}
			xml.Close();
		}
		public static int toneCount{
			get { return toneList.Count; }
		}
		public static Tone getTone(int index) {
			return toneList[index];
		}
	}
}
