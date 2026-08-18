using System.Collections.Specialized;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace MI50FanControl.Views
{
    public partial class DevLogView : WpfUserControl
    {
        public DevLogView()
        {
            InitializeComponent();

            ((INotifyCollectionChanged)LogListBox.Items).CollectionChanged += (s, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add && LogListBox.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                }
            };
        }
    }
}
