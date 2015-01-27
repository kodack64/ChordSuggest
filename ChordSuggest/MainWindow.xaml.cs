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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.IO;

namespace ChordSuggest {
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	/// 



	public partial class MainWindow : Window {

		public class ChordPad : TextBlock {
			public int x;
			public int y;
			public int rootId;
			public int harmonyId;
			public Chord chord;
			public string roleInMajor;
			public string roleInMinor;
			public ChordPad(int _x, int _y, Chord _chord) {
				x = _x;
				y = _y;
				chord = _chord;
				Text = chord.ToString();
				Margin = new Thickness(2);
				rootId = chord.root.id;
				harmonyId = getPadHarmonyId(chord.harmony);
				roleInMajor = "";
				roleInMinor = "";
			}
		}

		public static MainWindow myInstance;
		public static void Write(String str) {
			myInstance.Write_(str);
		}
		private void Write_(String str){
			DebugBox.Dispatcher.InvokeAsync(
				new Action(() => {
					DebugBox.AppendText(str);
					DebugBox.ScrollToEnd();
				})
			); 
		}


		public const int maxChannelCount = 16;
		ChordProgressionDatabase cpd = new ChordProgressionDatabase();
		ChordProgressionSuggest cps = new ChordProgressionSuggest();

		List<ChordPad> chordPadList = new List<ChordPad>();
		private Dictionary<long, int> harmonyToId = new Dictionary<long, int>();
		private List<Harmony> harmonies = new List<Harmony>();
	
		MidiManage mm = new MidiManage();
		public MainWindow() {
			InitializeComponent();
			myInstance = this;
			Write("Component initialized\n");

			Dispatcher.BeginInvoke(
			   new Action(() => {
				   initialize();
			   })
			);
		}

		public void initialize() {
			Write("Load chord infomation\n");
			ChordBasic.initialize();

			Write("Load pad infomation\n");
			loadPadInfo();

			Write("Load chord progression database\n");
			cpd.initialize();

			Write("Load chord progression suggest\n");
			cps.initialize();
			cps.read(cpd.enabledFileList);

			Write("Initialize midi devices\n");
			mm.initialize();

			this.MouseLeftButtonDown += new MouseButtonEventHandler(Callback_MouseDown);
			this.MouseLeftButtonUp += new MouseButtonEventHandler(Callback_MouseUp);

			if (mm.outDeviceList.Count > 0) ComboBox_outputDevice.SelectedIndex = 0;

			createScaleLabel();
			createTonePad();
			createChannelSelector();
			createProgramSelector();
			createKeySelector();

			// データバインド
			CheckBox_HoldMode.DataContext = this;
			fileDatabaseList.ItemsSource = cpd.fileStates;
			ComboBox_outputDevice.ItemsSource = mm.outDeviceList;
			ComboBox_Channel.DataContext = this;
			TextBlock_Program.DataContext = this;
			CheckBox_ignoreSlashChord.DataContext = cps;
			RadioButton_IsMajorCheck.DataContext = this;
		}


