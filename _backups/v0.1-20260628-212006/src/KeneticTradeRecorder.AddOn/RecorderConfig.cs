// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.AddOn - RecorderConfig.cs
//
//  PURPOSE:        Runtime configuration for the recorder, including the account
//                  filter (the v0.1 form of the "account selector"). Loaded from a
//                  JSON file in the recorder data directory; if absent, safe
//                  defaults are used (record ALL accounts, observe-only).
//
//  THREAD SAFETY:  Treated as immutable after load. The AddOn loads it once at
//                  startup; hot-reload is intentionally out of scope for v0.1.
//
//  NOTE ON SERIALIZATION: Uses System.Runtime.Serialization's DataContract +
//                  DataContractJsonSerializer, which ship with .NET Framework 4.8
//                  (NinjaTrader's runtime) and require NO external NuGet package.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace KeneticTradeRecorder.AddOn
{
    /// <summary>Account-filter mode for the recorder.</summary>
    public enum AccountFilterMode
    {
        /// <summary>Record every connected account (default).</summary>
        All = 0,
        /// <summary>Record only accounts whose name is in <see cref="RecorderConfig.Accounts"/>.</summary>
        IncludeOnly = 1,
        /// <summary>Record every account EXCEPT those in <see cref="RecorderConfig.Accounts"/>.</summary>
        Exclude = 2
    }

    /// <summary>Serializable recorder configuration. See header for load semantics.</summary>
    [DataContract]
    public sealed class RecorderConfig
    {
        /// <summary>Filter mode controlling which accounts are recorded.</summary>
        [DataMember(Order = 0)] public AccountFilterMode FilterMode { get; set; } = AccountFilterMode.All;

        /// <summary>Account names used by IncludeOnly / Exclude modes.</summary>
        [DataMember(Order = 1)] public List<string> Accounts { get; set; } = new List<string>();

        /// <summary>Absolute path to the directory where the recorder writes its data/logs.</summary>
        [DataMember(Order = 2)] public string DataDirectory { get; set; } = string.Empty;

        /// <summary>If true, also echo detected legs to the NinjaTrader Output window.</summary>
        [DataMember(Order = 3)] public bool EchoToOutputWindow { get; set; } = true;

        /// <summary>Decides whether a given account name should be recorded under the current filter.</summary>
        public bool ShouldRecord(string accountName)
        {
            string name = accountName ?? string.Empty;
            switch (FilterMode)
            {
                case AccountFilterMode.IncludeOnly:
                    return Accounts != null && Accounts.Contains(name);
                case AccountFilterMode.Exclude:
                    return Accounts == null || !Accounts.Contains(name);
                case AccountFilterMode.All:
                default:
                    return true;
            }
        }

        /// <summary>Loads config from <paramref name="path"/>, returning defaults if the file is missing or unreadable.</summary>
        public static RecorderConfig LoadOrDefault(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (var fs = File.OpenRead(path))
                    {
                        var ser = new DataContractJsonSerializer(typeof(RecorderConfig));
                        var cfg = (RecorderConfig?)ser.ReadObject(fs);
                        if (cfg != null)
                        {
                            cfg.Accounts ??= new List<string>();
                            return cfg;
                        }
                    }
                }
            }
            catch
            {
                // Intentionally swallow: a bad config must never prevent recording.
                // Callers may log the failure via their own diagnostics.
            }
            return new RecorderConfig();
        }

        /// <summary>Writes a default config file to <paramref name="path"/> (used by first-run bootstrap).</summary>
        public static void WriteDefault(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var cfg = new RecorderConfig { DataDirectory = dir ?? string.Empty };
            var ser = new DataContractJsonSerializer(typeof(RecorderConfig));
            using (var ms = new MemoryStream())
            {
                ser.WriteObject(ms, cfg);
                File.WriteAllText(path, Encoding.UTF8.GetString(ms.ToArray()));
            }
        }
    }
}
