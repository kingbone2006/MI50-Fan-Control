using WpfUserControl = System.Windows.Controls.UserControl;

namespace MI50FanControl.Views
{
    public partial class CurveEditorView : WpfUserControl
    {
        public CurveEditorView()
        {
            InitializeComponent();
            GraphVisualizer.CurveChanged += () =>
            {
                if (DataContext is ViewModels.CurveEditorViewModel vm)
                {
                    vm.SyncPointsFromProfile();
                }
            };
        }
    }
}
