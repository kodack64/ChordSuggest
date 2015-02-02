using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Windows;

namespace ChordSuggest {

	class MidiAdapter {

		[DllImport("winmm.dll")]
		protected static extern int midiOutGetNumDevs();
		[DllImport("winmm.dll")]
		protected static extern int midiOutGetDevCaps(int deviceID, ref MidiOutCaps caps, int sizeOfMidiOutCaps);
		[DllImport("winmm.dll")]
		private static extern int midiOutOpen(ref int handle, int deviceID, MidiCallback proc, int instance, int flags);
		[DllImport("winmm.dll")]
		protected static extern int midiOutShortMsg(int handle, uint message);
		[DllImport("winmm.dll")]
		protected static extern int midiOutReset(int handle);
		[DllImport("winmm.dll")]
		protected static extern int midiOutClose(int handle);
		[StructLayout(LayoutKind.Sequential)]
		public struct MidiOutCaps {
			public ushort wMid;
			public ushort wPid;
			public uint vDriverVersion;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string szPname;
			public ushort wTechnology;
			public ushort wVoices;
			public ushort wNotes;
			public ushort wChannelMask;
			public uint dwSupport;
		}
		protected delegate void MidiCallback(int handle, int msg,
			int instance, int param1, int param2);


		public class MidiOutDevice {
			public MidiOutDevice(int index_,MidiOutCaps moc) {
				name = moc.szPname;
				index = index_;
			}
			public string name;
			public int index;
		}
		private List<MidiOutDevice> outDeviceList = new List<MidiOutDevice>();
		public void initialize() {
			int deviceCount = midiOutGetNumDevs();
			for (int i = 0; i < deviceCount; i++) {
				MidiOutCaps moc = new MidiOutCaps();
				midiOutGetDevCaps(i, ref moc, (int)Marshal.SizeOf(typeof(MidiOutCaps)));
				MidiOutDevice outDev = new MidiOutDevice(i,moc);
				outDeviceList.Add(outDev);
			}
		}
		public int midiOutDeviceCount {	get { return outDeviceList.Count;}}
		public string getMidiOutDeviceName(int index){return outDeviceList[index].name;}
		public int currentHandle=-1;
		public void open(int index) {
			if (currentHandle != -1) close();
			midiOutOpen(ref currentHandle,index,null,0,0);
		}
		public void close() {
			midiOutReset(currentHandle);
			midiOutClose(currentHandle);
			currentHandle = -1;
		}
		private uint makeShortMassage(int status,int channel,int data1,int data2){
			return (uint)( (status<<4) | channel | (data1<<8) | (data2<<16));
		}
		public void sendNote(int note, int velocity,int channel) {
			midiOutShortMsg(currentHandle, makeShortMassage(0x9, channel, note, velocity));
		}
		public void stopNote(int note,int channel) {
			midiOutShortMsg(currentHandle, makeShortMassage(0x9, channel, note, 0));
		}
		public void changeProgram(int program, int channel) {
			midiOutShortMsg(currentHandle, makeShortMassage(0xC, channel, program, 0));
		}
	}

