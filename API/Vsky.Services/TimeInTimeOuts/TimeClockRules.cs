using System;
using System.Collections.Generic;
using System.Linq;
using Vsky.Models;

namespace Vsky.Services.TimeInTimeOuts
{
    /// <summary>Where an employee is in the day's Time In - Out.</summary>
    public enum TimeClockState
    {
        /// <summary>No session to show: never timed in, or the last session ended on an earlier day.</summary>
        NotStarted,
        Working,
        OnBreak,
        /// <summary>Timed out today.</summary>
        Completed
    }

    /// <summary>One break as the summary counts it: inside the session, never overlapping another.</summary>
    public class TimeClockBreak
    {
        public string Id { get; set; }
        public string Reason { get; set; }
        public DateTime StartUtc { get; set; }
        /// <summary>Null while the break is running.</summary>
        public DateTime? EndUtc { get; set; }
        public bool IsOpen { get; set; }
        /// <summary>Started and then called off: kept on the day with its reason, counting 0.</summary>
        public bool IsCancelled { get; set; }
        public string CancelReason { get; set; }
        /// <summary>The day's lunch break (its reason says lunch): counted against the lunch allowance.</summary>
        public bool IsLunch { get; set; }
        /// <summary>The dinner break of a late shift (its reason says dinner).</summary>
        public bool IsDinner { get; set; }
        /// <summary>Time out of the office on a gate pass (its reason says gate pass): set against the daily gate pass limit, not the break standard.</summary>
        public bool IsGatePass { get; set; }
        /// <summary>Entered after it was over (a break the employee forgot to clock), not timed as it happened.</summary>
        public bool IsAddedLater { get; set; }
        /// <summary>Seconds counted for this break (up to "now" while it runs); 0 when cancelled.</summary>
        public int Seconds { get; set; }
    }

    /// <summary>A session measured at one moment. Gross = Break + Worked, always, to the second.</summary>
    public class TimeClockSummary
    {
        public TimeClockState State { get; set; }
        public DateTime? TimeInUtc { get; set; }
        public DateTime? TimeOutUtc { get; set; }
        /// <summary>Time in to time out (or to now while the session is open).</summary>
        public int GrossSeconds { get; set; }
        public int BreakSeconds { get; set; }
        /// <summary>The final working time: gross minus breaks, never below 0.</summary>
        public int WorkedSeconds { get; set; }
        /// <summary>Still timed in after <see cref="TimeClockRules.ForgottenAfter"/>: the time-out was forgotten.</summary>
        public bool IsOverdue { get; set; }
        /// <summary>Seconds of lunch taken so far (part of BreakSeconds), to set against <see cref="TimeClockRules.LunchAllowance"/>.</summary>
        public int LunchSeconds { get; set; }
        /// <summary>Seconds of dinner taken so far (part of BreakSeconds).</summary>
        public int DinnerSeconds { get; set; }
        /// <summary>A lunch break stands on the session (clocked, added later or confirmed at time-out); cancelled ones do not count.</summary>
        public bool LunchLogged { get; set; }
        public bool DinnerLogged { get; set; }
        /// <summary>Seconds on gate pass so far (part of BreakSeconds, which is everything taken off the working time).</summary>
        public int GatePassSeconds { get; set; }
        public List<TimeClockBreak> Breaks { get; set; } = new List<TimeClockBreak>();
    }

    /// <summary>What a time-out writes on the session row.</summary>
    public class TimeClockTotals
    {
        public TimeSpan TotalHours { get; set; }
        public TimeSpan TotalBreak { get; set; }
        public TimeSpan ActualHours { get; set; }
    }

    /// <summary>
    /// Every rule of the dashboard's Time In - Out in one place, as pure functions: how a session and its breaks
    /// are measured, what may be done in each state and why not, and what a time-out stores. Nothing here
    /// touches the database or the clock; "now" is always passed in.
    /// </summary>
    public static class TimeClockRules
    {
        /// <summary>The TotalHours/TotalBreak/ActualHours columns are SQL "time": they cannot hold a full day.</summary>
        public static readonly TimeSpan MaxStored = new TimeSpan(23, 59, 59);

        /// <summary>A session still open after this long is a forgotten time-out (the Attendance Register's rule).</summary>
        public static readonly TimeSpan ForgottenAfter = TimeSpan.FromHours(16);

        /// <summary>The lunch break every employee is expected to take on a working day.</summary>
        public static readonly TimeSpan LunchAllowance = TimeSpan.FromMinutes(45);
        public const string LunchReason = "Lunch";
        public const string DinnerReason = "Dinner";

        /// <summary>
        /// The break time a day normally holds, lunch and dinner included. A standard, not a limit: an employee
        /// may take more, and the card only shows that they have.
        /// </summary>
        public static readonly TimeSpan BreakStandard = TimeSpan.FromMinutes(60);

        /// <summary>The most gate pass an employee may take in a day. Gate pass is counted apart from the breaks.</summary>
        public static readonly TimeSpan GatePassMax = TimeSpan.FromHours(2);
        public const string GatePassReason = "Gate Pass";

