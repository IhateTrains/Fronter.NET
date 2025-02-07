﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using commonItems;
using FluentAvalonia.Styling;
using Fronter.Extensions;
using Fronter.LogAppenders;
using Fronter.Models;
using Fronter.Models.Configuration;
using Fronter.Services;
using Fronter.Views;
using log4net;
using log4net.Core;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;
using ReactiveUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;

namespace Fronter.ViewModels;

public class MainWindowViewModel : ViewModelBase {
	private readonly TranslationSource loc = TranslationSource.Instance;
	public IEnumerable<KeyValuePair<string, string>> Languages => loc.LoadedLanguages
		.ToDictionary(l => l, l => loc.TranslateLanguage(l));

	public Configuration Config { get; }

	public PathPickerViewModel PathPicker { get; }
	public OptionsViewModel Options { get; }

	public Level LogFilterLevel {
		get => LogGridAppender.LogFilterLevel;
		private set => this.RaiseAndSetIfChanged(ref LogGridAppender.LogFilterLevel, value);
	}

	private string saveStatus = "CONVERTSTATUSPRE";
	private string convertStatus = "CONVERTSTATUSPRE";
	private string copyStatus = "CONVERTSTATUSPRE";

	public string SaveStatus {
		get => saveStatus;
		set => this.RaiseAndSetIfChanged(ref saveStatus, value);
	}
	public string ConvertStatus {
		get => convertStatus;
		set => this.RaiseAndSetIfChanged(ref convertStatus, value);
	}
	public string CopyStatus {
		get => copyStatus;
		set => this.RaiseAndSetIfChanged(ref copyStatus, value);
	}


	public MainWindowViewModel() {
		Config = new Configuration();

		var appenders = LogManager.GetRepository().GetAppenders();
		var gridAppender = appenders.First(a => a.Name == "grid");
		if (gridAppender is not LogGridAppender logGridAppender) {
			throw new LogException($"Log appender \"{gridAppender.Name}\" is not a {typeof(LogGridAppender)}");
		}
		LogGridAppender = logGridAppender;
		LogGridAppender.LogGrid = MainWindow.Instance.FindControl<DataGrid>("LogGrid");

		PathPicker = new PathPickerViewModel(Config);
		Options = new OptionsViewModel(Config.Options);
	}

	public ReadOnlyObservableCollection<LogLine> FilteredLogLines => LogGridAppender.FilteredLogLines;
	public void ToggleLogFilterLevel(string value) {
		LogFilterLevel = LogManager.GetRepository().LevelMap[value];
		LogGridAppender.ToggleLogFilterLevel();
		this.RaisePropertyChanged(nameof(FilteredLogLines));
		Dispatcher.UIThread.Post(ScrollToLogEnd, DispatcherPriority.MinValue);
	}

	private ushort progress = 0;
	public ushort Progress {
		get => progress;
		set => this.RaiseAndSetIfChanged(ref progress, value);
	}

	private bool VerifyMandatoryPaths() {
		foreach (var folder in Config.RequiredFolders) {
			if (!folder.Mandatory || Directory.Exists(folder.Value)) {
				continue;
			}

			Logger.Error($"Mandatory folder {folder.Name} at {folder.Value} not found.");
			return false;
		}

		foreach (var file in Config.RequiredFiles.Where(file => file.Mandatory && !File.Exists(file.Value))) {
			Logger.Error($"Mandatory file {file.Name} at {file.Value} not found.");
			return false;
		}

		return true;
	}

	public void LaunchConverter() {
		Progress = 0;
		SaveStatus = "CONVERTSTATUSPRE";
		ConvertStatus = "CONVERTSTATUSPRE";
		CopyStatus = "CONVERTSTATUSPRE";

		if (!VerifyMandatoryPaths()) {
			return;
		}
		Config.ExportConfiguration();

		var converterLauncher = new ConverterLauncher(Config);
		bool success;
		var converterThread = new Thread(() => {
			ConvertStatus = "CONVERTSTATUSIN";
			success = converterLauncher.LaunchConverter();
			if (success) {
				ConvertStatus = "CONVERTSTATUSPOSTSUCCESS";
				var modCopier = new ModCopier(Config);
				bool copySuccess;
				var copyThread = new Thread(() => {
					CopyStatus = "CONVERTSTATUSIN";
					copySuccess = modCopier.CopyMod();
					CopyStatus = copySuccess ? "CONVERTSTATUSPOSTSUCCESS" : "CONVERTSTATUSPOSTFAIL";
				});
				copyThread.Start();
			} else {
				ConvertStatus = "CONVERTSTATUSPOSTFAIL";
			}
		});
		converterThread.Start();
	}

	public async void CheckForUpdates() {
		if (!Config.UpdateCheckerEnabled) {
			return;
		}

		if (!Config.CheckForUpdatesOnStartup) {
			return;
		}

		if (!UpdateChecker.IsUpdateAvailable("commit_id.txt", Config.PagesCommitIdUrl)) {
			return;
		}

		var info = UpdateChecker.GetLatestReleaseInfo(Config.Name);

		const string updateNow = "Update now";
		const string maybeLater = "Maybe later";
		var msgBody = UpdateChecker.GetUpdateMessageBody(loc.Translate("NEWVERSIONBODY"), info);
		var messageBoxWindow = MessageBoxManager
			.GetMessageBoxCustomWindow(new MessageBoxCustomParams {
				Icon = Icon.Info,
				ContentHeader = "An update is available!",
				ContentTitle = loc.Translate("NEWVERSIONTITLE"),
				ContentMessage = msgBody,
				Markdown = true,
				ButtonDefinitions = new[] {
					new ButtonDefinition {Name = updateNow, IsDefault = true},
					new ButtonDefinition {Name = maybeLater, IsCancel = true}
				},
			});
		var result = await messageBoxWindow.ShowDialog(MainWindow.Instance);
		if (result != updateNow) {
			return;
		}

		if (info.ZipUrl is not null) {
			UpdateChecker.StartUpdaterAndDie(info.ZipUrl, Config.ConverterFolder);
		} else {
			BrowserLauncher.Open(Config.ConverterReleaseForumThread);
			BrowserLauncher.Open(Config.LatestGitHubConverterReleaseUrl);
		}
	}

	public static void Exit() {
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
			desktop.Shutdown(0);
		}
	}

	public async void OpenAboutDialog() {
		var messageBoxWindow = MessageBoxManager
			.GetMessageBoxStandardWindow(new MessageBoxStandardParams {
				ContentTitle = TranslationSource.Instance["ABOUT_TITLE"],
				Icon = Icon.Info,
				ContentHeader = TranslationSource.Instance["ABOUT_HEADER"],
				ContentMessage = TranslationSource.Instance["ABOUT_BODY"],
				ButtonDefinitions = ButtonEnum.Ok,
				SizeToContent = SizeToContent.WidthAndHeight,
				MinHeight = 250,
				ShowInCenter = true,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			});
		await messageBoxWindow.ShowDialog(MainWindow.Instance);
	}

	public static void OpenPatreonPage() {
		BrowserLauncher.Open("https://www.patreon.com/ParadoxGameConverters");
	}

	public void SetLanguage(string languageKey) {
		loc.SaveLanguage(languageKey);
	}

	public void SetTheme(string themeName) {
		var theme = AvaloniaLocator.Current.GetService<FluentAvaloniaTheme>();
		if (theme is null) {
			return;
		}
		theme.RequestedTheme = themeName;
	}

	public LogGridAppender LogGridAppender { get; }

	private void ScrollToLogEnd() {
		LogGridAppender.ScrollToLogEnd();
	}
}