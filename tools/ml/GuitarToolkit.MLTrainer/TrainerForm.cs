using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GuitarToolkit.MLTrainer;

public sealed class TrainerForm : Form
{
    private static readonly Regex EpochRegex = new(
        @"epoch=(?<epoch>\d+)/(?<total>\d+)\s+train_loss=(?<train>[0-9.]+)\s+val_loss=(?<val>[0-9.]+)\s+acc=(?<acc>[0-9.]+)\s+top3=(?<top3>[0-9.]+)",
        RegexOptions.Compiled);
    private static readonly Regex ProgressRegex = new(
        @"train_progress\s+epoch=(?<epoch>\d+)/(?<total>\d+)\s+batch=(?<batch>\d+)/(?<batches>\d+)\s+percent=(?<percent>[0-9.]+)\s+train_loss=(?<loss>[0-9.]+)",
        RegexOptions.Compiled);

    private readonly ProcessRunner _runner = new();
    private readonly CancellationTokenSource _shutdown = new();

    private readonly TextBox _pythonBox = new() { Text = "python" };
    private readonly TextBox _progressionRootBox = new();
    private readonly TextBox _melodyRootBox = new();
    private readonly TextBox _logBox = new();
    private readonly ListView _epochList = new();
    private readonly TextBox _previewBox = new();
    private readonly TextBox _resultBox = new();
    private readonly ListView _metricsList = new();
    private readonly ListView _comparisonList = new();
    private readonly ListView _historyList = new();

