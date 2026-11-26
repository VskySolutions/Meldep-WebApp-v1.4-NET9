using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using AutoMapper;
using Ical.Net.DataTypes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerBI.Api;
using MimeKit;
using Vsky.Api.ApiErrors;
using Vsky.Api.Extensions;
using Vsky.Api.Models;
using Vsky.Data;
using Vsky.Models;
using Vsky.Services.Common;
using Vsky.Services.DropDowns;
using Vsky.Services.DropDownTypes;
using Vsky.Services.EmployeeDesignations;
using Vsky.Services.Employees;
using Vsky.Services.Messages;
using Vsky.Services.MovementRegisters;
using Vsky.Services.Notifications;
using Vsky.Services.Sites;
using Vsky.Services.TestPlans;
using Vsky.Services.TimeInTimeOuts;
using Vsky.Services.Timesheets;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Vsky.Api.Models.EmployeeModel;

namespace Vsky.Api.Controllers
{
    [Route("time-in-time-out")]
    [SiteMember]
    public class TimeInTimeOutController : BaseController
    {

        #region Define Services      
        private readonly GlobalVariable _globalVariable;
        private readonly IMapper _mapper;
        private readonly ITimeInTimeOutService _timeInTimeOutService;
        private readonly ITimeInTimeOutBreakDetailService _timeInTimeOutBreakDetailService;
        private readonly IEmployeeService _employeeService;
        private readonly IEmployeeDesignationService _employeeDesignationService;
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly IMasterNotificationService _masterNotificationService;
        private readonly INotificationService _notificationService;
        private readonly IDropDownService _dropDownService;
        private readonly IDropDownTypeService _dropDownTypeService;
        private readonly ISiteService _siteService;
        private readonly ICommonService _commonService;
        private readonly IMovementRegisterServices _movementRegisterService;
        private readonly IMovementRegisterDetailsService _movementRegisterDetailsService;
        private readonly ITimesheetService _timesheetService;
        #endregion

        #region Services Initializations      
        public TimeInTimeOutController(
            GlobalVariable globalVariable,
            IMapper mapper,
            ITimeInTimeOutService timeInTimeOutService,
            ITimeInTimeOutBreakDetailService timeInTimeOutBreakDetailService,
            IEmployeeService employeeService,
            IEmployeeDesignationService employeeDesignationService,
            IWorkflowMessageService workflowMessageService,
            IMasterNotificationService masterNotificationService,
            INotificationService notificationService,
            IDropDownService dropDownService,
            IDropDownTypeService dropDownTypeService,
            ISiteService siteService,
            ICommonService commonService,
            IMovementRegisterServices movementRegisterServices,
            IMovementRegisterDetailsService movementRegisterDetailsService,
            ITimesheetService timesheetService
        )
        {
            _globalVariable = globalVariable;
            _mapper = mapper;
            _timeInTimeOutService = timeInTimeOutService;
            _timeInTimeOutBreakDetailService = timeInTimeOutBreakDetailService;
            _employeeService = employeeService;
            _employeeDesignationService = employeeDesignationService;
            _workflowMessageService = workflowMessageService;
            _masterNotificationService = masterNotificationService;
            _notificationService = notificationService;
            _dropDownService = dropDownService;
            _dropDownTypeService = dropDownTypeService;
            _siteService = siteService;
            _commonService = commonService;
            _movementRegisterService = movementRegisterServices;
            _movementRegisterDetailsService = movementRegisterDetailsService;
            _timesheetService = timesheetService;
        }
        #endregion

        #region GetAllTimeInTimeOuts
        // Title: Get All TimeInTimeOuts
        // Description: This endpoint fetches a list of TimeInTimeOuts based on the provided search criteria such as name, sorting, and pagination. 
        [HttpPost("list")]
        [ReadAccess]
        public IActionResult GetAllTimeInTimeOuts(TimeInTimeOutSearchModel searchModel)
        {
            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var createdBy = searchModel.CreatedBy == "Created By Me" ? LoggedUserId : "";

                var list = _timeInTimeOutService.GetAllTimeInTimeOuts(
                    SiteId,
                    createdBy,
                     searchModel.EmployeeId,
                    searchModel.SearchText,
                    searchModel.FromDate,
                    searchModel.ToDate,
                    searchModel.SortBy,
                    searchModel.Descending,
                    searchModel.Page,
                    searchModel.PageSize
                );

                // Map the fetched list to a model suitable for the response
                var model = new TimeInTimeOutListModel
                {
                    Data = _mapper.Map<IList<TimeInTimeOutModel>>(list),
                    Total = list.TotalCount
                };
              
                return Ok(model);
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }
        #endregion

