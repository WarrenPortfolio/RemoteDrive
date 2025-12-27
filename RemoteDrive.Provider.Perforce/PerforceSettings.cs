namespace RemoteDrive.Provider.Perforce
{
    public class PerforceSettings
    {
        public string ServerAndPort { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string ClientName { get; set; }

        public string AppName { get; set; }
        public string AppVersion { get; set; }
    }
}