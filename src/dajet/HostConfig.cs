namespace DaJet.Stream
{
    internal sealed class HostConfig
    {
        public uint LogSize { get; set; } = 1024U * 1024U; // kilobytes
        public string LogFile { get; set; } = "dajet.log";
        public string LogPath { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        public int Refresh { get; set; } = 600; // seconds
    }
}