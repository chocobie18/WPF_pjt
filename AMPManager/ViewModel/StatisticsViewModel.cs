using System;
using System.Windows.Input;
using AMPManager.Core;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace AMPManager.ViewModel
{
    public class StatisticsViewModel : BaseViewModel
    {
        private DatabaseManager _dbManager = new DatabaseManager();

        // 1. 기간 선택
        private DateTime _startDate = DateTime.Now.AddDays(-7);
        private DateTime _endDate = DateTime.Now;

        public DateTime StartDate
        {
            get => _startDate;
            set { SetProperty(ref _startDate, value); LoadChartData(); }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set { SetProperty(ref _endDate, value); LoadChartData(); }
        }

        public ICommand SearchCommand { get; }

        // 2. 그래프 모델
        private PlotModel _defectRateModel;
        public PlotModel DefectRateModel
        {
            get => _defectRateModel;
            set => SetProperty(ref _defectRateModel, value);
        }

        // 3. 통계 수치
        private string _avgWidth = "-";
        private string _avgLength = "-";
        private string _avgContour = "-";
        private string _avgCenter = "-";

        public string AvgWidth { get => _avgWidth; set => SetProperty(ref _avgWidth, value); }
        public string AvgLength { get => _avgLength; set => SetProperty(ref _avgLength, value); }
        public string AvgContour { get => _avgContour; set => SetProperty(ref _avgContour, value); }
        public string AvgCenter { get => _avgCenter; set => SetProperty(ref _avgCenter, value); }

        public StatisticsViewModel()
        {
            SearchCommand = new RelayCommand(o => LoadChartData());

            InitializeChart();
            LoadChartData();
        }

        private void InitializeChart()
        {
            var model = new PlotModel { Title = "" };
            var textColor = OxyColor.Parse("#E0E0E0");
            var gridColor = OxyColor.Parse("#4A4A5A");

            model.Background = OxyColors.Transparent;
            model.PlotAreaBorderColor = OxyColors.Transparent;
            model.TextColor = textColor;

            // X축 (날짜)
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "MM/dd",
                AxislineColor = gridColor,
                TicklineColor = gridColor,
                TextColor = textColor,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = gridColor
            });

            // [수정된 부분] Y축 (불량률 %) - 절대 범위 설정 추가!
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "불량률(%)",
                Minimum = 0,    // 시작할 때 보이는 최소값
                Maximum = 100,  // 시작할 때 보이는 최대값

                // ★ 여기가 핵심입니다 ★
                AbsoluteMinimum = 0,   // 아무리 축소해도 0 밑으로 안 내려감
                AbsoluteMaximum = 100, // 아무리 축소해도 100 위로 안 올라감

                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = gridColor,
                AxislineColor = gridColor,
                TextColor = textColor
            });

            DefectRateModel = model;
        }

        private void LoadChartData()
        {
            var dailyRates = _dbManager.GetDailyDefectRates(StartDate, EndDate);
            var averages = _dbManager.GetAverageSpecs();

            AvgWidth = $"{averages.w:F2} mm";
            AvgLength = $"{averages.l:F2} mm";
            AvgContour = $"{averages.c:F2}";
            AvgCenter = $"{averages.cp:F1}";

            if (DefectRateModel != null)
            {
                DefectRateModel.Series.Clear();

                var lineSeries = new LineSeries
                {
                    Color = OxyColor.Parse("#00C1D4"),
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    MarkerStroke = OxyColor.Parse("#00C1D4"),
                    MarkerFill = OxyColor.Parse("#2F2F3D"),
                    StrokeThickness = 3
                };

                foreach (var item in dailyRates)
                {
                    if (DateTime.TryParse(item.Key, out DateTime dt))
                    {
                        lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(dt), item.Value));
                    }
                }

                DefectRateModel.Series.Add(lineSeries);
                DefectRateModel.InvalidatePlot(true);
            }
        }
    }
}