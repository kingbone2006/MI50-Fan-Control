using System;
using System.Windows.Markup;
using MI50FanControl.Services;
using WpfBinding = System.Windows.Data.Binding;

namespace MI50FanControl.Converters
{
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        public LocExtension() { }
        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return string.Empty;

            var binding = new WpfBinding($"[{Key}]")
            {
                Source = LocalizationService.Instance,
                Mode = System.Windows.Data.BindingMode.OneWay
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