	class MidiManage {
		public MidiAdapter midiAdapter = new MidiAdapter();
		public List<string> outDeviceList = new List<string>();
		public int currentOpenDevice = -1;
		public Canvas canvas=null;
		public void initialize() {
			midiAdapter.initialize();
			int outDeviceCount = midiAdapter.midiOutDeviceCount;
			for (int i = 0; i < outDeviceCount; i++) {
				outDeviceList.Add(midiAdapter.getMidiOutDeviceName(i));
			}

			whiteKeyCountInOctave = 0;
			for (int i = 0; i < ChordBasic.toneCount; i++) {
				if (isWhiteKey(i)) whiteKeyCountInOctave++;
			}
		}
		public void open(int id) {
			close();
			currentOpenDevice = id;
			midiAdapter.open(currentOpenDevice);
			MainWindow.Write("\t"+outDeviceList[id] + " is opened\n") ;
		}
		public void close() {
			if (currentOpenDevice != -1) {
				MainWindow.Write("Close current midi device\n"); 
				midiAdapter.close();
			}
		}
		HashSet<int> playedNotes = new HashSet<int>();
		int[] lastPlayedNotes = null;
		public void playChord(Chord chord,int velocity,int channel,int transpose=0) {
			if (chord == null) return;
			var notes = VoicingManage.getInstance().chordToNotes(chord,transpose);
			paintPlayedNote(notes);
			foreach (int note in notes) {
				midiAdapter.sendNote(note,velocity,channel);
				playedNotes.Add(note);
			}
			lastPlayedNotes = notes;
		}
		public void stopChord(Chord chord,int channel,int transpose=0) {
			stopNotes(channel);
/*			if (chord == null) return;
			var notes = chord.noteNumbers;
			foreach (int note in notes) {
				midiAdapter.stopNote(note+transpose,channel);
			}*/
		}
		public void stopNotes(int channel) {
			clearPlayedNote();
			foreach(int note in playedNotes){
				midiAdapter.stopNote(note, channel);
			}
			playedNotes.Clear();
		}
		public void changeProgram(int program, int channel) {
			midiAdapter.changeProgram(program,channel);
		}
		public void setCanvas(Canvas _canvas) { canvas = _canvas; }
		private int lowestKey = 0;
		private int highestKey = 128;
		private double keyWidth = 10;
		private double blackKeyWidth = 5;
		private double keyHeight = 30;
		private double blackKeyHeight = 20;
		private double ellipseSize = 10;
		private double ellipseHeight = 20;
		private double blackKeyEllipseHeight = 10;
		private List<UIElement> keyboardUieList = new List<UIElement>();
		public void paintKeyboard() {
			foreach (UIElement uie in keyboardUieList) {
				canvas.Children.Remove(uie);
			}
			double position = 0;
			keyWidth = canvas.ActualWidth / (getWhiteKeyCount(highestKey)-getWhiteKeyCount(lowestKey));
			blackKeyWidth = keyWidth / 2;
			Rectangle rect;
			for (int i = lowestKey; i < highestKey; i++) {
				if (isWhiteKey(i)) {
					rect = new Rectangle() {Stroke = Brushes.Black };
					rect.Width = keyWidth;
					rect.Height = keyHeight;
					Canvas.SetLeft(rect, position);
					Canvas.SetTop(rect,0);
					canvas.Children.Add(rect);
					keyboardUieList.Add(rect);
					position += keyWidth;
				} else {
					rect = new Rectangle() { Fill = Brushes.Black };
					rect.Width = blackKeyWidth;
					rect.Height = blackKeyHeight;
					Canvas.SetLeft(rect, position-blackKeyWidth/2);
					Canvas.SetTop(rect, 0);
					canvas.Children.Add(rect);
					keyboardUieList.Add(rect);
				}
			}
		}
		private bool isWhiteKey(int note) {
			int myTone = (((note - ChordBasic.toneList[0].noteNumber) % ChordBasic.toneCount)+ChordBasic.toneCount)%ChordBasic.toneCount;
			return (ChordBasic.toneList[myTone].intervalPrefix == Tone.IntervalPrefix.perfect || ChordBasic.toneList[myTone].intervalPrefix == Tone.IntervalPrefix.major);
		}
		private int getWhiteKeyCount(int note) {
			int octFromLowestKey = (note - lowestKey) / ChordBasic.toneCount;
			int keyCount =  octFromLowestKey * whiteKeyCountInOctave;
			for (int i = octFromLowestKey * ChordBasic.toneCount + lowestKey ; i < note; i++) {
				if (isWhiteKey(i)) keyCount++;
			}
			return keyCount;
		}
		private List<UIElement> uieList = new List<UIElement>();
		private int whiteKeyCountInOctave;
		public void paintPlayedNote(int[] notes) {
			foreach (UIElement uie in uieList) {
				canvas.Children.Remove(uie);
			}
			foreach (int note in notes) {
				double position = getWhiteKeyCount(note)*keyWidth;
				if (isWhiteKey(note)) position += keyWidth / 2;
				position -= ellipseSize / 2;
				Ellipse el = new Ellipse();
				el.Fill = Brushes.Red;
				el.Width = ellipseSize;
				el.Height = ellipseSize;
				Canvas.SetLeft(el, position);
				if(isWhiteKey(note))Canvas.SetTop(el, ellipseHeight);
				else Canvas.SetTop(el, blackKeyEllipseHeight);
				canvas.Children.Add(el);
				uieList.Add(el);
			}
		}
		public void clearPlayedNote() {
			foreach (UIElement uie in uieList) {
				canvas.Children.Remove(uie);
			}
		}
	}

	class VoicingManage {
		private VoicingManage() { }
		private static VoicingManage myInstance=null;
		public static void createInstance(){
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
		public int[] chordToNotes(Chord chord,int transpose) {
			List<int> chordNotes = chord.noteNumbers.ToList();
			for (int i = 0; i < chordNotes.Count; i++) chordNotes[i] += transpose;
			int chordRoot = chordNotes[0];
			int keyRoot = chord.root.id+transpose+ChordBasic.toneList[0].noteNumber;
			int oct = ChordBasic.toneCount;

			List<int> baseNotes = new List<int>();
			int baseNote = chord.baseNoteNumber+transpose-oct;
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
					if(nearToPolicy == NearToPolicy._Previous)lastChordCenter = chordNotes.Sum() * 1.0 / chordNotes.Count;
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
					if (rtn[i + 1] - rtn[i] < minimumInterval && rtn[i+1]>=chordRoot) {
						rtn[i + 1] += oct;
						rtn.Sort();
						i++;
					}
				}
				for (int i = rtn.Count - 1; i >= 1; i--) {
					if (rtn[i] - rtn[i - 1] < minimumInterval && rtn[i-1]<=chordRoot) {
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
						for (int i = 0; i<baseNotes.Count;i++ ) baseNotes[i] -= Math.Sign(baseNotes[i] - lastBase) * oct;
					}
					if (baseNearToPolicy == BaseNearToPolicy._Previous) lastBase = baseNotes[0];
					rtn.AddRange(baseNotes);
				} else {
					if (keepNotePolicy == KeepNotePolicy._Nothing) {
						// nothing
					}
					if (keepNotePolicy == KeepNotePolicy._KeepKeyRoot) {
						while (Math.Abs(baseNotes[0] - (keyRoot-oct)) > oct / 2) {
							for (int i = 0; i < baseNotes.Count; i++) baseNotes[i] -= Math.Sign(baseNotes[i] - (keyRoot-oct)) * oct;
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
