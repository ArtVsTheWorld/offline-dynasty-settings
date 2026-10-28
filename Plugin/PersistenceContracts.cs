using System;
using System.Collections.Generic;

namespace OfflineDynastySettings
{
    internal sealed class MemoryCheck
    {
        public long Address { get; set; }
        public string Bytes { get; set; }
        public MemoryCheck() { }
        internal MemoryCheck(long address, byte[] bytes) { Address = address; Bytes = Convert.ToBase64String(bytes); }
    }
    internal sealed class PersistenceContext
    {
        // Includes clock, playcall and controller entries, bodies and native tails.
        internal const int MaximumChecks = 128;
        public PersistenceContext() { }
        public int Format { get; set; }
        public int Pid { get; set; }
        public long Started { get; set; }
        public long ImageBase { get; set; }
        public long ClockData { get; set; }
        public long SplitData { get; set; }
        public string PluginFolder { get; set; }
        public string ReadyEvent { get; set; }
        public List<MemoryCheck> Checks { get; set; }
    }
}
