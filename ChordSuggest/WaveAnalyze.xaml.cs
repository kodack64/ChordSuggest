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
using System.Threading;
using System.ComponentModel;

namespace ChordSuggest {

	/// <summary>
	/// WaveAnalyze.xaml の相互作用ロジック
	/// </summary>
	public partial class WaveAnalyze : Window,INotifyPropertyChanged {
		private string myTargetFile;
		public WaveAnalyze(string file) {
			myTargetFile = file;
			InitializeComponent();
			this.Dispatcher.InvokeAsync(
				new Action(() => {
					loadFile();
				})
			);
		}

		// fft params
		int channels;
		int sampleRate;
		int bitDepth;
		int dataCount;
		byte[] byteArray;

		// fft load params
		double[,] waveData;
		double[] targetData;
	
		// fft settings
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName) {
			PropertyChangedEventHandler handler = this.PropertyChanged;
			if (handler != null)
				handler(this, new PropertyChangedEventArgs(propertyName));
		}
		int shiftBitSetting_;
		public int shiftBitSetting {
			set {
				shiftBitSetting_ = value;
				OnPropertyChanged("shiftBitSetting");
				Label_ShiftLength.Content = String.Format("{0} samples, {1} , {2} blocks",
					1 << shiftBitSetting_,
					(1.0 * (1 << shiftBitSetting_) / sampleRate < 1.0) ? (1.0 * (1 << shiftBitSetting_) / sampleRate * 1e3).ToString("G3") + "ms" : (1.0 * (1 << shiftBitSetting_) / sampleRate).ToString("G3") + "sec", 
					Math.Max((dataCount-(1<<blockBitSetting)) / (1 << shiftBitSetting_),0.0)
					);
			}
			get {
				return shiftBitSetting_;
			}
		}
		int blockBitSetting_;
		public int blockBitSetting {
			set {
				blockBitSetting_ = value;
				OnPropertyChanged("blockBitSetting");
				Label_BlockLength.Content = String.Format("{0}samples, {1:G3}, {2:G3}Hz resolution (cannot distinguish note lower than {3})", 
					1 << blockBitSetting_,
					(1.0 * (1 << blockBitSetting_) / sampleRate < 1.0) ? (1.0 * (1 << blockBitSetting_) / sampleRate * 1e3).ToString("G3") + "ms" : (1.0 * (1 << blockBitSetting_) / sampleRate).ToString("G3") + "sec",
					1.0 * sampleRate / (1 << blockBitSetting_), 
					getDivisibleNote(1.0 * sampleRate / (1 << blockBitSetting_))
					);
			}
			get {
				return blockBitSetting_;
			}
		}
		private string getDivisibleNote(double freq) {
			int note;
			for (note = 0; note < 128; note++) {
				double cfreq = 440 * Math.Pow(2, ((note+1) - 69) / 12.0) - 440 * Math.Pow(2, (note - 69) / 12.0);
				if (cfreq > freq) break;
			}
			return  ChordBasic.toneList[(((note % ChordBasic.toneCount) + ChordBasic.toneCount) % ChordBasic.toneCount)].name + (note / ChordBasic.toneCount).ToString();			
		}

		int shiftBit;
		int blockBit;
		int shiftSize;
		int blockSize;
		int blockCount;
		int cpuCount;
		double baseFreq;
		bool useParallel;
		int processedCount;
		int targetType;
		int windowType;
		bool workIsCancelled;

		// fft result
		double[,] noteSpectrums;
		BackgroundWorker worker;

		private void loadFile() {

			// read files
			BinaryReader reader = new BinaryReader(File.Open(myTargetFile,FileMode.Open));
			int chunkID = reader.ReadInt32();
			int fileSize = reader.ReadInt32();
			int riffType = reader.ReadInt32();
			int fmtID = reader.ReadInt32();
			int fmtSize = reader.ReadInt32();
			int fmtCode = reader.ReadInt16();
			channels = reader.ReadInt16();
			sampleRate = reader.ReadInt32();
			int fmtAvgBPS = reader.ReadInt32();
			int fmtBlockAlign = reader.ReadInt16();
			bitDepth = reader.ReadInt16();
			if (fmtSize == 18) {
				int fmtExtraSize = reader.ReadInt16();
				reader.ReadBytes(fmtExtraSize);
			}
			int dataID = reader.ReadInt32();
			int dataSize = reader.ReadInt32();
			byteArray = new byte[dataSize];
			byteArray = reader.ReadBytes(dataSize);
			reader.Close();


			// convert to double
			int byteDepth = bitDepth / 8;
			dataCount = dataSize / byteDepth/channels;
			waveData = new double[channels, dataCount];
			targetData = new double[dataCount];
			int cur = 0;
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
			}

