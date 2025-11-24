using System;
using AMPManager.Core;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace AMPManager.ViewModel
{
    public class StatisticsViewModel : BaseViewModel
    {
        // 1. 기간 선택 (시작일, 종료일)
        public DateTime StartDate { get; set; } = DateTime.Now.AddDays(-7);
        public DateTime EndDate { get; set; } = DateTime.Now;

        // 2. 그래프 모델 (꺾은선 그래프)
        public PlotModel DefectRateModel { get; private set; }

        // 3. 하단 4개 통계 수치 (DB의 Product 테이블 컬럼 기준)
        public string AvgWidth { get; set; } = "6.05 mm";       // 평균 넓이
        public string AvgLength { get; set; } = "20.15 mm";     // 평균 길이
        public string AvgContour { get; set; } = "50.20";       // 평균 외곽선
        public string AvgCenter { get; set; } = "100.5";        // 평균 무게중심

        public StatisticsViewModel()
        {
            // 그래프 디자인 및 더미 데이터 생성
            CreateChart();
        }

        private void CreateChart()
        {
            var model = new PlotModel { Title = "" };

            // 색상 테마 (기존 앱 디자인 유지)
            var accentColor = OxyColor.Parse("#00C1D4");
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
                TextColor = textColor
            });

            // Y축 (불량률 %)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "불량률(%)",
                Minimum = 0,
                Maximum = 100,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = gridColor,
                AxislineColor = gridColor,
                TicklineColor = gridColor,
                TextColor = textColor
            });

            // 데이터 시리즈 (꺾은선)
            var lineSeries = new LineSeries
            {
                Color = accentColor,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerStroke = accentColor,
                MarkerFill = OxyColor.Parse("#2F2F3D"), // 배경색과 맞춤
                StrokeThickness = 3
            };

            // [테스트 데이터] 최근 7일간 불량률 (나중에 DB 연동)
            lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now.AddDays(-6)), 5));
            lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now.AddDays(-5)), 12));
            lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now.AddDays(-4)), 8));
            lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now.AddDays(-3)), 25)); // 튀는 값
            lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now.AddDays(-2)), 4));
            lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now.AddDays(-1)), 2));
            lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), 6));

            model.Series.Add(lineSeries);
            DefectRateModel = model;
        }
    }
}