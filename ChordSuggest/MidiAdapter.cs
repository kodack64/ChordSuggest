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

		[DllImport("winmm.dll")]
		protected static extern int midiInGetNumDevs();
		[DllImport("winmm.dll")]
		protected static extern int midiInGetDevCaps(int deviceID, ref MidiInCaps caps, int sizeOfMidiOutCaps);


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
		[StructLayout(LayoutKind.Sequential)]
		public struct MidiInCaps {
			public ushort wMid;
			public ushort wPid;
			public uint vDriverVersion;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string szPname;
			public uint dwSupport;
		}
		protected delegate void MidiCallback(int handle, int msg,
			int instance, int param1, int param2);


		public class MidiOutDevice {
			public MidiOutDevice(int index_, MidiOutCaps moc) {
				name = moc.szPname;
				index = index_;
			}
			public string name;
			public int index;
		}
		public class MidiInDevice {
			public MidiInDevice(int index_, MidiInCaps moc) {
				name = moc.szPname;
				index = index_;
			}
			public string name;
			public int index;
		}
		private List<MidiOutDevice> outDeviceList = new List<MidiOutDevice>();
		private List<MidiInDevice> inDeviceList = new List<MidiInDevice>();

	
		
		
		
		public void initialize() {
			int outDeviceCount = midiOutGetNumDevs();
			for (int i = 0; i < outDeviceCount; i++) {
				MidiOutCaps moc = new MidiOutCaps();
				midiOutGetDevCaps(i, ref moc, (int)Marshal.SizeOf(typeof(MidiOutCaps)));
				MidiOutDevice outDev = new MidiOutDevice(i, moc);
				outDeviceList.Add(outDev);
			}
			int inDeviceCount = midiInGetNumDevs();
			for (int i = 0; i < inDeviceCount; i++) {
				MidiInCaps mic = new MidiInCaps();
				midiInGetDevCaps(i, ref mic, (int)Marshal.SizeOf(typeof(MidiInCaps)));
				MidiInDevice inDev = new MidiInDevice(i, mic);
				inDeviceList.Add(inDev);
			}
		}
		public int midiOutDeviceCount { get { return outDeviceList.Count; } }
		public string getMidiOutDeviceName(int index) { return outDeviceList[index].name; }
		public int midiInDeviceCount { get { return inDeviceList.Count; } }
		public string getMidiInDeviceName(int index) { return inDeviceList[index].name; }
		public int currentHandle = -1;
		public void open(int index) {
			if (currentHandle != -1) close();
			midiOutOpen(ref currentHandle, index, null, 0, 0);
		}
		public void close() {
			midiOutReset(currentHandle);
			midiOutClose(currentHandle);
			currentHandle = -1;
		}
		private uint makeShortMassage(int status, int channel, int data1, int data2) {
			return (uint)((status << 4) | channel | (data1 << 8) | (data2 << 16));
		}
		public void sendNote(int note, int velocity, int channel) {
			midiOutShortMsg(currentHandle, makeShortMassage(0x9, channel, note, velocity));
		}
		public void stopNote(int note, int channel) {
			midiOutShortMsg(currentHandle, makeShortMassage(0x9, channel, note, 0));
		}
		public void changeProgram(int program, int channel) {
			midiOutShortMsg(currentHandle, makeShortMassage(0xC, channel, program, 0));
		}
	}
}
