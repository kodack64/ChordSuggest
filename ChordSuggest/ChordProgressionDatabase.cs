using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace ChordSuggest {

	class FileState{
		string name_;
		public string name{
			get {return name_;}
			set{name_ = value;}
		}
		bool isEnabled_;
		public bool isEnabled {
			get {return isEnabled_;}
			set {isEnabled_ = value;}			
		}
	}
	class ChordProgressionDatabase {
		private ObservableCollection<FileState> fileList = new ObservableCollection<FileState>();
		private static string fileSwitchName = "Resource/FileSwitch.txt";
		public static string folder = "Progression/";
		public void initialize() {
			refresh();
			loadFileSwitch();
		}
		public void refresh() {
			fileList.Clear();
			foreach (string file in Directory.GetFiles(folder, "*.txt")) {
				fileList.Add(new FileState() { name = file.Substring(folder.Length), isEnabled = false });
			}
			MainWindow.Write("\t"+fileList.Count.ToString()+" files found\n");
		}
		public void loadFileSwitch(){
			StreamReader sr = new StreamReader(fileSwitchName);
			while (!sr.EndOfStream) {
				string file = sr.ReadLine();
				foreach(var fs in fileList.Where(p => p.name==file)) fs.isEnabled=true;
			}
			sr.Close();
			MainWindow.Write("\t" + fileList.Count(p => p.isEnabled).ToString() + " files enabled\n");
		}
		public void saveFileSwitch() {
			try {
				StreamWriter sw = new StreamWriter(fileSwitchName);
				foreach (string str in enabledFileList) {
					sw.WriteLine(str);
				}
				sw.Close();
			} catch (Exception) {
				MainWindow.Write("***Error*** cannot save fileswitch\n");
			}
		}
		public ObservableCollection<FileState> fileStates {
			get {
				return fileList;
			}
		}
		public List<string> enabledFileList {
			get {
				List<string> rtn = new List<string>();
				foreach (FileState fs in fileList) {
					if (fs.isEnabled) rtn.Add(fs.name);
				}
				return rtn;
			}
		}
	}
}
