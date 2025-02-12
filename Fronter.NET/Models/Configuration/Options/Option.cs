﻿using commonItems;
using System.Collections.Generic;

namespace Fronter.Models.Configuration.Options;

public class Option {
	public Option(BufferedReader reader, int id) {
		var parser = new Parser();
		RegisterKeys(parser);
		parser.ParseStream(reader);
	}
	private void RegisterKeys(Parser parser) {
		parser.RegisterKeyword("name", reader => Name = reader.GetString());
		parser.RegisterKeyword("tooltip", reader => Tooltip = reader.GetString());
		parser.RegisterKeyword("displayName", reader => DisplayName = reader.GetString());
		parser.RegisterKeyword("radioSelector", reader => {
			RadioSelector = new RadioSelector(reader);
		});
		parser.RegisterKeyword("textSelector", reader => {
			TextSelector = new TextSelector(reader);
		});
		parser.RegisterKeyword("checkBoxSelector", reader => {
			CheckBoxSelector = new CheckBoxSelector(reader);
		});
		parser.RegisterRegex(CommonRegexes.Catchall, ParserHelpers.IgnoreAndLogItem);
	}

	public void SetRadioSelectorValue(string selection) {
		if (RadioSelector is null) {
			Logger.Warn("Attempted setting a radio control in unknown radio option!");
			return;
		}
		RadioSelector.SetSelectedValue(selection);
	}

	public void SetCheckBoxSelectorValue(ISet<string> selection) {
		if (CheckBoxSelector is null) {
			Logger.Warn("Attempted setting a checkbox control in unknown checkbox option!");
			return;
		}
		CheckBoxSelector.SetSelectedValues(selection);
	}

	public void SetRadioSelectorId(int selection) {
		if (RadioSelector is null) {
			Logger.Warn("Attempted setting a radio control in unknown radio option!");
			return;
		}
		RadioSelector.SetSelectedId(selection);
	}

	public void SetCheckBoxSelectorIds(ISet<int> selection) {
		if (CheckBoxSelector is null) {
			Logger.Warn("Attempted setting a checkbox control in unknown checkbox option!");
			return;
		}
		CheckBoxSelector.SetSelectedIds(selection);
	}

	public void SetTextSelectorValue(string selection) {
		if (TextSelector is null) {
			Logger.Warn("Attempted setting a text control which does not exist!");
			return;
		}
		TextSelector.Value = selection;
	}

	public string GetValue() {
		if (RadioSelector is not null) {
			return RadioSelector.GetSelectedValue();
		}

		return TextSelector is not null ? TextSelector.Value : string.Empty;
	}

	public HashSet<string> GetValues() {
		return CheckBoxSelector is not null ? CheckBoxSelector.GetSelectedValues() : new HashSet<string>();
	}

	public void SetValue(string selection) {
		if (TextSelector is not null) {
			TextSelector.Value = selection;
		}

		RadioSelector?.SetSelectedValue(selection);
	}

	public void SetValue(ISet<string> selection) {
		CheckBoxSelector?.SetSelectedValues(selection);
	}

	public bool IsCheckBoxSelectorPreloaded() {
		return CheckBoxSelector?.Preloaded == true;
	}

	public void SetCheckBoxSelectorPreloaded() {
		if (CheckBoxSelector is null) {
			return;
		}

		CheckBoxSelector.Preloaded = true;
	}

	public int Id { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string DisplayName { get; private set; } = string.Empty;
	public string Tooltip { get; private set; } = string.Empty;
	public RadioSelector? RadioSelector { get; private set; }
	public TextSelector? TextSelector { get; private set; }
	public CheckBoxSelector? CheckBoxSelector { get; private set; }
}