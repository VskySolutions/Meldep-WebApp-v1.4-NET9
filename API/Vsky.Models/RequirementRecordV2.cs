using System;
using System.Collections.Generic;

namespace Vsky.Models
{
    // Read-side shapes of the requirement details page (v2). None of these is mapped to a table.

    /// <summary>How much linked work a requirement has: the figures its header and Related tab show.</summary>
    public class RequirementRelatedCountsV2
    {
        public int Tasks { get; set; }
        public int OpenTasks { get; set; }
        public int Issues { get; set; }
        public int OpenIssues { get; set; }
        public int TestCases { get; set; }
        public int PassedTestCases { get; set; }
        public int QuestionsAnswers { get; set; }
        public int ActionItems { get; set; }
        public decimal TimesheetHours { get; set; }
        public int Notes { get; set; }
        public int LogEntries { get; set; }
        public int Documents { get; set; }
        public int DataMappings { get; set; }
    }

    // A requirement's log read like a chat: one page of the newest entries (or of the entries older than
    // a cursor). The original description is the oldest entry of all.
    public class RequirementLogThreadV2
    {
        /// <summary>The page, newest first.</summary>
        public List<RequirementLogEntryV2> Items { get; set; } = new List<RequirementLogEntryV2>();

        /// <summary>Whether older entries exist beyond this page.</summary>
        public bool HasMore { get; set; }

        /// <summary>Change-log entries, plus one for the description when there is one.</summary>
        public int Total { get; set; }

        /// <summary>Whether the requirement has a description (the first entry written becomes it otherwise).</summary>
        public bool HasDescription { get; set; }
    }

    public class RequirementLogEntryV2
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public DateTime CreatedOnUtc { get; set; }
        public string CreatedById { get; set; }
        public string CreatedByUserName { get; set; }
        public string CreatedByName { get; set; }

        /// <summary>True for the requirement's own description, which is edited through the requirement.</summary>
        public bool IsRequirementDescription { get; set; }
    }
}