        #region GetTimeInTimeOutDetailsByEmployeeId
        [HttpGet("{employeeId}")]
        public async Task<IActionResult> GetTimeInTimeOutDetailsByEmployeeId(string employeeId)
        {
            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var entity = await _timeInTimeOutService.GetTimeInTimeOutByEmployeeId(SiteId,employeeId);
                var model = _mapper.Map<TimeInTimeOutModel>(entity);
               
                return Ok(model);
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }
        #endregion

        #region GetTimeInTimeOutDetailsById
        // Title: GetTimeInTimeOutDetailsById
        // Description: This endpoint retrieves the details of a specific TimeInTimeOut based on its unique identifier (ID). 
        [HttpGet("details/{id}")]
        [SiteOwned(typeof(Vsky.Models.TimeInTimeOut))]
        public async Task<IActionResult> GetTimeInTimeOutDetailsById(string id)
        {
            try
            {
                // Fetch the TimeInTimeOut entity by its ID from the service
                var entity = await _timeInTimeOutService.GetTimeInTimeOutDetailsById(id);
                if (entity == null)
                    return BadRequest(new BadRequestError("No time in time out found with the specified id."));

                // Map the TimeInTimeOut entity to a TimeInTimeOutModel object
                var model = _mapper.Map<TimeInTimeOutModel>(entity);

                return Ok(model);
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }
        #endregion

        #region Time-in / time-out only mode
        /// <summary>
        /// appsettings.json, "TimeClock:OnlyTimeInTimeOut". True turns the card into plain Time In / Time Out:
        /// every break action (break, lunch, dinner, gate pass, missed break) is hidden on the card and refused
        /// here. Ending a break already running stays possible, so nobody is left on a break. Default false.
        /// </summary>
        public const string OnlyTimeInTimeOutKey = "TimeClock:OnlyTimeInTimeOut";

        private bool OnlyTimeInTimeOut =>
            HttpContext?.RequestServices.GetService<IConfiguration>()?.GetValue(OnlyTimeInTimeOutKey, false) ?? false;
        #endregion

        #region GetMyTimeClockStatus
        // Title: GetMyTimeClockStatus
        // Description: The signed-in employee's current Time In - Out: the state (not started, working, on break, completed), the breaks and the gross, break and working time. Feeds the dashboard card.
        [HttpGet("my/status")]
        public async Task<IActionResult> GetMyTimeClockStatus()
        {
            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var EmployeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                return Ok(await BuildTimeClockStatus(SiteId, EmployeeId));
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }
        #endregion

        #region CreateTimeInTimeOut
        // Title: CreateTimeInTimeOut
        // Description: Times the signed-in employee in: a new session starting now. Refused while an earlier session is still open, so a double click or a second tab cannot open two.
        [HttpPost]
        public async Task<IActionResult> InsertTimeIn()
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var LoggedUserId = User.GetLoggedInUserId<string>();
                    var SiteId = _globalVariable.SiteId;
                    var SiteData = await _siteService.GetById(SiteId);
                    var GetDateTime = _siteService.GetDateTime(SiteData.TimeZone);
                    //var EmployeeId = _commonService.GetEmployeeIdByUserId(SiteId, LoggedUserId);
                    var EmployeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                    var latest = EmployeeId == null ? null : await _timeInTimeOutService.GetTimeInTimeOutByEmployeeId(SiteId, EmployeeId);
                    var refusal = TimeClockRules.RefuseTimeIn(EmployeeId, latest);
                    if (refusal != null)
                        return BadRequest(refusal);

                    var entity = new TimeInTimeOut
                    {
                        EmployeeId = EmployeeId,
                        SiteId = SiteId,
                        TimeIn = GetDateTime.TimeOfDay,        // current time in 24-hour format
                        TimeInDate = GetDateTime.Date,         // current date
                        CreatedById = LoggedUserId,
                        UpdatedById = LoggedUserId,
                        CreatedOnUtc = GetDateTime,
                        UpdatedOnUtc = GetDateTime
                    };
                    _timeInTimeOutService.InsertTimeInTimeOut(entity);

                    return Ok(await BuildTimeClockStatus(SiteId, EmployeeId));
                }
                // Return model state errors if the model state is not valid
                return ModelStateError(ModelState);
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }
        #endregion

