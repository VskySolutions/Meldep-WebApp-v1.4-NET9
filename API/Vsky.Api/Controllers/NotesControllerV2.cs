using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Vsky.Api.Models;
using Vsky.Models;
using Vsky.Services.Note;

namespace Vsky.Api.Controllers
{
    // The chat-style notes thread (v2). Read-only: notes are still saved and deleted through NotesController.
    [Route("notes-v2")]
    [SiteMember]
    public class NotesControllerV2 : BaseController
    {
        private readonly GlobalVariable _globalVariable;
        private readonly IMapper _mapper;
        private readonly INoteServiceV2 _noteServiceV2;

        public NotesControllerV2(GlobalVariable globalVariable, IMapper mapper, INoteServiceV2 noteServiceV2)
        {
            _globalVariable = globalVariable;
            _mapper = mapper;
            _noteServiceV2 = noteServiceV2;
        }

        #region GetNoteThread
        // Title: Get Note Thread
        // Description: One page of a record's notes read like a chat (the newest `take` notes, or those older
        // than `beforeId`), with the per-type counts the filter chips show.
        [HttpGet("thread")]
        public IActionResult GetNoteThread(string subModuleId, string type, string noteTypeId = null, string beforeId = null, int take = 10)
        {
            try
            {
                var SiteId = _globalVariable.SiteId;
                var thread = _noteServiceV2.GetNoteThread(SiteId, subModuleId, type, noteTypeId, beforeId, take);
                var model = new NoteThreadModelV2
                {
                    Data = _mapper.Map<List<NoteModel>>(thread.Items),
                    HasMore = thread.HasMore,
                    Total = thread.Total,
                    CountsByType = thread.CountsByType
                };
                return Ok(model);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        #endregion
    }
}
