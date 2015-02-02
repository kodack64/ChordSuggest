using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using System.Runtime.InteropServices;

namespace ChordSuggest {

	/// <summary>
	/// WaveAnalyze.xaml の相互作用ロジック
	/// </summary>
	public partial class WaveAnalyze : Window {
		private string myTargetFile;
		public WaveAnalyze(string file) {
			myTargetFile = file;
			InitializeComponent();
			loadFile();
		}
		private void loadFile() {
			BinaryReader reader = new BinaryReader(File.Open(myTargetFile,FileMode.Open));
			int chunkID = reader.ReadInt32();
			int fileSize = reader.ReadInt32();
			int riffType = reader.ReadInt32();
			int fmtID = reader.ReadInt32();
			int fmtSize = reader.ReadInt32();
			int fmtCode = reader.ReadInt16();
			int channels = reader.ReadInt16();
			int sampleRate = reader.ReadInt32();
			int fmtAvgBPS = reader.ReadInt32();
			int fmtBlockAlign = reader.ReadInt16();
			int bitDepth = reader.ReadInt16();
			if (fmtSize == 18) {
				// Read any extra values
				int fmtExtraSize = reader.ReadInt16();
				reader.ReadBytes(fmtExtraSize);
			}
			int dataID = reader.ReadInt32();
			int dataSize = reader.ReadInt32();
			byte[] byteArray = new byte[dataSize];
			byteArray = reader.ReadBytes(dataSize);
			reader.Close();

			int byteDepth = bitDepth / 8;
			int dataCount = dataSize / byteDepth/channels;
			double[,] waveData = new double[channels,dataCount];
			double[] targetData = new double[dataCount];
			int cur =0;
			for (int i = 0; i < dataCount; i++) {
				for (int c = 0; c < channels; c++) {
					if (byteDepth == 2) {
						waveData[c, i] = ((Int16)(byteArray[cur] + 256 * byteArray[cur + 1])) / 32768.0;
						cur += 2;
					}
/*					Int64 val =0;
					Int64 currentIndex = 1;
					for (int b = 0; b < byteDepth; b++) {
						val+= byteArray[cur] * currentIndex;
						if (b + 1 < byteDepth) currentIndex = currentIndex << 8;
						cur++;
					}*/
//						double temp = 2.0 * val / (1 << bitDepth);
//						waveData[c, i] = temp < 1.0 ? temp : -2.0 + temp;
				}
				targetData[i] = waveData[0,i];
			}

//			int shiftBit = 12;
//			int blockBit = 13;
			int shiftBit = 14;
			int blockBit = 14;
			int start = 0;
			double[] block = new double[(1 << blockBit)];
			double[] spectrum;
			StreamWriter sw = new StreamWriter("test.txt");
			while (start + (1 << blockBit) < dataCount) {
				Array.Copy(targetData,start,block,0,(1<<blockBit));
				getSpectrum(block,out spectrum,blockBit);
				start += (1 << shiftBit);

				double basefreq = 1.0 / ((1 << blockBit) * 1.0 / sampleRate);
				for (int i = 0; i < Math.Min((int)(1000/basefreq),(1<<blockBit)/2); i++) {
					sw.Write(spectrum[i].ToString()+" ");
				}
				sw.WriteLine();
			}
			sw.Close();
		}
		private void getSpectrum(double[] din,out double[] spectrum, int bitSize) {
			int dataSize = 1 << bitSize;
			int[] reverseBitArray = BitScrollArray(dataSize);
			double[] outputRe = new double[1<<bitSize];
			double[] outputIm = new double[1<<bitSize];
			for (int i = 0; i < dataSize; i++) {
				outputRe[i] = din[reverseBitArray[i]];
				outputIm[i] = 0;
			}
			for (int stage = 1; stage <= bitSize; stage++) {
				int butterflyDistance = 1 << stage;
				int numType = butterflyDistance >> 1;
				int butterflySize = butterflyDistance >> 1;

				double wRe = 1.0;
				double wIm = 0.0;
				double uRe =
					System.Math.Cos(System.Math.PI / butterflySize);
				double uIm =
					-System.Math.Sin(System.Math.PI / butterflySize);

				for (int type = 0; type < numType; type++) {
					for (int j = type; j < dataSize; j += butterflyDistance) {
						int jp = j + butterflySize;
						double tempRe =
							outputRe[jp] * wRe - outputIm[jp] * wIm;
						double tempIm =
							outputRe[jp] * wIm + outputIm[jp] * wRe;
						outputRe[jp] = outputRe[j] - tempRe;
						outputIm[jp] = outputIm[j] - tempIm;
						outputRe[j] += tempRe;
						outputIm[j] += tempIm;
					}
					double tempWRe = wRe * uRe - wIm * uIm;
					double tempWIm = wRe * uIm + wIm * uRe;
					wRe = tempWRe;
					wIm = tempWIm;
				}
			}
			spectrum = new double[dataSize];
			for (int i = 0; i < dataSize; i++) {
				spectrum[i] = Math.Sqrt(outputRe[i]*outputRe[i]+outputIm[i]*outputIm[i]);
			}
		}
		private int[] BitScrollArray(int arraySize) {
			int[] reBitArray = new int[arraySize];
			int arraySizeHarf = arraySize >> 1;

			reBitArray[0] = 0;
			for (int i = 1; i < arraySize; i <<= 1) {
				for (int j = 0; j < i; j++)
					reBitArray[j + i] = reBitArray[j] + arraySizeHarf;
					arraySizeHarf >>= 1;
			}

			return reBitArray;
		}
	}
}
