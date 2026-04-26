namespace WindowsGSM.Functions
{
    public class CustomServerSetting
    {
        public CustomServerSetting()
        {
        }

        public CustomServerSetting(string key)
        {
            Key = key;
            Label = key;
        }

        public CustomServerSetting(string key, string label, string defaultValue = "")
        {
            Key = key;
            Label = label;
            DefaultValue = defaultValue;
        }

        public CustomServerSetting(string key, string label, string defaultValue, params string[] options)
            : this(key, label, defaultValue)
        {
            Options = options;
        }

        public string Key { get; set; }
        public string Label { get; set; }
        public string DefaultValue { get; set; } = string.Empty;
        public string[] Options { get; set; } = new string[0];
    }
}
