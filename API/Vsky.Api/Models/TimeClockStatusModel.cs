using System.Collections.Generic;

namespace Vsky.Api.Models
{
    /// <summary>
    /// The dashboard's Time In - Out card in one answer. Instants travel as Unix milliseconds (for the live
    /// counters) beside ready-made site-time text (for display), and durations as whole seconds, so nothing
    /// here depends on the date and time converters of the API.
    /// </summary>
    public class TimeClockStatusModel
    {
        /// <summary>NotStarted, Working, OnBreak or Completed.</summary>
        public string State { get; set; }
        public string SessionId { get; set; }
        /// <summary>False when the signed-in user has no employee record: nothing can be clocked.</summary>
        public bool HasEmployee { get; set; } = true;
        /// <summary>
        /// From appsettings.json ("TimeClock:OnlyTimeInTimeOut"): the card offers Time In and Time Out only,
        /// with every break action hidden.
        /// </summary>
        public bool OnlyTimeInTimeOut { get; set; }
        /// <summary>Still timed in after 16 hours: the time-out was forgotten.</summary>
        public bool IsOverdue { get; set; }

        /// <summary>The server's clock when this was measured; the page counts on from here.</summary>
        public long ServerNowMs { get; set; }
        public long? TimeInMs { get; set; }
        public long? TimeOutMs { get; set; }
        public string TimeInDateText { get; set; }
        public string TimeInText { get; set; }
        public string TimeOutDateText { get; set; }
        public string TimeOutText { get; set; }

        public int GrossSeconds { get; set; }
        public int BreakSeconds { get; set; }
        public int WorkedSeconds { get; set; }
        public string GrossText { get; set; }
        public string BreakText { get; set; }
        public string WorkedText { get; set; }
        /// <summary>The note written at the time-out, if any.</summary>
        public string TimeOutNote { get; set; }

        /// <summary>Lunch taken so far (part of BreakSeconds) and the daily lunch allowance it is set against.</summary>
        public int LunchSeconds { get; set; }
        public int LunchAllowanceSeconds { get; set; }
        /// <summary>Dinner taken so far; it has the same allowance as lunch.</summary>
        public int DinnerSeconds { get; set; }
        /// <summary>Whether a lunch / a dinner break stands on the session. The time-out asks about the ones that do not.</summary>
        public bool LunchLogged { get; set; }
        public bool DinnerLogged { get; set; }

        /// <summary>
        /// Today's ordinary break time (lunch and dinner included, gate pass not), across the day's sessions,
        /// and the standard it is shown against. The standard is not a limit.
        /// </summary>
        public int BreakUsedSeconds { get; set; }
        public int BreakStandardSeconds { get; set; }
        /// <summary>Today's gate pass time across the day's sessions, and the most allowed in a day.</summary>
        public int GatePassSeconds { get; set; }
        public int GatePassMaxSeconds { get; set; }

        /// <summary>
        /// Timesheet reminders, never a block. TimesheetMissing: no timesheet is filled for the day of this
        /// session (asked about at the time-out). PendingTimesheets: the earlier days worked, this week and
        /// last, that still have no timesheet and can still be filled; latest first. Older days are left out:
        /// the timesheet form no longer accepts them.
        /// </summary>
        public bool TimesheetMissing { get; set; }
        public string TimesheetDateText { get; set; }
        public List<TimeClockPendingDayModel> PendingTimesheets { get; set; } = new List<TimeClockPendingDayModel>();

        public List<TimeClockBreakModel> Breaks { get; set; } = new List<TimeClockBreakModel>();
    }

    /// <summary>A day worked that has no timesheet: "Mon" and "11/23/2026".</summary>
    public class TimeClockPendingDayModel
    {
        public string DayText { get; set; }
        public string DateText { get; set; }
    }

    public class TimeClockBreakModel
    {
        public string Id { get; set; }
        public string Reason { get; set; }
        public long StartMs { get; set; }
        public long? EndMs { get; set; }
        public string StartText { get; set; }
        public string EndText { get; set; }
        public int Seconds { get; set; }
        public bool IsOpen { get; set; }
        /// <summary>Started and then called off: shown with its reason, counting 0.</summary>
        public bool IsCancelled { get; set; }
        public string CancelReason { get; set; }
        public bool IsLunch { get; set; }
        public bool IsDinner { get; set; }
        public bool IsGatePass { get; set; }
        /// <summary>Entered after it was over, not timed as it happened.</summary>
        public bool IsAddedLater { get; set; }
    }

    /// <summary>A break the employee forgot to clock: its clock times in site time, "HH:mm" (24-hour).</summary>
    public class TimeClockPastBreakModel
    {
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string BreakReason { get; set; }
    }

    /// <summary>
    /// What a time-out may carry: the lunch or dinner the employee confirms they took without logging it,
    /// and a note, all optional.
    /// </summary>
    public class TimeClockTimeOutModel
    {
        public bool AddLunch { get; set; }
        public bool AddDinner { get; set; }
        public string Note { get; set; }
    }

    /// <summary>A gate pass: how many minutes, and what for.</summary>
    public class TimeClockGatePassModel
    {
        public int Minutes { get; set; }
        public string Purpose { get; set; }
    }

    public class TimeClockBreakCancelModel
    {
        public string CancelReason { get; set; }
    }

    public class TimeClockBreakStartModel
    {
        public string BreakReason { get; set; }
    }
}