        #region UpdateTimeInTimeOut
        // Title: UpdateTimeInTimeOut
        // Description: Times the signed-in employee out of their open session. A break still running is ended at the same moment, and the session stores its total hours, total break and actual (working) hours. AddLunch / AddDinner: the employee confirmed, when asked at the time-out, a lunch / dinner they had not logged; a full meal break is then added so the working hours are exact. Note: an optional message saved with the session.
        [HttpPut("{id}")]
        [SiteOwned(typeof(Vsky.Models.TimeInTimeOut))]
        public async Task<IActionResult> UpdateTimeOut(string id, TimeClockTimeOutModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var LoggedUserId = User.GetLoggedInUserId<string>();
                    var SiteId = _globalVariable.SiteId;
                    var SiteData = await _siteService.GetById(SiteId);
                    var GetDateTime = _siteService.GetDateTime(SiteData.TimeZone);
                    var EmployeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                    // Fetch the time in time out entity by its ID
                    var entity = await _timeInTimeOutService.GetTimeInTimeOutById(id);

                    // If no time in time out is found with the given ID, return a bad request with an error message
                    if (entity == null)
                        return BadRequest(new BadRequestError("No time in time out found with the specified id."));

                    // In time-in / time-out only mode there are no breaks to confirm either.
                    var addLunch = !OnlyTimeInTimeOut && (model?.AddLunch ?? false);
                    var addDinner = !OnlyTimeInTimeOut && (model?.AddDinner ?? false);

                    var refusal = TimeClockRules.RefuseTimeOut(entity, EmployeeId) ?? TimeClockRules.RefuseNote(model?.Note);
                    if (refusal != null)
                        return BadRequest(refusal);

                    // Nobody is on a break after they have left: a running break ends with the session.
                    var breakList = await _timeInTimeOutBreakDetailService.GetBreakInOutByTimeInOutId(entity.Id);
                    var openBreak = TimeClockRules.OpenBreakOf(breakList);
                    if (openBreak != null)
                    {
                        TimeClockRules.EndBreak(openBreak, GetDateTime);
                        openBreak.UpdatedById = LoggedUserId;
                        openBreak.UpdatedOnUtc = GetDateTime;
                        _timeInTimeOutBreakDetailService.UpdateTimeInTimeOutBreakDetail(openBreak);
                    }

                    // A lunch or dinner taken but never logged, confirmed in the time-out question. The rule
                    // itself declines a meal that is already on the session.
                    var dayBreaks = addLunch || addDinner ? await GetDayBreaks(SiteId, entity, breakList) : breakList;
                    foreach (var dinner in new[] { false, true })
                    {
                        // Not asked for, or the day already has its meal break (lunch or dinner, one a day).
                        if (!(dinner ? addDinner : addLunch)
                            || TimeClockRules.IsMealLogged(dayBreaks, dinner: false)
                            || TimeClockRules.IsMealLogged(dayBreaks, dinner: true))
                            continue;

                        var mealBreak = TimeClockRules.NewMealBreakAtTimeOut(entity, breakList, dinner, GetDateTime);
                        if (mealBreak == null)
                            continue;

                        mealBreak.CreatedById = LoggedUserId;
                        mealBreak.UpdatedById = LoggedUserId;
                        _timeInTimeOutBreakDetailService.InsertTimeInTimeOutBreakDetail(mealBreak);
                        breakList.Add(mealBreak);
                        // Lunch or dinner, never both.
                        break;
                    }

                    // Total = time in to time out, Actual = Total - breaks; each capped below 24 hours (SQL time).
                    var totals = TimeClockRules.TotalsAtTimeOut(entity, breakList, GetDateTime);

                    entity.TimeOut = GetDateTime.TimeOfDay;
                    entity.TimeOutDate = GetDateTime.Date;
                    entity.TotalHours = totals.TotalHours;
                    entity.TotalBreak = totals.TotalBreak;
                    entity.ActualHours = totals.ActualHours;
                    entity.TimeOutNote = TimeClockRules.CleanNote(model?.Note);
                    entity.UpdatedById = LoggedUserId;
                    entity.UpdatedOnUtc = GetDateTime;

                    _timeInTimeOutService.UpdateTimeInTimeOut(entity);

                    return Ok(await BuildTimeClockStatus(SiteId, entity.EmployeeId));
                }
                return ModelStateError(ModelState);
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }
        #endregion

        #region StartBreak
        // Title: StartBreak
        // Description: Starts a break in the signed-in employee's open session, now. Refused when not timed in, already timed out, already on a break, or when the session is not theirs.
        [HttpPost("{id}/break/start")]
        [SiteOwned(typeof(Vsky.Models.TimeInTimeOut))]
        public async Task<IActionResult> StartBreak(string id, TimeClockBreakStartModel model)
        {
            if (OnlyTimeInTimeOut) return BadRequest(TimeClockRules.BreaksTurnedOff);

            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var SiteData = await _siteService.GetById(SiteId);
                var GetDateTime = _siteService.GetDateTime(SiteData.TimeZone);
                var EmployeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                var entity = await _timeInTimeOutService.GetTimeInTimeOutById(id);
                var breakList = entity == null
                    ? new List<TimeInTimeOutBreakDetail>()
                    : await _timeInTimeOutBreakDetailService.GetBreakInOutByTimeInOutId(entity.Id);

                var refusal = TimeClockRules.RefuseBreakStart(entity, EmployeeId, breakList, model?.BreakReason, GetDateTime);
                // One lunch and one dinner a day, and at most two hours of gate pass, across the day's sessions.
                if (refusal == null)
                {
                    var dayBreaks = await GetDayBreaks(SiteId, entity, breakList);
                    refusal = TimeClockRules.RefuseSecondMeal(model?.BreakReason, dayBreaks)
                        ?? TimeClockRules.RefuseGatePass(model?.BreakReason, dayBreaks, GetDateTime, null);
                }
                if (refusal != null)
                    return BadRequest(refusal);

                // Lunch and dinner have no timer: the full 45 minutes are logged at once. Any other reason
                // starts a break that runs until it is ended.
                var dinner = TimeClockRules.IsDinnerReason(model?.BreakReason);
                var newBreak = dinner != null
                    ? TimeClockRules.NewMealBreak(entity.Id, dinner.Value, GetDateTime)
                    : new TimeInTimeOutBreakDetail
                    {
                        TimeInTimeOutId = entity.Id,
                        BreakOut = GetDateTime,
                        BreakReason = TimeClockRules.CleanReason(model?.BreakReason),
                        CreatedOnUtc = GetDateTime,
                        UpdatedOnUtc = GetDateTime
                    };
                newBreak.CreatedById = LoggedUserId;
                newBreak.UpdatedById = LoggedUserId;
                _timeInTimeOutBreakDetailService.InsertTimeInTimeOutBreakDetail(newBreak);

                return Ok(await BuildTimeClockStatus(SiteId, EmployeeId));
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }
        #endregion

        #region EndBreak
        // Title: EndBreak
        // Description: Ends the running break of the signed-in employee's open session, now, and stores its length.
        [HttpPut("{id}/break/end")]
        [SiteOwned(typeof(Vsky.Models.TimeInTimeOut))]
        public async Task<IActionResult> EndBreak(string id)
        {
            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var SiteData = await _siteService.GetById(SiteId);
                var GetDateTime = _siteService.GetDateTime(SiteData.TimeZone);
                var EmployeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                var entity = await _timeInTimeOutService.GetTimeInTimeOutById(id);
                var breakList = entity == null
                    ? new List<TimeInTimeOutBreakDetail>()
                    : await _timeInTimeOutBreakDetailService.GetBreakInOutByTimeInOutId(entity.Id);

                var refusal = TimeClockRules.RefuseBreakEnd(entity, EmployeeId, breakList);
                if (refusal != null)
                    return BadRequest(refusal);

                var openBreak = TimeClockRules.OpenBreakOf(breakList);
                TimeClockRules.EndBreak(openBreak, GetDateTime);
                openBreak.UpdatedById = LoggedUserId;
                openBreak.UpdatedOnUtc = GetDateTime;
                _timeInTimeOutBreakDetailService.UpdateTimeInTimeOutBreakDetail(openBreak);

                return Ok(await BuildTimeClockStatus(SiteId, EmployeeId));
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }
        #endregion

        #region AddPastBreak
        // Title: AddPastBreak
        // Description: Adds a break the signed-in employee took but forgot to clock, from its start and end clock times (site time). Allowed on their open session and on the one they timed out of, whose stored totals are then worked out again. Refused when the break falls outside the session or overlaps another break.
        [HttpPost("{id}/break/add")]
        [SiteOwned(typeof(Vsky.Models.TimeInTimeOut))]
        public async Task<IActionResult> AddPastBreak(string id, TimeClockPastBreakModel model)
        {
            if (OnlyTimeInTimeOut) return BadRequest(TimeClockRules.BreaksTurnedOff);

            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var SiteData = await _siteService.GetById(SiteId);
                var GetDateTime = _siteService.GetDateTime(SiteData.TimeZone);
                var EmployeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                var entity = await _timeInTimeOutService.GetTimeInTimeOutById(id);
                var breakList = entity == null
                    ? new List<TimeInTimeOutBreakDetail>()
                    : await _timeInTimeOutBreakDetailService.GetBreakInOutByTimeInOutId(entity.Id);

                var refusal = TimeClockRules.ResolvePastBreak(
                    entity, EmployeeId, breakList,
                    ParseClock(model?.StartTime), ParseClock(model?.EndTime), model?.BreakReason,
                    GetSiteTimeZone(), GetDateTime,
                    out var startUtc, out var endUtc);
                // One lunch and one dinner a day, and at most two hours of gate pass, across the day's sessions.
                if (refusal == null)
                {
                    var dayBreaks = await GetDayBreaks(SiteId, entity, breakList);
                    // Lunch, dinner and gate pass are stated amounts with their own way in, not breaks with times.
                    refusal = (TimeClockRules.IsDinnerReason(model?.BreakReason) != null ? TimeClockRules.MealNotABreak : null)
                        ?? TimeClockRules.RefuseGatePass(model?.BreakReason, dayBreaks, GetDateTime, endUtc - startUtc);
                }
                if (refusal != null)
                    return BadRequest(refusal);

                var pastBreak = TimeClockRules.NewPastBreak(entity.Id, startUtc, endUtc, model?.BreakReason, GetDateTime);
                pastBreak.CreatedById = LoggedUserId;
                pastBreak.UpdatedById = LoggedUserId;
                _timeInTimeOutBreakDetailService.InsertTimeInTimeOutBreakDetail(pastBreak);

                // A session already timed out has its totals stored: they now include this break.
                var timeOutUtc = TimeClockRules.TimeOutUtcOf(entity);
                if (timeOutUtc != null)
                {
                    breakList.Add(pastBreak);
                    var totals = TimeClockRules.TotalsAtTimeOut(entity, breakList, timeOutUtc.Value);
                    entity.TotalHours = totals.TotalHours;
                    entity.TotalBreak = totals.TotalBreak;
                    entity.ActualHours = totals.ActualHours;
                    entity.UpdatedById = LoggedUserId;
                    entity.UpdatedOnUtc = GetDateTime;
                    _timeInTimeOutService.UpdateTimeInTimeOut(entity);
                }

                return Ok(await BuildTimeClockStatus(SiteId, EmployeeId));
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }

        // "13:05" (24-hour, as a time field sends it) to a time of day; null when it is not one.
        private static TimeSpan? ParseClock(string text)
        {
            return TimeSpan.TryParseExact((text ?? "").Trim(), new[] { @"h\:mm", @"hh\:mm", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out var clock)
                && clock >= TimeSpan.Zero && clock < TimeSpan.FromDays(1)
                ? clock
                : (TimeSpan?)null;
        }
        #endregion

        #region Gate pass
        // Title: LogGatePass
        // Description: Logs a gate pass on the signed-in employee's open session for the minutes chosen (the slider on the card). It counts in full from now, comes off the working hours, and the day's gate passes may not pass two hours.
        [HttpPost("{id}/gate-pass")]
        [SiteOwned(typeof(Vsky.Models.TimeInTimeOut))]
        public async Task<IActionResult> LogGatePass(string id, TimeClockGatePassModel model)
        {
            return await SaveGatePass(id, null, model);
        }

        // Title: UpdateGatePass
        // Description: Changes the minutes (and purpose) of a gate pass already logged, within the same two hours a day.
        [HttpPut("{id}/gate-pass/{breakId}")]
        [SiteOwned(typeof(Vsky.Models.TimeInTimeOut))]
        public async Task<IActionResult> UpdateGatePass(string id, string breakId, TimeClockGatePassModel model)
        {
            return await SaveGatePass(id, breakId, model);
        }

        // Title: CancelGatePass
        // Description: Cancels a logged gate pass, lunch or dinner (the entries that have no timer) with a reason. It stays on the day as cancelled and counts 0 minutes.
        [HttpPut("{id}/gate-pass/{breakId}/cancel")]
        [HttpPut("{id}/break/{breakId}/cancel")]
        [SiteOwned(typeof(Vsky.Models.TimeInTimeOut))]
        public async Task<IActionResult> CancelGatePass(string id, string breakId, TimeClockBreakCancelModel model)
        {
            if (OnlyTimeInTimeOut) return BadRequest(TimeClockRules.BreaksTurnedOff);

            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var GetDateTime = _siteService.GetDateTime();
                var EmployeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                var entity = await _timeInTimeOutService.GetTimeInTimeOutById(id);
                var breakList = entity == null
                    ? new List<TimeInTimeOutBreakDetail>()
                    : await _timeInTimeOutBreakDetailService.GetBreakInOutByTimeInOutId(entity.Id);
                var gatePass = TimeClockRules.LoggedBreakOf(breakList, breakId);

                var refusal = TimeClockRules.RefuseGatePassCancel(entity, EmployeeId, gatePass, model?.CancelReason);
                if (refusal != null)
                    return BadRequest(refusal);

                TimeClockRules.CancelBreak(gatePass, model.CancelReason);
                gatePass.UpdatedById = LoggedUserId;
                gatePass.UpdatedOnUtc = GetDateTime;
                _timeInTimeOutBreakDetailService.UpdateTimeInTimeOutBreakDetail(gatePass);

                return Ok(await BuildTimeClockStatus(SiteId, EmployeeId));
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }

        // Logging a new gate pass (breakId null) and changing one share every check.
        [NonAction]
        private async Task<IActionResult> SaveGatePass(string id, string breakId, TimeClockGatePassModel model)
        {
            if (OnlyTimeInTimeOut) return BadRequest(TimeClockRules.BreaksTurnedOff);

            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var GetDateTime = _siteService.GetDateTime();
                var EmployeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                var entity = await _timeInTimeOutService.GetTimeInTimeOutById(id);
                var breakList = entity == null
                    ? new List<TimeInTimeOutBreakDetail>()
                    : await _timeInTimeOutBreakDetailService.GetBreakInOutByTimeInOutId(entity.Id);

                var gatePass = breakId == null ? null : TimeClockRules.GatePassOf(breakList, breakId);
                if (breakId != null && gatePass == null)
                    return BadRequest(TimeClockRules.GatePassNotFound);

                // Two hours a day, across the day's sessions.
                var dayBreaks = entity == null ? breakList : await GetDayBreaks(SiteId, entity, breakList);
                var refusal = TimeClockRules.RefuseGatePassLog(entity, EmployeeId, dayBreaks, model?.Minutes ?? 0, model?.Purpose, breakId, GetDateTime);
                if (refusal != null)
                    return BadRequest(refusal);

                if (gatePass == null)
                {
                    gatePass = TimeClockRules.NewGatePass(entity.Id, model.Minutes, model.Purpose, GetDateTime);
                    gatePass.CreatedById = LoggedUserId;
                    gatePass.UpdatedById = LoggedUserId;
                    _timeInTimeOutBreakDetailService.InsertTimeInTimeOutBreakDetail(gatePass);
                }
                else
                {
                    TimeClockRules.SetGatePass(gatePass, model.Minutes, model.Purpose);
                    gatePass.UpdatedById = LoggedUserId;
                    gatePass.UpdatedOnUtc = GetDateTime;
                    _timeInTimeOutBreakDetailService.UpdateTimeInTimeOutBreakDetail(gatePass);
                }

                return Ok(await BuildTimeClockStatus(SiteId, EmployeeId));
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }
        #endregion

        #region CancelBreak
        // Title: CancelBreak
        // Description: Cancels the running break of the signed-in employee's open session (started by mistake, called back to work). The break stays on the day as cancelled, with the reason given, and counts 0 minutes.
        [HttpPut("{id}/break/cancel")]
        [SiteOwned(typeof(Vsky.Models.TimeInTimeOut))]
        public async Task<IActionResult> CancelBreak(string id, TimeClockBreakCancelModel model)
        {
            if (OnlyTimeInTimeOut) return BadRequest(TimeClockRules.BreaksTurnedOff);

            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var SiteData = await _siteService.GetById(SiteId);
                var GetDateTime = _siteService.GetDateTime(SiteData.TimeZone);
                var EmployeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                var entity = await _timeInTimeOutService.GetTimeInTimeOutById(id);
                var breakList = entity == null
                    ? new List<TimeInTimeOutBreakDetail>()
                    : await _timeInTimeOutBreakDetailService.GetBreakInOutByTimeInOutId(entity.Id);

                var refusal = TimeClockRules.RefuseBreakCancel(entity, EmployeeId, breakList, model?.CancelReason);
                if (refusal != null)
                    return BadRequest(refusal);

                var openBreak = TimeClockRules.OpenBreakOf(breakList);
                TimeClockRules.CancelBreak(openBreak, model.CancelReason);
                openBreak.UpdatedById = LoggedUserId;
                openBreak.UpdatedOnUtc = GetDateTime;
                _timeInTimeOutBreakDetailService.UpdateTimeInTimeOutBreakDetail(openBreak);

                return Ok(await BuildTimeClockStatus(SiteId, EmployeeId));
            }
            catch (Exception ex)
            {
                return BadRequest(SafeErrorMessage(ex));
            }
        }
        #endregion

        #region Time clock status
        // The employee's latest session measured now, in the shape the dashboard card reads. Every time clock
        // action answers with this, so the card never has to guess what the server did.
        [NonAction]
        private async Task<TimeClockStatusModel> BuildTimeClockStatus(string siteId, string employeeId)
        {
            var nowUtc = _siteService.GetDateTime();
            var zone = GetSiteTimeZone();
            var status = new TimeClockStatusModel
            {
                State = TimeClockState.NotStarted.ToString(),
                HasEmployee = !string.IsNullOrEmpty(employeeId),
                OnlyTimeInTimeOut = OnlyTimeInTimeOut,
                LunchAllowanceSeconds = (int)TimeClockRules.LunchAllowance.TotalSeconds,
                BreakStandardSeconds = (int)TimeClockRules.BreakStandard.TotalSeconds,
                GatePassMaxSeconds = (int)TimeClockRules.GatePassMax.TotalSeconds,
                ServerNowMs = ToUnixMs(nowUtc)
            };

            if (!status.HasEmployee)
                return status;

            var session = await _timeInTimeOutService.GetTimeInTimeOutByEmployeeId(siteId, employeeId);
            if (session == null)
                return status;

            var breakList = await _timeInTimeOutBreakDetailService.GetBreakInOutByTimeInOutId(session.Id);
            var summary = TimeClockRules.Summarize(session, breakList, nowUtc);

            // A session timed out on an earlier day is not today's: the card starts the day empty.
            if (summary.State == TimeClockState.NotStarted ||
                (summary.TimeOutUtc != null && TimeClockRules.IsFromEarlierDay(ToSiteTime(summary.TimeOutUtc.Value, zone), ToSiteTime(nowUtc, zone))))
                return status;

            status.State = summary.State.ToString();
            status.SessionId = session.Id;
            status.IsOverdue = summary.IsOverdue;
            status.TimeInMs = ToUnixMs(summary.TimeInUtc.Value);
            status.TimeInDateText = DateText(summary.TimeInUtc.Value, zone);
            status.TimeInText = ClockText(summary.TimeInUtc.Value, zone);
            if (summary.TimeOutUtc != null)
            {
                status.TimeOutMs = ToUnixMs(summary.TimeOutUtc.Value);
                status.TimeOutDateText = DateText(summary.TimeOutUtc.Value, zone);
                status.TimeOutText = ClockText(summary.TimeOutUtc.Value, zone);
            }

            status.GrossSeconds = summary.GrossSeconds;
            status.BreakSeconds = summary.BreakSeconds;
            status.WorkedSeconds = summary.WorkedSeconds;
            status.LunchSeconds = summary.LunchSeconds;
            status.DinnerSeconds = summary.DinnerSeconds;
            status.LunchLogged = summary.LunchLogged;
            status.DinnerLogged = summary.DinnerLogged;

            // One lunch and one dinner a day: a meal taken in an earlier session of today is today's meal.
            var otherBreaks = (await GetDayBreaks(siteId, session, breakList)).Where(b => b.TimeInTimeOutId != session.Id).ToList();
            status.LunchLogged = status.LunchLogged || TimeClockRules.IsMealLogged(otherBreaks, dinner: false);
            status.DinnerLogged = status.DinnerLogged || TimeClockRules.IsMealLogged(otherBreaks, dinner: true);
            status.LunchSeconds += TimeClockRules.MealSeconds(otherBreaks, dinner: false);
            status.DinnerSeconds += TimeClockRules.MealSeconds(otherBreaks, dinner: true);

            // The break standard and the gate pass limit are per day too. BreakSeconds stays everything taken
            // off this session's working time (breaks and gate pass); these two split the day's total by kind.
            status.GatePassSeconds = summary.GatePassSeconds + TimeClockRules.AwaySeconds(otherBreaks, gatePass: true, nowUtc);
            status.BreakUsedSeconds = summary.BreakSeconds - summary.GatePassSeconds + TimeClockRules.AwaySeconds(otherBreaks, gatePass: false, nowUtc);
            status.GrossText = TimeClockRules.DurationText(summary.GrossSeconds);
            status.BreakText = TimeClockRules.DurationText(summary.BreakSeconds);
            status.WorkedText = TimeClockRules.DurationText(summary.WorkedSeconds);
            status.TimeOutNote = summary.TimeOutUtc == null ? null : session.TimeOutNote;

            await AddTimesheetReminders(status, siteId, employeeId, summary.TimeInUtc.Value, zone);

            status.Breaks = summary.Breaks.Select(b => new TimeClockBreakModel
            {
                Id = b.Id,
                Reason = b.Reason,
                StartMs = ToUnixMs(b.StartUtc),
                EndMs = b.EndUtc == null ? (long?)null : ToUnixMs(b.EndUtc.Value),
                StartText = ClockText(b.StartUtc, zone),
                EndText = b.EndUtc == null ? null : ClockText(b.EndUtc.Value, zone),
                Seconds = b.Seconds,
                IsOpen = b.IsOpen,
                IsCancelled = b.IsCancelled,
                CancelReason = b.CancelReason,
                IsLunch = b.IsLunch,
                IsDinner = b.IsDinner,
                IsGatePass = b.IsGatePass,
                IsAddedLater = b.IsAddedLater
            }).ToList();

            return status;
        }

        // Reminders only: whether the timesheet of the session's day is filled (a timesheet with hours on
        // it), and whether the one of the last day worked before it is. Nothing here stops a time-in or a time-out.
        private async Task AddTimesheetReminders(TimeClockStatusModel status, string siteId, string employeeId, DateTime timeInUtc, TimeZoneInfo zone)
        {
            var day = ToSiteTime(timeInUtc, zone).Date;
            // Only the days a timesheet can still be created for: this week and last.
            var windowStart = TimeClockRules.TimesheetWindowStart(day);

            // Two reads cover them: the days worked and the days with a filled timesheet.
            var sessions = await _timeInTimeOutService.GetTimeInTimeOutsByEmployeeBetween(siteId, employeeId, windowStart.AddDays(-1), day.AddDays(1));
            var filled = await _timesheetService.GetFilledTimesheetDates(siteId, employeeId, windowStart, day);

            status.TimesheetDateText = day.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
            status.TimesheetMissing = !filled.Contains(day);

            status.PendingTimesheets = TimeClockRules.DaysWithoutTimesheet(sessions, filled, windowStart, day, zone)
                .Select(d => new TimeClockPendingDayModel
                {
                    DayText = d.ToString("ddd", CultureInfo.InvariantCulture),
                    DateText = d.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
                })
                .ToList();
        }

        // The breaks of the whole day the session belongs to: its own, plus those of the employee's other
        // sessions timed in on the same site-local date ("Time In Again" makes a second session).
        private async Task<List<TimeInTimeOutBreakDetail>> GetDayBreaks(string siteId, TimeInTimeOut session, List<TimeInTimeOutBreakDetail> sessionBreaks)
        {
            var dayBreaks = new List<TimeInTimeOutBreakDetail>(sessionBreaks ?? new List<TimeInTimeOutBreakDetail>());
            var timeInUtc = TimeClockRules.TimeInUtcOf(session);
            if (timeInUtc == null || string.IsNullOrEmpty(session.EmployeeId))
                return dayBreaks;

            // The site-local day can begin or end on a neighbouring UTC date, so those are read as well.
            var zone = GetSiteTimeZone();
            var nearby = await _timeInTimeOutService.GetTimeInTimeOutsByEmployeeBetween(
                siteId, session.EmployeeId, timeInUtc.Value.Date.AddDays(-1), timeInUtc.Value.Date.AddDays(1));
            var otherIds = nearby
                .Where(other => other.Id != session.Id && TimeClockRules.IsSameDay(session, other, zone))
                .Select(other => other.Id)
                .ToList();

            dayBreaks.AddRange(await _timeInTimeOutBreakDetailService.GetBreaksByTimeInOutIds(otherIds));
            return dayBreaks;
        }

        // The site's time zone, the same way the date and time converters of the API read it.
        private TimeZoneInfo GetSiteTimeZone()
        {
            var timezone = string.IsNullOrEmpty(_globalVariable.TimeZone) ? "India Standard Time" : _globalVariable.TimeZone;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch (Exception)
            {
                return TimeZoneInfo.Utc;
            }
        }

        private static DateTime ToSiteTime(DateTime utc, TimeZoneInfo zone)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        }

        private static long ToUnixMs(DateTime utc)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        }

        private static string ClockText(DateTime utc, TimeZoneInfo zone)
        {
            return ToSiteTime(utc, zone).ToString("hh:mm tt", CultureInfo.InvariantCulture);
        }

        private static string DateText(DateTime utc, TimeZoneInfo zone)
        {
            return ToSiteTime(utc, zone).ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        }
        #endregion

        #region private functions
       

        private TimeSpan ParseTime(string timeOutStr)
        {

            if (string.IsNullOrWhiteSpace(timeOutStr))
                return TimeSpan.Zero;

            // Parse strictly as 12-hour format (e.g., "01:05 AM", "11:30 PM")
            if (DateTime.TryParseExact(timeOutStr, "hh:mm tt",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
            {
                // Extract only the time portion as 24-hour TimeSpan (no date info)
                return new TimeSpan(dateTime.Hour, dateTime.Minute, 0);
            }

            return TimeSpan.Zero;
        }

        #endregion

    }
}