using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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


	class MidiManage {
		public MidiAdapter midiAdapter = new MidiAdapter();
		public List<string> outDeviceList = new List<string>();
		public List<string> inDeviceList = new List<string>();
		public int currentOpenDevice = -1;
		public Canvas canvas=null;
		public void initialize() {
			midiAdapter.initialize();
			int outDeviceCount = midiAdapter.midiOutDeviceCount;
			for (int i = 0; i < outDeviceCount; i++) {
				outDeviceList.Add(midiAdapter.getMidiOutDeviceName(i));
			}
			int inDeviceCount = midiAdapter.midiInDeviceCount;
			for (int i = 0; i < inDeviceCount; i++) {
				inDeviceList.Add(midiAdapter.getMidiInDeviceName(i));
			}

			whiteKeyCountInOctave = 0;
			for (int i = 0; i < ChordBasic.toneCount; i++) {
				if (ChordBasic.isWhiteKey(i)) whiteKeyCountInOctave++;
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
				if (ChordBasic.isWhiteKey(i)) {
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
		private int getWhiteKeyCount(int note) {
			int octFromLowestKey = (note - lowestKey) / ChordBasic.toneCount;
			int keyCount =  octFromLowestKey * whiteKeyCountInOctave;
			for (int i = octFromLowestKey * ChordBasic.toneCount + lowestKey ; i < note; i++) {
				if (ChordBasic.isWhiteKey(i)) keyCount++;
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
				if (ChordBasic.isWhiteKey(note)) position += keyWidth / 2;
				position -= ellipseSize / 2;
				Ellipse el = new Ellipse();
				el.Fill = Brushes.Red;
				el.Width = ellipseSize;
				el.Height = ellipseSize;
				Canvas.SetLeft(el, position);
				if(ChordBasic.isWhiteKey(note))Canvas.SetTop(el, ellipseHeight);
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

}