        /// <summary>Written after the meal's name on a break the employee only confirmed when timing out.</summary>
        public const string ConfirmedAtTimeOut = " (confirmed at time out)";

        public const int ReasonMaxLength = 100;
        public const string DefaultReason = "Break";

        public const string NoEmployee = "Employee not found. Please create employee.";
        public const string AlreadyTimedIn = "You are already timed in. Time out before timing in again.";
        public const string NotTimedIn = "You are not timed in.";
        public const string AlreadyTimedOut = "This session is already timed out.";
        public const string NotYourSession = "This Time In - Out entry belongs to another employee.";
        public const string AlreadyOnBreak = "You are already on a break. End it before starting another.";
        public const string NotOnBreak = "You are not on a break.";
        public const string SessionTooOld = "This session was left open for more than a day. Time out to close it, then time in again.";
        public const string ReasonTooLong = "The break reason can be at most 100 characters.";
        public const string PastBreakTimesRequired = "Enter the start and end time of the break.";
        public const string PastBreakEndBeforeStart = "The break must end after it starts.";
        public const string PastBreakBeforeTimeIn = "The break cannot start before your time in.";
        public const string PastBreakInFuture = "The break cannot end in the future. To take a break now, use Start Break.";
        public const string PastBreakAfterTimeOut = "The break cannot end after your time out.";
        public const string PastBreakOverlaps = "This break overlaps another break of the day.";
        public const int NoteMaxLength = 500;
        public const string NoteTooLong = "The time-out note can be at most 500 characters.";
        public const string BreaksTurnedOff = "Breaks are turned off: only Time In and Time Out are in use.";
        public const string GatePassUsedUp = "You have used your 2 hours of gate pass for today.";
        public const string GatePassOverMax = "This gate pass would take you over the 2 hours of gate pass allowed in a day.";
        public const string GatePassNotABreak = "A gate pass is not started like a break. Use Add Gate Pass and choose its minutes.";
        public const string GatePassMinutesRequired = "Choose how many minutes the gate pass is for.";
        public const string GatePassPurposeRequired = "Please say what the gate pass is for.";
        public const string GatePassPurposeTooLong = "The purpose of the gate pass can be at most 80 characters.";
        public const string GatePassNotFound = "This entry was not found on your session, or it is already cancelled.";
        public const string MealNotABreak = "Lunch and dinner are added from Add Lunch / Dinner Break, as 45 mins.";
        public const int GatePassPurposeMaxLength = 80;
        public const string LunchAlreadyTaken = "A lunch break is already logged for today. Only one lunch or dinner break is allowed in a day.";
        public const string DinnerAlreadyTaken = "A dinner break is already logged for today. Only one lunch or dinner break is allowed in a day.";
        public const string CancelReasonRequired = "Please give a reason for cancelling the break.";
        public const string CancelReasonTooLong = "The cancellation reason can be at most 100 characters.";

        /// <summary>
        /// The table has no column for a cancellation, so a cancelled break is written with what it has:
        /// BreakIn equal to BreakOut (it ended the instant it began), TotalBreak 0, and the cancellation
        /// reason after this marker in BreakReason ("Lunch | Cancelled: started by mistake").
        /// </summary>
        public const string CancelMarker = " | Cancelled: ";

        /// <summary>
        /// Written after the reason of a break that was entered by hand (a missed break, a lunch or dinner added
        /// afterwards or confirmed at the time-out) instead of being timed as it happened: "Lunch | Manual".
        /// It stays on the row, so anyone reading the table later can tell the two apart.
        /// </summary>
        public const string ManualMarker = " | Manual";

        #region Instants
        /// <summary>Clock arithmetic is done on whole seconds, so the parts always add up to the total.</summary>
        public static DateTime TruncateToSecond(DateTime value)
        {
            return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
        }

        /// <summary>The UTC instant of the time-in: the session stores its date and its time of day apart.</summary>
        public static DateTime? TimeInUtcOf(TimeInTimeOut session)
        {
            return session?.TimeInDate == null ? (DateTime?)null : session.TimeInDate.Value.Date.Add(session.TimeIn);
        }

        /// <summary>The UTC instant of the time-out; null while the session is open.</summary>
        public static DateTime? TimeOutUtcOf(TimeInTimeOut session)
        {
            return session?.TimeOutDate == null ? (DateTime?)null : session.TimeOutDate.Value.Date.Add(session.TimeOut);
        }

        public static bool IsOpen(TimeInTimeOut session)
        {
            return session != null && session.TimeInDate != null && session.TimeOutDate == null;
        }
        #endregion

