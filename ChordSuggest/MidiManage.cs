using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

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
		public void initialize() {
			midiAdapter.initialize();
			int outDeviceCount = midiAdapter.midiOutDeviceCount;
			for (int i = 0; i < outDeviceCount; i++) {
				outDeviceList.Add(midiAdapter.getMidiOutDeviceName(i));
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
		public void playChord(Chord chord,int velocity,int channel,int transpose=0) {
			if (chord == null) return;
			var notes = chord.noteNumbers;
			foreach (int note in notes) {
				midiAdapter.sendNote(note+transpose,velocity,channel);
			}
		}
		public void stopChord(Chord chord,int channel,int transpose=0) {
			if (chord == null) return;
			var notes = chord.noteNumbers;
			foreach (int note in notes) {
				midiAdapter.stopNote(note+transpose,channel);
			}
		}
		public void changeProgram(int program,int channel){
			midiAdapter.changeProgram(program,channel);
		}
	}
}
