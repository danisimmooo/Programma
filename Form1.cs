using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.WindowsForms;

namespace HeatConductivityApp
{
    public partial class Form1 : Form
    {
        // База данных материалов с рекомендуемой толщиной по умолчанию (в мм)
        public class MaterialItem
        {
            public string Name { get; set; }
            public double Lambda { get; set; } // Вт/(м·К)
            public double Density { get; set; } // кг/м³
            public double HeatCapacity { get; set; } // Дж/(кг·К)
            public int RecommendedThickMm { get; set; } // Рекомендуемая толщина

            public override string ToString() => Name;
        }

        private List<MaterialItem> materialLibrary = new List<MaterialItem>
        {
            new MaterialItem { Name = "Железобетон", Lambda = 1.70, Density = 2500, HeatCapacity = 840, RecommendedThickMm = 200 },
            new MaterialItem { Name = "Кирпич керамический", Lambda = 0.70, Density = 1800, HeatCapacity = 880, RecommendedThickMm = 250 },
            new MaterialItem { Name = "Блоки газосиликатные (D500)", Lambda = 0.13, Density = 500, HeatCapacity = 840, RecommendedThickMm = 400 },
            new MaterialItem { Name = "Минеральная вата (Фасад)", Lambda = 0.040, Density = 110, HeatCapacity = 840, RecommendedThickMm = 100 },
            new MaterialItem { Name = "Пенополистирол (XPS)", Lambda = 0.032, Density = 35, HeatCapacity = 1450, RecommendedThickMm = 100 },
            new MaterialItem { Name = "Дерево (брус сосна/ель)", Lambda = 0.15, Density = 500, HeatCapacity = 2300, RecommendedThickMm = 150 },
            new MaterialItem { Name = "Штукатурка защитно-отделочная", Lambda = 0.87, Density = 1700, HeatCapacity = 1050, RecommendedThickMm = 15 }
        };

        // UI Элементы
        private TabControl mainTabControl;
        private DataGridView gridLayers;
        private ComboBox cbPresets, cbClimatePresets;
        private Button btnAddLayer, btnRemoveLayer, btnExportReport;
        private ToolTip toolTip;

        private NumericUpDown numTLeft, numTRight, numHumidityIn, numTotalTime, numDt;
        private Button btnStartCalc, btnStopCalc;

        private Label lblCalcStatus, lblCalcTime, lblCalcSteps;
        private Label lblRTotal, lblHeatLoss, lblDewPointStatus, lblSelectedPointInfo;
        private TextBox txtEngineeringAdvice;

        private PictureBox picHeatMap;
        private Panel panelGraphHost;

        // Графики OxyPlot
        private PlotView plotTimeView;
        private LineSeries timeSeries;

        private PlotView plotProfileView;
        private LineSeries profileSeries;

        private PlotView plotRContributionView;
        private TextBox txtMoistureAnalysis;

        // Физическая сетка
        private int Nx = 101;
        private int Ny = 51;
        private double Lx = 0.4;
        private double Ly = 1.0;

        private double[,] T;
        private double[,] K;
        private double[,] C;
        private double[,] T_new;

        private bool isRunning = false;
        private Point selectedPoint = new Point(50, 25);

        public Form1()
        {
            InitializeComponent();

            this.Text = "Инженерный расчёт теплопроводности и теплопотерь (Нормы Республики Беларусь)";
            this.Size = new Size(1400, 880);
            this.MinimumSize = new Size(1280, 780);
            this.StartPosition = FormStartPosition.CenterScreen;

            toolTip = new ToolTip
            {
                ToolTipTitle = "Нормы Республики Беларусь (СН / СНиП)",
                IsBalloon = true,
                AutoPopDelay = 10000,
                InitialDelay = 300,
                ReshowDelay = 100
            };

            this.Controls.Clear();
            BuildInterface();
            SetupPlots();
            InitModelFromGrid();
        }