			Label_FileName.Content = myTargetFile;
			double time = dataCount/sampleRate;
			string str;
			if(time>=3600)str=String.Format("{0}h {1}m {2}s",((int)time)/3600,(((int)time)%3600)/60,(((int)time)%60));
			else if(time>=60)str=String.Format("{0}m {1}s",(((int)time)%3600)/60,(((int)time)%60));
			else str= time.ToString("G3")+"s";
			Label_PlayTime.Content = str;
			Label_ChannelCount.Content = channels.ToString() + " ch";
			Label_SampleCount.Content = dataCount.ToString()+" samples";
			Label_SamplingRate.Content = sampleRate.ToString() + " Hz";
			Label_BitDepth.Content = bitDepth.ToString()+" bit";

			if (channels == 1) {
				ComboBox_TargetArray.Items.Add("Monoral");
			} else {
				ComboBox_TargetArray.Items.Add("Stereo Mixed");
				ComboBox_TargetArray.Items.Add("L+R");
				ComboBox_TargetArray.Items.Add("L-R");
				ComboBox_TargetArray.Items.Add("L");
				ComboBox_TargetArray.Items.Add("R");
			}
			ComboBox_TargetArray.SelectedIndex = 0;

			Slider_ShiftBit.Minimum = 8;
			Slider_ShiftBit.Maximum = 16;
			Slider_ShiftBit.IsSnapToTickEnabled = true;
			Slider_ShiftBit.DataContext = this;
			shiftBitSetting = 10;

			Slider_BlockBit.Minimum = 8;
			Slider_BlockBit.Maximum = 16;
			Slider_BlockBit.IsSnapToTickEnabled = true;
			Slider_BlockBit.DataContext = this;
			blockBitSetting = 14;

			ComboBox_WindowFunction.Items.Add("ハミング窓");
			ComboBox_WindowFunction.Items.Add("ハン窓");
			ComboBox_WindowFunction.Items.Add("矩形窓");
			ComboBox_WindowFunction.SelectedIndex = 0;

			ProgressBar_Convert.Maximum = 100;
			ProgressBar_Convert.Minimum = 0;
			ProgressBar_Convert.Value = 0;


