using System;
using System.Collections.Generic;
using System.Linq;

namespace atc.utilities {
    public enum HealthIssueSeverity
    {
        Information = 0,
        Warning = 1,
        Error = 2
    }

    public class HealthIssue
    {
        public HealthIssueSeverity Severity {get; set;}
        public string Description {get; set;}
    }

    public class HealthStatus: List<HealthIssue> 
    {
        public bool Healthy => !this.Any(issue => issue.Severity != HealthIssueSeverity.Information);
    }
}