        private void BuildInterface()
        {
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(8)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 370f));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310f));

            // ==================== 1. ЛЕВАЯ ПАНЕЛЬ ====================
            Panel leftContainer = new Panel { Dock = DockStyle.Fill };
            FlowLayoutPanel leftPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            // Слои стены
            GroupBox gbLayers = new GroupBox
            {
                Text = "Конструкция стены (Слои)",
                Width = 350,
                Height = 310,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            TableLayoutPanel tlpLayersArea = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            tlpLayersArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 35f));
            tlpLayersArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tlpLayersArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 35f));

            FlowLayoutPanel pnlPresets = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            pnlPresets.Controls.Add(new Label { Text = "Шаблон:", AutoSize = true, Margin = new Padding(0, 6, 5, 0), Font = new Font("Segoe UI", 8.5f, FontStyle.Regular) });
            cbPresets = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250, Font = new Font("Segoe UI", 8.5f, FontStyle.Regular) };
            cbPresets.Items.AddRange(new string[] { "-- Типовая стена (Беларусь) --", "Газосиликат + Минвата + Штукатурка", "Кирпичная стена с утеплением XPS", "Панельный дом (Ж/Б + Минвата)", "Брус с утеплением минватой" });
            cbPresets.SelectedIndex = 0;
            cbPresets.SelectedIndexChanged += CbPresets_SelectedIndexChanged;
            toolTip.SetToolTip(cbPresets, "Типовые белорусские фасадные системы для соблюдения требований R ≥ 3.20 м²·К/Вт.");
            pnlPresets.Controls.Add(cbPresets);
            tlpLayersArea.Controls.Add(pnlPresets, 0, 0);

            gridLayers = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };

            var colMat = new DataGridViewComboBoxColumn
            {
                HeaderText = "Материал",
                Name = "colMat",
                DataSource = materialLibrary,
                DisplayMember = "Name",
                ValueMember = "Name"
            };
            gridLayers.Columns.Add(colMat);
            gridLayers.Columns.Add("Thick", "Толщина, мм");

            gridLayers.Rows.Add("Блоки газосиликатные (D500)", "400");
            gridLayers.Rows.Add("Минеральная вата (Фасад)", "100");
            gridLayers.Rows.Add("Штукатурка защитно-отделочная", "15");

            // Пересчет при редактировании
            gridLayers.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                {
                    var cellVal = gridLayers.Rows[e.RowIndex].Cells[0].Value?.ToString();
                    var mat = materialLibrary.Find(m => m.Name.Equals(cellVal, StringComparison.OrdinalIgnoreCase));
                    if (mat != null)
                    {
                        gridLayers.Rows[e.RowIndex].Cells[1].Value = mat.RecommendedThickMm;
                    }
                }
                InitModelFromGrid();
            };

            tlpLayersArea.Controls.Add(gridLayers, 0, 1);

            FlowLayoutPanel pnlLayerBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            btnAddLayer = new Button { Text = "➕ Слой", Width = 110, Height = 28, Font = new Font("Segoe UI", 8.5f) };
            btnRemoveLayer = new Button { Text = "❌ Удалить", Width = 110, Height = 28, Font = new Font("Segoe UI", 8.5f) };

            btnAddLayer.Click += BtnAddLayer_Click;
            btnRemoveLayer.Click += BtnRemoveLayer_Click;

            pnlLayerBtns.Controls.Add(btnAddLayer);
            pnlLayerBtns.Controls.Add(btnRemoveLayer);
            tlpLayersArea.Controls.Add(pnlLayerBtns, 0, 2);

            gbLayers.Controls.Add(tlpLayersArea);
            leftPanel.Controls.Add(gbLayers);

            // Климатические условия РБ
            GroupBox gbBounds = new GroupBox
            {
                Text = "Климатические условия (Беларусь)",
                Width = 350,
                Height = 165,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            TableLayoutPanel tlpBounds = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };

            cbClimatePresets = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Font = new Font("Segoe UI", 8.5f, FontStyle.Regular) };
            cbClimatePresets.Items.AddRange(new string[] { "Минск (-24°C)", "Брест (-21°C)", "Витебск (-25°C)", "Гомель (-23°C)", "Гродно (-22°C)", "Могилев (-24°C)" });
            cbClimatePresets.SelectedIndex = 0;
            cbClimatePresets.SelectedIndexChanged += CbClimatePresets_SelectedIndexChanged;

            numTLeft = CreateNumberInput(-50, 50, -24, "Расчетная температура наиболее холодной пятидневки в Беларуси (СН 2.04.02-2020)");
            numTRight = CreateNumberInput(0, 50, 20, "Норматив +20°C для жилых помещений в РБ (СанПиН / СН)");
            numHumidityIn = CreateNumberInput(10, 100, 55, "Расчетная относительная влажность воздуха в жилье (50-55% по нормам РБ)");

            tlpBounds.Controls.Add(new Label { Text = "Город РБ:", AutoSize = true, Font = new Font("Segoe UI", 8.5f) }, 0, 0);
            tlpBounds.Controls.Add(cbClimatePresets, 1, 0);
            tlpBounds.Controls.Add(new Label { Text = "Улица (x=0), °C:", AutoSize = true, Font = new Font("Segoe UI", 8.5f) }, 0, 1);
            tlpBounds.Controls.Add(numTLeft, 1, 1);
            tlpBounds.Controls.Add(new Label { Text = "Комната (x=L), °C:", AutoSize = true, Font = new Font("Segoe UI", 8.5f) }, 0, 2);
            tlpBounds.Controls.Add(numTRight, 1, 2);
            tlpBounds.Controls.Add(new Label { Text = "Влажность комн., %:", AutoSize = true, Font = new Font("Segoe UI", 8.5f) }, 0, 3);
            tlpBounds.Controls.Add(numHumidityIn, 1, 3);

            gbBounds.Controls.Add(tlpBounds);
            leftPanel.Controls.Add(gbBounds);

            // Время симуляции
            GroupBox gbTime = new GroupBox
            {
                Text = "Параметры симуляции",
                Width = 350,
                Height = 100,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            TableLayoutPanel tlpTime = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            numTotalTime = CreateNumberInput(1, 864000, 86400, "Рекомендуется 86400 сек (24 часа)");
            numDt = CreateNumberInput(1, 600, 10, "Шаг интегрирования (рекомендуется 5-15 сек)");

            tlpTime.Controls.Add(new Label { Text = "Время расчета (сек):", AutoSize = true, Font = new Font("Segoe UI", 8.5f) }, 0, 0);
            tlpTime.Controls.Add(numTotalTime, 1, 0);
            tlpTime.Controls.Add(new Label { Text = "Шаг dt (сек):", AutoSize = true, Font = new Font("Segoe UI", 8.5f) }, 0, 1);
            tlpTime.Controls.Add(numDt, 1, 1);

            gbTime.Controls.Add(tlpTime);
            leftPanel.Controls.Add(gbTime);

            // Кнопки запуска
            FlowLayoutPanel pnlButtons = new FlowLayoutPanel { Width = 350, Height = 50, Margin = new Padding(0, 10, 0, 0) };
            btnStartCalc = new Button { Text = "▶ Запустить динамику", Width = 165, Height = 38, BackColor = Color.DodgerBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            btnStartCalc.Click += btnStart_Click;

            btnStopCalc = new Button { Text = "⏹ Стоп", Width = 155, Height = 38, Enabled = false, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f) };
            btnStopCalc.Click += btnStop_Click;

            pnlButtons.Controls.Add(btnStartCalc);
            pnlButtons.Controls.Add(btnStopCalc);
            leftPanel.Controls.Add(pnlButtons);

            leftContainer.Controls.Add(leftPanel);
            mainLayout.Controls.Add(leftContainer, 0, 0);

            // ==================== 2. ЦЕНТРАЛЬНАЯ ПАНЕЛЬ ====================
            mainTabControl = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Regular) };

            TabPage tabMainView = new TabPage("🔥 2D Тепловое поле и Датчик");
            TableLayoutPanel centerPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            centerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
            centerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));

            GroupBox gbHeatMap = new GroupBox { Text = "Тепловое поле (Кликните мышью для выбора точки датчика)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            picHeatMap = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.Black };
            picHeatMap.MouseClick += PicHeatMap_MouseClick;
            gbHeatMap.Controls.Add(picHeatMap);
            centerPanel.Controls.Add(gbHeatMap, 0, 0);

            GroupBox gbGraph = new GroupBox { Text = "Динамика температуры в точке датчика T(t)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            panelGraphHost = new Panel { Dock = DockStyle.Fill };
            gbGraph.Controls.Add(panelGraphHost);
            centerPanel.Controls.Add(gbGraph, 0, 1);

            tabMainView.Controls.Add(centerPanel);
            mainTabControl.TabPages.Add(tabMainView);

            TabPage tabAnalyticsView = new TabPage("📊 Аналитика и Градиент T(x)");
            TableLayoutPanel analyticsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2 };
            analyticsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            analyticsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            analyticsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
            analyticsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));

            GroupBox gbProfile = new GroupBox { Text = "Температурный профиль по толщине стены T(x)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            plotProfileView = new PlotView { Dock = DockStyle.Fill };
            gbProfile.Controls.Add(plotProfileView);
            analyticsPanel.Controls.Add(gbProfile, 0, 0);

            GroupBox gbRContr = new GroupBox { Text = "Вклад слоев в теплосопротивление R", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            plotRContributionView = new PlotView { Dock = DockStyle.Fill };
            gbRContr.Controls.Add(plotRContributionView);
            analyticsPanel.Controls.Add(gbRContr, 1, 0);

            GroupBox gbMoisture = new GroupBox { Text = "Анализ влажностного режима и точки росы (СН 2.04.01-2020)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            txtMoistureAnalysis = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                Font = new Font("Consolas", 8.5f, FontStyle.Regular)
            };
            gbMoisture.Controls.Add(txtMoistureAnalysis);
            analyticsPanel.Controls.Add(gbMoisture, 0, 1);
            analyticsPanel.SetColumnSpan(gbMoisture, 2);

            tabAnalyticsView.Controls.Add(analyticsPanel);
            mainTabControl.TabPages.Add(tabAnalyticsView);

            mainLayout.Controls.Add(mainTabControl, 1, 0);

            // ==================== 3. ПРАВАЯ ПАНЕЛЬ ИНФОРМАЦИИ ====================
            FlowLayoutPanel rightPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };

            GroupBox gbEngineering = new GroupBox { Text = "Инженерные показатели РБ", Width = 300, Height = 210, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            TableLayoutPanel tlpEng = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(5) };

            lblRTotal = new Label { Text = "R = 0.0 м²·К/Вт", AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.DarkBlue };
            lblHeatLoss = new Label { Text = "q = 0.0 Вт/м²", AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.DarkRed };
            lblDewPointStatus = new Label { Text = "Точка росы: -", AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            lblSelectedPointInfo = new Label { Text = "Датчик: X = 0.20 м | T = 0 °C", AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Italic) };

            lblCalcStatus = new Label { Text = "Статус: Готов", AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            lblCalcTime = new Label { Text = "Время: 00:00:00", AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            lblCalcSteps = new Label { Text = "Шагов: 0", AutoSize = true, Font = new Font("Segoe UI", 8.5f) };

            tlpEng.Controls.Add(lblRTotal, 0, 0);
            tlpEng.Controls.Add(lblHeatLoss, 0, 1);
            tlpEng.Controls.Add(lblDewPointStatus, 0, 2);
            tlpEng.Controls.Add(lblSelectedPointInfo, 0, 3);
            tlpEng.Controls.Add(lblCalcStatus, 0, 4);
            tlpEng.Controls.Add(lblCalcSteps, 0, 5);

            gbEngineering.Controls.Add(tlpEng);
            rightPanel.Controls.Add(gbEngineering);

            GroupBox gbAdvice = new GroupBox { Text = "Нормы СН 2.04.01-2020 (РБ)", Width = 300, Height = 280, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            txtEngineeringAdvice = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };
            gbAdvice.Controls.Add(txtEngineeringAdvice);
            rightPanel.Controls.Add(gbAdvice);

            btnExportReport = new Button
            {
                Text = "📄 Сформировать отчёт (РБ)",
                Width = 300,
                Height = 40,
                BackColor = Color.ForestGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(0, 15, 0, 0)
            };
            btnExportReport.Click += BtnExportReport_Click;
            rightPanel.Controls.Add(btnExportReport);

            mainLayout.Controls.Add(rightPanel, 2, 0);
            this.Controls.Add(mainLayout);
        }

        private NumericUpDown CreateNumberInput(decimal min, decimal max, decimal val, string tipText)
        {
            var num = new NumericUpDown { Minimum = min, Maximum = max, Value = val, Width = 80, Font = new Font("Segoe UI", 8.5f, FontStyle.Regular) };
            if (!string.IsNullOrEmpty(tipText))
            {
                toolTip.SetToolTip(num, tipText);
            }
            num.ValueChanged += (s, e) => InitModelFromGrid();
            return num;
        }

        private void SetupPlots()
        {
            plotTimeView = new PlotView { Dock = DockStyle.Fill };
            panelGraphHost.Controls.Add(plotTimeView);

            var modelTime = new PlotModel { Title = "Температура T(t) в месте установки датчика" };
            timeSeries = new LineSeries { Title = "T (°C)", Color = OxyColors.Red, StrokeThickness = 2 };
            modelTime.Series.Add(timeSeries);
            plotTimeView.Model = modelTime;

            var modelProfile = new PlotModel { Title = "Градиент температуры по слоям стены" };
            profileSeries = new LineSeries { Title = "T(x)", Color = OxyColors.Blue, StrokeThickness = 3 };
            modelProfile.Series.Add(profileSeries);
            plotProfileView.Model = modelProfile;
        }

        private void CbClimatePresets_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (cbClimatePresets.SelectedIndex)
            {
                case 0: numTLeft.Value = -24; numTRight.Value = 20; numHumidityIn.Value = 55; break; // Минск
                case 1: numTLeft.Value = -21; numTRight.Value = 20; numHumidityIn.Value = 55; break; // Брест
                case 2: numTLeft.Value = -25; numTRight.Value = 20; numHumidityIn.Value = 50; break; // Витебск
                case 3: numTLeft.Value = -23; numTRight.Value = 20; numHumidityIn.Value = 55; break; // Гомель
                case 4: numTLeft.Value = -22; numTRight.Value = 20; numHumidityIn.Value = 55; break; // Гродно
                case 5: numTLeft.Value = -24; numTRight.Value = 20; numHumidityIn.Value = 50; break; // Могилев
            }
        }

        private void BtnAddLayer_Click(object sender, EventArgs e)
        {
            gridLayers.Rows.Add("Минеральная вата (Фасад)", "100");
            InitModelFromGrid();
        }

        private void BtnRemoveLayer_Click(object sender, EventArgs e)
        {
            if (gridLayers.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in gridLayers.SelectedRows)
                {
                    if (!row.IsNewRow) gridLayers.Rows.Remove(row);
                }
            }
            else if (gridLayers.Rows.Count > 0)
            {
                gridLayers.Rows.RemoveAt(gridLayers.Rows.Count - 1);
            }
            InitModelFromGrid();
        }

        private void CbPresets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbPresets.SelectedIndex <= 0) return;

            gridLayers.Rows.Clear();

            switch (cbPresets.SelectedIndex)
            {
                case 1: // Газосиликат + Минвата + Штукатурка
                    gridLayers.Rows.Add("Блоки газосиликатные (D500)", "400");
                    gridLayers.Rows.Add("Минеральная вата (Фасад)", "100");
                    gridLayers.Rows.Add("Штукатурка защитно-отделочная", "15");
                    break;
                case 2: // Кирпич + XPS
                    gridLayers.Rows.Add("Кирпич керамический", "380");
                    gridLayers.Rows.Add("Пенополистирол (XPS)", "120");
                    gridLayers.Rows.Add("Штукатурка защитно-отделочная", "15");
                    break;
                case 3: // Панельный дом (Беларусь)
                    gridLayers.Rows.Add("Железобетон", "160");
                    gridLayers.Rows.Add("Минеральная вата (Фасад)", "140");
                    gridLayers.Rows.Add("Железобетон", "80");
                    break;
                case 4: // Брус + Минвата
                    gridLayers.Rows.Add("Дерево (брус сосна/ель)", "150");
                    gridLayers.Rows.Add("Минеральная вата (Фасад)", "100");
                    gridLayers.Rows.Add("Штукатурка защитно-отделочная", "15");
                    break;
            }

            InitModelFromGrid();
        }

        private void PicHeatMap_MouseClick(object sender, MouseEventArgs e)
        {
            if (picHeatMap.Image == null) return;

            double pctX = (double)e.X / picHeatMap.Width;
            double pctY = (double)e.Y / picHeatMap.Height;

            int gridI = Math.Max(0, Math.Min(Nx - 1, (int)(pctX * Nx)));
            int gridJ = Math.Max(0, Math.Min(Ny - 1, (int)((1.0 - pctY) * Ny)));

            selectedPoint = new Point(gridI, gridJ);
            double currentX = (gridI / (double)(Nx - 1)) * Lx;

            lblSelectedPointInfo.Text = $"Датчик: X = {currentX:F2} м | T = {T[gridI, gridJ]:F1} °C";
            timeSeries.Points.Clear();
            plotTimeView.Model.InvalidatePlot(true);
            RenderHeatMap();
        }

        private void CalculateThermalResistance()
        {
            double rTotal = 0.13 + 0.04; // R_si + R_se по СН 2.04.01-2020
            double totalThickM = 0;

            foreach (DataGridViewRow row in gridLayers.Rows)
            {
                if (row.Cells[0].Value == null || row.Cells[1].Value == null) continue;

                string matName = row.Cells[0].Value.ToString();
                if (double.TryParse(row.Cells[1].Value.ToString(), out double thickMm))
                {
                    var mat = materialLibrary.Find(m => m.Name.Equals(matName, StringComparison.OrdinalIgnoreCase));
                    if (mat != null && mat.Lambda > 0)
                    {
                        double thickM = thickMm / 1000.0;
                        rTotal += thickM / mat.Lambda;
                        totalThickM += thickM;
                    }
                }
            }

            Lx = totalThickM > 0 ? totalThickM : 0.4;
            double deltaT = Math.Abs((double)numTRight.Value - (double)numTLeft.Value);
            double q = deltaT / rTotal;

            lblRTotal.Text = $"R = {rTotal:F3} м²·К/Вт";
            lblHeatLoss.Text = $"q = {q:F1} Вт/м²";

            double tIn = (double)numTRight.Value;
            double hum = (double)numHumidityIn.Value;
            double dewPoint = tIn - ((100 - hum) / 5.0);

            lblDewPointStatus.Text = $"Точка росы: {dewPoint:F1} °C";
            lblDewPointStatus.ForeColor = dewPoint > 0 ? Color.DarkOrange : Color.Green;

            StringBuilder advice = new StringBuilder();
            advice.AppendLine($"Толщина стены: {Lx * 1000:F0} мм");
            advice.AppendLine($"Сопротивление R = {rTotal:F2} м²·К/Вт");

            // Норматив Республики Беларусь
            double reqR = 3.20;
            if (rTotal >= reqR)
            {
                advice.AppendLine($"✔ Соответствует норме СН 2.04.01-2020 (РБ) (R ≥ {reqR}).");
            }
            else
            {
                advice.AppendLine($"⚠️ Ниже нормы СН 2.04.01-2020 (R < {reqR}). Увеличьте утеплитель на ~{(reqR - rTotal) * 0.04 * 1000:F0} мм.");
            }

            advice.AppendLine();
            advice.AppendLine($"При Tвнутр = {tIn}°C и {hum}% влаги роса выпадает при {dewPoint:F1}°C.");

            txtEngineeringAdvice.Text = advice.ToString();
        }

        private void UpdateAnalyticsTab()
        {
            if (T == null || plotProfileView == null) return;

            // 1. Построение профиля T(x)
            profileSeries.Points.Clear();
            int midY = Ny / 2;
            double dx = Lx / (Nx - 1);

            for (int i = 0; i < Nx; i++)
            {
                double posX = i * dx;
                profileSeries.Points.Add(new DataPoint(posX, T[i, midY]));
            }
            plotProfileView.Model.InvalidatePlot(true);

            // 2. Вклад в R
            var rCategoryModel = new PlotModel { Title = "Сопротивление слоёв R (м²·К/Вт)" };
            var barSeries = new BarSeries
            {
                Title = "R слоя",
                FillColor = OxyColors.DodgerBlue,
                StrokeColor = OxyColors.DarkBlue,
                StrokeThickness = 1
            };

            StringBuilder sbMoisture = new StringBuilder();
            sbMoisture.AppendLine("=== ПОСЛОЙНЫЙ РАЗБОР ТЕМПЕРАТУР И ВЛАЖНОСТИ (НОРМЫ РБ) ===");
            sbMoisture.AppendLine($"Т улицы = {numTLeft.Value}°C | Т комнаты = {numTRight.Value}°C (Влажность {numHumidityIn.Value}%)");

            double dewPoint = (double)numTRight.Value - ((100 - (double)numHumidityIn.Value) / 5.0);
            sbMoisture.AppendLine($"Расчетная точка росы: {dewPoint:F1} °C\r\n");

            double currentT = (double)numTLeft.Value;
            double deltaTTotal = (double)numTRight.Value - (double)numTLeft.Value;

            double rTotal = 0.13 + 0.04;
            foreach (DataGridViewRow row in gridLayers.Rows)
            {
                if (row.Cells[0].Value == null || row.Cells[1].Value == null) continue;
                string matName = row.Cells[0].Value.ToString();
                if (double.TryParse(row.Cells[1].Value.ToString(), out double thickMm))
                {
                    var mat = materialLibrary.Find(m => m.Name.Equals(matName, StringComparison.OrdinalIgnoreCase));
                    if (mat != null) rTotal += (thickMm / 1000.0) / mat.Lambda;
                }
            }

            int layerIdx = 1;
            foreach (DataGridViewRow row in gridLayers.Rows)
            {
                if (row.Cells[0].Value == null || row.Cells[1].Value == null) continue;

                string matName = row.Cells[0].Value.ToString();
                if (double.TryParse(row.Cells[1].Value.ToString(), out double thickMm))
                {
                    var mat = materialLibrary.Find(m => m.Name.Equals(matName, StringComparison.OrdinalIgnoreCase));
                    if (mat != null)
                    {
                        double rLayer = (thickMm / 1000.0) / mat.Lambda;
                        double dropT = deltaTTotal * (rLayer / rTotal);
                        double nextT = currentT + dropT;

                        barSeries.Items.Add(new BarItem { Value = rLayer });

                        sbMoisture.AppendLine($"Слой {layerIdx++}: [{matName}] ({thickMm} мм)");
                        sbMoisture.AppendLine($"  • Температура в слое: от {currentT:F1} °C до {nextT:F1} °C");
                        sbMoisture.AppendLine($"  • Сопротивление R слоя: {rLayer:F3} м²·К/Вт");

                        if ((currentT <= dewPoint && nextT >= dewPoint) || (currentT >= dewPoint && nextT <= dewPoint))
                        {
                            sbMoisture.AppendLine($"  ⚠️ ВНИМАНИЕ: Граница точки росы ({dewPoint:F1}°C) проходит ВНУТРИ этого слоя!");
                        }

                        sbMoisture.AppendLine(new string('-', 50));
                        currentT = nextT;
                    }
                }
            }

            rCategoryModel.Series.Add(barSeries);
            plotRContributionView.Model = rCategoryModel;
            plotRContributionView.Model.InvalidatePlot(true);

            txtMoistureAnalysis.Text = sbMoisture.ToString();
        }

        private void InitModelFromGrid()
        {
            T = new double[Nx, Ny];
            K = new double[Nx, Ny];
            C = new double[Nx, Ny];
            T_new = new double[Nx, Ny];

            CalculateThermalResistance();

            double dx = Lx / (Nx - 1);
            double tLeft = (double)numTLeft.Value;
            double tRight = (double)numTRight.Value;

            for (int i = 0; i < Nx; i++)
            {
                double currentX = i * dx;
                double accumulatedX = 0;

                MaterialItem currentMat = materialLibrary.Count > 0 ? materialLibrary[0] : null;

                foreach (DataGridViewRow row in gridLayers.Rows)
                {
                    if (row.Cells[0].Value == null || row.Cells[1].Value == null) continue;

                    if (double.TryParse(row.Cells[1].Value.ToString(), out double thickMm))
                    {
                        double thickM = thickMm / 1000.0;
                        accumulatedX += thickM;

                        string selectedMatName = row.Cells[0].Value.ToString();
                        var foundMat = materialLibrary.Find(m => m.Name.Equals(selectedMatName, StringComparison.OrdinalIgnoreCase));

                        if (foundMat != null) currentMat = foundMat;
                        if (currentX <= accumulatedX) break;
                    }
                }

                if (currentMat == null && materialLibrary.Count > 0) currentMat = materialLibrary[0];

                for (int j = 0; j < Ny; j++)
                {
                    K[i, j] = currentMat != null ? currentMat.Lambda : 1.0;
                    C[i, j] = currentMat != null ? (currentMat.Density * currentMat.HeatCapacity) : 2000000.0;

                    // Изначально вся стена теплая (комнатная T), чтобы был зафиксирован процесс остывания/прогревания
                    T[i, j] = tRight;
                }
            }

            ApplyBoundaryConditions();
            RenderHeatMap();
            UpdateAnalyticsTab();

            if (lblSelectedPointInfo != null)
            {
                lblSelectedPointInfo.Text = $"Датчик: X = {(selectedPoint.X / (double)(Nx - 1)) * Lx:F2} м | T = {T[selectedPoint.X, selectedPoint.Y]:F1} °C";
            }
        }

        private void ApplyBoundaryConditions()
        {
            double tLeft = (double)numTLeft.Value;
            double tRight = (double)numTRight.Value;

            for (int j = 0; j < Ny; j++)
            {
                T[0, j] = tLeft;
                T[Nx - 1, j] = tRight;
            }
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (isRunning) return;

            InitModelFromGrid();

            isRunning = true;
            btnStartCalc.Enabled = false;
            btnStopCalc.Enabled = true;

            double totalT = (double)numTotalTime.Value;
            double dtStep = (double)numDt.Value;
            double dx = Lx / (Nx - 1);
            double dy = Ly / (Ny - 1);

            double currentTime = 0;
            int step = 0;
            int totalSteps = (int)(totalT / dtStep);

            lblCalcStatus.Text = "Статус: Расчет динамики...";
            lblCalcStatus.ForeColor = Color.Green;
            timeSeries.Points.Clear();

            DateTime startTime = DateTime.Now;

            await Task.Run(() =>
            {
                while (currentTime < totalT && isRunning)
                {
                    for (int i = 1; i < Nx - 1; i++)
                    {
                        for (int j = 1; j < Ny - 1; j++)
                        {
                            double k_val = K[i, j];
                            double c_val = C[i, j];

                            double alpha = (k_val / c_val) * dtStep;

                            double d2T_dx2 = (T[i + 1, j] - 2 * T[i, j] + T[i - 1, j]) / (dx * dx);
                            double d2T_dy2 = (T[i, j + 1] - 2 * T[i, j] + T[i, j - 1]) / (dy * dy);

                            T_new[i, j] = T[i, j] + alpha * (d2T_dx2 + d2T_dy2);
                        }
                    }

                    for (int i = 1; i < Nx - 1; i++)
                        for (int j = 1; j < Ny - 1; j++)
                            T[i, j] = T_new[i, j];

                    for (int i = 0; i < Nx; i++)
                    {
                        T[i, 0] = T[i, 1];
                        T[i, Ny - 1] = T[i, Ny - 2];
                    }

                    currentTime += dtStep;
                    step++;

                    if (step % 20 == 0 || currentTime >= totalT)
                    {
                        double sensorT = T[selectedPoint.X, selectedPoint.Y];
                        double timeSnap = currentTime / 3600.0;
                        TimeSpan elapsed = DateTime.Now - startTime;

                        this.Invoke((MethodInvoker)delegate
                        {
                            lblCalcTime.Text = "Время: " + elapsed.ToString(@"hh\:mm\:ss");
                            lblCalcSteps.Text = $"Шаг: {step} / {totalSteps}";
                            lblSelectedPointInfo.Text = $"Датчик: X = {(selectedPoint.X / (double)(Nx - 1)) * Lx:F2} м | T = {sensorT:F1} °C";

                            timeSeries.Points.Add(new DataPoint(timeSnap, sensorT));
                            plotTimeView.Model.InvalidatePlot(true);
                            RenderHeatMap();
                            UpdateAnalyticsTab();
                        });
                    }
                }
            });

            isRunning = false;
            btnStartCalc.Enabled = true;
            btnStopCalc.Enabled = false;
            lblCalcStatus.Text = "Статус: Завершено";
            lblCalcStatus.ForeColor = Color.DarkGreen;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            isRunning = false;
            lblCalcStatus.Text = "Статус: Остановлено";
            lblCalcStatus.ForeColor = Color.Red;
        }

        private void RenderHeatMap()
        {
            if (picHeatMap == null || picHeatMap.Width <= 0 || picHeatMap.Height <= 0) return;

            Bitmap bmp = new Bitmap(Nx, Ny);

            double tMin = (double)numTLeft.Value;
            double tMax = (double)numTRight.Value;

            if (tMin >= tMax) tMax = tMin + 1.0;

            for (int i = 0; i < Nx; i++)
            {
                for (int j = 0; j < Ny; j++)
                {
                    double temp = T[i, j];
                    Color c = GetTemperatureColor(temp, tMin, tMax);
                    bmp.SetPixel(i, Ny - 1 - j, c);
                }
            }

            // Маленький аккуратный датчик
            using (Graphics g = Graphics.FromImage(bmp))
            {
                int px = selectedPoint.X;
                int py = Ny - 1 - selectedPoint.Y;

                int r = 1; // Радиус микро-точки
                using (Pen blackPen = new Pen(Color.Black, 1))
                using (SolidBrush whiteBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(whiteBrush, px - r, py - r, r * 2 + 1, r * 2 + 1);
                    g.DrawEllipse(blackPen, px - r - 1, py - r - 1, (r + 1) * 2, (r + 1) * 2);
                }
            }

            if (picHeatMap.Image != null)
            {
                var oldImg = picHeatMap.Image;
                picHeatMap.Image = null;
                oldImg.Dispose();
            }

            picHeatMap.Image = new Bitmap((Image)bmp, new Size(picHeatMap.Width, picHeatMap.Height));
            bmp.Dispose();
        }

        private Color GetTemperatureColor(double val, double min, double max)
        {
            if (double.IsNaN(val) || double.IsInfinity(val)) val = min;
            if (Math.Abs(max - min) < 0.0001) max = min + 1.0;

            double norm = (val - min) / (max - min);
            if (double.IsNaN(norm)) norm = 0.0;
            norm = Math.Max(0.0, Math.Min(1.0, norm));

            int r = Math.Max(0, Math.Min(255, (int)(255 * norm)));
            int b = Math.Max(0, Math.Min(255, (int)(255 * (1.0 - norm))));
            int g = Math.Max(0, Math.Min(255, (int)(255 * (1.0 - Math.Abs(norm - 0.5) * 2.0))));

            return Color.FromArgb(r, g, b);
        }

        private void BtnExportReport_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
                sfd.FileName = $"Расчет_Теплотехника_РБ_{DateTime.Now:yyyyMMdd_HHmm}.txt";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    StringBuilder report = new StringBuilder();
                    report.AppendLine("=========================================================");
                    report.AppendLine("   ОТЧЁТ О ТЕПЛОТЕХНИЧЕСКОМ РАСЧЁТЕ (СН 2.04.01-2020 РБ) ");
                    report.AppendLine("=========================================================");
                    report.AppendLine($"Дата проведения расчета: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                    report.AppendLine();

                    report.AppendLine("1. СОСТАВ ОГРАЖДАЮЩЕЙ КОНСТРУКЦИИ:");
                    int idx = 1;
                    foreach (DataGridViewRow row in gridLayers.Rows)
                    {
                        if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                        {
                            report.AppendLine($"   Слой {idx++}: {row.Cells[0].Value} - {row.Cells[1].Value} мм");
                        }
                    }
                    report.AppendLine($"   Общая толщина стены: {Lx * 1000:F0} мм");
                    report.AppendLine();

                    report.AppendLine("2. КЛИМАТИЧЕСКИЕ УСЛОВИЯ И РЕГИОН РБ:");
                    report.AppendLine($"   Населенный пункт: {cbClimatePresets.SelectedItem}");
                    report.AppendLine($"   Наружная температура (x=0): {numTLeft.Value} °C");
                    report.AppendLine($"   Внутренняя температура (x=L): {numTRight.Value} °C");
                    report.AppendLine($"   Относительная влажность внутри: {numHumidityIn.Value} %");
                    report.AppendLine();

                    report.AppendLine("3. ИНЖЕНЕРНЫЕ РАСЧЕТНЫЕ ПОКАЗАТЕЛИ:");
                    report.AppendLine($"   Сопротивление теплопередаче (R): {lblRTotal.Text}");
                    report.AppendLine($"   Норматив РБ (СН 2.04.01-2020): R_req = 3.20 м²·К/Вт");
                    report.AppendLine($"   Плотность теплового потока (q): {lblHeatLoss.Text}");
                    report.AppendLine($"   {lblDewPointStatus.Text}");
                    report.AppendLine($"   Положение датчика контроля: {lblSelectedPointInfo.Text}");
                    report.AppendLine();

                    report.AppendLine("4. ВЛАЖНОСТНЫЙ АНАЛИЗ И ТЕМПЕРАТУРНЫЙ ГРАДИЕНТ:");
                    report.AppendLine(txtMoistureAnalysis.Text);
                    report.AppendLine();

                    report.AppendLine("5. ВЫВОДЫ И ЗАКЛЮЧЕНИЕ:");
                    report.AppendLine(txtEngineeringAdvice.Text);
                    report.AppendLine("=========================================================");

                    File.WriteAllText(sfd.FileName, report.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Отчёт сохранён по нормам РБ:\n{sfd.FileName}", "Экспорт завершён", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}