			Button_StartWork.IsEnabled = true;
		}
		private void Callback_WorkerStart(object sender, RoutedEventArgs arg) {

			Button_StartWork.IsEnabled = false;
			Button_CancelWork.IsEnabled= true;
			workIsCancelled = false;

			shiftBit = shiftBitSetting;
			blockBit = blockBitSetting;
			blockSize = 1 << blockBit;
			shiftSize = 1 << shiftBit;
			blockCount = (dataCount - blockSize) / shiftSize;
			baseFreq = 1.0 * sampleRate / blockSize;
			useParallel = (CheckBox_UseParallelize.IsChecked.Value == true);
			noteSpectrums = new double[blockCount, 128];

			ProgressBar_Convert.Maximum = blockCount;
			ProgressBar_Convert.Minimum = 0;
			ProgressBar_Convert.Value = 0;

			windowType = ComboBox_WindowFunction.SelectedIndex;
			targetType = ComboBox_TargetArray.SelectedIndex;

			processedCount = 0;

			worker = new BackgroundWorker();
			worker.WorkerReportsProgress = true;
			worker.WorkerSupportsCancellation = true;
			worker.DoWork += new DoWorkEventHandler(doAnalyze);
			worker.ProgressChanged += new ProgressChangedEventHandler(progressChanged);
			worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workCompleted);
			worker.RunWorkerAsync();
		}
		private void Callback_WorkerCancel(object sender, RoutedEventArgs arg) {
			worker.CancelAsync();
			Button_CancelWork.IsEnabled = false;
			workIsCancelled = true;
		}
		private void progressChanged(object sender, ProgressChangedEventArgs arg) {
			int value = arg.ProgressPercentage;
			if (value == -1) {
				Label_ConvertProgress.Content = "Data preparing...";
			} else {
				ProgressBar_Convert.Value = value;
				Label_ConvertProgress.Content = String.Format("Block {0}/{1}", value, blockCount);
			}
		}
		private void workCompleted(object sender, RunWorkerCompletedEventArgs arg) {
			if (arg.Error != null) {
				Label_ConvertProgress.Content = "処理中に例外が起きました";
			} else if (workIsCancelled) {
				Label_ConvertProgress.Content = String.Format("Cancelled {0}/{1}", processedCount, blockCount);
			} else {
				Label_ConvertProgress.Content = String.Format("Completed {0} blocks",blockCount);
				Tab_Spectrum.IsEnabled = true;
			}
			Button_StartWork.IsEnabled = true;
			Button_CancelWork.IsEnabled = false;
		}
		private void doAnalyze(object sender, DoWorkEventArgs arg) {
			// fft
			// memo
			// shiftBit/blockBit and time resolution
			// 2^10 / 44100			= 23ms
			// 2^11 / 44100			= 46ms
			// 2^12 / 44100			= 93ms
			// 2^13 / 44100			= 186ms
			// 2^14 / 44100			= 372ms
			// 2^15 / 44100			= 743ms
			// 2^16 / 44100			= 1486ms

			// note and required time resolution
			// 64th note in 120bpm	= 31ms
			// 32th note in 120bpm	= 63ms
			// 16th note in 120bpm	= 125ms
			// 8th note in 120bpm	= 250ms
			// 4th note in 120bpm	= 500ms
			// 2th note in 120bpm	= 1000ms
			// whole note in 120bpm	= 2000ms

			// block bit and frequency resoluton in sample rate = 44100
			// 44100/(2^16)			= 0.673Hz
			// 44100/(2^15)			= 1.35Hz
			// 44100/(2^14)			= 2.69Hz
			// 44100/(2^13)			= 5.38Hz
			// 44100/(2^12)			= 10.8Hz
			// 44100/(2^11)			= 21.5Hz
			// 44100/(2^11)			= 43.1Hz

			// note number and required frequency resolution
			// 0-1					= 0.486Hz
			// 12-13				= 0.9725Hz
			// 24-25				= 1.94Hz
			// 36-37				= 3.89Hz
			// 48-49				= 7.78Hz
			// 60-61				= 15.6Hz
			// 72-73				= 31.1Hz

			// note number and frequency
			// 0					= 8.18Hz
			// 12					= 16.4Hz
			// 24					= 32.7Hz
			// 36					= 65.4Hz
			// 48					= 131Hz
			// 60					= 263Hz center C
			// 69					= 440Hz center A
			// 72					= 523Hz


			BackgroundWorker worker = sender as BackgroundWorker;
			worker.ReportProgress(-1);
			// plus
			if (targetType == 1) {
				for (int i = 0; i < dataCount; i++) {
					targetData[i] = (waveData[0, i] + waveData[1, i]) / 2;
				}
			}
			// diff
			else if (targetType == 2) {
				for (int i = 0; i < dataCount; i++) {
					targetData[i] = (waveData[0, i] - waveData[1, i]);
				}
			}
			// L
			else if (targetType == 3) {
				for (int i = 0; i < dataCount; i++) {
					targetData[i] = waveData[0, i];
				}
			}
			// R
			else if (targetType == 4) {
				for (int i = 0; i < dataCount; i++) {
					targetData[i] = waveData[1, i];
				}
			}
			// mixed
			else {
				//(targetType == 0)
				for (int i = 0; i < dataCount; i++) {
					if (channels == 1) targetData[i] = waveData[0, i];
					else targetData[i] = waveData[0, i] * waveData[1, i];
				}
			}
			reverseBitArray = BitScrollArray(blockSize);
			if (useParallel) {
				cpuCount = Environment.ProcessorCount;
				Parallel.For(0, cpuCount, analyzeBlockParallel);
			} else {
				cpuCount = 1;
				analyzeBlockParallel(0);
			}

			/*
			StreamWriter sw = new StreamWriter("spectrum.txt");
			for (int j = 0; j < 128; j++) {
				for (int i = 0; i < blockCount; i++) {
					sw.Write(noteSpectrums[i, j].ToString("G3") + " ");
				}
				sw.WriteLine();
			}
			 */

		}
		private void analyzeBlockParallel(int id) {
			int startBlock = (blockCount/cpuCount)*id;
			if (id < blockCount % cpuCount) startBlock += id;
			else startBlock += blockCount % cpuCount;
			int endBlock = startBlock + (blockCount / cpuCount) +(id<blockCount%cpuCount?1:0);

			double[] outputRe = new double[blockSize];
			double[] outputIm = new double[blockSize];
			for (int cur = startBlock; cur < endBlock; cur++) {
				int start = cur * shiftSize;
				getSpectrum(outputRe,outputIm,blockBit,start,cur);
				processedCount++;
				worker.ReportProgress(processedCount);
				if (worker.CancellationPending) {
					break;
				}
			}
		}

		int[] reverseBitArray;
		private void getSpectrum(double[] outputRe,double[] outputIm,int bitSize,int start,int blockId) {
			// rectangular
			if (windowType == 1) {
				for (int i = 0; i < blockSize; i++) {
					int cur = reverseBitArray[i];
					outputRe[i] = targetData[start + cur];
					outputIm[i] = 0;
				}
			}
			// hann
			else if (windowType == 2) {
				for (int i = 0; i < blockSize; i++) {
					int cur = reverseBitArray[i];
					outputRe[i] = targetData[start + cur] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * cur / blockSize));
					outputIm[i] = 0;
				}
			}
			// hamming
			else {
				for (int i = 0; i < blockSize; i++) {
					int cur = reverseBitArray[i];
					outputRe[i] = targetData[start + cur] * (0.54 - 0.46 * Math.Cos(2 * Math.PI * cur / blockSize));
					outputIm[i] = 0;
				}
			}
			for (int stage = 1; stage <= bitSize; stage++) {
				int butterflyDistance = 1 << stage;
				int numType = butterflyDistance >> 1;
				int butterflySize = butterflyDistance >> 1;

				double wRe = 1.0;
				double wIm = 0.0;
				double uRe = Math.Cos(Math.PI / butterflySize);
				double uIm = -Math.Sin(Math.PI / butterflySize);

				for (int type = 0; type < numType; type++) {
					for (int j = type; j < blockSize; j += butterflyDistance) {
						int jp = j + butterflySize;
						double tempRe = outputRe[jp] * wRe - outputIm[jp] * wIm;
						double tempIm = outputRe[jp] * wIm + outputIm[jp] * wRe;
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
			for (int i = 0; i < blockSize/2; i++) {
				outputRe[i] = Math.Sqrt(outputRe[i]*outputRe[i]+outputIm[i]*outputIm[i])/blockSize;
			}
	
			int specCur = 1;
			int note = 0;
			double currentFreq = 440 * Math.Pow(2, (note - 69) / 12.0);
			while (note<128 && specCur<blockSize/2) {
				while (specCur * baseFreq > currentFreq && note<128) {
					noteSpectrums[blockId,note] =outputRe[specCur - 1] + (outputRe[specCur] - outputRe[specCur - 1]) / baseFreq * (currentFreq - (specCur-1)*baseFreq);
					note++;
					currentFreq = 440 * Math.Pow(2, (note - 69) / 12.0);
				}
				specCur++;
			}
/*			if (blockId == 180) {
				StreamWriter sw = new StreamWriter("150.txt");
				for (int i = 0; i < blockSize/2; i++) {
					sw.WriteLine("{0} {1}",i*baseFreq,outputRe[i]);
				}
				sw.Close();
				sw = new StreamWriter("note.txt");
				for (int i = 0; i < 128; i++) {
					sw.WriteLine("{0} {1}",i,440 * Math.Pow(2, (i - 69) / 12.0));
				}
				sw.Close();
			}*/
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