    private readonly TextBox _datasetBox = new() { Text = "synthetic_dataset_gui.jsonl" };
    private readonly NumericUpDown _datasetCountBox = new() { Minimum = 100, Maximum = 1_000_000, Value = 80000, Increment = 1000 };
    private readonly NumericUpDown _seedBox = new() { Minimum = 1, Maximum = 999999, Value = 7777 };
    private readonly ComboBox _profileBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly TextBox _outputDirBox = new() { Text = @"runs\progression_gui" };
    private readonly TextBox _resumeBox = new() { Text = @"runs\progression_diverse_plateau\best_model.pt" };
    private readonly NumericUpDown _epochsBox = new() { Minimum = 1, Maximum = 500, Value = 40 };
    private readonly NumericUpDown _batchBox = new() { Minimum = 1, Maximum = 4096, Value = 256 };
    private readonly NumericUpDown _learningRateBox = new() { DecimalPlaces = 5, Minimum = 0.00001M, Maximum = 1, Increment = 0.00005M, Value = 0.00010M };
    private readonly NumericUpDown _labelSmoothingBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 0.5M, Increment = 0.005M, Value = 0.040M };
    private readonly NumericUpDown _progressEveryBox = new() { Minimum = 0, Maximum = 10000, Value = 100, Increment = 10 };
    private readonly CheckBox _resetOptimizerBox = new() { Text = "Начать с новым optimizer при дообучении", Checked = true, AutoSize = true };
    private readonly CheckBox _cpuBox = new() { Text = "Отключить GPU и обучать на CPU", AutoSize = true };
    private readonly ProgressBar _trainProgress = new() { Minimum = 0, Maximum = 1000, Dock = DockStyle.Top, Height = 18 };
    private readonly Label _progressLabel = new() { Text = "Прогресс эпохи: ожидание запуска", AutoSize = true };
    private readonly ToolTip _toolTip = new();
    private ProgressBar? _activeProgressBar;
    private Label? _activeProgressLabel;

    private readonly TextBox _checkpointBox = new() { Text = @"runs\progression_gui\best_model.pt" };
    private readonly TextBox _promptsBox = new() { Text = "eval_prompts_full.jsonl" };
    private readonly TextBox _compareABox = new() { Text = @"runs\progression_gui\best_model.pt" };
    private readonly TextBox _compareBBox = new() { Text = @"runs\progression_diverse_plateau\best_model.pt" };
    private readonly TextBox _compareCBox = new();
    private readonly TextBox _previousBox = new() { Text = "<BOS>,i,VI" };
    private readonly ComboBox _styleBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _modeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _moodBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly TextBox _melodyDatasetBox = new() { Text = "synthetic_melody_dataset_gui.jsonl" };
    private readonly TextBox _melodyVocabBox = new() { Text = "vocab_v3.json" };
    private readonly NumericUpDown _melodyDatasetCountBox = new() { Minimum = 100, Maximum = 1_000_000, Value = 100000, Increment = 1000 };
    private readonly NumericUpDown _melodySeedBox = new() { Minimum = 1, Maximum = 999999, Value = 1984 };
    private readonly TextBox _melodyOutputDirBox = new() { Text = @"runs\melody_v3_gui" };
    private readonly TextBox _melodyResumeBox = new();
    private readonly NumericUpDown _melodyEpochsBox = new() { Minimum = 1, Maximum = 500, Value = 30 };
    private readonly NumericUpDown _melodyBatchBox = new() { Minimum = 1, Maximum = 4096, Value = 2048 };
    private readonly NumericUpDown _melodyLearningRateBox = new() { DecimalPlaces = 5, Minimum = 0.00001M, Maximum = 1, Increment = 0.00005M, Value = 0.00030M };
    private readonly NumericUpDown _melodyLabelSmoothingBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 0.5M, Increment = 0.005M, Value = 0.020M };
    private readonly NumericUpDown _melodyModePenaltyBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 2, Increment = 0.025M, Value = 0.120M };
    private readonly NumericUpDown _melodyMoodPenaltyBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 2, Increment = 0.025M, Value = 0.080M };
    private readonly NumericUpDown _melodyStylePenaltyBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 2, Increment = 0.025M, Value = 0.060M };
    private readonly NumericUpDown _melodyEntropyPenaltyBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 1, Increment = 0.005M, Value = 0.015M };
    private readonly NumericUpDown _melodyIntervalPenaltyBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 2, Increment = 0.025M, Value = 0.100M };
    private readonly NumericUpDown _melodyOctavePenaltyBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 2, Increment = 0.025M, Value = 0.060M };
    private readonly NumericUpDown _melodyRepeatPenaltyBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 2, Increment = 0.025M, Value = 0.120M };
    private readonly NumericUpDown _melodyRestPenaltyBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 2, Increment = 0.025M, Value = 0.100M };
    private readonly NumericUpDown _melodyDurationPenaltyBox = new() { DecimalPlaces = 3, Minimum = 0, Maximum = 2, Increment = 0.025M, Value = 0.060M };
    private readonly NumericUpDown _melodyEmbeddingSizeBox = new() { Minimum = 32, Maximum = 1024, Increment = 32, Value = 128 };
    private readonly NumericUpDown _melodyHeadsBox = new() { Minimum = 1, Maximum = 16, Value = 4 };
    private readonly NumericUpDown _melodyLayersBox = new() { Minimum = 1, Maximum = 12, Value = 3 };
    private readonly NumericUpDown _melodyFeedforwardSizeBox = new() { Minimum = 64, Maximum = 4096, Increment = 64, Value = 512 };
    private readonly NumericUpDown _melodyDropoutBox = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 0.8M, Increment = 0.05M, Value = 0.10M };
    private readonly NumericUpDown _melodyNumWorkersBox = new() { Minimum = 0, Maximum = 16, Value = 2 };
    private readonly NumericUpDown _melodyProgressEveryBox = new() { Minimum = 0, Maximum = 10000, Value = 10, Increment = 10 };
    private readonly CheckBox _melodyResetOptimizerBox = new() { Text = "Начать с новым optimizer при дообучении", Checked = true, AutoSize = true };
    private readonly CheckBox _melodyCpuBox = new() { Text = "Отключить GPU и обучать на CPU", AutoSize = true };
    private readonly CheckBox _melodyAmpBox = new() { Text = "AMP / mixed precision на GPU", Checked = true, AutoSize = true };
    private readonly ProgressBar _melodyTrainProgress = new() { Minimum = 0, Maximum = 1000, Dock = DockStyle.Top, Height = 18 };
    private readonly Label _melodyProgressLabel = new() { Text = "Прогресс эпохи: ожидание запуска", AutoSize = true };
    private readonly TextBox _melodyCheckpointBox = new() { Text = @"runs\melody_v3_gui\best_model.pt" };
    private readonly TextBox _melodyPreviousBox = new() { Text = "<BOS>,D:1:4:1/8,D:b3:4:1/8" };
    private readonly ComboBox _melodyStyleBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _melodyModeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _melodyMoodBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _melodyMeterBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _melodyBarsBox = new() { Minimum = 1, Maximum = 4, Value = 2 };
    private readonly TextBox _melodyProgressionBox = new() { Text = "i,VI" };
    private readonly NumericUpDown _melodyPreviewBpmBox = new() { Minimum = 40, Maximum = 260, Value = 100 };
    private readonly NumericUpDown _melodyPreviewTemperatureBox = new() { DecimalPlaces = 2, Minimum = 0.05M, Maximum = 2, Increment = 0.05M, Value = 0.85M };
    private readonly NumericUpDown _melodyPreviewTopKBox = new() { Minimum = 1, Maximum = 32, Value = 8 };
    private readonly TextBox _melodyGenerationOutputBox = new() { Text = @"runs\melody_generations" };
    private readonly NumericUpDown _melodyGenerationBox = new() { Minimum = 1, Maximum = 999, Value = 1 };
    private readonly NumericUpDown _melodyPopulationBox = new() { Minimum = 1, Maximum = 8, Value = 6 };
    private readonly NumericUpDown _melodyGenerationEpochsBox = new() { Minimum = 1, Maximum = 100, Value = 8 };

    public TrainerForm()
    {
        Text = "GuitarToolkit ML Trainer";
        MinimumSize = new Size(1180, 760);
        StartPosition = FormStartPosition.CenterScreen;

        _progressionRootBox.Text = FindProgressionRoot();
        _melodyRootBox.Text = FindMelodyRoot();
        _profileBox.Items.AddRange(["focused", "balanced", "diverse", "mood"]);
        _profileBox.SelectedItem = "mood";
        _styleBox.Items.AddRange(["STYLE_METAL", "STYLE_ROCK", "STYLE_POP", "STYLE_AMBIENT", "STYLE_BLUES"]);
        _styleBox.SelectedItem = "STYLE_METAL";
        _modeBox.Items.AddRange(["MODE_NATURAL_MINOR", "MODE_MAJOR", "MODE_DORIAN", "MODE_PHRYGIAN", "MODE_HARMONIC_MINOR"]);
        _modeBox.SelectedItem = "MODE_NATURAL_MINOR";
        _moodBox.Items.AddRange(["MOOD_DARK", "MOOD_EPIC", "MOOD_BRIGHT", "MOOD_CALM", "MOOD_TENSE"]);
        _moodBox.SelectedItem = "MOOD_DARK";
        _melodyStyleBox.Items.AddRange(["STYLE_METAL", "STYLE_ROCK", "STYLE_POP", "STYLE_AMBIENT", "STYLE_BLUES"]);
        _melodyStyleBox.SelectedItem = "STYLE_METAL";
        _melodyModeBox.Items.AddRange(["MODE_NATURAL_MINOR", "MODE_MAJOR", "MODE_DORIAN", "MODE_PHRYGIAN", "MODE_HARMONIC_MINOR"]);
        _melodyModeBox.SelectedItem = "MODE_NATURAL_MINOR";
        _melodyMoodBox.Items.AddRange(["MOOD_DARK", "MOOD_EPIC", "MOOD_BRIGHT", "MOOD_CALM", "MOOD_TENSE"]);
        _melodyMoodBox.SelectedItem = "MOOD_DARK";
        _melodyMeterBox.Items.AddRange(["METER_4_4", "METER_3_4", "METER_6_8"]);
        _melodyMeterBox.SelectedItem = "METER_4_4";
        ConfigureToolTips();

        Controls.Add(BuildLayout());
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _shutdown.Cancel();
        _runner.Stop();
        base.OnFormClosing(e);
    }

    private Control BuildLayout()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildProgressionTab());
        tabs.TabPages.Add(BuildMelodyTransformerTrainerTab());
        tabs.TabPages.Add(BuildSettingsTab());

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        root.Controls.Add(tabs, 0, 0);
        root.Controls.Add(BuildOutputPanel(), 0, 1);
        return root;
    }

    private TabPage BuildProgressionTab()
    {
        var page = new TabPage("Progression GRU/LSTM");
        var columns = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8)
        };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));

        columns.Controls.Add(BuildDatasetPanel(), 0, 0);
        columns.Controls.Add(BuildTrainingPanel(), 1, 0);
        columns.Controls.Add(BuildEvaluationPanel(), 2, 0);
        page.Controls.Add(columns);
        return page;
    }

    private Control BuildDatasetPanel()
    {
        var panel = Panel("Датасет");
        AddRow(panel, "Файл", _datasetBox);
        AddRow(panel, "Количество", _datasetCountBox);
        AddRow(panel, "Seed", _seedBox);
        AddRow(panel, "Профиль", _profileBox);
        AddButtonRow(panel,
            Button("Сгенерировать", GenerateDataset_Click),
            Button("Проверить", ValidateDataset_Click),
            Button("Превью", PreviewDataset_Click),
            Button("Выбрать файл", BrowseDataset_Click));

        var note = Note("focused = точность, balanced = базовый баланс, diverse = больше неожиданных ходов, mood = targeted fine-tune на различие настроений.");
        panel.Controls.Add(note);
        return panel;
    }

    private Control BuildTrainingPanel()
    {
        var panel = Panel("Обучение");
        AddRow(panel, "Output dir", _outputDirBox);
        AddRow(panel, "Resume", _resumeBox);
        AddRow(panel, "Эпохи", _epochsBox);
        AddRow(panel, "Batch", _batchBox);
        AddRow(panel, "Скорость обучения (learning rate)", _learningRateBox);
        AddRow(panel, "Мягкость ответов (label smoothing)", _labelSmoothingBox);
        AddRow(panel, "Показывать прогресс каждые N batches", _progressEveryBox);
        panel.Controls.Add(_resetOptimizerBox);
        panel.Controls.Add(_cpuBox);
        panel.Controls.Add(_progressLabel);
        panel.Controls.Add(_trainProgress);
        AddButtonRow(panel, Button("Старт", Train_Click), Button("Стоп", Stop_Click), Button("Открыть runs", OpenRuns_Click));

        var note = Note("Для RTX 3060 Ti обычно стартуй с batch 256. Если будет CUDA out of memory, снизь до 128.");
        panel.Controls.Add(note);
        return panel;
    }

    private Control BuildEvaluationPanel()
    {
        var panel = Panel("Проверка и экспорт");
        AddRow(panel, "Checkpoint", _checkpointBox);
        AddRow(panel, "Eval prompts", _promptsBox);
        AddRow(panel, "Previous", _previousBox);
        AddRow(panel, "Style", _styleBox);
        AddRow(panel, "Mode", _modeBox);
        AddRow(panel, "Mood", _moodBox);
        AddButtonRow(panel,
            Button("Inspect", Inspect_Click),
            Button("Evaluate", Evaluate_Click),
            Button("Export ONNX", Export_Click));
        AddButtonRow(panel, Button("Install in app", Install_Click), Button("Папка модели", OpenModelFolder_Click));
        AddRow(panel, "Compare A", _compareABox);
        AddRow(panel, "Compare B", _compareBBox);
        AddRow(panel, "Compare C", _compareCBox);
        AddButtonRow(panel, Button("Compare models", Compare_Click), Button("Load history", LoadHistory_Click));

        var note = Note("Inspect показывает вероятности следующей ступени. Evaluate считает энтропию, top3 и разрезы по style/mode/mood.");
        panel.Controls.Add(note);
        return panel;
    }

    private TabPage BuildMelodyTransformerTab()
    {
        var page = new TabPage("Melody Transformer");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(14)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var left = Panel("Будущая модель");
        left.Controls.Add(Note("Эта вкладка заранее резервирует место под вторую модель: маленький Transformer для коротких мелодий и риффов в выбранном размере такта."));
        left.Controls.Add(Note("План входа: стиль, лад, настроение, размер, длина, опорная прогрессия. План выхода: токены нот/пауз/длительностей, которые основная программа потом сыграет своим синтезом."));

        var right = Panel("Будущие операции");
        right.Controls.Add(Note("Когда появятся scripts для melody_transformer, сюда добавим генерацию датасета, обучение, evaluate, export ONNX и тестовый preview MIDI/нот."));
        right.Controls.Add(Note("Сейчас вкладка намеренно не запускает несуществующие команды, чтобы не смешивать рабочую progression-модель и будущий Transformer."));

        panel.Controls.Add(left, 0, 0);
        panel.Controls.Add(right, 1, 0);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildSettingsTab()
    {
        var page = new TabPage("Настройки");
        var panel = Panel("Пути");
        AddRow(panel, "Python", _pythonBox);
        AddRow(panel, "Progression tools", _progressionRootBox);
        AddRow(panel, "Melody tools", _melodyRootBox);
        AddButtonRow(panel, Button("Проверить GPU", CheckGpu_Click), Button("Открыть tools", OpenTools_Click));
        panel.Controls.Add(Note("Если CUDA подключена правильно, проверка GPU покажет torch.cuda.is_available() = True и имя видеокарты."));
        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildMelodyTransformerTrainerTab()
    {
        var page = new TabPage("Melody Transformer");
        var columns = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8)
        };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));

        columns.Controls.Add(BuildMelodyDatasetPanel(), 0, 0);
        columns.Controls.Add(BuildMelodyTrainingPanel(), 1, 0);
        columns.Controls.Add(BuildMelodyEvaluationPanel(), 2, 0);
        page.Controls.Add(columns);
        return page;
    }

    private Control BuildMelodyDatasetPanel()
    {
        var panel = Panel("Датасет мелодий");
        AddRow(panel, "Файл", _melodyDatasetBox);
        AddRow(panel, "Vocab", _melodyVocabBox);
        AddRow(panel, "Количество", _melodyDatasetCountBox);
        AddRow(panel, "Seed", _melodySeedBox);
        AddButtonRow(panel,
            Button("Сгенерировать", MelodyGenerateDataset_Click),
            Button("Проверить", MelodyValidateDataset_Click),
            Button("Превью", MelodyPreviewDataset_Click),
            Button("Выбрать файл", MelodyBrowseDataset_Click));

        panel.Controls.Add(Note("Это датасет коротких фраз: стиль, лад, настроение, размер, длина и опорная прогрессия на входе; токены нот, пауз и длительностей на выходе."));
        return panel;
    }

    private Control BuildMelodyTrainingPanel()
    {
        var panel = Panel("Обучение Transformer");
        AddRow(panel, "Output dir", _melodyOutputDirBox);
        AddRow(panel, "Resume", _melodyResumeBox);
        AddRow(panel, "Эпохи", _melodyEpochsBox);
        AddRow(panel, "Batch", _melodyBatchBox);
        AddRow(panel, "Learning rate", _melodyLearningRateBox);
        AddRow(panel, "Label smoothing", _melodyLabelSmoothingBox);
        AddRow(panel, "Штраф вне лада", _melodyModePenaltyBox);
        AddRow(panel, "Штраф вне настроения", _melodyMoodPenaltyBox);
        AddRow(panel, "Штраф вне стиля", _melodyStylePenaltyBox);
        AddRow(panel, "Штраф размазанности", _melodyEntropyPenaltyBox);
        AddRow(panel, "Штраф скачков интервала", _melodyIntervalPenaltyBox);
        AddRow(panel, "Штраф странной октавы", _melodyOctavePenaltyBox);
        AddRow(panel, "Anti-repeat penalty", _melodyRepeatPenaltyBox);
        AddRow(panel, "Anti-rest penalty", _melodyRestPenaltyBox);
        AddRow(panel, "Anti-duration penalty", _melodyDurationPenaltyBox);
        AddRow(panel, "Embedding", _melodyEmbeddingSizeBox);
        AddRow(panel, "Heads", _melodyHeadsBox);
        AddRow(panel, "Layers", _melodyLayersBox);
        AddRow(panel, "Feedforward", _melodyFeedforwardSizeBox);
        AddRow(panel, "Dropout", _melodyDropoutBox);
        AddRow(panel, "Data workers", _melodyNumWorkersBox);
        AddRow(panel, "Показывать прогресс каждые N batches", _melodyProgressEveryBox);
        panel.Controls.Add(_melodyResetOptimizerBox);
        panel.Controls.Add(_melodyCpuBox);
        panel.Controls.Add(_melodyAmpBox);
        panel.Controls.Add(_melodyProgressLabel);
        panel.Controls.Add(_melodyTrainProgress);
        AddButtonRow(panel, Button("Старт", MelodyTrain_Click), Button("Стоп", Stop_Click), Button("Открыть runs", MelodyOpenRuns_Click));

        panel.Controls.Add(Note("Штрафы учат модель не раздавать вероятность плохим токенам. Стартуй мягко: лад 0.12, mood 0.08, стиль 0.06, размазанность 0.025."));
        AddRow(panel, "Папка поколений", _melodyGenerationOutputBox);
        AddRow(panel, "Номер поколения", _melodyGenerationBox);
        AddRow(panel, "Кандидатов", _melodyPopulationBox);
        AddRow(panel, "Эпох на кандидата", _melodyGenerationEpochsBox);
        AddButtonRow(panel, Button("Запустить поколение", MelodyEvolveGeneration_Click));
        panel.Controls.Add(Note("Поколение обучает несколько кандидатов, проводит экзамен и сохраняет трех чемпионов: theoretical, balanced и art_house. Balanced автоматически станет Resume для следующего круга."));
        return panel;
    }

    private Control BuildMelodyEvaluationPanel()
    {
        var panel = Panel("Проверка и экспорт");
        AddRow(panel, "Checkpoint", _melodyCheckpointBox);
        AddRow(panel, "Previous", _melodyPreviousBox);
        AddRow(panel, "Style", _melodyStyleBox);
        AddRow(panel, "Mode", _melodyModeBox);
        AddRow(panel, "Mood", _melodyMoodBox);
        AddRow(panel, "Meter", _melodyMeterBox);
        AddRow(panel, "Bars", _melodyBarsBox);
        AddRow(panel, "Progression", _melodyProgressionBox);
        AddRow(panel, "Preview BPM", _melodyPreviewBpmBox);
        AddRow(panel, "Preview temperature", _melodyPreviewTemperatureBox);
        AddRow(panel, "Preview top-k", _melodyPreviewTopKBox);
        AddButtonRow(panel, Button("Install in app", MelodyInstall_Click));
        AddButtonRow(panel,
            Button("Inspect", MelodyInspect_Click),
            Button("Evaluate", MelodyEvaluate_Click),
            Button("Export ONNX", MelodyExport_Click));
        AddButtonRow(panel, Button("Preview WAV", MelodyPreviewWav_Click), Button("Папка модели", OpenModelFolder_Click), Button("Папка tools", MelodyOpenTools_Click));

        panel.Controls.Add(Note("Inspect показывает следующий токен мелодии. Evaluate считает сводные метрики: разнообразие, попадание в лад/настроение, ритм, энтропию и top3."));
        return panel;
    }

    private Control BuildOutputPanel()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ScrollBars = ScrollBars.Both;
        _logBox.Font = new Font("Consolas", 9);
        tabs.TabPages.Add(new TabPage("Лог") { Controls = { _logBox } });

        _epochList.Dock = DockStyle.Fill;
        _epochList.View = View.Details;
        _epochList.FullRowSelect = true;
        _epochList.Columns.Add("Epoch", 80);
        _epochList.Columns.Add("Train loss", 100);
        _epochList.Columns.Add("Val loss", 100);
        _epochList.Columns.Add("Acc", 80);
        _epochList.Columns.Add("Top3", 80);
        tabs.TabPages.Add(new TabPage("Эпохи") { Controls = { _epochList } });

        _previewBox.Dock = DockStyle.Fill;
        _previewBox.Multiline = true;
        _previewBox.ScrollBars = ScrollBars.Both;
        _previewBox.Font = new Font("Consolas", 9);
        tabs.TabPages.Add(new TabPage("Превью датасета") { Controls = { _previewBox } });

        _resultBox.Dock = DockStyle.Fill;
        _resultBox.Multiline = true;
        _resultBox.ScrollBars = ScrollBars.Both;
        _resultBox.Font = new Font("Consolas", 9);
        tabs.TabPages.Add(new TabPage("Результат") { Controls = { _resultBox } });

        _metricsList.Dock = DockStyle.Fill;
        _metricsList.View = View.Details;
        _metricsList.FullRowSelect = true;
        _metricsList.Columns.Add("Метрика", 240);
        _metricsList.Columns.Add("Значение", 120);
        _metricsList.Columns.Add("Смысл", 520);
        tabs.TabPages.Add(new TabPage("Оценка модели") { Controls = { _metricsList } });

        _comparisonList.Dock = DockStyle.Fill;
        _comparisonList.View = View.Details;
        _comparisonList.FullRowSelect = true;
        _comparisonList.Columns.Add("Model", 260);
        _comparisonList.Columns.Add("Overall", 80);
        _comparisonList.Columns.Add("Diversity", 80);
        _comparisonList.Columns.Add("Musical", 80);
        _comparisonList.Columns.Add("Mood", 80);
        _comparisonList.Columns.Add("Style", 80);
        _comparisonList.Columns.Add("Entropy", 80);
        _comparisonList.Columns.Add("Top3 mass", 90);
        tabs.TabPages.Add(new TabPage("Сравнение") { Controls = { _comparisonList } });

        _historyList.Dock = DockStyle.Fill;
        _historyList.View = View.Details;
        _historyList.FullRowSelect = true;
        _historyList.Columns.Add("Date", 150);
        _historyList.Columns.Add("Model", 260);
        _historyList.Columns.Add("Overall", 80);
        _historyList.Columns.Add("Diversity", 80);
        _historyList.Columns.Add("Musical", 80);
        _historyList.Columns.Add("Mood", 80);
        _historyList.Columns.Add("Style", 80);
        tabs.TabPages.Add(new TabPage("История") { Controls = { _historyList } });

        return tabs;
    }

    private async void GenerateDataset_Click(object? sender, EventArgs e)
    {
        await RunPythonAsync("generate_synthetic_dataset.py", $"--output {Quote(_datasetBox.Text)} --count {_datasetCountBox.Value:0} --seed {_seedBox.Value:0} --profile {_profileBox.Text}");
    }

    private async void MelodyGenerateDataset_Click(object? sender, EventArgs e)
    {
        await RunPythonAsync(
            "generate_synthetic_dataset.py",
            $"--output {Quote(_melodyDatasetBox.Text)} --count {_melodyDatasetCountBox.Value:0} --seed {_melodySeedBox.Value:0} --version 3 --vocab-output {Quote(_melodyVocabBox.Text)}",
            workingDirectory: _melodyRootBox.Text);
    }

    private async void ValidateDataset_Click(object? sender, EventArgs e)
    {
        await RunPythonAsync("validate_dataset.py", $"--dataset {Quote(_datasetBox.Text)}");
    }

    private async void MelodyValidateDataset_Click(object? sender, EventArgs e)
    {
        await RunPythonAsync(
            "validate_dataset.py",
            $"--dataset {Quote(_melodyDatasetBox.Text)}",
            workingDirectory: _melodyRootBox.Text);
    }

    private void PreviewDataset_Click(object? sender, EventArgs e)
    {
        string path = ResolveToolPath(_datasetBox.Text);
        if (!File.Exists(path))
        {
            AppendLog($"dataset not found: {path}");
            return;
        }

        _previewBox.Text = string.Join(Environment.NewLine, File.ReadLines(path).Take(120));
    }

    private void BrowseDataset_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выбрать датасет JSONL",
            InitialDirectory = Directory.Exists(_progressionRootBox.Text) ? _progressionRootBox.Text : AppContext.BaseDirectory,
            Filter = "JSONL dataset (*.jsonl)|*.jsonl|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _datasetBox.Text = MakeToolRelativePath(dialog.FileName);
    }

    private void MelodyPreviewDataset_Click(object? sender, EventArgs e)
    {
        string path = ResolveToolPath(_melodyDatasetBox.Text, _melodyRootBox.Text);
        if (!File.Exists(path))
        {
            AppendLog($"dataset not found: {path}");
            return;
        }

        _previewBox.Text = string.Join(Environment.NewLine, File.ReadLines(path).Take(120));
    }

    private void MelodyBrowseDataset_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выбрать датасет JSONL",
            InitialDirectory = Directory.Exists(_melodyRootBox.Text) ? _melodyRootBox.Text : AppContext.BaseDirectory,
            Filter = "JSONL dataset (*.jsonl)|*.jsonl|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _melodyDatasetBox.Text = MakeToolRelativePath(dialog.FileName, _melodyRootBox.Text);
    }

    private string MakeToolRelativePath(string path)
    {
        return MakeToolRelativePath(path, _progressionRootBox.Text);
    }

    private static string MakeToolRelativePath(string path, string rootDirectory)
    {
        string root = Path.GetFullPath(rootDirectory);
        string fullPath = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(root, fullPath);
        return relative.StartsWith("..", StringComparison.Ordinal) ? fullPath : relative;
    }

    private async void Train_Click(object? sender, EventArgs e)
    {
        _epochList.Items.Clear();
        _trainProgress.Value = 0;
        _activeProgressBar = _trainProgress;
        _activeProgressLabel = _progressLabel;
        _progressLabel.Text = "Прогресс эпохи: запуск обучения";
        string args =
            $"--dataset {Quote(_datasetBox.Text)} " +
            $"--epochs {_epochsBox.Value:0} " +
            $"--batch-size {_batchBox.Value:0} " +
            $"--learning-rate {DecimalText(_learningRateBox.Value)} " +
            $"--label-smoothing {DecimalText(_labelSmoothingBox.Value)} " +
            $"--output-dir {Quote(_outputDirBox.Text)} " +
            $"--resume {Quote(_resumeBox.Text)} " +
            $"--save-every 10 " +
            $"--progress-every {_progressEveryBox.Value:0}";

        if (_resetOptimizerBox.Checked)
            args += " --reset-optimizer";
        if (_cpuBox.Checked)
            args += " --cpu";

        await RunPythonAsync("train.py", args);
    }

    private async void MelodyTrain_Click(object? sender, EventArgs e)
    {
        _epochList.Items.Clear();
        _melodyTrainProgress.Value = 0;
        _activeProgressBar = _melodyTrainProgress;
        _activeProgressLabel = _melodyProgressLabel;
        _melodyProgressLabel.Text = "Прогресс эпохи: запуск обучения";

        string args =
            $"--dataset {Quote(_melodyDatasetBox.Text)} " +
            $"--vocab {Quote(_melodyVocabBox.Text)} " +
            $"--epochs {_melodyEpochsBox.Value:0} " +
            $"--batch-size {_melodyBatchBox.Value:0} " +
            $"--learning-rate {DecimalText(_melodyLearningRateBox.Value)} " +
            $"--label-smoothing {DecimalText(_melodyLabelSmoothingBox.Value)} " +
            $"--mode-penalty {DecimalText(_melodyModePenaltyBox.Value)} " +
            $"--mood-penalty {DecimalText(_melodyMoodPenaltyBox.Value)} " +
            $"--style-penalty {DecimalText(_melodyStylePenaltyBox.Value)} " +
            $"--entropy-penalty {DecimalText(_melodyEntropyPenaltyBox.Value)} " +
            $"--interval-penalty {DecimalText(_melodyIntervalPenaltyBox.Value)} " +
            $"--octave-penalty {DecimalText(_melodyOctavePenaltyBox.Value)} " +
            $"--repeat-penalty {DecimalText(_melodyRepeatPenaltyBox.Value)} " +
            $"--rest-penalty {DecimalText(_melodyRestPenaltyBox.Value)} " +
            $"--duration-penalty {DecimalText(_melodyDurationPenaltyBox.Value)} " +
            $"--embedding-size {_melodyEmbeddingSizeBox.Value:0} " +
            $"--heads {_melodyHeadsBox.Value:0} " +
            $"--layers {_melodyLayersBox.Value:0} " +
            $"--feedforward-size {_melodyFeedforwardSizeBox.Value:0} " +
            $"--dropout {DecimalText(_melodyDropoutBox.Value)} " +
            $"--output-dir {Quote(_melodyOutputDirBox.Text)} " +
            $"--save-every 10 " +
            $"--progress-every {_melodyProgressEveryBox.Value:0} " +
            $"--num-workers {_melodyNumWorkersBox.Value:0}";

        if (!string.IsNullOrWhiteSpace(_melodyResumeBox.Text))
            args += $" --resume {Quote(_melodyResumeBox.Text)}";
        if (_melodyResetOptimizerBox.Checked)
            args += " --reset-optimizer";
        if (_melodyCpuBox.Checked)
            args += " --cpu";
        if (_melodyAmpBox.Checked)
            args += " --amp";

        await RunPythonAsync("train.py", args, workingDirectory: _melodyRootBox.Text);
    }

    private async void MelodyEvolveGeneration_Click(object? sender, EventArgs e)
    {
        _epochList.Items.Clear();
        _melodyTrainProgress.Value = 0;
        _activeProgressBar = _melodyTrainProgress;
        _activeProgressLabel = _melodyProgressLabel;
        _melodyProgressLabel.Text = "Поколение: запуск кандидатов";

        string args =
            $"--dataset {Quote(_melodyDatasetBox.Text)} " +
            $"--vocab {Quote(_melodyVocabBox.Text)} " +
            $"--output-dir {Quote(_melodyGenerationOutputBox.Text)} " +
            $"--generation {_melodyGenerationBox.Value:0} " +
            $"--population {_melodyPopulationBox.Value:0} " +
            $"--epochs {_melodyGenerationEpochsBox.Value:0} " +
            $"--batch-size {_melodyBatchBox.Value:0} " +
            $"--learning-rate {DecimalText(_melodyLearningRateBox.Value)} " +
            $"--label-smoothing {DecimalText(_melodyLabelSmoothingBox.Value)} " +
            $"--mode-penalty {DecimalText(_melodyModePenaltyBox.Value)} " +
            $"--mood-penalty {DecimalText(_melodyMoodPenaltyBox.Value)} " +
            $"--style-penalty {DecimalText(_melodyStylePenaltyBox.Value)} " +
            $"--entropy-penalty {DecimalText(_melodyEntropyPenaltyBox.Value)} " +
            $"--interval-penalty {DecimalText(_melodyIntervalPenaltyBox.Value)} " +
            $"--octave-penalty {DecimalText(_melodyOctavePenaltyBox.Value)} " +
            $"--repeat-penalty {DecimalText(_melodyRepeatPenaltyBox.Value)} " +
            $"--rest-penalty {DecimalText(_melodyRestPenaltyBox.Value)} " +
            $"--duration-penalty {DecimalText(_melodyDurationPenaltyBox.Value)} " +
            $"--embedding-size {_melodyEmbeddingSizeBox.Value:0} " +
            $"--heads {_melodyHeadsBox.Value:0} " +
            $"--layers {_melodyLayersBox.Value:0} " +
            $"--feedforward-size {_melodyFeedforwardSizeBox.Value:0} " +
            $"--dropout {DecimalText(_melodyDropoutBox.Value)} " +
            $"--progress-every {_melodyProgressEveryBox.Value:0} " +
            $"--num-workers {_melodyNumWorkersBox.Value:0} " +
            $"--seed {_melodySeedBox.Value:0}";

        if (!string.IsNullOrWhiteSpace(_melodyResumeBox.Text))
            args += $" --resume {Quote(_melodyResumeBox.Text)}";
        if (_melodyResetOptimizerBox.Checked)
            args += " --reset-optimizer";
        if (_melodyCpuBox.Checked)
            args += " --cpu";
        if (_melodyAmpBox.Checked)
            args += " --amp";

        await RunPythonAsync("evolve_generation.py", args, captureResult: true, workingDirectory: _melodyRootBox.Text);
        ApplyLatestMelodyGenerationChampion();
    }

    private void Stop_Click(object? sender, EventArgs e)
    {
        _runner.Stop();
        AppendLog("stop requested");
    }

    private async void Inspect_Click(object? sender, EventArgs e)
    {
        string args =
            $"--checkpoint {Quote(_checkpointBox.Text)} " +
            $"--previous {Quote(_previousBox.Text)} " +
            $"--style {_styleBox.Text} --mode {_modeBox.Text} --mood {_moodBox.Text}";
        await RunPythonAsync("inspect_checkpoint.py", args, captureResult: true);
    }

    private async void Evaluate_Click(object? sender, EventArgs e)
    {
        string args = $"--checkpoint {Quote(_checkpointBox.Text)} --prompts {Quote(_promptsBox.Text)} --top-k 8";
        await RunPythonAsync("evaluate_checkpoint.py", args, captureResult: true, parseEvaluation: true);
    }

    private async void Compare_Click(object? sender, EventArgs e)
    {
        var checkpoints = new[] { _compareABox.Text, _compareBBox.Text, _compareCBox.Text }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (checkpoints.Count == 0)
        {
            AppendLog("compare skipped: no checkpoints selected");
            return;
        }

        _comparisonList.Items.Clear();
        foreach (string checkpoint in checkpoints)
        {
            string args = $"evaluate_checkpoint.py --checkpoint {Quote(checkpoint)} --prompts {Quote(_promptsBox.Text)} --top-k 8";
            string? json = await RunProcessCaptureAsync(_pythonBox.Text, args, _progressionRootBox.Text);
            if (string.IsNullOrWhiteSpace(json))
                continue;

            EvaluationSummary? summary = ParseEvaluationSummary(json);
            if (summary == null)
                continue;

            AddComparisonRow(summary);
            SaveEvaluationHistory(json, summary);
        }
    }

    private void LoadHistory_Click(object? sender, EventArgs e)
    {
        LoadEvaluationHistory();
    }

    private async void Export_Click(object? sender, EventArgs e)
    {
        string output = Path.Combine(Path.GetDirectoryName(_checkpointBox.Text) ?? string.Empty, "ProgressionNextTokenModel.onnx");
        string args = $"--checkpoint {Quote(_checkpointBox.Text)} --output {Quote(output)}";
        await RunPythonAsync("export_onnx.py", args);
    }

    private async void Install_Click(object? sender, EventArgs e)
    {
        string model = Path.Combine(Path.GetDirectoryName(_checkpointBox.Text) ?? string.Empty, "ProgressionNextTokenModel.onnx");
        string source = ResolveToolPath(model);
        if (!File.Exists(source))
        {
            AppendLog($"model not found: {source}");
            return;
        }

        string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GuitarToolkit", "models");
        Directory.CreateDirectory(targetDir);
        string targetPath = Path.Combine(targetDir, "ProgressionNextTokenModel.onnx");
        File.Copy(source, targetPath, overwrite: true);
        AppendLog($"installed={targetPath}");
    }

    private async void MelodyInspect_Click(object? sender, EventArgs e)
    {
        string args =
            $"--checkpoint {Quote(_melodyCheckpointBox.Text)} " +
            $"--vocab {Quote(_melodyVocabBox.Text)} " +
            $"--previous {Quote(_melodyPreviousBox.Text)} " +
            $"--style {_melodyStyleBox.Text} --mode {_melodyModeBox.Text} --mood {_melodyMoodBox.Text} " +
            $"--meter {_melodyMeterBox.Text} --bars {_melodyBarsBox.Value:0} " +
            $"--progression {Quote(_melodyProgressionBox.Text)}";
        await RunPythonAsync("inspect_checkpoint.py", args, captureResult: true, workingDirectory: _melodyRootBox.Text);
    }

    private async void MelodyEvaluate_Click(object? sender, EventArgs e)
    {
        string args = $"--checkpoint {Quote(_melodyCheckpointBox.Text)} --vocab {Quote(_melodyVocabBox.Text)} --top-k 8";
        await RunPythonAsync("evaluate_checkpoint.py", args, captureResult: true, parseEvaluation: true, workingDirectory: _melodyRootBox.Text);
    }

    private async void MelodyExport_Click(object? sender, EventArgs e)
    {
        string output = Path.Combine(Path.GetDirectoryName(_melodyCheckpointBox.Text) ?? string.Empty, "MelodyPhraseTransformer.onnx");
        string args = $"--checkpoint {Quote(_melodyCheckpointBox.Text)} --vocab {Quote(_melodyVocabBox.Text)} --output {Quote(output)}";
        await RunPythonAsync("export_onnx.py", args, workingDirectory: _melodyRootBox.Text);
    }

    private void MelodyInstall_Click(object? sender, EventArgs e)
    {
        string model = Path.Combine(Path.GetDirectoryName(_melodyCheckpointBox.Text) ?? string.Empty, "MelodyPhraseTransformer.onnx");
        string sourceModel = ResolveToolPath(model, _melodyRootBox.Text);
        string sourceVocabulary = ResolveToolPath(_melodyVocabBox.Text, _melodyRootBox.Text);
        if (!File.Exists(sourceModel))
        {
            AppendLog($"melody model not found: {sourceModel}");
            AppendLog("export ONNX first, then install it in the app");
            return;
        }

        if (!File.Exists(sourceVocabulary))
        {
            AppendLog($"melody vocab not found: {sourceVocabulary}");
            return;
        }

        string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GuitarToolkit", "models");
        Directory.CreateDirectory(targetDir);
        string targetModel = Path.Combine(targetDir, "MelodyPhraseTransformer.onnx");
        string targetVocabulary = Path.Combine(targetDir, "MelodyPhraseTransformer.vocab.json");
        File.Copy(sourceModel, targetModel, overwrite: true);
        File.Copy(sourceVocabulary, targetVocabulary, overwrite: true);
        AppendLog($"installed melody model={targetModel}");
        AppendLog($"installed melody vocab={targetVocabulary}");
    }

    private async void MelodyPreviewWav_Click(object? sender, EventArgs e)
    {
        string output = Path.Combine(Path.GetDirectoryName(_melodyCheckpointBox.Text) ?? "runs", "melody_preview.wav");
        string args =
            $"--checkpoint {Quote(_melodyCheckpointBox.Text)} " +
            $"--vocab {Quote(_melodyVocabBox.Text)} " +
            $"--previous {Quote(_melodyPreviousBox.Text)} " +
            $"--style {_melodyStyleBox.Text} --mode {_melodyModeBox.Text} --mood {_melodyMoodBox.Text} " +
            $"--meter {_melodyMeterBox.Text} --bars {_melodyBarsBox.Value:0} " +
            $"--progression {Quote(_melodyProgressionBox.Text)} " +
            $"--bpm {_melodyPreviewBpmBox.Value:0} " +
            $"--temperature {DecimalText(_melodyPreviewTemperatureBox.Value)} " +
            $"--top-k {_melodyPreviewTopKBox.Value:0} " +
            $"--output {Quote(output)}";
        await RunPythonAsync("generate_preview.py", args, captureResult: true, workingDirectory: _melodyRootBox.Text);

        string wav = ResolveToolPath(output, _melodyRootBox.Text);
        if (File.Exists(wav))
            Process.Start(new ProcessStartInfo { FileName = wav, UseShellExecute = true });
    }

    private void ApplyLatestMelodyGenerationChampion()
    {
        string summaryPath = ResolveToolPath(Path.Combine(_melodyGenerationOutputBox.Text, "generation_summary.json"), _melodyRootBox.Text);
        if (!File.Exists(summaryPath))
        {
            AppendLog($"generation summary not found: {summaryPath}");
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(summaryPath));
            JsonElement champions = document.RootElement.GetProperty("champions");
            string balanced = GetString(champions.GetProperty("balanced"), "champion_checkpoint");
            string relative = MakeToolRelativePath(ResolveToolPath(balanced, _melodyRootBox.Text), _melodyRootBox.Text);
            _melodyResumeBox.Text = relative;
            _melodyCheckpointBox.Text = relative;
            _melodyGenerationBox.Value = Math.Min(_melodyGenerationBox.Maximum, _melodyGenerationBox.Value + 1);
            AppendLog($"next generation parent={relative}");
        }
        catch (JsonException ex)
        {
            AppendLog($"generation summary parse failed: {ex.Message}");
        }
        catch (KeyNotFoundException ex)
        {
            AppendLog($"generation summary is missing expected field: {ex.Message}");
        }
    }

    private async void CheckGpu_Click(object? sender, EventArgs e)
    {
        await RunProcessAsync(
            _pythonBox.Text,
            "-c \"import torch; print(torch.__version__, torch.version.cuda, torch.cuda.is_available(), torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'CPU')\"",
            _progressionRootBox.Text);
    }

    private void OpenRuns_Click(object? sender, EventArgs e) => OpenFolder(ResolveToolPath("runs"));

    private void MelodyOpenRuns_Click(object? sender, EventArgs e) => OpenFolder(ResolveToolPath("runs", _melodyRootBox.Text));

    private void OpenTools_Click(object? sender, EventArgs e) => OpenFolder(_progressionRootBox.Text);

    private void MelodyOpenTools_Click(object? sender, EventArgs e) => OpenFolder(_melodyRootBox.Text);

    private void OpenModelFolder_Click(object? sender, EventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GuitarToolkit", "models");
        OpenFolder(path);
    }

    private async Task RunPythonAsync(string script, string arguments, bool captureResult = false, bool parseEvaluation = false, string? workingDirectory = null)
    {
        await RunProcessAsync(_pythonBox.Text, $"{script} {arguments}", workingDirectory ?? _progressionRootBox.Text, captureResult, parseEvaluation);
    }

    private async Task RunProcessAsync(string fileName, string arguments, string workingDirectory, bool captureResult = false, bool parseEvaluation = false)
    {
        if (_runner.IsRunning)
        {
            AppendLog("another process is already running");
            return;
        }

        var result = new StringBuilder();
        AppendLog($"> {fileName} {arguments}");
        try
        {
            int exitCode = await _runner.RunAsync(
                fileName,
                arguments,
                workingDirectory,
                line =>
                {
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        AppendLog(line);
                        ParseEpoch(line);
                        ParseProgress(line);
                        if (captureResult)
                            result.AppendLine(line);
                    }));
                },
                _shutdown.Token);

            AppendLog($"exit={exitCode}");
            if (captureResult)
            {
                _resultBox.Text = result.ToString();
                if (parseEvaluation)
                {
                    RenderEvaluationMetrics(result.ToString());
                    EvaluationSummary? summary = ParseEvaluationSummary(result.ToString());
                    if (summary != null)
                        SaveEvaluationHistory(result.ToString(), summary);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendLog(ex.Message);
        }
    }

    private async Task<string?> RunProcessCaptureAsync(string fileName, string arguments, string workingDirectory)
    {
        if (_runner.IsRunning)
        {
            AppendLog("another process is already running");
            return null;
        }

        var result = new StringBuilder();
        AppendLog($"> {fileName} {arguments}");
        try
        {
            int exitCode = await _runner.RunAsync(
                fileName,
                arguments,
                workingDirectory,
                line =>
                {
                    BeginInvoke((MethodInvoker)(() => AppendLog(line)));
                    result.AppendLine(line);
                },
                _shutdown.Token);

            AppendLog($"exit={exitCode}");
            return exitCode == 0 ? result.ToString() : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendLog(ex.Message);
            return null;
        }
    }

    private void ParseEpoch(string line)
    {
        Match match = EpochRegex.Match(line);
        if (!match.Success)
            return;

        var item = new ListViewItem($"{match.Groups["epoch"].Value}/{match.Groups["total"].Value}");
        item.SubItems.Add(match.Groups["train"].Value);
        item.SubItems.Add(match.Groups["val"].Value);
        item.SubItems.Add(match.Groups["acc"].Value);
        item.SubItems.Add(match.Groups["top3"].Value);
        _epochList.Items.Add(item);
        item.EnsureVisible();
        ProgressBar progressBar = _activeProgressBar ?? _trainProgress;
        Label progressLabel = _activeProgressLabel ?? _progressLabel;
        progressBar.Value = 1000;
        progressLabel.Text = $"Эпоха {match.Groups["epoch"].Value}/{match.Groups["total"].Value}: validation готова";
    }

    private void ParseProgress(string line)
    {
        Match match = ProgressRegex.Match(line);
        if (!match.Success)
            return;

        double percent = double.Parse(match.Groups["percent"].Value, CultureInfo.InvariantCulture);
        ProgressBar progressBar = _activeProgressBar ?? _trainProgress;
        Label progressLabel = _activeProgressLabel ?? _progressLabel;
        progressBar.Value = Math.Clamp((int)Math.Round(percent * 10), 0, 1000);
        progressLabel.Text =
            $"Эпоха {match.Groups["epoch"].Value}/{match.Groups["total"].Value}: " +
            $"{percent:0.0}% " +
            $"batch {match.Groups["batch"].Value}/{match.Groups["batches"].Value}, " +
            $"loss {match.Groups["loss"].Value}";
    }

    private void AppendLog(string text)
    {
        _logBox.AppendText(text + Environment.NewLine);
    }

    private void RenderEvaluationMetrics(string json)
    {
        _metricsList.Items.Clear();

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement summary = document.RootElement.GetProperty("summary");

            AddMetric(summary, "overall_score_percent", "Итоговая оценка", "%", "Сводный балл: разнообразие, музыкальность, настроение, стиль и баланс уверенности.");
            AddMetric(summary, "diversity_score_percent", "Разнообразие", "%", "Насколько модель оставляет живой выбор вместо одного жесткого ответа.");
            AddMetric(summary, "musicality_score_percent", "Музыкальное попадание", "%", "Масса вероятности и top-1 внутри допустимых для лада ступеней.");
            AddMetric(summary, "mood_fit_score_percent", "Попадание в настроение", "%", "Насколько ответы соответствуют выбранному mood.");
            AddMetric(summary, "style_fit_score_percent", "Попадание в стиль", "%", "Насколько ответы соответствуют выбранному style.");
            AddMetric(summary, "interval_score_percent", "Осмысленность интервалов", "%", "Насколько модель избегает случайных слишком широких скачков между соседними нотами.");
            AddMetric(summary, "octave_score_percent", "Управление октавами", "%", "Насколько регистр нот соответствует музыкальному контексту и настроению.");
            AddMetric(summary, "phrase_life_score_percent", "Живость фразы", "%", "Сводная оценка против спама одинаковых нот, серий пауз и однообразных длительностей.");
            AddMetric(summary, "anti_repeat_score_percent", "Анти-повторы", "%", "Насколько модель не залипает на одной и той же ноте.");
            AddMetric(summary, "anti_rest_score_percent", "Анти-паузы", "%", "Насколько модель не уходит в серии пауз.");
            AddMetric(summary, "anti_duration_score_percent", "Анти-ритм-спам", "%", "Насколько модель не топчется на одной длительности.");
            AddMetric(summary, "confidence_balance_percent", "Баланс уверенности", "%", "Штрафует слишком зажатую и слишком размазанную модель.");
            AddMetric(summary, "distinct_top1_percent", "Уникальность top-1", "%", "Сколько разных первых ответов модель дала на тестовый набор.");
            AddMetric(summary, "avg_entropy", "Средняя энтропия", "", "Сырая мера вариативности распределения.");
            AddMetric(summary, "avg_top3_mass", "Масса top-3", "", "Сколько вероятности забирают три первых варианта.");
            AddMetric(summary, "top1_musical_hit_percent", "Top-1 в ладу", "%", "Процент тестов, где первый ответ попал в допустимые ступени.");
            AddMetric(summary, "top1_mood_hit_percent", "Top-1 в настроении", "%", "Процент тестов, где первый ответ попал в mood-набор.");
            AddMetric(summary, "top1_style_hit_percent", "Top-1 в стиле", "%", "Процент тестов, где первый ответ попал в style-набор.");
        }
        catch (JsonException ex)
        {
            AppendLog($"evaluation metrics parse failed: {ex.Message}");
        }
        catch (KeyNotFoundException ex)
        {
            AppendLog($"evaluation summary is missing expected field: {ex.Message}");
        }
    }

    private EvaluationSummary? ParseEvaluationSummary(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement summary = document.RootElement.GetProperty("summary");
            return new EvaluationSummary(
                GetString(summary, "checkpoint"),
                GetDouble(summary, "overall_score_percent"),
                GetDouble(summary, "diversity_score_percent"),
                GetDouble(summary, "musicality_score_percent"),
                GetDouble(summary, "mood_fit_score_percent"),
                GetDouble(summary, "style_fit_score_percent"),
                GetDouble(summary, "avg_entropy"),
                GetDouble(summary, "avg_top3_mass"),
                GetDouble(summary, "distinct_top1_percent"));
        }
        catch (JsonException ex)
        {
            AppendLog($"evaluation summary parse failed: {ex.Message}");
            return null;
        }
        catch (KeyNotFoundException ex)
        {
            AppendLog($"evaluation summary is missing expected field: {ex.Message}");
            return null;
        }
    }

    private void AddComparisonRow(EvaluationSummary summary)
    {
        var item = new ListViewItem(ModelLabel(summary.Checkpoint));
        item.SubItems.Add(PercentText(summary.Overall));
        item.SubItems.Add(PercentText(summary.Diversity));
        item.SubItems.Add(PercentText(summary.Musicality));
        item.SubItems.Add(PercentText(summary.MoodFit));
        item.SubItems.Add(PercentText(summary.StyleFit));
        item.SubItems.Add(summary.Entropy.ToString("0.####", CultureInfo.InvariantCulture));
        item.SubItems.Add(summary.Top3Mass.ToString("0.####", CultureInfo.InvariantCulture));
        _comparisonList.Items.Add(item);
    }

    private void SaveEvaluationHistory(string json, EvaluationSummary summary)
    {
        string historyPath = EvaluationHistoryPath();
        Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);

        using JsonDocument document = JsonDocument.Parse(json);
        var record = new
        {
            timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
            prompts = _promptsBox.Text,
            checkpoint = summary.Checkpoint,
            overall = summary.Overall,
            diversity = summary.Diversity,
            musicality = summary.Musicality,
            mood_fit = summary.MoodFit,
            style_fit = summary.StyleFit,
            entropy = summary.Entropy,
            top3_mass = summary.Top3Mass,
            distinct_top1 = summary.DistinctTop1,
            raw = document.RootElement.Clone()
        };

        File.AppendAllText(historyPath, JsonSerializer.Serialize(record) + Environment.NewLine);
        LoadEvaluationHistory();
    }

    private void LoadEvaluationHistory()
    {
        _historyList.Items.Clear();
        string historyPath = EvaluationHistoryPath();
        if (!File.Exists(historyPath))
        {
            AppendLog($"history not found: {historyPath}");
            return;
        }

        foreach (string line in File.ReadLines(historyPath).Reverse().Take(200))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            var item = new ListViewItem(GetString(root, "timestamp"));
            item.SubItems.Add(ModelLabel(GetString(root, "checkpoint")));
            item.SubItems.Add(PercentText(GetDouble(root, "overall")));
            item.SubItems.Add(PercentText(GetDouble(root, "diversity")));
            item.SubItems.Add(PercentText(GetDouble(root, "musicality")));
            item.SubItems.Add(PercentText(GetDouble(root, "mood_fit")));
            item.SubItems.Add(PercentText(GetDouble(root, "style_fit")));
            _historyList.Items.Add(item);
        }
    }

    private void AddMetric(JsonElement summary, string propertyName, string label, string suffix, string description)
    {
        if (!summary.TryGetProperty(propertyName, out JsonElement value))
            return;

        string text = value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out int integer)
                ? integer.ToString(CultureInfo.InvariantCulture)
                : value.GetDouble().ToString("0.####", CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        var item = new ListViewItem(label);
        item.SubItems.Add(string.IsNullOrEmpty(suffix) ? text : $"{text}{suffix}");
        item.SubItems.Add(description);
        _metricsList.Items.Add(item);
    }

    private string EvaluationHistoryPath()
    {
        return ResolveToolPath(Path.Combine("runs", "model_evaluation_history.jsonl"));
    }

    private static string ModelLabel(string checkpoint)
    {
        string directory = Path.GetFileName(Path.GetDirectoryName(checkpoint) ?? string.Empty);
        string file = Path.GetFileName(checkpoint);
        return string.IsNullOrWhiteSpace(directory) ? file : $"{directory}/{file}";
    }

    private static string PercentText(double value)
    {
        return $"{value:0.#}%";
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetString() ?? string.Empty;
    }

    private static double GetDouble(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetDouble();
    }

    private string ResolveToolPath(string path)
    {
        return ResolveToolPath(path, _progressionRootBox.Text);
    }

    private static string ResolveToolPath(string path, string rootDirectory)
    {
        if (Path.IsPathRooted(path))
            return path;

        return Path.GetFullPath(Path.Combine(rootDirectory, path));
    }

    private static string FindProgressionRoot()
    {
        string bundled = Path.Combine(AppContext.BaseDirectory, "progression_next_token");
        if (Directory.Exists(bundled))
            return bundled;

        string sibling = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "progression_next_token"));
        if (Directory.Exists(sibling))
            return sibling;

        string current = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            string candidate = Path.GetFullPath(Path.Combine(current, "..", "..", "..", "..", "progression_next_token"));
            if (Directory.Exists(candidate))
                return candidate;

            current = Path.GetFullPath(Path.Combine(current, ".."));
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "progression_next_token"));
    }

    private static string FindMelodyRoot()
    {
        string bundled = Path.Combine(AppContext.BaseDirectory, "melody_phrase_transformer");
        if (Directory.Exists(bundled))
            return bundled;

        string sibling = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "melody_phrase_transformer"));
        if (Directory.Exists(sibling))
            return sibling;

        string current = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            string candidate = Path.GetFullPath(Path.Combine(current, "..", "..", "..", "..", "melody_phrase_transformer"));
            if (Directory.Exists(candidate))
                return candidate;

            current = Path.GetFullPath(Path.Combine(current, ".."));
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "melody_phrase_transformer"));
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string DecimalText(decimal value)
    {
        return value.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    private static TableLayoutPanel Panel(string title)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(10)
        };
        panel.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        });
        return panel;
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        control.Dock = DockStyle.Top;
        control.Margin = new Padding(0, 0, 0, 10);
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 0, 0, 3) });
        panel.Controls.Add(control);
    }

    private static void AddButtonRow(TableLayoutPanel panel, params Button[] buttons)
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 5, 0, 10) };
        foreach (Button button in buttons)
            row.Controls.Add(button);

        panel.Controls.Add(row);
    }

    private static Button Button(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30, Margin = new Padding(0, 0, 8, 0) };
        button.Click += handler;
        return button;
    }

    private static Label Note(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Height = 72,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 8, 0, 0)
        };
    }

    private void ConfigureToolTips()
    {
        _toolTip.SetToolTip(_learningRateBox, "Размер шага обучения. Меньше = медленнее, но аккуратнее; для финального fine-tune обычно 0.00005-0.0001.");
        _toolTip.SetToolTip(_labelSmoothingBox, "Оставляет часть вероятности альтернативным аккордам. Больше = вариативнее, но выше риск странных ходов.");
        _toolTip.SetToolTip(_progressEveryBox, "Как часто train.py пишет прогресс внутри эпохи. 0 отключает промежуточный вывод.");
        _toolTip.SetToolTip(_resetOptimizerBox, "Веса модели сохраняются, но AdamW начинает без старой инерции. Обычно включать при новом датасете или learning rate.");
        _toolTip.SetToolTip(_cpuBox, "Полезно только для отладки. Для нормального обучения оставь выключенным, чтобы работала видеокарта.");
        _toolTip.SetToolTip(_melodyBatchBox, "Сколько примеров считать за раз. На RTX 3060 Ti для v2 пробуй 256, затем 384/512, пока хватает VRAM.");
        _toolTip.SetToolTip(_melodyAmpBox, "Mixed precision на CUDA. Обычно ускоряет обучение и снижает расход видеопамяти.");
        _toolTip.SetToolTip(_melodyNumWorkersBox, "Потоки загрузки датасета. На Windows начни с 0; если GPU простаивает, попробуй 2.");
        _toolTip.SetToolTip(_melodyIntervalPenaltyBox, "Штрафует вероятность слишком широких случайных скачков между соседними нотами.");
        _toolTip.SetToolTip(_melodyOctavePenaltyBox, "Мягко учит модель выбирать регистр по настроению, не держась слепо за одну октаву.");
        _toolTip.SetToolTip(_melodyEmbeddingSizeBox, "Размер внутреннего представления токена. Больше = умнее, но медленнее.");
        _toolTip.SetToolTip(_melodyLayersBox, "Количество слоев Transformer. 2 быстро, 3-4 качественнее и медленнее.");
        _toolTip.SetToolTip(_melodyHeadsBox, "Количество attention heads. Должно делить embedding без остатка.");
        _toolTip.SetToolTip(_melodyFeedforwardSizeBox, "Размер внутреннего MLP в Transformer. Обычно 3-4x от embedding.");
    }

    private sealed record EvaluationSummary(
        string Checkpoint,
        double Overall,
        double Diversity,
        double Musicality,
        double MoodFit,
        double StyleFit,
        double Entropy,
        double Top3Mass,
        double DistinctTop1);
}
