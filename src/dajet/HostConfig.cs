namespace DaJet.Runtime
{
    internal sealed class HostConfig
    {
        public uint LogSize { get; set; } = 1024U * 1024U; // bytes
        public string LogFile { get; set; } = "dajet.log";
        public string LogPath { get; set; } = string.Empty;
        public string LogTarget { get; set; } = "file"; // "file" (default) | "console"
        public string RootPath { get; set; } = string.Empty;
        public int Refresh { get; set; } = 600; // seconds
        public int ShutdownTimeout { get; set; } = 30; // seconds - wait for streams to stop
    }
}
