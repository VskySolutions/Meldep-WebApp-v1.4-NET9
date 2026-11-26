using System;
using System.ComponentModel.DataAnnotations.Schema;
using Vsky.Core;

namespace Vsky.Models;

/// <summary>
/// One break inside a Time In - Out session. The names read from the office door: BreakOut is the moment
/// the employee stepped out (the break starts) and BreakIn the moment they came back (the break ends).
/// Both are full UTC instants, so a break that crosses midnight needs no guessing; BreakIn stays null
/// while the break is running.
/// </summary>
public class TimeInTimeOutBreakDetail : BaseEntity
{
    public string TimeInTimeOutId { get; set; }
    public string MovementRegisterId { get; set; }
    //public string SiteId { get; set; }
    public DateTime? BreakIn { get; set; }
    public DateTime? BreakOut { get; set; }
    /// <summary>Length of the finished break; null while it is running.</summary>
    public TimeSpan? TotalBreak { get; set; }
    public string BreakReason { get; set; }
    public string CreatedById { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public string UpdatedById { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public bool Deleted { get; set; }

    [NotMapped]
    public string BreakInStr { get; set; }
    [NotMapped]
    public string BreakOutStr { get; set; }

    //public virtual Site Sites { get; set; }
    public virtual TimeInTimeOut TimeInTimeOut { get; set; }
}
