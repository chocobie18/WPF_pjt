using AMPManager.Model;
using Microsoft.VisualBasic.Logging;
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
using System.Windows.Shapes;


namespace AMPManager.View
{
    public partial class LogDetailWindow : Window
    {
        public LogDetailWindow(LogEntry log)
        {
            InitializeComponent();

            // [수정] Brushes 앞에 'System.Windows.Media.'를 붙여서 WPF용임을 확실히 합니다.
            log.GetType().GetProperty("StatusColor")?.SetValue(log,
                log.Status == "불량" ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.MintCream);

            this.DataContext = log;
        }
    }
}