		// UI関連
		private List<Label> scaleLabels= new List<Label>();
		private void createScaleLabel() {
			for (int key = 0; key < ChordBasic.toneCount; key++) {
				ScalePanel.ColumnDefinitions.Add(new ColumnDefinition());
			}
			for (int key = 0; key < ChordBasic.toneCount; key++) {
				var panel = new DockPanel();
				Grid.SetColumn(panel, key);
				var border = new Border();
				border.BorderThickness = new Thickness(1);
				border.BorderBrush = new SolidColorBrush(Colors.Black);
				Label label = new Label() { Content = "I", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
				border.Child = label;
				scaleLabels.Add(label);
				panel.Children.Add(border);
				ScalePanel.Children.Add(panel);
			}
			makeScaleLabel();
		}
		private string[] RomanNumber = { "I", "II", "III", "IV", "V", "VI", "VII" };
		private int[] majorDiatonic = { 0, 2, 4, 5, 7, 9, 11 };
		private int[] minorDiatonic = { 0, 2, 3, 5, 7, 8, 11 };
		private void makeScaleLabel() {
			int scaleCount = 0;
			for (int key = 0; key < ChordBasic.toneCount; key++) {
				if(
					(isMajor && (Array.IndexOf(majorDiatonic,key)>=0)) ||
					(!isMajor && (Array.IndexOf(minorDiatonic, key) >= 0))
					) {
					scaleLabels[key].Content = RomanNumber[scaleCount];
					scaleCount++;
				} else {
					scaleLabels[key].Content = "";
				}
			}
		}
		private void createKeySelector() {
			for (int i = 0; i < ChordBasic.toneCount; i++) {
				ComboBox_ChooseKey.Items.Add(ChordBasic.getTone(i).name);
			}
			ComboBox_ChooseKey.SelectionChanged += new SelectionChangedEventHandler(Callback_KeyChanged);
			ComboBox_ChooseKey.SelectedIndex = 0;
		}
		private void createChannelSelector() {
			for (int i = 0; i < maxChannelCount; i++) {
				ComboBox_Channel.Items.Add((i + 1).ToString());
			}
		}
		private void loadPadInfo() {
			XmlReader xml;
			xml = XmlReader.Create(new StreamReader("Resource/ChordPad.xml"));
			while (xml.Read()) {
				if (xml.NodeType == XmlNodeType.Element && xml.Name == "pad") {
					int id;
					string notation;
					id = int.Parse(xml.GetAttribute("id"));
					notation = xml.GetAttribute("notation");
					var res = Harmony.createHarmony(notation);
					if (res != null) {
						harmonyToId.Add(res.uniqueId, harmonies.Count);
						harmonies.Add(res);
					}
				}
			}
			xml.Close();
			for (int x = 0; x < ChordBasic.toneCount; x++) {
				for (int y = 0; y < harmonies.Count; y++) {
					chordPadList.Add(new ChordPad(x, y, new Chord(ChordBasic.getTone(x), harmonies[y])));
				}
			}
			xml = XmlReader.Create(new StreamReader("Resource/ChordRole.xml"));
			while (xml.Read()) {
				if (xml.NodeType == XmlNodeType.Element && xml.Name == "role") {
					string roleChordString = xml.GetAttribute("chord");
					var chordPad = chordPadList.Find(m => m.chord.ToString()==roleChordString);
					if (chordPad != null) {
						if (xml.GetAttribute("scale") == "M") {
							chordPad.roleInMajor = xml.GetAttribute("name");
						} else {
							chordPad.roleInMinor = xml.GetAttribute("name");
						}
					}
				}
			}
			xml.Close();
			isMajor = true;
		}
		private void createTonePad() {
			for (int key = 0; key < ChordBasic.toneCount; key++) {
				ChordPadPanel.ColumnDefinitions.Add(new ColumnDefinition());
			}
			for (int chord = 0; chord < harmonyCount; chord++) {
				ChordPadPanel.RowDefinitions.Add(new RowDefinition());
			}
			foreach (var chordPad in chordPadList) {
				var panel = new DockPanel();
				Grid.SetColumn(panel, chordPad.x);
				Grid.SetRow(panel, chordPad.y);
				var border = new Border();
				border.BorderThickness = new Thickness(1);
				border.BorderBrush = new SolidColorBrush(Colors.Black);
				panel.MouseEnter += new MouseEventHandler(Callback_MouseEnter);
				panel.MouseLeave += new MouseEventHandler(Callback_MouseLeave);
				border.Child = chordPad;
				panel.Children.Add(border);
				ChordPadPanel.Children.Add(panel);
			}
			updateUIColorDefault();
		}
		public void Callback_MouseEnter(object sender, MouseEventArgs arg) {
			currentChord = ((ChordPad)((Border)((DockPanel)sender).Children[0]).Child).chord;
		}
		public void Callback_MouseLeave(object sender, MouseEventArgs arg) {
//			currentChord = null;
		}
		private Chord playingChord = null;
		private Chord currentChord = null;

	
		class ProgramMenuItem : MenuItem {
			public int programNumber;
		}
		private void createProgramSelector() {
			ContextMenu contextMenu = new ContextMenu();
			MenuItem menuItem=null;
			XmlReader reader = XmlReader.Create(new StreamReader("Resource/Program.xml"));
			while (reader.Read()) {
				if (reader.NodeType == XmlNodeType.Element && reader.Name == "kind") {
					menuItem = new MenuItem() { Header = reader.GetAttribute("name") };
					contextMenu.Items.Add(menuItem);
				}
				if (reader.NodeType == XmlNodeType.Element && reader.Name == "program") {
					if (menuItem != null) {
						string name = reader.GetAttribute("name");
						int programNumber = int.Parse(reader.GetAttribute("programNumber"));

						programList[programNumber] = name;
						ProgramMenuItem programItem = new ProgramMenuItem() { Header = name ,programNumber=programNumber};
						programItem.Click += new RoutedEventHandler(Callback_ProgramChange);
						menuItem.Items.Add(programItem);
					}
				}
			}
			reader.Close();
			TextBlock_Program.ContextMenu = contextMenu;
		}
		public void Callback_ProgramChange(object sender, RoutedEventArgs arg) {
			var programItem = sender as ProgramMenuItem;
			currentProgramNumber = programItem.programNumber;
			TextBlock_Program.Text = programName;
			mm.changeProgram(currentProgramNumber, channelNumber);
		}
		public void Callback_ProgramShow(object sender, MouseButtonEventArgs arg) {
			TextBlock_Program.ContextMenu.IsOpen = true;
		}

		// バインドするデータ
		bool holdMode_ = false;
		public bool holdMode {
			get { return holdMode_; }
			set { holdMode_ = value; noteOff(); }
		} 
		public int channelNumber_ = 0;
		public int channelNumber {
			get { return channelNumber_; }
			set { channelNumber_ = value; }
		}
		public Dictionary<int,string> programList = new Dictionary<int,string>();
		public int currentProgramNumber = 0;
		public string programName {
			get { return programList[currentProgramNumber]+" "; }
		}
		public bool isMajor { get; set; }


		// Callback Functions
		public void Callback_MouseDown(object sender, MouseButtonEventArgs arg) {
			if(TabControl.SelectedIndex==0)noteOn();
		}
		public void Callback_MouseUp(object sender, MouseButtonEventArgs arg) {
			if (TabControl.SelectedIndex == 0) if (!holdMode) noteOff();
		}
		private void Callback_NoteOffButtonClicked(object sender, MouseButtonEventArgs e) {
			currentChord = null;
			noteOff();
		}

		private void Callback_RefreshFileList(object sender, RoutedEventArgs e) {
			Write("Refresh file list\n");
			cpd.saveFileSwitch();
			cpd.refresh();
			cpd.loadFileSwitch();
		}
		private void Callback_ReloadDatabase(object sender, RoutedEventArgs e) {
			Write("Reload database\n");
			cps.read(cpd.enabledFileList);
			updateUIColorDefault();
		}
		private void Callback_DeviceChanged(object sender, SelectionChangedEventArgs e) {
			Write("Connect midi device\n");
			mm.open(ComboBox_outputDevice.SelectedIndex);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			mm.close();
			cpd.saveFileSwitch();
		}

		// 音声関連
		private void noteOn() {
			noteOff();
			playingChord = currentChord;
			if (playingChord != null) {
//				Write("On:" + playingChord.ToString() + "\n");
				mm.playChord(playingChord, 127, channelNumber,currentKey);
				cps.suggestNextChord(playingChord);
				updateUIColorSuggest();
			}
		}
		private void noteOff() {
			if (playingChord != null) {
//				Write("Off:" + playingChord.ToString() + "\n");
				mm.stopChord(playingChord, channelNumber,currentKey);
				updateUIColorDefault();
				playingChord = null;
			}
		}
		public void updateUIColorDefault() {
			foreach (var chordPad in chordPadList) {
				double weight = cps.getDefaultChordWeight(chordPad.rootId, chordPad.harmonyId);
				if (playingChord==null || cps.getSuggestChordWeight(chordPad.rootId, chordPad.harmonyId) > 0 || playingChord.Equals(chordPad.chord)) {
					chordPad.Background = getDefaultBrushColor(weight);
				}
			}
		}
		public void updateUIColorSuggest() {
			foreach (var chordPad in chordPadList) {
				if (playingChord.Equals(chordPad.chord)) chordPad.Background = getPlayingBrushColor();
				else{
					double weight = cps.getSuggestChordWeight(chordPad.rootId, chordPad.harmonyId);
					if(weight>0) chordPad.Background = getSuggestBrushColor(weight);
				}
			}
		}

		private void updateChordPadText() {
			currentKey = ComboBox_ChooseKey.SelectedIndex;
			foreach (var chordPad in chordPadList) {
				string newText;
				if (isMajor)	newText = chordPad.chord.ToString(currentKey) + "\n" + chordPad.roleInMajor;
				else	newText = chordPad.chord.ToStringMinor(currentKey) + "\n" + chordPad.roleInMinor;
				if (chordPad.Text != newText) chordPad.Text = newText;
			}
		}
		private void Callback_ChangeMajorMinor(object sender, RoutedEventArgs e) {
			updateChordPadText();
			makeScaleLabel();
		}
		public int currentKey = 0;
		public void Callback_KeyChanged(object sender, RoutedEventArgs arg) {
			updateChordPadText();
		}
		private SolidColorBrush getDefaultBrushColor(double weight) {
			byte maxDepth = 0xff / 2;
			byte val = (byte)(0xff - maxDepth * weight);
			return new SolidColorBrush(new Color() { R = val, G = val, B = val, A = 0xff });
		}
		private SolidColorBrush getSuggestBrushColor(double weight) {
			byte maxDepth = 0xff / 2;
			byte val = (byte)(0xff - maxDepth * weight);
			var c = Colors.Blue;
			return new SolidColorBrush(new Color() { R = val, G = val, B = 0xff, A = 0xff });
		}
		private SolidColorBrush getPlayingBrushColor() {
			return new SolidColorBrush(Colors.LightSalmon);
		}

		public static bool hasHarmony(Harmony harmony) {
			return myInstance.harmonyToId.ContainsKey(harmony.uniqueId);
		}
		public static int getPadHarmonyId(Harmony harmony) {
			return myInstance.harmonyToId[harmony.uniqueId];
		}
		public static int harmonyCount {
			get { return myInstance.harmonies.Count; }
		}

		private void Callback_ReadFromClipBoard(object sender, RoutedEventArgs e) {
			string clipboardText = Clipboard.GetText();
			TextBox_ForConvert.Text = clipboardText;
			convertProgression();
		}
		private void Callback_ReadFromTextBox(object sender, RoutedEventArgs e) {
			convertProgression();
		}
		private void convertProgression() {
			int transposeKey = 0;
			string str = TextBox_ForConvert.Text;
			str = removeTags(str);
			string[] lines = str.Split('\n');
			List<List<Chord>> chordProgressions = new List<List<Chord>>();
			for (int i = 0; i < lines.Length; i++) {
				string line = lines[i];
				string[] del = {"|"," ","\t","\r"};
				string[] items = line.Split(del, StringSplitOptions.RemoveEmptyEntries);
				string lastItem = "";
				List<Chord> progression = new List<Chord>();
				for (int j = 0; j < items.Length; j++) {
					string item = items[j];
					if(item=="%"){
						item = lastItem;
					}
					if (item.Contains("key") || item.Contains("Key")) {
						var matchKeys = ChordBasic.toneList.FindAll(m => item.Contains(m.name) || (m.subname.Length>0 && item.Contains(m.subname)));
						if (matchKeys.Count > 0) {

							int cur = 0;
							int maxlen = 0;
							for (int key = 0; key < matchKeys.Count; key++) {
								if (item.Contains(matchKeys[key].name)) {
									if (maxlen < matchKeys[key].name.Length) {
										maxlen = matchKeys[key].name.Length;
										cur = key;
									}
								}
								if (item.Contains(matchKeys[key].subname)) {
									if (maxlen < matchKeys[key].subname.Length) {
										maxlen = matchKeys[key].subname.Length;
										cur = key;
									}
								}
							}

							transposeKey = matchKeys[cur].id;
//							Write("Transpose to "+ matchKeys[0].name+" with "+item+"\n");
						}
					} else {
						var res = Chord.createChordFromChordName(item);
						if (res == null) {
//							Write(item + " cannot translate\n");
						} else {
							res.transpose((ChordBasic.toneCount-transposeKey)%ChordBasic.toneCount);
							progression.Add(res);
						}
					}
					lastItem = item;
				}
				if(progression.Count>0)chordProgressions.Add(progression);
			}
			Write("Text converted\n");

			string resultString = "";
			for (int i = 0; i < chordProgressions.Count; i++) {
				for (int j = 0; j < chordProgressions[i].Count; j++) {
					resultString += chordProgressions[i][j].ToString();
					if (j + 1 != chordProgressions[i].Count) resultString += "\t";
				}
				resultString += "\n";
			}
			TextBox_Converted.Text = resultString;
		}
		private string removeTags(string str) {
			string rtn = "";
			int tagStartCount = 0;
			int notagCount = 0;
			for (int i = 0; i < str.Length; i++) {
				if (str[i] == '<'){
					if (tagStartCount == 0) {
						rtn += str.Substring(i - notagCount, notagCount);
						notagCount = 0;
					}
					tagStartCount++;
				} else if (str[i] == '>') {
					tagStartCount--;
					if (tagStartCount < 0) tagStartCount = 0;
					if (tagStartCount == 0) {
						rtn += " ";
					}
				} else {
					if (tagStartCount == 0) notagCount++;
					if (i == str.Length - 1 && tagStartCount==0) {
						rtn += str.Substring(i - notagCount+1);
					}
				}
			}
			return rtn;
		}
		private void Callback_SaveChord(object sender, RoutedEventArgs e) {
			string fileName = "Progression/" + TextBox_FileName.Text;
			try {
				StreamWriter sw = new StreamWriter(fileName, true);
				sw.Write(TextBox_Converted.Text);
				sw.Close();
				Write("Chord progressions are saved to " + TextBox_FileName.Text+"\n");
			} catch (Exception) {
				Write("warning - cannot access "+fileName+"\n");
			}
		}
	}
}
