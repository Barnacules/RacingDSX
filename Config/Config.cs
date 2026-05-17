using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RacingDSX.Config
{
    public class Config
    {
        public bool DisableAppCheck { get; set; }
        public VerboseLevel VerboseLevel { get; set; } = VerboseLevel.Off;
        public Dictionary<String, Profile> Profiles { get; set; } = new Dictionary<String, Profile>();
        [JsonIgnore]
        public Profile ActiveProfile { get; set; } = null;

        public int DSXPort { get; set; } = 6969; // This sets the default dsx port

        public String DefaultProfile { get; set; } = "Forza";

        // DSX IP addresses — allows sending to a remote or non-localhost DSX instance.
        // Defaults to loopback. Users can add additional IPs and select one.
        public List<string> DSXIPs { get; set; } = new List<string> { "127.0.0.1" };
        public int SelectedDSXIP { get; set; } = 0;

        /// <summary>Returns the currently selected DSX IP, falling back to loopback if the index is out of range.</summary>
        [JsonIgnore]
        public string ActiveDSXIP => (DSXIPs != null && SelectedDSXIP >= 0 && SelectedDSXIP < DSXIPs.Count)
            ? DSXIPs[SelectedDSXIP]
            : "127.0.0.1";
    }
}