        #region Summary
        /// <summary>
        /// Measures a session at <paramref name="nowUtc"/>. Each break is cut to the session (it cannot start
        /// before the time-in or end after the time-out), a running break counts up to now, and breaks that
        /// overlap are counted once.
        /// </summary>
        public static TimeClockSummary Summarize(TimeInTimeOut session, IEnumerable<TimeInTimeOutBreakDetail> breaks, DateTime nowUtc)
        {
            var summary = new TimeClockSummary { State = TimeClockState.NotStarted };

            var timeIn = TimeInUtcOf(session);
            if (timeIn == null)
                return summary;

            var start = TruncateToSecond(timeIn.Value);
            var timeOut = TimeOutUtcOf(session);
            var open = timeOut == null;

            // A time-out before the time-in (a corrected clock, bad data) counts as no time at all.
            var end = TruncateToSecond(open ? nowUtc : timeOut.Value);
            if (end < start) end = start;
            if (end - start > MaxStored) end = start.Add(MaxStored);

            summary.TimeInUtc = start;
            summary.TimeOutUtc = open ? (DateTime?)null : end;
            summary.GrossSeconds = (int)(end - start).TotalSeconds;
            summary.IsOverdue = open && TruncateToSecond(nowUtc) - start >= ForgottenAfter;

            var cursor = start;
            var rows = (breaks ?? Enumerable.Empty<TimeInTimeOutBreakDetail>())
                .Where(b => b != null && !b.Deleted && b.BreakOut != null)
                .OrderBy(b => b.BreakOut.Value)
                .ThenBy(b => b.CreatedOnUtc);

            foreach (var row in rows)
            {
                // Only an open session can have a running break; on a closed one it ended at the time-out.
                var running = row.BreakIn == null && open;

                // A cancelled break stays on the day at the moment it was started, and counts nothing.
                if (IsCancelled(row))
                {
                    var at = Clamp(TruncateToSecond(row.BreakOut.Value), start, end);
                    summary.Breaks.Add(new TimeClockBreak
                    {
                        Id = row.Id,
                        Reason = ReasonOf(row),
                        StartUtc = at,
                        EndUtc = at,
                        IsCancelled = true,
                        CancelReason = CancelReasonOf(row),
                        IsLunch = IsLunch(row),
                        IsDinner = IsDinner(row),
                        IsGatePass = IsGatePass(row)
                    });
                    continue;
                }

                // A gate pass is logged with the minutes the employee chose, and counts all of them from the
                // moment it is logged: it is a stated amount, not a stretch of the day measured by the clock.
                // Lunch and dinner are the same: the allowance, logged at once, with no timer.
                if (IsLoggedAmount(row))
                {
                    summary.Breaks.Add(new TimeClockBreak
                    {
                        Id = row.Id,
                        Reason = ReasonOf(row),
                        StartUtc = TruncateToSecond(row.BreakOut.Value),
                        EndUtc = TruncateToSecond(row.BreakIn.Value),
                        IsGatePass = IsGatePass(row),
                        IsLunch = IsLunch(row),
                        IsDinner = IsDinner(row),
                        IsAddedLater = IsAddedLater(row),
                        Seconds = (int)row.TotalBreak.Value.TotalSeconds
                    });
                    continue;
                }

                var breakStart = Clamp(TruncateToSecond(row.BreakOut.Value), cursor, end);
                var breakEnd = Clamp(row.BreakIn == null ? end : TruncateToSecond(row.BreakIn.Value), breakStart, end);

                summary.Breaks.Add(new TimeClockBreak
                {
                    Id = row.Id,
                    Reason = ReasonOf(row),
                    StartUtc = breakStart,
                    EndUtc = running ? (DateTime?)null : breakEnd,
                    IsOpen = running,
                    IsLunch = IsLunch(row),
                    IsDinner = IsDinner(row),
                    IsGatePass = IsGatePass(row),
                    IsAddedLater = IsAddedLater(row),
                    Seconds = (int)(breakEnd - breakStart).TotalSeconds
                });

                cursor = breakEnd;
            }

            summary.BreakSeconds = Math.Min(summary.GrossSeconds, summary.Breaks.Sum(b => b.Seconds));
            summary.LunchSeconds = summary.Breaks.Where(b => b.IsLunch).Sum(b => b.Seconds);
            summary.DinnerSeconds = summary.Breaks.Where(b => b.IsDinner).Sum(b => b.Seconds);
            summary.LunchLogged = summary.Breaks.Any(b => b.IsLunch && !b.IsCancelled);
            summary.DinnerLogged = summary.Breaks.Any(b => b.IsDinner && !b.IsCancelled);
            summary.GatePassSeconds = Math.Min(summary.BreakSeconds, summary.Breaks.Where(b => b.IsGatePass).Sum(b => b.Seconds));
            summary.WorkedSeconds = Math.Max(0, summary.GrossSeconds - summary.BreakSeconds);
            summary.State = !open
                ? TimeClockState.Completed
                : summary.Breaks.Any(b => b.IsOpen) ? TimeClockState.OnBreak : TimeClockState.Working;

            return summary;
        }

        /// <summary>
        /// A session that ended on an earlier day (site-local) is yesterday's news: the card starts the new day
        /// empty instead of showing those hours as today's.
        /// </summary>
        public static bool IsFromEarlierDay(DateTime timeOutLocal, DateTime nowLocal)
        {
            return timeOutLocal.Date < nowLocal.Date;
        }
        #endregion

