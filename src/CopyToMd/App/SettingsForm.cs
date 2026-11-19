using System.ComponentModel;
using CopyToMd.Configuration;
using CopyToMd.Conversion;
using CopyToMd.Target;

namespace CopyToMd.App;

public sealed class SettingsForm : Form
{
    private sealed class RuleRow
    {
        public string Name { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string TitlePattern { get; set; } = string.Empty;
        public string Extensions { get; set; } = string.Empty;
        public OutputMode Mode { get; set; } = OutputMode.PlainText;
    }

    private readonly AppSettings working;
    private readonly TextBox hotKeyBox = new() { Width = 260 };
    private readonly CheckBox winModifier = new() { Text = "包含 Win 键", AutoSize = true };
    private readonly Label hotKeyHint = new() { AutoSize = true, Text = "点击输入框后按下组合键，例如 Ctrl+Alt+Shift+V。" };
    private readonly CheckBox notifications = new() { Text = "成功粘贴后显示托盘通知", AutoSize = true };
    private readonly NumericUpDown maxCharacters = new() { Minimum = 1_000, Maximum = 5_000_000, Increment = 10_000, Width = 140 };
    private readonly ComboBox presets = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly PropertyGrid styleGrid = new() { Dock = DockStyle.Fill, HelpVisible = true, ToolbarVisible = true };
    private readonly DataGridView rulesGrid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    private readonly BindingList<RuleRow> ruleRows = [];

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings current)
    {
        working = Clone(current);
        Result = current;
        Text = "Copy to MD 设置";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 620);
        Size = new Size(820, 680);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildStyleTab());
        tabs.TabPages.Add(BuildRulesTab());

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(8) };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 90 };
        var save = new Button { Text = "保存并应用", Width = 110 };
        var reset = new Button { Text = "恢复默认", Width = 90 };
        save.Click += (_, _) => SaveAndClose();
        reset.Click += (_, _) => ResetDefaults();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        buttons.Controls.Add(reset);

        Controls.Add(tabs);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private TabPage BuildGeneralTab()
    {
        var page = new TabPage("常规");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), AutoScroll = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        hotKeyBox.Text = working.HotKey;
        hotKeyBox.KeyDown += CaptureHotKey;
        hotKeyBox.TextChanged += (_, _) => ValidateHotKey();
        winModifier.Checked = working.HotKey.Contains("Win", StringComparison.OrdinalIgnoreCase);
        winModifier.CheckedChanged += (_, _) => ValidateHotKey();

        layout.Controls.Add(new Label { Text = "智能粘贴快捷键", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        var hotKeyPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        hotKeyPanel.Controls.Add(hotKeyBox);
        hotKeyPanel.Controls.Add(winModifier);
        layout.Controls.Add(hotKeyPanel, 1, 0);
        layout.Controls.Add(hotKeyHint, 1, 1);

        notifications.Checked = working.ShowSuccessNotifications;
        layout.Controls.Add(new Label { Text = "通知", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(notifications, 1, 2);

        maxCharacters.Value = working.MaxClipboardCharacters;
        layout.Controls.Add(new Label { Text = "剪贴板字符上限", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(maxCharacters, 1, 3);

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildStyleTab()
    {
        var page = new TabPage("输出风格");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8) };
        top.Controls.Add(new Label { Text = "风格预设", AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
        presets.Items.AddRange(["自定义", "保留原格式", "极简笔记", "学术 Markdown", "Word 简洁"]);
        presets.SelectedIndex = 0;
        presets.SelectedIndexChanged += (_, _) => ApplyPreset();
        top.Controls.Add(presets);
        top.Controls.Add(new Label { Text = "下方可逐项微调；修改后预设变为自定义。", AutoSize = true, Padding = new Padding(12, 6, 0, 0) });

        styleGrid.SelectedObject = working.Style;
        styleGrid.PropertyValueChanged += (_, _) => presets.SelectedIndex = 0;

        page.Controls.Add(styleGrid);
        page.Controls.Add(top);
        return page;
    }

    private TabPage BuildRulesTab()
    {
        var page = new TabPage("目标规则");
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8) };
        var add = new Button { Text = "添加规则", Width = 90 };
        var remove = new Button { Text = "删除规则", Width = 90 };
        var up = new Button { Text = "上移", Width = 70 };
        var down = new Button { Text = "下移", Width = 70 };
        add.Click += (_, _) => ruleRows.Add(new RuleRow());
        remove.Click += (_, _) =>
        {
            if (rulesGrid.CurrentRow?.DataBoundItem is RuleRow row)
            {
                ruleRows.Remove(row);
            }
        };
        up.Click += (_, _) => MoveRule(-1);
        down.Click += (_, _) => MoveRule(1);
        toolbar.Controls.Add(add);
        toolbar.Controls.Add(remove);
        toolbar.Controls.Add(up);
        toolbar.Controls.Add(down);
        toolbar.Controls.Add(new Label { Text = "用户规则优先于默认规则，按列表顺序匹配。", AutoSize = true, Padding = new Padding(12, 6, 0, 0) });

        rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "名称", DataPropertyName = nameof(RuleRow.Name), Width = 120 });
        rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "进程名", DataPropertyName = nameof(RuleRow.ProcessName), Width = 120 });
        rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "标题正则", DataPropertyName = nameof(RuleRow.TitlePattern), Width = 160 });
        rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "扩展名(逗号分隔)", DataPropertyName = nameof(RuleRow.Extensions), Width = 140 });
        rulesGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "输出模式",
            DataPropertyName = nameof(RuleRow.Mode),
            DataSource = new[] { OutputMode.Markdown, OutputMode.Word, OutputMode.PlainText },
            Width = 110
        });

        foreach (var rule in working.UserRules)
        {
            ruleRows.Add(new RuleRow
            {
                Name = rule.Name,
                ProcessName = rule.ProcessName,
                TitlePattern = rule.TitlePattern ?? string.Empty,
                Extensions = string.Join(", ", rule.Extensions),
                Mode = rule.Mode == OutputMode.Auto ? OutputMode.PlainText : rule.Mode
            });
        }

        rulesGrid.DataSource = ruleRows;
        page.Controls.Add(rulesGrid);
        page.Controls.Add(toolbar);
        return page;
    }

    private void CaptureHotKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin)
        {
            return;
        }

        var parts = new List<string>();
        if (e.Control)
        {
            parts.Add("Ctrl");
        }

        if (e.Alt)
        {
            parts.Add("Alt");
        }

        if (e.Shift)
        {
            parts.Add("Shift");
        }

        if (winModifier.Checked)
        {
            parts.Add("Win");
        }

        parts.Add(e.KeyCode.ToString());
        hotKeyBox.Text = string.Join("+", parts);
        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    private void ValidateHotKey()
    {
        try
        {
            HotKeyGesture.Parse(hotKeyBox.Text);
            hotKeyHint.Text = "快捷键有效。保存后立即生效。";
            hotKeyHint.ForeColor = Color.Green;
        }
        catch (FormatException exception)
        {
            hotKeyHint.Text = exception.Message;
            hotKeyHint.ForeColor = Color.Firebrick;
        }
    }

    private void ApplyPreset()
    {
        var preset = presets.SelectedIndex switch
        {
            1 => StyleSettings.PreserveAll(),
            2 => StyleSettings.Minimal(),
            3 => StyleSettings.AcademicMarkdown(),
            4 => StyleSettings.SimpleWord(),
            _ => null
        };

        if (preset is null)
        {
            return;
        }

        working.Style = preset;
        styleGrid.SelectedObject = working.Style;
        styleGrid.Refresh();
    }

    private void MoveRule(int direction)
    {
        if (rulesGrid.CurrentRow?.DataBoundItem is not RuleRow row)
        {
            return;
        }

        var index = ruleRows.IndexOf(row);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= ruleRows.Count)
        {
            return;
        }

        ruleRows.RemoveAt(index);
        ruleRows.Insert(target, row);
        rulesGrid.ClearSelection();
        rulesGrid.Rows[target].Selected = true;
    }

    private void ResetDefaults()
    {
        var defaults = new AppSettings();
        working.HotKey = defaults.HotKey;
        working.ShowSuccessNotifications = defaults.ShowSuccessNotifications;
        working.MaxClipboardCharacters = defaults.MaxClipboardCharacters;
        working.Style = defaults.Style;
        working.UserRules = defaults.UserRules;

        hotKeyBox.Text = working.HotKey;
        winModifier.Checked = false;
        notifications.Checked = working.ShowSuccessNotifications;
        maxCharacters.Value = working.MaxClipboardCharacters;
        presets.SelectedIndex = 0;
        styleGrid.SelectedObject = working.Style;
        ruleRows.Clear();
    }

    private void SaveAndClose()
    {
        try
        {
            HotKeyGesture.Parse(hotKeyBox.Text);
        }
        catch (FormatException exception)
        {
            MessageBox.Show(this, exception.Message, "快捷键无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        working.HotKey = hotKeyBox.Text.Trim();
        working.ShowSuccessNotifications = notifications.Checked;
        working.MaxClipboardCharacters = (int)maxCharacters.Value;
        working.UserRules = [];
        foreach (var row in ruleRows)
        {
            var extensions = row.Extensions
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(extension => extension.TrimStart('.'))
                .Where(extension => extension.Length > 0)
                .ToArray();
            if (string.IsNullOrWhiteSpace(row.ProcessName) && string.IsNullOrWhiteSpace(row.TitlePattern) && extensions.Length == 0)
            {
                continue;
            }

            if (row.Mode == OutputMode.Auto)
            {
                MessageBox.Show(this, "目标规则的输出模式不能是 Auto。", "规则无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            working.UserRules.Add(new TargetRule
            {
                Name = row.Name.Trim(),
                ProcessName = row.ProcessName.Trim(),
                TitlePattern = string.IsNullOrWhiteSpace(row.TitlePattern) ? null : row.TitlePattern.Trim(),
                Extensions = extensions,
                Mode = row.Mode
            });
        }

        Result = working;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static AppSettings Clone(AppSettings source) => new()
    {
        HotKey = source.HotKey,
        ShowSuccessNotifications = source.ShowSuccessNotifications,
        MaxClipboardCharacters = source.MaxClipboardCharacters,
        Style = source.Style.Clone(),
        UserRules = source.UserRules.Select(rule => new TargetRule
        {
            Name = rule.Name,
            ProcessName = rule.ProcessName,
            TitlePattern = rule.TitlePattern,
            Extensions = (string[])rule.Extensions.Clone(),
            Mode = rule.Mode
        }).ToList()
    };
}
