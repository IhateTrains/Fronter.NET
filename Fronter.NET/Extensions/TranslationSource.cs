﻿using commonItems;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Fronter.Extensions; 

// idea based on https://gist.github.com/jakubfijalkowski/0771bfbd26ce68456d3e
public class TranslationSource : ReactiveObject {
	private TranslationSource() {
		var languagesPath = Path.Combine("Resources", "languages.txt");
		if (!File.Exists(languagesPath)) {
			Logger.Error("No languages dictionary found!");
			return;
		}

		var languagesParser = new Parser();
		languagesParser.RegisterRegex(CommonRegexes.String, (langReader, langKey) => {
			languages.Add(langKey, CultureInfo.GetCultureInfo(langReader.GetString()));
			LoadedLanguages.Add(langKey);
		});
		languagesParser.ParseFile(languagesPath);
		
		LoadLanguages();

		var fronterLanguagePath = Path.Combine("Configuration", "fronter-language.txt");
		if (File.Exists(fronterLanguagePath)) {
			var parser = new Parser();
			parser.RegisterKeyword("language", reader => CurrentLanguage = reader.GetString());
			parser.ParseFile(fronterLanguagePath);
		}
	}

	public static TranslationSource Instance { get; } = new();

	public string Translate(string key) {
		string toReturn;

		if (translations.TryGetValue(key, out var dictionary)) {
			if (dictionary.TryGetValue(CurrentLanguage, out var text)) {
				toReturn = text;
			} else if (dictionary.TryGetValue("english", out var englishText)) {
				Logger.Debug($"{CurrentLanguage} localization not found for key {key}, using english one");
				toReturn = englishText;
			} else {
				Logger.Debug($"{CurrentLanguage} localization not found for key {key}");
				return string.Empty;
			}
		} else {
			return string.Empty;
		}

		toReturn = Regex.Replace(toReturn, @"\\n", Environment.NewLine);
		return toReturn;
	}

	public string TranslateLanguage(string language) {
		return !languages.ContainsKey(language) ? string.Empty : languages[language].NativeName;
	}

	public string this[string key] => Translate(key);

	public void SaveLanguage(string languageKey) {
		if (!LoadedLanguages.Contains(languageKey)) {
			return;
		}
		CurrentLanguage = languageKey;

		var langFilePath = Path.Combine("Configuration", "fronter-language.txt");
		using var fs = new FileStream(langFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
		using var writer = new StreamWriter(fs);
		writer.WriteLine($"language={languageKey}");
		writer.Close();
	}
	private void LoadLanguages() {
		var fileNames = SystemUtils.GetAllFilesInFolder("Configuration");

		foreach (var fileName in fileNames) {
			if (!fileName.EndsWith(".yml")) {
				continue;
			}

			var langFilePath = Path.Combine("Configuration", fileName);
			using var langFileStream = File.OpenRead(langFilePath);
			using var langFileReader = new StreamReader(langFileStream);

			var firstLine = langFileReader.ReadLine();
			if (firstLine?.IndexOf("l_", StringComparison.Ordinal) != 0) {
				Logger.Error($"{langFilePath} is not a localization file!");
				continue;
			}
			var pos = firstLine.IndexOf(':');
			if (pos == -1) {
				Logger.Error($"Invalid localization language: {firstLine}");
				continue;
			}
			var language = firstLine.Substring(2, pos - 2);

			while (!langFileReader.EndOfStream) {
				var line = langFileReader.ReadLine();
				if (line is null) {
					break;
				}

				pos = line.IndexOf(':');
				if (pos == -1) {
					continue;
				}
				var key = line[..pos].Trim();
				pos = line.IndexOf('\"');
				if (pos == -1) {
					Logger.Error($"Invalid localization line: {line}");
					continue;
				}
				var secpos = line.LastIndexOf('\"');
				if (secpos == -1) {
					Logger.Error($"Invalid localization line: {line}");
					continue;
				}
				var text = line.Substring(pos + 1, secpos - pos - 1);

				if (translations.TryGetValue(key, out var dictionary)) {
					dictionary[language] = text;
				} else {
					var newDict = new Dictionary<string, string> { [language] = text };
					translations.Add(key, newDict);
				}
			}
		}
	}

	public List<string> LoadedLanguages { get; } = new();
	private readonly Dictionary<string, CultureInfo> languages = new();
	private readonly Dictionary<string, Dictionary<string, string>> translations = new(); // key, <language, text>

	private string currentLanguage = "english";
	public string CurrentLanguage {
		get => currentLanguage;
		private set {
			currentLanguage = value;
			this.RaisePropertyChanged(nameof(CurrentLanguage));
			this.RaisePropertyChanged("Item");
		}
	}
}