        #region What may be done
        /// <summary>Null when a new time-in may be made; otherwise the sentence to refuse it with.</summary>
        public static string RefuseTimeIn(string employeeId, TimeInTimeOut latest)
        {
            if (string.IsNullOrEmpty(employeeId)) return NoEmployee;
            return IsOpen(latest) ? AlreadyTimedIn : null;
        }

        public static string RefuseTimeOut(TimeInTimeOut session, string employeeId)
        {
            if (session == null || session.TimeInDate == null) return NotTimedIn;
            if (!string.IsNullOrEmpty(employeeId) && session.EmployeeId != employeeId) return NotYourSession;
            return session.TimeOutDate != null ? AlreadyTimedOut : null;
        }

        public static string RefuseBreakStart(TimeInTimeOut session, string employeeId, IEnumerable<TimeInTimeOutBreakDetail> breaks, string reason, DateTime nowUtc)
        {
            var refusal = RefuseBreakChange(session, employeeId);
            if (refusal != null) return refusal;
            if (nowUtc - TimeInUtcOf(session).Value > MaxStored) return SessionTooOld;
            if (OpenBreakOf(breaks) != null) return AlreadyOnBreak;
            return (reason ?? "").Trim().Length > ReasonMaxLength ? ReasonTooLong : null;
        }

        public static string RefuseBreakEnd(TimeInTimeOut session, string employeeId, IEnumerable<TimeInTimeOutBreakDetail> breaks)
        {
            var refusal = RefuseBreakChange(session, employeeId);
            if (refusal != null) return refusal;
            return OpenBreakOf(breaks) == null ? NotOnBreak : null;
        }

        /// <summary>Only a running break can be cancelled, and only with a reason.</summary>
        public static string RefuseBreakCancel(TimeInTimeOut session, string employeeId, IEnumerable<TimeInTimeOutBreakDetail> breaks, string cancelReason)
        {
            var refusal = RefuseBreakEnd(session, employeeId, breaks);
            if (refusal != null) return refusal;
            if (string.IsNullOrWhiteSpace(cancelReason)) return CancelReasonRequired;
            return cancelReason.Trim().Length > ReasonMaxLength ? CancelReasonTooLong : null;
        }

        /// <summary>
        /// Works out when a break the employee forgot to clock took place, from the two clock times they give
        /// (site time, to the minute), and says why not when it cannot stand. The break belongs to the session:
        /// it starts at or after the time-in and ends by now (by the time-out on a closed session); on a session
        /// that runs past midnight a clock time earlier than the time-in means the next day. It may not overlap
        /// another break, running ones included; cancelled breaks are no obstacle.
        /// </summary>
        public static string ResolvePastBreak(
            TimeInTimeOut session,
            string employeeId,
            IEnumerable<TimeInTimeOutBreakDetail> breaks,
            TimeSpan? startClock,
            TimeSpan? endClock,
            string reason,
            TimeZoneInfo zone,
            DateTime nowUtc,
            out DateTime startUtc,
            out DateTime endUtc)
        {
            startUtc = default;
            endUtc = default;

            if (session == null || session.TimeInDate == null) return NotTimedIn;
            if (string.IsNullOrEmpty(employeeId) || session.EmployeeId != employeeId) return NotYourSession;
            if (startClock == null || endClock == null) return PastBreakTimesRequired;
            if ((reason ?? "").Trim().Length > ReasonMaxLength) return ReasonTooLong;

            // The employee reads and types clock times to the minute, so the session is taken to the minute too:
            // a time-in at 09:02:40 allows a break from 09:02, a time-out at 18:30:10 one until 18:31.
            var timeInUtc = TimeInUtcOf(session).Value;
            var timeOutUtc = TimeOutUtcOf(session);
            var closed = timeOutUtc != null;
            var from = ToLocal(TruncateToMinute(timeInUtc), zone);
            var until = closed
                ? ToLocal(TruncateToMinute(timeOutUtc.Value.AddSeconds(59)), zone)
                : ToLocal(nowUtc, zone);
            if (until - from > MaxStored) until = from.Add(MaxStored);

            var start = from.Date.Add(startClock.Value);
            var beforeTimeIn = start < from;
            if (beforeTimeIn) start = start.AddDays(1);
            // Past the end of the session either way: an early clock time was before the time-in, a late one is still ahead.
            if (start >= until) return beforeTimeIn ? PastBreakBeforeTimeIn : closed ? PastBreakAfterTimeOut : PastBreakInFuture;

            var end = start.Date.Add(endClock.Value);
            if (end <= start)
            {
                // Only a break that really ran over midnight ends on an earlier clock time than it started.
                if (end.AddDays(1) > until) return PastBreakEndBeforeStart;
                end = end.AddDays(1);
            }

            if (end > until) return closed ? PastBreakAfterTimeOut : PastBreakInFuture;

            try
            {
                startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(start, DateTimeKind.Unspecified), zone);
                endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(end, DateTimeKind.Unspecified), zone);
            }
            catch (ArgumentException)
            {
                // A clock time that does not exist on that day (the hour skipped when clocks go forward).
                return PastBreakTimesRequired;
            }
            if (endUtc <= startUtc) return PastBreakEndBeforeStart;

