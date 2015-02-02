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
using System.Diagnostics;

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
			Stopwatch wa = new Stopwatch();
			double loadTime, convertTime, fftTime;
			wa.Start();
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
				int fmtExtraSize = reader.ReadInt16();
				reader.ReadBytes(fmtExtraSize);
			}
			int dataID = reader.ReadInt32();
			int dataSize = reader.ReadInt32();
			byte[] byteArray = new byte[dataSize];
			byteArray = reader.ReadBytes(dataSize);
			reader.Close();
			wa.Stop();
			loadTime = wa.ElapsedMilliseconds * 1e-3;
			wa.Reset();
			wa.Start();
			int byteDepth = bitDepth / 8;
			int dataCount = dataSize / byteDepth/channels;
			double[,] waveData = new double[channels,dataCount];
			double[] targetData = new double[dataCount];
			int cur =0;
			for (int i = 0; i < dataCount; i++) {
				for (int c = 0; c < channels; c++) {
					if (byteDepth == 1) {
						waveData[c, i] = byteArray[cur] / 128.0-1.0;
					}else if (byteDepth == 2) {
						waveData[c, i] = ((Int16)(byteArray[cur] + 256 * byteArray[cur + 1])) / 32768.0;
					}else if (byteDepth == 3) {
						double temp = ((Int32)(byteArray[cur] + 256 * byteArray[cur + 1] + 65536 * byteArray[cur + 2])) / 8388608.0;
						waveData[c, i] = temp <1.0?temp:temp-2.0;
					}else if (byteDepth == 4) {
						waveData[c, i] = BitConverter.ToSingle(byteArray, cur);
					}
					cur += byteDepth;
				}
				targetData[i] = waveData[0,i];
			}
			wa.Stop();
			convertTime = wa.ElapsedMilliseconds * 1e-3;
			wa.Reset();
			wa.Start();
			int shiftBit = 10;
			int blockBit = 14;
//			int shiftBit = 14;
//			int blockBit = 14;
			int blockSize = 1 << blockBit;
			int shiftSize = 1 << shiftBit;
			int start = 0;
			int blockCount = (dataCount-blockSize) / shiftSize;
			int blockCur = 0;
			int blockPer = 1;
			double baseFreq = 1.0 * sampleRate / blockSize;
			double[] block = new double[blockSize];
			double[] spectrum = new double[blockSize];
//			StreamWriter sw;
//			sw = new StreamWriter("spectrum.txt");
			setFFTBitSize(blockBit);
			while (start + blockSize < dataCount) {
				for (int i = 0; i<blockSize;i++ ){
					block[i] = targetData[start+i] * (0.5-0.5*Math.Cos(2*Math.PI*i/blockSize));
				}
				getSpectrum(block,spectrum,blockBit);
				start += shiftSize;

//				for (int i = 0; i < Math.Min((int)(1000/basefreq),(1<<blockBit)/2); i++) {
//					sw.Write(spectrum[i].ToString()+" ");
//				}
//				sw.WriteLine();
				blockCur++;
				if (blockCount * blockPer <= blockCur*100) {
					Console.WriteLine("{0}% ({1}/{2}block) completed at {3}sec",blockPer,blockCur,blockCount,wa.ElapsedMilliseconds*1e-3);
					blockPer++;
				}
			}
//			sw.Close();
			wa.Stop();
			fftTime = wa.ElapsedMilliseconds * 1e-3;

			double waveTime = dataCount/44100.0;
			Console.WriteLine("{0}{1}{3}",loadTime,convertTime,fftTime);


/*			sw = new StreamWriter("wave.txt");
			for (int i = 0; i<Math.Min(100000,dataCount);i++ ) {
				sw.WriteLine((i*1.0/sampleRate).ToString()+" "+targetData[i].ToString());
			}
			sw.Close();*/
		}
		int bitSize;
		int dataSize;
		int[] reverseBitArray;
		double[] outputRe;
		double[] outputIm;
		private void setFFTBitSize(int bitSize_) {
			bitSize = bitSize_;
			dataSize = 1 << bitSize;
			reverseBitArray = BitScrollArray(1<<bitSize);
			outputRe = new double[1 << bitSize];
			outputIm = new double[1 << bitSize];
		}
		private void getSpectrum(double[] din,double[] spectrum, int bitSize) {
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
			for (int i = 0; i < dataSize/2; i++) {
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
