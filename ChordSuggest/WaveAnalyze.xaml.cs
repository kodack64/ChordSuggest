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



	/// <summary>
	/// WaveAnalyze.xaml の相互作用ロジック
	/// </summary>
	public partial class WaveAnalyze : Window,INotifyPropertyChanged {
		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged(string propertyName) {
			PropertyChangedEventHandler handler = this.PropertyChanged;
			if (handler != null)
				handler(this, new PropertyChangedEventArgs(propertyName));
		}
		private string myTargetFile;
		public WaveAnalyze(string file) {
			myTargetFile = file;
			InitializeComponent();
			this.Dispatcher.InvokeAsync(
				new Action(() => {
					initialize();
				})
			);
		}

		// file info
		int channels;
		int sampleRate;
		int bitDepth;
		int dataCount;
		double[,] waveData;
		double[,] targetData;
		BackgroundWorker fileLoadWorker;
	
		// fft settings
		int blockBitSetting_;
		public int blockBitSetting {
			set {
				blockBitSetting_ = value;
				OnPropertyChanged("blockBitSetting");
				updateEstimation();
			}
			get {
				return blockBitSetting_;
			}
		}
		int shiftBitSetting_;
		public int shiftBitSetting {
			set {
				shiftBitSetting_ = value;
				OnPropertyChanged("shiftBitSetting");
				updateEstimation();
			}
			get {
				return shiftBitSetting_;
			}
		}
		public void Callback_TargetArraySelectionChanged(object sender, EventArgs arg) {
			updateEstimation();
		}
		public void updateEstimation() {
			Label_BlockLength.Content = String.Format("{0}samples, {1:G3}, {2:G3}Hz resolution (cannot distinguish note lower than {3})",
				1 << blockBitSetting_,
				(1.0 * (1 << blockBitSetting_) / sampleRate < 1.0) ? (1.0 * (1 << blockBitSetting_) / sampleRate * 1e3).ToString("G3") + "ms" : (1.0 * (1 << blockBitSetting_) / sampleRate).ToString("G3") + "sec",
				1.0 * sampleRate / (1 << blockBitSetting_),
				getDivisibleNote(1.0 * sampleRate / (1 << blockBitSetting_))
				);
			Label_ShiftLength.Content = String.Format("{0} samples, {1} , {2} blocks",
				1 << shiftBitSetting_,
				(1.0 * (1 << shiftBitSetting_) / sampleRate < 1.0) ? (1.0 * (1 << shiftBitSetting_) / sampleRate * 1e3).ToString("G3") + "ms" : (1.0 * (1 << shiftBitSetting_) / sampleRate).ToString("G3") + "sec",
				Math.Max((dataCount - (1 << blockBitSetting_)) / (1 << shiftBitSetting_) * (ComboBox_TargetArray.SelectedIndex == 0 ? channels : 1), 0.0)
				);
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
		int[] reverseBitArray;
		BackgroundWorker FFTWorker;

		// fft result
		double[,] noteSpectrums;
		double minimumSpectrum;
		double maximumSpectrum;
	
		// image convert settings
		BackgroundWorker imageConvertWorker;
		int xScale_;
		int xScale {
			set {
				xScale_ = Math.Max(value,1);
				Image_Spectrum.Width = xScale * blockCount;
			}
			get {
				return xScale_;
			}
		}
		int yScale_;
		int yScale {
			set {
				yScale_ = Math.Max(value,1);
				Canvas_Keyboard.Height = yScale_ * 128;
				for (int i = 0; i < keyboardList.Count; i++) {
					keyboardList[i].Height = yScale_;
					Canvas.SetTop(keyboardList[i],yScale_*(127-i));
				}
				Image_Spectrum.Height = yScale * 128;
			}
			get {
				return yScale_;
			}
		}
		List<Rectangle> keyboardList = new List<Rectangle>();
		PixelFormat pf;
		double thresholdPower = 0.01;
		double amplitude = 3;

		// image convert result
		byte[] pixels;
		int rawStride;

		//timer
		Stopwatch releaseTimer = new Stopwatch();
		double releaseTime;
		int lastRelease;

		private void initialize() {
			Label_FileName.Content = myTargetFile;

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

			Canvas_Keyboard.Height = 128 * yScale;
			for (int note = 0; note < 128; note++) {
				Rectangle rect = new Rectangle();
				rect.Width = 50;
				rect.Height = yScale;
				if (ChordBasic.isWhiteKey(note)) {
					rect.Stroke = Brushes.Black;
				} else {
					rect.Fill = Brushes.Black;
				}
				rect.StrokeThickness = 1;
				Canvas.SetLeft(rect, 0);
				Canvas.SetTop(rect, (127 - note) * yScale);
				Canvas_Keyboard.Children.Add(rect);
				keyboardList.Add(rect);
			}
			releaseTime = 1.0;

			fileLoadWorker = new BackgroundWorker();
			fileLoadWorker.WorkerReportsProgress = true;
			fileLoadWorker.WorkerSupportsCancellation = true;
			fileLoadWorker.DoWork += new DoWorkEventHandler(doFileLoad);
			fileLoadWorker.ProgressChanged += new ProgressChangedEventHandler(progressChangedFileLoad);
			fileLoadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(fileLoadCompleted);
			fileLoadWorker.RunWorkerAsync();
		}
		private void doFileLoad(object sender, DoWorkEventArgs arg) {

			// read files
			fileLoadWorker.ReportProgress(0);
			BinaryReader reader = new BinaryReader(File.Open(myTargetFile, FileMode.Open));
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
			byte[] byteArray;
			byteArray = new byte[dataSize];
			byteArray = reader.ReadBytes(dataSize);
			reader.Close();


			// convert to double
			int loadProgress = 0;
			int byteDepth = bitDepth / 8;
			dataCount = dataSize / byteDepth / channels;
			waveData = new double[channels, dataCount];
			int cur = 0;
			for (int i = 0; i < dataCount; i++) {
				for (int c = 0; c < channels; c++) {
					if (byteDepth == 1) {
						waveData[c, i] = byteArray[cur] / 128.0 - 1.0;
					} else if (byteDepth == 2) {
						waveData[c, i] = ((Int16)(byteArray[cur] + 256 * byteArray[cur + 1])) / 32768.0;
					} else if (byteDepth == 3) {
						double temp = ((Int32)(byteArray[cur] + 256 * byteArray[cur + 1] + 65536 * byteArray[cur + 2])) / 8388608.0;
						waveData[c, i] = temp < 1.0 ? temp : temp - 2.0;
					} else if (byteDepth == 4) {
						waveData[c, i] = BitConverter.ToSingle(byteArray, cur);
					}
					cur += byteDepth;
				}
				if (i * 100 / dataCount >= loadProgress) {
					loadProgress++;
					fileLoadWorker.ReportProgress(loadProgress);
				}
			}
		}
		private void progressChangedFileLoad(object sender, ProgressChangedEventArgs arg) {
			int value = arg.ProgressPercentage;
			if (value == 0) {
				lastRelease = 0;
				Label_FileLoadProgress.Content = String.Format("File Load...", value);
				releaseTimer.Start();
			} else {
				if (releaseTimer.ElapsedMilliseconds * 1e-3 > releaseTime) {
					double restTime = (releaseTimer.ElapsedMilliseconds * 1e-3 / (value - lastRelease)) * (100 - value);
					Label_FileLoadProgress.Content = String.Format("File Convert : {0}%", value);
					releaseTimer.Reset();
					releaseTimer.Start();
					lastRelease = value;
				}
			}
			ProgressBar_FileLoad.Value = value;
		}
		private void fileLoadCompleted(object sender, RunWorkerCompletedEventArgs arg) {
			Label_FileLoadProgress.Content = String.Format("File loaded");

			double time = dataCount / sampleRate;
			string str;
			if (time >= 3600) str = String.Format("{0}h {1}m {2}s", ((int)time) / 3600, (((int)time) % 3600) / 60, (((int)time) % 60));
			else if (time >= 60) str = String.Format("{0}m {1}s", (((int)time) % 3600) / 60, (((int)time) % 60));
			else str = time.ToString("G3") + "s";
			Label_PlayTime.Content = str;
			Label_ChannelCount.Content = channels.ToString() + " ch";
			Label_SampleCount.Content = dataCount.ToString() + " samples";
			Label_SamplingRate.Content = sampleRate.ToString() + " Hz";
			Label_BitDepth.Content = bitDepth.ToString() + " bit";

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
			Button_StartWork.IsEnabled = true;
			updateEstimation();
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

//			xScale = Math.Max(((1 << shiftBit) / sampleRate) * 100, 5);
//			yScale = 10;
			xScale = 1;
			yScale = 1;

			ProgressBar_FFT.Maximum = blockCount;
			ProgressBar_FFT.Minimum = 0;
			ProgressBar_FFT.Value = 0;
			ProgressBar_ImageConvert.Maximum = blockCount;
			ProgressBar_ImageConvert.Minimum = 0;
			ProgressBar_ImageConvert.Value = 0;

			Label_FFTProgress.Content = "Preparing data";

			windowType = ComboBox_WindowFunction.SelectedIndex;
			targetType = ComboBox_TargetArray.SelectedIndex;

			processedCount = 0;

			FFTWorker = new BackgroundWorker();
			FFTWorker.WorkerReportsProgress = true;
			FFTWorker.WorkerSupportsCancellation = true;
			FFTWorker.DoWork += new DoWorkEventHandler(doFFTAnalyze);
			FFTWorker.ProgressChanged += new ProgressChangedEventHandler(progressChangedFFT);
			FFTWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(FFTCompleted);
			FFTWorker.RunWorkerAsync();
		}
		private void Callback_WorkerCancel(object sender, RoutedEventArgs arg) {
			FFTWorker.CancelAsync();
			Button_CancelWork.IsEnabled = false;
			workIsCancelled = true;
		}
		private void progressChangedFFT(object sender, ProgressChangedEventArgs arg) {
			int value = arg.ProgressPercentage;
			if (value == 0) {
				lastRelease = 0;
				Label_FFTProgress.Content = String.Format("FFT : Block {0}/{1}", value, blockCount*totalTargetChannel);
				ProgressBar_FFT.Maximum = blockCount*totalTargetChannel;
				releaseTimer.Start();
			} else {
				if (releaseTimer.ElapsedMilliseconds*1e-3 > releaseTime) {
					double restTime = (releaseTimer.ElapsedMilliseconds * 1e-3 / (value - lastRelease)) * (blockCount * totalTargetChannel - value);
					Label_FFTProgress.Content = String.Format("FFT : Block {0}/{1}, {2:0.0}sec to complete", value, blockCount * totalTargetChannel, restTime);
					releaseTimer.Reset();
					releaseTimer.Start();
					lastRelease = value;
				}
			}
			ProgressBar_FFT.Value = value;
		}


		private int currentTargetChannel;
		private int totalTargetChannel;
		private void doFFTAnalyze(object sender, DoWorkEventArgs arg) {
			BackgroundWorker FFTWorker = sender as BackgroundWorker;
			FFTWorker.ReportProgress(0);
			// plus
			if (targetType == 1) {
				totalTargetChannel = 1;
				targetData = new double[1,dataCount];
				for (int i = 0; i < dataCount; i++) targetData[0,i] = (waveData[0, i] + waveData[1, i]) / 2;
			}
			// diff
			else if (targetType == 2) {
				totalTargetChannel = 1;
				targetData = new double[1, dataCount];
				for (int i = 0; i < dataCount; i++) targetData[0,i] = (waveData[0, i] - waveData[1, i]);
			}
			// L
			else if (targetType == 3) {
				totalTargetChannel = 1;
				targetData = new double[1, dataCount];
				for (int i = 0; i < dataCount; i++) targetData[0,i] = waveData[0, i];
			}
			// R
			else if (targetType == 4) {
				totalTargetChannel = 1;
				targetData = new double[1, dataCount];
				for (int i = 0; i < dataCount; i++) targetData[0,i] = waveData[1, i];
			}
			// mixed
			else {
				//(targetType == 0)
				totalTargetChannel = channels;
				targetData = new double[channels, dataCount];
				for (int ch = 0; ch < channels; ch++) {
					for (int i = 0; i < dataCount; i++) {
						targetData[ch, i] = waveData[ch, i];
					}
				}
			}
/*			for (int i = targetData.Length - 1; i >= 1; i--) {
				targetData[i] = targetData[i] - 0.97 * targetData[i - 1];
			}*/

			reverseBitArray = new int[blockSize];
			int halfBlockSize = blockSize >> 1;
			reverseBitArray[0] = 0;
			for (int i = 1; i < blockSize; i <<= 1) {
				for (int j = 0; j < i; j++)
					reverseBitArray[j + i] = reverseBitArray[j] + halfBlockSize;
				halfBlockSize >>= 1;
			}


			for (currentTargetChannel = 0; currentTargetChannel < totalTargetChannel; currentTargetChannel++) {
				if (useParallel) {
					cpuCount = Environment.ProcessorCount;
					Parallel.For(0, cpuCount, analyzeBlockParallel);
				} else {
					cpuCount = 1;
					analyzeBlockParallel(0);
				}
			}
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
				FFTWorker.ReportProgress(processedCount);
				if (FFTWorker.CancellationPending) {
					break;
				}
			}
		}

		private void getSpectrum(double[] outputRe,double[] outputIm,int bitSize,int start,int blockId) {
			// rectangular
			if (windowType == 1) {
				for (int i = 0; i < blockSize; i++) {
					int cur = reverseBitArray[i];
					outputRe[i] = targetData[currentTargetChannel,start + cur];
					outputIm[i] = 0;
				}
			}
			// hann
			else if (windowType == 2) {
				for (int i = 0; i < blockSize; i++) {
					int cur = reverseBitArray[i];
					outputRe[i] = targetData[currentTargetChannel, start + cur] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * cur / blockSize));
					outputIm[i] = 0;
				}
			}
			// hamming
			else {
				for (int i = 0; i < blockSize; i++) {
					int cur = reverseBitArray[i];
					outputRe[i] = targetData[currentTargetChannel, start + cur] * (0.54 - 0.46 * Math.Cos(2 * Math.PI * cur / blockSize));
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
					noteSpectrums[blockId,note] +=outputRe[specCur - 1] + (outputRe[specCur] - outputRe[specCur - 1]) / baseFreq * (currentFreq - (specCur-1)*baseFreq);
					note++;
					currentFreq = 440 * Math.Pow(2, (note - 69) / 12.0);
				}
				specCur++;
			}
		}
		private void FFTCompleted(object sender, RunWorkerCompletedEventArgs arg) {
			if (arg.Error != null) {
				Label_FFTProgress.Content = "処理中に例外が起きました";
			} else if (workIsCancelled) {
				Label_FFTProgress.Content = String.Format("FFT Cancelled {0}/{1}", processedCount, blockCount);
			} else {
				Label_FFTProgress.Content = String.Format("FFT completed", blockCount);
				ProgressBar_FFT.Value = ProgressBar_FFT.Maximum;

				imageConvertWorker = new BackgroundWorker();
				imageConvertWorker.WorkerReportsProgress = true;
				imageConvertWorker.WorkerSupportsCancellation = true;
				imageConvertWorker.DoWork += new DoWorkEventHandler(doImageConvert);
				imageConvertWorker.ProgressChanged += new ProgressChangedEventHandler(progressChangedImageConvert);
				imageConvertWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(imageConvertCompleted);
				imageConvertWorker.RunWorkerAsync();
			}
			Button_StartWork.IsEnabled = true;
			Button_CancelWork.IsEnabled = false;
		}
		private void doImageConvert(object sender, DoWorkEventArgs arg) {
			imageConvertWorker.ReportProgress(0);
			minimumSpectrum = double.MaxValue;
			maximumSpectrum = double.MinValue;
			for (int note = 0; note < 128; note++) {
				for (int block = 0; block < blockCount; block++) {
					minimumSpectrum = Math.Min(minimumSpectrum, noteSpectrums[block, note]);
					maximumSpectrum = Math.Max(maximumSpectrum, noteSpectrums[block, note]);
				}
			}
			pf = PixelFormats.Rgb24;
			rawStride = (blockCount*pf.BitsPerPixel+7)/8;
			pixels = new byte[128 * rawStride];

			for (int block = 0; block < blockCount; block++) {
				for (int note = 0; note < 128; note++) {
					double power = noteSpectrums[block, note];
					power = Math.Log(1 + (power - minimumSpectrum) / (maximumSpectrum - minimumSpectrum)) / Math.Log(2.0);
					power = power < thresholdPower ? 0 : Math.Min(power * amplitude, 1.0);
					pixels[3 * block + (127 - note) * rawStride] = (byte)Math.Max(0, 0xff * (1.0 - Math.Abs(3.0 - power * 3.0)));
					pixels[3 * block + (127 - note) * rawStride + 1] = (byte)Math.Max(0, 0xff * (1.0 - Math.Abs(2.0 - power * 3.0)));
					pixels[3 * block + (127 - note) * rawStride + 2] = (byte)Math.Max(0, 0xff * (1.0 - Math.Abs(1.0 - power * 3.0)));
				}
				imageConvertWorker.ReportProgress(block);
			}
			imageConvertWorker.ReportProgress(blockCount);
		}
		private void progressChangedImageConvert(object sender, ProgressChangedEventArgs arg) {
			int value = arg.ProgressPercentage;
			if (value == 0) {
				lastRelease = 0;
				Label_ImageConvertProgress.Content = String.Format("Preparing data", value, blockCount);
				releaseTimer.Start();
			} else if(value==blockCount){
				releaseTimer.Stop();
				Label_ImageConvertProgress.Content = String.Format("Rendering", value, blockCount);
			} else {
				if (releaseTimer.ElapsedMilliseconds*1e-3 > releaseTime) {
					double restTime = (releaseTimer.ElapsedMilliseconds * 1e-3/(value-lastRelease)) * (blockCount - value);
					Label_ImageConvertProgress.Content = String.Format("Converting to image : Block {0}/{1}, {2:0.0}sec to complete", value, blockCount, restTime);
					releaseTimer.Reset();
					releaseTimer.Start();
					lastRelease = value;
				}
			}
			ProgressBar_ImageConvert.Value = value;
		}
		private void imageConvertCompleted(object sender, RunWorkerCompletedEventArgs arg) {

			Image_Spectrum.Stretch = Stretch.Fill;
			Image_Spectrum.Source = BitmapSource.Create(blockCount,128,96,96,pf,null,pixels,rawStride);

			yScale = 7;
			xScale = 10;
			Image_Spectrum.Width = blockCount * xScale;
			Image_Spectrum.Height = 128 * yScale;

			Tab_Spectrum.IsEnabled = true;
			MyTabControl.SelectedIndex = 0;
			Label_ImageConvertProgress.Content = String.Format("Image convert completed", blockCount);
		}

		private void Callback_Scrolled(object sender,EventArgs arg) {
			var sv = sender as ScrollViewer;
			Scroll_Keyboard.ScrollToVerticalOffset(sv.VerticalOffset);
		}
		private void Callback_XScaleUp(object sender, EventArgs arg) {
			xScale++;
		}
		private void Callback_XScaleDown(object sender, EventArgs arg) {
			xScale--;
		}
		private void Callback_YScaleUp(object sender, EventArgs arg) {
			yScale++;
		}
		private void Callback_YScaleDown(object sender, EventArgs arg) {
			yScale--;
		}
		private void Callback_ThresholdChanged(object sender, EventArgs arg) {
			thresholdPower = Slider_Threshold.Value / 100.0;
			repaintImage();
		}
		private void Callback_AmplitudeChanged(object sender, EventArgs arg) {
			amplitude = Slider_Amplitude.Value / 10.0;
			repaintImage();
		}
		private void repaintImage() {
			if (Tab_Spectrum.IsEnabled) {
				Stopwatch rep = new Stopwatch();
				rep.Start();
				for (int block = 0; block < blockCount; block++) {
					for (int note = 0; note < 128; note++) {
						double power = noteSpectrums[block, note];
						power = Math.Log(1 + (power - minimumSpectrum) / (maximumSpectrum - minimumSpectrum)) / Math.Log(2.0);
						power = power < thresholdPower ? 0 : Math.Min(power * amplitude, 1.0);
						pixels[3 * block + (127 - note) * rawStride] = (byte)Math.Max(0, 0xff * (1.0 - Math.Abs(3.0 - power * 3.0)));
						pixels[3 * block + (127 - note) * rawStride + 1] = (byte)Math.Max(0, 0xff * (1.0 - Math.Abs(2.0 - power * 3.0)));
						pixels[3 * block + (127 - note) * rawStride + 2] = (byte)Math.Max(0, 0xff * (1.0 - Math.Abs(1.0 - power * 3.0)));
					}
				}
				Image_Spectrum.Source = BitmapSource.Create(blockCount, 128, 96, 96, pf, null, pixels, rawStride);
//				Console.WriteLine(rep.ElapsedMilliseconds.ToString());
			}
		}
	}
}