            var limitUtc = closed ? timeOutUtc.Value : nowUtc;
            foreach (var other in (breaks ?? Enumerable.Empty<TimeInTimeOutBreakDetail>()).Where(b => b != null && !b.Deleted && b.BreakOut != null && !IsCancelled(b) && !IsLoggedAmount(b)))
            {
                // Compared to the minute, as the list shows them: a break may start in the minute another ended.
                var otherStart = TruncateToMinute(other.BreakOut.Value);
                var otherEnd = TruncateToMinute(other.BreakIn ?? limitUtc.AddSeconds(59));
                if (startUtc < otherEnd && otherStart < endUtc) return PastBreakOverlaps;
            }

            return null;
        }

        private static string RefuseBreakChange(TimeInTimeOut session, string employeeId)
        {
            if (session == null || session.TimeInDate == null) return NotTimedIn;
            if (string.IsNullOrEmpty(employeeId) || session.EmployeeId != employeeId) return NotYourSession;
            return session.TimeOutDate != null ? AlreadyTimedOut : null;
        }

        /// <summary>The running break of a session, if any (the latest, should bad data hold more than one).</summary>
        public static TimeInTimeOutBreakDetail OpenBreakOf(IEnumerable<TimeInTimeOutBreakDetail> breaks)
        {
            return (breaks ?? Enumerable.Empty<TimeInTimeOutBreakDetail>())
                .Where(b => b != null && !b.Deleted && b.BreakOut != null && b.BreakIn == null)
                .OrderByDescending(b => b.BreakOut.Value)
                .FirstOrDefault();
        }

        /// <summary>The time-out note as it is stored: trimmed, and null when nothing was written.</summary>
        public static string CleanNote(string note)
        {
            return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }

        public static string RefuseNote(string note)
        {
            return (note ?? "").Trim().Length > NoteMaxLength ? NoteTooLong : null;
        }

        public static string CleanReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? DefaultReason : reason.Trim();
        }
        #endregion

        #region Writing
        /// <summary>
        /// Ends a running break at <paramref name="nowUtc"/>. A break never ends before it started, and its
        /// stored length never passes what the column can hold.
        /// </summary>
        public static void EndBreak(TimeInTimeOutBreakDetail openBreak, DateTime nowUtc)
        {
            var start = openBreak.BreakOut.Value;
            // An end equal to the start is how a cancelled break is written, so an ended one is never that:
            // when the clock gives no later moment, it ends one tick on (still 0 seconds long).
            var end = nowUtc <= start ? start.AddTicks(1) : nowUtc;
            var length = TruncateToSecond(end) - TruncateToSecond(start);

            openBreak.BreakIn = end;
            openBreak.TotalBreak = length > MaxStored ? MaxStored : length;
        }

        /// <summary>
        /// Cancels a running break: it ends the instant it began, is 0 long, and carries why it was called off.
        /// The break's own reason is kept in front of the cancellation reason.
        /// </summary>
        public static void CancelBreak(TimeInTimeOutBreakDetail openBreak, string cancelReason)
        {
            openBreak.BreakIn = openBreak.BreakOut;
            openBreak.TotalBreak = TimeSpan.Zero;
            openBreak.BreakReason = ReasonOf(openBreak) + CancelMarker + cancelReason.Trim();
        }

        /// <summary>The row of a break entered after the fact: it ran from start to end and is created now.</summary>
        public static TimeInTimeOutBreakDetail NewPastBreak(string timeInTimeOutId, DateTime startUtc, DateTime endUtc, string reason, DateTime nowUtc)
        {
            var length = endUtc - startUtc;
            return new TimeInTimeOutBreakDetail
            {
                TimeInTimeOutId = timeInTimeOutId,
                BreakOut = startUtc,
                BreakIn = endUtc,
                TotalBreak = length > MaxStored ? MaxStored : length,
                BreakReason = CleanReason(reason) + ManualMarker,
                CreatedOnUtc = nowUtc,
                UpdatedOnUtc = nowUtc
            };
        }

        /// <summary>
        /// A timed break is created the moment it starts; one created only after it had ended was added later.
        /// (The table has no column for this, and needs none.)
        /// </summary>
        public static bool IsAddedLater(TimeInTimeOutBreakDetail row)
        {
            if (row == null || row.BreakOut == null || row.BreakIn == null || IsCancelled(row)) return false;

            // Marked as manual when it was written; the dates say the same for a row without the mark.
            return (row.BreakReason ?? "").IndexOf(ManualMarker, StringComparison.Ordinal) >= 0
                || (row.CreatedOnUtc > row.BreakOut.Value && row.CreatedOnUtc >= row.BreakIn.Value);
        }

        /// <summary>Lunch is told by its reason: "Lunch", "Lunch Break", "lunch with client".</summary>
        public static bool IsLunch(TimeInTimeOutBreakDetail row)
        {
            return ReasonOf(row).IndexOf(LunchReason, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Dinner is told by its reason, as lunch is.</summary>
        public static bool IsDinner(TimeInTimeOutBreakDetail row)
        {
            return ReasonOf(row).IndexOf(DinnerReason, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// One lunch and one dinner a day: a break whose reason says lunch (or dinner) is refused when the day,
        /// across every session of it, already holds one. A cancelled one does not count, so a lunch started by
        /// mistake and cancelled can still be taken.
        /// </summary>
        public static string RefuseSecondMeal(string reason, IEnumerable<TimeInTimeOutBreakDetail> dayBreaks)
        {
            // Lunch or dinner, not both: either one on the day closes the day for the other as well.
            var asked = new TimeInTimeOutBreakDetail { BreakReason = reason };
            if (!IsLunch(asked) && !IsDinner(asked)) return null;
            if (IsMealLogged(dayBreaks, dinner: false)) return LunchAlreadyTaken;
            return IsMealLogged(dayBreaks, dinner: true) ? DinnerAlreadyTaken : null;
        }

        /// <summary>A gate pass is told by its reason: "Gate Pass", "Gate Pass: bank work", "gatepass".</summary>
        public static bool IsGatePass(TimeInTimeOutBreakDetail row)
        {
            return ReasonOf(row).Replace(" ", "").Replace("-", "").IndexOf("gatepass", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Seconds away among these, gate pass or ordinary breaks as asked: finished ones by their two instants,
        /// a running one up to now. Cancelled ones count nothing.
        /// </summary>
        public static int AwaySeconds(IEnumerable<TimeInTimeOutBreakDetail> breaks, bool gatePass, DateTime nowUtc)
        {
            return (int)(breaks ?? Enumerable.Empty<TimeInTimeOutBreakDetail>())
                .Where(b => b != null && !b.Deleted && b.BreakOut != null && !IsCancelled(b) && IsGatePass(b) == gatePass)
                .Sum(b => IsLoggedAmount(b)
                    ? b.TotalBreak.Value.TotalSeconds
                    : Math.Max(0, ((b.BreakIn ?? nowUtc) - b.BreakOut.Value).TotalSeconds));
        }

        /// <summary>
        /// Gate pass has a hard daily limit, across every session of the day. Starting one is refused once the
        /// limit is used; one entered afterwards (its length known) is refused when it would pass the limit.
        /// A gate pass already running is never cut short: going over is shown, not undone.
        /// </summary>
        public static string RefuseGatePass(string reason, IEnumerable<TimeInTimeOutBreakDetail> dayBreaks, DateTime nowUtc, TimeSpan? length)
        {
            // A gate pass has its own way in (LogGatePass): it is never started or back-dated as a break.
            return IsGatePass(new TimeInTimeOutBreakDetail { BreakReason = reason }) ? GatePassNotABreak : null;
        }

        /// <summary>A gate pass logged with its minutes (as against an old one that was timed like a break).</summary>
        public static bool IsLoggedGatePass(TimeInTimeOutBreakDetail row)
        {
            return row != null && row.BreakOut != null && row.BreakIn != null && row.TotalBreak != null
                && !IsCancelled(row) && IsGatePass(row);
        }

        /// <summary>
        /// Whether a gate pass of <paramref name="minutes"/> may be logged on the session, or an existing one
        /// (<paramref name="existingId"/>) changed to that many. Only while timed in, on one's own session, with
        /// a purpose, and never taking the day past <see cref="GatePassMax"/>: the day's other gate passes count,
        /// the one being changed does not.
        /// </summary>
        public static string RefuseGatePassLog(TimeInTimeOut session, string employeeId, IEnumerable<TimeInTimeOutBreakDetail> dayBreaks, int minutes, string purpose, string existingId, DateTime nowUtc)
        {
            var refusal = RefuseBreakChange(session, employeeId);
            if (refusal != null) return refusal;
            if (minutes <= 0) return GatePassMinutesRequired;
            if (string.IsNullOrWhiteSpace(purpose)) return GatePassPurposeRequired;
            if (purpose.Trim().Length > GatePassPurposeMaxLength) return GatePassPurposeTooLong;

            var others = (dayBreaks ?? Enumerable.Empty<TimeInTimeOutBreakDetail>()).Where(b => b != null && b.Id != existingId);
            var used = AwaySeconds(others, gatePass: true, nowUtc);
            if (used >= GatePassMax.TotalSeconds) return GatePassUsedUp;
            return used + minutes * 60 > GatePassMax.TotalSeconds ? GatePassOverMax : null;
        }

        /// <summary>The gate pass of the session with this id that can still be changed or cancelled; null when there is none.</summary>
        public static TimeInTimeOutBreakDetail GatePassOf(IEnumerable<TimeInTimeOutBreakDetail> sessionBreaks, string breakId)
        {
            return (sessionBreaks ?? Enumerable.Empty<TimeInTimeOutBreakDetail>())
                .FirstOrDefault(b => b != null && !b.Deleted && b.Id == breakId && IsLoggedGatePass(b));
        }

        /// <summary>A new gate pass: logged now, for the minutes chosen.</summary>
        public static TimeInTimeOutBreakDetail NewGatePass(string timeInTimeOutId, int minutes, string purpose, DateTime nowUtc)
        {
            var row = new TimeInTimeOutBreakDetail
            {
                TimeInTimeOutId = timeInTimeOutId,
                BreakOut = nowUtc,
                CreatedOnUtc = nowUtc,
                UpdatedOnUtc = nowUtc
            };
            SetGatePass(row, minutes, purpose);
            return row;
        }

        /// <summary>Sets (or changes) the minutes and the purpose of a gate pass; it keeps the moment it was logged.</summary>
        public static void SetGatePass(TimeInTimeOutBreakDetail row, int minutes, string purpose)
        {
            var length = TimeSpan.FromMinutes(minutes);
            row.TotalBreak = length;
            row.BreakIn = row.BreakOut.Value.Add(length);
            row.BreakReason = GatePassReason + ": " + purpose.Trim();
        }

        /// <summary>Cancelling a gate pass needs a reason, like cancelling a break.</summary>
        public static string RefuseGatePassCancel(TimeInTimeOut session, string employeeId, TimeInTimeOutBreakDetail gatePass, string cancelReason)
        {
            var refusal = RefuseBreakChange(session, employeeId);
            if (refusal != null) return refusal;
            if (gatePass == null) return GatePassNotFound;
            if (string.IsNullOrWhiteSpace(cancelReason)) return CancelReasonRequired;
            return cancelReason.Trim().Length > ReasonMaxLength ? CancelReasonTooLong : null;
        }

        /// <summary>Seconds of the finished lunch (or dinner) breaks among these, by their stored length.</summary>
        public static int MealSeconds(IEnumerable<TimeInTimeOutBreakDetail> breaks, bool dinner)
        {
            return (int)(breaks ?? Enumerable.Empty<TimeInTimeOutBreakDetail>())
                .Where(b => b != null && !b.Deleted && b.BreakOut != null && b.BreakIn != null && !IsCancelled(b) && (dinner ? IsDinner(b) : IsLunch(b)))
                .Sum(b => (b.TotalBreak ?? b.BreakIn.Value - b.BreakOut.Value).TotalSeconds);
        }

        /// <summary>
        /// The first day a timesheet can still be created for: the Sunday of last week. The timesheet form lets
        /// an employee pick a date from this week or last week only (addEdit.vue, getTimesheetAllowedDateRange).
        /// </summary>
        public static DateTime TimesheetWindowStart(DateTime today)
        {
            return today.Date.AddDays(-(int)today.DayOfWeek - 7);
        }

        /// <summary>
        /// The days the employee worked (timed in, by site-local date) from <paramref name="from"/> up to but not
        /// including <paramref name="before"/>, that have no filled timesheet; latest first. Going by the days
        /// actually worked skips weekends, holidays and leave without having to know them.
        /// </summary>
        public static List<DateTime> DaysWithoutTimesheet(IEnumerable<TimeInTimeOut> sessions, IEnumerable<DateTime> filledDates, DateTime from, DateTime before, TimeZoneInfo zone)
        {
            var filled = new HashSet<DateTime>((filledDates ?? Enumerable.Empty<DateTime>()).Select(d => d.Date));

            return (sessions ?? Enumerable.Empty<TimeInTimeOut>())
                .Select(TimeInUtcOf)
                .Where(timeIn => timeIn != null)
                .Select(timeIn => ToLocal(timeIn.Value, zone).Date)
                .Distinct()
                .Where(day => day >= from.Date && day < before.Date && !filled.Contains(day))
                .OrderByDescending(day => day)
                .ToList();
        }

        /// <summary>Two sessions belong to the same day when their time-ins fall on the same site-local date.</summary>
        public static bool IsSameDay(TimeInTimeOut first, TimeInTimeOut second, TimeZoneInfo zone)
        {
            var a = TimeInUtcOf(first);
            var b = TimeInUtcOf(second);
            return a != null && b != null && ToLocal(a.Value, zone).Date == ToLocal(b.Value, zone).Date;
        }

        /// <summary>A lunch (or dinner) break that counts stands among these: not deleted, not cancelled.</summary>
        public static bool IsMealLogged(IEnumerable<TimeInTimeOutBreakDetail> breaks, bool dinner)
        {
            return (breaks ?? Enumerable.Empty<TimeInTimeOutBreakDetail>())
                .Any(b => b != null && !b.Deleted && b.BreakOut != null && !IsCancelled(b) && (dinner ? IsDinner(b) : IsLunch(b)));
        }

        /// <summary>
        /// The break written when an employee confirms, while timing out, a lunch or dinner they never clocked:
        /// the full allowance, so the working hours come out right. Null when that meal is already logged.
        /// </summary>
        public static TimeInTimeOutBreakDetail NewMealBreakAtTimeOut(TimeInTimeOut session, IEnumerable<TimeInTimeOutBreakDetail> breaks, bool dinner, DateTime timeOutUtc)
        {
            if (TimeInUtcOf(session) == null || IsMealLogged(breaks, dinner)) return null;

            // Like any lunch or dinner it is a stated 45 minutes, not a stretch of the day; it is marked as
            // confirmed at the time-out, and as a manual entry.
            var row = NewMealBreak(session.Id, dinner, timeOutUtc);
            row.BreakReason += ConfirmedAtTimeOut + ManualMarker;
            return row;
        }

        /// <summary>
        /// The day's lunch or dinner: logged at once as the full allowance (45 minutes), with no timer. It counts
        /// in full from the moment it is logged, like a gate pass.
        /// </summary>
        public static TimeInTimeOutBreakDetail NewMealBreak(string timeInTimeOutId, bool dinner, DateTime nowUtc)
        {
            return new TimeInTimeOutBreakDetail
            {
                TimeInTimeOutId = timeInTimeOutId,
                BreakOut = nowUtc,
                BreakIn = nowUtc.Add(LunchAllowance),
                TotalBreak = LunchAllowance,
                BreakReason = dinner ? DinnerReason : LunchReason,
                CreatedOnUtc = nowUtc,
                UpdatedOnUtc = nowUtc
            };
        }

        /// <summary>Whether a reason names the day's meal break, and which: null when it is neither lunch nor dinner.</summary>
        public static bool? IsDinnerReason(string reason)
        {
            var asked = new TimeInTimeOutBreakDetail { BreakReason = reason };
            if (IsDinner(asked)) return true;
            return IsLunch(asked) ? false : (bool?)null;
        }

        /// <summary>
        /// A break that is a stated amount rather than a stretch of the day measured by the clock: a gate pass
        /// (its chosen minutes) or a lunch / dinner (the allowance). It counts its stored length in full.
        /// </summary>
        public static bool IsLoggedAmount(TimeInTimeOutBreakDetail row)
        {
            return row != null && row.BreakOut != null && row.BreakIn != null && row.TotalBreak != null
                && !IsCancelled(row) && (IsGatePass(row) || IsLunch(row) || IsDinner(row));
        }

        /// <summary>The gate pass, lunch or dinner of the session with this id that can still be cancelled; null when there is none.</summary>
        public static TimeInTimeOutBreakDetail LoggedBreakOf(IEnumerable<TimeInTimeOutBreakDetail> sessionBreaks, string breakId)
        {
            return (sessionBreaks ?? Enumerable.Empty<TimeInTimeOutBreakDetail>())
                .FirstOrDefault(b => b != null && !b.Deleted && b.Id == breakId && IsLoggedAmount(b));
        }

        public static bool IsCancelled(TimeInTimeOutBreakDetail row)
        {
            return row != null && row.BreakOut != null && row.BreakIn == row.BreakOut;
        }

        /// <summary>The reason the break was taken for, without any cancellation reason written after it.</summary>
        public static string ReasonOf(TimeInTimeOutBreakDetail row)
        {
            var text = row?.BreakReason ?? "";
            var marker = text.IndexOf(CancelMarker, StringComparison.Ordinal);
            if (marker >= 0) text = text.Substring(0, marker);
            var manual = text.IndexOf(ManualMarker, StringComparison.Ordinal);
            if (manual >= 0) text = text.Substring(0, manual);
            return CleanReason(text);
        }

        /// <summary>Why a cancelled break was called off; null for a break that was not cancelled.</summary>
        public static string CancelReasonOf(TimeInTimeOutBreakDetail row)
        {
            if (!IsCancelled(row)) return null;
            var text = row.BreakReason ?? "";
            var marker = text.IndexOf(CancelMarker, StringComparison.Ordinal);
            return marker < 0 ? "" : text.Substring(marker + CancelMarker.Length).Trim();
        }

        /// <summary>
        /// The totals a time-out stores, from the session as it stands once it is closed at
        /// <paramref name="timeOutUtc"/>. Any break still running is counted up to the time-out.
        /// </summary>
        public static TimeClockTotals TotalsAtTimeOut(TimeInTimeOut session, IEnumerable<TimeInTimeOutBreakDetail> breaks, DateTime timeOutUtc)
        {
            // Measured as an open session at the moment of the time-out: same clamps, same merging of breaks.
            var open = new TimeInTimeOut { TimeInDate = session.TimeInDate, TimeIn = session.TimeIn };
            var summary = Summarize(open, breaks, timeOutUtc);

            return new TimeClockTotals
            {
                TotalHours = TimeSpan.FromSeconds(summary.GrossSeconds),
                TotalBreak = TimeSpan.FromSeconds(summary.BreakSeconds),
                ActualHours = TimeSpan.FromSeconds(summary.WorkedSeconds)
            };
        }
        #endregion

        #region Text
        /// <summary>"7 hr 42 mins", "45 mins", "2 hr"; under a minute is "0 mins". Whole minutes, rounded down.</summary>
        public static string DurationText(int seconds)
        {
            var totalMinutes = Math.Max(0, seconds) / 60;
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours > 0 && minutes > 0) return $"{hours} hr {minutes} mins";
            return hours > 0 ? $"{hours} hr" : $"{minutes} mins";
        }
        #endregion

        private static DateTime TruncateToMinute(DateTime value)
        {
            return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMinute));
        }

        private static DateTime ToLocal(DateTime utc, TimeZoneInfo zone)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        }

        private static DateTime Clamp(DateTime value, DateTime min, DateTime max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }
    }
}
